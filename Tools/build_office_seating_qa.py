#!/usr/bin/env python3
"""Build Northwest seating transition QA sheets and immutable source metrics."""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


@dataclass(frozen=True)
class Character:
    character_id: str
    label: str
    asset_root: str

    def frame_folder(self, repo_root: Path) -> Path:
        return repo_root / self.asset_root / "Pixel" / "OfficeSeatingV1" / "Frames"


CHARACTERS = (
    Character("player", "Player", "Assets/Art/Characters/Player"),
    Character("older_sister", "Older Sister", "Assets/Art/Characters/Family/OlderSister"),
    Character("father", "Father", "Assets/Art/Characters/Family/Father"),
    Character("mother", "Mother", "Assets/Art/Characters/Family/Mother"),
)
CLIPS = (("sit_down", 4), ("sit_work", 6), ("stand_up", 4))
EXPECTED_FRAME_COUNT = sum(count for _, count in CLIPS) * len(CHARACTERS)

# Human-reviewed anatomy points in Unity Sprite-rect coordinates (origin: bottom-left).
# Sequence order is SitDown 0..3, Work 0..5, StandUp 0..3.
CALIBRATION: dict[str, dict[str, tuple[tuple[int, int], ...]]] = {
    "player": {
        "pelvis": ((130, 69), (137, 60), (143, 52), (145, 49),
                   (130, 49), (130, 49), (130, 49), (130, 49), (130, 49), (130, 49),
                   (145, 49), (143, 52), (137, 60), (130, 69)),
        "hand": ((106, 42), (89, 52), (77, 69), (76, 68),
                 (78, 90), (67, 80), (78, 90), (68, 91), (74, 86), (78, 91),
                 (76, 68), (77, 69), (89, 52), (106, 42)),
    },
    "older_sister": {
        "pelvis": ((130, 73), (138, 63), (145, 55), (147, 51),
                   (130, 63), (131, 63), (131, 63), (130, 63), (130, 63), (129, 63),
                   (147, 51), (145, 55), (138, 63), (130, 73)),
        "hand": ((109, 50), (158, 70), (170, 58), (176, 58),
                 (75, 108), (75, 108), (75, 108), (74, 108), (74, 108), (74, 108),
                 (176, 58), (170, 58), (158, 70), (109, 50)),
    },
    "father": {
        "pelvis": ((128, 69), (138, 59), (144, 52), (145, 49),
                   (123, 50), (122, 50), (123, 50), (123, 50), (123, 50), (123, 50),
                   (145, 49), (144, 52), (138, 59), (128, 69)),
        "hand": ((99, 78), (86, 72), (76, 70), (75, 70),
                 (76, 104), (76, 104), (76, 104), (76, 104), (76, 104), (76, 104),
                 (75, 70), (76, 70), (86, 72), (99, 78)),
    },
    "mother": {
        "pelvis": ((131, 74), (140, 64), (146, 55), (148, 51),
                   (126, 62), (126, 62), (126, 62), (126, 62), (126, 62), (126, 62),
                   (148, 51), (146, 55), (140, 64), (131, 74)),
        "hand": ((99, 59), (93, 55), (80, 69), (78, 70),
                 (84, 87), (75, 78), (86, 91), (81, 84), (84, 90), (85, 89),
                 (78, 70), (80, 69), (93, 55), (99, 59)),
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def load_font(size: int) -> ImageFont.ImageFont:
    for candidate in (Path("C:/Windows/Fonts/arial.ttf"), Path("C:/Windows/Fonts/segoeui.ttf")):
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def frame_path(repo_root: Path, character: Character, clip: str, frame: int) -> Path:
    return character.frame_folder(repo_root) / f"{character.character_id}_northwest_{clip}_{frame}.png"


def inspect_frame(path: Path) -> dict[str, Any]:
    source = path.read_bytes()
    with Image.open(path) as loaded:
        sprite = loaded.convert("RGBA")
    alpha = sprite.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError(f"Empty frame: {path}")
    colors = alpha.getcolors(maxcolors=256)
    values = {value for _, value in colors} if colors is not None else set(range(256))
    return {
        "size": list(sprite.size),
        "mode": sprite.mode,
        "alphaValues": sorted(values),
        "alphaBoundsTopLeft": [bbox[0], bbox[1], bbox[2] - 1, bbox[3] - 1],
        "bottomMarginPx": sprite.height - bbox[3],
        "sha256": hashlib.sha256(source).hexdigest().upper(),
    }


def anchor_near_opaque(path: Path, anchor: tuple[int, int], radius: int = 6) -> bool:
    with Image.open(path) as loaded:
        alpha = loaded.convert("RGBA").getchannel("A")
    center_x = anchor[0]
    center_y = 255 - anchor[1]
    for y in range(max(0, center_y - radius), min(256, center_y + radius + 1)):
        for x in range(max(0, center_x - radius), min(256, center_x + radius + 1)):
            if alpha.getpixel((x, y)) > 0:
                return True
    return False


def draw_coordinate_sprite(
    canvas: Image.Image,
    sprite: Image.Image,
    x: int,
    y: int,
    tile: int,
    pelvis: tuple[int, int],
    hand: tuple[int, int],
) -> None:
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(239, 232, 218), outline=(92, 76, 70), width=2)
    canvas.alpha_composite(sprite.resize((tile, tile), Image.Resampling.NEAREST), (x, y))
    for source_coordinate in range(0, 257, 32):
        coordinate = int(round(source_coordinate * tile / 256.0))
        color = (64, 86, 104, 90) if source_coordinate not in (0, 256) else (64, 86, 104, 180)
        draw.line((x + coordinate, y, x + coordinate, y + tile), fill=color, width=1)
        draw.line((x, y + coordinate, x + tile, y + coordinate), fill=color, width=1)
    for point, color in ((pelvis, (56, 205, 255)), (hand, (255, 88, 88))):
        point_x = x + int(round(point[0] * tile / 256.0))
        point_y = y + int(round((256 - point[1]) * tile / 256.0))
        draw.ellipse(
            (point_x - 5, point_y - 5, point_x + 5, point_y + 5),
            fill=color,
            outline=(15, 20, 25),
            width=2,
        )


def build_character_sheet(repo_root: Path, output: Path, character: Character) -> None:
    tile = 256
    columns = 7
    rows = 2
    header = 42
    label_height = 30
    width = columns * tile
    height = header + rows * (tile + label_height)
    canvas = Image.new("RGBA", (width, height), (29, 34, 45, 255))
    draw = ImageDraw.Draw(canvas)
    title_font = load_font(20)
    label_font = load_font(15)
    draw.text((10, 9), f"{character.label} / NORTHWEST / 14 frames / grid=32px", font=title_font, fill=(245, 238, 222))
    index = 0
    for clip, count in CLIPS:
        for frame in range(count):
            column = index % columns
            row = index // columns
            x = column * tile
            y = header + row * (tile + label_height)
            path = frame_path(repo_root, character, clip, frame)
            with Image.open(path) as loaded:
                sprite = loaded.convert("RGBA")
            pelvis = CALIBRATION[character.character_id]["pelvis"][index]
            hand = CALIBRATION[character.character_id]["hand"][index]
            draw_coordinate_sprite(canvas, sprite, x, y, tile, pelvis, hand)
            draw.text((x + 7, y + tile + 5), f"{clip}/{frame}", font=label_font, fill=(245, 238, 222))
            index += 1
    canvas.convert("RGB").save(output / f"{character.character_id}_northwest_seating14_grid_v1.png", quality=95)


def build_animation(repo_root: Path, output: Path, character: Character) -> None:
    frames: list[Image.Image] = []
    sequence = [(clip, frame) for clip, count in CLIPS for frame in range(count)]
    sequence.extend(("sit_work", frame) for frame in range(6))
    for clip, frame in sequence:
        with Image.open(frame_path(repo_root, character, clip, frame)) as loaded:
            sprite = loaded.convert("RGBA").resize((384, 384), Image.Resampling.NEAREST)
        background = Image.new("RGBA", sprite.size, (239, 232, 218, 255))
        background.alpha_composite(sprite)
        frames.append(background.convert("P", palette=Image.Palette.ADAPTIVE))
    frames[0].save(
        output / f"{character.character_id}_northwest_seating_transition_v1.gif",
        save_all=True,
        append_images=frames[1:],
        duration=125,
        loop=0,
        disposal=2,
    )


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    metrics: dict[str, Any] = {"schemaVersion": 1, "frameCount": 0, "frames": {}, "failures": []}
    for character in CHARACTERS:
        build_character_sheet(repo_root, output, character)
        build_animation(repo_root, output, character)
        sequence_index = 0
        for clip, count in CLIPS:
            for frame in range(count):
                path = frame_path(repo_root, character, clip, frame)
                relative = str(path.relative_to(repo_root)).replace("\\", "/")
                try:
                    frame_metrics = inspect_frame(path)
                    pelvis = CALIBRATION[character.character_id]["pelvis"][sequence_index]
                    hand = CALIBRATION[character.character_id]["hand"][sequence_index]
                    frame_metrics["pelvisAnchorPx"] = list(pelvis)
                    frame_metrics["handAnchorPx"] = list(hand)
                    metrics["frames"][relative] = frame_metrics
                    if frame_metrics["size"] != [256, 256] or frame_metrics["mode"] != "RGBA":
                        metrics["failures"].append(f"format:{relative}")
                    if any(value not in (0, 255) for value in frame_metrics["alphaValues"]):
                        metrics["failures"].append(f"soft-alpha:{relative}")
                    if frame_metrics["bottomMarginPx"] < 0:
                        metrics["failures"].append(f"clipped:{relative}")
                    if not anchor_near_opaque(path, pelvis):
                        metrics["failures"].append(f"pelvis-off-body:{relative}:{pelvis}")
                    if not anchor_near_opaque(path, hand):
                        metrics["failures"].append(f"hand-off-body:{relative}:{hand}")
                except Exception as exception:
                    metrics["failures"].append(f"{relative}:{exception}")
                sequence_index += 1
    metrics["frameCount"] = len(metrics["frames"])
    (output / "northwest_seating_56_frame_metrics_v1.json").write_text(
        json.dumps(metrics, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    if metrics["frameCount"] != EXPECTED_FRAME_COUNT or metrics["failures"]:
        raise RuntimeError(
            f"Seating QA failed: frames={metrics['frameCount']}/{EXPECTED_FRAME_COUNT}, failures={metrics['failures']}"
        )
    print(f"OFFICE_SEATING_SOURCE_QA: PASS frames={metrics['frameCount']} output={output}")


if __name__ == "__main__":
    main()
