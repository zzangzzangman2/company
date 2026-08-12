#!/usr/bin/env python3
"""Stabilize the mother's seated-work loop while preserving the authored hand poses.

The first work frame is the canonical body.  Later frames contribute pixels only
inside a small near-arm polygon after integer registration against the canonical
head/torso/legs.  This deliberately prevents head, torso, skirt, and foot bob.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


FRAME_COUNT = 6
CANVAS_SIZE = (256, 256)
ARM_POLYGON = (
    (69, 118),
    (111, 116),
    (132, 128),
    (134, 145),
    (124, 159),
    (94, 157),
    (69, 149),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--frames-dir",
        type=Path,
        default=Path(
            "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames"
        ),
    )
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--qa-output", type=Path)
    return parser.parse_args()


def frame_path(frames_dir: Path, frame: int) -> Path:
    return frames_dir / f"mother_northwest_sit_work_{frame}.png"


def load_frames(frames_dir: Path) -> list[Image.Image]:
    result: list[Image.Image] = []
    for frame in range(FRAME_COUNT):
        path = frame_path(frames_dir, frame)
        image = Image.open(path).convert("RGBA")
        if image.size != CANVAS_SIZE:
            raise ValueError(f"{path} must be 256x256, got {image.size}")
        result.append(image)
    return result


def polygon_mask() -> Image.Image:
    mask = Image.new("L", CANVAS_SIZE, 0)
    ImageDraw.Draw(mask).polygon(ARM_POLYGON, fill=255)
    return mask


def shifted_array(source: np.ndarray, dx: int, dy: int) -> np.ndarray:
    shifted = np.zeros_like(source)
    source_x0 = max(0, -dx)
    source_x1 = min(CANVAS_SIZE[0], CANVAS_SIZE[0] - dx)
    source_y0 = max(0, -dy)
    source_y1 = min(CANVAS_SIZE[1], CANVAS_SIZE[1] - dy)
    target_x0 = source_x0 + dx
    target_x1 = source_x1 + dx
    target_y0 = source_y0 + dy
    target_y1 = source_y1 + dy
    shifted[target_y0:target_y1, target_x0:target_x1] = source[
        source_y0:source_y1, source_x0:source_x1
    ]
    return shifted


def registration_offset(
    canonical: np.ndarray,
    source: np.ndarray,
    animated_mask: np.ndarray,
) -> tuple[int, int, float]:
    best = (0, 0, float("inf"))
    stable_region = ~animated_mask
    canonical_alpha = canonical[:, :, 3] > 0
    for dy in range(-7, 8):
        for dx in range(-7, 8):
            shifted = shifted_array(source, dx, dy)
            shifted_alpha = shifted[:, :, 3] > 0
            sample = stable_region & (canonical_alpha | shifted_alpha)
            delta = np.abs(
                canonical.astype(np.int16) - shifted.astype(np.int16)
            ).sum(axis=2)
            score = float(delta[sample].mean()) if sample.any() else float("inf")
            if score < best[2]:
                best = (dx, dy, score)
    return best


def stabilize(frames: list[Image.Image]) -> tuple[list[Image.Image], list[tuple[int, int, float]]]:
    canonical = np.asarray(frames[0], dtype=np.uint8)
    mask_image = polygon_mask()
    mask = np.asarray(mask_image, dtype=np.uint8) > 0
    outputs = [frames[0].copy()]
    offsets = [(0, 0, 0.0)]
    for source_image in frames[1:]:
        source = np.asarray(source_image, dtype=np.uint8)
        dx, dy, score = registration_offset(canonical, source, mask)
        shifted = shifted_array(source, dx, dy)
        composed = canonical.copy()
        composed[mask] = shifted[mask]
        outputs.append(Image.fromarray(composed, "RGBA"))
        offsets.append((dx, dy, score))
    return outputs, offsets


def sha256(image: Image.Image) -> str:
    return hashlib.sha256(image.tobytes()).hexdigest().upper()


def checker(size: tuple[int, int]) -> Image.Image:
    result = Image.new("RGBA", size, (215, 222, 224, 255))
    draw = ImageDraw.Draw(result)
    step = 16
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            if ((x // step) + (y // step)) % 2:
                draw.rectangle((x, y, x + step - 1, y + step - 1), fill=(184, 194, 197, 255))
    return result


def write_qa(path: Path, outputs: list[Image.Image], offsets: list[tuple[int, int, float]]) -> None:
    scale = 2
    cell_w = CANVAS_SIZE[0] * scale
    cell_h = CANVAS_SIZE[1] * scale + 34
    sheet = Image.new("RGBA", (cell_w * FRAME_COUNT, cell_h), (28, 34, 38, 255))
    draw = ImageDraw.Draw(sheet)
    for index, image in enumerate(outputs):
        background = checker(CANVAS_SIZE)
        background.alpha_composite(image)
        background = background.resize((cell_w, CANVAS_SIZE[1] * scale), Image.Resampling.NEAREST)
        x = index * cell_w
        sheet.alpha_composite(background, (x, 0))
        dx, dy, score = offsets[index]
        draw.text((x + 8, CANVAS_SIZE[1] * scale + 8), f"frame {index}  register {dx:+d},{dy:+d}  score {score:.1f}", fill=(245, 245, 245, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path, quality=95)


def main() -> int:
    args = parse_args()
    frames = load_frames(args.frames_dir)
    outputs, offsets = stabilize(frames)
    for index, (image, offset) in enumerate(zip(outputs, offsets)):
        print(
            f"frame={index} offset={offset[0]:+d},{offset[1]:+d} "
            f"score={offset[2]:.2f} rgba_sha256={sha256(image)}"
        )
    if args.qa_output:
        write_qa(args.qa_output, outputs, offsets)
        print(f"qa={args.qa_output}")
    if args.apply:
        for index, image in enumerate(outputs):
            image.save(frame_path(args.frames_dir, index), optimize=True)
        print("applied=1")
    else:
        print("applied=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
