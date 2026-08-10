#!/usr/bin/env python3
"""Split the approved 8-direction / 6-frame character sheets into Unity sprites."""

from __future__ import annotations

import argparse
import hashlib
from dataclasses import dataclass
from pathlib import Path

import cv2
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


@dataclass(frozen=True)
class CharacterSpec:
    character_id: str
    asset_root: str


CHARACTERS = (
    CharacterSpec("player", "Assets/Art/Characters/Player"),
    CharacterSpec("older_sister", "Assets/Art/Characters/OlderSister"),
    CharacterSpec("father", "Assets/Art/Characters/Father"),
    CharacterSpec("mother", "Assets/Art/Characters/Mother"),
    CharacterSpec("kim_seoa", "Assets/Art/Characters/Employees/KimSeoa"),
    CharacterSpec("lee_jian", "Assets/Art/Characters/Employees/LeeJian"),
    CharacterSpec("choi_iseo", "Assets/Art/Characters/Employees/ChoiIseo"),
    CharacterSpec("jung_arin", "Assets/Art/Characters/Employees/JungArin"),
    CharacterSpec("park_haeun", "Assets/Art/Characters/Employees/ParkHaeun"),
    CharacterSpec("han_sua", "Assets/Art/Characters/Employees/HanSua"),
    CharacterSpec("oh_jiwoo", "Assets/Art/Characters/Employees/OhJiwoo"),
    CharacterSpec("yoon_chaea", "Assets/Art/Characters/Employees/YoonChaea"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Unity project root (defaults to the parent of Tools).",
    )
    parser.add_argument(
        "--verify-only",
        action="store_true",
        help="Validate sheets and already-split frames without writing files.",
    )
    return parser.parse_args()


def require_hard_alpha(image: Image.Image, path: Path) -> None:
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
    component_count, labels, stats, centroids = cv2.connectedComponentsWithStats(binary, 8)
    main_labels = [
        label
        for label in range(1, component_count)
        if int(stats[label, cv2.CC_STAT_AREA]) >= 1000
    ]
    if len(main_labels) != 24:
        areas = sorted(
            (int(stats[label, cv2.CC_STAT_AREA]) for label in range(1, component_count)),
            reverse=True,
        )[:30]
        raise ValueError(f"{sheet_path} must contain 24 main silhouettes; found {len(main_labels)}. areas={areas}")

    main_labels.sort(key=lambda label: (float(centroids[label][1]), float(centroids[label][0])))
    rows: list[list[Image.Image]] = []
    for row_index in range(4):
        row_labels = main_labels[row_index * PHASE_COUNT : (row_index + 1) * PHASE_COUNT]
        row_labels.sort(key=lambda label: float(centroids[label][0]))
        row_frames: list[Image.Image] = []
        for label in row_labels:
            x = int(stats[label, cv2.CC_STAT_LEFT])
            y = int(stats[label, cv2.CC_STAT_TOP])
            width = int(stats[label, cv2.CC_STAT_WIDTH])
            height = int(stats[label, cv2.CC_STAT_HEIGHT])
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
            validate_frame(canvas, f"{sheet_path} component {label}")
            row_frames.append(canvas)
        rows.append(row_frames)
    return rows


def split_character(repo_root: Path, spec: CharacterSpec, verify_only: bool) -> int:
    high_motion = repo_root / spec.asset_root / "Pixel" / "HighMotion"
    frame_folder = high_motion / "Frames"
    if not verify_only:
        frame_folder.mkdir(parents=True, exist_ok=True)

    written = 0
    for part, directions in PART_DIRECTIONS.items():
        sheet_path = high_motion / f"{spec.character_id}_pixel_walk8dir6_{part}_v1.png"
        if not sheet_path.is_file():
            raise FileNotFoundError(sheet_path)

        with Image.open(sheet_path) as loaded:
            sheet = loaded.convert("RGBA")
        if sheet.size != SHEET_SIZE:
            raise ValueError(f"{sheet_path} must be {SHEET_SIZE}, got {sheet.size}")
        require_hard_alpha(sheet, sheet_path)
        aligned_frames = extract_aligned_frames(sheet, sheet_path)

        for row, direction in enumerate(directions):
            for phase in range(PHASE_COUNT):
                frame = aligned_frames[row][phase]
                frame_name = f"{spec.character_id}_{direction}_walk_{phase}.png"
                frame_path = frame_folder / frame_name
                validate_frame(frame, str(frame_path))
                if verify_only:
                    if not frame_path.is_file():
                        raise FileNotFoundError(frame_path)
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
    return written


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    total = sum(split_character(repo_root, spec, args.verify_only) for spec in CHARACTERS)
    if total != 576:
        raise AssertionError(f"Expected 576 frames, got {total}")
    action = "verified" if args.verify_only else "wrote"
    print(f"HIGH_MOTION_SPLIT: PASS {action}=576 characters=12 directions=8 phases=6")


if __name__ == "__main__":
    main()
