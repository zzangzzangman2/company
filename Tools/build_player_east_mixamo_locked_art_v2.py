from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


FRAME_NAMES = [f"player_east_walk_{index}_v2.png" for index in range(6)]
CANVAS = (256, 256)
LOWER_CUT_Y = 171
GREEN = (0, 255, 0, 255)

# REJECTED RESEARCH ONLY. These approximate donor controls were used to prove
# that warping existing raster lowers cannot satisfy the locked anatomy. Even P0
# and P1 are not production contact donors, and the low-pass draft is rejected.
# Keep this script only for reproducible failure evidence; never promote its art.
SOURCE_CONTROLS = {
    0: {
        "pelvis": (128, 174),
        "q0Hip": (126, 175),
        "q0Knee": (145, 198),
        "q0Ankle": (154, 219),
        "q0Heel": (150, 232),
        "q0Toe": (165, 232),
        "q3Hip": (130, 175),
        "q3Knee": (112, 198),
        "q3Ankle": (99, 217),
        "q3Heel": (92, 225),
        "q3Toe": (108, 232),
    },
    1: {
        "pelvis": (128, 175),
        "q1Hip": (126, 176),
        "q1Knee": (138, 199),
        "q1Ankle": (136, 222),
        "q1Heel": (128, 233),
        "q1Toe": (148, 233),
        "q4Hip": (130, 176),
        "q4Knee": (122, 198),
        "q4Ankle": (106, 212),
        "q4Heel": (100, 219),
        "q4Toe": (120, 224),
    },
    2: {
        "pelvis": (128, 173),
        "q2Hip": (126, 174),
        "q2Knee": (119, 198),
        "q2Ankle": (111, 219),
        "q2Heel": (103, 229),
        "q2Toe": (125, 232),
        "q5Hip": (130, 174),
        "q5Knee": (143, 196),
        "q5Ankle": (151, 217),
        "q5Heel": (145, 224),
        "q5Toe": (161, 228),
    },
}


def connected_components(binary: np.ndarray) -> list[list[tuple[int, int]]]:
    height, width = binary.shape
    seen = np.zeros_like(binary, dtype=bool)
    output: list[list[tuple[int, int]]] = []
    for y in range(height):
        for x in range(width):
            if not binary[y, x] or seen[y, x]:
                continue
            seen[y, x] = True
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                component.append((px, py))
                for oy in (-1, 0, 1):
                    for ox in (-1, 0, 1):
                        if ox == 0 and oy == 0:
                            continue
                        nx, ny = px + ox, py + oy
                        if (
                            0 <= nx < width
                            and 0 <= ny < height
                            and binary[ny, nx]
                            and not seen[ny, nx]
                        ):
                            seen[ny, nx] = True
                            queue.append((nx, ny))
            output.append(component)
    return output


def lower_body_mask(frame: Image.Image, cut_y: int = LOWER_CUT_Y) -> np.ndarray:
    alpha = np.asarray(frame.convert("RGBA"))[:, :, 3]
    binary = alpha > 0
    binary[:cut_y] = False
    output = np.zeros_like(binary)
    for component in connected_components(binary):
        if len(component) < 20 or max(y for _, y in component) < 218:
            continue
        for x, y in component:
            output[y, x] = True
    return output


def mask_center_x(mask: np.ndarray, y0: int = 171, y1: int = 181) -> float:
    ys, xs = np.nonzero(mask[y0:y1])
    if len(xs) == 0:
        return 128.0
    return float(xs.mean())


def shift_rgba(frame: Image.Image, dx: int, dy: int = 0) -> Image.Image:
    source = np.asarray(frame.convert("RGBA"))
    output = np.zeros_like(source)
    src_x0 = max(0, -dx)
    src_x1 = min(source.shape[1], source.shape[1] - dx)
    src_y0 = max(0, -dy)
    src_y1 = min(source.shape[0], source.shape[0] - dy)
    dst_x0 = src_x0 + dx
    dst_x1 = src_x1 + dx
    dst_y0 = src_y0 + dy
    dst_y1 = src_y1 + dy
    if src_x1 > src_x0 and src_y1 > src_y0:
        output[dst_y0:dst_y1, dst_x0:dst_x1] = source[src_y0:src_y1, src_x0:src_x1]
    return Image.fromarray(output, "RGBA")


def aligned_frame_and_lower(frame: Image.Image) -> tuple[Image.Image, Image.Image, int]:
    mask = lower_body_mask(frame)
    dx = round(128.0 - mask_center_x(mask))
    aligned = shift_rgba(frame, dx)
    aligned_rgba = np.asarray(aligned).copy()
    aligned_mask = lower_body_mask(aligned)
    lower = np.zeros_like(aligned_rgba)
    lower[aligned_mask] = aligned_rgba[aligned_mask]
    upper = aligned_rgba.copy()
    upper[aligned_mask] = 0
    return Image.fromarray(upper, "RGBA"), Image.fromarray(lower, "RGBA"), dx


