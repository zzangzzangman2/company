"""Build human-facing V2 comparison sheets and GIFs from four Blender outputs.

The script does not alter Unity assets.  It pairs each runtime 2D P0 frame with
the clean-room 3D front render and assembles individual plus four-role turntable
GIFs for the user's visual approval gate.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROLE_ORDER = ("Player", "Father", "Mother", "Older Sister")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--player-dir", required=True, type=Path)
    parser.add_argument("--father-dir", required=True, type=Path)
    parser.add_argument("--mother-dir", required=True, type=Path)
    parser.add_argument("--sister-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = (
        Path("C:/Windows/Fonts/malgunbd.ttf" if bold else "C:/Windows/Fonts/malgun.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
    )
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def numeric_key(path: Path) -> tuple[int, str]:
    matches = re.findall(r"(\d+)", path.stem)
    return (int(matches[-1]) if matches else -1, path.as_posix().lower())


def find_front(root: Path) -> Path:
    candidates = [
        path
        for path in root.rglob("*.png")
        if "front" in path.name.lower() and "turntable" not in path.as_posix().lower()
    ]
    if not candidates:
        raise FileNotFoundError(f"No front render found below {root}")
    candidates.sort(key=lambda path: ("final" not in path.name.lower(), "draft" in path.name.lower(), len(path.as_posix())))
    return candidates[0]


def find_turntable(root: Path) -> list[Path]:
    candidates = [
        path
        for path in root.rglob("*.png")
        if "turntable" in path.parent.name.lower()
    ]
    candidates.sort(key=numeric_key)
    if len(candidates) < 8:
        raise FileNotFoundError(f"Expected at least 8 turntable frames below {root}; found {len(candidates)}")
    return candidates


def alpha_trim(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = rgba.getchannel("A").getbbox()
    return rgba.crop(bbox) if bbox else rgba


def runtime_p0(sheet_path: Path) -> Image.Image:
    sheet = Image.open(sheet_path).convert("RGBA")
    cell_w = sheet.width // 6
    cell_h = sheet.height // 4
    return alpha_trim(sheet.crop((0, 0, cell_w, cell_h)))


def contain(
    image: Image.Image,
    size: tuple[int, int],
    background: tuple[int, int, int, int],
    *,
    allow_upscale: bool = False,
    resample: Image.Resampling = Image.Resampling.LANCZOS,
) -> Image.Image:
    source = image.convert("RGBA")
    scale = min(size[0] / source.width, size[1] / source.height)
    if not allow_upscale:
        scale = min(scale, 1.0)
    resized = (
        max(1, round(source.width * scale)),
        max(1, round(source.height * scale)),
    )
    source = source.resize(resized, resample)
    canvas = Image.new("RGBA", size, background)
    x = (size[0] - source.width) // 2
    y = (size[1] - source.height) // 2
    canvas.alpha_composite(source, (x, y))
    return canvas


def role_panel(label: str, reference: Image.Image, render: Image.Image) -> Image.Image:
    width, height = 800, 590
    panel = Image.new("RGBA", (width, height), (34, 39, 50, 255))
    draw = ImageDraw.Draw(panel)
    draw.rounded_rectangle((8, 8, width - 8, height - 8), radius=22, fill=(42, 48, 61, 255), outline=(91, 107, 133, 255), width=2)
    draw.text((30, 20), label, font=font(30, True), fill=(248, 250, 255, 255))
    draw.text((108, 63), "Runtime 2D", font=font(19), fill=(172, 190, 220, 255), anchor="mm")
    draw.text((522, 63), "New 3D candidate", font=font(19), fill=(172, 190, 220, 255), anchor="mm")
    reference_box = contain(
        reference,
        (300, 475),
        (27, 31, 40, 255),
        allow_upscale=True,
        resample=Image.Resampling.NEAREST,
    )
    render_box = contain(render, (430, 475), (27, 31, 40, 255))
    panel.alpha_composite(reference_box, (26, 88))
    panel.alpha_composite(render_box, (344, 88))
    return panel


def gif_panel(label: str, frame: Image.Image, size: tuple[int, int]) -> Image.Image:
    panel = Image.new("RGBA", size, (34, 39, 50, 255))
    draw = ImageDraw.Draw(panel)
    draw.text((size[0] // 2, 24), label, font=font(24, True), fill=(248, 250, 255, 255), anchor="mm")
    inner = contain(frame, (size[0] - 20, size[1] - 56), (27, 31, 40, 255))
    panel.alpha_composite(inner, (10, 46))
    return panel


def palettize(frame: Image.Image) -> Image.Image:
    return frame.convert("RGB").quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)


def save_gif(frames: list[Image.Image], path: Path, duration: int = 105) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    converted = [palettize(frame) for frame in frames]
    converted[0].save(
        path,
        save_all=True,
        append_images=converted[1:],
        duration=duration,
        loop=0,
        disposal=2,
        optimize=False,
    )


def main() -> None:
    args = parse_args()
    repo = args.repo.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    role_dirs = {
        "Player": args.player_dir.resolve(),
        "Father": args.father_dir.resolve(),
        "Mother": args.mother_dir.resolve(),
        "Older Sister": args.sister_dir.resolve(),
    }
    sheets = {
        "Player": repo / "Assets/Art/Characters/Player/Pixel/HighMotion/player_pixel_walk8dir6_a_v1.png",
        "Father": repo / "Assets/Art/Characters/Father/Pixel/HighMotion/father_pixel_walk8dir6_a_v1.png",
        "Mother": repo / "Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_a_v1.png",
        "Older Sister": repo / "Assets/Art/Characters/OlderSister/Pixel/HighMotion/older_sister_pixel_walk8dir6_a_v1.png",
    }

    comparison_panels: list[Image.Image] = []
    turntables: dict[str, list[Image.Image]] = {}
    for role in ROLE_ORDER:
        reference = runtime_p0(sheets[role])
        front = Image.open(find_front(role_dirs[role])).convert("RGBA")
        comparison_panels.append(role_panel(role, reference, front))
        frames = [Image.open(path).convert("RGBA") for path in find_turntable(role_dirs[role])]
        turntables[role] = frames
        save_gif([gif_panel(role, frame, (480, 620)) for frame in frames], output / f"{role.lower().replace(' ', '-')}-runtime2d-v2-turntable.gif")

    comparison = Image.new("RGBA", (1600, 1180), (23, 27, 35, 255))
    comparison.alpha_composite(comparison_panels[0], (0, 0))
    comparison.alpha_composite(comparison_panels[1], (800, 0))
    comparison.alpha_composite(comparison_panels[2], (0, 590))
    comparison.alpha_composite(comparison_panels[3], (800, 590))
    comparison.convert("RGB").save(output / "family-runtime2d-v2-2d-vs-3d.png", quality=95)

    count = min(len(frames) for frames in turntables.values())
    combined_frames: list[Image.Image] = []
    cell_size = (390, 500)
    for index in range(count):
        canvas = Image.new("RGBA", (cell_size[0] * 2, cell_size[1] * 2), (23, 27, 35, 255))
        for role_index, role in enumerate(ROLE_ORDER):
            panel = gif_panel(role, turntables[role][index], cell_size)
            canvas.alpha_composite(panel, ((role_index % 2) * cell_size[0], (role_index // 2) * cell_size[1]))
        combined_frames.append(canvas)
    save_gif(combined_frames, output / "family-runtime2d-v2-four-role-turntable.gif", duration=115)

    print(f"FAMILY_RUNTIME2D_V2_REVIEW: PASS ({output})")


if __name__ == "__main__":
    main()
