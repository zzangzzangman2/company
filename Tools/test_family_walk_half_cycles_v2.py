#!/usr/bin/env python3
"""Regression tests for the identity-locked 4 x 8 x 6 family walk set."""

from __future__ import annotations

import hashlib
import unittest

from PIL import ImageOps

import build_family_walk_half_cycles_v2 as build


class FamilyWalkHalfCyclesV2Tests(unittest.TestCase):
    def test_all_rows_have_six_unique_grounded_hard_alpha_frames(self) -> None:
        for character in build.CHARACTERS:
            for direction in build.DIRECTIONS:
                frames = build.derived_frames(character, direction)
                with self.subTest(member=character.member_id, direction=direction):
                    self.assertEqual(
                        6,
                        len({hashlib.sha256(frame.tobytes()).digest() for frame in frames}),
                    )
                    for frame in frames:
                        self.assertEqual((256, 256), frame.size)
                        self.assertEqual("RGBA", frame.mode)
                        self.assertEqual({0, 255}, set(frame.getchannel("A").get_flattened_data()))
                        bbox = frame.getchannel("A").getbbox()
                        self.assertIsNotNone(bbox)
                        assert bbox is not None
                        self.assertEqual(build.GROUND_Y, bbox[3] - 1)
                        self.assertEqual(character.target_height, bbox[3] - bbox[1])

    def test_generated_opposite_direction_pairs_are_exact_mirrors(self) -> None:
        for character in build.CHARACTERS:
            for source_direction, mirrored_direction in build.MIRROR_PAIRS:
                for phase in range(6):
                    with self.subTest(
                        member=character.member_id,
                        direction=mirrored_direction,
                        phase=phase,
                    ):
                        expected = ImageOps.mirror(
                            build.load_source(character, source_direction, phase)
                        )
                        actual = build.load_source(character, mirrored_direction, phase)
                        self.assertEqual(expected.tobytes(), actual.tobytes())

    def test_marker_review_is_separate_and_silhouette_identical(self) -> None:
        for character in build.CHARACTERS:
            for direction in build.DIRECTIONS:
                for phase in range(6):
                    with self.subTest(
                        member=character.member_id,
                        direction=direction,
                        phase=phase,
                    ):
                        shipping = build.load_source(character, direction, phase)
                        marker = build.load_marker(character, direction, phase)
                        self.assertEqual(
                            shipping.getchannel("A").tobytes(),
                            marker.getchannel("A").tobytes(),
                        )
                        cyan, magenta = build.marker_masks(marker)
                        self.assertGreaterEqual(int(cyan.sum()), 30)
                        self.assertGreaterEqual(int(magenta.sum()), 30)

    def test_all_rows_pass_identity_lock_source_contract(self) -> None:
        self.assertEqual([], build.validate_sources())

    def test_runtime_frames_and_sheets_match_sources(self) -> None:
        build.check_outputs()


if __name__ == "__main__":
    unittest.main()
