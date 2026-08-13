#!/usr/bin/env python3
"""Build coherent 8-direction walk cycles without redrawing character identity.

The source HighMotion sheets are read-only.  Each direction keeps frame zero's
approved head, torso, hair, clothing and arms as one canonical body.  The tool
evaluates all 15 pairs of source-authored leg-contact poses, selects the pair
that produces the strongest valid loop, and makes four crisp in-betweens by
translating only the leg/foot silhouettes.  It never cross-dissolves, scales,
rotates or resamples source pixels.

The default mode writes a review candidate under Artifacts.  ``--apply`` is an
explicit promotion step: it first backs up the 24 canonical source sheets,
then replaces sheet pixels and frame PNGs at their existing paths while never
touching Unity .meta files or their GUIDs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import shutil
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin


CANVAS_SIZE = (256, 256)
FRAME_COUNT = 6
DIRECTIONS = (
    "south", "southwest", "west", "northwest",
    "north", "northeast", "east", "southeast",
)
PART_DIRECTIONS = {"a": DIRECTIONS[:4], "b": DIRECTIONS[4:]}
WALK_RE = re.compile(
    r"^(?P<character>[a-z0-9_]+?)_(?P<direction>[a-z]+)_walk_(?P<phase>\d+)\.png$"
)
SHEET_LAYOUT_KEY = "familyCompanyHighMotionLayout"
SHEET_LAYOUT_VALUE = "grid-4x6-v1"
MAX_MEDIAN = 45.0
MAX_WORST = 60.0
# Opposite-pose contrast remains a useful coherence warning, but it must not reject a planted
# six-phase gait whose every adjacent lower-body transition already clears the strict motion gate.
MAX_RATIO = 0.95
MAX_FOOT_DRIFT = 1
MAX_STABLE_DRIFT = 1
MAX_NEW_VERTICAL_CRACK = 8
MIN_ADJACENT_MOTION = 0.18
MIN_PHASE_MOTION = 0.30
MAX_REPEATED_RUN = 1


@dataclass(frozen=True)
class CharacterSource:
    character_id: str
    root: Path
    frames_dir: Path
    sheets: dict[str, Path]


@dataclass
class StabilizedLoop:
    character_id: str
    direction: str
    source_paths: list[Path]
    output_frames: list[Image.Image]
    canonical_index: int
    endpoint_indices: tuple[int, int]
    motion_mask: np.ndarray
    adjacent: list[float]
    opposite: list[float]
    foot_bottoms: list[int]
    stable_centres: list[tuple[float, float]]
    source_stable_drift: int
    source_vertical_crack: int
    output_vertical_crack: int

    @property
    def median(self) -> float:
        return float(np.median(self.adjacent))

    @property
    def worst(self) -> float:
        return max(self.adjacent)

    @property
    def opposite_median(self) -> float:
        return float(np.median(self.opposite))

    @property
    def ratio(self) -> float:
        denominator = self.opposite_median
        return self.median / denominator if denominator > 0 else float("nan")

    @property
    def unique_frames(self) -> int:
        return len({image.tobytes() for image in self.output_frames})

    @property
    def foot_drift(self) -> int:
        return max(self.foot_bottoms) - min(self.foot_bottoms)

    @property
    def stable_drift(self) -> float:
        xs = [point[0] for point in self.stable_centres]
        ys = [point[1] for point in self.stable_centres]
        return max(max(xs) - min(xs), max(ys) - min(ys))

    def failures(self) -> list[str]:
        failures = []
        if self.unique_frames != FRAME_COUNT:
            failures.append(f"unique frames {self.unique_frames} != {FRAME_COUNT}")
        if not math.isfinite(self.ratio):
            failures.append("ratio is not finite")
        if self.median > MAX_MEDIAN:
            failures.append(f"median {self.median:.1f}% > {MAX_MEDIAN:.0f}%")
        if self.worst > MAX_WORST:
            failures.append(f"worst {self.worst:.1f}% > {MAX_WORST:.0f}%")
        if math.isfinite(self.ratio) and self.ratio > MAX_RATIO:
            failures.append(f"ratio {self.ratio:.2f} > {MAX_RATIO:.2f}")
        if self.foot_drift > MAX_FOOT_DRIFT:
            failures.append(f"foot drift {self.foot_drift}px > {MAX_FOOT_DRIFT}px")
        if self.stable_drift > MAX_STABLE_DRIFT:
            failures.append(f"stable drift {self.stable_drift:.1f}px > {MAX_STABLE_DRIFT}px")
        adjacent_motion, phase_motion, repeated_run = gait_motion_quality(self.output_frames)
        if adjacent_motion < MIN_ADJACENT_MOTION:
            failures.append(
                f"adjacent gait motion {adjacent_motion:.3f} < {MIN_ADJACENT_MOTION:.2f}"
            )
        if phase_motion < MIN_PHASE_MOTION:
            failures.append(f"phase gait motion {phase_motion:.3f} < {MIN_PHASE_MOTION:.2f}")
        if repeated_run > MAX_REPEATED_RUN:
            failures.append(
                f"repeated near-identical pose run {repeated_run} > {MAX_REPEATED_RUN}"
            )
        crack_delta = self.output_vertical_crack - self.source_vertical_crack
        if crack_delta > MAX_NEW_VERTICAL_CRACK:
            failures.append(
                f"new upper-body alpha crack +{crack_delta}px > {MAX_NEW_VERTICAL_CRACK}px"
            )
        return failures


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("Artifacts/AnimationCoherence/StabilizedLocomotionV1"),
    )
    parser.add_argument("--pelvis", type=float, default=0.56)
    parser.add_argument("--arm-inner", type=float, default=0.14)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument(
        "--promotion-root",
        type=Path,
        help="optional target repo for promoting a candidate built from a restored source mirror",
    )
    return parser.parse_args()


def discover(art_root: Path) -> list[CharacterSource]:
    result = []
    for frames_dir in sorted(art_root.rglob("Pixel/HighMotion/Frames")):
        pngs = sorted(frames_dir.glob("*.png"))
        matches = [WALK_RE.match(path.name) for path in pngs]
        character_ids = {match["character"] for match in matches if match}
        if len(character_ids) != 1:
            raise ValueError(
                f"{frames_dir} must contain one character walk set, got {sorted(character_ids)}"
            )
        character_id = next(iter(character_ids))
        root = frames_dir.parent
        sheets = {
            part: root / f"{character_id}_pixel_walk8dir6_{part}_v1.png"
            for part in PART_DIRECTIONS
        }
        missing = [str(path) for path in sheets.values() if not path.is_file()]
        if missing:
            raise FileNotFoundError("missing canonical HighMotion sheets: " + ", ".join(missing))
        result.append(CharacterSource(character_id, root, frames_dir, sheets))
    if not result:
        raise ValueError(f"no Pixel/HighMotion/Frames sets found under {art_root}")
    return result


def require_contract(source: CharacterSource) -> dict[str, list[Path]]:
    buckets: dict[str, list[tuple[int, Path]]] = defaultdict(list)
    unexpected = []
    for path in sorted(source.frames_dir.glob("*.png")):
        match = WALK_RE.match(path.name)
        if not match or match["character"] != source.character_id:
            unexpected.append(path.name)
            continue
        buckets[match["direction"]].append((int(match["phase"]), path))
    if unexpected:
        raise ValueError(f"unexpected walk frame names in {source.frames_dir}: {unexpected}")
    if set(buckets) != set(DIRECTIONS):
        raise ValueError(
            f"{source.character_id}: expected directions {DIRECTIONS}, got {sorted(buckets)}"
        )
    result = {}
    for direction in DIRECTIONS:
        entries = sorted(buckets[direction])
        indices = [index for index, _ in entries]
        if indices != list(range(FRAME_COUNT)):
            raise ValueError(
                f"{source.character_id}/{direction}: expected phases 0..5, got {indices}"
            )
        result[direction] = [path for _, path in entries]
    return result


def load_rgba(path: Path) -> Image.Image:
    with Image.open(path) as loaded:
        if loaded.mode != "RGBA":
            raise ValueError(f"{path}: source mode must be RGBA, got {loaded.mode}")
        image = loaded.copy()
    if image.size != CANVAS_SIZE:
        raise ValueError(f"{path}: expected {CANVAS_SIZE}, got {image.size}")
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    values = set(np.unique(alpha).tolist())
    if not values.issubset({0, 255}):
        raise ValueError(f"{path}: alpha must be hard 0/255, got {sorted(values)}")
    if not np.any(alpha):
        raise ValueError(f"{path}: empty alpha")
    return image


def hard_alpha(frame: np.ndarray) -> np.ndarray:
    result = frame.copy()
    opaque = result[:, :, 3] >= 128
    result[:, :, 3] = np.where(opaque, 255, 0).astype(np.uint8)
    result[~opaque, :3] = 0
    return result


def alpha_bounds(frame: np.ndarray) -> tuple[int, int, int, int]:
    rows, columns = np.nonzero(frame[:, :, 3] > 0)
    return int(columns.min()), int(rows.min()), int(columns.max()), int(rows.max())


def change_percent(a: np.ndarray, b: np.ndarray) -> float:
    delta = np.abs(a.astype(np.int16) - b.astype(np.int16))
    changed = (delta[:, :, :3].max(axis=2) >= 12) | (delta[:, :, 3] >= 12)
    union = int(((a[:, :, 3] >= 8) | (b[:, :, 3] >= 8)).sum())
    return float(changed.sum()) / union * 100.0 if union else 0.0


def gait_motion_quality(images: list[Image.Image]) -> tuple[float, float, int]:
    """Reject technically unique frames that are visually one frozen pose.

    The previous gate counted byte-unique PNGs, so one foot silhouette repeated for three phases
    passed when a few interpolated pixels differed.  This score measures actual leg/foot pixel
    motion inside the lower 28% of the character and explicitly forbids adjacent frozen poses.
    """
    arrays = [np.asarray(image, dtype=np.uint8) for image in images]
    scores: list[float] = []
    for index, frame in enumerate(arrays):
        other = arrays[(index + 1) % len(arrays)]
        alpha = (frame[:, :, 3] > 0) | (other[:, :, 3] > 0)
        rows, _ = np.nonzero(alpha)
        if not len(rows):
            scores.append(0.0)
            continue
        top, bottom = int(rows.min()), int(rows.max())
        lower_start = top + int(round((bottom - top + 1) * 0.72))
        region = np.zeros(alpha.shape, dtype=bool)
        region[lower_start : bottom + 1] = True
        sample = alpha & region
        delta = np.abs(frame.astype(np.int16) - other.astype(np.int16))
        changed = (delta[:, :, :3].max(axis=2) >= 12) | (delta[:, :, 3] >= 12)
        denominator = int(sample.sum())
        scores.append(float((changed & region).sum()) / denominator if denominator else 0.0)
    repeated_run = max_near_identical_run(scores, MIN_ADJACENT_MOTION)
    return min(scores), float(np.median(scores)), repeated_run


def max_near_identical_run(scores: list[float], threshold: float) -> int:
    if not scores:
        return 0
    frozen = [score < threshold for score in scores]
    if all(frozen):
        return len(frozen)
    doubled = frozen + frozen
    longest = current = 0
    for value in doubled:
        current = current + 1 if value else 0
        longest = max(longest, current)
    return min(longest, len(frozen))


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    result = mask.copy()
    height, width = mask.shape
    for _ in range(radius):
        padded = np.pad(result, 1, constant_values=False)
        result = np.logical_or.reduce(
            tuple(
                padded[dy : dy + height, dx : dx + width]
                for dy in range(3)
                for dx in range(3)
            )
        )
    return result


def motion_mask(
    frames: list[np.ndarray],
    canonical: np.ndarray,
    pelvis: float,
    arm_inner: float,
) -> np.ndarray:
    left, top, right, bottom = alpha_bounds(canonical)
    height = bottom - top + 1
    yy, xx = np.mgrid[0 : canonical.shape[0], 0 : canonical.shape[1]]
    leg_split = top + max(0.78, pelvis + 0.16) * height
    vicinity = (
        (xx >= left - 8)
        & (xx <= right + 8)
        & (yy >= top - 2)
        & (yy <= bottom + 1)
    )
    # The source drawings have no independent pixel layers.  Cutting an
    # "arm" rectangle also cuts hair and clothing, producing long transparent
    # seams when it is recombined with the canonical torso.  Keep the complete
    # upper body canonical and animate only the authored leg/foot silhouettes.
    return (yy >= leg_split) & vicinity


def stabilize_sources(
    frames: list[np.ndarray], canonical_index: int, moving: np.ndarray
) -> tuple[list[np.ndarray], int]:
    canonical = frames[canonical_index]
    stable = ~moving
    outputs = []
    maximum_source_drift = 0
    for source in frames:
        best_score = float("inf")
        best_dx = 0
        for dx in range(-5, 6):
            shifted = shift_horizontal(source, dx)
            sample = stable & ((canonical[:, :, 3] > 0) | (shifted[:, :, 3] > 0))
            delta = np.abs(canonical.astype(np.int16) - shifted.astype(np.int16)).sum(axis=2)
            score = float(delta[sample].mean()) if sample.any() else float("inf")
            if score < best_score:
                best_score, best_dx = score, dx
        maximum_source_drift = max(maximum_source_drift, abs(best_dx))
        shifted = shift_horizontal(source, best_dx)
        output = canonical.copy()
        output[moving] = shifted[moving]
        outputs.append(output)
    return outputs, maximum_source_drift


def shift_horizontal(source: np.ndarray, dx: int) -> np.ndarray:
    shifted = np.zeros_like(source)
    if dx >= 0:
        shifted[:, dx:] = source[:, : source.shape[1] - dx]
    else:
        shifted[:, :dx] = source[:, -dx:]
    return shifted


def align_like_splitter(source: np.ndarray) -> np.ndarray:
    """Normalize the exact anchor used by split_high_motion_sheets.py."""
    rows, columns = np.nonzero(source[:, :, 3] > 0)
    min_y, max_y = int(rows.min()), int(rows.max())
    upper_limit = min_y + max(1, int((max_y - min_y) * 0.55))
    anchor_x = int(round(float(np.median(columns[rows <= upper_limit]))))
    dx = (CANVAS_SIZE[0] // 2) - anchor_x
    dy = (CANVAS_SIZE[1] - 8) - max_y
    result = np.zeros_like(source)
    source_x0, source_x1 = max(0, -dx), min(source.shape[1], source.shape[1] - dx)
    source_y0, source_y1 = max(0, -dy), min(source.shape[0], source.shape[0] - dy)
    if source_x1 > source_x0 and source_y1 > source_y0:
        result[
            source_y0 + dy : source_y1 + dy,
            source_x0 + dx : source_x1 + dx,
        ] = source[source_y0:source_y1, source_x0:source_x1]
    return result


def articulated_masks(
    canonical: np.ndarray, pelvis: float, arm_inner: float
) -> list[np.ndarray]:
    left, top, right, bottom = alpha_bounds(canonical)
    width = max(1, right - left + 1)
    height = max(1, bottom - top + 1)
    centre = (left + right) * 0.5
    pelvis_y = top + pelvis * height
    leg_split = top + max(0.78, pelvis + 0.16) * height
    shoulder_y = top + 0.30 * height
    arm_bottom = top + min(0.72, pelvis + 0.10) * height
    yy, xx = np.indices(canonical.shape[:2])
    vicinity = (
        (xx >= left - 8)
        & (xx <= right + 8)
        & (yy >= top - 2)
        & (yy <= bottom + 1)
    )
    empty = np.zeros_like(yy, dtype=bool)
    regions = (
        empty,
        (yy >= leg_split) & (xx < centre),
        (yy >= leg_split) & (xx >= centre),
        empty,
        empty,
    )
    return [region & vicinity for region in regions]


def segment_centre(frame: np.ndarray, region: np.ndarray) -> tuple[float, float] | None:
    rows, columns = np.nonzero((frame[:, :, 3] > 0) & region)
    if not len(columns):
        return None
    return float(columns.mean()), float(rows.mean())


def shift_layer(frame: np.ndarray, dx: int) -> np.ndarray:
    shifted = np.zeros_like(frame)
    if dx >= 0:
        shifted[:, dx:] = frame[:, : frame.shape[1] - dx]
    else:
        shifted[:, :dx] = frame[:, -dx:]
    return shifted


def articulated_pose(
    stable_base: np.ndarray,
    source: np.ndarray,
    target: np.ndarray,
    regions: list[np.ndarray],
    allowed: np.ndarray,
    progress: float,
    arc_sign: int,
    arc_profile: int,
    arc_pixels: int,
) -> np.ndarray:
    result = stable_base.copy()
    result[allowed] = 0
    if arc_profile == 1:
        arc = (0, +arc_sign * arc_pixels, -arc_sign * arc_pixels, 0, 0)
    elif arc_profile == 2:
        arc = (0, 0, 0, -arc_sign * arc_pixels, +arc_sign * arc_pixels)
    else:
        arc = (0, 0, 0, 0, 0)
    for index, region in enumerate(regions):
        source_centre = segment_centre(source, region)
        target_centre = segment_centre(target, region)
        if source_centre is None or target_centre is None:
            dx = arc[index]
        else:
            dx = int(round((target_centre[0] - source_centre[0]) * progress)) + arc[index]
        dx = int(np.clip(dx, -5, 5))
        layer = np.zeros_like(source)
        selected = region & (source[:, :, 3] > 0)
        layer[selected] = source[selected]
        moved = shift_layer(layer, dx)
        opaque = moved[:, :, 3] > 0
        result[opaque] = moved[opaque]
    return hard_alpha(result)


def cycle_metrics(frames: list[np.ndarray]) -> tuple[list[float], list[float], float, float, float, int]:
    adjacent = [change_percent(frames[index], frames[(index + 1) % FRAME_COUNT]) for index in range(FRAME_COUNT)]
    opposite = [change_percent(frames[index], frames[(index + 3) % FRAME_COUNT]) for index in range(FRAME_COUNT)]
    median = float(np.median(adjacent))
    worst = max(adjacent)
    opposite_median = float(np.median(opposite))
    ratio = median / opposite_median if opposite_median > 0 else float("inf")
    unique = len({frame.tobytes() for frame in frames})
    return adjacent, opposite, median, worst, ratio, unique


def max_vertical_alpha_crack(frame: np.ndarray) -> int:
    """Longest upper-body transparent column trapped between opaque pixels."""
    alpha = frame[:, :, 3] > 0
    opaque_left = np.maximum.accumulate(alpha, axis=1)
    opaque_right = np.maximum.accumulate(alpha[:, ::-1], axis=1)[:, ::-1]
    trapped = (~alpha) & opaque_left & opaque_right
    _, top, _, bottom = alpha_bounds(frame)
    leg_split = int(round(top + 0.78 * (bottom - top + 1)))
    trapped[leg_split:, :] = False
    longest = 0
    for column in range(trapped.shape[1]):
        run = 0
        for value in trapped[:, column]:
            run = run + 1 if value else 0
            longest = max(longest, run)
    return longest


def select_articulated_cycle(
    frames: list[np.ndarray], moving: np.ndarray, pelvis: float, arm_inner: float
) -> tuple[list[np.ndarray], tuple[int, int]]:
    best: tuple[float, list[np.ndarray], tuple[int, int], tuple[float, float, float, int]] | None = None

    def consider(settings: tuple[tuple[float, float, int], ...]) -> None:
        nonlocal best
        for first in range(FRAME_COUNT):
            for second in range(first + 1, FRAME_COUNT):
                first_pose, second_pose = frames[first], frames[second]
                contact_change = change_percent(first_pose, second_pose)
                # Leg-only cycles intentionally keep the complete upper body
                # fixed, so useful authored contacts occupy a small percentage
                # of the full 256px canvas.
                if contact_change < 2.0:
                    continue
                regions = articulated_masks(first_pose, pelvis, arm_inner)
                for first_step, second_step, arc_pixels in settings:
                    output = [
                        articulated_pose(first_pose, first_pose, second_pose, regions, moving, 0.0, +1, 0, arc_pixels),
                        articulated_pose(first_pose, first_pose, second_pose, regions, moving, first_step, +1, 1, arc_pixels),
                        articulated_pose(first_pose, first_pose, second_pose, regions, moving, second_step, +1, 2, arc_pixels),
                        articulated_pose(first_pose, second_pose, first_pose, regions, moving, 0.0, -1, 0, arc_pixels),
                        articulated_pose(first_pose, second_pose, first_pose, regions, moving, first_step, -1, 1, arc_pixels),
                        articulated_pose(first_pose, second_pose, first_pose, regions, moving, second_step, -1, 2, arc_pixels),
                    ]
                    output = [align_like_splitter(frame) for frame in output]
                    motion_scores = gait_motion_quality(
                        [Image.fromarray(frame, "RGBA") for frame in output]
                    )
                    _, _, median, worst, ratio, unique = cycle_metrics(output)
                    objective = (
                        (FRAME_COUNT - unique) * 10000.0
                        + max(0.0, ratio - MAX_RATIO) * 3000.0
                        + max(0.0, worst - MAX_WORST) * 300.0
                        + max(0.0, median - MAX_MEDIAN) * 150.0
                        + ratio * 100.0
                        + worst * 0.2
                        + max(0.0, MIN_ADJACENT_MOTION - motion_scores[0]) * 50000.0
                        + max(0.0, MIN_PHASE_MOTION - motion_scores[1]) * 25000.0
                        + max(0, motion_scores[2] - MAX_REPEATED_RUN) * 10000.0
                        - min(contact_change, 60.0) * 0.05
                    )
                    if best is None or objective < best[0]:
                        best = objective, output, (first, second), (median, worst, ratio, unique)

    consider(((1.0 / 3.0, 2.0 / 3.0, 2), (0.22, 0.78, 3)))
    if best is not None:
        median, worst, ratio, unique = best[3]
        if unique == FRAME_COUNT and median <= MAX_MEDIAN and worst <= MAX_WORST and ratio <= MAX_RATIO:
            return best[1], best[2]
    consider(((0.20, 0.80, 2), (0.25, 0.75, 3), (0.35, 0.65, 4), (1.0 / 3.0, 2.0 / 3.0, 4)))
    if best is None:
        raise ValueError("no source-authored contact pair has enough visible contrast")
    return best[1], best[2]


def stable_centre(frame: np.ndarray, moving: np.ndarray) -> tuple[float, float]:
    pixels = (frame[:, :, 3] > 0) & ~moving
    rows, columns = np.nonzero(pixels)
    if not len(columns):
        return 0.0, 0.0
    return float(np.median(columns)), float(np.median(rows))


def build_loop(
    character_id: str,
    direction: str,
    paths: list[Path],
    pelvis: float,
    arm_inner: float,
) -> StabilizedLoop:
    source_images = [load_rgba(path) for path in paths]
    source_frames = [hard_alpha(np.asarray(image, dtype=np.uint8)) for image in source_images]
    canonical_index = 0
    moving = motion_mask(source_frames, source_frames[canonical_index], pelvis, arm_inner)
    stabilized, source_stable_drift = stabilize_sources(
        source_frames, canonical_index, moving
    )
    arrays, endpoint_indices = select_articulated_cycle(stabilized, moving, pelvis, arm_inner)
    images = [Image.fromarray(array, "RGBA") for array in arrays]
    adjacent = [change_percent(arrays[i], arrays[(i + 1) % FRAME_COUNT]) for i in range(FRAME_COUNT)]
    opposite = [change_percent(arrays[i], arrays[(i + 3) % FRAME_COUNT]) for i in range(FRAME_COUNT)]
    foot_bottoms = [alpha_bounds(array)[3] for array in arrays]
    centres = [stable_centre(array, moving) for array in arrays]
    source_vertical_crack = max(max_vertical_alpha_crack(array) for array in source_frames)
    output_vertical_crack = max(max_vertical_alpha_crack(array) for array in arrays)
    return StabilizedLoop(
        character_id,
        direction,
        paths,
        images,
        canonical_index,
        endpoint_indices,
        moving,
        adjacent,
        opposite,
        foot_bottoms,
        centres,
        source_stable_drift,
        source_vertical_crack,
        output_vertical_crack,
    )


def output_frame_path(
    repo_root: Path, output: Path, loop: StabilizedLoop, phase: int
) -> Path:
    return output / "Candidate" / loop.source_paths[phase].relative_to(repo_root)


def write_frames(repo_root: Path, output: Path, loops: list[StabilizedLoop]) -> None:
    for loop in loops:
        for phase, image in enumerate(loop.output_frames):
            path = output_frame_path(repo_root, output, loop, phase)
            path.parent.mkdir(parents=True, exist_ok=True)
            image.save(path, format="PNG", compress_level=9)


def build_sheet(character: CharacterSource, loops: list[StabilizedLoop], part: str) -> Image.Image:
    sheet_path = character.sheets[part]
    with Image.open(sheet_path) as loaded:
        if loaded.mode != "RGBA" or loaded.size != (1536, 1024):
            raise ValueError(f"{sheet_path}: expected 1536x1024 RGBA")
    # Candidate frames are already aligned to the splitter's x anchor and y=248
    # baseline.  A clean 4x6 sheet preserves that alignment exactly and avoids
    # accidentally retaining pixels from the old independent redraws.
    sheet = Image.new("RGBA", (1536, 1024), (0, 0, 0, 0))
    selected = {loop.direction: loop for loop in loops if loop.character_id == character.character_id}
    for row, direction in enumerate(PART_DIRECTIONS[part]):
        loop = selected[direction]
        for phase, candidate in enumerate(loop.output_frames):
            sheet.alpha_composite(candidate, (phase * 256, row * 256))
    return sheet


def save_sheet(image: Image.Image, path: Path) -> None:
    metadata = PngImagePlugin.PngInfo()
    metadata.add_text(SHEET_LAYOUT_KEY, SHEET_LAYOUT_VALUE)
    image.save(path, format="PNG", compress_level=9, pnginfo=metadata)


def write_sheets(repo_root: Path, output: Path, characters: list[CharacterSource], loops: list[StabilizedLoop]) -> dict[str, Image.Image]:
    sheets = {}
    for character in characters:
        for part in PART_DIRECTIONS:
            image = build_sheet(character, loops, part)
            path = output / "Candidate" / character.sheets[part].relative_to(repo_root)
            path.parent.mkdir(parents=True, exist_ok=True)
            save_sheet(image, path)
            sheets[str(character.sheets[part])] = image
    return sheets


def font(size: int) -> ImageFont.ImageFont:
    for candidate in (Path("C:/Windows/Fonts/segoeui.ttf"), Path("C:/Windows/Fonts/arial.ttf")):
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def render_contact_sheets(output: Path, loops: list[StabilizedLoop]) -> None:
    tile = 96
    label = 134
    header = 30
    small = font(12)
    title = font(16)
    by_character = defaultdict(list)
    for loop in loops:
        by_character[loop.character_id].append(loop)
    for character_id, character_loops in sorted(by_character.items()):
        by_direction = {loop.direction: loop for loop in character_loops}
        for version in ("before", "after"):
            canvas = Image.new(
                "RGBA",
                (label + len(DIRECTIONS) * tile, header + FRAME_COUNT * tile),
                (28, 34, 38, 255),
            )
            draw = ImageDraw.Draw(canvas)
            draw.text((8, 7), f"{character_id} / {version}", font=title, fill=(245, 245, 245))
            for column, direction in enumerate(DIRECTIONS):
                draw.text((label + column * tile + 4, 8), direction, font=small, fill=(245, 245, 245))
            for phase in range(FRAME_COUNT):
                draw.text((8, header + phase * tile + 40), f"phase {phase}", font=title, fill=(245, 245, 245))
                for column, direction in enumerate(DIRECTIONS):
                    loop = by_direction[direction]
                    image = (
                        load_rgba(loop.source_paths[phase])
                        if version == "before"
                        else loop.output_frames[phase]
                    )
                    sprite = image.resize((tile, tile), Image.Resampling.NEAREST)
                    x, y = label + column * tile, header + phase * tile
                    draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(239, 232, 218), outline=(86, 72, 70))
                    canvas.alpha_composite(sprite, (x, y))
            path = output / "Evidence" / f"{character_id}_{version}_8x6.png"
            path.parent.mkdir(parents=True, exist_ok=True)
            canvas.convert("RGB").save(path, format="PNG")


def render_gifs(output: Path, loops: list[StabilizedLoop]) -> None:
    by_character = defaultdict(list)
    for loop in loops:
        by_character[loop.character_id].append(loop)
    tile = 128
    for character_id, character_loops in sorted(by_character.items()):
        by_direction = {loop.direction: loop for loop in character_loops}
        frames = []
        for phase in range(FRAME_COUNT):
            canvas = Image.new("RGBA", (len(DIRECTIONS) * tile, tile), (239, 232, 218, 255))
            for column, direction in enumerate(DIRECTIONS):
                sprite = by_direction[direction].output_frames[phase].resize((tile, tile), Image.Resampling.NEAREST)
                canvas.alpha_composite(sprite, (column * tile, 0))
            frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE))
        path = output / "Evidence" / f"{character_id}_after_walk.gif"
        frames[0].save(path, save_all=True, append_images=frames[1:], duration=110, loop=0, disposal=2)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def ensure_backup_meta(repo_root: Path, asset_path: Path) -> None:
    meta_path = Path(f"{asset_path}.meta")
    if meta_path.exists():
        return
    relative = asset_path.relative_to(repo_root).as_posix()
    guid = hashlib.md5(f"family-company/coherence-backup/v1/{relative}".encode("utf-8")).hexdigest()
    meta_path.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8", newline="\n")


def invalidate_direction_approvals(repo_root: Path) -> Path | None:
    manifest = (
        repo_root
        / "Assets"
        / "FamilyCompany"
        / "Content"
        / "Resources"
        / "HighMotion"
        / "HighMotionDirectionManifest.asset"
    )
    if not manifest.is_file():
        return None
    original = manifest.read_text(encoding="utf-8")
    updated = re.sub(
        r"(?m)^(\s*visualApproval:)\s+[0-9a-fA-F]+$",
        lambda match: f"{match.group(1)} 0000000000000000",
        original,
    )
    updated = re.sub(
        r"(?m)^(\s*frameVisualApproval:)\s+[0-9a-fA-F]+$",
        lambda match: f"{match.group(1)} {'00' * (FRAME_COUNT * len(DIRECTIONS))}",
        updated,
    )
    if updated != original:
        manifest.write_text(updated, encoding="utf-8", newline="\n")
    return manifest


def write_report(output: Path, characters: list[CharacterSource], loops: list[StabilizedLoop]) -> None:
    failed = [loop for loop in loops if loop.failures()]
    lines = [
        "LOCOMOTION STABILIZATION V1",
        "",
        f"characters={len(characters)} loops={len(loops)} frames={len(loops) * FRAME_COUNT}",
        f"gates median<={MAX_MEDIAN:.0f}% worst<={MAX_WORST:.0f}% ratio<={MAX_RATIO:.2f} unique=6 footDrift<={MAX_FOOT_DRIFT}px stableDrift<={MAX_STABLE_DRIFT}px newUpperBodyCrack<={MAX_NEW_VERTICAL_CRACK}px adjacentMotion>={MIN_ADJACENT_MOTION:.2f} phaseMotion>={MIN_PHASE_MOTION:.2f} repeatedRun<={MAX_REPEATED_RUN}",
        "",
        f"{'character':<16}{'direction':<11}{'canonical':>10}{'endpoints':>12}{'median':>9}{'worst':>9}{'ratio':>8}{'unique':>8}{'foot':>7}{'stable':>8} verdict",
    ]
    for loop in loops:
        verdict = "ok" if not loop.failures() else "FAIL: " + "; ".join(loop.failures())
        lines.append(
            f"{loop.character_id:<16}{loop.direction:<11}{loop.canonical_index:>10}"
            f"{str(loop.endpoint_indices):>12}{loop.median:>8.1f}%{loop.worst:>8.1f}%"
            f"{loop.ratio:>8.2f}{loop.unique_frames:>8}{loop.foot_drift:>7}"
            f"{loop.stable_drift:>8.1f} {verdict}"
        )
    lines += ["", f"{'PASS' if not failed else 'FAIL'} {len(loops) - len(failed)}/{len(loops)} loops"]
    output.mkdir(parents=True, exist_ok=True)
    (output / "stabilization-report.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    payload = {
        "schemaVersion": 1,
        "contract": {
            "characters": len(characters),
            "directions": len(DIRECTIONS),
            "framesPerLoop": FRAME_COUNT,
            "loops": len(loops),
            "frames": len(loops) * FRAME_COUNT,
        },
        "summary": {
            "pass": not failed,
            "passedLoops": len(loops) - len(failed),
            "failedLoops": len(failed),
        },
        "loops": [
            {
                "character": loop.character_id,
                "direction": loop.direction,
                "canonicalIndex": loop.canonical_index,
                "endpointIndices": list(loop.endpoint_indices),
                "sourcePaths": [str(path) for path in loop.source_paths],
                "sourceSha256": [sha256_bytes(path.read_bytes()) for path in loop.source_paths],
                "outputSha256": [sha256_bytes(image.tobytes()) for image in loop.output_frames],
                "adjacent": [round(value, 3) for value in loop.adjacent],
                "opposite": [round(value, 3) for value in loop.opposite],
                "median": round(loop.median, 3),
                "worst": round(loop.worst, 3),
                "ratio": round(loop.ratio, 4),
                "uniqueFrames": loop.unique_frames,
                "footBottoms": loop.foot_bottoms,
                "footDriftPx": loop.foot_drift,
                "stableCentres": [[round(x, 2), round(y, 2)] for x, y in loop.stable_centres],
                "stableDriftPx": round(loop.stable_drift, 2),
                "sourceStableRegistrationMaxPx": loop.source_stable_drift,
                "sourceUpperBodyAlphaCrackPx": loop.source_vertical_crack,
                "outputUpperBodyAlphaCrackPx": loop.output_vertical_crack,
                "newUpperBodyAlphaCrackPx": loop.output_vertical_crack - loop.source_vertical_crack,
                "adjacentGaitMotionMin": round(gait_motion_quality(loop.output_frames)[0], 4),
                "adjacentGaitMotionMedian": round(gait_motion_quality(loop.output_frames)[1], 4),
                "nearIdenticalPoseRun": gait_motion_quality(loop.output_frames)[2],
                "failures": loop.failures(),
            }
            for loop in loops
        ],
    }
    (output / "stabilization-report.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def apply_candidate(
    source_root: Path,
    target_root: Path,
    output: Path,
    characters: list[CharacterSource],
    loops: list[StabilizedLoop],
    sheets: dict[str, Image.Image],
) -> None:
    failed = [loop for loop in loops if loop.failures()]
    if failed:
        raise RuntimeError(f"refusing to apply {len(failed)} failing loops")
    backup = target_root / "Assets" / "Art" / "Characters" / "BeforeCoherenceV1"

    def target_path(source_path: Path) -> Path:
        return target_root / source_path.relative_to(source_root)

    protected_meta_paths = [Path(f"{target_path(path)}.meta") for loop in loops for path in loop.source_paths]
    protected_meta_paths += [
        Path(f"{target_path(path)}.meta")
        for character in characters
        for path in character.sheets.values()
    ]
    missing_meta = [str(path) for path in protected_meta_paths if not path.is_file()]
    if missing_meta:
        raise FileNotFoundError("refusing promotion with missing runtime meta: " + ", ".join(missing_meta))
    meta_before = {path: path.read_bytes() for path in protected_meta_paths}
    if backup.exists():
        for character in characters:
            for sheet_path in character.sheets.values():
                preserved = backup / character.character_id / sheet_path.name
                if not preserved.is_file() or preserved.read_bytes() != sheet_path.read_bytes():
                    raise RuntimeError(f"existing backup does not match restored source: {preserved}")
    else:
        backup.mkdir(parents=True, exist_ok=False)
        ensure_backup_meta(target_root, backup)
        for character in characters:
            character_backup = backup / character.character_id
            character_backup.mkdir(parents=True, exist_ok=True)
            ensure_backup_meta(target_root, character_backup)
            for sheet_path in character.sheets.values():
                preserved = character_backup / sheet_path.name
                shutil.copy2(sheet_path, preserved)
                ensure_backup_meta(target_root, preserved)
    for sheet_path_text, image in sheets.items():
        save_sheet(image, target_path(Path(sheet_path_text)))
    for loop in loops:
        for phase, image in enumerate(loop.output_frames):
            image.save(target_path(loop.source_paths[phase]), format="PNG", compress_level=9)
    readme = backup / "README.md"
    readme.write_text(
        "# Before Coherence V1\n\nThese 24 canonical HighMotion sheets are the exact pre-stabilization runtime sources.\n",
        encoding="utf-8",
    )
    ensure_backup_meta(target_root, readme)
    changed_meta = [str(path) for path, content in meta_before.items() if path.read_bytes() != content]
    if changed_meta:
        raise RuntimeError("runtime Unity meta changed during promotion: " + ", ".join(changed_meta))
    approval_manifest = invalidate_direction_approvals(target_root)
    if approval_manifest is not None:
        print(f"invalidated-stale-direction-approvals={approval_manifest}")
    print(f"applied=1 backup={backup}")


def main() -> int:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    output = args.output if args.output.is_absolute() else repo_root / args.output
    characters = discover(repo_root / "Assets" / "Art" / "Characters")
    loops = []
    for character in characters:
        contract = require_contract(character)
        for direction in DIRECTIONS:
            loops.append(
                build_loop(
                    character.character_id,
                    direction,
                    contract[direction],
                    args.pelvis,
                    args.arm_inner,
                )
            )
    expected_loops = len(characters) * len(DIRECTIONS)
    if len(loops) != expected_loops:
        raise RuntimeError(f"expected {expected_loops} loops, built {len(loops)}")
    write_frames(repo_root, output, loops)
    sheets = write_sheets(repo_root, output, characters, loops)
    render_contact_sheets(output, loops)
    render_gifs(output, loops)
    write_report(output, characters, loops)
    failed = [loop for loop in loops if loop.failures()]
    print(
        f"LOCOMOTION_STABILIZATION: {'PASS' if not failed else 'FAIL'} "
        f"characters={len(characters)} loops={len(loops)} frames={len(loops) * FRAME_COUNT} "
        f"failed={len(failed)} output={output}"
    )
    if args.apply:
        target_root = args.promotion_root.resolve() if args.promotion_root else repo_root
        apply_candidate(repo_root, target_root, output, characters, loops, sheets)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
