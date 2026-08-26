import argparse
import re
from pathlib import Path

import cv2
import numpy as np


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--crop-size", type=int, default=200)
    parser.add_argument("--threshold", type=int, default=18)
    return parser.parse_args()


def index_of(path: Path) -> int:
    match = re.search(r"-(\d+)\.png$", path.name)
    if not match:
        raise ValueError(path)
    return int(match.group(1))


def main():
    args = parse_args()
    frames = sorted(
        (Path(args.runtime) / "frames").glob("*.png"),
        key=index_of,
    )
    if not frames:
        raise RuntimeError("No runtime frames found")
    output = Path(args.output)
    crop_root = output / "direct-tracked-crops"
    mask_root = output / "direct-tracked-masks"
    crop_root.mkdir(parents=True, exist_ok=True)
    mask_root.mkdir(parents=True, exist_ok=True)

    sample_indices = np.linspace(0, len(frames) - 1, 41, dtype=int)
    samples = [cv2.imread(str(frames[index]), cv2.IMREAD_COLOR) for index in sample_indices]
    background = np.median(np.stack(samples, axis=0), axis=0).astype(np.uint8)
    cv2.imwrite(str(output / "direct-tracked-background.png"), background)

    height, width = background.shape[:2]
    half = args.crop_size // 2
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
    previous = None
    centers = []
    for path in frames:
        frame = cv2.imread(str(path), cv2.IMREAD_COLOR)
        delta = cv2.absdiff(frame, background).max(axis=2)
        mask = (delta >= args.threshold).astype(np.uint8) * 255
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((2, 2), np.uint8))
        mask = cv2.dilate(mask, kernel, iterations=2)
        count, labels, stats, centroids = cv2.connectedComponentsWithStats(mask, 8)

        candidates = []
        for component in range(1, count):
            x, y, w, h, area = stats[component]
            if area < 120 or area > 18000 or h < 22 or w < 12:
                continue
            cx, cy = centroids[component]
            # The only time-varying render layer is the 3D Father. Prefer a human-sized component;
            # temporal distance is only a tie-breaker near wall occlusion.
            shape_score = area - abs(h - 95) * 4 - abs(w - 65) * 3
            continuity = 0 if previous is None else np.hypot(cx - previous[0], cy - previous[1])
            candidates.append((shape_score - continuity * 2, area, cx, cy, component))
        if not candidates:
            raise RuntimeError(f"No moving Father component in {path.name}")
        _, _, cx, cy, component = max(candidates)
        previous = (float(cx), float(cy))
        centers.append(previous)

        x0 = int(round(cx)) - half
        y0 = int(round(cy)) - half
        x0 = max(0, min(width - args.crop_size, x0))
        y0 = max(0, min(height - args.crop_size, y0))
        crop = frame[y0 : y0 + args.crop_size, x0 : x0 + args.crop_size]
        frame_index = index_of(path)
        cv2.imwrite(str(crop_root / f"frame-{frame_index:03d}.png"), crop)
        if frame_index % 30 == 0:
            diagnostic = np.zeros_like(mask)
            diagnostic[labels == component] = 255
            cv2.imwrite(str(mask_root / f"mask-{frame_index:03d}.png"), diagnostic)

    steps = [
        np.hypot(centers[index][0] - centers[index - 1][0], centers[index][1] - centers[index - 1][1])
        for index in range(1, len(centers))
    ]
    print(
        "DIRECT_FATHER_TRACK=PASS "
        f"frames={len(frames)} maxCenterStep={max(steps):.3f} meanCenterStep={np.mean(steps):.3f}"
    )


if __name__ == "__main__":
    main()
