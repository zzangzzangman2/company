#!/usr/bin/env python3
"""Build the approved mother north walk half-cycle into HighMotion.

The three ImageGen source poses are a physically ordered half-cycle:
contact -> recoil -> passing.  North is a back view and the costume is
bilaterally symmetric, so the opposite half is an exact horizontal mirror.
This guarantees that feet and contralateral arms cannot drift out of phase.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps
from PIL.PngImagePlugin import PngInfo


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPO_ROOT / "ArtSources" / "MotherNorthWalkV2"
HIGH_MOTION_ROOT = (
    REPO_ROOT / "Assets" / "Art" / "Characters" / "Mother" / "Pixel" / "HighMotion"
)
SHEET_PATH = HIGH_MOTION_ROOT / "mother_pixel_walk8dir6_b_v1.png"
FRAMES_ROOT = HIGH_MOTION_ROOT / "Frames"
TARGET_SIZE = 256
TARGET_HEIGHT = 225
GROUND_Y = 248
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest().upper()


def remove_green_chroma(path: Path) -> Image.Image:
    rgba = Image.open(path).convert("RGBA")
    rgb = np.asarray(rgba)[..., :3].astype(np.int16)
    red, green, blue = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    chroma = (green >= 100) & ((green - red) >= 42) & ((green - blue) >= 36)
    alpha = (~chroma).astype(np.uint8) * 255
    rgba.putalpha(Image.fromarray(alpha, mode="L"))
    bbox = rgba.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError(f"green key removed the entire sprite: {path}")
    return rgba.crop(bbox)


def normalize(sprite: Image.Image) -> Image.Image:
    scale = TARGET_HEIGHT / sprite.height
    target_width = max(1, round(sprite.width * scale))
    sprite = sprite.resize((target_width, TARGET_HEIGHT), Image.Resampling.NEAREST)
    sprite.putalpha(sprite.getchannel("A").point(lambda value: 255 if value >= 128 else 0))
    canvas = Image.new("RGBA", (TARGET_SIZE, TARGET_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(
        sprite,
        ((TARGET_SIZE - target_width) // 2, GROUND_Y - TARGET_HEIGHT),
    )
    return canvas


def build_frames() -> list[Image.Image]:
    source_paths = [
        SOURCE_ROOT / "mother_north_half_0_contact.png",
        SOURCE_ROOT / "mother_north_half_1_recoil.png",
        SOURCE_ROOT / "mother_north_half_2_passing.png",
    ]
    missing = [str(path) for path in source_paths if not path.is_file()]
    if missing:
        raise FileNotFoundError("missing mother north source pose(s): " + ", ".join(missing))
    half_cycle = [normalize(remove_green_chroma(path)) for path in source_paths]
    return half_cycle + [ImageOps.mirror(frame) for frame in half_cycle]


def write_outputs(frames: list[Image.Image]) -> list[Path]:
    written: list[Path] = []
    for index, frame in enumerate(frames):
        if frame.size != (TARGET_SIZE, TARGET_SIZE):
            raise ValueError(f"frame {index} has unexpected size {frame.size}")
        frame_path = FRAMES_ROOT / f"mother_north_walk_{index}.png"
        frame.save(frame_path)
        written.append(frame_path)

    sheet = Image.new(
        "RGBA",
        (TARGET_SIZE * 6, TARGET_SIZE * 4),
        (0, 0, 0, 0),
    )
    directions = ("north", "northeast", "east", "southeast")
    for row, direction in enumerate(directions):
        row_frames = frames if row == 0 else [
            Image.open(FRAMES_ROOT / f"mother_{direction}_walk_{phase}.png").convert("RGBA")
            for phase in range(6)
        ]
        for phase, frame in enumerate(row_frames):
            if frame.size != (TARGET_SIZE, TARGET_SIZE):
                raise ValueError(
                    f"mother {direction} frame {phase} has unexpected size {frame.size}"
                )
            sheet.paste(frame, (phase * TARGET_SIZE, row * TARGET_SIZE))
    metadata = PngInfo()
    metadata.add_text(LAYOUT_METADATA_KEY, GRID_LAYOUT_MARKER)
    sheet.save(SHEET_PATH, pnginfo=metadata)
    written.append(SHEET_PATH)
    return written


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--check",
        action="store_true",
        help="rebuild in memory and verify tracked outputs without rewriting them",
    )
    args = parser.parse_args()

    frames = build_frames()
    if args.check:
        expected_sheet = Image.open(SHEET_PATH).convert("RGBA")
        for index, frame in enumerate(frames):
            tracked_frame = Image.open(FRAMES_ROOT / f"mother_north_walk_{index}.png").convert("RGBA")
            if tracked_frame.tobytes() != frame.tobytes():
                raise SystemExit(f"mother north frame {index} is stale")
            tracked_cell = expected_sheet.crop((index * TARGET_SIZE, 0, (index + 1) * TARGET_SIZE, TARGET_SIZE))
            if tracked_cell.tobytes() != frame.tobytes():
                raise SystemExit(f"mother B sheet north cell {index} is stale")
        print("PASS mother north walk v2 outputs match deterministic sources")
        return 0

    for path in write_outputs(frames):
        print(f"WROTE {path.relative_to(REPO_ROOT)} sha256={sha256(path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
