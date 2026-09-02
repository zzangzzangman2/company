"""Repair the rejected Sister V2 atlas without regenerating or changing its UVs."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageFilter


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--uv-mask-npz", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--debug-mask", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args()


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def rasterize_categories(npz_path, width, height):
    data = np.load(npz_path)
    uvs = data["uv"]
    categories = data["category"]
    masks = np.zeros((7, height, width), dtype=np.uint8)
    pixel_uvs = np.empty_like(uvs, dtype=np.int32)
    pixel_uvs[:, :, 0] = np.rint(uvs[:, :, 0] * (width - 1)).astype(np.int32)
    pixel_uvs[:, :, 1] = np.rint((1.0 - uvs[:, :, 1]) * (height - 1)).astype(np.int32)
    np.clip(pixel_uvs[:, :, 0], 0, width - 1, out=pixel_uvs[:, :, 0])
    np.clip(pixel_uvs[:, :, 1], 0, height - 1, out=pixel_uvs[:, :, 1])
    for triangle, category in zip(pixel_uvs, categories):
        cv2.fillConvexPoly(masks[int(category)], triangle, 255, lineType=cv2.LINE_8)
    return masks


def set_tinted_value(rgb, mask, base_rgb, low, high):
    value = rgb.max(axis=2).astype(np.float32)
    selected = value[mask]
    if selected.size == 0:
        return
    p10, p90 = np.percentile(selected, [10, 90])
    detail = np.clip((value - p10) / max(float(p90 - p10), 1.0), 0.0, 1.0)
    target_value = low + detail * (high - low)
    base = np.asarray(base_rgb, dtype=np.float32)
    base /= max(float(base.max()), 1.0)
    tinted = target_value[:, :, None] * base[None, None, :]
    rgb[mask] = np.clip(tinted[mask], 0, 255).astype(np.uint8)


def main():
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()
    debug_path = Path(args.debug_mask).resolve()
    receipt_path = Path(args.receipt).resolve()
    for path in (output_path, debug_path, receipt_path):
        path.parent.mkdir(parents=True, exist_ok=True)

    source_image = Image.open(input_path).convert("RGBA")
    input_hash = sha256(input_path)
    rgba = np.asarray(source_image).copy()
    rgb = rgba[:, :, :3]
    height, width = rgb.shape[:2]
    category_masks = rasterize_categories(args.uv_mask_npz, width, height) > 0
    head = category_masks[1]
    torso = category_masks[2]
    arms = category_masks[3] | category_masks[4]
    legs = category_masks[5] | category_masks[6]

    r = rgb[:, :, 0].astype(np.int16)
    g = rgb[:, :, 1].astype(np.int16)
    b = rgb[:, :, 2].astype(np.int16)
    maximum = np.maximum(np.maximum(r, g), b)
    minimum = np.minimum(np.minimum(r, g), b)
    saturation_span = maximum - minimum
    dark = maximum < 155
    teal = head & (g > 75) & (b > 65) & (g > r + 18) & (b > r + 10)
    navy = (torso | legs) & dark & (b > r + 12) & (b > g + 4)
    hair = head & dark & ~teal
    tank = torso & dark & ~navy

    set_tinted_value(rgb, hair, (61, 50, 68), 24.0, 82.0)
    set_tinted_value(rgb, tank, (88, 82, 102), 76.0, 132.0)
    set_tinted_value(rgb, navy, (48, 76, 154), 72.0, 158.0)

    r = rgb[:, :, 0].astype(np.int16)
    g = rgb[:, :, 1].astype(np.int16)
    b = rgb[:, :, 2].astype(np.int16)
    maximum = np.maximum(np.maximum(r, g), b)
    minimum = np.minimum(np.minimum(r, g), b)
    likely_skin = (head | torso | arms | legs) & (r > 175) & (r > b + 8) & (maximum - minimum < 95)
    if likely_skin.any():
        skin = rgb[likely_skin].astype(np.float32)
        skin[:, 0] = np.minimum(skin[:, 0] * 0.93, 236.0)
        skin[:, 1] = np.minimum(skin[:, 1] * 0.91, 213.0)
        skin[:, 2] = np.minimum(skin[:, 2] * 0.90, 205.0)
        rgb[likely_skin] = np.clip(skin, 0, 255).astype(np.uint8)

    teal_u8 = (teal.astype(np.uint8) * 255)
    teal_components = []
    count, labels, stats, _ = cv2.connectedComponentsWithStats(teal_u8, connectivity=8)
    iris_mask = np.zeros_like(teal, dtype=np.uint8)
    for component in range(1, count):
        area = int(stats[component, cv2.CC_STAT_AREA])
        if 6 <= area <= 10000:
            component_mask = (labels == component).astype(np.uint8) * 255
            # The previous 7x7 expansion still rasterized one frontal eye to only 2 px high in
            # the real 1280x720 isometric map. Keep the same source iris islands, but expand them
            # enough for both eyes to clear the mandatory 3 px S8 screen gate.
            component_mask = cv2.dilate(component_mask, np.ones((11, 11), np.uint8), iterations=1)
            iris_mask = cv2.bitwise_or(iris_mask, component_mask)
            teal_components.append(area)
    iris = (iris_mask > 0) & head
    outline = (cv2.dilate(iris_mask, np.ones((13, 13), np.uint8), iterations=1) > 0) & head & ~iris
    rgb[outline] = np.array([22, 24, 31], dtype=np.uint8)
    if iris.any():
        iris_pixels = rgb[iris].astype(np.float32)
        iris_value = iris_pixels.max(axis=1)
        detail = np.clip((iris_value - 50.0) / 150.0, 0.0, 1.0)
        iris_pixels[:, 0] = np.clip(10 + 20 * detail, 0, 255)
        iris_pixels[:, 1] = np.clip(150 + 80 * detail, 0, 255)
        iris_pixels[:, 2] = np.clip(145 + 80 * detail, 0, 255)
        rgb[iris] = iris_pixels.astype(np.uint8)

    bright_neutral = (maximum > 165) & (saturation_span < 55)
    shorts_nearby = cv2.dilate(navy.astype(np.uint8) * 255, np.ones((17, 17), np.uint8), iterations=1) > 0
    piping_seed = bright_neutral & shorts_nearby & (torso | legs)
    piping = (cv2.dilate(piping_seed.astype(np.uint8) * 255, np.ones((5, 5), np.uint8), iterations=1) > 0) & shorts_nearby
    rgb[piping] = np.array([242, 244, 247], dtype=np.uint8)

    repaired = Image.fromarray(rgba, mode="RGBA").filter(
        ImageFilter.UnsharpMask(radius=1.2, percent=135, threshold=3)
    )
    repaired.save(output_path)

    debug = np.zeros((height, width, 3), dtype=np.uint8)
    debug[head] = (160, 80, 220)
    debug[torso] = (230, 160, 40)
    debug[arms] = (40, 190, 220)
    debug[legs] = (60, 190, 80)
    debug[hair] = (45, 32, 50)
    debug[tank] = (105, 96, 125)
    debug[navy] = (40, 75, 175)
    debug[piping] = (250, 250, 250)
    debug[iris] = (10, 240, 225)
    Image.fromarray(debug, mode="RGB").save(debug_path)

    result_rgb = np.asarray(repaired)[:, :, :3].astype(np.float32)
    luma = 0.299 * result_rgb[:, :, 0] + 0.587 * result_rgb[:, :, 1] + 0.114 * result_rgb[:, :, 2]
    receipt = {
        "contract": "FC-OLDER-SISTER-V3-SD-ALBEDO-REPAIR-V1",
        "status": "LOCAL_REPAIR_VISUAL_REVIEW_REQUIRED",
        "input": str(input_path),
        "inputSha256": input_hash,
        "uvMaskNpz": str(Path(args.uv_mask_npz).resolve()),
        "output": str(output_path),
        "outputSha256": sha256(output_path),
        "newProviderCreditCharge": 0,
        "pixelCounts": {
            "hair": int(hair.sum()),
            "tank": int(tank.sum()),
            "navyShorts": int(navy.sum()),
            "whitePiping": int(piping.sum()),
            "tealIris": int(iris.sum()),
            "skinControlled": int(likely_skin.sum()),
        },
        "tealSourceComponentAreas": teal_components,
        "irisExpansionKernelPixels": 11,
        "irisOutlineKernelPixels": 13,
        "atlasMeanLuma": float(luma.mean()),
        "policy": "same Meshy UV atlas, deterministic category masks from original skin weights; value-separated hair/tank/shorts, thickened existing piping, enlarged existing teal iris pixels, controlled skin highlights",
        "productionEligible": False,
    }
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("OLDER_SISTER_V3_SD_ALBEDO_REPAIR=PASS")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    main()
