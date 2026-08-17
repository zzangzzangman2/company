#!/usr/bin/env python3
"""Semantic regression tests for the authored mother north walk cycle."""

from __future__ import annotations

import hashlib
import unittest
from pathlib import Path

from PIL import Image

import build_family_walk_half_cycles_v2 as build


REPO_ROOT = Path(__file__).resolve().parents[1]
HIGH_MOTION_ROOT = (
    REPO_ROOT / "Assets" / "Art" / "Characters" / "Mother" / "Pixel" / "HighMotion"
)
FRAMES_ROOT = HIGH_MOTION_ROOT / "Frames"
SHEET_PATH = HIGH_MOTION_ROOT / "mother_pixel_walk8dir6_b_v1.png"


def load_frames() -> list[Image.Image]:
    return [
        Image.open(FRAMES_ROOT / f"mother_north_walk_{index}.png").convert("RGBA")
        for index in range(6)
    ]


def bottom_y(image: Image.Image, x0: int, x1: int) -> int:
    alpha = image.getchannel("A")
    ys = [
        y
        for y in range(185, 256)
        for x in range(x0, x1)
        if alpha.getpixel((x, y))
    ]
    return max(ys) if ys else -1


class MotherNorthWalkV2Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.frames = load_frames()

    def test_all_six_frames_are_unique_and_grounded(self) -> None:
        hashes = {
            hashlib.sha256(frame.tobytes()).hexdigest()
            for frame in self.frames
        }
        self.assertEqual(6, len(hashes))
        for index, frame in enumerate(self.frames):
            self.assertEqual((256, 256), frame.size)
            self.assertEqual(247, bottom_y(frame, 0, 256), f"frame {index} ground line")

    def test_height_and_body_volume_stay_stable(self) -> None:
        bboxes = [frame.getchannel("A").getbbox() for frame in self.frames]
        self.assertNotIn(None, bboxes)
        self.assertEqual(
            {build.CHARACTER_BY_ID["mother"].target_height},
            {box[3] - box[1] for box in bboxes if box},
        )
        areas = [
            sum(1 for value in frame.getchannel("A").get_flattened_data() if value)
            for frame in self.frames
        ]
        self.assertLessEqual(max(areas) / min(areas), 1.06, areas)

    def test_all_six_transitions_move_the_lower_body(self) -> None:
        adjacent = [
            build.visible_pixel_change(
                self.frames[index],
                self.frames[(index + 1) % 6],
                lower_body_only=True,
            )
            for index in range(6)
        ]
        self.assertGreaterEqual(
            min(adjacent), build.MIN_ADJACENT_LOWER_BODY_CHANGE, adjacent
        )
        self.assertGreaterEqual(
            sum(adjacent) / 6,
            build.MIN_MEAN_ADJACENT_LOWER_BODY_CHANGE,
            adjacent,
        )

    def test_opposite_contacts_change_body_and_feet(self) -> None:
        frame0, frame3 = self.frames[0], self.frames[3]
        self.assertGreaterEqual(
            build.visible_pixel_change(frame0, frame3),
            build.MIN_OPPOSITE_PIXEL_CHANGE,
        )
        self.assertGreaterEqual(
            build.visible_pixel_change(frame0, frame3, lower_body_only=True),
            build.MIN_OPPOSITE_LOWER_BODY_CHANGE,
        )

    def test_sheet_cells_match_runtime_frames(self) -> None:
        with Image.open(SHEET_PATH) as loaded:
            self.assertEqual(
                "grid-4x6-v1",
                loaded.info.get("familyCompanyHighMotionLayout"),
            )
            sheet = loaded.convert("RGBA")
        for phase, frame in enumerate(self.frames):
            cell = sheet.crop((phase * 256, 0, (phase + 1) * 256, 256))
            self.assertEqual(frame.tobytes(), cell.tobytes(), f"north phase {phase}")


if __name__ == "__main__":
    unittest.main()
