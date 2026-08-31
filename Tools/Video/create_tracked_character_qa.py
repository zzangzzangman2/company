#!/usr/bin/env python3
"""Create pixel-tracked close-up QA artifacts from a fixed-camera frame sequence."""

from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path

import cv2
import numpy as np


def frame_number(path: Path) -> int:
    match = re.search(r"(\d+)$", path.stem)
    if not match:
        raise ValueError(f"Frame filename has no trailing number: {path.name}")
    return int(match.group(1))


def clamp_crop(center_x: float, center_y: float, width: int, height: int, image_shape):
    image_height, image_width = image_shape[:2]
    x0 = int(round(center_x - width / 2))
    y0 = int(round(center_y - height / 2))
    x0 = max(0, min(x0, image_width - width))
    y0 = max(0, min(y0, image_height - height))
    return x0, y0, x0 + width, y0 + height


def detect_centers(frames: list[np.ndarray]) -> tuple[list[tuple[float, float]], list[tuple[int, int, int, int]]]:
    sample = np.stack(frames, axis=0)
    background = np.median(sample, axis=0).astype(np.uint8)
    raw_centers: list[tuple[float, float] | None] = []
    raw_boxes: list[tuple[int, int, int, int] | None] = []
    previous: tuple[float, float] | None = None

    for frame in frames:
        delta = cv2.absdiff(frame, background)
        gray = np.max(delta, axis=2).astype(np.uint8)
        mask = np.where(gray >= 24, 255, 0).astype(np.uint8)
        mask[:36, :] = 0
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=2)
        mask = cv2.dilate(mask, np.ones((5, 5), np.uint8), iterations=1)
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        candidates = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            area = cv2.contourArea(contour)
            if area < 30 or w < 7 or h < 14 or w > 160 or h > 220:
                continue
            cx = x + w / 2
            cy = y + h / 2
            compactness = area / max(1.0, w * h)
            size_score = min(area, 2500.0) * max(0.25, compactness)
            if previous is None:
                # Starter-office proofs keep the walking candidate below the stationary family
                # lineup. Prefer the lower moving silhouette for the seed so an idling NPC at the
                # top of the map cannot steal frames 0..1 before temporal tracking takes over.
                score = size_score + cy * 3.0
            else:
                distance = np.hypot(cx - previous[0], cy - previous[1])
                score = size_score - distance * 2.0
            candidates.append((score, cx, cy, (x, y, w, h)))

        if candidates:
            _, cx, cy, box = max(candidates, key=lambda item: item[0])
            previous = (cx, cy)
            raw_centers.append(previous)
            raw_boxes.append(box)
        else:
            raw_centers.append(None)
            raw_boxes.append(None)

    valid_indices = [index for index, center in enumerate(raw_centers) if center is not None]
    if not valid_indices:
        raise RuntimeError("Could not locate a moving subject in any frame")

    center_x = np.interp(
        np.arange(len(frames)), valid_indices, [raw_centers[index][0] for index in valid_indices]
    )
    center_y = np.interp(
        np.arange(len(frames)), valid_indices, [raw_centers[index][1] for index in valid_indices]
    )
    center_x = cv2.GaussianBlur(center_x.reshape(-1, 1), (1, 7), 0).reshape(-1)
    center_y = cv2.GaussianBlur(center_y.reshape(-1, 1), (1, 7), 0).reshape(-1)

    centers = list(zip(center_x.tolist(), center_y.tolist()))
    boxes = [box if box is not None else (0, 0, 0, 0) for box in raw_boxes]
    return centers, boxes


def write_contact_sheets(crops: list[np.ndarray], output_dir: Path, columns: int, rows: int) -> None:
    cell_width, cell_height = 320, 360
    page_size = columns * rows
    for page_start in range(0, len(crops), page_size):
        page = np.zeros((cell_height * rows, cell_width * columns, 3), dtype=np.uint8)
        page[:] = (20, 20, 20)
        for local_index, crop in enumerate(crops[page_start : page_start + page_size]):
            row, column = divmod(local_index, columns)
            resized = cv2.resize(crop, (cell_width, cell_height), interpolation=cv2.INTER_LANCZOS4)
            page[row * cell_height : (row + 1) * cell_height, column * cell_width : (column + 1) * cell_width] = resized
            frame_index = page_start + local_index
            cv2.putText(
                page,
                f"frame {frame_index:03d}",
                (column * cell_width + 8, row * cell_height + 24),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.65,
                (0, 255, 255),
                2,
                cv2.LINE_AA,
            )
        page_number = page_start // page_size + 1
        cv2.imwrite(str(output_dir / f"tracked-contact-{page_number:02d}.png"), page)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("frames_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--pattern", default="*.png")
    parser.add_argument("--crop-width", type=int, default=180)
    parser.add_argument("--crop-height", type=int, default=210)
    args = parser.parse_args()

    paths = sorted(args.frames_dir.glob(args.pattern), key=frame_number)
    if not paths:
        raise FileNotFoundError(f"No frames matched {args.pattern} in {args.frames_dir}")
    frames = [cv2.imread(str(path), cv2.IMREAD_COLOR) for path in paths]
    if any(frame is None for frame in frames):
        raise RuntimeError("One or more frames could not be decoded")
    if len({frame.shape for frame in frames}) != 1:
        raise RuntimeError("Frame dimensions are inconsistent")

    args.output_dir.mkdir(parents=True, exist_ok=True)
    tracked_dir = args.output_dir / "tracked-frames"
    tracked_dir.mkdir(exist_ok=True)
    centers, boxes = detect_centers(frames)
    crops = []

    with (args.output_dir / "tracked-centers.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["frame", "source", "centerX", "centerY", "detectedX", "detectedY", "detectedW", "detectedH"])
        for index, (path, frame, center, box) in enumerate(zip(paths, frames, centers, boxes)):
            x0, y0, x1, y1 = clamp_crop(
                center[0], center[1], args.crop_width, args.crop_height, frame.shape
            )
            crop = frame[y0:y1, x0:x1].copy()
            crops.append(crop)
            cv2.imwrite(str(tracked_dir / f"tracked-{index:03d}.png"), crop)
            writer.writerow([index, path.name, f"{center[0]:.3f}", f"{center[1]:.3f}", *box])

    write_contact_sheets(crops, args.output_dir, columns=5, rows=5)
    print(f"FRAMES={len(frames)}")
    print(f"TRACKED_DIR={tracked_dir}")
    print(f"CONTACT_SHEETS={(len(crops) + 24) // 25}")


if __name__ == "__main__":
    main()
