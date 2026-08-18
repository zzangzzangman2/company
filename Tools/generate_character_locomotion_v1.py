#!/usr/bin/env python3
"""Build the family 4-character, 8-direction, 6-phase walk candidate.

The repository's approved pre-coherence six-pose sheets are the full-body motion
authority.  Committed V1 identity anchors preserve the stable head/hat only for
profiles that need it; no waist splice is permitted.  Generated runtime output is
never reused as generation input.

Default execution is non-destructive and writes a review candidate under
Artifacts/CharacterLocomotionGenerationV1.  ``--write`` is deliberately separate
and publishes only an already generated candidate while preserving every .meta.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from PIL.PngImagePlugin import PngInfo

from split_high_motion_sheets import extract_aligned_frames


REPO_ROOT = Path(__file__).resolve().parents[1]
PROFILE_PATH = REPO_ROOT / "Tools" / "character_locomotion_profiles_v1.json"
DONOR_ROOT = REPO_ROOT / "Assets" / "Art" / "Characters" / "BeforeCoherenceV1"
IDENTITY_ROOT = REPO_ROOT / "Tools" / "CharacterLocomotionIdentityV1"
DEFAULT_OUTPUT = REPO_ROOT / "Artifacts" / "CharacterLocomotionGenerationV1"
FRAME_SIZE = 256
PHASE_COUNT = 6
GROUND_Y = 247
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"
DIRECTIONS_A = ("south", "southwest", "west", "northwest")
DIRECTIONS_B = ("north", "northeast", "east", "southeast")
DIRECTIONS = DIRECTIONS_A + DIRECTIONS_B


@dataclass(frozen=True)
class Character:
    character_id: str
    asset_folder: str

    @property
    def high_motion_root(self) -> Path:
        return REPO_ROOT / "Assets" / "Art" / "Characters" / self.asset_folder / "Pixel" / "HighMotion"

    @property
    def frame_root(self) -> Path:
        return self.high_motion_root / "Frames"

    @property
    def donor_root(self) -> Path:
        return DONOR_ROOT / self.character_id

    def frame_path(self, direction: str, phase: int) -> Path:
        return self.frame_root / f"{self.character_id}_{direction}_walk_{phase}.png"

    def sheet_path(self, part: str) -> Path:
        return self.high_motion_root / f"{self.character_id}_pixel_walk8dir6_{part}_v1.png"

    def donor_sheet_path(self, part: str) -> Path:
        return self.donor_root / f"{self.character_id}_pixel_walk8dir6_{part}_v1.png"

    def identity_path(self, direction: str) -> Path:
        return IDENTITY_ROOT / self.character_id / f"{self.character_id}_{direction}_identity_v1.png"


CHARACTERS = (
    Character("player", "Player"),
    Character("older_sister", "OlderSister"),
    Character("father", "Father"),
    Character("mother", "Mother"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--write", action="store_true", help="Publish the generated candidate to stable runtime PNG paths.")
    parser.add_argument("--publish-existing", action="store_true", help="Publish an already generated candidate without rebuilding it.")
    return parser.parse_args()


def hard_alpha(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA")).copy()
    visible = rgba[:, :, 3] >= 128
    rgba[:, :, 3] = np.where(visible, 255, 0).astype(np.uint8)
    rgba[~visible, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def remove_tiny_islands(image: Image.Image, minimum_pixels: int = 6) -> Image.Image:
    rgba = np.asarray(hard_alpha(image), dtype=np.uint8).copy()
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        (rgba[:, :, 3] > 0).astype(np.uint8), 8
    )
    for index in range(1, count):
        if int(stats[index, cv2.CC_STAT_AREA]) < minimum_pixels:
            rgba[labels == index] = 0
    return Image.fromarray(rgba, "RGBA")


def remove_ground_shadow_islands(image: Image.Image) -> Image.Image:
    """Remove detached one-to-three-pixel ground streaks before foot normalization.

    Several approved source sheets contain a thin magenta/brown shadow below the real
    shoe.  Treating that streak as anatomy shifts the whole sprite upward, clips hair or
    headwear at y=0, and leaves the visible feet floating.  A real foot has vertical mass;
    only detached, very shallow components near the source bottom are removed here.
    """

    rgba = np.asarray(hard_alpha(image), dtype=np.uint8).copy()
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        (rgba[:, :, 3] > 0).astype(np.uint8), 8
    )
    for index in range(1, count):
        _, y, width, height, area = (int(value) for value in stats[index])
        if y >= GROUND_Y - 12 and height <= 3 and width >= 3 and area <= 80:
            rgba[labels == index] = 0
    return Image.fromarray(rgba, "RGBA")


def alpha_bounds(array: np.ndarray) -> tuple[int, int, int, int]:
    rows, columns = np.nonzero(array[:, :, 3] > 0)
    if not len(columns):
        raise ValueError("empty character frame")
    return int(columns.min()), int(rows.min()), int(columns.max()), int(rows.max())


def load_profiles() -> dict[str, dict[str, float | int]]:
    payload = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != 1:
        raise ValueError(f"unsupported locomotion profile schema: {payload.get('schemaVersion')}")
    profiles = payload.get("characters", {})
    expected = {character.character_id for character in CHARACTERS}
    if not expected.issubset(profiles):
        raise ValueError(f"profile character mismatch: missing={sorted(expected - set(profiles))}")
    return profiles


def load_donor_rows(character: Character) -> dict[str, list[Image.Image]]:
    result: dict[str, list[Image.Image]] = {}
    for part, directions in (("a", DIRECTIONS_A), ("b", DIRECTIONS_B)):
        sheet_path = character.donor_sheet_path(part)
        if not sheet_path.is_file():
            raise FileNotFoundError(sheet_path)
        with Image.open(sheet_path) as loaded:
            sheet = hard_alpha(loaded)
        rows = extract_aligned_frames(sheet, sheet_path)
        for row, direction in enumerate(directions):
            result[direction] = [
                normalize_ground(remove_ground_shadow_islands(remove_tiny_islands(frame)))
                for frame in rows[row]
            ]
    return result


def normalize_ground(image: Image.Image) -> Image.Image:
    array = np.asarray(image.convert("RGBA"))
    _, _, _, bottom = alpha_bounds(array)
    dy = GROUND_Y - bottom
    if dy == 0:
        return hard_alpha(image)
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, (0, dy))
    return hard_alpha(canvas)


def shift_layer(layer: np.ndarray, dx: int, dy: int) -> np.ndarray:
    shifted = np.zeros_like(layer)
    height, width = layer.shape[:2]
    source_x0 = max(0, -dx)
    source_x1 = min(width, width - dx)
    source_y0 = max(0, -dy)
    source_y1 = min(height, height - dy)
    if source_x1 <= source_x0 or source_y1 <= source_y0:
        return shifted
    shifted[
        source_y0 + dy : source_y1 + dy,
        source_x0 + dx : source_x1 + dx,
    ] = layer[source_y0:source_y1, source_x0:source_x1]
    return shifted


def build_direction(
    character: Character,
    direction: str,
    donor_images: list[Image.Image],
    profile: dict[str, float | int],
) -> tuple[list[Image.Image], dict[str, object]]:
    with Image.open(character.identity_path(direction)) as loaded:
        identity = np.asarray(hard_alpha(loaded), dtype=np.uint8)
    donor_arrays = [np.asarray(hard_alpha(image), dtype=np.uint8) for image in donor_images]
    _, top, _, bottom = alpha_bounds(identity)
    lower_fraction = float(profile["lowerBodyStart"])
    seam_y = int(round(top + (bottom - top + 1) * lower_fraction))
    identity_head_fraction = float(profile.get("identityHeadFraction", 0.0))
    head_underlap = int(profile.get("identityHeadUnderlapPx", 10))
    body_drop_by_phase = (0, 1, 0, 0, 1, 0)
    head_cut = (
        int(round(top + (bottom - top + 1) * identity_head_fraction))
        if identity_head_fraction > 0.0
        else 0
    )

    generated_arrays: list[np.ndarray] = []
    yy, _ = np.indices((FRAME_SIZE, FRAME_SIZE))
    for phase, donor in enumerate(donor_arrays):
        output = donor.copy()
        if head_cut:
            body_drop = body_drop_by_phase[phase]
            expected_identity = shift_layer(identity, 0, body_drop)
            clear_end = min(FRAME_SIZE, head_cut + body_drop)
            output[:clear_end] = 0
            identity_visible = (
                (expected_identity[:, :, 3] > 0)
                & (yy < min(FRAME_SIZE, clear_end + head_underlap))
            )
            output[identity_visible] = expected_identity[identity_visible]
        generated_arrays.append(output)

    outputs: list[Image.Image] = []
    for phase, output in enumerate(generated_arrays):
        output = output.copy()
        output[GROUND_Y + 1 :] = 0
        cleaned = np.asarray(
            remove_ground_shadow_islands(remove_tiny_islands(Image.fromarray(output, "RGBA"))),
            dtype=np.uint8,
        ).copy()
        protected_end = min(FRAME_SIZE, head_cut + body_drop_by_phase[phase]) if head_cut else 0
        if protected_end:
            cleaned[:protected_end] = output[:protected_end]
        output = cleaned
        _, _, _, output_bottom = alpha_bounds(output)
        if output_bottom != GROUND_Y:
            raise ValueError(
                f"{character.character_id}/{direction}/{phase}: generated ground={output_bottom}, expected={GROUND_Y}"
            )
        outputs.append(Image.fromarray(output, "RGBA"))

    if head_cut:
        for phase, output in enumerate(outputs):
            expected = shift_layer(identity, 0, body_drop_by_phase[phase])
            clear_end = min(FRAME_SIZE, head_cut + body_drop_by_phase[phase])
            if np.asarray(output)[:clear_end].tobytes() != expected[:clear_end].tobytes():
                raise AssertionError(f"{character.character_id}/{direction}/{phase}: head identity drift")
    if len({image.tobytes() for image in outputs}) != PHASE_COUNT:
        raise ValueError(f"{character.character_id}/{direction}: generated frames are not six unique poses")

    report = {
        "seamY": seam_y,
        "composition": "authored-full-body",
        "identityHeadFraction": identity_head_fraction,
        "identityHeadRows": head_cut,
        "identityHeadUnderlapPx": head_underlap if head_cut else 0,
        "rigidBodyDropPx": list(body_drop_by_phase),
        "frameSha256": [hashlib.sha256(image.tobytes()).hexdigest().upper() for image in outputs],
    }
    return outputs, report


def candidate_frame_path(output: Path, character: Character, direction: str, phase: int) -> Path:
    return output / "Candidate" / character.character_id / "Frames" / f"{character.character_id}_{direction}_walk_{phase}.png"


def candidate_sheet_path(output: Path, character: Character, part: str) -> Path:
    return output / "Candidate" / character.character_id / f"{character.character_id}_pixel_walk8dir6_{part}_v1.png"


def build_sheet(frames: dict[str, list[Image.Image]], directions: tuple[str, ...]) -> Image.Image:
    sheet = Image.new("RGBA", (FRAME_SIZE * PHASE_COUNT, FRAME_SIZE * len(directions)), (0, 0, 0, 0))
    for row, direction in enumerate(directions):
        for phase, frame in enumerate(frames[direction]):
            sheet.paste(frame, (phase * FRAME_SIZE, row * FRAME_SIZE))
    return sheet


def save_candidate(output: Path) -> dict[str, object]:
    profiles = load_profiles()
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, object] = {"schemaVersion": 1, "characters": {}}
    all_frames: dict[str, dict[str, list[Image.Image]]] = {}
    for character in CHARACTERS:
        donors = load_donor_rows(character)
        character_frames: dict[str, list[Image.Image]] = {}
        character_report: dict[str, object] = {}
        for direction in DIRECTIONS:
            frames, direction_report = build_direction(
                character, direction, donors[direction], profiles[character.character_id]
            )
            character_frames[direction] = frames
            character_report[direction] = direction_report
            for phase, frame in enumerate(frames):
                path = candidate_frame_path(output, character, direction, phase)
                path.parent.mkdir(parents=True, exist_ok=True)
                frame.save(path, format="PNG", compress_level=9)
        for part, directions in (("a", DIRECTIONS_A), ("b", DIRECTIONS_B)):
            metadata = PngInfo()
            metadata.add_text(LAYOUT_METADATA_KEY, GRID_LAYOUT_MARKER)
            path = candidate_sheet_path(output, character, part)
            build_sheet(character_frames, directions).save(path, format="PNG", compress_level=9, pnginfo=metadata)
        all_frames[character.character_id] = character_frames
        report["characters"][character.character_id] = character_report
        print(f"BUILT {character.character_id}: directions=8 frames=48")
    render_contact_sheets(output, all_frames)
    (output / "generation-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n"
    )
    return report


def font(size: int) -> ImageFont.ImageFont:
    for path in (
        Path("C:/Windows/Fonts/malgun.ttf"),
        Path("C:/Windows/Fonts/arial.ttf"),
    ):
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def render_contact_sheets(
    output: Path,
    all_frames: dict[str, dict[str, list[Image.Image]]],
) -> None:
    evidence = output / "Evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    tile = 112
    label_height = 28
    header_width = 132
    face = font(15)
    small = font(12)
    for direction in DIRECTIONS:
        canvas = Image.new(
            "RGB",
            (header_width + tile * PHASE_COUNT, label_height + tile * len(CHARACTERS)),
            (28, 33, 44),
        )
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 6), direction.upper(), font=face, fill=(245, 245, 245))
        for phase in range(PHASE_COUNT):
            draw.text((header_width + phase * tile + 40, 6), f"P{phase}", font=face, fill=(245, 245, 245))
        for row, character in enumerate(CHARACTERS):
            y = label_height + row * tile
            draw.text((8, y + 45), character.character_id, font=small, fill=(235, 235, 235))
            for phase, frame in enumerate(all_frames[character.character_id][direction]):
                panel = Image.new("RGBA", (tile, tile), (239, 232, 219, 255))
                sprite = frame.resize((tile, tile), Image.Resampling.NEAREST)
                panel.alpha_composite(sprite)
                canvas.paste(panel.convert("RGB"), (header_width + phase * tile, y))
        canvas.save(evidence / f"all_characters_{direction}_6phase_contact_v1.png", quality=95)

    # Runtime-like loop evidence.  These are derived from the exact candidate frames, not a second
    # animation source, and make contact/support/pass timing visible without opening 576 files.
    player = next(character for character in CHARACTERS if character.character_id == "player")
    for direction in DIRECTIONS:
        gif_frames: list[Image.Image] = []
        for phase, frame in enumerate(all_frames[player.character_id][direction]):
            panel = Image.new("RGB", (384, 420), (239, 232, 219))
            sprite = frame.resize((384, 384), Image.Resampling.NEAREST)
            panel.paste(sprite, (0, 28), sprite)
            draw = ImageDraw.Draw(panel)
            draw.text((10, 6), f"PLAYER {direction.upper()}  P{phase}", font=face, fill=(28, 33, 44))
            gif_frames.append(panel)
        gif_frames[0].save(
            evidence / f"player_{direction}_walk_6phase_v1.gif",
            save_all=True,
            append_images=gif_frames[1:],
            duration=165,
            loop=0,
            disposal=2,
        )

    east_frames: list[Image.Image] = []
    for phase in range(PHASE_COUNT):
        canvas = Image.new("RGB", (356, 76 + 112 * len(CHARACTERS)), (28, 33, 44))
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 8), f"ALL CHARACTERS EAST  P{phase}", font=face, fill=(245, 245, 245))
        for row, character in enumerate(CHARACTERS):
            y = 48 + row * 112
            draw.text((8, y + 45), character.character_id, font=small, fill=(235, 235, 235))
            panel = Image.new("RGBA", (112, 112), (239, 232, 219, 255))
            sprite = all_frames[character.character_id]["east"][phase].resize(
                (112, 112), Image.Resampling.NEAREST
            )
            panel.alpha_composite(sprite)
            canvas.paste(panel.convert("RGB"), (236, y))
        east_frames.append(canvas)
    east_frames[0].save(
        evidence / "all_characters_east_walk_6phase_v1.gif",
        save_all=True,
        append_images=east_frames[1:],
        duration=165,
        loop=0,
        disposal=2,
    )


def publish(output: Path) -> None:
    backup = output / "BeforePublish"
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase in range(PHASE_COUNT):
                source = candidate_frame_path(output, character, direction, phase)
                destination = character.frame_path(direction, phase)
                if not source.is_file():
                    raise FileNotFoundError(source)
                backup_path = backup / destination.relative_to(REPO_ROOT)
                backup_path.parent.mkdir(parents=True, exist_ok=True)
                if not backup_path.exists():
                    shutil.copyfile(destination, backup_path)
                shutil.copyfile(source, destination)
        for part in ("a", "b"):
            source = candidate_sheet_path(output, character, part)
            destination = character.sheet_path(part)
            backup_path = backup / destination.relative_to(REPO_ROOT)
            backup_path.parent.mkdir(parents=True, exist_ok=True)
            if not backup_path.exists():
                shutil.copyfile(destination, backup_path)
            shutil.copyfile(source, destination)
        print(f"PUBLISHED {character.character_id}: preserved existing .meta files")


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    if not args.publish_existing:
        save_candidate(output)
    if args.write or args.publish_existing:
        publish(output)
    print(f"CHARACTER_LOCOMOTION_GENERATION_V1: CANDIDATE_READY output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