def align_transparent_lower(frame: Image.Image) -> tuple[Image.Image, int]:
    mask = lower_body_mask(frame)
    dx = round(128.0 - mask_center_x(mask))
    aligned = shift_rgba(frame, dx)
    rgba = np.asarray(aligned).copy()
    keep = lower_body_mask(aligned)
    rgba[~keep] = 0
    return Image.fromarray(rgba, "RGBA"), dx


def target_controls(target_rows: list[dict], phase_family: int) -> dict[str, tuple[int, int]]:
    if phase_family == 0:
        row = target_rows[0]["points"]
        return {
            "pelvis": tuple(row["pelvis"]),
            "q0Hip": tuple(row["leftHip"]),
            "q0Knee": tuple(row["leftKnee"]),
            "q0Ankle": tuple(row["leftAnkle"]),
            "q0Heel": tuple(row["leftHeel"]),
            "q0Toe": tuple(row["leftToe"]),
            "q3Hip": tuple(row["rightHip"]),
            "q3Knee": tuple(row["rightKnee"]),
            "q3Ankle": tuple(row["rightAnkle"]),
            "q3Heel": tuple(row["rightHeel"]),
            "q3Toe": tuple(row["rightToe"]),
        }
    if phase_family == 1:
        row = target_rows[1]["points"]
        return {
            "pelvis": tuple(row["pelvis"]),
            "q1Hip": tuple(row["leftHip"]),
            "q1Knee": tuple(row["leftKnee"]),
            "q1Ankle": tuple(row["leftAnkle"]),
            "q1Heel": tuple(row["leftHeel"]),
            "q1Toe": tuple(row["leftToe"]),
            "q4Hip": tuple(row["rightHip"]),
            "q4Knee": tuple(row["rightKnee"]),
            "q4Ankle": tuple(row["rightAnkle"]),
            "q4Heel": tuple(row["rightHeel"]),
            "q4Toe": tuple(row["rightToe"]),
        }
    row = target_rows[2]["points"]
    return {
        "pelvis": tuple(row["pelvis"]),
        "q2Hip": tuple(row["leftHip"]),
        "q2Knee": tuple(row["leftKnee"]),
        "q2Ankle": tuple(row["leftAnkle"]),
        "q2Heel": tuple(row["leftHeel"]),
        "q2Toe": tuple(row["leftToe"]),
        "q5Hip": tuple(row["rightHip"]),
        "q5Knee": tuple(row["rightKnee"]),
        "q5Ankle": tuple(row["rightAnkle"]),
        "q5Heel": tuple(row["rightHeel"]),
        "q5Toe": tuple(row["rightToe"]),
    }


def idw_warp(
    source: Image.Image,
    source_controls: dict[str, tuple[int, int]],
    destination_controls: dict[str, tuple[int, int]],
    power: float = 2.75,
) -> Image.Image:
    keys = list(source_controls)
    if set(keys) != set(destination_controls):
        raise ValueError("Source/destination control names differ")
    src = np.asarray(source.convert("RGBA"))
    src_points = np.asarray([source_controls[key] for key in keys], dtype=np.float32)
    dst_points = np.asarray([destination_controls[key] for key in keys], dtype=np.float32)
    yy, xx = np.mgrid[0 : src.shape[0], 0 : src.shape[1]].astype(np.float32)
    displacement_x = np.zeros_like(xx)
    displacement_y = np.zeros_like(yy)
    weight_sum = np.zeros_like(xx)
    for src_point, dst_point in zip(src_points, dst_points):
        distance_sq = (xx - dst_point[0]) ** 2 + (yy - dst_point[1]) ** 2
        weight = 1.0 / np.maximum(distance_sq, 0.0001) ** (power * 0.5)
        displacement_x += weight * (src_point[0] - dst_point[0])
        displacement_y += weight * (src_point[1] - dst_point[1])
        weight_sum += weight
    map_x = xx + displacement_x / weight_sum
    map_y = yy + displacement_y / weight_sum
    warped = cv2.remap(
        src,
        map_x,
        map_y,
        interpolation=cv2.INTER_NEAREST,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=(0, 0, 0, 0),
    )
    warped[warped[:, :, 3] < 128] = 0
    warped[warped[:, :, 3] >= 128, 3] = 255
    return Image.fromarray(warped, "RGBA")


