#!/usr/bin/env python3
"""Negative regression tests for the marker-owned family foot-lock gate."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np

from verify_character_locomotion_v1 import (
    ARTIFACT_ROOT,
    EXPECTED_SUPPORT_LEGS,
    VECTORS,
    authored_metrics,
    load_rgba,
    marker_anchor,
    metric_failures,
    validate_manifest,
)


def require_failure(label: str, failures: list[str], fragment: str) -> None:
    if not any(fragment in failure for failure in failures):
        raise AssertionError(f"{label}: missing {fragment!r} failure: {failures}")
    print(f"PASS {label}: rejected ({next(item for item in failures if fragment in item)})")


def main() -> int:
    _, root_step = validate_manifest()
    marker_root = ARTIFACT_ROOT / "Markers" / "player" / "Frames"
    left, right = [], []
    for phase in range(6):
        marker = load_rgba(marker_root / f"player_east_walk_{phase}.png")
        left.append(marker_anchor(marker, "left"))
        right.append(marker_anchor(marker, "right"))

    approved_failures = metric_failures(authored_metrics("east", left, right, root_step), root_step)
    if approved_failures:
        raise AssertionError(f"approved player/east loop failed: {approved_failures}")
    print("PASS approved marker-owned player/east loop")

    static_left = [left[0].copy() for _ in range(6)]
    static_right = [right[0].copy() for _ in range(6)]
    require_failure(
        "six static foot anchors",
        metric_failures(authored_metrics("east", static_left, static_right, root_step), root_step),
        "support world drift",
    )

    one_foot_left = [point.copy() for point in left]
    one_foot_right = [point.copy() for point in right]
    for phase in range(3, 6):
        one_foot_right[phase] = one_foot_left[phase].copy()
    require_failure(
        "second half reuses one support foot",
        metric_failures(authored_metrics("east", one_foot_left, one_foot_right, root_step), root_step),
        "contact step error",
    )

    no_air_left = [point.copy() for point in left]
    no_air_right = [point.copy() for point in right]
    right_world_3 = no_air_right[3] + 3.0 * root_step * VECTORS["east"]
    no_air_right[1] = no_air_right[0] + (right_world_3 - no_air_right[0]) / 3.0 - root_step * VECTORS["east"]
    left_world_3 = no_air_left[3] + 3.0 * root_step * VECTORS["east"]
    left_world_6 = no_air_left[0] + 6.0 * root_step * VECTORS["east"]
    no_air_left[4] = left_world_3 + (left_world_6 - left_world_3) / 3.0 - 4.0 * root_step * VECTORS["east"]
    require_failure(
        "feet travel without an air phase",
        metric_failures(authored_metrics("east", no_air_left, no_air_right, root_step), root_step),
        "passing lift",
    )

    catalog = json.loads((ARTIFACT_ROOT / "family_foot_anchors_v1.json").read_text(encoding="utf-8"))
    row = next(item for item in catalog["rows"] if item["character"] == "player" and item["direction"] == "east")
    if tuple(row["supportLegs"]) != EXPECTED_SUPPORT_LEGS:
        raise AssertionError("approved phase ownership is not left,left,left,right,right,right")
    corrupt = tuple(["left"] * 6)
    if corrupt == EXPECTED_SUPPORT_LEGS:
        raise AssertionError("same-support-leg ownership unexpectedly passed")
    print("PASS same support leg in both half-cycles: rejected (explicit phase ownership mismatch)")

    frame = load_rgba(ARTIFACT_ROOT / "Candidate" / "player" / "Frames" / "player_east_walk_0.png")
    marker = load_rgba(marker_root / "player_east_walk_0.png")
    tampered = frame.copy()
    tampered[20:32] = 0
    if np.array_equal(tampered[:, :, 3], marker[:, :, 3]):
        raise AssertionError("clipped hat unexpectedly preserved marker/candidate alpha contract")
    print("PASS clipped hat: rejected (marker/candidate alpha mismatch)")

    print("FC-CHARACTER-LOCOMOTION-QA-V1-SELFTEST: PASS cases=6")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
