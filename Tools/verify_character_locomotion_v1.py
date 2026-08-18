#!/usr/bin/env python3
"""Fail-closed footstep QA for Character Locomotion Generation V1.

Unlike the legacy coherence gate, this verifier measures the actual lower-limb
trajectory.  A loop with six byte-unique images but stationary feet must fail.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
from dataclasses import asdict, dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

from generate_character_locomotion_v1 import CHARACTERS, DIRECTIONS, PHASE_COUNT, PROFILE_PATH, REPO_ROOT


GROUND_Y = 247
FOOT_WINDOW_PX = 34
FOOT_CORE_PX = 18
MIN_CLUSTER_PIXELS = 10
MIN_CONTACT_SEPARATION_PX = 7.0
MIN_CONTACT_ALTERNATION_EXCURSION_PX = 1.0
MIN_HALF_EXCURSION_PX = 2.75
MIN_ADJACENT_EXCURSION_PX = 2.5
MIN_ADJACENT_CHANGE_RATIO = 0.50
MIN_ADJACENT_SILHOUETTE_CHANGE_RATIO = 0.13
MIN_SWING_GROUND_EVACUATION_PX = 4
MIN_SWING_VERTICAL_LIFT_PX = 0.85
MIN_SUPPORT_GROUND_PIXELS = 1
MIN_UPPER_RIGID_EXCURSION_PX = 0.50
MAX_UPPER_RIGID_EXCURSION_PX = 1.25
MAX_ALIGNED_UPPER_IDENTITY_CHANGE_RATIO = 0.015


@dataclass(frozen=True)
class Foot:
    x: float
    y: float
    bottom: int
    pixels: int


@dataclass
class LoopResult:
    character: str
    direction: str
    contactSeparation0Px: float
    contactSeparation3Px: float
    contactAlternationExcursionPx: float
    swingExcursion02Px: float
    swingExcursion35Px: float
    minimumAuthoredAdjacentExcursionPx: float
    minimumAdjacentFootChangeRatio: float
    minimumAdjacentFootSilhouetteChangeRatio: float
    swingGroundEvacuation2Px: int
    swingGroundEvacuation5Px: int
    swingElevatedAddition2Px: int
    swingElevatedAddition5Px: int
    swingVerticalLift2Px: float
    swingVerticalLift5Px: float
    supportGroundPixels2: int
    supportGroundPixels5: int
    swingCentroidX2: float
    swingCentroidX5: float
    upperChangeRatio: float
    upperCentroidExcursionPx: float
    upperRigidExcursionPx: float
    alignedUpperIdentityChangeRatio: float
    verdict: str
    failures: list[str]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--candidate-root",
        type=Path,
        default=None,
        help="Candidate root containing <character>/Frames. Defaults to stable runtime frames.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=REPO_ROOT / "Artifacts" / "CharacterLocomotionGenerationV1" / "QuantitativeQa",
    )
    return parser.parse_args()


def load_profiles() -> dict[str, dict[str, float | int]]:
    return json.loads(PROFILE_PATH.read_text(encoding="utf-8"))["characters"]


def frame_path(candidate_root: Path | None, character, direction: str, phase: int) -> Path:
    name = f"{character.character_id}_{direction}_walk_{phase}.png"
    if candidate_root is None:
        return character.frame_root / name
    return candidate_root / character.character_id / "Frames" / name


def load_frames(candidate_root: Path | None, character, direction: str) -> list[np.ndarray]:
    frames: list[np.ndarray] = []
    for phase in range(PHASE_COUNT):
        path = frame_path(candidate_root, character, direction, phase)
        with Image.open(path) as loaded:
            image = loaded.convert("RGBA")
        if image.size != (256, 256):
            raise ValueError(f"{path}: expected 256x256, got {image.size}")
        array = np.asarray(image, dtype=np.uint8)
        unexpected = set(np.unique(array[:, :, 3])) - {0, 255}
        if unexpected:
            raise ValueError(f"{path}: partial alpha values {sorted(unexpected)}")
        frames.append(array)
    return frames


def bounds(frame: np.ndarray) -> tuple[int, int, int, int]:
    rows, columns = np.nonzero(frame[:, :, 3] > 0)
    if not len(columns):
        raise ValueError("empty frame")
    return int(columns.min()), int(rows.min()), int(columns.max()), int(rows.max())


def foot_corridor(frames: list[np.ndarray], margin: int) -> tuple[int, int]:
    contacts = [frames[0], frames[3]]
    xs: list[int] = []
    for frame in contacts:
        alpha = frame[:, :, 3] > 0
        rows, columns = np.nonzero(alpha & (np.indices(alpha.shape)[0] >= GROUND_Y - 15))
        xs.extend(int(value) for value in columns)
    if not xs:
        raise ValueError("contact frames have no ground-adjacent pixels")
    return max(0, min(xs) - margin), min(255, max(xs) + margin)


def two_foot_clusters(frame: np.ndarray, corridor: tuple[int, int]) -> tuple[Foot, Foot]:
    alpha = frame[:, :, 3] > 0
    yy, xx = np.indices(alpha.shape)
    left, right = corridor
    sample = alpha & (yy >= GROUND_Y - FOOT_WINDOW_PX) & (xx >= left) & (xx <= right)
    rows, columns = np.nonzero(sample)
    if len(columns) < MIN_CLUSTER_PIXELS * 2:
        raise ValueError(f"insufficient foot-window pixels: {len(columns)}")

    centres = np.array([np.percentile(columns, 25), np.percentile(columns, 75)], dtype=np.float64)
    labels = np.zeros(len(columns), dtype=np.int8)
    for _ in range(16):
        distances = np.abs(columns[:, None] - centres[None, :])
        labels = np.argmin(distances, axis=1).astype(np.int8)
        updated = centres.copy()
        for index in range(2):
            selected = columns[labels == index]
            if len(selected) < MIN_CLUSTER_PIXELS:
                split = float(np.median(columns))
                labels = (columns > split).astype(np.int8)
                selected = columns[labels == index]
            if len(selected):
                updated[index] = float(selected.mean())
        if np.allclose(updated, centres, atol=0.01):
            break
        centres = updated

    feet: list[Foot] = []
    for index in range(2):
        selected_rows = rows[labels == index]
        selected_columns = columns[labels == index]
        if len(selected_columns) < MIN_CLUSTER_PIXELS:
            raise ValueError(f"foot cluster {index} has only {len(selected_columns)} pixels")
        bottom = int(selected_rows.max())
        core = selected_rows >= max(GROUND_Y - FOOT_CORE_PX, bottom - 17)
        feet.append(
            Foot(
                x=float(selected_columns[core].mean()),
                y=float(selected_rows[core].mean()),
                bottom=bottom,
                pixels=int(len(selected_columns)),
            )
        )
    feet.sort(key=lambda foot: foot.x)
    return feet[0], feet[1]


def distance(first: Foot, second: Foot) -> float:
    return math.hypot(first.x - second.x, first.y - second.y)


def assignment_distance(first: tuple[Foot, Foot], second: tuple[Foot, Foot]) -> float:
    straight = distance(first[0], second[0]) + distance(first[1], second[1])
    crossed = distance(first[0], second[1]) + distance(first[1], second[0])
    return min(straight, crossed) * 0.5


def contact_separation(feet: tuple[Foot, Foot]) -> float:
    return distance(feet[0], feet[1])


def foot_mask(frame: np.ndarray, corridor: tuple[int, int]) -> np.ndarray:
    alpha = frame[:, :, 3] > 0
    yy, xx = np.indices(alpha.shape)
    left, right = corridor
    return alpha & (yy >= GROUND_Y - FOOT_WINDOW_PX) & (xx >= left) & (xx <= right)


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    result = mask.copy()
    height, width = mask.shape
    for _ in range(radius):
        padded = np.pad(result, 1, constant_values=False)
        result = np.logical_or.reduce(
            tuple(padded[dy : dy + height, dx : dx + width] for dy in range(3) for dx in range(3))
        )
    return result


def mask_excursion(first: np.ndarray, second: np.ndarray, quantile: float = 0.75) -> float:
    """Return a fail-closed Chebyshev displacement for the moving part of two foot masks.

    One planted foot may overlap exactly, so the 75th percentile deliberately observes the
    other half of the silhouette.  A byte-identical/static foot region is always 0 px.
    """
    if not first.any() or not second.any():
        return 0.0

    def directed(source: np.ndarray, target: np.ndarray) -> int:
        expanded = target.copy()
        source_count = int(source.sum())
        for radius in range(0, 25):
            if int((source & expanded).sum()) / source_count >= quantile:
                return radius
            expanded = dilate(expanded, 1)
        return 25

    return float(max(directed(first, second), directed(second, first)))


def optical_foot_excursion(
    first_frame: np.ndarray,
    second_frame: np.ndarray,
    corridor: tuple[int, int],
    percentile: float = 95.0,
    vertical_only: bool = False,
) -> float:
    """Measure internal shoe/leg motion, not merely the outer alpha silhouette."""
    left, right = corridor
    top = GROUND_Y - FOOT_WINDOW_PX
    bottom = GROUND_Y + 1

    def composite(frame: np.ndarray) -> np.ndarray:
        rgba = frame[top:bottom, left : right + 1]
        alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
        # Neutral mid-grey makes both light and dark outline motion visible to dense flow.
        rgb = rgba[:, :, :3].astype(np.float32) * alpha + 127.0 * (1.0 - alpha)
        return cv2.cvtColor(rgb.astype(np.uint8), cv2.COLOR_RGB2GRAY)

    first = composite(first_frame)
    second = composite(second_frame)
    if np.array_equal(first, second):
        return 0.0
    flow = cv2.calcOpticalFlowFarneback(
        first,
        second,
        None,
        0.5,
        3,
        9,
        5,
        5,
        1.1,
        0,
    )
    magnitude = np.maximum(0.0, -flow[:, :, 1]) if vertical_only else np.linalg.norm(flow, axis=2)
    rgba_first = first_frame[top:bottom, left : right + 1]
    rgba_second = second_frame[top:bottom, left : right + 1]
    union = (rgba_first[:, :, 3] > 0) | (rgba_second[:, :, 3] > 0)
    delta = np.abs(rgba_first.astype(np.int16) - rgba_second.astype(np.int16))
    changed = union & ((delta[:, :, :3].max(axis=2) >= 12) | (delta[:, :, 3] >= 12))
    sample = magnitude[changed]
    if not len(sample):
        return 0.0
    return float(np.percentile(sample, percentile))


def foot_change_ratio(first_frame: np.ndarray, second_frame: np.ndarray, corridor: tuple[int, int]) -> float:
    first = foot_mask(first_frame, corridor)
    second = foot_mask(second_frame, corridor)
    region = first | second
    if not region.any():
        return 0.0
    delta = np.abs(first_frame.astype(np.int16) - second_frame.astype(np.int16))
    changed = (delta[:, :, :3].max(axis=2) >= 12) | (delta[:, :, 3] >= 12)
    return float((changed & region).sum() / region.sum())


def foot_silhouette_change_ratio(first: np.ndarray, second: np.ndarray) -> float:
    union = first | second
    if not union.any():
        return 0.0
    return float(np.logical_xor(first, second).sum() / union.sum())


def swing_signature(contact: np.ndarray, passing: np.ndarray, corridor: tuple[int, int]) -> tuple[int, int, int, float]:
    contact_mask = foot_mask(contact, corridor)
    passing_mask = foot_mask(passing, corridor)
    yy, _ = np.indices(contact_mask.shape)
    ground_band = yy >= GROUND_Y - 8
    elevated_band = (yy >= GROUND_Y - FOOT_WINDOW_PX) & (yy <= GROUND_Y - 3)
    # One-pixel tolerance ignores antialias-free edge stair-steps while still requiring a
    # meaningful cluster of contact pixels to leave the floor and reappear above it.
    evacuated = contact_mask & ground_band & ~dilate(passing_mask, 1)
    elevated = passing_mask & elevated_band & ~dilate(contact_mask, 1)
    support_ground = passing_mask & (yy == GROUND_Y)
    _, evacuated_x = np.nonzero(evacuated)
    centroid_x = float(evacuated_x.mean()) if len(evacuated_x) else -1.0
    return int(evacuated.sum()), int(elevated.sum()), int(support_ground.sum()), centroid_x


def upper_motion_metrics(
    frames: list[np.ndarray], lower_fraction: float
) -> tuple[float, float, float, float]:
    _, top, _, bottom = bounds(frames[0])
    end = int(round(top + (bottom - top + 1) * lower_fraction)) - 2
    reference = frames[0][:end]
    union = reference[:, :, 3] > 0
    changed_total = 0
    aligned_change_total = 0
    denominator = max(1, int(union.sum()))
    reference_rows, reference_columns = np.nonzero(union)
    reference_centroid = np.array(
        [float(reference_columns.mean()), float(reference_rows.mean())], dtype=np.float64
    )
    centroid_excursion = 0.0
    rigid_excursion = 0.0
    for frame in frames[1:]:
        other = frame[:end]
        delta = np.abs(reference.astype(np.int16) - other.astype(np.int16))
        changed = (delta[:, :, :3].max(axis=2) >= 12) | (delta[:, :, 3] >= 12)
        changed_total = max(changed_total, int(changed.sum()))
        rows, columns = np.nonzero(other[:, :, 3] > 0)
        if not len(rows):
            return 1.0, float("inf"), float("inf"), 1.0
        centroid = np.array([float(columns.mean()), float(rows.mean())], dtype=np.float64)
        centroid_excursion = max(centroid_excursion, float(np.linalg.norm(centroid - reference_centroid)))

        best_aligned = None
        best_shift = (0, 0)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                aligned = np.zeros_like(reference)
                source_y0 = max(0, -dy)
                source_y1 = min(end, end - dy)
                source_x0 = max(0, -dx)
                source_x1 = min(reference.shape[1], reference.shape[1] - dx)
                if source_y1 <= source_y0 or source_x1 <= source_x0:
                    continue
                aligned[
                    source_y0 + dy : source_y1 + dy,
                    source_x0 + dx : source_x1 + dx,
                ] = reference[source_y0:source_y1, source_x0:source_x1]
                aligned_delta = np.abs(aligned.astype(np.int16) - other.astype(np.int16))
                aligned_changed = (
                    (aligned_delta[:, :, :3].max(axis=2) >= 12) | (aligned_delta[:, :, 3] >= 12)
                )
                count = int(aligned_changed.sum())
                if best_aligned is None or count < best_aligned:
                    best_aligned = count
                    best_shift = (dx, dy)
        aligned_change_total = max(aligned_change_total, best_aligned or 0)
        rigid_excursion = max(
            rigid_excursion,
            math.sqrt(float(best_shift[0] * best_shift[0] + best_shift[1] * best_shift[1])),
        )
    return (
        float(changed_total / denominator),
        centroid_excursion,
        rigid_excursion,
        float(aligned_change_total / denominator),
    )


def measure_loop(character, direction: str, frames: list[np.ndarray], profile: dict[str, float | int]) -> LoopResult:
    failures: list[str] = []
    corridor = foot_corridor(frames, int(profile["footCorridorMarginPx"]))
    feet = [two_foot_clusters(frame, corridor) for frame in frames]
    masks = [foot_mask(frame, corridor) for frame in frames]
    contact0 = contact_separation(feet[0])
    contact3 = contact_separation(feet[3])
    contact_alternation = optical_foot_excursion(frames[0], frames[3], corridor)
    half02 = optical_foot_excursion(frames[0], frames[2], corridor)
    half35 = optical_foot_excursion(frames[3], frames[5], corridor)
    authored_adjacent = [
        optical_foot_excursion(frames[0], frames[1], corridor),
        optical_foot_excursion(frames[1], frames[2], corridor),
        optical_foot_excursion(frames[3], frames[4], corridor),
        optical_foot_excursion(frames[4], frames[5], corridor),
    ]
    adjacent_change = [
        foot_change_ratio(frames[0], frames[1], corridor),
        foot_change_ratio(frames[1], frames[2], corridor),
        foot_change_ratio(frames[3], frames[4], corridor),
        foot_change_ratio(frames[4], frames[5], corridor),
    ]
    adjacent_silhouette_change = [
        foot_silhouette_change_ratio(masks[0], masks[1]),
        foot_silhouette_change_ratio(masks[1], masks[2]),
        foot_silhouette_change_ratio(masks[3], masks[4]),
        foot_silhouette_change_ratio(masks[4], masks[5]),
    ]
    evacuated2, elevated2, support2, centroid2 = swing_signature(frames[0], frames[2], corridor)
    evacuated5, elevated5, support5, centroid5 = swing_signature(frames[3], frames[5], corridor)
    vertical_lift2 = optical_foot_excursion(frames[0], frames[2], corridor, vertical_only=True)
    vertical_lift5 = optical_foot_excursion(frames[3], frames[5], corridor, vertical_only=True)
    upper, upper_centroid, upper_rigid, aligned_upper = upper_motion_metrics(
        frames, float(profile["lowerBodyStart"])
    )

    if contact0 < MIN_CONTACT_SEPARATION_PX:
        failures.append(f"contact-0 separation {contact0:.2f}px < {MIN_CONTACT_SEPARATION_PX:.2f}px")
    if contact3 < MIN_CONTACT_SEPARATION_PX:
        failures.append(f"contact-3 separation {contact3:.2f}px < {MIN_CONTACT_SEPARATION_PX:.2f}px")
    if contact_alternation < MIN_CONTACT_ALTERNATION_EXCURSION_PX:
        failures.append(
            f"contact alternation excursion {contact_alternation:.2f}px < "
            f"{MIN_CONTACT_ALTERNATION_EXCURSION_PX:.2f}px"
        )
    if half02 < MIN_HALF_EXCURSION_PX:
        failures.append(f"phase 0->2 foot excursion {half02:.2f}px < {MIN_HALF_EXCURSION_PX:.2f}px")
    if half35 < MIN_HALF_EXCURSION_PX:
        failures.append(f"phase 3->5 foot excursion {half35:.2f}px < {MIN_HALF_EXCURSION_PX:.2f}px")
    minimum_adjacent = min(authored_adjacent)
    if minimum_adjacent < MIN_ADJACENT_EXCURSION_PX:
        failures.append(
            f"authored adjacent foot excursion {minimum_adjacent:.2f}px < {MIN_ADJACENT_EXCURSION_PX:.2f}px"
        )
    minimum_change = min(adjacent_change)
    if minimum_change < MIN_ADJACENT_CHANGE_RATIO:
        failures.append(
            f"adjacent foot-region change {minimum_change:.4f} < {MIN_ADJACENT_CHANGE_RATIO:.4f}"
        )
    minimum_silhouette_change = min(adjacent_silhouette_change)
    if minimum_silhouette_change < MIN_ADJACENT_SILHOUETTE_CHANGE_RATIO:
        failures.append(
            f"adjacent foot silhouette change {minimum_silhouette_change:.4f} < "
            f"{MIN_ADJACENT_SILHOUETTE_CHANGE_RATIO:.4f}"
        )
    if evacuated2 < MIN_SWING_GROUND_EVACUATION_PX:
        failures.append(f"phase-2 evacuated contact pixels {evacuated2} < {MIN_SWING_GROUND_EVACUATION_PX}")
    if evacuated5 < MIN_SWING_GROUND_EVACUATION_PX:
        failures.append(f"phase-5 evacuated contact pixels {evacuated5} < {MIN_SWING_GROUND_EVACUATION_PX}")
    if vertical_lift2 < MIN_SWING_VERTICAL_LIFT_PX:
        failures.append(f"phase-2 vertical swing lift {vertical_lift2:.2f}px < {MIN_SWING_VERTICAL_LIFT_PX:.2f}px")
    if vertical_lift5 < MIN_SWING_VERTICAL_LIFT_PX:
        failures.append(f"phase-5 vertical swing lift {vertical_lift5:.2f}px < {MIN_SWING_VERTICAL_LIFT_PX:.2f}px")
    if support2 < MIN_SUPPORT_GROUND_PIXELS:
        failures.append(f"phase-2 support ground pixels {support2} < {MIN_SUPPORT_GROUND_PIXELS}")
    if support5 < MIN_SUPPORT_GROUND_PIXELS:
        failures.append(f"phase-5 support ground pixels {support5} < {MIN_SUPPORT_GROUND_PIXELS}")
    if upper_rigid < MIN_UPPER_RIGID_EXCURSION_PX:
        failures.append(
            f"upper/body rigid weight excursion {upper_rigid:.2f}px < "
            f"{MIN_UPPER_RIGID_EXCURSION_PX:.2f}px"
        )
    if upper_rigid > MAX_UPPER_RIGID_EXCURSION_PX:
        failures.append(
            f"upper/body rigid weight excursion {upper_rigid:.2f}px > "
            f"{MAX_UPPER_RIGID_EXCURSION_PX:.2f}px"
        )
    if aligned_upper > MAX_ALIGNED_UPPER_IDENTITY_CHANGE_RATIO:
        failures.append(
            f"aligned upper identity change {aligned_upper:.4f} > "
            f"{MAX_ALIGNED_UPPER_IDENTITY_CHANGE_RATIO:.4f}"
        )

    return LoopResult(
        character=character.character_id,
        direction=direction,
        contactSeparation0Px=round(contact0, 3),
        contactSeparation3Px=round(contact3, 3),
        contactAlternationExcursionPx=round(contact_alternation, 3),
        swingExcursion02Px=round(half02, 3),
        swingExcursion35Px=round(half35, 3),
        minimumAuthoredAdjacentExcursionPx=round(minimum_adjacent, 3),
        minimumAdjacentFootChangeRatio=round(minimum_change, 6),
        minimumAdjacentFootSilhouetteChangeRatio=round(minimum_silhouette_change, 6),
        swingGroundEvacuation2Px=evacuated2,
        swingGroundEvacuation5Px=evacuated5,
        swingElevatedAddition2Px=elevated2,
        swingElevatedAddition5Px=elevated5,
        swingVerticalLift2Px=round(vertical_lift2, 3),
        swingVerticalLift5Px=round(vertical_lift5, 3),
        supportGroundPixels2=support2,
        supportGroundPixels5=support5,
        swingCentroidX2=round(centroid2, 3),
        swingCentroidX5=round(centroid5, 3),
        upperChangeRatio=round(upper, 6),
        upperCentroidExcursionPx=round(upper_centroid, 3),
        upperRigidExcursionPx=round(upper_rigid, 3),
        alignedUpperIdentityChangeRatio=round(aligned_upper, 6),
        verdict="PASS" if not failures else "FAIL",
        failures=failures,
    )


def main() -> int:
    args = parse_args()
    candidate_root = args.candidate_root.resolve() if args.candidate_root else None
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    profiles = load_profiles()
    results: list[LoopResult] = []
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            frames = load_frames(candidate_root, character, direction)
            try:
                result = measure_loop(character, direction, frames, profiles[character.character_id])
            except (ValueError, FloatingPointError) as error:
                result = LoopResult(
                    character=character.character_id,
                    direction=direction,
                    contactSeparation0Px=0.0,
                    contactSeparation3Px=0.0,
                    contactAlternationExcursionPx=0.0,
                    swingExcursion02Px=0.0,
                    swingExcursion35Px=0.0,
                    minimumAuthoredAdjacentExcursionPx=0.0,
                    minimumAdjacentFootChangeRatio=0.0,
                    minimumAdjacentFootSilhouetteChangeRatio=0.0,
                    swingGroundEvacuation2Px=0,
                    swingGroundEvacuation5Px=0,
                    swingElevatedAddition2Px=0,
                    swingElevatedAddition5Px=0,
                    swingVerticalLift2Px=0.0,
                    swingVerticalLift5Px=0.0,
                    supportGroundPixels2=0,
                    supportGroundPixels5=0,
                    swingCentroidX2=-1.0,
                    swingCentroidX5=-1.0,
                    upperChangeRatio=1.0,
                    upperCentroidExcursionPx=0.0,
                    upperRigidExcursionPx=0.0,
                    alignedUpperIdentityChangeRatio=1.0,
                    verdict="FAIL",
                    failures=[f"unmeasurable: {error}"],
                )
            results.append(result)

    failed = [result for result in results if result.verdict != "PASS"]
    payload = {
        "schemaVersion": 1,
        "contract": "FC-CHARACTER-LOCOMOTION-QA-V1",
        "source": str(candidate_root) if candidate_root else "runtime",
        "thresholds": {
            "minimumContactSeparationPx": MIN_CONTACT_SEPARATION_PX,
            "minimumContactAlternationExcursionPx": MIN_CONTACT_ALTERNATION_EXCURSION_PX,
            "minimumHalfExcursionPx": MIN_HALF_EXCURSION_PX,
            "minimumAuthoredAdjacentExcursionPx": MIN_ADJACENT_EXCURSION_PX,
            "minimumAdjacentFootChangeRatio": MIN_ADJACENT_CHANGE_RATIO,
            "minimumAdjacentFootSilhouetteChangeRatio": MIN_ADJACENT_SILHOUETTE_CHANGE_RATIO,
            "minimumSwingGroundEvacuationPx": MIN_SWING_GROUND_EVACUATION_PX,
            "minimumSwingVerticalLiftPx": MIN_SWING_VERTICAL_LIFT_PX,
            "minimumSupportGroundPixels": MIN_SUPPORT_GROUND_PIXELS,
            "minimumUpperRigidExcursionPx": MIN_UPPER_RIGID_EXCURSION_PX,
            "maximumUpperRigidExcursionPx": MAX_UPPER_RIGID_EXCURSION_PX,
            "maximumAlignedUpperIdentityChangeRatio": MAX_ALIGNED_UPPER_IDENTITY_CHANGE_RATIO,
        },
        "summary": {"characters": 12, "loops": len(results), "passed": len(results) - len(failed), "failed": len(failed)},
        "loops": [asdict(result) for result in results],
    }
    (output / "character-locomotion-qa-v1.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n"
    )
    with (output / "character-locomotion-qa-v1.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(asdict(results[0]).keys()))
        writer.writeheader()
        for result in results:
            row = asdict(result)
            row["failures"] = " | ".join(result.failures)
            writer.writerow(row)

    print(
        f"FC-CHARACTER-LOCOMOTION-QA-V1: {'PASS' if not failed else 'FAIL'} | "
        f"characters=12 loops={len(results)} passed={len(results)-len(failed)} failed={len(failed)} source={payload['source']}"
    )
    for result in failed[:40]:
        print(f"FAIL {result.character}/{result.direction}: {'; '.join(result.failures)}")
    if len(failed) > 40:
        print(f"... {len(failed) - 40} additional failing loops in JSON/CSV")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
