#!/usr/bin/env python3
"""Validate the retired identity-locked family walk source set.

FC-WALK-GUARDRAIL-V1 deliberately has one production path. The tracked 256px source frames and
their separate anatomy-marker review copies are the source of truth. This tool can only validate
that source, copy it to the stable Unity runtime paths, and assemble the two 4x6 sheets. The old
V4/V5/V6/V7 import, joint-rig, strip-rotation and normalization modes were removed because whichever
mode ran last silently changed the tracked art generation.

Character Locomotion Generation V1 now owns every shipping walk PNG.  This module remains readable
for provenance and marker audits, but it must never overwrite the shared 12-character runtime set.
`--check` delegates to the current fail-closed runtime gate; `--write` prints the canonical command
and exits nonzero.
"""

from __future__ import annotations

import argparse
import hashlib
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps
from PIL.PngImagePlugin import PngInfo


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPO_ROOT / "ArtSources" / "FamilyWalkHalfCyclesV2"
MARKER_ROOT = SOURCE_ROOT / "MarkerReviewV1"
IDENTITY_ROOT = SOURCE_ROOT / "IdentityModelV1"
TARGET_SIZE = 256
GROUND_Y = 247
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"
MIN_OPPOSITE_PIXEL_CHANGE = 0.02
MIN_OPPOSITE_LOWER_BODY_CHANGE = 0.07
MIN_ADJACENT_LOWER_BODY_CHANGE = 0.02
MIN_MEAN_ADJACENT_LOWER_BODY_CHANGE = 0.04
PIXEL_CHANGE_TOLERANCE = 24

DIRECTIONS_A = ("south", "southwest", "west", "northwest")
DIRECTIONS_B = ("north", "northeast", "east", "southeast")
DIRECTIONS = DIRECTIONS_A + DIRECTIONS_B
MIRROR_PAIRS = (
    ("west", "east"),
    ("southwest", "southeast"),
    ("northeast", "northwest"),
)
AUTHORED_DIRECTIONS = ("south", "southeast", "east", "northeast", "north")
V5_LOWER_BODY_START = {
    "player": 0.72,
    "older_sister": 0.66,
    "father": 0.64,
    "mother": 0.70,
}


@dataclass(frozen=True)
class Character:
    member_id: str
    folder: str
    prefix: str
    target_height: int

    @property
    def high_motion_root(self) -> Path:
        return (
            REPO_ROOT
            / "Assets"
            / "Art"
            / "Characters"
            / self.folder
            / "Pixel"
            / "HighMotion"
        )

    @property
    def frames_root(self) -> Path:
        return self.high_motion_root / "Frames"

    def runtime_frame(self, direction: str, phase: int) -> Path:
        return self.frames_root / f"{self.prefix}_{direction}_walk_{phase}.png"

    def source_frame(self, direction: str, phase: int) -> Path:
        return (
            SOURCE_ROOT
            / self.member_id
            / direction
            / f"{self.prefix}_{direction}_half_{phase}.png"
        )

    def marker_frame(self, direction: str, phase: int) -> Path:
        return MARKER_ROOT / f"{self.prefix}_{direction}_walk_{phase}.png"

    def identity_anchor(self, direction: str) -> Path:
        return IDENTITY_ROOT / f"{self.prefix}_{direction}_identity_anchor.png"

    def sheet_path(self, suffix: str) -> Path:
        return self.high_motion_root / f"{self.prefix}_pixel_walk8dir6_{suffix}_v1.png"


CHARACTERS = (
    Character("player", "Player", "player", 217),
    Character("older_sister", "OlderSister", "older_sister", 204),
    Character("father", "Father", "father", 228),
    Character("mother", "Mother", "mother", 225),
)
CHARACTER_BY_ID = {character.member_id: character for character in CHARACTERS}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def hard_alpha(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA")).copy()
    rgba[:, :, 3] = np.where(rgba[:, :, 3] >= 128, 255, 0).astype(np.uint8)
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba, mode="RGBA")


def load_source(character: Character, direction: str, phase: int) -> Image.Image:
    return hard_alpha(Image.open(character.source_frame(direction, phase)))


def load_marker(character: Character, direction: str, phase: int) -> Image.Image:
    return hard_alpha(Image.open(character.marker_frame(direction, phase)))


def derived_frames(character: Character, direction: str) -> list[Image.Image]:
    return [load_source(character, direction, phase) for phase in range(6)]


def visible_pixel_change(
    first: Image.Image,
    second: Image.Image,
    lower_body_only: bool = False,
) -> float:
    a = np.asarray(first.convert("RGBA")).astype(np.int16)
    b = np.asarray(second.convert("RGBA")).astype(np.int16)
    visible = (a[:, :, 3] > 0) | (b[:, :, 3] > 0)
    if lower_body_only:
        visible[: int(TARGET_SIZE * 0.64)] = False
    changed = visible & (
        np.max(np.abs(a[:, :, :3] - b[:, :, :3]), axis=2) > PIXEL_CHANGE_TOLERANCE
    )
    return 0.0 if not visible.any() else float(changed.sum() / visible.sum())


def marker_masks(image: Image.Image) -> tuple[np.ndarray, np.ndarray]:
    rgba = np.asarray(image.convert("RGBA"))
    rgb = rgba[:, :, :3].astype(np.int16)
    alpha = rgba[:, :, 3] > 0
    cyan = alpha & (rgb[:, :, 0] <= 40) & (rgb[:, :, 1] >= 180) & (rgb[:, :, 2] >= 180)
    magenta = alpha & (rgb[:, :, 0] >= 180) & (rgb[:, :, 1] <= 40) & (rgb[:, :, 2] >= 110)
    return cyan, magenta


