#!/usr/bin/env python3
"""Split discovered 8-direction / 6-frame character sheets into Unity sprites."""

from __future__ import annotations

import argparse
import hashlib
import re
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image


CELL_SIZE = 256
SHEET_SIZE = (1536, 1024)
PHASE_COUNT = 6
META_GUID_NAMESPACE = "family-company/high-motion-frame/v1/"
PART_DIRECTIONS = {
    "a": ("south", "southwest", "west", "northwest"),
    "b": ("north", "northeast", "east", "southeast"),
}
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"
SHEET_RE = re.compile(
    r"^(?P<character>[a-z0-9_]+?)_pixel_walk8dir6_(?P<part>[ab])_v1\.png$"
)


@dataclass(frozen=True)
class CharacterSpec:
    character_id: str
    high_motion_root: Path

    @property
    def frame_folder(self) -> Path:
        return self.high_motion_root / "Frames"

    def sheet_path(self, part: str) -> Path:
        return self.high_motion_root / (
            f"{self.character_id}_pixel_walk8dir6_{part}_v1.png"
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Unity project or candidate mirror root (defaults to the parent of Tools).",
    )
    parser.add_argument(
        "--verify-only",
        action="store_true",
        help="Validate sheets and already-split frames without writing files.",
    )
    parser.add_argument(
        "--character",
        action="append",
        default=None,
        help=(
            "Limit processing to a character ID. Repeat the option for multiple "
            "characters; discovery still validates the complete asset tree."
        ),
    )
    parser.add_argument(
        "--assume-grid-layout",
        action="store_true",
        help=(
            "Crop exact 256px 4x6 cells even when legacy sheets do not carry the "
            f"{LAYOUT_METADATA_KEY} PNG marker."
        ),
    )
    return parser.parse_args()


def discover_characters(repo_root: Path) -> list[CharacterSpec]:
    """Discover complete sheet-pair + Frames contracts without a character registry."""
    art_root = repo_root / "Assets" / "Art" / "Characters"
    if not art_root.is_dir():
        raise FileNotFoundError(f"Character art root not found: {art_root}")

    result: list[CharacterSpec] = []
    seen_ids: dict[str, Path] = {}
    for high_motion_root in sorted(art_root.rglob("Pixel/HighMotion")):
        if not high_motion_root.is_dir():
            continue

        discovered: dict[str, dict[str, Path]] = {}
        for sheet_path in sorted(high_motion_root.glob("*_pixel_walk8dir6_?_v1.png")):
            match = SHEET_RE.match(sheet_path.name)
            if match is None:
                raise ValueError(f"Unexpected HighMotion sheet name: {sheet_path}")
            character_id = match["character"]
            part = match["part"]
            parts = discovered.setdefault(character_id, {})
            if part in parts:
                raise ValueError(
                    f"Duplicate HighMotion sheet part {part} for {character_id}: "
                    f"{parts[part]}, {sheet_path}"
                )
            parts[part] = sheet_path

        if not discovered:
            continue
        if len(discovered) != 1:
            raise ValueError(
                f"{high_motion_root} must contain one character sheet pair; "
                f"found {sorted(discovered)}"
            )

        character_id, parts = next(iter(discovered.items()))
        if set(parts) != set(PART_DIRECTIONS):
            raise FileNotFoundError(
                f"{character_id} must have sheet parts a and b in {high_motion_root}; "
                f"found {sorted(parts)}"
            )
        frame_folder = high_motion_root / "Frames"
        if not frame_folder.is_dir():
            raise FileNotFoundError(
                f"Missing HighMotion Frames directory for {character_id}: {frame_folder}"
            )
        if character_id in seen_ids:
            raise ValueError(
                f"Duplicate HighMotion character ID {character_id}: "
                f"{seen_ids[character_id]}, {high_motion_root}"
            )
        seen_ids[character_id] = high_motion_root
        result.append(CharacterSpec(character_id, high_motion_root))

    if not result:
        raise ValueError(f"No complete HighMotion character sets found under {art_root}")
    return result


def expected_frame_names(character_id: str) -> set[str]:
    return {
        f"{character_id}_{direction}_walk_{phase}.png"
        for directions in PART_DIRECTIONS.values()
        for direction in directions
        for phase in range(PHASE_COUNT)
    }


def validate_frame_contract(spec: CharacterSpec) -> None:
    expected = expected_frame_names(spec.character_id)
    actual = {path.name for path in spec.frame_folder.glob("*.png")}
    if actual == expected:
        return
    missing = sorted(expected - actual)
    unexpected = sorted(actual - expected)
    raise ValueError(
        f"{spec.character_id}: Frames must contain exactly 48 walk PNGs; "
        f"missing={missing} unexpected={unexpected}"
    )


def require_hard_alpha(image: Image.Image, path: Path | str) -> None:
    histogram = image.getchannel("A").histogram()
    unexpected = [value for value, count in enumerate(histogram) if count and value not in (0, 255)]
    if unexpected:
        raise ValueError(f"{path} has partial alpha values: {unexpected}")


