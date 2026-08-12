#!/usr/bin/env python3
"""Build compact visual QA previews for the 8-direction / 6-frame sprites."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DIRECTIONS = (
    "south",
    "southwest",
    "west",
    "northwest",
    "north",
    "northeast",
    "east",
    "southeast",
)


@dataclass(frozen=True)
class Character:
    character_id: str
    label: str
    asset_root: str

    def frame_folder(self, repo_root: Path) -> Path:
        return repo_root / self.asset_root / "Pixel" / "HighMotion" / "Frames"


CHARACTERS = (
    Character("player", "Player (no hat)", "Assets/Art/Characters/Player"),
    Character("older_sister", "Older Sister", "Assets/Art/Characters/OlderSister"),
    Character("father", "Father", "Assets/Art/Characters/Father"),
    Character("mother", "Mother", "Assets/Art/Characters/Mother"),
    Character("kim_seoa", "Kim Seoa", "Assets/Art/Characters/Employees/KimSeoa"),
    Character("lee_jian", "Lee Jian", "Assets/Art/Characters/Employees/LeeJian"),
    Character("choi_iseo", "Choi Iseo", "Assets/Art/Characters/Employees/ChoiIseo"),
    Character("jung_arin", "Jung Arin", "Assets/Art/Characters/Employees/JungArin"),
    Character("park_haeun", "Park Haeun", "Assets/Art/Characters/Employees/ParkHaeun"),
    Character("han_sua", "Han Sua", "Assets/Art/Characters/Employees/HanSua"),
    Character("oh_jiwoo", "Oh Jiwoo", "Assets/Art/Characters/Employees/OhJiwoo"),
    Character("yoon_chaea", "Yoon Chaea", "Assets/Art/Characters/Employees/YoonChaea"),
)

FAMILY_CHARACTERS = CHARACTERS[:4]
PHASE_COUNT = 6


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


def frame_path(repo_root: Path, character: Character, direction: str, phase: int) -> Path:
    return character.frame_folder(repo_root) / f"{character.character_id}_{direction}_walk_{phase}.png"


def render_sprite(path: Path, size: int) -> Image.Image:
    with Image.open(path) as loaded:
        sprite = loaded.convert("RGBA")
    return sprite.resize((size, size), Image.Resampling.NEAREST)


def inspect_frame(path: Path) -> dict[str, Any]:
    with Image.open(path) as loaded:
        sprite = loaded.convert("RGBA")
    width, height = sprite.size
    alpha = sprite.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError(f"Empty alpha channel: {path}")
    left, top, right_exclusive, bottom_exclusive = bbox
    right = right_exclusive - 1
    bottom = bottom_exclusive - 1
    center_x = (left + right) * 0.5
    body_height = max(1, bottom - top + 1)
    pelvis_y = top + body_height * 0.56
    foot_band_top = top + body_height * 0.78
    alpha_pixels = alpha.load()
    left_foot: list[tuple[int, int]] = []
    right_foot: list[tuple[int, int]] = []
    for y in range(max(top, int(foot_band_top)), bottom_exclusive):
        for x in range(left, right_exclusive):
            if alpha_pixels[x, y] == 0:
                continue
            (left_foot if x <= center_x else right_foot).append((x, y))

    def centroid(points: list[tuple[int, int]], fallback_x: float) -> list[float]:
        if not points:
            return [round(fallback_x, 2), float(bottom)]
        return [
            round(sum(point[0] for point in points) / len(points), 2),
            round(sum(point[1] for point in points) / len(points), 2),
        ]

    return {
        "size": [width, height],
        "alphaBounds": [left, top, right, bottom],
        "rootProxy": [round(center_x, 2), float(bottom)],
        "pelvisProxy": [round(center_x, 2), round(pelvis_y, 2)],
        "leftFootProxy": centroid(left_foot, center_x - body_height * 0.08),
        "rightFootProxy": centroid(right_foot, center_x + body_height * 0.08),
    }


def scale_point(point: list[float], tile: int) -> tuple[int, int]:
    return int(round(point[0] * tile / 256.0)), int(round(point[1] * tile / 256.0))


def draw_anchor_proxies(
    canvas: Image.Image,
    metrics: dict[str, Any],
    x: int,
    y: int,
    tile: int,
) -> None:
    draw = ImageDraw.Draw(canvas)
    points = (
        ("rootProxy", (255, 76, 76), 3),
        ("pelvisProxy", (75, 210, 255), 3),
        ("leftFootProxy", (106, 239, 125), 2),
        ("rightFootProxy", (255, 210, 74), 2),
    )
    for key, color, radius in points:
        point_x, point_y = scale_point(metrics[key], tile)
        point_x += x
        point_y += y
        draw.ellipse(
            (point_x - radius, point_y - radius, point_x + radius, point_y + radius),
            fill=color,
            outline=(20, 20, 20),
        )


def draw_tile(canvas: Image.Image, sprite: Image.Image, x: int, y: int, size: int) -> None:
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((x, y, x + size - 1, y + size - 1), 8, fill=(239, 232, 218), outline=(86, 72, 70), width=2)
    canvas.alpha_composite(sprite, (x, y))


def build_direction_contact(repo_root: Path, output: Path) -> None:
    tile = 96
    label_width = 154
    header = 40
    width = label_width + tile * len(DIRECTIONS)
    height = header + tile * len(CHARACTERS)
    canvas = Image.new("RGBA", (width, height), (29, 34, 45, 255))
    draw = ImageDraw.Draw(canvas)
    small_font = load_font(12)
    label_font = load_font(16)
    for column, direction in enumerate(DIRECTIONS):
        draw.text((label_width + column * tile + 5, 12), direction.upper(), font=small_font, fill=(245, 238, 222))
    for row, character in enumerate(CHARACTERS):
        y = header + row * tile
        draw.text((10, y + 38), character.label, font=label_font, fill=(245, 238, 222))
        for column, direction in enumerate(DIRECTIONS):
            sprite = render_sprite(frame_path(repo_root, character, direction, 2), tile)
            draw_tile(canvas, sprite, label_width + column * tile, y, tile)
    canvas.convert("RGB").save(output / "all_characters_8direction_contact_v1.png", quality=95)


def build_south_walk_gif(repo_root: Path, output: Path) -> None:
    tile = 144
    label_height = 24
    columns = 4
    rows = 3
    frames: list[Image.Image] = []
    font = load_font(15)
    for phase in range(6):
        canvas = Image.new("RGBA", (columns * tile, rows * (tile + label_height)), (29, 34, 45, 255))
        draw = ImageDraw.Draw(canvas)
        for index, character in enumerate(CHARACTERS):
            column = index % columns
            row = index // columns
            x = column * tile
            y = row * (tile + label_height)
            sprite = render_sprite(frame_path(repo_root, character, "south", phase), tile)
            draw_tile(canvas, sprite, x, y, tile)
            draw.text((x + 7, y + tile + 3), character.label, font=font, fill=(245, 238, 222))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE))
    frames[0].save(
        output / "all_characters_south_walk6_preview_v1.gif",
        save_all=True,
        append_images=frames[1:],
        duration=110,
        loop=0,
        disposal=2,
    )


def build_player_direction_gif(repo_root: Path, output: Path) -> None:
    tile = 144
    label_height = 24
    columns = 4
    rows = 2
    frames: list[Image.Image] = []
    font = load_font(15)
    player = CHARACTERS[0]
    for phase in range(6):
        canvas = Image.new("RGBA", (columns * tile, rows * (tile + label_height)), (29, 34, 45, 255))
        draw = ImageDraw.Draw(canvas)
        for index, direction in enumerate(DIRECTIONS):
            column = index % columns
            row = index // columns
            x = column * tile
            y = row * (tile + label_height)
            sprite = render_sprite(frame_path(repo_root, player, direction, phase), tile)
            draw_tile(canvas, sprite, x, y, tile)
            draw.text((x + 7, y + tile + 3), direction.upper(), font=font, fill=(245, 238, 222))
        frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE))
    frames[0].save(
        output / "player_no_hat_walk8direction_preview_v1.gif",
        save_all=True,
        append_images=frames[1:],
        duration=110,
        loop=0,
        disposal=2,
    )


def build_family_full_contact(
    repo_root: Path,
    output: Path,
    frame_metrics: dict[str, dict[str, Any]],
) -> None:
    tile = 112
    label_width = 148
    header = 34
    row_label_height = 18
    family_sheets: list[Image.Image] = []
    header_font = load_font(12)
    label_font = load_font(15)
    phase_font = load_font(11)
    for character in FAMILY_CHARACTERS:
        width = label_width + tile * len(DIRECTIONS)
        height = header + (tile + row_label_height) * PHASE_COUNT
        canvas = Image.new("RGBA", (width, height), (29, 34, 45, 255))
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 8), character.label, font=label_font, fill=(245, 238, 222))
        for column, direction in enumerate(DIRECTIONS):
            draw.text(
                (label_width + column * tile + 5, 10),
                direction.upper(),
                font=header_font,
                fill=(245, 238, 222),
            )
        for phase in range(PHASE_COUNT):
            y = header + phase * (tile + row_label_height)
            draw.text((10, y + tile // 2 - 8), f"PHASE {phase}", font=label_font, fill=(245, 238, 222))
            for column, direction in enumerate(DIRECTIONS):
                path = frame_path(repo_root, character, direction, phase)
                key = str(path.relative_to(repo_root)).replace("\\", "/")
                x = label_width + column * tile
                sprite = render_sprite(path, tile)
                draw_tile(canvas, sprite, x, y, tile)
                draw_anchor_proxies(canvas, frame_metrics[key], x, y, tile)
                draw.text((x + 5, y + tile + 2), f"{direction} / {phase}", font=phase_font, fill=(245, 238, 222))
        rgb = canvas.convert("RGB")
        rgb.save(output / f"{character.character_id}_walk_8x6_anchor_contact_v1.png", quality=95)
        family_sheets.append(rgb)

    combined = Image.new(
        "RGB",
        (max(sheet.width for sheet in family_sheets), sum(sheet.height for sheet in family_sheets)),
        (29, 34, 45),
    )
    y = 0
    for sheet in family_sheets:
        combined.paste(sheet, (0, y))
        y += sheet.height
    combined.save(output / "family_4x8x6_anchor_contact_v1.png", quality=95)


def build_family_direction_gifs(repo_root: Path, output: Path) -> None:
    tile = 144
    label_height = 24
    columns = 4
    rows = 2
    font = load_font(15)
    for character in FAMILY_CHARACTERS:
        frames: list[Image.Image] = []
        for phase in range(PHASE_COUNT):
            canvas = Image.new("RGBA", (columns * tile, rows * (tile + label_height)), (29, 34, 45, 255))
            draw = ImageDraw.Draw(canvas)
            for index, direction in enumerate(DIRECTIONS):
                column = index % columns
                row = index // columns
                x = column * tile
                y = row * (tile + label_height)
                sprite = render_sprite(frame_path(repo_root, character, direction, phase), tile)
                draw_tile(canvas, sprite, x, y, tile)
                draw.text((x + 7, y + tile + 3), direction.upper(), font=font, fill=(245, 238, 222))
            frames.append(canvas.convert("P", palette=Image.Palette.ADAPTIVE))
        frames[0].save(
            output / f"{character.character_id}_walk_8direction_preview_v1.gif",
            save_all=True,
            append_images=frames[1:],
            duration=110,
            loop=0,
            disposal=2,
        )


def build_family_metrics(repo_root: Path, output: Path) -> dict[str, dict[str, Any]]:
    frame_metrics: dict[str, dict[str, Any]] = {}
    summary: dict[str, Any] = {"schemaVersion": 1, "frameCount": 0, "characters": {}}
    failures: list[str] = []
    warnings: list[str] = []
    for character in FAMILY_CHARACTERS:
        character_summary: dict[str, Any] = {}
        for direction in DIRECTIONS:
            direction_frames: list[dict[str, Any]] = []
            for phase in range(PHASE_COUNT):
                path = frame_path(repo_root, character, direction, phase)
                if not path.is_file():
                    failures.append(f"missing:{path}")
                    continue
                metrics = inspect_frame(path)
                if metrics["size"] != [256, 256]:
                    failures.append(f"invalid-size:{path}:{metrics['size']}")
                key = str(path.relative_to(repo_root)).replace("\\", "/")
                frame_metrics[key] = metrics
                direction_frames.append({"phase": phase, "asset": key, **metrics})
            if len(direction_frames) != PHASE_COUNT:
                continue
            root_xs = [frame["rootProxy"][0] for frame in direction_frames]
            root_ys = [frame["rootProxy"][1] for frame in direction_frames]
            pelvis_xs = [frame["pelvisProxy"][0] for frame in direction_frames]
            pelvis_ys = [frame["pelvisProxy"][1] for frame in direction_frames]
            ranges = {
                "rootXRangePx": round(max(root_xs) - min(root_xs), 2),
                "rootYRangePx": round(max(root_ys) - min(root_ys), 2),
                "pelvisXRangePx": round(max(pelvis_xs) - min(pelvis_xs), 2),
                "pelvisYRangePx": round(max(pelvis_ys) - min(pelvis_ys), 2),
            }
            if ranges["rootYRangePx"] > 12 or ranges["pelvisYRangePx"] > 18:
                warnings.append(f"review-jitter:{character.character_id}:{direction}:{ranges}")
            character_summary[direction] = {"ranges": ranges, "frames": direction_frames}
        summary["characters"][character.character_id] = character_summary
    summary["frameCount"] = len(frame_metrics)
    summary["failures"] = failures
    summary["warnings"] = warnings
    (output / "family_walk_192_frame_metrics_v1.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    if failures:
        raise RuntimeError("; ".join(failures))
    if len(frame_metrics) != len(FAMILY_CHARACTERS) * len(DIRECTIONS) * PHASE_COUNT:
        raise RuntimeError(f"Expected 192 family frames, measured {len(frame_metrics)}")
    return frame_metrics


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    frame_metrics = build_family_metrics(repo_root, output)
    build_direction_contact(repo_root, output)
    build_south_walk_gif(repo_root, output)
    build_player_direction_gif(repo_root, output)
    build_family_full_contact(repo_root, output, frame_metrics)
    build_family_direction_gifs(repo_root, output)
    print(f"HIGH_MOTION_QA: PASS familyFrames={len(frame_metrics)} output={output}")


if __name__ == "__main__":
    main()
