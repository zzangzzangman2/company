from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


GREEN = (0, 255, 0, 255)
FRAME_NAMES = [f"player_east_walk_{index}_v2.png" for index in range(6)]
REJECTED_REASON = (
    "V13 is rejected research: its gait was invented from image guides instead of tracing "
    "KShopGo/Mixamo samples, and it does not preserve the approved protagonist lower-body style."
)


def make_contact_guide(base_guide: Path, output_path: Path) -> None:
    """Create a two-cell guide for an east-facing opposite-foot heel strike."""
    sheet = Image.open(base_guide).convert("RGBA")
    if sheet.size != (512, 256):
        raise ValueError(f"Expected a 512x256 guide, got {sheet.size}")

    draw = ImageDraw.Draw(sheet)
    cell_x = 256

    # Preserve the right-cell upper body exactly and replace only the lower guide.
    draw.rectangle((cell_x, 171, 511, 255), fill=GREEN)

    # Back/support leg: nearly straight and trailing to screen-left.
    support = [(cell_x + 120, 176), (cell_x + 109, 201), (cell_x + 103, 223)]
    draw.line(support, fill=(0, 226, 255, 255), width=9, joint="curve")
    draw.ellipse((cell_x + 116, 172, cell_x + 124, 180), fill=(0, 226, 255, 255))
    draw.ellipse((cell_x + 105, 197, cell_x + 113, 205), fill=(0, 226, 255, 255))
    draw.polygon(
        [
            (cell_x + 99, 220),
            (cell_x + 108, 220),
            (cell_x + 122, 228),
            (cell_x + 122, 233),
            (cell_x + 99, 233),
        ],
        fill=(245, 248, 250, 255),
    )

    # Front/landing leg: knee and toe remain east of the pelvis; heel meets floor.
    landing = [(cell_x + 134, 176), (cell_x + 148, 201), (cell_x + 151, 222)]
    draw.line(landing, fill=(255, 166, 0, 255), width=9, joint="curve")
    draw.ellipse((cell_x + 130, 172, cell_x + 138, 180), fill=(255, 166, 0, 255))
    draw.ellipse((cell_x + 144, 197, cell_x + 152, 205), fill=(255, 166, 0, 255))
    draw.polygon(
        [
            (cell_x + 147, 219),
            (cell_x + 156, 219),
            (cell_x + 174, 228),
            (cell_x + 174, 233),
            (cell_x + 147, 233),
        ],
        fill=(245, 248, 250, 255),
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def key_chroma_green(image: Image.Image) -> Image.Image:
    """Recover RGBA from a sprite rendered over chroma green."""
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    red = rgb[:, :, 0]
    green = rgb[:, :, 1]
    blue = rgb[:, :, 2]

    # With a #00ff00 backing color, one of R, B, or (255-G) estimates coverage.
    raw_alpha = np.maximum.reduce((red, blue, 255.0 - green)) / 255.0
    alpha = np.clip((raw_alpha - 0.025) / 0.95, 0.0, 1.0)
    # Image generation leaves a faint 8-10% green-screen texture after resize.
    # Remove it while retaining meaningful antialias coverage on the sprite.
    alpha[alpha < 0.14] = 0.0
    safe = np.maximum(raw_alpha, 1.0 / 255.0)

    recovered = np.empty_like(rgb)
    recovered[:, :, 0] = red / safe
    recovered[:, :, 2] = blue / safe
    recovered[:, :, 1] = (green - (1.0 - raw_alpha) * 255.0) / safe
    recovered = np.clip(recovered, 0.0, 255.0)

    rgba = np.dstack((recovered, alpha * 255.0)).round().astype(np.uint8)
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def connected_components(binary: np.ndarray) -> list[list[tuple[int, int]]]:
    height, width = binary.shape
    seen = np.zeros_like(binary, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    for y in range(height):
        for x in range(width):
            if not binary[y, x] or seen[y, x]:
                continue
            seen[y, x] = True
            queue: deque[tuple[int, int]] = deque([(x, y)])
            pixels: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                pixels.append((px, py))
                for oy in (-1, 0, 1):
                    for ox in (-1, 0, 1):
                        if ox == 0 and oy == 0:
                            continue
                        nx, ny = px + ox, py + oy
                        if (
                            0 <= nx < width
                            and 0 <= ny < height
                            and binary[ny, nx]
                            and not seen[ny, nx]
                        ):
                            seen[ny, nx] = True
                            queue.append((nx, ny))
            components.append(pixels)
    return components


def lower_body_mask(image: Image.Image, cut_y: int = 171) -> np.ndarray:
    alpha = np.asarray(image.convert("RGBA"))[:, :, 3]
    binary = alpha >= 32
    binary[:cut_y, :] = False
    mask = np.zeros_like(binary, dtype=bool)
    for component in connected_components(binary):
        if len(component) < 20:
            continue
        max_y = max(y for _, y in component)
        if max_y < 218:
            continue
        for x, y in component:
            mask[y, x] = True
    return mask


def translate_rgba(image: Image.Image, dx: int, dy: int) -> Image.Image:
    translated = Image.new("RGBA", image.size, (0, 0, 0, 0))
    translated.alpha_composite(image, (dx, dy))
    return translated


def translate_mask(mask: np.ndarray, dx: int, dy: int) -> np.ndarray:
    result = np.zeros_like(mask)
    height, width = mask.shape
    source_x0 = max(0, -dx)
    source_y0 = max(0, -dy)
    source_x1 = min(width, width - dx)
    source_y1 = min(height, height - dy)
    target_x0 = source_x0 + dx
    target_y0 = source_y0 + dy
    target_x1 = source_x1 + dx
    target_y1 = source_y1 + dy
    if source_x1 > source_x0 and source_y1 > source_y0:
        result[target_y0:target_y1, target_x0:target_x1] = mask[
            source_y0:source_y1, source_x0:source_x1
        ]
    return result


def waist_center(mask: np.ndarray, y0: int = 171, y1: int = 180) -> float:
    ys, xs = np.nonzero(mask[y0:y1, :])
    if len(xs) == 0:
        raise ValueError("No lower-body pixels at waist seam")
    return float(xs.mean())


def compose_lower(
    source_frame: Image.Image,
    donor: Image.Image,
    *,
    cut_y: int = 171,
) -> tuple[Image.Image, dict[str, float | int]]:
    source_frame = source_frame.convert("RGBA")
    donor = donor.convert("RGBA")
    source_mask = lower_body_mask(source_frame, cut_y)
    donor_mask = lower_body_mask(donor, cut_y)

    source_center = waist_center(source_mask)
    donor_center = waist_center(donor_mask)
    dx = int(round(source_center - donor_center))
    translated_donor = translate_rgba(donor, dx, 0)
    translated_mask = translate_mask(donor_mask, dx, 0)

    source_pixels = np.asarray(source_frame).copy()
    output_pixels = source_pixels.copy()
    output_pixels[source_mask] = 0
    donor_pixels = np.asarray(translated_donor)
    output_pixels[translated_mask] = donor_pixels[translated_mask]

    # The original coat, sleeve, and hands own the foreground z-order.
    protected = (source_pixels[:, :, 3] > 0) & ~source_mask
    output_pixels[protected] = source_pixels[protected]
    output = Image.fromarray(output_pixels, "RGBA")
    return output, {
        "sourceWaistCenter": round(source_center, 3),
        "donorWaistCenter": round(donor_center, 3),
        "donorDx": dx,
    }


def alpha_bottom(image: Image.Image) -> int:
    alpha = np.asarray(image.convert("RGBA"))[:, :, 3]
    ys = np.nonzero(alpha >= 8)[0]
    if len(ys) == 0:
        raise ValueError("Empty frame")
    return int(ys.max())


def flatten(image: Image.Image, color: tuple[int, int, int, int]) -> Image.Image:
    background = Image.new("RGBA", image.size, color)
    background.alpha_composite(image.convert("RGBA"))
    return background.convert("RGB")


def write_contact_sheet(
    frames: list[Image.Image], output_path: Path, background: tuple[int, int, int, int]
) -> None:
    sheet = Image.new("RGBA", (256 * 3, 256 * 2), background)
    for index, frame in enumerate(frames):
        x = (index % 3) * 256
        y = (index // 3) * 256
        sheet.alpha_composite(frame.convert("RGBA"), (x, y))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def write_feet_sheet(frames: list[Image.Image], output_path: Path) -> None:
    crop_box = (72, 164, 184, 244)
    scale = 3
    crop_width = (crop_box[2] - crop_box[0]) * scale
    crop_height = (crop_box[3] - crop_box[1]) * scale
    sheet = Image.new("RGB", (crop_width * 3, crop_height * 2), (0, 0, 0))
    for index, frame in enumerate(frames):
        crop = flatten(frame, (0, 0, 0, 255)).crop(crop_box)
        crop = crop.resize((crop_width, crop_height), Image.Resampling.NEAREST)
        sheet.paste(crop, ((index % 3) * crop_width, (index // 3) * crop_height))
    sheet.save(output_path)


def crop_generated_right_cell(sheet_path: Path) -> Image.Image:
    sheet = Image.open(sheet_path).convert("RGB")
    if sheet.size != (512, 256):
        raise ValueError(f"Expected 512x256 generated sheet, got {sheet.size}")
    return key_chroma_green(sheet.crop((256, 0, 512, 256)))


def build_source_lower_palette(source_frames: list[Image.Image], colors: int = 48) -> np.ndarray:
    samples: list[np.ndarray] = []
    for frame in source_frames[:3]:
        rgba = np.asarray(frame.convert("RGBA"))
        mask = lower_body_mask(frame)
        samples.append(rgba[mask, :3])
    pixels = np.concatenate(samples, axis=0).astype(np.uint8)
    strip = Image.fromarray(pixels.reshape((1, len(pixels), 3)), "RGB")
    quantized = strip.quantize(
        colors=colors,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    )
    used = sorted(set(np.asarray(quantized).ravel().tolist()))
    flat_palette = quantized.getpalette()
    return np.array(
        [flat_palette[index * 3 : index * 3 + 3] for index in used], dtype=np.int16
    )


def normalize_authored_lower(
    donor: Image.Image,
    palette: np.ndarray,
    *,
    x_scale: float,
    cut_y: int = 171,
    target_bottom: int = 233,
) -> Image.Image:
    donor = donor.convert("RGBA")
    donor_pixels = np.asarray(donor).copy()
    donor_mask = lower_body_mask(donor, cut_y)
    isolated = np.zeros_like(donor_pixels)
    isolated[donor_mask] = donor_pixels[donor_mask]
    isolated_image = Image.fromarray(isolated, "RGBA")

    current_bottom = alpha_bottom(isolated_image)
    lower_crop = isolated_image.crop((0, cut_y, 256, current_bottom + 1))
    target_height = target_bottom - cut_y + 1
    if lower_crop.height != target_height:
        lower_crop = lower_crop.resize(
            (256, target_height), Image.Resampling.LANCZOS
        )
    normalized = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    normalized.alpha_composite(lower_crop, (0, cut_y))

    if abs(x_scale - 1.0) > 1e-6:
        scaled_width = int(round(256 * x_scale))
        scaled = normalized.resize((scaled_width, 256), Image.Resampling.LANCZOS)
        x = int(round((256 - scaled_width) / 2.0))
        centered = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        centered.alpha_composite(scaled, (x, 0))
        normalized = centered

    rgba = np.asarray(normalized).copy()
    opaque = rgba[:, :, 3] >= 96
    colors = rgba[opaque, :3].astype(np.int16)
    if len(colors):
        distances = ((colors[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2)
        rgba[opaque, :3] = palette[np.argmin(distances, axis=1)].astype(np.uint8)
    rgba[:, :, 3] = np.where(opaque, 255, 0).astype(np.uint8)
    rgba[~opaque, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def build_candidate(
    source_dir: Path,
    contact_sheet: Path,
    passing_sheet: Path,
    output_dir: Path,
) -> None:
    source_frames = [
        Image.open(source_dir / name).convert("RGBA") for name in FRAME_NAMES
    ]
    source_palette = build_source_lower_palette(source_frames)
    contact = normalize_authored_lower(
        crop_generated_right_cell(contact_sheet), source_palette, x_scale=0.82
    )
    passing = normalize_authored_lower(
        crop_generated_right_cell(passing_sheet), source_palette, x_scale=1.0
    )

    normalized_dir = output_dir / "NormalizedDonors"
    normalized_dir.mkdir(parents=True, exist_ok=True)
    contact.save(normalized_dir / "authored-contact.png")
    passing.save(normalized_dir / "authored-pass.png")

    # Physical gait order: clean A-contact -> clean pass -> authored B-contact
    # -> held B-contact -> authored opposite pass -> clean A-contact.
    donors = [source_frames[0], source_frames[1], contact, contact, passing, source_frames[2]]
    frames: list[Image.Image] = []
    metrics: list[dict[str, object]] = []
    for index, (source, donor) in enumerate(zip(source_frames, donors)):
        frame, alignment = compose_lower(source, donor)
        before_ground = alpha_bottom(frame)
        ground_dy = 233 - before_ground
        frame = translate_rgba(frame, 0, ground_dy)
        frames.append(frame)
        metrics.append(
            {
                "phase": index,
                "lowerDonor": ["v3-p0", "v3-p1", "authored-contact", "authored-contact", "authored-pass", "v3-p2"][index],
                "bottomBeforeGround": before_ground,
                "wholeFrameGroundDy": ground_dy,
                "bottomAfterGround": alpha_bottom(frame),
                **alignment,
            }
        )

    frames_dir = output_dir / "Frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    for name, frame in zip(FRAME_NAMES, frames):
        frame.save(frames_dir / name)

    write_contact_sheet(frames, output_dir / "contact-sheet-transparent.png", (0, 0, 0, 0))
    write_contact_sheet(frames, output_dir / "contact-sheet-black.png", (0, 0, 0, 255))
    write_contact_sheet(frames, output_dir / "contact-sheet-green.png", GREEN)
    write_feet_sheet(frames, output_dir / "feet-close-black-3x.png")
    black_frames = [flatten(frame, (0, 0, 0, 255)) for frame in frames]
    black_frames[0].save(
        output_dir / "east-walk-black.gif",
        save_all=True,
        append_images=black_frames[1:],
        duration=125,
        loop=0,
        disposal=2,
    )
    frames[0].save(
        output_dir / "east-walk-transparent.gif",
        save_all=True,
        append_images=frames[1:],
        duration=125,
        loop=0,
        disposal=2,
    )

    receipt = {
        "contract": "PLAYER-EAST-AUTHORED-LOWER-V13-CANDIDATE",
        "status": "REJECTED_RESEARCH",
        "rejectedReason": REJECTED_REASON,
        "sourceFrames": str(source_dir),
        "authoredContactSheet": str(contact_sheet),
        "authoredPassingSheet": str(passing_sheet),
        "generatedImageUsedInShippingFrames": False,
        "upperBodyPolicy": "source phase retained; only connected lower-body components at y>=171 are replaced",
        "groundY": 233,
        "phaseMetrics": metrics,
    }
    (output_dir / "receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--make-contact-guide", action="store_true")
    parser.add_argument("--base-guide", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--source-dir", type=Path)
    parser.add_argument("--contact-sheet", type=Path)
    parser.add_argument("--passing-sheet", type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--allow-rejected-research", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.make_contact_guide:
        if args.base_guide is None or args.output is None:
            raise SystemExit("--base-guide and --output are required")
        make_contact_guide(args.base_guide, args.output)
        return
    if args.build:
        if not args.allow_rejected_research:
            raise SystemExit(REJECTED_REASON)
        required = (args.source_dir, args.contact_sheet, args.passing_sheet, args.output_dir)
        if any(path is None for path in required):
            raise SystemExit(
                "--source-dir, --contact-sheet, --passing-sheet, and --output-dir are required"
            )
        build_candidate(
            args.source_dir,
            args.contact_sheet,
            args.passing_sheet,
            args.output_dir,
        )
        return
    raise SystemExit("No operation selected")


if __name__ == "__main__":
    main()