def validate_sources() -> list[str]:
    failures: list[str] = []
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            row = f"{character.member_id}/{direction}"
            try:
                frames = derived_frames(character, direction)
                markers = [load_marker(character, direction, phase) for phase in range(6)]
            except (FileNotFoundError, OSError) as error:
                failures.append(f"{row}:missing:{error}")
                continue
            if len({frame.tobytes() for frame in frames}) != 6:
                failures.append(f"{row}:not-six-unique")
            first_upper: bytes | None = None
            for phase, (frame, marker) in enumerate(zip(frames, markers)):
                alpha = np.asarray(frame.getchannel("A"))
                bbox = frame.getchannel("A").getbbox()
                if frame.size != (TARGET_SIZE, TARGET_SIZE) or frame.mode != "RGBA":
                    failures.append(f"{row}/{phase}:format")
                    continue
                if set(np.unique(alpha)) - {0, 255}:
                    failures.append(f"{row}/{phase}:soft-alpha")
                if bbox is None or bbox[3] - 1 != GROUND_Y:
                    failures.append(f"{row}/{phase}:ground")
                elif bbox[3] - bbox[1] != character.target_height:
                    failures.append(f"{row}/{phase}:height")
                if frame.getchannel("A").tobytes() != marker.getchannel("A").tobytes():
                    failures.append(f"{row}/{phase}:marker-silhouette")
                cyan, magenta = marker_masks(marker)
                if int(cyan.sum()) < 30 or int(magenta.sum()) < 30:
                    failures.append(f"{row}/{phase}:marker-identity")
                upper_end = round(
                    (bbox[1] if bbox else 0)
                    + character.target_height * V5_LOWER_BODY_START[character.member_id]
                )
                upper = frame.crop((0, 0, TARGET_SIZE, upper_end)).tobytes()
                if first_upper is None:
                    first_upper = upper
                elif upper != first_upper:
                    failures.append(f"{row}/{phase}:upper-body-drift")
            anchor_path = character.identity_anchor(direction)
            if not anchor_path.exists():
                failures.append(f"{row}:identity-anchor-missing")
    for character in CHARACTERS:
        for source_direction, mirrored_direction in MIRROR_PAIRS:
            for phase in range(6):
                expected = ImageOps.mirror(load_source(character, source_direction, phase))
                actual = load_source(character, mirrored_direction, phase)
                if expected.tobytes() != actual.tobytes():
                    failures.append(
                        f"{character.member_id}/{source_direction}->{mirrored_direction}/{phase}:not-mirror"
                    )
    return failures


def run_twostep_gate() -> None:
    command = [
        "powershell",
        "-NoLogo",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(REPO_ROOT / "Tools" / "Verify-FamilyWalkTwoStep.ps1"),
        "-ProjectRoot",
        str(REPO_ROOT),
        "-Source",
        "artsources",
        "-MarkerDirectory",
        str(MARKER_ROOT),
    ]
    result = subprocess.run(command, cwd=REPO_ROOT, text=True, capture_output=True)
    print(result.stdout, end="")
    if result.returncode != 0:
        print(result.stderr, end="")
        raise SystemExit("FC-WALK-TWOSTEP-GATE-V1 failed; refusing to publish")


def build_sheets(character: Character, rows: tuple[str, ...]) -> Image.Image:
    sheet = Image.new("RGBA", (TARGET_SIZE * 6, TARGET_SIZE * 4), (0, 0, 0, 0))
    for row, direction in enumerate(rows):
        for phase, frame in enumerate(derived_frames(character, direction)):
            sheet.paste(frame, (phase * TARGET_SIZE, row * TARGET_SIZE))
    return sheet


def write_outputs() -> None:
    raise SystemExit(
        "FAMILY_WALK_V2_PUBLISH_RETIRED: use "
        "`py -3 Tools/generate_character_locomotion_v1.py --write`; "
        "the V2 source is provenance only and cannot overwrite shipping PNGs"
    )


def check_outputs() -> None:
    command = [sys.executable, str(REPO_ROOT / "Tools" / "verify_character_locomotion_v1.py")]
    result = subprocess.run(command, cwd=REPO_ROOT, text=True, capture_output=True, timeout=120)
    print(result.stdout, end="")
    if result.returncode != 0:
        print(result.stderr, end="")
        raise SystemExit("FC-CHARACTER-LOCOMOTION-QA-V1 failed")
    print("FAMILY_WALK_V2_CHECK_DELEGATED: PASS | owner=CharacterLocomotionGenerationV1")


def main() -> int:
    parser = argparse.ArgumentParser()
    operations = parser.add_mutually_exclusive_group(required=True)
    operations.add_argument("--validate-sources", action="store_true")
    operations.add_argument("--write", action="store_true")
    operations.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.validate_sources:
        failures = validate_sources()
        if failures:
            raise SystemExit("source contract failures: " + ", ".join(failures))
        run_twostep_gate()
        print("FAMILY_WALK_IDENTITY_LOCKED_SOURCES: PASS | rows=32 frames=192 markers=192")
    elif args.write:
        write_outputs()
    else:
        check_outputs()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
