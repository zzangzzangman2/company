#!/usr/bin/env python3
"""Build the approved strict side-profile mother east/west walk source.

The generated source is tracked once, then this deterministic tool removes the
green background, extracts its six whole-body panels, normalizes them to the
shipping 256px canvas, and derives west by mirroring east.  Runtime publishing
remains owned by generate_character_locomotion_v1.py.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPO_ROOT / "ArtSources" / "MotherSideWalkV3"
RAW_PATH = SOURCE_ROOT / "mother_east_six_pose_raw_v1.png"
FRAME_ROOT = SOURCE_ROOT / "Frames"
MANIFEST_PATH = SOURCE_ROOT / "mother_side_walk_v3_manifest.json"
RAW_SHA256 = "DB24D3B44BDA89C978CBCE5A7D583A260B0D70D9CF0E0633DE29745B7EE83E32"
FRAME_SIZE = 256
TARGET_HEIGHT = 225
GROUND_Y = 247
PHASE_COUNT = 6


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def hard_alpha(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8).copy()
    visible = rgba[:, :, 3] >= 128
    rgba[:, :, 3] = np.where(visible, 255, 0).astype(np.uint8)
    rgba[~visible, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def foreground_mask(rgb: np.ndarray) -> np.ndarray:
    values = rgb.astype(np.int16)
    red, green, blue = values[:, :, 0], values[:, :, 1], values[:, :, 2]
    chroma_green = (green >= red + 24) & (green >= blue + 18) & (green >= 70)
    foreground = (~chroma_green).astype(np.uint8)
    kernel = np.ones((3, 3), dtype=np.uint8)
    return cv2.morphologyEx(foreground, cv2.MORPH_CLOSE, kernel)


def panel_ranges(mask: np.ndarray) -> list[tuple[int, int]]:
    count, _, stats, centroids = cv2.connectedComponentsWithStats(mask, 8)
    centers: list[float] = []
    for index in range(1, count):
        area = int(stats[index, cv2.CC_STAT_AREA])
        height = int(stats[index, cv2.CC_STAT_HEIGHT])
        if area >= 7000 and height >= 420:
            centers.append(float(centroids[index, 0]))
    centers.sort()
    if len(centers) != PHASE_COUNT:
        raise ValueError(f"expected six figure components, found centers={centers}")
    boundaries = [0]
    boundaries.extend(int(round((left + right) * 0.5)) for left, right in zip(centers, centers[1:]))
    boundaries.append(mask.shape[1])
    return list(zip(boundaries[:-1], boundaries[1:]))


def extract_east_frames() -> list[Image.Image]:
    if sha256_bytes(RAW_PATH.read_bytes()) != RAW_SHA256:
        raise ValueError("MotherSideWalkV3 raw source SHA changed without review")
    with Image.open(RAW_PATH) as loaded:
        rgb = np.asarray(loaded.convert("RGB"), dtype=np.uint8)
    mask = foreground_mask(rgb)
    outputs: list[Image.Image] = []
    for phase, (x0, x1) in enumerate(panel_ranges(mask)):
        local_mask = mask[:, x0:x1] > 0
        rows, columns = np.nonzero(local_mask)
        if not len(columns):
            raise ValueError(f"phase {phase}: empty panel")
        left, right = int(columns.min()), int(columns.max()) + 1
        top, bottom = int(rows.min()), int(rows.max()) + 1
        crop_rgb = rgb[top:bottom, x0 + left : x0 + right].copy()
        crop_alpha = (local_mask[top:bottom, left:right] * 255).astype(np.uint8)

        # Remove green spill from antialiased fringe pixels without changing the
        # teal skirt: only a green channel stronger than both red and blue is capped.
        max_other = np.maximum(crop_rgb[:, :, 0], crop_rgb[:, :, 2]).astype(np.int16)
        green = crop_rgb[:, :, 1].astype(np.int16)
        spill = green > max_other + 18
        crop_rgb[:, :, 1] = np.where(spill, np.minimum(green, max_other + 12), green).astype(np.uint8)
        rgba = np.dstack((crop_rgb, crop_alpha))
        figure = Image.fromarray(rgba, "RGBA")
        scale = TARGET_HEIGHT / figure.height
        width = max(1, int(round(figure.width * scale)))
        figure = figure.resize((width, TARGET_HEIGHT), Image.Resampling.LANCZOS)
        figure = hard_alpha(figure)
        canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
        canvas.alpha_composite(figure, ((FRAME_SIZE - width) // 2, GROUND_Y - TARGET_HEIGHT + 1))
        outputs.append(hard_alpha(canvas))
    if len({frame.tobytes() for frame in outputs}) != PHASE_COUNT:
        raise ValueError("MotherSideWalkV3 east poses are not six unique frames")
    return outputs


def build_frames() -> dict[str, list[Image.Image]]:
    east = extract_east_frames()
    west = [frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for frame in east]
    return {"east": east, "west": west}


def frame_name(direction: str, phase: int) -> str:
    return f"mother_{direction}_walk_{phase}.png"


def manifest_payload(frames: dict[str, list[Image.Image]]) -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "contract": "FC-MOTHER-SIDE-WALK-V3",
        "rawSource": RAW_PATH.relative_to(REPO_ROOT).as_posix(),
        "rawSha256": RAW_SHA256,
        "semanticApproval": {
            "east": {
                "head": "east",
                "torso": "east",
                "pelvis": "east",
                "knees": "east",
                "ankles": "east",
                "shoeToes": "east",
            },
            "west": {
                "head": "west",
                "torso": "west",
                "pelvis": "west",
                "knees": "west",
                "ankles": "west",
                "shoeToes": "west",
            },
            "supportAlternates": True,
            "approvedFrameCount": 12,
        },
        "derivation": "green-key east whole-body six-pose; west is deterministic horizontal mirror",
        "frames": {
            direction: [
                {"phase": phase, "file": frame_name(direction, phase), "rgbaSha256": sha256_bytes(frame.tobytes())}
                for phase, frame in enumerate(direction_frames)
            ]
            for direction, direction_frames in frames.items()
        },
    }


def write_outputs() -> None:
    frames = build_frames()
    FRAME_ROOT.mkdir(parents=True, exist_ok=True)
    for direction, direction_frames in frames.items():
        for phase, frame in enumerate(direction_frames):
            frame.save(FRAME_ROOT / frame_name(direction, phase))
    MANIFEST_PATH.write_text(
        json.dumps(manifest_payload(frames), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print("FC-MOTHER-SIDE-WALK-V3: WROTE | directions=2 frames=12")


def check_outputs() -> None:
    expected = build_frames()
    tracked_manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if tracked_manifest != manifest_payload(expected):
        raise ValueError("MotherSideWalkV3 manifest is stale")
    for direction, direction_frames in expected.items():
        for phase, frame in enumerate(direction_frames):
            path = FRAME_ROOT / frame_name(direction, phase)
            with Image.open(path) as loaded:
                actual = hard_alpha(loaded)
            if actual.tobytes() != frame.tobytes():
                raise ValueError(f"{path}: tracked frame differs from deterministic source")
            alpha = np.asarray(actual.getchannel("A"), dtype=np.uint8)
            rows, _ = np.nonzero(alpha)
            if int(rows.max()) != GROUND_Y or int(rows.min()) < 4:
                raise ValueError(f"{path}: ground/top-margin contract failed")
    for phase in range(PHASE_COUNT):
        mirrored = expected["east"][phase].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if mirrored.tobytes() != expected["west"][phase].tobytes():
            raise ValueError(f"phase {phase}: west is not the exact east mirror")
    print(
        "FC-MOTHER-SIDE-WALK-V3: PASS | directions=2 frames=12 "
        "heading=head/torso/pelvis/knees/ankles/shoe-toes"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    operations = parser.add_mutually_exclusive_group(required=True)
    operations.add_argument("--write", action="store_true")
    operations.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.write:
        write_outputs()
    else:
        check_outputs()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
