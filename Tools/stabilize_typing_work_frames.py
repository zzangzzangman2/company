#!/usr/bin/env python3
"""Build planted typing loops from each character's authored seated pose.

The office work-action source images were generated independently per frame, so
heads, torsos and hair changed while typing.  This tool discovers every
OfficeWorkActionsV1 character automatically, uses that character/direction's
approved seating work pose as the planted body, and animates only two small
fingertip groups.  Adding another character with the same asset contract needs
no member-specific coordinates.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


CANVAS_SIZE = (256, 256)
FRAME_COUNT = 6
DIRECTIONS = (
    "south", "southwest", "west", "northwest",
    "north", "northeast", "east", "southeast",
)
LEFT_FACING = frozenset({"southwest", "west", "northwest"})
RIGHT_FACING = frozenset({"northeast", "east", "southeast"})
PHASES = ((0, 0), (1, 0), (0, 1), (2, 0), (0, 2), (1, 1))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--art-root",
        type=Path,
        default=Path("Assets/Art/Characters"),
    )
    parser.add_argument(
        "--all-directions",
        action="store_true",
        help="also prepare directions not yet approved by the seating-pose catalog",
    )
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--qa-output", type=Path)
    return parser.parse_args()


def discover(art_root: Path) -> list[tuple[str, Path, Path]]:
    result: list[tuple[str, Path, Path]] = []
    pattern = "OfficeWorkActionsV1/Frames/Typing"
    for typing_dir in sorted(art_root.rglob("Typing")):
        normalized = typing_dir.as_posix()
        if pattern not in normalized:
            continue
        work_root = typing_dir.parents[1]
        seating_frames = work_root.parent / "OfficeSeatingV1" / "Frames"
        if not seating_frames.is_dir():
            continue
        candidates = sorted(typing_dir.glob("*_typing_00_northwest_v1.png"))
        if len(candidates) != 1:
            raise ValueError(f"Could not resolve one character id in {typing_dir}")
        member_id = candidates[0].name.removesuffix("_typing_00_northwest_v1.png")
        result.append((member_id, typing_dir, seating_frames))
    if not result:
        raise ValueError(f"No OfficeWorkActionsV1 typing roots found below {art_root}")
    return result


def source_path(seating_frames: Path, member_id: str, direction: str) -> Path:
    return seating_frames / f"{member_id}_{direction}_sit_work_0.png"


def target_path(typing_dir: Path, member_id: str, direction: str, frame: int) -> Path:
    return typing_dir / f"{member_id}_typing_{frame:02d}_{direction}_v1.png"


def load_rgba(path: Path) -> np.ndarray:
    with Image.open(path) as loaded:
        if loaded.size != CANVAS_SIZE:
            raise ValueError(f"{path} must be {CANVAS_SIZE}, got {loaded.size}")
        return np.asarray(loaded.convert("RGBA"), dtype=np.uint8).copy()


def subject_box(rgba: np.ndarray) -> tuple[int, int, int, int]:
    alpha = rgba[:, :, 3] > 0
    ys, xs = np.nonzero(alpha)
    if not len(xs):
        raise ValueError("Seated source frame is empty")
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def skin_mask(rgba: np.ndarray) -> np.ndarray:
    rgb = rgba[:, :, :3].astype(np.int16)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (
        (rgba[:, :, 3] > 0)
        & (red >= 145)
        & (green >= 72)
        & (blue >= 48)
        & (red - green >= 18)
        & (green - blue >= 8)
        & (green * 100 >= red * 52)
    )


def fingertip_groups(rgba: np.ndarray, direction: str) -> tuple[np.ndarray, np.ndarray]:
    left, top, right, bottom = subject_box(rgba)
    width = right - left + 1
    height = bottom - top + 1
    yy, xx = np.indices((CANVAS_SIZE[1], CANVAS_SIZE[0]))
    band = (yy >= top + height * 0.43) & (yy <= top + height * 0.64)
    mask = skin_mask(rgba) & band

    center_x = (left + right) * 0.5
    if direction in LEFT_FACING:
        mask &= xx <= center_x
        coordinates = np.nonzero(mask)
        if len(coordinates[1]):
            edge = float(np.percentile(coordinates[1], 28))
            mask &= xx <= edge + 2
    elif direction in RIGHT_FACING:
        mask &= xx >= center_x
        coordinates = np.nonzero(mask)
        if len(coordinates[1]):
            edge = float(np.percentile(coordinates[1], 72))
            mask &= xx >= edge - 2
    else:
        # Front/back poses expose both hands around the horizontal middle.
        mask &= (xx >= left + width * 0.18) & (xx <= right - width * 0.18)

    ys, xs = np.nonzero(mask)
    if len(xs) < 4:
        # Fail closed to a tiny opaque action-zone patch rather than touching a face/leg.
        alpha = rgba[:, :, 3] > 0
        mask = alpha & band & (xx >= left + width * 0.20) & (xx <= right - width * 0.20)
        ys, xs = np.nonzero(mask)
    if len(xs) < 2:
        raise ValueError(f"No fingertip pixels found for {direction}")

    # A key press is a fingertip gesture, never an arm or torso translation.  Keep
    # only the outer action edge even if a skin-tone shirt/pants palette happened
    # to enter the broad colour candidate mask.
    points = list(zip(xs.tolist(), ys.tolist()))
    if direction in LEFT_FACING:
        points.sort(key=lambda point: (point[0], point[1]))
    elif direction in RIGHT_FACING:
        points.sort(key=lambda point: (-point[0], point[1]))
    else:
        expected_y = top + height * 0.55
        expected_x = (left + right) * 0.5
        points.sort(key=lambda point: (abs(point[1] - expected_y), abs(point[0] - expected_x)))
    limited = np.zeros_like(mask)
    for x, y in points[:96]:
        limited[y, x] = True
    mask = limited

    if direction in LEFT_FACING or direction in RIGHT_FACING:
        split = float(np.median(ys))
        first = mask & (yy <= split)
        second = mask & (yy > split)
    else:
        split = float(np.median(xs))
        first = mask & (xx <= split)
        second = mask & (xx > split)
    if not first.any() or not second.any():
        ordered = sorted(zip(xs.tolist(), ys.tolist()), key=lambda point: (point[1], point[0]))
        first = np.zeros_like(mask)
        second = np.zeros_like(mask)
        for index, (x, y) in enumerate(ordered):
            (first if index % 2 == 0 else second)[y, x] = True
    return first, second


def overlay_shifted(base: np.ndarray, masks: tuple[np.ndarray, np.ndarray], phase: tuple[int, int]) -> np.ndarray:
    output = base.copy()
    for mask, dy in zip(masks, phase):
        if dy <= 0:
            continue
        ys, xs = np.nonzero(mask)
        target_y = np.minimum(CANVAS_SIZE[1] - 1, ys + dy)
        output[target_y, xs] = base[ys, xs]
        # A one-tone contact shadow makes the pressed fingertip read at game scale.
        dark_y = np.minimum(CANVAS_SIZE[1] - 1, target_y + 1)
        shadow = base[ys, xs].copy()
        shadow[:, :3] = np.maximum(0, shadow[:, :3].astype(np.int16) - 18).astype(np.uint8)
        vacant = output[dark_y, xs, 3] == 0
        output[dark_y[vacant], xs[vacant]] = shadow[vacant]
    return output


def rgba_sha256(rgba: np.ndarray) -> str:
    return hashlib.sha256(rgba.tobytes()).hexdigest().upper()


def checker() -> Image.Image:
    result = Image.new("RGBA", CANVAS_SIZE, (29, 45, 49, 255))
    draw = ImageDraw.Draw(result)
    for y in range(0, CANVAS_SIZE[1], 16):
        for x in range(0, CANVAS_SIZE[0], 16):
            if (x // 16 + y // 16) % 2:
                draw.rectangle((x, y, x + 15, y + 15), fill=(36, 55, 58, 255))
    return result


def write_qa(path: Path, rows: list[tuple[str, str, list[np.ndarray]]]) -> None:
    scale = 1
    label_height = 24
    sheet = Image.new(
        "RGBA",
        (CANVAS_SIZE[0] * FRAME_COUNT * scale, (CANVAS_SIZE[1] + label_height) * len(rows)),
        (10, 27, 29, 255),
    )
    draw = ImageDraw.Draw(sheet)
    for row, (member_id, direction, frames) in enumerate(rows):
        y = row * (CANVAS_SIZE[1] + label_height)
        for index, rgba in enumerate(frames):
            cell = checker()
            cell.alpha_composite(Image.fromarray(rgba, "RGBA"))
            sheet.alpha_composite(cell, (index * CANVAS_SIZE[0], y))
        draw.text((8, y + CANVAS_SIZE[1] + 5), f"{member_id} / {direction} / planted body + local fingertips", fill=(222, 242, 235, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path, quality=94)


def main() -> int:
    args = parse_args()
    rows: list[tuple[str, str, list[np.ndarray]]] = []
    total = 0
    requested_directions = DIRECTIONS if args.all_directions else ("northwest",)
    for member_id, typing_dir, seating_frames in discover(args.art_root):
        for direction in requested_directions:
            base_path = source_path(seating_frames, member_id, direction)
            if not base_path.is_file():
                raise FileNotFoundError(base_path)
            base = load_rgba(base_path)
            masks = fingertip_groups(base, direction)
            outputs = [overlay_shifted(base, masks, phase) for phase in PHASES]
            if len({rgba_sha256(frame) for frame in outputs}) != FRAME_COUNT:
                raise ValueError(f"Typing phases are not unique: {member_id}/{direction}")
            rows.append((member_id, direction, outputs))
            for frame, rgba in enumerate(outputs):
                path = target_path(typing_dir, member_id, direction, frame)
                if not path.is_file():
                    raise FileNotFoundError(path)
                if args.apply:
                    Image.fromarray(rgba, "RGBA").save(path, optimize=True)
                total += 1
            print(
                f"typing={member_id}/{direction} frames={FRAME_COUNT} "
                f"fingertips={int(masks[0].sum())}+{int(masks[1].sum())}"
            )
    if args.qa_output:
        write_qa(args.qa_output, rows)
        print(f"qa={args.qa_output}")
    print(f"frames={total} applied={1 if args.apply else 0}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
