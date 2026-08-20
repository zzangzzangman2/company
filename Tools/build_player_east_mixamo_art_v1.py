from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


GREEN = (0, 255, 0, 255)
FRAME_NAMES = [f"player_east_walk_{index}_v2.png" for index in range(6)]


def connected_components(binary: np.ndarray) -> list[list[tuple[int, int]]]:
    height, width = binary.shape
    seen = np.zeros_like(binary, dtype=bool)
    output: list[list[tuple[int, int]]] = []
    for y in range(height):
        for x in range(width):
            if not binary[y, x] or seen[y, x]:
                continue
            seen[y, x] = True
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                component.append((px, py))
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
            output.append(component)
    return output


def key_green(image: Image.Image) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    raw_alpha = np.maximum.reduce((red, blue, 255.0 - green)) / 255.0
    alpha = np.clip((raw_alpha - 0.025) / 0.95, 0.0, 1.0)
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


def lower_mask(image: Image.Image, cut_y: int = 171) -> np.ndarray:
    alpha = np.asarray(image.convert("RGBA"))[:, :, 3]
    binary = alpha >= 32
    binary[:cut_y] = False
    mask = np.zeros_like(binary)
    for component in connected_components(binary):
        if len(component) < 20 or max(y for _, y in component) < 215:
            continue
        for x, y in component:
            mask[y, x] = True
    return mask


def crop_six_sheet(path: Path) -> list[Image.Image]:
    sheet = Image.open(path).convert("RGB")
    if sheet.size != (1536, 1024):
        raise ValueError(f"Expected 1536x1024 six-sheet, got {sheet.size}")
    output = []
    for index in range(6):
        x = (index % 3) * 512
        y = (index // 3) * 512
        cell = sheet.crop((x, y, x + 512, y + 512)).resize(
            (256, 256), Image.Resampling.LANCZOS
        )
        output.append(key_green(cell))
    return output


def crop_two_sheet_right(path: Path) -> Image.Image:
    sheet = Image.open(path).convert("RGB")
    if sheet.width != sheet.height * 2:
        raise ValueError(f"Expected a 2:1 two-cell sheet, got {sheet.size}")
    cell = sheet.crop((sheet.height, 0, sheet.width, sheet.height)).resize(
        (256, 256), Image.Resampling.LANCZOS
    )
    return key_green(cell)


def source_palette(source_frames: list[Image.Image]) -> np.ndarray:
    samples: list[np.ndarray] = []
    for frame in source_frames:
        rgba = np.asarray(frame.convert("RGBA"))
        samples.append(rgba[lower_mask(frame), :3])
    return np.unique(np.concatenate(samples, axis=0), axis=0).astype(np.int32)


def normalize_lower(draft: Image.Image, palette: np.ndarray) -> Image.Image:
    rgba = np.asarray(draft.convert("RGBA")).copy()
    mask = lower_mask(draft)
    output = np.zeros_like(rgba)
    colors = rgba[mask, :3].astype(np.int32)
    if len(colors):
        # int32 is required: squaring int16 silently overflows and maps navy to beige.
        distances = ((colors[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2)
        output[mask, :3] = palette[np.argmin(distances, axis=1)].astype(np.uint8)
        output[mask, 3] = 255
    return Image.fromarray(output, "RGBA")


def write_sheet(frames: list[Image.Image], path: Path, background=GREEN) -> None:
    sheet = Image.new("RGBA", (768, 512), background)
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame.convert("RGBA"), ((index % 3) * 256, (index // 3) * 256))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def extract_drafts(
    source_dir: Path,
    six_sheet: Path,
    p1_sheet: Path,
    p2_sheet: Path,
    output_dir: Path,
) -> None:
    source_frames = [Image.open(source_dir / name).convert("RGBA") for name in FRAME_NAMES]
    palette = source_palette(source_frames)
    drafts = crop_six_sheet(six_sheet)
    drafts[1] = crop_two_sheet_right(p1_sheet)
    drafts[2] = crop_two_sheet_right(p2_sheet)
    normalized = [normalize_lower(frame, palette) for frame in drafts]

    draft_dir = output_dir / "DraftCells"
    normalized_dir = output_dir / "NormalizedLowerDrafts"
    draft_dir.mkdir(parents=True, exist_ok=True)
    normalized_dir.mkdir(parents=True, exist_ok=True)
    for index, (draft, lower) in enumerate(zip(drafts, normalized)):
        draft.save(draft_dir / f"p{index}.png")
        lower.save(normalized_dir / f"p{index}.png")
    write_sheet(drafts, output_dir / "generated-draft-cells.png")
    write_sheet(normalized, output_dir / "generated-normalized-lowers.png", (0, 0, 0, 255))

    metrics = []
    for index, lower in enumerate(normalized):
        alpha = np.asarray(lower)[:, :, 3] > 0
        ys, xs = np.nonzero(alpha)
        metrics.append(
            {
                "pose": index,
                "bbox": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                "bottom": int(ys.max()),
                "pixels": int(alpha.sum()),
            }
        )
    (output_dir / "draft-metrics.json").write_text(
        json.dumps(metrics, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--six-sheet", type=Path, required=True)
    parser.add_argument("--p1-sheet", type=Path, required=True)
    parser.add_argument("--p2-sheet", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    extract_drafts(
        args.source_dir,
        args.six_sheet,
        args.p1_sheet,
        args.p2_sheet,
        args.output_dir,
    )


if __name__ == "__main__":
    main()