def point_segment_distance_sq(
    xx: np.ndarray,
    yy: np.ndarray,
    start: tuple[int, int],
    end: tuple[int, int],
) -> np.ndarray:
    ax, ay = float(start[0]), float(start[1])
    bx, by = float(end[0]), float(end[1])
    vx, vy = bx - ax, by - ay
    denominator = max(vx * vx + vy * vy, 0.0001)
    t = np.clip(((xx - ax) * vx + (yy - ay) * vy) / denominator, 0.0, 1.0)
    dx = xx - (ax + t * vx)
    dy = yy - (ay + t * vy)
    return dx * dx + dy * dy


def owner_distance_sq(
    controls: dict[str, tuple[int, int]], prefix: str, shape: tuple[int, int]
) -> np.ndarray:
    yy, xx = np.mgrid[0 : shape[0], 0 : shape[1]].astype(np.float32)
    joints = [
        controls[prefix + "Hip"],
        controls[prefix + "Knee"],
        controls[prefix + "Ankle"],
        controls[prefix + "Toe"],
    ]
    distances = [
        point_segment_distance_sq(xx, yy, joints[index], joints[index + 1])
        for index in range(len(joints) - 1)
    ]
    distances.append(
        point_segment_distance_sq(
            xx,
            yy,
            controls[prefix + "Heel"],
            controls[prefix + "Toe"],
        )
    )
    return np.minimum.reduce(distances)


def masked_rgba(source: Image.Image, mask: np.ndarray) -> Image.Image:
    rgba = np.asarray(source.convert("RGBA")).copy()
    rgba[~mask] = 0
    return Image.fromarray(rgba, "RGBA")


def warp_phase_family(
    source: Image.Image,
    source_controls: dict[str, tuple[int, int]],
    destination_controls: dict[str, tuple[int, int]],
    prefixes: tuple[str, str],
) -> tuple[Image.Image, Image.Image, Image.Image]:
    rgba = np.asarray(source.convert("RGBA"))
    alpha = rgba[:, :, 3] > 0
    distance_a = owner_distance_sq(source_controls, prefixes[0], alpha.shape)
    distance_b = owner_distance_sq(source_controls, prefixes[1], alpha.shape)
    yy = np.indices(alpha.shape)[0]

    # Keep the pelvis/crotch as one east-authored piece. Each complete leg then
    # starts underneath it and is deformed only by its own hip-to-shoe controls,
    # preventing the opposite leg from pulling a shin into an S-curve.
    shared_mask = alpha & (yy < 186)
    owner_a_mask = alpha & (yy >= 181) & (distance_a <= distance_b)
    owner_b_mask = alpha & (yy >= 181) & (distance_b < distance_a)
    shared = masked_rgba(source, shared_mask)

    def warp_owner(prefix: str, mask: np.ndarray) -> Image.Image:
        keys = [
            "pelvis",
            prefix + "Hip",
            prefix + "Knee",
            prefix + "Ankle",
            prefix + "Heel",
            prefix + "Toe",
        ]
        src = {key: source_controls[key] for key in keys}
        dst = {key: destination_controls[key] for key in keys}
        return idw_warp(masked_rgba(source, mask), src, dst, power=3.25)

    return shared, warp_owner(prefixes[0], owner_a_mask), warp_owner(prefixes[1], owner_b_mask)


def compose_lower_parts(
    shared: Image.Image,
    first: Image.Image,
    second: Image.Image,
) -> Image.Image:
    output = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    output.alpha_composite(first)
    output.alpha_composite(second)
    output.alpha_composite(shared)
    return output


def alpha_composite(base: Image.Image, overlay: Image.Image) -> Image.Image:
    output = base.convert("RGBA").copy()
    output.alpha_composite(overlay.convert("RGBA"))
    return output


def flatten(frame: Image.Image, background: tuple[int, int, int, int]) -> Image.Image:
    output = Image.new("RGBA", frame.size, background)
    output.alpha_composite(frame.convert("RGBA"))
    return output


