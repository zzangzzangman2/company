#!/usr/bin/env python3
"""Publish the two exact Player-north contact poses from the canonical 4x2 sheet.

No pixels are generated or interpolated. Approved direction outputs are not
read or modified; north is extracted independently from column two.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


REPO = Path(__file__).resolve().parents[1]
SOURCE = REPO / "Assets/Art/Characters/Player/Pixel/player_pixel_walk4x2_v1.png"
OUTPUT = REPO / "Assets/Resources/FamilyCompany/PlayerNorthContactV1/Frames"
GRID_COLUMNS = 4
GRID_ROWS = 2
NORTH_COLUMN = 2
HORIZONTAL_PADDING = 4
TOP_PADDING = 4
RUNTIME_PIXELS_PER_UNIT = 324


def main() -> None:
    sheet = Image.open(SOURCE).convert("RGBA")
    if sheet.size != (1536, 1024):
        raise ValueError(f"unexpected canonical sheet size: {sheet.size}")
    cell_width = sheet.width // GRID_COLUMNS
    cell_height = sheet.height // GRID_ROWS
    trimmed: list[Image.Image] = []
    source_boxes: list[list[int]] = []
    for row in range(GRID_ROWS):
        cell_box = (
            NORTH_COLUMN * cell_width,
            row * cell_height,
            (NORTH_COLUMN + 1) * cell_width,
            (row + 1) * cell_height,
        )
        cell = sheet.crop(cell_box)
        alpha_box = cell.getchannel("A").getbbox()
        if alpha_box is None:
            raise ValueError(f"north contact row {row} has no opaque pixels")
        image = cell.crop(alpha_box)
        trimmed.append(image)
        source_boxes.append([
            cell_box[0] + alpha_box[0],
            cell_box[1] + alpha_box[1],
            alpha_box[2] - alpha_box[0],
            alpha_box[3] - alpha_box[1],
        ])

    canvas_width = max(image.width for image in trimmed) + HORIZONTAL_PADDING * 2
    canvas_height = max(image.height for image in trimmed) + TOP_PADDING
    OUTPUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for index, image in enumerate(trimmed):
        canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
        x = (canvas_width - image.width) // 2
        y = canvas_height - image.height
        canvas.alpha_composite(image, (x, y))
        path = OUTPUT / f"player_north_contact_{index}_v1.png"
        canvas.save(path)
        frames.append({
            "phase": index * 0.5,
            "sourceBox": source_boxes[index],
            "canvasSize": [canvas_width, canvas_height],
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
        })

    receipt = {
        "contract": "FC-PLAYER-NORTH-CONTACT-V1",
        "source": str(SOURCE.relative_to(REPO)).replace("\\", "/"),
        "sourceSha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest().upper(),
        "direction": "north",
        "frameCount": 2,
        "generatedPixels": False,
        "interpolatedPixels": False,
        "runtimePixelsPerUnit": RUNTIME_PIXELS_PER_UNIT,
        "groundAlignment": "opaque-bottom-at-pivot",
        "frames": frames,
    }
    (OUTPUT.parent / "source-receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(
        "PLAYER_NORTH_CONTACT_V1: PASS | frames=2 generatedPixels=false "
        f"canvas={canvas_width}x{canvas_height}"
    )


if __name__ == "__main__":
    main()
