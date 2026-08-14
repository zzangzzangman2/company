"""Deterministically author the canonical seated-chair base and foreground.

The source chair is a flattened RGBA Sprite, so the occupied foreground must be
derived from semantic parts instead of a canvas rectangle.  This tool performs
two deliberately bounded operations:

1. Widen only the upholstered backrest inside the chair's existing visual bbox.
   The wheel, stem, ground contact, canvas, pivot, and one-cell footprint do not
   move or scale.
2. Build the occupied foreground from the complete curved seat-front rim.  The
   backrest and both armrests stay base-only behind the actor; every wheel/stem
   pixel also remains base-only.

The checked-in PNGs are the runtime assets.  This script is their reproducible
authoring recipe; it never creates or replaces Unity .meta files.
"""

from __future__ import annotations

import hashlib
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "Assets" / "Art" / "Office" / "Tiles" / "Furniture" / "Runtime"
SOURCE_PATH = (
    ROOT / "Assets" / "Art" / "Office" / "Tiles" / "Furniture" / "Source"
    / "office_swivel_chair_northwest_alpha_v3.png"
)
BASE_PATH = RUNTIME / "office_swivel_chair_v3.png"
FRONT_PATH = RUNTIME / "office_swivel_chair_front_v3.png"

CANVAS = (640, 512)
SOURCE_FILE_SHA256 = "869B32F4A522099A2B52A4F0A9391C667565BB5E0E4D5F228C535FAA4C96FCC3"
AUTHORED_BASE_FILE_SHA256 = "ABD07ED0AF918A35107D139B5164A0BAF8BB5069BF512DF9588D10DF85D176CB"
RUNTIME_MAXIMUM_WIDTH = 175
RUNTIME_MAXIMUM_HEIGHT = 260
RUNTIME_VISIBLE_MARGIN_PX = 24

BACKREST_CONTOUR = (
    (311, 306), (319, 303), (354, 282), (380, 269), (398, 273),
    (408, 283), (409, 347), (405, 355), (405, 375), (398, 385),
    (369, 395), (341, 407), (319, 400), (312, 388),
)
AUTHORED_BACKREST_WIDTH_PX = 175
AUTHORED_BACKREST_OFFSET_X_PX = -12

SEAT_RIM_TOP_CURVE = (
    (238, 349), (246, 351), (254, 355), (262, 360), (270, 365),
    (278, 369), (286, 372), (294, 375), (302, 378), (310, 381),
    (318, 384), (326, 386), (334, 388), (342, 390), (350, 391),
    (358, 392), (368, 391), (374, 394),
)


def file_sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def polygon_mask(size: tuple[int, int], points: tuple[tuple[int, int], ...]) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def build_runtime_base_from_source() -> Image.Image:
    """Mirror OfficeFurnitureAssetBuilder.BuildOne for the V3 chair exactly."""
    source = Image.open(SOURCE_PATH).convert("RGBA")
    source_pixels = source.load()
    alpha = source.getchannel("A").point(lambda value: 255 if value >= 16 else 0)
    bounds = alpha.getbbox()
    if bounds is None:
        raise RuntimeError("Canonical chair source has no visible pixels.")
    minimum_x, minimum_top_y, maximum_x, maximum_bottom_y = bounds
    source_width = maximum_x - minimum_x
    source_height = maximum_bottom_y - minimum_top_y
    scale = min(
        RUNTIME_MAXIMUM_WIDTH / source_width,
        RUNTIME_MAXIMUM_HEIGHT / source_height,
    )
    runtime_width = max(1, round(source_width * scale))
    runtime_height = max(1, round(source_height * scale))
    destination_x = (CANVAS[0] - runtime_width) // 2
    source_bottom_y = source.height - maximum_bottom_y

    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    result_pixels = result.load()
    for runtime_y in range(runtime_height):
        source_y_up = source_bottom_y + min(
            source_height - 1,
            math.floor(runtime_y / scale),
        )
        source_top_y = source.height - 1 - source_y_up
        destination_top_y = CANVAS[1] - 1 - (RUNTIME_VISIBLE_MARGIN_PX + runtime_y)
        for runtime_x in range(runtime_width):
            source_x = minimum_x + min(
                source_width - 1,
                math.floor(runtime_x / scale),
            )
            red, green, blue, source_alpha = source_pixels[source_x, source_top_y]
            result_pixels[destination_x + runtime_x, destination_top_y] = (
                (0, 0, 0, 0)
                if source_alpha < 128
                else (red, green, blue, 255)
            )
    return result


def widen_backrest(source: Image.Image) -> Image.Image:
    alpha = source.getchannel("A")
    slab_mask = ImageChops.multiply(
        polygon_mask(source.size, BACKREST_CONTOUR),
        alpha,
    )
    bbox = slab_mask.getbbox()
    if bbox is None:
        raise RuntimeError("Backrest contour contains no source pixels.")

    slab = Image.new("RGBA", (bbox[2] - bbox[0], bbox[3] - bbox[1]), (0, 0, 0, 0))
    slab.paste(source.crop(bbox), (0, 0), slab_mask.crop(bbox))
    widened = slab.resize(
        (AUTHORED_BACKREST_WIDTH_PX, slab.height),
        Image.Resampling.BICUBIC,
    )
    # Alpha remains a pixel-authored nearest-neighbour contour.  Only the colour
    # ramp is interpolated, matching the antialiased source art.
    widened.putalpha(
        slab_mask.crop(bbox).resize(
            (AUTHORED_BACKREST_WIDTH_PX, slab.height),
            Image.Resampling.NEAREST,
        )
    )

    result = Image.new("RGBA", source.size, (0, 0, 0, 0))
    result.alpha_composite(
        widened,
        (
            bbox[2] - AUTHORED_BACKREST_WIDTH_PX + AUTHORED_BACKREST_OFFSET_X_PX,
            bbox[1],
        ),
    )

    # Original non-backrest parts are painted after the widened slab.  Thus the
    # seat, far arm, near arm, stem, and wheels keep their exact source pixels.
    remaining = source.copy()
    remaining_pixels = remaining.load()
    slab_pixels = slab_mask.load()
    for y in range(source.height):
        for x in range(source.width):
            if slab_pixels[x, y] != 0:
                remaining_pixels[x, y] = (0, 0, 0, 0)
    result.alpha_composite(remaining)
    return result


