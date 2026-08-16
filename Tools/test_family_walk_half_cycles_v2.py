#!/usr/bin/env python3
"""Regression tests for all 32 direction-preserving family walk rows."""

from __future__ import annotations

import hashlib
import unittest

from PIL import Image, ImageOps

import build_family_walk_half_cycles_v2 as build


def bottom_y(image: Image.Image) -> int:
    alpha = image.getchannel("A")
    ys = [
        y
        for y in range(image.height)
        for x in range(image.width)
        if alpha.getpixel((x, y))
    ]
    return max(ys) if ys else -1


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
                        self.assertEqual(
                            {0, 255},
                            set(frame.getchannel("A").get_flattened_data()),
                        )
                        self.assertEqual(247, bottom_y(frame))

    def test_second_half_is_exact_direction_preserving_mirror(self) -> None:
        for character in build.CHARACTERS:
            for direction in build.DIRECTIONS:
                frames = build.derived_frames(character, direction)
                mirror_direction = build.MIRROR_SOURCE[direction]
                for phase in range(3):
                    expected = ImageOps.mirror(
                        build.load_source(character, mirror_direction, phase)
                    )
                    with self.subTest(
                        member=character.member_id,
                        direction=direction,
                        phase=phase + 3,
                    ):
                        self.assertEqual(expected.tobytes(), frames[phase + 3].tobytes())

    def test_opposite_contacts_change_at_least_30_percent(self) -> None:
        failures: dict[str, float] = {}
        for character in build.CHARACTERS:
            for direction in build.DIRECTIONS:
                frames = build.derived_frames(character, direction)
                change = build.silhouette_change(frames[0], frames[3])
                if change < build.MIN_OPPOSITE_CHANGE:
                    failures[f"{character.member_id}/{direction}"] = round(change, 4)
        self.assertEqual({}, failures)

    def test_runtime_frames_and_sheets_match_sources(self) -> None:
        build.check_outputs()


if __name__ == "__main__":
    unittest.main()
