#!/usr/bin/env python3
"""Regression self-tests for the fail-closed Character Locomotion V1 gate."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np

from generate_character_locomotion_v1 import CHARACTERS, DEFAULT_OUTPUT
from verify_character_locomotion_v1 import load_frames, measure_loop


PROFILE_PATH = Path(__file__).with_name("character_locomotion_profiles_v1.json")


def assert_failed(label: str, result, required_fragment: str) -> None:
    if result.verdict != "FAIL":
        raise AssertionError(f"{label}: invalid loop unexpectedly passed")
    if not any(required_fragment in failure for failure in result.failures):
        raise AssertionError(f"{label}: missing {required_fragment!r} failure: {result.failures}")
    print(f"PASS {label}: rejected ({'; '.join(result.failures[:2])})")


def main() -> int:
    profiles = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))["characters"]
    character = CHARACTERS[0]
    direction = "south"
    profile = profiles[character.character_id]
    candidate_root = DEFAULT_OUTPUT / "Candidate"
    frames = load_frames(candidate_root, character, direction)

    approved = measure_loop(character, direction, frames, profile)
    if approved.verdict != "PASS":
        raise AssertionError(f"approved candidate failed: {approved.failures}")
    print("PASS approved candidate loop")

    static = [frames[0].copy() for _ in range(6)]
    assert_failed(
        "six byte-identical/static frames",
        measure_loop(character, direction, static, profile),
        "foot excursion",
    )

    # Deliberately animate only a thin trouser strip while keeping every foot pixel frozen.
    # This models the old false-positive failure mode: six unique images with no walking.
    pants_only = [frames[0].copy() for _ in range(6)]
    for phase, frame in enumerate(pants_only):
        dx = (-1, 0, 1, 1, 0, -1)[phase]
        source = frames[0][196:207].copy()
        frame[196:207] = 0
        if dx >= 0:
            frame[196:207, dx:] = source[:, : 256 - dx]
        else:
            frame[196:207, :dx] = source[:, -dx:]
    assert_failed(
        "pants-only wobble with stationary feet",
        measure_loop(character, direction, pants_only, profile),
        "foot excursion",
    )

    zero_air = [frame.copy() for frame in frames]
    zero_air[2] = zero_air[0].copy()
    zero_air[5] = zero_air[3].copy()
    assert_failed(
        "contact poses repeated as passing phases",
        measure_loop(character, direction, zero_air, profile),
        "vertical swing lift",
    )

    # Preserve the approved feet but freeze every protected upper-body row.  This is the exact
    # legacy false positive described by the external animation review: real leg differences under
    # a byte-identical torso must no longer qualify as the authored V1 gait.
    frozen_upper = [frame.copy() for frame in frames]
    alpha = frames[0][:, :, 3] > 0
    rows = np.nonzero(alpha)[0]
    top, bottom = int(rows.min()), int(rows.max())
    upper_end = int(round(top + (bottom - top + 1) * float(profile["lowerBodyStart"]))) - 2
    for frame in frozen_upper[1:]:
        frame[:upper_end] = frozen_upper[0][:upper_end]
    assert_failed(
        "valid feet under byte-identical frozen upper body",
        measure_loop(character, direction, frozen_upper, profile),
        "upper/body authored change",
    )

    detached_waist = [frame.copy() for frame in frames]
    for frame in detached_waist:
        frame[181:185] = 0
    assert_failed(
        "transparent torso/lower-body cut with otherwise valid feet",
        measure_loop(character, direction, detached_waist, profile),
        "waist reference mismatch",
    )

    clipped_hat = [frame.copy() for frame in frames]
    visible_rows = np.nonzero(frames[0][:, :, 3] > 0)[0]
    clip_end = int(visible_rows.min()) + 12
    for frame in clipped_hat:
        frame[:clip_end] = 0
    assert_failed(
        "uniformly clipped hat/head silhouette",
        measure_loop(character, direction, clipped_hat, profile),
        "head identity mismatch",
    )

    print("FC-CHARACTER-LOCOMOTION-QA-V1-SELFTEST: PASS cases=7")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
