#!/usr/bin/env python3
"""Build every family walk row from an authored three-pose half-cycle.

The tracked source of truth is ArtSources/FamilyWalkHalfCyclesV2.  North and
south are self-mirrored.  Lateral/diagonal rows use the authored half-cycle of
the opposite-facing row for phases 3..5, mirrored horizontally.  That keeps
the displayed facing intact while making the opposite gait mechanically exact.

Writing runtime assets is deliberately gated: all 32 rows must reach at least
30 percent silhouette change between phase 0 and the derived phase 3 first.
"""

from __future__ import annotations

import argparse
import hashlib
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageOps
from PIL.PngImagePlugin import PngInfo


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPO_ROOT / "ArtSources" / "FamilyWalkHalfCyclesV2"
TARGET_SIZE = 256
GROUND_Y = 248
MIN_OPPOSITE_CHANGE = 0.30
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"

DIRECTIONS_A = ("south", "southwest", "west", "northwest")
DIRECTIONS_B = ("north", "northeast", "east", "southeast")
DIRECTIONS = DIRECTIONS_A + DIRECTIONS_B
MIRROR_SOURCE = {
    "north": "north",
    "south": "south",
    "northeast": "northwest",
    "northwest": "northeast",
    "east": "west",
    "west": "east",
    "southeast": "southwest",
    "southwest": "southeast",
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
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest().upper()


def hard_alpha(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgba.putalpha(rgba.getchannel("A").point(lambda value: 255 if value >= 128 else 0))
    return rgba


def remove_green_chroma(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgb = np.asarray(rgba)[..., :3].astype(np.int16)
    red, green, blue = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    chroma = (green >= 90) & ((green - red) >= 36) & ((green - blue) >= 30)
    original_alpha = np.asarray(rgba.getchannel("A"))
    alpha = ((~chroma) & (original_alpha >= 32)).astype(np.uint8) * 255
    rgba.putalpha(Image.fromarray(alpha, mode="L"))
    bbox = rgba.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("green key removed the entire sprite")
    return rgba.crop(bbox)


def normalize_sprite(sprite: Image.Image, target_height: int) -> Image.Image:
    sprite = hard_alpha(sprite)
    bbox = sprite.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("cannot normalize an empty sprite")
    sprite = sprite.crop(bbox)
    scale = target_height / sprite.height
    target_width = max(1, round(sprite.width * scale))
    if target_width > TARGET_SIZE:
        raise ValueError(
            f"normalized sprite would be {target_width}px wide; expected <= {TARGET_SIZE}"
        )
    sprite = sprite.resize((target_width, target_height), Image.Resampling.NEAREST)
    sprite = hard_alpha(sprite)
    canvas = Image.new("RGBA", (TARGET_SIZE, TARGET_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(
        sprite,
        ((TARGET_SIZE - target_width) // 2, GROUND_Y - target_height),
    )
    return canvas


def load_source(character: Character, direction: str, phase: int) -> Image.Image:
    path = character.source_frame(direction, phase)
    if not path.is_file():
        raise FileNotFoundError(f"missing half-cycle source: {path}")
    image = hard_alpha(Image.open(path))
    if image.size != (TARGET_SIZE, TARGET_SIZE):
        raise ValueError(f"source must be 256x256 RGBA: {path} ({image.size})")
    return image


def derived_frames(character: Character, direction: str) -> list[Image.Image]:
    first_half = [load_source(character, direction, phase) for phase in range(3)]
    mirror_direction = MIRROR_SOURCE[direction]
    opposite_half = [
        ImageOps.mirror(load_source(character, mirror_direction, phase))
        for phase in range(3)
    ]
    return first_half + opposite_half


def silhouette_change(left: Image.Image, right: Image.Image) -> float:
    left_alpha = left.getchannel("A").point(lambda value: 255 if value else 0)
    right_alpha = right.getchannel("A").point(lambda value: 255 if value else 0)
    union = ImageChops.lighter(left_alpha, right_alpha)
    difference = ImageChops.logical_xor(left_alpha.convert("1"), right_alpha.convert("1"))
    union_pixels = sum(1 for value in union.get_flattened_data() if value)
    difference_pixels = sum(1 for value in difference.get_flattened_data() if value)
    return difference_pixels / union_pixels if union_pixels else 0.0


def validate_sources() -> list[str]:
    failures: list[str] = []
    for direction in DIRECTIONS:
        for character in CHARACTERS:
            frames = derived_frames(character, direction)
            change = silhouette_change(frames[0], frames[3])
            state = "PASS" if change >= MIN_OPPOSITE_CHANGE else "FAIL"
            print(
                f"{state} {character.member_id:<14} {direction:<10} "
                f"phase0<->3={change * 100:5.1f}%"
            )
            if change < MIN_OPPOSITE_CHANGE:
                failures.append(
                    f"{character.member_id}/{direction}={change * 100:.1f}%"
                )
    return failures


def bootstrap_existing() -> None:
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase in range(3):
                source = character.runtime_frame(direction, phase)
                destination = character.source_frame(direction, phase)
                destination.parent.mkdir(parents=True, exist_ok=True)
                if destination.exists():
                    continue
                shutil.copy2(source, destination)
                print(f"BOOTSTRAP {destination.relative_to(REPO_ROOT)}")


def import_strip(
    member_id: str,
    direction: str,
    strip_path: Path,
    mirror_import: bool = False,
    phase_offset: int = 0,
) -> None:
    character = CHARACTER_BY_ID.get(member_id)
    if character is None:
        raise ValueError(f"unknown member: {member_id}")
    if direction not in DIRECTIONS:
        raise ValueError(f"unknown direction: {direction}")
    strip = Image.open(strip_path).convert("RGBA")
    if strip.width < 3 or strip.height < 1:
        raise ValueError(f"invalid strip dimensions: {strip.size}")
    x_edges = [round(strip.width * index / 3) for index in range(4)]
    outputs: list[Image.Image] = []
    for phase in range(3):
        panel = strip.crop((x_edges[phase], 0, x_edges[phase + 1], strip.height))
        normalized = normalize_sprite(remove_green_chroma(panel), character.target_height)
        outputs.append(ImageOps.mirror(normalized) if mirror_import else normalized)
    if len({hashlib.sha256(image.tobytes()).digest() for image in outputs}) != 3:
        raise ValueError("generated half-cycle contains duplicate poses")
    phase_offset %= 3
    outputs = outputs[phase_offset:] + outputs[:phase_offset]
    for phase, image in enumerate(outputs):
        destination = character.source_frame(direction, phase)
        destination.parent.mkdir(parents=True, exist_ok=True)
        image.save(destination)
        print(f"IMPORTED {destination.relative_to(REPO_ROOT)} sha256={sha256(destination)}")


def rotate_sources(member_id: str, direction: str, phase_offset: int) -> None:
    character = CHARACTER_BY_ID.get(member_id)
    if character is None:
        raise ValueError(f"unknown member: {member_id}")
    if direction not in DIRECTIONS:
        raise ValueError(f"unknown direction: {direction}")
    outputs = [load_source(character, direction, phase).copy() for phase in range(3)]
    phase_offset %= 3
    outputs = outputs[phase_offset:] + outputs[:phase_offset]
    for phase, image in enumerate(outputs):
        destination = character.source_frame(direction, phase)
        image.save(destination)
        print(f"ROTATED {destination.relative_to(REPO_ROOT)} sha256={sha256(destination)}")


def normalize_source_ground_lines() -> None:
    target_bottom = GROUND_Y - 1
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase in range(3):
                image = load_source(character, direction, phase)
                bbox = image.getchannel("A").getbbox()
                if bbox is None:
                    raise ValueError(
                        f"empty source: {character.member_id}/{direction}/{phase}"
                    )
                bottom = bbox[3] - 1
                delta_y = target_bottom - bottom
                if delta_y == 0:
                    continue
                moved = Image.new("RGBA", (TARGET_SIZE, TARGET_SIZE), (0, 0, 0, 0))
                moved.alpha_composite(image, (0, delta_y))
                moved_bbox = moved.getchannel("A").getbbox()
                if moved_bbox is None or moved_bbox[3] - 1 != target_bottom:
                    raise ValueError(
                        f"ground normalization clipped {character.member_id}/{direction}/{phase}"
                    )
                destination = character.source_frame(direction, phase)
                hard_alpha(moved).save(destination)
                print(
                    f"GROUNDED {destination.relative_to(REPO_ROOT)} "
                    f"from={bottom} to={target_bottom}"
                )


def build_sheets(character: Character, rows: tuple[str, ...], suffix: str) -> Image.Image:
    sheet = Image.new("RGBA", (TARGET_SIZE * 6, TARGET_SIZE * 4), (0, 0, 0, 0))
    for row, direction in enumerate(rows):
        for phase, frame in enumerate(derived_frames(character, direction)):
            sheet.paste(frame, (phase * TARGET_SIZE, row * TARGET_SIZE))
    return sheet


def write_outputs() -> None:
    failures = validate_sources()
    if failures:
        raise SystemExit(
            "refusing to write runtime assets; phase 0<->3 threshold failed: "
            + ", ".join(failures)
        )
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase, frame in enumerate(derived_frames(character, direction)):
                destination = character.runtime_frame(direction, phase)
                frame.save(destination)
                print(f"WROTE {destination.relative_to(REPO_ROOT)}")
        for rows, suffix in ((DIRECTIONS_A, "a"), (DIRECTIONS_B, "b")):
            metadata = PngInfo()
            metadata.add_text(LAYOUT_METADATA_KEY, GRID_LAYOUT_MARKER)
            destination = character.sheet_path(suffix)
            build_sheets(character, rows, suffix).save(destination, pnginfo=metadata)
            print(f"WROTE {destination.relative_to(REPO_ROOT)} sha256={sha256(destination)}")


def check_outputs() -> None:
    failures = validate_sources()
    if failures:
        raise SystemExit("source threshold failures: " + ", ".join(failures))
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase, expected in enumerate(derived_frames(character, direction)):
                tracked = hard_alpha(Image.open(character.runtime_frame(direction, phase)))
                if tracked.tobytes() != expected.tobytes():
                    raise SystemExit(
                        f"stale runtime frame: {character.member_id}/{direction}/{phase}"
                    )
        for rows, suffix in ((DIRECTIONS_A, "a"), (DIRECTIONS_B, "b")):
            with Image.open(character.sheet_path(suffix)) as loaded:
                if loaded.info.get(LAYOUT_METADATA_KEY) != GRID_LAYOUT_MARKER:
                    raise SystemExit(f"missing sheet marker: {character.sheet_path(suffix)}")
                tracked_sheet = loaded.convert("RGBA")
            expected_sheet = build_sheets(character, rows, suffix)
            if tracked_sheet.tobytes() != expected_sheet.tobytes():
                raise SystemExit(f"stale sheet: {character.member_id}/{suffix}")
    print("PASS all family half-cycle V2 runtime outputs match deterministic sources")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bootstrap-existing", action="store_true")
    parser.add_argument("--validate-sources", action="store_true")
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--import-strip", nargs=3, metavar=("MEMBER", "DIRECTION", "PNG"))
    parser.add_argument("--rotate-source", nargs=3, metavar=("MEMBER", "DIRECTION", "OFFSET"))
    parser.add_argument("--normalize-sources", action="store_true")
    parser.add_argument(
        "--mirror-import",
        action="store_true",
        help="mirror every imported panel after normalization (for a wrong-facing generation)",
    )
    parser.add_argument(
        "--phase-offset",
        type=int,
        default=0,
        help="cyclically rotate the three authored poses while preserving their order",
    )
    args = parser.parse_args()
    selected = sum(
        bool(value)
        for value in (
            args.bootstrap_existing,
            args.validate_sources,
            args.write,
            args.check,
            args.import_strip,
            args.rotate_source,
            args.normalize_sources,
        )
    )
    if selected != 1:
        parser.error("select exactly one operation")
    if args.mirror_import and not args.import_strip:
        parser.error("--mirror-import requires --import-strip")
    if args.phase_offset and not args.import_strip:
        parser.error("--phase-offset requires --import-strip")
    if (args.mirror_import or args.phase_offset) and args.rotate_source:
        parser.error("import modifiers cannot be combined with --rotate-source")
    if args.bootstrap_existing:
        bootstrap_existing()
    elif args.validate_sources:
        failures = validate_sources()
        if failures:
            raise SystemExit("threshold failures: " + ", ".join(failures))
    elif args.write:
        write_outputs()
    elif args.check:
        check_outputs()
    elif args.normalize_sources:
        normalize_source_ground_lines()
    elif args.import_strip:
        assert args.import_strip is not None
        member_id, direction, png = args.import_strip
        import_strip(
            member_id,
            direction,
            Path(png).resolve(),
            args.mirror_import,
            args.phase_offset,
        )
    else:
        assert args.rotate_source is not None
        member_id, direction, offset = args.rotate_source
        rotate_sources(member_id, direction, int(offset))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
