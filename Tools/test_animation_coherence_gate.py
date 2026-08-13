#!/usr/bin/env python3
"""Regression tests for the strict locomotion asset gate."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from PIL import Image

import measure_animation_coherence as coherence


class AnimationCoherenceGateTests(unittest.TestCase):
    def write_frame(
        self, root: Path, name: str, *, phase: int = 0, alpha: int = 255
    ) -> Path:
        path = root / name
        image = Image.new("RGBA", coherence.CANVAS_SIZE, (0, 0, 0, 0))
        for y in range(80, 249):
            for x in range(96 + phase, 160 + phase):
                image.putpixel((x, y), (80 + phase, 120, 160, alpha))
        image.save(path)
        return path

    def test_identical_walk_frames_fail_ratio_and_uniqueness(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = [self.write_frame(root, f"actor_south_walk_{index}.png") for index in range(6)]
            loop = coherence.Loop(
                "actor", "walk", "south", list(enumerate(paths)), enforce_walk_quality=True
            )
            coherence.measure(loop)
            self.assertIn("duplicate-frame", loop.failure_codes)
            self.assertIn("ratio-not-finite", loop.failure_codes)

    def test_six_unique_walk_frames_do_not_trigger_structure_failures(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = [
                self.write_frame(root, f"actor_south_walk_{index}.png", phase=index)
                for index in range(6)
            ]
            loop = coherence.Loop(
                "actor", "walk", "south", list(enumerate(paths)), enforce_walk_quality=True
            )
            coherence.measure(loop)
            self.assertNotIn("duplicate-frame", loop.failure_codes)
            self.assertNotIn("walk-indices", loop.failure_codes)

    def test_missing_walk_direction_fails_contract(self) -> None:
        loops = [
            coherence.Loop("actor", "walk", direction, [])
            for direction in coherence.DIRECTIONS[:-1]
        ]
        contract = coherence.build_walk_contract(
            loops, {"invalidWalkFrameNames": [], "singleFrameBuckets": []}, True
        )
        self.assertFalse(contract["pass"])
        self.assertIn("actor", contract["missingDirections"])

    def test_micro_action_remains_exempt_from_locomotion_ratio(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = [self.write_frame(root, f"actor_typing_{index}_north_v1.png") for index in range(2)]
            loop = coherence.Loop("actor", "typing", "north", list(enumerate(paths)))
            coherence.measure(loop)
            self.assertNotIn("ratio-not-finite", loop.failure_codes)


if __name__ == "__main__":
    unittest.main()