def write_sheet(
    frames: list[Image.Image], path: Path, background: tuple[int, int, int, int]
) -> None:
    sheet = Image.new("RGBA", (768, 512), background)
    for index, frame in enumerate(frames):
        sheet.alpha_composite(flatten(frame, background), ((index % 3) * 256, (index // 3) * 256))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def write_gif(frames: list[Image.Image], path: Path, background: tuple[int, int, int, int]) -> None:
    rendered = [flatten(frame, background).convert("RGB") for frame in frames]
    rendered[0].save(
        path,
        save_all=True,
        append_images=rendered[1:],
        duration=133,
        loop=0,
        disposal=2,
    )


def image_metrics(frame: Image.Image) -> dict:
    alpha = np.asarray(frame.convert("RGBA"))[:, :, 3] > 0
    ys, xs = np.nonzero(alpha)
    lower = alpha.copy()
    lower[:LOWER_CUT_Y] = False
    lower_ys, lower_xs = np.nonzero(lower)
    floor_xs = np.nonzero(lower[233])[0]
    return {
        "bbox": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
        "lowerBbox": [
            int(lower_xs.min()),
            int(lower_ys.min()),
            int(lower_xs.max()),
            int(lower_ys.max()),
        ],
        "bottom": int(lower_ys.max()),
        "floorRunMinX": int(floor_xs.min()) if len(floor_xs) else None,
        "floorRunMaxX": int(floor_xs.max()) if len(floor_xs) else None,
    }


def build(
    source_dir: Path,
    low_pass_donor: Path,
    target_json: Path,
    output_dir: Path,
) -> None:
    target_rows = json.loads(target_json.read_text(encoding="utf-8"))
    source_frames = [Image.open(source_dir / name).convert("RGBA") for name in FRAME_NAMES]
    aligned_upper: list[Image.Image] = []
    clean_lowers: list[Image.Image] = []
    source_alignment_dx: list[int] = []
    for frame in source_frames:
        upper, lower, dx = aligned_frame_and_lower(frame)
        aligned_upper.append(upper)
        clean_lowers.append(lower)
        source_alignment_dx.append(dx)

    low_pass, low_pass_dx = align_transparent_lower(Image.open(low_pass_donor).convert("RGBA"))
    donors = [clean_lowers[0], clean_lowers[1], low_pass]
    family_prefixes = [("q0", "q3"), ("q1", "q4"), ("q2", "q5")]
    warped_parts = [
        warp_phase_family(
            donors[index],
            SOURCE_CONTROLS[index],
            target_controls(target_rows, index),
            family_prefixes[index],
        )
        for index in range(3)
    ]

    frames: list[Image.Image] = []
    frames_dir = output_dir / "Frames"
    lowers_dir = output_dir / "LockedLowers"
    frames_dir.mkdir(parents=True, exist_ok=True)
    lowers_dir.mkdir(parents=True, exist_ok=True)
    for index in range(6):
        shared, owner_a, owner_b = warped_parts[index % 3]
        # Physical left is the near/front limb. The phase-neutral q owner changes
        # at the half-cycle, so the layer order swaps without mirroring geometry.
        lower = (
            compose_lower_parts(shared, owner_b, owner_a)
            if index < 3
            else compose_lower_parts(shared, owner_a, owner_b)
        )
        frame = alpha_composite(lower, aligned_upper[index])
        frame.save(frames_dir / FRAME_NAMES[index])
        lower.save(lowers_dir / f"p{index}.png")
        frames.append(frame)

    write_sheet(frames, output_dir / "contact-sheet-black.png", (0, 0, 0, 255))
    write_sheet(frames, output_dir / "contact-sheet-green.png", GREEN)
    write_sheet(frames, output_dir / "contact-sheet-transparent.png", (0, 0, 0, 0))
    write_gif(frames, output_dir / "east-walk-black.gif", (0, 0, 0, 255))
    write_gif(frames, output_dir / "east-walk-green.gif", GREEN)

    receipt = {
        "contract": "FC-PLAYER-EAST-MIXAMO-LOCKED-ART-V2-REJECTED-RESEARCH",
        "status": "REJECTED_RESEARCH",
        "rejectionReason": (
            "Existing raster lower-body donors cannot preserve a coherent east-facing "
            "pelvis-to-toe chain; P2/P5 cross and bend unnaturally."
        ),
        "sourceUpperFrames": str(source_dir).replace("\\", "/"),
        "phaseDonors": {
            "contact": str(source_dir / FRAME_NAMES[0]).replace("\\", "/"),
            "loadRecovery": str(source_dir / FRAME_NAMES[1]).replace("\\", "/"),
            "terminalLowPass": str(low_pass_donor).replace("\\", "/"),
        },
        "targetJoints": str(target_json).replace("\\", "/"),
        "sourceAlignmentDx": source_alignment_dx,
        "lowPassAlignmentDx": low_pass_dx,
        "lowerBodyMirrorUsed": False,
        "isolatedShoeMoveUsed": False,
        "wholeHipToShoeWarp": True,
        "rootAdvanceSourcePx": 0.99380799 / 6.0 * 180.0 / 1.55,
        "frameDurationMs": 133,
        "metrics": [dict(pose=index, **image_metrics(frame)) for index, frame in enumerate(frames)],
    }
    (output_dir / "receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--low-pass-donor", type=Path, required=True)
    parser.add_argument("--target-json", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--allow-rejected-research",
        action="store_true",
        help="Explicitly reproduce rejected evidence; output must never be promoted.",
    )
    args = parser.parse_args()
    if not args.allow_rejected_research:
        parser.error(
            "This builder is REJECTED_RESEARCH. Pass --allow-rejected-research "
            "only to reproduce failure evidence."
        )
    build(args.source_dir, args.low_pass_donor, args.target_json, args.output_dir)


if __name__ == "__main__":
    main()
