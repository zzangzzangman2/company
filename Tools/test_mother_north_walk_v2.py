#!/usr/bin/env python3
"""Semantic regression tests for the authored mother north walk cycle."""

from __future__ import annotations

import hashlib
import unittest
from pathlib import Path

from PIL import Image, ImageChops, ImageOps


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


def silhouette_change(
    left: Image.Image,
    right: Image.Image,
    box: tuple[int, int, int, int],
) -> float:
    left_alpha = left.getchannel("A").crop(box).point(lambda value: 255 if value else 0)
    right_alpha = right.getchannel("A").crop(box).point(lambda value: 255 if value else 0)
    union = ImageChops.lighter(left_alpha, right_alpha)
    difference = ImageChops.logical_xor(left_alpha.convert("1"), right_alpha.convert("1"))
    union_pixels = sum(1 for value in union.get_flattened_data() if value)
    difference_pixels = sum(1 for value in difference.get_flattened_data() if value)
    return difference_pixels / union_pixels


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

    def test_second_half_is_exact_opposite_gait(self) -> None:
        for index in range(3):
            self.assertEqual(
                ImageOps.mirror(self.frames[index]).tobytes(),
                self.frames[index + 3].tobytes(),
                f"frame {index + 3} must mirror frame {index}",
            )

    def test_support_foot_changes_only_at_passing_phases(self) -> None:
        support_sides: list[str] = []
        for frame in self.frames:
            left_y = bottom_y(frame, 0, 128)
            right_y = bottom_y(frame, 128, 256)
            support_sides.append("L" if left_y > right_y else "R" if right_y > left_y else "=")
        self.assertEqual(["R", "R", "L", "L", "L", "R"], support_sides)

    def test_opposite_contacts_move_arms_skirt_and_feet(self) -> None:
        frame0, frame3 = self.frames[0], self.frames[3]
        bboxes = [frame.getchannel("A").getbbox() for frame in (frame0, frame3)]
        self.assertNotIn(None, bboxes)
        left = min(box[0] for box in bboxes if box)
        top = min(box[1] for box in bboxes if box)
        right = max(box[2] for box in bboxes if box)
        bottom = max(box[3] for box in bboxes if box)
        height = bottom - top
        regions = {
            "upper": (left, top, right, top + round(height * 0.58)),
            "skirt": (left, top + round(height * 0.40), right, top + round(height * 0.82)),
            "feet": (left, top + round(height * 0.76), right, bottom),
        }
        changes = {
            name: silhouette_change(frame0, frame3, box)
            for name, box in regions.items()
        }
        self.assertGreaterEqual(changes["upper"], 0.20, changes)
        self.assertGreaterEqual(changes["skirt"], 0.20, changes)
        self.assertGreaterEqual(changes["feet"], 0.50, changes)

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
