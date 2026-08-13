#!/usr/bin/env python3
"""Prepare generated wall art as tile-exact, hard-alpha Unity source PNGs."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


CANVAS = (1536, 1536)
ANCHOR_X = 250
ANCHOR_Y = 160
SPAN_X = 960
SPAN_Y = 480
TILE_SPAN_COMPRESSION = 0.5
# Generated perspective measured 2.43:1 between post bases.  Stretch only the vertical screen
# axis so their runtime displacement becomes the project's exact 2:1 (320px, 160px) grid basis.
VERTICAL_STRETCH = 1.214


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def key_green(image: Image.Image) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    # Image generation slightly varies the pure-green field; distance-to-green is intentionally
    # generous, while brown/orange wall pixels remain far outside this chroma wedge.
    keyed = (rgb[:, :, 1] > 150) & (rgb[:, :, 1] > rgb[:, :, 0] * 1.45) & (rgb[:, :, 1] > rgb[:, :, 2] * 1.45)
    alpha = np.where(keyed, 0, 255).astype(np.uint8)
    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def alpha_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    rows, columns = np.nonzero(alpha)
    if not len(rows):
        raise ValueError("wall source became empty after chroma removal")
    return int(columns.min()), int(rows.min()), int(columns.max()) + 1, int(rows.max()) + 1


def compress_to_single_tile(image: Image.Image) -> Image.Image:
    """Halve the generated two-tile baseline while preserving upright post height."""
    pivot_x = 66.0
    pivot_y = float(image.height)
    factor = TILE_SPAN_COMPRESSION
    # The normalized generated baseline rises one screen pixel for every two X pixels.  This
    # affine transform compresses only that isometric basis; a vertical vector (0,y) is unchanged.
    shear = (1.0 - factor) * 0.5
    inverse = (
        1.0 / factor,
        0.0,
        pivot_x * (1.0 - 1.0 / factor),
        -shear / factor,
        1.0,
        shear * pivot_x / factor,
    )
    output_width = max(1, round(pivot_x + factor * (image.width - pivot_x)))
    return image.transform(
        (output_width, image.height),
        Image.Transform.AFFINE,
        inverse,
        resample=Image.Resampling.NEAREST,
        fillcolor=(0, 0, 0, 0),
    )


def main() -> int:
    args = parse_args()
    keyed = key_green(Image.open(args.input))
    bounds = alpha_bounds(keyed)
    crop = keyed.crop(bounds)

    # The source module's left and right post bases are the wall endpoints.  Normalize every
    # variant to the exact same 960x480 isometric span so translating a prop by one grid cell
    # makes the next endpoint land exactly on the previous endpoint.
    target_width = SPAN_X + 1
    scale = target_width / crop.width
    target_height = max(1, round(crop.height * scale * VERTICAL_STRETCH))
    crop = crop.resize((target_width, target_height), Image.Resampling.NEAREST)
    crop = compress_to_single_tile(crop)
    if target_height > CANVAS[1] - ANCHOR_Y:
        raise ValueError(f"scaled wall height {target_height} exceeds the source canvas")

    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    # PIL top origin: place the left post base at (ANCHOR_X, CANVAS.h-ANCHOR_Y) and the right
    # post base one isometric cell up.  Generated crop includes both bases at its bottom contour;
    # anchoring the crop bottom preserves a deterministic floor contact used by the Unity pivot.
    destination_x = ANCHOR_X
    destination_y = CANVAS[1] - ANCHOR_Y - target_height
    canvas.alpha_composite(crop, (destination_x, destination_y))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output, optimize=True)
    print(
        f"prepared {args.output} | source={crop.width}x{crop.height} "
        f"anchor=({ANCHOR_X},{ANCHOR_Y}) tile-span=({SPAN_X * TILE_SPAN_COMPRESSION:.0f},"
        f"{SPAN_Y * TILE_SPAN_COMPRESSION:.0f})"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
