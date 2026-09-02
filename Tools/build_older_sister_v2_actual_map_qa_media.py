#!/usr/bin/env python3
"""Build deterministic Older Sister V3 actual-map tile-centre QA media.

The filename is retained as a compatibility entry point for the already-documented V2 QA
workflow. Outputs and gates describe only the zero-credit V3 SD repair.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import statistics
from pathlib import Path

from PIL import Image, ImageDraw


FRAME_PATTERN = re.compile(r"-(\d+)\.png$")
CAPTURE_WIDTH = 1280
CAPTURE_HEIGHT = 720
OVERLAY_ORTHOGRAPHIC_SIZE = 6.5
TILE_WIDTH_PIXELS = 85.3
TILE_HEIGHT_PIXELS = 42.7


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def frame_number(path: Path) -> int:
    match = FRAME_PATTERN.search(path.name)
    if match is None:
        raise ValueError(f"Unrecognized frame name: {path.name}")
    return int(match.group(1))


def rotate_xz(x: float, z: float, yaw_degrees: float) -> tuple[float, float]:
    yaw = math.radians(yaw_degrees)
    return (
        math.cos(yaw) * x + math.sin(yaw) * z,
        -math.sin(yaw) * x + math.cos(yaw) * z,
    )


def ground_world_to_pixel(x: float, z: float) -> tuple[float, float]:
    pixels_per_world_x = CAPTURE_WIDTH / (
        2.0 * OVERLAY_ORTHOGRAPHIC_SIZE * (CAPTURE_WIDTH / CAPTURE_HEIGHT)
    )
    pixels_per_world_z = (
        CAPTURE_HEIGHT / (2.0 * OVERLAY_ORTHOGRAPHIC_SIZE) / math.sqrt(2.0)
    )
    return (
        CAPTURE_WIDTH * 0.5 + x * pixels_per_world_x,
        CAPTURE_HEIGHT * 0.5 - z * pixels_per_world_z,
    )


def actor_centre_world(sample: dict, offset: dict) -> tuple[float, float]:
    offset_x, offset_z = rotate_xz(
        offset["x"], offset["y"], sample["rootWorldYawDegrees"]
    )
    root = sample["rootWorldPosition"]
    return root["x"] - offset_x, root["z"] - offset_z


def sample_for_capture(samples: list[dict], capture_index: int, stride: int) -> dict:
    sample_index = 0 if capture_index == 0 else capture_index * stride - 1
    return samples[min(sample_index, len(samples) - 1)]


def draw_tile_reference(image: Image.Image, centre: tuple[float, float]) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    x, y = centre
    half_w = TILE_WIDTH_PIXELS * 0.5
    half_h = TILE_HEIGHT_PIXELS * 0.5
    points = [(x, y - half_h), (x + half_w, y), (x, y + half_h), (x - half_w, y)]
    draw.line(points + [points[0]], fill=(24, 227, 213, 235), width=2, joint="curve")
    draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(255, 88, 98, 255))


def tracked_crop(image: Image.Image, centre: tuple[float, float]) -> Image.Image:
    crop_width, crop_height = 240, 180
    left = round(centre[0] - crop_width * 0.5)
    top = round(centre[1] - crop_height * 0.62)
    left = max(0, min(image.width - crop_width, left))
    top = max(0, min(image.height - crop_height, top))
    return image.crop((left, top, left + crop_width, top + crop_height)).resize(
        (960, 720), Image.Resampling.NEAREST
    )


def foot_tile_metrics(receipt: dict, offset: dict) -> tuple[list[float], dict]:
    px_x = CAPTURE_WIDTH / (
        2.0 * OVERLAY_ORTHOGRAPHIC_SIZE * (CAPTURE_WIDTH / CAPTURE_HEIGHT)
    )
    px_z = CAPTURE_HEIGHT / (2.0 * OVERLAY_ORTHOGRAPHIC_SIZE) / math.sqrt(2.0)
    errors: list[float] = []
    foot_point_count = 0
    foot_point_outside_count = 0
    planted_point_count = 0
    planted_point_outside_count = 0
    minimum_foot_margin = float("inf")
    minimum_planted_margin = float("inf")
    diamond_half_width = TILE_WIDTH_PIXELS * 0.5
    diamond_half_height = TILE_HEIGHT_PIXELS * 0.5
    diamond_normal_scale = 1.0 / math.sqrt(
        1.0 / diamond_half_width**2 + 1.0 / diamond_half_height**2
    )
    for sample in receipt["fatherCaptureSamples"]:
        left = sample["leftFootLocal"]
        right = sample["rightFootLocal"]
        local_x = (left["x"] + right["x"]) * 0.5 + offset["x"]
        local_z = (left["z"] + right["z"]) * 0.5 + offset["y"]
        world_x, world_z = rotate_xz(
            local_x, local_z, sample["rootWorldYawDegrees"]
        )
        errors.append(math.hypot(world_x * px_x, world_z * px_z))
        for field, planted_field in (
            ("leftFootLocal", "leftFootPlanted"),
            ("rightFootLocal", "rightFootPlanted"),
        ):
            foot = sample[field]
            local_x = foot["x"] + offset["x"]
            local_z = foot["z"] + offset["y"]
            world_x, world_z = rotate_xz(
                local_x, local_z, sample["rootWorldYawDegrees"]
            )
            screen_x = world_x * px_x
            screen_y = world_z * px_z
            normalized = (
                abs(screen_x) / diamond_half_width +
                abs(screen_y) / diamond_half_height
            )
            margin = (1.0 - normalized) * diamond_normal_scale
            foot_point_count += 1
            foot_point_outside_count += int(normalized > 1.0)
            minimum_foot_margin = min(minimum_foot_margin, margin)
            if sample[planted_field]:
                planted_point_count += 1
                planted_point_outside_count += int(normalized > 1.0)
                minimum_planted_margin = min(minimum_planted_margin, margin)
    return errors, {
        "individualFootBonePointCount": foot_point_count,
        "individualFootBoneOutsideDiamondCount": foot_point_outside_count,
        "minimumIndividualFootBoneLineMarginPixels": minimum_foot_margin,
        "plantedFootBonePointCount": planted_point_count,
        "plantedFootBoneOutsideDiamondCount": planted_point_outside_count,
        "minimumPlantedFootBoneLineMarginPixels": minimum_planted_margin,
    }


def build(args: argparse.Namespace) -> None:
    run_root = args.run_root.resolve()
    output = args.output.resolve()
    receipt_path = run_root / "runtime-receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    candidate = receipt["candidates"][0]
    if candidate["familyId"] != "older_sister":
        raise ValueError("Runtime receipt is not the Older Sister candidate proof.")
    if receipt["receiptStatus"] != "OLDER_SISTER_V3_SD_REPAIR_NATIVE_613_WALK_MAP_PROOF_COMPLETE":
        raise ValueError("Actual-map proof did not complete.")

    frames = sorted((run_root / "frames").glob("*.png"), key=frame_number)
    if len(frames) != receipt["compositeCapturedFrames"]:
        raise ValueError("Frame count does not match the runtime receipt.")
    output.mkdir(parents=True, exist_ok=True)
    tracked_root = output / "tile-center-frames"
    tracked_root.mkdir(parents=True, exist_ok=True)

    samples = receipt["fatherCaptureSamples"]
    offset = candidate["standingFootCenterOffsetLocal"]
    stride = receipt["compositeCaptureFrameStride"]
    contact_indices = []
    for leg in range(8):
        leg_samples = [
            index
            for index, sample in enumerate(samples)
            if sample["routeCircuit"] == 0 and sample["routeLeg"] == leg
        ]
        midpoint = leg_samples[len(leg_samples) // 2]
        contact_indices.append(0 if midpoint == 0 else (midpoint + 1) // stride)

    contact_panels: list[Image.Image] = []
    for capture_index, frame_path in enumerate(frames):
        sample = sample_for_capture(samples, capture_index, stride)
        centre = ground_world_to_pixel(*actor_centre_world(sample, offset))
        with Image.open(frame_path) as source:
            frame = source.convert("RGB")
        draw_tile_reference(frame, centre)
        crop = tracked_crop(frame, centre)
        crop.save(tracked_root / f"tile-center-{capture_index:03d}.png", optimize=True)
        if capture_index in contact_indices:
            panel = crop.resize((480, 360), Image.Resampling.NEAREST)
            draw = ImageDraw.Draw(panel)
            label = f"LEG {contact_indices.index(capture_index)}  FRAME {capture_index}"
            draw.rectangle((0, 0, 230, 24), fill=(20, 45, 48))
            draw.text((8, 6), label, fill=(255, 244, 216))
            contact_panels.append(panel)

    sheet = Image.new("RGB", (1920, 720), (24, 46, 48))
    for index, panel in enumerate(contact_panels):
        sheet.paste(panel, ((index % 4) * 480, (index // 4) * 360))
    contact_path = output / "older-sister-v3-actual-map-direction-contact.png"
    sheet.save(contact_path, optimize=True)

    errors, individual_foot_metrics = foot_tile_metrics(receipt, offset)
    metrics = {
        "contract": "FC-OLDER-SISTER-V3-ACTUAL-MAP-TILE-CENTRE-QA-V1",
        "status": "CANDIDATE_USER_APPROVAL_REQUIRED",
        "productionEligible": False,
        "runtimeReceipt": str(receipt_path),
        "runtimeReceiptSha256": sha256(receipt_path),
        "captureFrameCount": len(frames),
        "telemetrySampleCount": len(samples),
        "directions": sorted({sample["direction"] for sample in samples}),
        "targetStandingHeight": candidate["target3DHeight"],
        "targetScreenHeightPixels1280x720": candidate["target3DHeight"] * 39.3,
        "standingFootCenterOffsetLocal": offset,
        "footMidpointTileErrorMedianPixels": statistics.median(errors),
        "footMidpointTileErrorMaximumPixels": max(errors),
        "footMidpointTileErrorGateMedianPixels": 4.0,
        "footMidpointTileErrorGateMaximumPixels": 8.0,
        "footMidpointTileErrorPass": statistics.median(errors) <= 4.0 and max(errors) <= 8.0,
        **individual_foot_metrics,
        "walkGroundClearanceBeforeCorrection": candidate[
            "walkGroundClearanceBeforeCorrection"
        ],
        "standingGroundLiftCorrection": candidate["standingGroundLiftCorrection"],
        "walkGroundClearanceAfterCorrection": candidate[
            "walkGroundClearanceAfterCorrection"
        ],
        "approvedPlayerWalkCycleLowestVertex": 0.1376,
        "walkBodyHorizontalReach": candidate["walkBodyHorizontalReach"],
        "strideOfficeUnits": receipt["fatherMotionStrideOfficeUnits"],
        "phaseOffsetCycles": receipt["nativeMotionPhaseOffsetCycles"],
        "cycleSeconds": receipt["sharedCycleSeconds"],
        "sourceMotionPolicy": "same paid V2 package mesh, skin, UV and authored action 613; deterministic bind-space SD deformation only; poseStrength 1; no donor, retarget, procedural gait, damping, or per-contact host translation",
        "newProviderCreditCharge": 0,
        "contactSheet": str(contact_path),
        "contactSheetSha256": sha256(contact_path),
    }
    (output / "older-sister-v3-actual-map-metrics.json").write_text(
        json.dumps(metrics, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    build(parser.parse_args())


if __name__ == "__main__":
    main()
