from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def components(mask: np.ndarray) -> list[int]:
    visited = np.zeros(mask.shape, dtype=bool)
    sizes: list[int] = []
    height, width = mask.shape
    for start_row, start_column in zip(*np.where(mask & ~visited)):
        if visited[start_row, start_column]:
            continue
        queue: deque[tuple[int, int]] = deque([(int(start_row), int(start_column))])
        visited[start_row, start_column] = True
        size = 0
        while queue:
            row, column = queue.popleft()
            size += 1
            for next_row in range(max(0, row - 1), min(height, row + 2)):
                for next_column in range(max(0, column - 1), min(width, column + 2)):
                    if not mask[next_row, next_column] or visited[next_row, next_column]:
                        continue
                    visited[next_row, next_column] = True
                    queue.append((next_row, next_column))
        sizes.append(size)
    return sorted(sizes, reverse=True)


def marker_masks(pixels: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    rgb = pixels[:, :, :3].astype(np.int16)
    alpha = pixels[:, :, 3] > 0
    cyan = alpha & (rgb[:, :, 0] <= 40) & (rgb[:, :, 1] >= 180) & (rgb[:, :, 2] >= 180)
    magenta = alpha & (rgb[:, :, 0] >= 180) & (rgb[:, :, 1] <= 40) & (rgb[:, :, 2] >= 110)
    return cyan, magenta


def shipping_palette_violations(
    pixels: np.ndarray,
    member: str,
    anatomy_owned: np.ndarray,
    ground: int,
) -> np.ndarray:
    """Prove owned shipping pixels came from the clean renderer, not marker-paint cleanup.

    Hue bands cannot distinguish marker residue from the father's teal shirt, the mother's teal
    skirt, or the player's canonical navy trousers. The clean renderer has a stronger invariant:
    every visible anatomy pixel lies on one of two fixed canonical palette segments. Any copied
    cyan/magenta pixel, including a dark antialiased fringe, is off both member-specific segments.
    """
    body_palettes = {
        "mother": ((108, 58, 43), (246, 185, 148)),
        "older_sister": ((108, 58, 43), (246, 185, 148)),
        "father": ((47, 45, 48), (108, 103, 104)),
        "player": ((20, 31, 68), (56, 77, 132)),
    }
    shoe_palettes = {
        "mother": ((48, 21, 13), (128, 74, 40)),
        "older_sister": ((108, 58, 43), (246, 185, 148)),
        "father": ((48, 21, 13), (128, 74, 40)),
        "player": ((92, 84, 82), (245, 240, 230)),
    }
    rgb = pixels[:, :, :3].astype(np.float32)
    rows = np.indices(anatomy_owned.shape)[0]
    shoe_band = rows >= ground - 10
    violations = np.zeros(anatomy_owned.shape, dtype=bool)
    for region, palettes in ((~shoe_band, body_palettes), (shoe_band, shoe_palettes)):
        selected = anatomy_owned & region
        if not selected.any():
            continue
        dark = np.asarray(palettes[member][0], dtype=np.float32)
        light = np.asarray(palettes[member][1], dtype=np.float32)
        vector = light - dark
        denominator = float(np.dot(vector, vector))
        amount = np.clip(((rgb - dark) * vector).sum(axis=2) / denominator, 0.0, 1.0)
        closest = dark + amount[:, :, None] * vector
        distance = np.abs(rgb - closest).max(axis=2)
        violations |= selected & (distance > 1.1)
    return violations


def marker_foot(mask: np.ndarray) -> tuple[float, int]:
    rows, _ = np.where(mask)
    bottom = int(rows.max())
    foot = mask.copy()
    foot[: max(0, bottom - 13)] = False
    _, columns = np.where(foot)
    return float(columns.mean()), bottom


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--prefix", required=True)
    parser.add_argument("--marker-directory", required=True, type=Path)
    parser.add_argument("--lower-start", required=True, type=int)
    parser.add_argument("--max-stride-head", type=float, default=0.55)
    parser.add_argument("--max-lift", type=int, default=2)
    args = parser.parse_args()
    member = "older_sister" if args.prefix.startswith("older_sister_") else args.prefix.split("_", 1)[0]

    frames = [
        np.asarray(Image.open(args.directory / f"{args.prefix}_{phase}.png").convert("RGBA"))
        for phase in range(6)
    ]
    markers = [
        np.asarray(Image.open(args.marker_directory / f"{args.prefix}_{phase}.png").convert("RGBA"))
        for phase in range(6)
    ]
    failures: list[str] = []

    expected_size = frames[0].shape
    if expected_size != (256, 256, 4) or any(frame.shape != expected_size for frame in frames):
        failures.append(f"dimensions={','.join(str(frame.shape) for frame in frames)}")

    upper_reference = frames[0][: args.lower_start]
    upper_delta = [int(np.count_nonzero(frame[: args.lower_start] != upper_reference)) for frame in frames]
    if any(upper_delta):
        failures.append(f"upper-rgba-delta={upper_delta}")

    bboxes: list[tuple[int, int, int, int]] = []
    grounds: list[int] = []
    chroma_counts: list[int] = []
    fragment_counts: list[int] = []
    for phase, frame in enumerate(frames):
        alpha_values = set(int(value) for value in np.unique(frame[:, :, 3]))
        if not alpha_values.issubset({0, 255}):
            failures.append(f"phase-{phase}-alpha={sorted(alpha_values)}")
        alpha = frame[:, :, 3] > 0
        rows, columns = np.where(alpha)
        bboxes.append((int(columns.min()), int(rows.min()), int(columns.max()) + 1, int(rows.max()) + 1))
        grounds.append(int(rows.max()))
        sizes = components(alpha)
        fragment_counts.append(max(0, len(sizes) - 1))
        if len(sizes) != 1:
            failures.append(f"phase-{phase}-alpha-components={sizes[:8]}")
        marker_pixels = np.asarray(
            Image.open(args.marker_directory / f"{args.prefix}_{phase}.png").convert("RGBA")
        )
        marker_cyan, marker_magenta = marker_masks(marker_pixels)
        anatomy_owned = marker_cyan | marker_magenta
        residue = shipping_palette_violations(frame, member, anatomy_owned, int(rows.max()))
        chroma_counts.append(int(residue[args.lower_start :].sum()))
        if chroma_counts[-1] != 0:
            failures.append(f"phase-{phase}-final-chroma={chroma_counts[-1]}")

    # A correct lower-body reflection moves the feet to opposite horizontal sides while the upper
    # body deliberately stays unmirrored.  Requiring identical left/right full-body bounds would
    # therefore reject a real two-step cycle whenever a foot extends beyond an asymmetric sleeve,
    # hair, or side-profile torso.  Lock the vertical silhouette here; horizontal body stability is
    # already enforced more strongly by the exact upper RGBA check and the stride/head limit.
    vertical_bboxes = [(bbox[1], bbox[3]) for bbox in bboxes]
    if len(set(vertical_bboxes)) != 1:
        failures.append(f"full-body-vertical-bbox={vertical_bboxes}")
    if len(set(grounds)) != 1:
        failures.append(f"ground={grounds}")

    alpha0 = frames[0][:, :, 3] > 0
    rows0, _ = np.where(alpha0)
    top = int(rows0.min())
    bottom = int(rows0.max()) + 1
    head_end = top + round((bottom - top) * 0.22)
    _, head_columns = np.where(alpha0[top:head_end])
    head_width = int(head_columns.max() - head_columns.min() + 1)

    marker_stats: list[dict[str, tuple[float, int]]] = []
    for phase, marker in enumerate(markers):
        cyan, magenta = marker_masks(marker)
        if int(cyan.sum()) < 30 or int(magenta.sum()) < 30:
            failures.append(f"phase-{phase}-marker-count={int(cyan.sum())}/{int(magenta.sum())}")
            continue
        marker_stats.append({"cyan": marker_foot(cyan), "magenta": marker_foot(magenta)})

    max_stride = 0.0
    lifts: list[tuple[int, int]] = []
    if len(marker_stats) == 6:
        for phase, stats in enumerate(marker_stats):
            max_stride = max(max_stride, abs(stats["cyan"][0] - stats["magenta"][0]))
            lifts.append((grounds[phase] - stats["cyan"][1], grounds[phase] - stats["magenta"][1]))
        stride_ratio = max_stride / head_width
        if stride_ratio > args.max_stride_head:
            failures.append(f"stride/head={stride_ratio:.3f}>{args.max_stride_head:.3f}")
        if any(max(pair) > args.max_lift for pair in lifts):
            failures.append(f"foot-lift={lifts}>{args.max_lift}")
    else:
        stride_ratio = float("nan")

    print(
        "ART_METRICS "
        f"upper-rgba-delta={upper_delta} bbox={bboxes} ground={grounds} "
        f"alpha-fragments={fragment_counts} final-chroma={chroma_counts} "
        f"head-width={head_width} max-stride={max_stride:.2f} stride/head={stride_ratio:.3f} "
        f"marker-foot-lifts={lifts}"
    )
    if failures:
        print("FAMILY_WALK_CANDIDATE_ART: FAIL | " + " | ".join(failures))
        raise SystemExit(1)
    print("FAMILY_WALK_CANDIDATE_ART: PASS")


if __name__ == "__main__":
    main()
