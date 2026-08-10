#!/usr/bin/env python3
"""Build compact visual QA previews for the 8-direction / 6-frame sprites."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path

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


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    build_direction_contact(repo_root, output)
    build_south_walk_gif(repo_root, output)
    build_player_direction_gif(repo_root, output)
    print(f"HIGH_MOTION_QA: PASS output={output}")


if __name__ == "__main__":
    main()
