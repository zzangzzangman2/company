#!/usr/bin/env python3
"""Prepare generated wall art as tile-exact, hard-alpha Unity source PNGs."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


CANVAS = (1536, 1536)
ANCHOR_X = 250
ANCHOR_Y = 172
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
    parser.add_argument(
        "--height-scale",
        type=float,
        default=1.0,
        help="Compress wall height toward its isometric floor baseline (0 < value <= 1).",
    )
    parser.add_argument(
        "--left-endpoint",
        type=parse_point,
        help="Optional source-image floor endpoint as X,Y for a wall already spanning one tile.",
    )
    parser.add_argument(
        "--right-endpoint",
        type=parse_point,
        help="Optional source-image floor endpoint as X,Y for a wall already spanning one tile.",
    )
    parser.add_argument(
        "--edge-pad",
        type=int,
        default=0,
        help="Extend each scanline's two terminal pixels outward without moving floor endpoints.",
    )
    parser.add_argument(
        "--exterior-threshold-only",
        action="store_true",
        help=(
            "Discard every opaque pixel on the interior/vertical side of the canonical inner "
            "edge, leaving a thin exterior-side threshold with no door frame or leaf."
        ),
    )
    parser.add_argument(
        "--far-wall-face-only",
        action="store_true",
        help=(
            "For the two far edges, discard the screen-lower base pixels that would project "
            "inside the floor, retaining only the edge connection and exterior-rising wall face."
        ),
    )
    return parser.parse_args()


def parse_point(value: str) -> tuple[float, float]:
    try:
        x, y = value.split(",", 1)
        return float(x), float(y)
    except (TypeError, ValueError) as exception:
        raise argparse.ArgumentTypeError("point must be X,Y") from exception


def key_green(image: Image.Image) -> Image.Image:
    source = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    rgb = source[:, :, :3]
    # Image generation slightly varies the pure-green field; distance-to-green is intentionally
    # generous, while brown/orange wall pixels remain far outside this chroma wedge.
    keyed = (
        (rgb[:, :, 1] > 150)
        & (rgb[:, :, 1] > rgb[:, :, 0] * 1.45)
        & (rgb[:, :, 1] > rgb[:, :, 2] * 1.45)
    )
    # Built-in ImageGen can return either a chroma field or actual alpha. Preserve genuine
    # transparency and harden generated edge alpha while still accepting the older green-key
    # workflow used by the first perimeter-wall pass.
    alpha = np.where(keyed | (source[:, :, 3] < 128), 0, 255).astype(np.uint8)
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


def compress_height_to_baseline(image: Image.Image, factor: float) -> Image.Image:
    """Shorten the upright wall without changing its exact one-tile floor endpoints."""
    if not 0.0 < factor <= 1.0:
        raise ValueError("height-scale must be within (0, 1]")
    if factor == 1.0:
        return image

    # In normalized source coordinates the left endpoint is (316,172) from the bottom and the
    # right endpoint is one 2:1 source tile away at (+480,+240). PIL uses a top-left origin, so
    # the floor baseline is y = (CANVAS.h - 1 - 172) - 0.5 * (x - 316). Keep every baseline point
    # fixed and compress only the perpendicular screen-height component above it.
    baseline_intercept = CANVAS[1] - 1 - ANCHOR_Y + 0.5 * (ANCHOR_X + 66)
    inverse = (
        1.0,
        0.0,
        0.0,
        0.5 * (1.0 / factor - 1.0),
        1.0 / factor,
        baseline_intercept * (1.0 - 1.0 / factor),
    )
    return image.transform(
        image.size,
        Image.Transform.AFFINE,
        inverse,
        resample=Image.Resampling.NEAREST,
        fillcolor=(0, 0, 0, 0),
    )


def normalize_authored_single_tile(
    keyed: Image.Image,
    bounds: tuple[int, int, int, int],
    left_endpoint: tuple[float, float],
    right_endpoint: tuple[float, float],
) -> Image.Image:
    """Uniformly map a generated one-tile wall to the canonical (+480,-240) source basis."""
    left_x, left_y = left_endpoint
    right_x, right_y = right_endpoint
    delta_x = right_x - left_x
    delta_y = right_y - left_y
    if delta_x <= 0.0 or abs(delta_y / delta_x + 0.5) > 0.002:
        raise ValueError(
            f"source endpoints must follow the 2:1 rising basis; delta=({delta_x},{delta_y})"
        )

    crop = keyed.crop(bounds)
    scale = (SPAN_X * TILE_SPAN_COMPRESSION) / delta_x
    target_size = (
        max(1, round(crop.width * scale)),
        max(1, round(crop.height * scale)),
    )
    crop = crop.resize(target_size, Image.Resampling.NEAREST)
    canonical_left_x = ANCHOR_X + 66
    canonical_left_y = CANVAS[1] - 1 - ANCHOR_Y
    destination_x = round(canonical_left_x - (left_x - bounds[0]) * scale)
    destination_y = round(canonical_left_y - (left_y - bounds[1]) * scale)
    if (
        destination_x < 0
        or destination_y < 0
        or destination_x + crop.width > CANVAS[0]
        or destination_y + crop.height > CANVAS[1]
    ):
        raise ValueError(
            f"normalized one-tile wall exceeds source canvas: destination=({destination_x},"
            f"{destination_y}) size={crop.size}"
        )
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(crop, (destination_x, destination_y))
    return canvas


def extend_terminal_edges(image: Image.Image, padding: int) -> Image.Image:
    if padding < 0:
        raise ValueError("edge-pad must be non-negative")
    if padding == 0:
        return image
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[:, :, 3]
    output = pixels.copy()
    for y in range(image.height):
        visible = np.flatnonzero(alpha[y])
        if not len(visible):
            continue
        left = int(visible[0])
        right = int(visible[-1])
        output[y, max(0, left - padding):left] = pixels[y, left]
        output[y, right + 1:min(image.width, right + padding + 1)] = pixels[y, right]
    return Image.fromarray(output, "RGBA")


def retain_exterior_threshold(image: Image.Image) -> Image.Image:
    """Keep only the base on/outside the floor edge; remove door leaf, jambs and lintel."""
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[:, :, 3]
    rows, columns = np.indices(alpha.shape)
    left_x = ANCHOR_X + 66
    inner_edge_y = (CANVAS[1] - 1 - ANCHOR_Y) - 0.5 * (columns - left_x)
    interior_or_vertical = rows < inner_edge_y - 0.5
    pixels[interior_or_vertical] = 0
    pixels[alpha == 0] = 0
    return Image.fromarray(pixels, "RGBA")


def retain_far_wall_face(image: Image.Image) -> Image.Image:
    """Remove the screen-lower sill used by near edges; far-edge wall faces rise outward."""
    pixels = np.asarray(image, dtype=np.uint8).copy()
    alpha = pixels[:, :, 3]
    rows, columns = np.indices(alpha.shape)
    left_x = ANCHOR_X + 66
    inner_edge_y = (CANVAS[1] - 1 - ANCHOR_Y) - 0.5 * (columns - left_x)
    screen_lower_base = rows > inner_edge_y + 0.5
    pixels[screen_lower_base] = 0
    pixels[alpha == 0] = 0
    return Image.fromarray(pixels, "RGBA")


def validate_canonical_endpoints(image: Image.Image) -> None:
    """Require real art at both ends of the canonical one-tile (+480,+240) basis."""
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    for x, bottom_y in ((ANCHOR_X + 66, ANCHOR_Y), (ANCHOR_X + 66 + 480, ANCHOR_Y + 240)):
        center_y = image.height - 1 - bottom_y
        found = False
        for offset_y in range(-2, 3):
            for offset_x in range(-2, 3):
                if offset_x * offset_x + offset_y * offset_y > 4:
                    continue
                sample_x = x + offset_x
                sample_y = center_y + offset_y
                if 0 <= sample_x < image.width and 0 <= sample_y < image.height:
                    found = found or alpha[sample_y, sample_x] > 16
        if not found:
            raise ValueError(f"no visible art within 2px of canonical endpoint ({x},{bottom_y})")


def main() -> int:
    args = parse_args()
    if args.exterior_threshold_only and args.far_wall_face_only:
        raise ValueError("threshold-only and far-wall-face-only are mutually exclusive")
    if (args.left_endpoint is None) != (args.right_endpoint is None):
        raise ValueError("left-endpoint and right-endpoint must be provided together")
    keyed = key_green(Image.open(args.input))
    bounds = alpha_bounds(keyed)
    if args.left_endpoint is not None:
        canvas = normalize_authored_single_tile(
            keyed,
            bounds,
            args.left_endpoint,
            args.right_endpoint,
        )
        source_size = "endpoint-normalized"
    else:
        crop = keyed.crop(bounds)
        # Legacy generated wall candidates spanned two bays. Normalize them to the same exact
        # source basis, then halve only their isometric floor axis.
        target_width = SPAN_X + 1
        scale = target_width / crop.width
        target_height = max(1, round(crop.height * scale * VERTICAL_STRETCH))
        crop = crop.resize((target_width, target_height), Image.Resampling.NEAREST)
        crop = compress_to_single_tile(crop)
        if target_height > CANVAS[1] - ANCHOR_Y:
            raise ValueError(f"scaled wall height {target_height} exceeds the source canvas")
        canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        destination_x = ANCHOR_X
        destination_y = CANVAS[1] - ANCHOR_Y - target_height
        canvas.alpha_composite(crop, (destination_x, destination_y))
        source_size = f"{crop.width}x{crop.height}"
    canvas = compress_height_to_baseline(canvas, args.height_scale)
    canvas = extend_terminal_edges(canvas, args.edge_pad)
    if args.exterior_threshold_only:
        canvas = retain_exterior_threshold(canvas)
    if args.far_wall_face_only:
        canvas = retain_far_wall_face(canvas)
    validate_canonical_endpoints(canvas)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output, optimize=True)
    print(
        f"prepared {args.output} | source={source_size} "
        f"anchor=({ANCHOR_X},{ANCHOR_Y}) tile-span=({SPAN_X * TILE_SPAN_COMPRESSION:.0f},"
        f"{SPAN_Y * TILE_SPAN_COMPRESSION:.0f}) height-scale={args.height_scale:.3f}"
        f" edge-pad={args.edge_pad} exterior-threshold-only={args.exterior_threshold_only}"
        f" far-wall-face-only={args.far_wall_face_only}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
