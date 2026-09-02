#!/usr/bin/env python3
"""Measure Sister V3 visibility from the real two-circuit Unity map capture."""

from __future__ import annotations

import argparse
import json
import statistics
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

import build_older_sister_v2_actual_map_qa_media as map_qa


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-root", required=True, type=Path)
    parser.add_argument("--geometry-receipt", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def percentile(values: list[float], q: float) -> float:
    return float(np.percentile(np.asarray(values, dtype=np.float64), q))


def main() -> None:
    args = parse_args()
    run_root = args.run_root.resolve()
    receipt = json.loads((run_root / "runtime-receipt.json").read_text(encoding="utf-8"))
    geometry = json.loads(args.geometry_receipt.resolve().read_text(encoding="utf-8"))
    frames = sorted((run_root / "frames").glob("*.png"), key=map_qa.frame_number)
    if len(frames) != receipt["compositeCapturedFrames"]:
        raise RuntimeError("Capture frame count does not match the runtime receipt.")

    # The office and camera are static while the candidate walks. A temporal median of widely
    # spaced frames reconstructs the exact background without a second synthetic render.
    background_samples = [
        np.asarray(Image.open(frames[index]).convert("RGB"), dtype=np.uint8)
        for index in range(0, len(frames), 10)
    ]
    background = np.median(np.stack(background_samples, axis=0), axis=0).astype(np.uint8)
    samples = receipt["fatherCaptureSamples"]
    candidate = receipt["candidates"][0]
    offset = candidate["standingFootCenterOffsetLocal"]
    stride = receipt["compositeCaptureFrameStride"]

    heights: list[float] = []
    widths: list[float] = []
    areas: list[float] = []
    lumas: list[float] = []
    saturations: list[float] = []
    values: list[float] = []
    clipping: list[float] = []
    eye_frames: list[dict] = []

    for capture_index, frame_path in enumerate(frames):
        image = np.asarray(Image.open(frame_path).convert("RGB"), dtype=np.uint8)
        sample = map_qa.sample_for_capture(samples, capture_index, stride)
        center_x, center_y = map_qa.ground_world_to_pixel(
            *map_qa.actor_centre_world(sample, offset)
        )
        left = max(0, round(center_x) - 65)
        right = min(image.shape[1], round(center_x) + 66)
        top = max(0, round(center_y) - 125)
        bottom = min(image.shape[0], round(center_y) + 18)
        crop = image[top:bottom, left:right]
        crop_background = background[top:bottom, left:right]
        difference = np.max(
            np.abs(crop.astype(np.int16) - crop_background.astype(np.int16)), axis=2
        )
        foreground = (difference >= 8).astype(np.uint8)
        foreground = cv2.morphologyEx(
            foreground, cv2.MORPH_CLOSE, np.ones((3, 3), dtype=np.uint8)
        )
        component_count, labels, stats, _ = cv2.connectedComponentsWithStats(
            foreground, connectivity=8
        )
        selected = np.zeros_like(foreground, dtype=bool)
        for component in range(1, component_count):
            x, y, width, height, area = stats[component]
            if area < 25:
                continue
            global_x = left + x + width * 0.5
            global_y = top + y + height * 0.5
            if abs(global_x - center_x) <= 55 and center_y - 120 <= global_y <= center_y + 12:
                selected |= labels == component
        ys, xs = np.where(selected)
        if len(xs) < 100:
            raise RuntimeError(f"Could not isolate candidate silhouette in frame {capture_index}.")
        pixels = crop[selected].astype(np.float32)
        maximum = pixels.max(axis=1)
        minimum = pixels.min(axis=1)
        heights.append(float(ys.max() - ys.min() + 1))
        widths.append(float(xs.max() - xs.min() + 1))
        areas.append(float(len(xs)))
        lumas.append(float((0.299 * pixels[:, 0] + 0.587 * pixels[:, 1] + 0.114 * pixels[:, 2]).mean()))
        saturations.append(float(np.where(maximum > 0, (maximum - minimum) / maximum, 0).mean()))
        values.append(float((maximum / 255.0).mean()))
        clipping.append(float(np.all(pixels >= 250, axis=1).mean()))

        # Teal exists only in the candidate's irises inside this tracked upper-body ROI. Require
        # two nearby connected components, rather than treating unrelated isolated pixels as eyes.
        red = crop[:, :, 0].astype(np.int16)
        green = crop[:, :, 1].astype(np.int16)
        blue = crop[:, :, 2].astype(np.int16)
        teal = (
            (green - red >= 18)
            & (blue - red >= 18)
            & (green >= 80)
            & (blue >= 90)
            & (np.abs(green - blue) <= 75)
        ).astype(np.uint8)
        teal_count, _, teal_stats, _ = cv2.connectedComponentsWithStats(teal, connectivity=8)
        components: list[dict] = []
        for component in range(1, teal_count):
            x, y, width, height, area = teal_stats[component]
            if area < 2:
                continue
            global_y = top + y + height * 0.5
            if not center_y - 115 <= global_y <= center_y - 35:
                continue
            components.append(
                {
                    "area": int(area),
                    "width": int(width),
                    "height": int(height),
                    "x": int(left + x),
                    "y": int(top + y),
                }
            )
        valid_pair = False
        for first_index, first in enumerate(components):
            for second in components[first_index + 1 :]:
                first_center_x = first["x"] + first["width"] * 0.5
                second_center_x = second["x"] + second["width"] * 0.5
                first_center_y = first["y"] + first["height"] * 0.5
                second_center_y = second["y"] + second["height"] * 0.5
                spacing = abs(first_center_x - second_center_x)
                same_row = abs(first_center_y - second_center_y) <= 5
                minimum_eye_height = min(first["height"], second["height"])
                if 3 <= spacing <= 14 and same_row and minimum_eye_height >= 3:
                    valid_pair = True
        if valid_pair:
            eye_frames.append({"frame": capture_index, "components": components})

    target_screen_height = float(candidate["target3DHeight"] * 39.3)
    head_ratio = float(geometry["measuredRatios"]["headToHeight"])
    projected_face_height = target_screen_height * head_ratio
    result = {
        "contract": "FC-OLDER-SISTER-V3-ACTUAL-MAP-VISUAL-GATES-V1",
        "status": "PASS",
        "productionEligible": False,
        "newProviderCreditCharge": 0,
        "captureFrameCount": len(frames),
        "backgroundPolicy": "temporal median of every tenth real Unity capture; no synthetic background",
        "visibleSilhouette": {
            "heightPixelsMedian": statistics.median(heights),
            "heightPixelsP05": percentile(heights, 5),
            "heightPixelsP95": percentile(heights, 95),
            "widthPixelsMedian": statistics.median(widths),
            "areaPixelsMedian": statistics.median(areas),
            "s1HeightPass": 81 <= statistics.median(heights) <= 99,
        },
        "color": {
            "lumaMedian": statistics.median(lumas),
            "lumaP05": percentile(lumas, 5),
            "lumaP95": percentile(lumas, 95),
            "saturationMedian": statistics.median(saturations),
            "valueMedian": statistics.median(values),
            "whiteClippingMedian": statistics.median(clipping),
            "c3LumaPass": 90 <= statistics.median(lumas) <= 125,
            "c4ClippingPass": statistics.median(clipping) <= 0.05,
        },
        "face": {
            "measuredHeadToHeightRatio": head_ratio,
            "projectedFaceHeightPixels": projected_face_height,
            "faceHeightGatePixels": 22,
            "faceHeightPass": projected_face_height >= 22,
            "eyeHeightGatePixels": 3,
            "framesWithBothEyesAtLeastThreePixels": len(eye_frames),
            "bothEyesThreePixelsPass": len(eye_frames) > 0,
            "bestEyeFrames": eye_frames[:12],
        },
    }
    required = (
        result["visibleSilhouette"]["s1HeightPass"]
        and result["color"]["c3LumaPass"]
        and result["color"]["c4ClippingPass"]
        and result["face"]["faceHeightPass"]
        and result["face"]["bothEyesThreePixelsPass"]
    )
    result["status"] = "PASS" if required else "FAIL"
    args.output.resolve().parent.mkdir(parents=True, exist_ok=True)
    args.output.resolve().write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if not required:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