def is_upholstery(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, alpha = pixel
    return (
        alpha > 0
        and green >= 42
        and green >= red + 10
        and green >= blue + 8
    )


def seat_rim_top_y(x: int) -> int:
    for (start_x, start_y), (end_x, end_y) in zip(
        SEAT_RIM_TOP_CURVE,
        SEAT_RIM_TOP_CURVE[1:],
    ):
        if start_x <= x <= end_x:
            progress = (x - start_x) / (end_x - start_x)
            return round(start_y + (end_y - start_y) * progress)
    return 10_000


def connected_components(points: set[tuple[int, int]]) -> list[set[tuple[int, int]]]:
    remaining = set(points)
    components: list[set[tuple[int, int]]] = []
    while remaining:
        seed = remaining.pop()
        component = {seed}
        pending = [seed]
        while pending:
            x, y = pending.pop()
            for offset_y in (-1, 0, 1):
                for offset_x in (-1, 0, 1):
                    neighbour = (x + offset_x, y + offset_y)
                    if neighbour not in remaining:
                        continue
                    remaining.remove(neighbour)
                    component.add(neighbour)
                    pending.append(neighbour)
        components.append(component)
    return components


def build_foreground(
    component_source: Image.Image,
    rendered_base: Image.Image | None = None,
) -> Image.Image:
    pixels = component_source.load()
    width, height = component_source.size
    opaque = {
        (x, y)
        for y in range(height)
        for x in range(width)
        if pixels[x, y][3] > 0
    }

    # The foreground is the curved upholstered seat-front rim. Its upper edge
    # follows the source highlight/part contour instead of a canvas axis.
    selected = {
        point
        for point in opaque
        if 238 <= point[0] <= 374
        and seat_rim_top_y(point[0]) <= point[1] <= 414
        and is_upholstery(pixels[point[0], point[1]])
    }
    frontier = set(selected)
    for _ in range(2):
        next_frontier: set[tuple[int, int]] = set()
        for x, y in frontier:
            for offset_y in (-1, 0, 1):
                for offset_x in (-1, 0, 1):
                    neighbour = (x + offset_x, y + offset_y)
                    if (
                        neighbour in opaque
                        and 236 <= neighbour[0] <= 376
                        and seat_rim_top_y(neighbour[0]) - 2 <= neighbour[1] <= 414
                        and neighbour not in selected
                    ):
                        next_frontier.add(neighbour)
        selected.update(next_frontier)
        frontier = next_frontier

    # A nearby upholstered side fragment is separated from the actual rim by
    # the chair's own outline. Keep the largest connected authored component.
    components = sorted(connected_components(selected), key=len, reverse=True)
    if not components:
        raise RuntimeError("Authored seat-rim contour contains no pixels.")
    selected = components[0]

    foreground = Image.new("RGBA", component_source.size, (0, 0, 0, 0))
    foreground_pixels = foreground.load()
    rendered_pixels = (rendered_base or component_source).load()
    for x, y in selected:
        foreground_pixels[x, y] = rendered_pixels[x, y]
    return foreground


def save_png(image: Image.Image, path: Path) -> None:
    image.save(path, format="PNG", optimize=True, compress_level=9)


def main() -> int:
    if file_sha256(SOURCE_PATH) != SOURCE_FILE_SHA256:
        raise RuntimeError(
            "Canonical chair source hash changed without authored-mask re-approval."
        )
    canonical_base = build_runtime_base_from_source()
    save_png(widen_backrest(canonical_base), BASE_PATH)
    if (
        AUTHORED_BASE_FILE_SHA256 != "PENDING"
        and file_sha256(BASE_PATH) != AUTHORED_BASE_FILE_SHA256
    ):
        raise RuntimeError("Deterministic authored chair base hash changed.")

    base = Image.open(BASE_PATH).convert("RGBA")
    # Author the near seat rim from the untouched canonical component layout.
    # Widening the rear slab must never make that slab leak onto the foreground
    # plane merely because its new silhouette touches the seat in alpha space.
    foreground = build_foreground(canonical_base, base)
    save_png(foreground, FRONT_PATH)
    foreground_pixels = foreground.load()
    visible = sum(
        1
        for y in range(foreground.height)
        for x in range(foreground.width)
        if foreground_pixels[x, y][3] > 0
    )
    print("baseSha256=" + file_sha256(BASE_PATH))
    print("foregroundSha256=" + file_sha256(FRONT_PATH))
    print("foregroundPixels=" + str(visible))
    print("canvas=" + str(base.size))
    print("baseBBox=" + str(base.getbbox()))
    print("foregroundBBox=" + str(foreground.getbbox()))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