def validate_frame(frame: Image.Image, label: str) -> None:
    if frame.size != (CELL_SIZE, CELL_SIZE):
        raise ValueError(f"{label} has invalid size {frame.size}")
    alpha = frame.getchannel("A")
    if alpha.getbbox() is None:
        raise ValueError(f"{label} is empty")


def ensure_unity_meta(repo_root: Path, asset_path: Path) -> None:
    """Create a stable minimal Unity meta without replacing an imported asset GUID."""
    meta_path = Path(f"{asset_path}.meta")
    if meta_path.exists():
        return
    relative_path = asset_path.relative_to(repo_root).as_posix()
    guid = hashlib.md5(f"{META_GUID_NAMESPACE}{relative_path}".encode("utf-8")).hexdigest()
    meta_path.write_text(
        f"fileFormatVersion: 2\nguid: {guid}\n",
        encoding="utf-8",
        newline="\n",
    )


def extract_aligned_frames(sheet: Image.Image, sheet_path: Path) -> list[list[Image.Image]]:
    """Find the 24 real silhouettes and align them to a stable in-place anchor."""
    rgba = np.asarray(sheet)
    binary = (rgba[:, :, 3] > 0).astype(np.uint8)
    components = connected_component_stats(binary)
    main_components = [component for component in components if component["area"] >= 1000]
    if len(main_components) != 24:
        areas = sorted((component["area"] for component in components), reverse=True)[:30]
        raise ValueError(
            f"{sheet_path} must contain 24 main silhouettes; "
            f"found {len(main_components)}. areas={areas}"
        )

    main_components.sort(key=lambda component: (component["centroid_y"], component["centroid_x"]))
    rows: list[list[Image.Image]] = []
    for row_index in range(4):
        row_components = main_components[
            row_index * PHASE_COUNT : (row_index + 1) * PHASE_COUNT
        ]
        row_components.sort(key=lambda component: component["centroid_x"])
        row_frames: list[Image.Image] = []
        for component in row_components:
            x = component["left"]
            y = component["top"]
            width = component["width"]
            height = component["height"]
            padding = 12
            left = max(0, x - padding)
            top = max(0, y - padding)
            right = min(sheet.width, x + width + padding)
            bottom = min(sheet.height, y + height + padding)
            crop = sheet.crop((left, top, right, bottom))
            alpha = np.asarray(crop.getchannel("A"))
            ys, xs = np.nonzero(alpha)
            if len(xs) == 0:
                raise ValueError(f"{sheet_path} contains an empty detected silhouette")

            min_y = int(ys.min())
            max_y = int(ys.max())
            upper_limit = min_y + max(1, int((max_y - min_y) * 0.55))
            upper_xs = xs[ys <= upper_limit]
            anchor_x = int(round(float(np.median(upper_xs))))
            offset_x = (CELL_SIZE // 2) - anchor_x
            offset_y = (CELL_SIZE - 8) - max_y

            canvas = Image.new("RGBA", (CELL_SIZE, CELL_SIZE), (0, 0, 0, 0))
            canvas.alpha_composite(crop, (offset_x, offset_y))
            validate_frame(canvas, f"{sheet_path} component at ({x},{y})")
            row_frames.append(canvas)
        rows.append(row_frames)
    return rows


def connected_component_stats(binary: np.ndarray) -> list[dict[str, int | float]]:
    """Return 8-connected component stats using row runs, without OpenCV."""
    height, _ = binary.shape
    runs: list[tuple[int, int, int]] = []
    parents: list[int] = []

    def find(index: int) -> int:
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    def union(left: int, right: int) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root != right_root:
            parents[right_root] = left_root

    previous: list[int] = []
    for y in range(height):
        xs = np.flatnonzero(binary[y])
        if len(xs) == 0:
            previous = []
            continue
        breaks = np.flatnonzero(np.diff(xs) > 1)
        starts = np.concatenate(([0], breaks + 1))
        ends = np.concatenate((breaks, [len(xs) - 1]))
        current: list[int] = []
        previous_cursor = 0
        for start_index, end_index in zip(starts, ends):
            x0 = int(xs[start_index])
            x1 = int(xs[end_index])
            run_index = len(runs)
            runs.append((y, x0, x1))
            parents.append(run_index)
            current.append(run_index)

            while (
                previous_cursor < len(previous)
                and runs[previous[previous_cursor]][2] < x0 - 1
            ):
                previous_cursor += 1
            cursor = previous_cursor
            while cursor < len(previous) and runs[previous[cursor]][1] <= x1 + 1:
                union(run_index, previous[cursor])
                cursor += 1
        previous = current

    aggregates: dict[int, dict[str, int]] = {}
    for index, (y, x0, x1) in enumerate(runs):
        root = find(index)
        length = x1 - x0 + 1
        aggregate = aggregates.setdefault(
            root,
            {
                "area": 0,
                "sum_x": 0,
                "sum_y": 0,
                "left": x0,
                "right": x1,
                "top": y,
                "bottom": y,
            },
        )
        aggregate["area"] += length
        aggregate["sum_x"] += ((x0 + x1) * length) // 2
        aggregate["sum_y"] += y * length
        aggregate["left"] = min(aggregate["left"], x0)
        aggregate["right"] = max(aggregate["right"], x1)
        aggregate["top"] = min(aggregate["top"], y)
        aggregate["bottom"] = max(aggregate["bottom"], y)

    result: list[dict[str, int | float]] = []
    for aggregate in aggregates.values():
        area = aggregate["area"]
        result.append(
            {
                "area": area,
                "left": aggregate["left"],
                "top": aggregate["top"],
                "width": aggregate["right"] - aggregate["left"] + 1,
                "height": aggregate["bottom"] - aggregate["top"] + 1,
                "centroid_x": aggregate["sum_x"] / area,
                "centroid_y": aggregate["sum_y"] / area,
            }
        )
    return result


def extract_grid_frames(sheet: Image.Image, sheet_path: Path) -> list[list[Image.Image]]:
    """Crop a marker-authored 4x6 sheet without applying legacy realignment."""
    rows: list[list[Image.Image]] = []
    for row_index in range(4):
        row_frames: list[Image.Image] = []
        for phase in range(PHASE_COUNT):
            left = phase * CELL_SIZE
            top = row_index * CELL_SIZE
            frame = sheet.crop(
                (left, top, left + CELL_SIZE, top + CELL_SIZE)
            )
            label = f"{sheet_path} grid row={row_index} phase={phase}"
            validate_frame(frame, label)
            require_hard_alpha(frame, label)
            row_frames.append(frame)
        rows.append(row_frames)
    return rows


def split_character(
    repo_root: Path,
    spec: CharacterSpec,
    verify_only: bool,
    assume_grid_layout: bool,
) -> int:
    if verify_only:
        validate_frame_contract(spec)
    else:
        spec.frame_folder.mkdir(parents=True, exist_ok=True)
    written = 0
    for part, directions in PART_DIRECTIONS.items():
        sheet_path = spec.sheet_path(part)
        if not sheet_path.is_file():
            raise FileNotFoundError(sheet_path)

        with Image.open(sheet_path) as loaded:
            layout_marker = loaded.info.get(LAYOUT_METADATA_KEY)
            sheet = loaded.convert("RGBA")
        if sheet.size != SHEET_SIZE:
            raise ValueError(f"{sheet_path} must be {SHEET_SIZE}, got {sheet.size}")
        require_hard_alpha(sheet, sheet_path)
        if assume_grid_layout:
            aligned_frames = extract_grid_frames(sheet, sheet_path)
        elif layout_marker is None:
            aligned_frames = extract_aligned_frames(sheet, sheet_path)
        elif layout_marker == GRID_LAYOUT_MARKER:
            aligned_frames = extract_grid_frames(sheet, sheet_path)
        else:
            raise ValueError(
                f"{sheet_path} has unsupported {LAYOUT_METADATA_KEY}={layout_marker!r}"
            )

        for row, direction in enumerate(directions):
            for phase in range(PHASE_COUNT):
                frame = aligned_frames[row][phase]
                frame_name = f"{spec.character_id}_{direction}_walk_{phase}.png"
                frame_path = spec.frame_folder / frame_name
                validate_frame(frame, str(frame_path))
                if verify_only:
                    with Image.open(frame_path) as existing:
                        existing_rgba = existing.convert("RGBA")
                    validate_frame(existing_rgba, str(frame_path))
                    if existing_rgba.tobytes() != frame.tobytes():
                        raise ValueError(f"{frame_path} does not match its source sheet cell")
                else:
                    frame.save(frame_path, format="PNG", compress_level=9)
                    ensure_unity_meta(repo_root, frame_path)
                written += 1

    if written != 48:
        raise AssertionError(f"{spec.character_id}: expected 48 frames, got {written}")
    validate_frame_contract(spec)
    return written


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    characters = discover_characters(repo_root)
    if args.character:
        requested = {character_id.strip().lower() for character_id in args.character}
        available = {spec.character_id for spec in characters}
        unknown = sorted(requested - available)
        if unknown:
            raise ValueError(
                f"Unknown HighMotion character IDs {unknown}; available={sorted(available)}"
            )
        characters = [spec for spec in characters if spec.character_id in requested]
    total = sum(
        split_character(
            repo_root,
            spec,
            args.verify_only,
            args.assume_grid_layout,
        )
        for spec in characters
    )
    expected_total = (
        len(characters)
        * sum(len(directions) for directions in PART_DIRECTIONS.values())
        * PHASE_COUNT
    )
    if total != expected_total:
        raise AssertionError(f"Expected {expected_total} frames, got {total}")
    action = "verified" if args.verify_only else "wrote"
    print(
        f"HIGH_MOTION_SPLIT: PASS {action}={total} characters={len(characters)} "
        f"directions=8 phases={PHASE_COUNT}"
    )


if __name__ == "__main__":
    main()
