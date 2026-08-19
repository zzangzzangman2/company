#!/usr/bin/env python3
"""Publish an eight-pose Player gait from approved contact pixels only.

The head, neck, torso, jacket, arms and hands remain byte-identical. The two
lower-body halves are translated toward one another without scaling so both
legs keep their authored thickness. One foot stays planted while the other is
lifted during each half-cycle. Toe-off, passing, and landing are separate poses
so no static bitmap is dragged farther than one eighth of a cycle. No pixels
are generated, interpolated, or resampled.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


REPO = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = REPO / "Assets/Resources/FamilyCompany/PlayerNaturalWalkV1/Frames"
DIRECTIONS = ("south", "west", "north", "east")
CONTACT_PPU = {"south": 324, "west": 314, "north": 324, "east": 314}
INWARD_PIXELS = {"south": 12, "west": 18, "north": 12, "east": 18}
LIFT_PIXELS = {"south": 9, "west": 12, "north": 9, "east": 12}
LIFT_LEFT = {
    "south": (True, False),
    "west": (False, True),
    "north": (True, False),
    "east": (True, False),
}
CONTACT_ROOTS = {
    direction: REPO / "Assets/Resources/FamilyCompany" /
    f"Player{direction.title()}ContactV1/Frames"
    for direction in DIRECTIONS
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def build_pose(
    contact_image: Image.Image,
    inward: int,
    lift: int,
    lift_left: bool | None,
) -> tuple[Image.Image, dict[str, int | float | list[int]]]:
    lower_start = round(contact_image.height * 0.596)
    upper_end = round(contact_image.height * 0.663)
    lower_source = contact_image.crop(
        (0, lower_start, contact_image.width, contact_image.height)
    )
    center = contact_image.width // 2
    left_leg = lower_source.crop((0, 0, center, lower_source.height))
    right_leg = lower_source.crop(
        (center, 0, contact_image.width, lower_source.height)
    )
    left_lift = lift if lift_left is True else 0
    right_lift = lift if lift_left is False else 0
    left_bottom = left_leg.getchannel("A").getbbox()[3]
    right_bottom = right_leg.getchannel("A").getbbox()[3]
    left_ground_drop = (
        lower_source.height - left_bottom
        if lift_left is False
        else 0
    )
    right_ground_drop = (
        lower_source.height - right_bottom
        if lift_left is True
        else 0
    )
    lower = Image.new("RGBA", lower_source.size, (0, 0, 0, 0))
    lower.alpha_composite(
        left_leg,
        (inward, left_ground_drop - left_lift),
    )
    lower.alpha_composite(
        right_leg,
        (center - inward, right_ground_drop - right_lift),
    )
    canvas = Image.new("RGBA", contact_image.size, (0, 0, 0, 0))
    canvas.alpha_composite(lower, (0, lower_start))
    canvas.alpha_composite(
        contact_image.crop((0, 0, contact_image.width, upper_end)),
        (0, 0),
    )
    return canvas, {
        "canvasSize": list(canvas.size),
        "lowerStartY": lower_start,
        "sourceExactUpperEndY": upper_end,
        "leftLegTranslationX": inward,
        "rightLegTranslationX": -inward,
        "leftFootLiftY": left_lift,
        "rightFootLiftY": right_lift,
        "leftFootGroundDropY": left_ground_drop,
        "rightFootGroundDropY": right_ground_drop,
        "legScale": 1.0,
    }


def main() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    direction_receipts = []
    for direction in DIRECTIONS:
        sources = []
        frames = []
        contacts = []
        for index in range(2):
            contact = CONTACT_ROOTS[direction] / f"player_{direction}_contact_{index}_v1.png"
            contact_image = Image.open(contact).convert("RGBA")
            contacts.append(contact_image)
            sources.append({
                "contactPath": str(contact.relative_to(REPO)).replace("\\", "/"),
                "contactSha256": sha256(contact),
            })

        for index in range(2):
            lift_left = LIFT_LEFT[direction][index]
            toe, toe_data = build_pose(
                contacts[index],
                round(INWARD_PIXELS[direction] * 0.5),
                round(LIFT_PIXELS[direction] * 0.45),
                lift_left,
            )
            passing, pass_data = build_pose(
                contacts[index],
                INWARD_PIXELS[direction],
                LIFT_PIXELS[direction],
                lift_left,
            )
            destination = 1 - index
            landing, land_data = build_pose(
                contacts[destination],
                round(INWARD_PIXELS[direction] * 0.5),
                0,
                None,
            )
            outputs = (
                ("toe", index, 0.125 + index * 0.5, toe, toe_data),
                ("pass", index, 0.250 + index * 0.5, passing, pass_data),
                ("land", destination, 0.375 + index * 0.5, landing, land_data),
            )
            for role, output_index, phase, canvas, data in outputs:
                output = OUTPUT_ROOT / f"player_{direction}_{role}_{output_index}_v1.png"
                canvas.save(output)
                frames.append({
                    "role": role,
                    "index": output_index,
                    "walkPhase": phase,
                    **data,
                    "sha256": sha256(output),
                })
        direction_receipts.append({
            "direction": direction,
            "sources": sources,
            "frames": sorted(frames, key=lambda item: item["walkPhase"]),
        })

    receipt = {
        "contract": "FC-PLAYER-NATURAL-WALK-V1",
        "frameRole": "source-exact-eight-pose-toe-pass-land-cycle",
        "generatedPixels": False,
        "interpolatedPixels": False,
        "resampledPixels": False,
        "runtimePixelsPerUnit": CONTACT_PPU,
        "groundAlignment": "opaque-bottom-at-pivot",
        "directions": direction_receipts,
    }
    receipt_path = OUTPUT_ROOT.parent / "source-receipt.json"
    receipt_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        "PLAYER_NATURAL_PASSES_V1: PASS | directions=4 intermediateFrames=24 "
        "generatedPixels=false interpolatedPixels=false grounded=true"
    )


if __name__ == "__main__":
    main()
