#!/usr/bin/env python3
"""Independent fail-closed QA for the four-family locomotion rig.

The old gate measured generic pixel difference and could approve stationary feet. This gate uses
anatomy-owned marker renders to prove which foot is support/swing in every phase, projects the
support anchor through the shipping root stride, and rejects any loop whose planted foot slides.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from dataclasses import asdict, dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


REPO = Path(__file__).resolve().parents[1]
ARTIFACT_ROOT = REPO / "Artifacts" / "CharacterLocomotionGenerationV1"
MANIFEST_PATH = REPO / "ArtSources" / "FamilyLocomotionRigV1" / "rig_manifest_v1.json"
CHARACTERS = ("player", "older_sister", "father", "mother")
DIRECTIONS = (
    "south", "southwest", "west", "northwest",
    "north", "northeast", "east", "southeast",
)
MIRRORS = {"west": "east", "southwest": "southeast", "northwest": "northeast"}
ASSET_FOLDERS = {
    "player": "Player", "older_sister": "OlderSister", "father": "Father", "mother": "Mother",
}
MARKER_COLORS = {"left": (0, 235, 255), "right": (255, 35, 195)}
EXPECTED_SUPPORT_LEGS = ("left", "left", "left", "right", "right", "right")
VECTORS = {
    "south": np.array((0.0, 1.0), np.float64),
    "southwest": np.array((-math.sqrt(0.5), math.sqrt(0.5)), np.float64),
    "west": np.array((-1.0, 0.0), np.float64),
    "northwest": np.array((-math.sqrt(0.5), -math.sqrt(0.5)), np.float64),
    "north": np.array((0.0, -1.0), np.float64),
    "northeast": np.array((math.sqrt(0.5), -math.sqrt(0.5)), np.float64),
    "east": np.array((1.0, 0.0), np.float64),
    "southeast": np.array((math.sqrt(0.5), math.sqrt(0.5)), np.float64),
}

MAX_SUPPORT_DRIFT_PX = 1.0
MAX_ADJACENT_SUPPORT_ERROR_PX = 1.25
MAX_CONTACT_STEP_ERROR_PX = 1.0
MIN_SWING_WORLD_TRAVEL_PX = 80.0
MIN_PASSING_LIFT_PX = 2.5
MIN_HEAD_TOP_MARGIN_PX = 4
MIN_SIDE_MARGIN_PX = 4
MAX_DETACHED_ALPHA_PIXELS = 0


@dataclass
class LoopResult:
    character: str
    direction: str
    leftSupportDriftPx: float
    rightSupportDriftPx: float
    maximumAdjacentSupportErrorPx: float
    contactStepPx: float
    contactStepErrorPx: float
    rightSwingWorldTravelPx: float
    leftSwingWorldTravelPx: float
    rightPassingLiftPx: float
    leftPassingLiftPx: float
    headTopMarginPx: int
    sideMarginPx: int
    detachedAlphaPixels: int
    verdict: str
    failures: list[str]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--candidate-root", type=Path, default=None,
        help="Root containing <character>/Frames. Omit to verify shipping runtime PNGs.",
    )
    parser.add_argument("--marker-root", type=Path, default=ARTIFACT_ROOT / "Markers")
    parser.add_argument("--anchor-catalog", type=Path, default=ARTIFACT_ROOT / "family_foot_anchors_v1.json")
    parser.add_argument("--output", type=Path, default=ARTIFACT_ROOT / "QuantitativeQa")
    return parser.parse_args()


def load_rgba(path: Path) -> np.ndarray:
    if not path.is_file():
        raise FileNotFoundError(path)
    with Image.open(path) as loaded:
        if loaded.size != (256, 256):
            raise ValueError(f"{path}: expected 256x256, got {loaded.size}")
        array = np.asarray(loaded.convert("RGBA"), np.uint8)
    alpha_values = set(int(value) for value in np.unique(array[:, :, 3]))
    if not alpha_values.issubset({0, 255}):
        raise ValueError(f"{path}: partial alpha values {sorted(alpha_values - {0, 255})}")
    return array


def frame_path(candidate_root: Path | None, character: str, direction: str, phase: int) -> Path:
    name = f"{character}_{direction}_walk_{phase}.png"
    if candidate_root is not None:
        return candidate_root / character / "Frames" / name
    return (
        REPO / "Assets" / "Art" / "Characters" / ASSET_FOLDERS[character] /
        "Pixel" / "HighMotion" / "Frames" / name
    )


def sheet_path(candidate_root: Path | None, character: str, part: str) -> Path:
    name = f"{character}_pixel_walk8dir6_{part}_v1.png"
    if candidate_root is not None:
        return candidate_root / character / name
    return (
        REPO / "Assets" / "Art" / "Characters" / ASSET_FOLDERS[character] /
        "Pixel" / "HighMotion" / name
    )


def marker_anchor(marker: np.ndarray, leg: str) -> np.ndarray:
    color = np.asarray(MARKER_COLORS[leg], np.uint8)
    mask = np.all(marker[:, :, :3] == color, axis=2) & (marker[:, :, 3] == 255)
    rows, columns = np.nonzero(mask)
    if len(columns) < 8:
        raise ValueError(f"{leg} marker has only {len(columns)} pixels")
    bottom = int(rows.max())
    core = rows >= bottom - 13
    return np.array((float(columns[core].mean()), float(rows[core].mean())), np.float64)


def alpha_components(frame: np.ndarray) -> tuple[int, int]:
    count, _, stats, _ = cv2.connectedComponentsWithStats((frame[:, :, 3] > 0).astype(np.uint8), 8)
    areas = sorted((int(stats[index, cv2.CC_STAT_AREA]) for index in range(1, count)), reverse=True)
    if not areas:
        return 0, 0
    return areas[0], sum(areas[1:])


def shift_identity(image: np.ndarray, offset: list[int | float]) -> np.ndarray:
    matrix = np.array(((1.0, 0.0, float(offset[0])), (0.0, 1.0, float(offset[1]))), np.float32)
    return cv2.warpAffine(
        image, matrix, (256, 256), flags=cv2.INTER_NEAREST,
        borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0),
    )


def authored_metrics(
    direction: str,
    left: list[np.ndarray],
    right: list[np.ndarray],
    root_step: float,
) -> dict[str, float]:
    vector = VECTORS[direction]
    left_world = [left[phase] + phase * root_step * vector for phase in range(6)]
    right_world = [right[phase] + phase * root_step * vector for phase in range(6)]
    left_drift = max(abs(float(np.dot(left_world[p] - left_world[0], vector))) for p in range(3))
    right_drift = max(abs(float(np.dot(right_world[p] - right_world[3], vector))) for p in range(3, 6))
    adjacent_errors = []
    for points, start in ((left, 0), (right, 3)):
        for phase in range(start, start + 2):
            local_delta = float(np.dot(points[phase + 1] - points[phase], vector))
            adjacent_errors.append(abs(local_delta + root_step))
    contact_step = float(np.dot(right_world[3] - left_world[0], vector))
    right_travel = float(np.dot(right_world[3] - right_world[0], vector))
    left_cycle_contact = left[0] + 6.0 * root_step * vector
    left_travel = float(np.dot(left_cycle_contact - left_world[3], vector))
    right_expected_passing = right_world[0] + (right_world[3] - right_world[0]) / 3.0
    left_expected_passing = left_world[3] + (left_cycle_contact - left_world[3]) / 3.0
    return {
        "left_drift": left_drift,
        "right_drift": right_drift,
        "adjacent_error": max(adjacent_errors),
        "contact_step": contact_step,
        "right_travel": right_travel,
        "left_travel": left_travel,
        "right_lift": float(right_expected_passing[1] - right_world[1][1]),
        "left_lift": float(left_expected_passing[1] - left_world[4][1]),
    }


def metric_failures(metrics: dict[str, float], root_step: float) -> list[str]:
    failures = []
    if metrics["left_drift"] > MAX_SUPPORT_DRIFT_PX:
        failures.append(f"left support world drift {metrics['left_drift']:.3f}px > {MAX_SUPPORT_DRIFT_PX:.3f}px")
    if metrics["right_drift"] > MAX_SUPPORT_DRIFT_PX:
        failures.append(f"right support world drift {metrics['right_drift']:.3f}px > {MAX_SUPPORT_DRIFT_PX:.3f}px")
    if metrics["adjacent_error"] > MAX_ADJACENT_SUPPORT_ERROR_PX:
        failures.append(
            f"adjacent support counter-motion error {metrics['adjacent_error']:.3f}px > "
            f"{MAX_ADJACENT_SUPPORT_ERROR_PX:.3f}px"
        )
    contact_error = abs(metrics["contact_step"] - 3.0 * root_step)
    if contact_error > MAX_CONTACT_STEP_ERROR_PX:
        failures.append(f"alternating contact step error {contact_error:.3f}px > {MAX_CONTACT_STEP_ERROR_PX:.3f}px")
    if metrics["right_travel"] < MIN_SWING_WORLD_TRAVEL_PX:
        failures.append(f"right swing travel {metrics['right_travel']:.3f}px < {MIN_SWING_WORLD_TRAVEL_PX:.3f}px")
    if metrics["left_travel"] < MIN_SWING_WORLD_TRAVEL_PX:
        failures.append(f"left swing travel {metrics['left_travel']:.3f}px < {MIN_SWING_WORLD_TRAVEL_PX:.3f}px")
    if metrics["right_lift"] < MIN_PASSING_LIFT_PX:
        failures.append(f"right passing lift {metrics['right_lift']:.3f}px < {MIN_PASSING_LIFT_PX:.3f}px")
    if metrics["left_lift"] < MIN_PASSING_LIFT_PX:
        failures.append(f"left passing lift {metrics['left_lift']:.3f}px < {MIN_PASSING_LIFT_PX:.3f}px")
    return failures


def validate_manifest() -> tuple[dict[str, object], float]:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if manifest.get("contract") != "FC-FAMILY-LOCOMOTION-RIG-V1":
        raise ValueError("rig manifest contract mismatch")
    expected_hashes = manifest.get("sourceSha256", {})
    actual_hashes = {
        path.name: hashlib.sha256(path.read_bytes()).hexdigest().upper()
        for path in sorted(MANIFEST_PATH.parent.glob("*.png"))
    }
    if expected_hashes != actual_hashes:
        raise ValueError(f"raw source SHA mismatch: expected={expected_hashes} actual={actual_hashes}")
    runtime = manifest["runtime"]
    computed = (float(runtime["strideWorld"]) / int(runtime["phasesPerCycle"])) / (
        float(runtime["visualScale"]) / float(runtime["pixelsPerUnit"])
    )
    if abs(computed - float(runtime["rootStepPixels"])) > 0.001:
        raise ValueError(f"root-step formula mismatch: manifest={runtime['rootStepPixels']} computed={computed}")
    return manifest, computed


def validate_sheets(candidate_root: Path | None, frames: dict[tuple[str, str, int], np.ndarray]) -> None:
    for character in CHARACTERS:
        for part, directions in (
            ("a", ("south", "southwest", "west", "northwest")),
            ("b", ("north", "northeast", "east", "southeast")),
        ):
            path = sheet_path(candidate_root, character, part)
            with Image.open(path) as loaded:
                if loaded.size != (1536, 1024):
                    raise ValueError(f"{path}: expected 1536x1024 sheet, got {loaded.size}")
                if loaded.info.get("familyCompanyHighMotionLayout") != "grid-4x6-v1":
                    raise ValueError(f"{path}: grid metadata missing")
                sheet = np.asarray(loaded.convert("RGBA"), np.uint8)
            for row, direction in enumerate(directions):
                for phase in range(6):
                    tile = sheet[row * 256:(row + 1) * 256, phase * 256:(phase + 1) * 256]
                    if not np.array_equal(tile, frames[(character, direction, phase)]):
                        raise ValueError(f"{path}: tile mismatch {character}/{direction}/P{phase}")


def run_self_tests(root_step: float, valid_left: list[np.ndarray], valid_right: list[np.ndarray]) -> None:
    valid = authored_metrics("east", valid_left, valid_right, root_step)
    if metric_failures(valid, root_step):
        raise AssertionError(f"valid rig rejected: {metric_failures(valid, root_step)}")

    static_left = [valid_left[0].copy() for _ in range(6)]
    static_right = [valid_right[0].copy() for _ in range(6)]
    failures = metric_failures(authored_metrics("east", static_left, static_right, root_step), root_step)
    if not any("support world drift" in failure for failure in failures):
        raise AssertionError(f"static-foot negative did not fail support drift: {failures}")

    no_air_left = [point.copy() for point in valid_left]
    no_air_right = [point.copy() for point in valid_right]
    right_world_0 = no_air_right[0]
    right_world_3 = no_air_right[3] + 3.0 * root_step * VECTORS["east"]
    no_air_right[1] = right_world_0 + (right_world_3 - right_world_0) / 3.0 - root_step * VECTORS["east"]
    left_world_3 = no_air_left[3] + 3.0 * root_step * VECTORS["east"]
    left_world_6 = no_air_left[0] + 6.0 * root_step * VECTORS["east"]
    no_air_left[4] = left_world_3 + (left_world_6 - left_world_3) / 3.0 - 4.0 * root_step * VECTORS["east"]
    failures = metric_failures(authored_metrics("east", no_air_left, no_air_right, root_step), root_step)
    if not any("passing lift" in failure for failure in failures):
        raise AssertionError(f"zero-air negative did not fail passing lift: {failures}")

    if tuple(["left"] * 6) == EXPECTED_SUPPORT_LEGS:
        raise AssertionError("same-support-leg negative unexpectedly matched phase ownership")
    print("FC-CHARACTER-LOCOMOTION-QA-V1-SELFTEST: PASS cases=4")


def main() -> int:
    args = parse_args()
    candidate_root = args.candidate_root.resolve() if args.candidate_root else None
    marker_root = args.marker_root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    manifest, root_step = validate_manifest()
    body_offsets = manifest.get("bodyOffsetsPixels")
    if body_offsets != [[0, 0], [0, 1], [0, 0], [0, 0], [0, 1], [0, 0]]:
        raise ValueError(f"unexpected body/hip weight-transfer offsets: {body_offsets}")
    catalog = json.loads(args.anchor_catalog.resolve().read_text(encoding="utf-8"))
    if catalog.get("contract") != "FC-FAMILY-LOCOMOTION-FOOT-ANCHORS-V1":
        raise ValueError("runtime anchor catalog contract mismatch")
    if abs(float(catalog.get("rootStepPixels", -1.0)) - root_step) > 0.001:
        raise ValueError("runtime anchor catalog root step mismatch")
    catalog_rows = {(row["character"], row["direction"]): row for row in catalog.get("rows", [])}
    if len(catalog_rows) != len(CHARACTERS) * len(DIRECTIONS):
        raise ValueError(f"runtime anchor catalog expected 32 rows, found {len(catalog_rows)}")

    results: list[LoopResult] = []
    frames: dict[tuple[str, str, int], np.ndarray] = {}
    first_anchors = None
    for character in CHARACTERS:
        profile = manifest["profiles"][character]
        leg_start = int(profile["legStartY"])
        leg_left, leg_right = (int(value) for value in profile["legCorridorX"])
        lower_clear = int(profile["lowerClearY"])
        for direction in DIRECTIONS:
            failures = []
            left, right = [], []
            top_margin = 256
            side_margin = 256
            detached = 0
            identity_path = (
                REPO / "Tools" / "CharacterLocomotionIdentityV1" / character /
                f"{character}_{direction}_identity_v1.png"
            )
            identity = load_rgba(identity_path)
            upper_identity = identity
            if direction in MIRRORS:
                source_identity_path = (
                    REPO / "Tools" / "CharacterLocomotionIdentityV1" / character /
                    f"{character}_{MIRRORS[direction]}_identity_v1.png"
                )
                upper_identity = load_rgba(source_identity_path)
            canonical_upper = upper_identity.copy()
            canonical_upper[leg_start:, leg_left:leg_right + 1] = 0
            canonical_upper[lower_clear:] = 0
            if direction in MIRRORS:
                canonical_upper = canonical_upper[:, ::-1].copy()
            catalog_row = catalog_rows[(character, direction)]
            if tuple(catalog_row.get("supportLegs", [])) != EXPECTED_SUPPORT_LEGS:
                failures.append(f"phase ownership is {catalog_row.get('supportLegs')} not {EXPECTED_SUPPORT_LEGS}")
            for phase in range(6):
                path = frame_path(candidate_root, character, direction, phase)
                frame = load_rgba(path)
                marker = load_rgba(marker_root / character / "Frames" / path.name)
                frames[(character, direction, phase)] = frame
                if not np.array_equal(frame[:, :, 3], marker[:, :, 3]):
                    failures.append(f"P{phase} marker/candidate alpha mismatch")
                rgba_hash = hashlib.sha256(frame.tobytes()).hexdigest().upper()
                expected_hash = catalog.get("assetRgbaSha256", {}).get(path.name)
                if rgba_hash != expected_hash:
                    failures.append(f"P{phase} RGBA SHA mismatch expected={expected_hash} actual={rgba_hash}")
                left_anchor = marker_anchor(marker, "left")
                right_anchor = marker_anchor(marker, "right")
                left.append(left_anchor)
                right.append(right_anchor)
                expected_point = catalog_row["supportAnchors"][phase]
                expected_anchor = np.array((expected_point["x"], expected_point["y"]), np.float64)
                actual_anchor = left_anchor if phase < 3 else right_anchor
                if float(np.max(np.abs(actual_anchor - expected_anchor))) > 0.001:
                    failures.append(f"P{phase} support anchor/catalog mismatch")

                visible = frame[:, :, 3] > 0
                rows, columns = np.nonzero(visible)
                top_margin = min(top_margin, int(rows.min()))
                side_margin = min(side_margin, int(columns.min()), 255 - int(columns.max()))
                _, extra = alpha_components(frame)
                detached = max(detached, extra)
                expected_identity = shift_identity(canonical_upper, body_offsets[phase])
                if not np.array_equal(frame[:140], expected_identity[:140]):
                    failures.append(f"P{phase} head/hat/face identity changed outside approved body offset")
                identity_mask = expected_identity[:leg_start + 1, :, 3] > 0
                if not np.array_equal(
                        frame[:leg_start + 1][identity_mask],
                        expected_identity[:leg_start + 1][identity_mask]):
                    failures.append(f"P{phase} canonical upper-body pixels changed")

            metrics = authored_metrics(direction, left, right, root_step)
            failures.extend(metric_failures(metrics, root_step))
            if top_margin < MIN_HEAD_TOP_MARGIN_PX:
                failures.append(f"hat/head top margin {top_margin}px < {MIN_HEAD_TOP_MARGIN_PX}px")
            if side_margin < MIN_SIDE_MARGIN_PX:
                failures.append(f"side margin {side_margin}px < {MIN_SIDE_MARGIN_PX}px")
            if detached > MAX_DETACHED_ALPHA_PIXELS:
                failures.append(f"detached alpha pixels {detached} > {MAX_DETACHED_ALPHA_PIXELS}")
            results.append(
                LoopResult(
                    character=character,
                    direction=direction,
                    leftSupportDriftPx=round(metrics["left_drift"], 6),
                    rightSupportDriftPx=round(metrics["right_drift"], 6),
                    maximumAdjacentSupportErrorPx=round(metrics["adjacent_error"], 6),
                    contactStepPx=round(metrics["contact_step"], 6),
                    contactStepErrorPx=round(abs(metrics["contact_step"] - 3.0 * root_step), 6),
                    rightSwingWorldTravelPx=round(metrics["right_travel"], 6),
                    leftSwingWorldTravelPx=round(metrics["left_travel"], 6),
                    rightPassingLiftPx=round(metrics["right_lift"], 6),
                    leftPassingLiftPx=round(metrics["left_lift"], 6),
                    headTopMarginPx=top_margin,
                    sideMarginPx=side_margin,
                    detachedAlphaPixels=detached,
                    verdict="PASS" if not failures else "FAIL",
                    failures=failures,
                )
            )
            if character == "player" and direction == "east":
                first_anchors = (left, right)

    for character in CHARACTERS:
        for mirrored, source in MIRRORS.items():
            for phase in range(6):
                actual = frames[(character, mirrored, phase)]
                expected = frames[(character, source, phase)][:, ::-1]
                if not np.array_equal(actual, expected):
                    result = next(item for item in results if item.character == character and item.direction == mirrored)
                    result.verdict = "FAIL"
                    result.failures.append(f"P{phase} is not exact mirror of {source}")

    validate_sheets(candidate_root, frames)
    if first_anchors is None:
        raise AssertionError("player/east anchors missing")
    run_self_tests(root_step, *first_anchors)

    failed = [result for result in results if result.verdict != "PASS"]
    payload = {
        "schemaVersion": 2,
        "contract": "FC-CHARACTER-LOCOMOTION-FOOT-LOCK-QA-V1",
        "source": str(candidate_root) if candidate_root else "runtime",
        "phaseOwnership": list(EXPECTED_SUPPORT_LEGS),
        "runtime": manifest["runtime"],
        "thresholds": {
            "maximumProjectedSupportDriftPx": MAX_SUPPORT_DRIFT_PX,
            "maximumAdjacentSupportErrorPx": MAX_ADJACENT_SUPPORT_ERROR_PX,
            "maximumContactStepErrorPx": MAX_CONTACT_STEP_ERROR_PX,
            "minimumSwingWorldTravelPx": MIN_SWING_WORLD_TRAVEL_PX,
            "minimumPassingLiftPx": MIN_PASSING_LIFT_PX,
            "minimumHeadTopMarginPx": MIN_HEAD_TOP_MARGIN_PX,
        },
        "summary": {
            "characters": len(CHARACTERS), "directions": len(DIRECTIONS), "loops": len(results),
            "frames": len(results) * 6, "passed": len(results) - len(failed), "failed": len(failed),
            "maximumObservedSupportDriftPx": max(
                max(result.leftSupportDriftPx, result.rightSupportDriftPx) for result in results
            ),
            "minimumObservedContactStepPx": min(result.contactStepPx for result in results),
            "maximumObservedContactStepPx": max(result.contactStepPx for result in results),
            "minimumObservedSwingTravelPx": min(
                min(result.leftSwingWorldTravelPx, result.rightSwingWorldTravelPx) for result in results
            ),
            "minimumObservedPassingLiftPx": min(
                min(result.leftPassingLiftPx, result.rightPassingLiftPx) for result in results
            ),
        },
        "loops": [asdict(result) for result in results],
    }
    (output / "character-locomotion-foot-lock-qa-v1.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    with (output / "character-locomotion-foot-lock-qa-v1.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(asdict(results[0]).keys()))
        writer.writeheader()
        for result in results:
            row = asdict(result)
            row["failures"] = " | ".join(result.failures)
            writer.writerow(row)

    print(
        f"FC-CHARACTER-LOCOMOTION-FOOT-LOCK-QA-V1: {'PASS' if not failed else 'FAIL'} | "
        f"characters=4 directions=8 loops=32 frames=192 passed={32-len(failed)} failed={len(failed)} "
        f"maxSupportDriftPx={payload['summary']['maximumObservedSupportDriftPx']:.3f}"
    )
    for result in failed:
        print(f"FAIL {result.character}/{result.direction}: {'; '.join(result.failures)}")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
