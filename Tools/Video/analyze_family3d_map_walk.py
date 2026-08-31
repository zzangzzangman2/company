#!/usr/bin/env python3
"""Measure actual-map family 3D walk receipts without treating metrics as visual approval."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import numpy as np


def percentile(values: list[float], amount: float) -> float:
    return float(np.percentile(np.asarray(values, dtype=np.float64), amount))


def correlation(left: list[float], right: list[float]) -> float:
    if len(left) < 2 or np.std(left) < 1e-9 or np.std(right) < 1e-9:
        return 0.0
    return float(np.corrcoef(left, right)[0, 1])


def analyse(receipt_path: Path, turn_settle_samples: int) -> dict:
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    samples = receipt.get("fatherCaptureSamples", [])
    if len(samples) < 2:
        raise ValueError(f"Receipt has fewer than two samples: {receipt_path}")

    leg_starts: dict[tuple[int, int], int] = {}
    for index, sample in enumerate(samples):
        leg_starts.setdefault((sample["routeCircuit"], sample["routeLeg"]), index)

    foot_metrics = {}
    for side in ("left", "right"):
        planted_key = f"{side}FootPlanted"
        local_key = f"{side}FootLocal"
        world_key = f"{side}FootWorld"
        reported_contacts = sum(bool(sample.get(planted_key)) for sample in samples)
        adaptive_threshold = percentile(
            [sample[local_key]["y"] for sample in samples], 35.0
        )
        speeds: list[float] = []

        for index in range(1, len(samples)):
            previous = samples[index - 1]
            current = samples[index]
            key = (current["routeCircuit"], current["routeLeg"])
            if (previous["routeCircuit"], previous["routeLeg"]) != key:
                continue
            if index - leg_starts[key] < turn_settle_samples:
                continue
            if reported_contacts:
                contacting = bool(previous.get(planted_key)) and bool(current.get(planted_key))
            else:
                contacting = (
                    previous[local_key]["y"] <= adaptive_threshold
                    and current[local_key]["y"] <= adaptive_threshold
                )
            if not contacting:
                continue

            delta_seconds = current["simulationSeconds"] - previous["simulationSeconds"]
            if delta_seconds <= 0:
                continue
            before = previous[world_key]
            after = current[world_key]
            speed = math.hypot(after["x"] - before["x"], after["z"] - before["z"])
            speeds.append(speed / delta_seconds)

        array = np.asarray(speeds, dtype=np.float64)
        foot_metrics[side] = {
            "contactSource": "authored613Phase" if reported_contacts else "adaptiveLowest35Percent",
            "reportedContactSamples": reported_contacts,
            "adaptiveHeightThreshold": adaptive_threshold,
            "measuredContactIntervals": len(speeds),
            "horizontalWorldSpeedMean": float(np.mean(array)),
            "horizontalWorldSpeedMedian": float(np.median(array)),
            "horizontalWorldSpeedP90": float(np.percentile(array, 90)),
            "horizontalWorldSpeedP95": float(np.percentile(array, 95)),
            "horizontalWorldSpeedRms": float(np.sqrt(np.mean(np.square(array)))),
        }

    root_speeds: list[float] = []
    facing_errors: list[float] = []
    for index in range(1, len(samples)):
        previous = samples[index - 1]
        current = samples[index]
        key = (current["routeCircuit"], current["routeLeg"])
        if (previous["routeCircuit"], previous["routeLeg"]) != key:
            continue
        delta_seconds = current["simulationSeconds"] - previous["simulationSeconds"]
        dx = current["rootWorldPosition"]["x"] - previous["rootWorldPosition"]["x"]
        dz = current["rootWorldPosition"]["z"] - previous["rootWorldPosition"]["z"]
        distance = math.hypot(dx, dz)
        if delta_seconds > 0:
            root_speeds.append(distance / delta_seconds)
        if index - leg_starts[key] < turn_settle_samples or distance < 1e-5:
            continue

        toe = current["toeForwardLocal"]
        yaw = math.radians(current["rootWorldYawDegrees"])
        forward_x = math.cos(yaw) * toe["x"] + math.sin(yaw) * toe["z"]
        forward_z = -math.sin(yaw) * toe["x"] + math.cos(yaw) * toe["z"]
        denominator = math.hypot(forward_x, forward_z) * distance
        dot = (forward_x * dx + forward_z * dz) / max(denominator, 1e-9)
        facing_errors.append(math.degrees(math.acos(max(-1.0, min(1.0, dot)))))

    torso_leans = [
        math.degrees(math.acos(max(-1.0, min(1.0, sample["torsoUpLocal"]["y"]))))
        for sample in samples
    ]
    left_foot_z = [sample["leftFootLocal"]["z"] for sample in samples]
    right_foot_z = [sample["rightFootLocal"]["z"] for sample in samples]
    left_hand_z = [sample["leftHandLocal"]["z"] for sample in samples]
    right_hand_z = [sample["rightHandLocal"]["z"] for sample in samples]

    combined_contact_speeds = []
    for metrics in foot_metrics.values():
        # Weighted aggregate is reconstructed from per-side means only for a compact comparison.
        combined_contact_speeds.extend(
            [metrics["horizontalWorldSpeedMean"]] * metrics["measuredContactIntervals"]
        )

    return {
        "receipt": str(receipt_path.resolve()),
        "receiptStatus": receipt.get("receiptStatus", ""),
        "sourceFamilyId": receipt.get("fatherMapWalkSourceFamilyId", ""),
        "productionMutation": receipt.get("productionMutation"),
        "productionEligible": receipt.get("productionEligible"),
        "captures": receipt.get("compositeCapturedFrames"),
        "samples": len(samples),
        "cycleSeconds": receipt.get("sharedCycleSeconds"),
        "strideOfficeUnits": receipt.get("fatherMotionStrideOfficeUnits"),
        "facingOffsetDegrees": receipt.get("fatherMotionFacingOffsetDegrees"),
        "routeCircuits": max(sample["routeCircuit"] for sample in samples) + 1,
        "routeLegCount": len({sample["routeLeg"] for sample in samples}),
        "rootHorizontalSpeedMedian": float(np.median(root_speeds)),
        "settledFacingErrorDegrees": {
            "mean": float(np.mean(facing_errors)),
            "median": float(np.median(facing_errors)),
            "p95": percentile(facing_errors, 95.0),
            "maximum": max(facing_errors),
        },
        "torsoLeanDegrees": {
            "minimum": min(torso_leans),
            "mean": float(np.mean(torso_leans)),
            "maximum": max(torso_leans),
        },
        "leftRightFootForwardCorrelation": correlation(left_foot_z, right_foot_z),
        "leftRightHandForwardCorrelation": correlation(left_hand_z, right_hand_z),
        "footContact": foot_metrics,
        "contactSpeedMeanOfSides": float(np.mean(combined_contact_speeds)),
        "warning": "Numeric support only. Full ordered-frame and animated GIF visual review remains mandatory.",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("receipts", nargs="+", type=Path)
    parser.add_argument("--turn-settle-samples", type=int, default=30)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    report = {
        "contract": "FC-FAMILY-3D-MAP-WALK-MEASUREMENT-V1",
        "turnSettleSamples": args.turn_settle_samples,
        "results": [analyse(path, args.turn_settle_samples) for path in args.receipts],
    }
    encoded = json.dumps(report, indent=2, ensure_ascii=False)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded + "\n", encoding="utf-8")
        print(f"REPORT={args.output.resolve()}")
    else:
        print(encoded)


if __name__ == "__main__":
    main()
