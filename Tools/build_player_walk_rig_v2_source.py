#!/usr/bin/env python3
"""Build the authored south paper-doll PSB for FC-PLAYER-WALK-RIG-V2.

This is a deterministic source-art operation.  It keeps the approved face, cap,
hair, jacket, and torso pixels from PlayerSouthContactV1, then separates the
limbs with hand-authored anatomical masks.  No frame interpolation or generated
full-character image is used.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT / (
    "Assets/Resources/FamilyCompany/PlayerSouthContactV1/Frames/"
    "player_south_contact_0_v1.png"
)
OUTPUT_DIR = PROJECT / "ArtSources/PlayerWalkRigV2"
OUTPUT_PSB = OUTPUT_DIR / "PlayerWalkRig_south.psb"
OUTPUT_PREVIEW = OUTPUT_DIR / "PlayerWalkRig_south_reference.png"
OUTPUT_MANIFEST = OUTPUT_DIR / "south-layer-manifest.json"
PYTHON_DEPS = PROJECT / "work/python_deps"

CANVAS = (384, 512)
SOURCE_OFFSET = (103, 111)


def polygon_mask(size: tuple[int, int], polygons: list[list[tuple[int, int]]]) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    for points in polygons:
        draw.polygon(points, fill=255)
    return mask


def rect_mask(size: tuple[int, int], box: tuple[int, int, int, int]) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rectangle(box, fill=255)
    return mask


def intersect(*masks: Image.Image) -> Image.Image:
    values = [mask.load() for mask in masks]
    result = Image.new("L", masks[0].size, 0)
    target = result.load()
    width, height = result.size
    for y in range(height):
        for x in range(width):
            target[x, y] = 255 if all(value[x, y] for value in values) else 0
    return result


def union(*masks: Image.Image) -> Image.Image:
    values = [mask.load() for mask in masks]
    result = Image.new("L", masks[0].size, 0)
    target = result.load()
    width, height = result.size
    for y in range(height):
        for x in range(width):
            target[x, y] = 255 if any(value[x, y] for value in values) else 0
    return result


def subtract(mask: Image.Image, other: Image.Image) -> Image.Image:
    a = mask.load()
    b = other.load()
    result = Image.new("L", mask.size, 0)
    target = result.load()
    width, height = mask.size
    for y in range(height):
        for x in range(width):
            target[x, y] = 255 if a[x, y] and not b[x, y] else 0
    return result


def source_layer(source: Image.Image, mask: Image.Image) -> Image.Image:
    layer = Image.new("RGBA", source.size, (0, 0, 0, 0))
    alpha = intersect(source.getchannel("A"), mask)
    layer.paste(source, (0, 0), alpha)
    return layer


def crop_layer(layer: Image.Image) -> tuple[Image.Image, int, int]:
    bbox = layer.getbbox()
    if bbox is None:
        raise RuntimeError("authored layer is empty")
    left, top, right, bottom = bbox
    return layer.crop(bbox), left + SOURCE_OFFSET[0], top + SOURCE_OFFSET[1]


def limb_polygon(
    start: tuple[float, float],
    end: tuple[float, float],
    width: float,
    extend: float,
) -> list[tuple[int, int]]:
    sx, sy = start
    ex, ey = end
    dx, dy = ex - sx, ey - sy
    length = max((dx * dx + dy * dy) ** 0.5, 0.001)
    ux, uy = dx / length, dy / length
    px, py = -uy * width * 0.5, ux * width * 0.5
    sx -= ux * extend
    sy -= uy * extend
    ex += ux * extend
    ey += uy * extend
    return [
        (round(sx + px), round(sy + py)),
        (round(ex + px), round(ey + py)),
        (round(ex - px), round(ey - py)),
        (round(sx - px), round(sy - py)),
    ]


def clean_leg_layers(size: tuple[int, int]) -> dict[str, Image.Image]:
    outline = (9, 16, 29, 255)
    denim = (51, 68, 108, 255)
    denim_dark = (25, 38, 65, 255)
    denim_light = (73, 89, 132, 255)

    joints = {
        "L": {"hip": (78, 246), "knee": (70, 321), "ankle": (67, 365)},
        "R": {"hip": (101, 246), "knee": (108, 321), "ankle": (111, 365)},
    }
    result: dict[str, Image.Image] = {}

    def segment(name: str, start: tuple[int, int], end: tuple[int, int], width: int) -> None:
        layer = Image.new("RGBA", size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        draw.polygon(limb_polygon(start, end, width, 6), fill=outline)
        draw.polygon(limb_polygon(start, end, width - 8, 3), fill=denim)
        draw.line((start[0] - 3, start[1] + 4, end[0] - 3, end[1] - 3), fill=denim_light, width=3)
        draw.line((start[0] + 5, start[1] + 5, end[0] + 5, end[1] - 2), fill=denim_dark, width=3)
        result[name] = layer

    def shoe(name: str, ankle: tuple[int, int]) -> None:
        x, y = ankle
        layer = Image.new("RGBA", size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        outer = [
            (x - 12, y - 8), (x + 12, y - 8), (x + 17, y + 7),
            (x + 21, y + 24), (x + 16, y + 34), (x - 16, y + 34),
            (x - 21, y + 24), (x - 17, y + 7),
        ]
        inner = [
            (x - 8, y - 3), (x + 8, y - 3), (x + 13, y + 9),
            (x + 16, y + 23), (x + 12, y + 27), (x - 12, y + 27),
            (x - 16, y + 23), (x - 13, y + 9),
        ]
        draw.polygon(outer, fill=(24, 26, 36, 255))
        draw.polygon(inner, fill=(241, 239, 233, 255))
        draw.polygon(
            [(x - 9, y - 2), (x + 9, y - 2), (x + 12, y + 8), (x - 12, y + 8)],
            fill=(57, 61, 73, 255),
        )
        draw.line((x - 11, y + 12, x + 11, y + 12), fill=(132, 137, 148, 255), width=3)
        draw.line((x - 13, y + 18, x + 13, y + 18), fill=(132, 137, 148, 255), width=3)
        draw.rectangle((x - 16, y + 27, x + 16, y + 32), fill=(82, 84, 93, 255))
        draw.line((x - 13, y + 29, x + 13, y + 29), fill=(225, 224, 218, 255), width=2)
        result[name] = layer

    for side, points in joints.items():
        segment(f"thigh_{side}", points["hip"], points["knee"], 18)
        segment(f"shin_{side}", points["knee"], points["ankle"], 15)
        shoe(f"shoe_{side}", points["ankle"])
    return result


def semantic_masks(size: tuple[int, int], source_alpha: Image.Image) -> dict[str, Image.Image]:
    # All coordinates are authored against the approved 177x401 south contact.
    hat = polygon_mask(size, [[(15, 0), (163, 0), (176, 74), (151, 96), (25, 92), (0, 72)]])
    head = polygon_mask(size, [[(8, 47), (169, 45), (174, 151), (146, 184), (36, 184), (5, 146)]])
    hair_front = polygon_mask(size, [[(5, 47), (169, 43), (159, 119), (127, 97), (103, 121), (72, 94), (45, 126), (7, 111)]])
    torso = polygon_mask(size, [[(42, 157), (136, 157), (143, 282), (122, 310), (56, 310), (35, 282)]])
    pelvis = polygon_mask(size, [[(52, 269), (128, 269), (126, 306), (101, 313), (76, 312), (52, 304)]])

    arm_l = polygon_mask(size, [[(35, 160), (67, 175), (56, 234), (46, 291), (8, 294), (0, 252), (15, 190)]])
    arm_r = polygon_mask(size, [[(113, 172), (143, 157), (163, 187), (177, 251), (169, 291), (131, 290), (121, 230)]])
    upper_arm_l = intersect(arm_l, rect_mask(size, (0, 154, 70, 232)))
    forearm_l = intersect(arm_l, rect_mask(size, (0, 219, 61, 281)))
    hand_l = intersect(arm_l, rect_mask(size, (0, 263, 54, 305)))
    upper_arm_r = intersect(arm_r, rect_mask(size, (108, 154, 176, 232)))
    forearm_r = intersect(arm_r, rect_mask(size, (118, 218, 176, 281)))
    hand_r = intersect(arm_r, rect_mask(size, (124, 263, 176, 304)))

    # These are anatomical silhouettes following the actual crossed limbs, not a
    # centre crop.  Joint overlaps are deliberate so rotations cannot open gaps.
    thigh_l = polygon_mask(size, [[(59, 278), (94, 274), (107, 316), (94, 350), (65, 348), (53, 310)]])
    shin_l = polygon_mask(size, [[(61, 326), (99, 326), (105, 382), (61, 389), (50, 357)]])
    shoe_l = polygon_mask(size, [[(51, 364), (101, 361), (112, 395), (93, 401), (55, 400), (43, 385)]])
    thigh_r = polygon_mask(size, [[(86, 275), (128, 276), (132, 325), (118, 348), (91, 342), (78, 310)]])
    shin_r = polygon_mask(size, [[(91, 323), (133, 318), (139, 375), (114, 389), (91, 373), (82, 343)]])
    shoe_r = polygon_mask(size, [[(86, 353), (137, 348), (146, 380), (130, 397), (93, 390), (78, 375)]])

    masks = {
        "hat": hat,
        "neck_head_face": subtract(head, hair_front),
        "hair_front": hair_front,
        "torso": torso,
        "pelvis": pelvis,
        "upper_arm_L": upper_arm_l,
        "forearm_L": forearm_l,
        "hand_L": hand_l,
        "upper_arm_R": upper_arm_r,
        "forearm_R": forearm_r,
        "hand_R": hand_r,
        "thigh_L": thigh_l,
        "shin_L": shin_l,
        "shoe_L": shoe_l,
        "thigh_R": thigh_r,
        "shin_R": shin_r,
        "shoe_R": shoe_r,
    }

    # Route fringe pixels explicitly into their semantic parent. This only fills
    # outlines missed by polygon corners; the moving limb cores stay hand-authored.
    covered = union(*masks.values())
    fringe = subtract(source_alpha, covered)
    fringe_pixels = fringe.load()
    routes = {name: mask.load() for name, mask in masks.items()}
    width, height = size
    for y in range(height):
        for x in range(width):
            if not fringe_pixels[x, y]:
                continue
            if y < 155:
                target = "neck_head_face"
            elif y < 270:
                target = "torso"
            elif y < 322:
                target = "pelvis"
            elif x < 88:
                target = "shin_L" if y < 365 else "shoe_L"
            else:
                target = "shin_R" if y < 360 else "shoe_R"
            routes[target][x, y] = 255
    return masks


def main() -> None:
    if PYTHON_DEPS.exists():
        sys.path.insert(0, str(PYTHON_DEPS))
    try:
        from psd_tools import PSDImage
        from psd_tools.constants import Compression
    except ImportError as exc:
        raise SystemExit(
            "psd-tools is required. Install psd-tools==1.17.4 into " + str(PYTHON_DEPS)
        ) from exc

    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (177, 401):
        raise RuntimeError(f"unexpected source dimensions: {source.size}")
    masks = semantic_masks(source.size, source.getchannel("A"))
    layers = {name: source_layer(source, mask) for name, mask in masks.items()}
    layers.update(clean_leg_layers(source.size))

    # Painter order: rear limbs, body, front limbs, then identity layers.
    painter_order = [
        "thigh_R", "shin_R", "shoe_R",
        "upper_arm_R", "forearm_R", "hand_R",
        "pelvis", "torso",
        "thigh_L", "shin_L", "shoe_L",
        "upper_arm_L", "forearm_L", "hand_L",
        "neck_head_face", "hair_front", "hat",
    ]
    preview = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    psd = PSDImage.new("RGBA", CANVAS, color=(0, 0, 0, 0))
    # Explicit PSB header: this is a small document, so psd-tools otherwise picks PSD.
    psd._record.header.version = 2
    manifest_layers: list[dict[str, object]] = []
    for sorting_order, name in enumerate(painter_order):
        cropped, left, top = crop_layer(layers[name])
        preview.alpha_composite(cropped, (left, top))
        psd.create_pixel_layer(
            cropped,
            name=name,
            top=top,
            left=left,
            compression=Compression.RLE,
        )
        manifest_layers.append(
            {
                "name": name,
                "left": left,
                "top": top,
                "width": cropped.width,
                "height": cropped.height,
                "sortingOrder": sorting_order,
            }
        )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    psd.save(OUTPUT_PSB)
    preview.save(OUTPUT_PREVIEW)
    reopened = PSDImage.open(OUTPUT_PSB)
    if reopened._record.header.version != 2 or len(list(reopened)) != len(painter_order):
        raise RuntimeError("saved PSB failed version/layer verification")

    manifest = {
        "contract": "FC-PLAYER-WALK-RIG-V2-LAYERS",
        "source": SOURCE.relative_to(PROJECT).as_posix(),
        "sourceSha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest().upper(),
        "psb": OUTPUT_PSB.relative_to(PROJECT).as_posix(),
        "psbSha256": hashlib.sha256(OUTPUT_PSB.read_bytes()).hexdigest().upper(),
        "canvas": list(CANVAS),
        "pixelsPerUnit": 324,
        "sourceOffset": list(SOURCE_OFFSET),
        "generatedFullFrames": False,
        "interpolatedPixels": False,
        "layers": manifest_layers,
        "jointsCanvasPx": {
            "pelvis": [192, 126],
            "hip_L": [181, 155],
            "knee_L": [173, 80],
            "ankle_L": [170, 36],
            "foot_L": [170, 4],
            "hip_R": [204, 155],
            "knee_R": [211, 80],
            "ankle_R": [214, 36],
            "foot_R": [214, 4],
            "shoulder_L": [151, 221],
            "elbow_L": [138, 171],
            "wrist_L": [127, 131],
            "shoulder_R": [232, 221],
            "elbow_R": [245, 171],
            "wrist_R": [256, 131]
        },
    }
    OUTPUT_MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"PLAYER_WALK_RIG_V2_SOURCE: PASS | psb={OUTPUT_PSB} layers={len(painter_order)}")


if __name__ == "__main__":
    main()
