#!/usr/bin/env python3
"""Split and validate the approved four-character OfficeSeatingV1 sheets."""
from __future__ import annotations

import argparse
import hashlib
import re
import statistics
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


CELL = 256
FOOT_BASELINE = CELL - 8
DIRECTIONS = ("south", "southwest", "west", "northwest", "north", "northeast", "east", "southeast")
PART_DIRECTIONS = {
    "a": DIRECTIONS[:4],
    "b": DIRECTIONS[4:],
}
GUID_NAMESPACE = "family-company/office-seating-v1/"
APPROVED_FRAME_OVERRIDES = {
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_0.png":
        "1F8D8A299555DD50A8ACE551B8627141CFD1C017DFD0B01FE01D57B559E54FF7",
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_1.png":
        "0A2F1A778FE97246DE2B908BDF3FE7D6AC5DA2EBB27E522EC9D6F7C7CB204A00",
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_2.png":
        "695FAFF1B75AA79E062690640FAE3B47C827297DD20C73131D2D843EA6A392F4",
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_3.png":
        "63A06E819D07EFFFF9E8A2F06918494B05DE9CB1D96ECD8046A750ED3FA8B5EF",
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_4.png":
        "85C8BDAE178B7EA0AEEE0EA3AF6FF10CC1D2A03D1E082E31E87AD7A427B99541",
    "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames/mother_northwest_sit_work_5.png":
        "BF481EDDB0FB2CF354A90D6666AB386BB7CC09AC2DE8C081B70C4002A6482986",
}


@dataclass(frozen=True)
class CharacterSpec:
    character_id: str
    seating_root: str
    high_motion_root: str


CHARACTERS = (
    CharacterSpec("player", "Assets/Art/Characters/Player/Pixel/OfficeSeatingV1", "Assets/Art/Characters/Player/Pixel/HighMotion"),
    CharacterSpec("older_sister", "Assets/Art/Characters/Family/OlderSister/Pixel/OfficeSeatingV1", "Assets/Art/Characters/OlderSister/Pixel/HighMotion"),
    CharacterSpec("father", "Assets/Art/Characters/Family/Father/Pixel/OfficeSeatingV1", "Assets/Art/Characters/Father/Pixel/HighMotion"),
    CharacterSpec("mother", "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1", "Assets/Art/Characters/Mother/Pixel/HighMotion"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--verify-only", action="store_true")
    return parser.parse_args()


def stable_guid(repo: Path, path: Path) -> str:
    relative = path.relative_to(repo).as_posix()
    return hashlib.md5(f"{GUID_NAMESPACE}{relative}".encode("utf-8")).hexdigest()


def ensure_folder_meta(repo: Path, folder: Path) -> None:
    meta = Path(f"{folder}.meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {stable_guid(repo, folder)}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8",
        newline="\n",
    )


def frame_meta_text(repo: Path, frame: Path, template: str) -> str:
    guid_match = re.search(r"^guid: ([0-9a-f]{32})$", Path(f"{frame}.meta").read_text(encoding="utf-8"), re.M) if Path(f"{frame}.meta").exists() else None
    guid = guid_match.group(1) if guid_match else stable_guid(repo, frame)
    text = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", template, count=1, flags=re.M)
    replacements = {
        r"enableMipMap: \d+": "enableMipMap: 0",
        r"filterMode: \d+": "filterMode: 0",
        r"nPOTScale: \d+": "nPOTScale: 0",
        r"spriteMode: \d+": "spriteMode: 1",
        r"alignment: \d+": "alignment: 9",
        r"spritePivot: \{[^\n]+\}": "spritePivot: {x: 0.5, y: 0}",
        r"spritePixelsToUnits: [^\n]+": "spritePixelsToUnits: 180",
        r"alphaIsTransparency: \d+": "alphaIsTransparency: 1",
        r"maxTextureSize: \d+": "maxTextureSize: 256",
        r"textureCompression: \d+": "textureCompression: 0",
    }
    for pattern, replacement in replacements.items():
        text = re.sub(pattern, replacement, text)
    return text


def write_frame_meta(repo: Path, frame: Path, template: str) -> None:
    Path(f"{frame}.meta").write_text(frame_meta_text(repo, frame, template), encoding="utf-8", newline="\n")


def ensure_default_meta(repo: Path, asset: Path) -> None:
    meta = Path(f"{asset}.meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {stable_guid(repo, asset)}\n", encoding="utf-8", newline="\n")


def require_hard_alpha(image: Image.Image, label: str) -> None:
    values = {value for value, count in enumerate(image.getchannel("A").histogram()) if count}
    if not values <= {0, 255}:
        raise ValueError(f"{label}: partial alpha values {sorted(values - {0, 255})}")


def main_components(image: Image.Image, expected: int, label: str) -> list[tuple[int, int, int, int, float, float]]:
    rgba = np.asarray(image)
    count, _, stats, centroids = cv2.connectedComponentsWithStats((rgba[:, :, 3] > 0).astype(np.uint8), 8)
    components = []
    for index in range(1, count):
        if int(stats[index, cv2.CC_STAT_AREA]) < 1000:
            continue
        x, y, width, height = (int(value) for value in stats[index, :4])
        components.append((x, y, width, height, float(centroids[index][0]), float(centroids[index][1])))
    if len(components) != expected:
        raise ValueError(f"{label}: expected {expected} main silhouettes, found {len(components)}")
    return components


def extract_rows(path: Path, columns: int, rows: int) -> list[list[Image.Image]]:
    with Image.open(path) as loaded:
        image = loaded.convert("RGBA")
    expected_size = (columns * CELL, rows * CELL)
    if image.size != expected_size:
        raise ValueError(f"{path}: expected {expected_size}, found {image.size}")
    require_hard_alpha(image, str(path))
    components = main_components(image, columns * rows, str(path))
    components.sort(key=lambda item: item[5])
    result = []
    for row_index in range(rows):
        row = components[row_index * columns:(row_index + 1) * columns]
        row.sort(key=lambda item: item[4])
        if max(item[5] for item in row) - min(item[5] for item in row) > 80:
            raise ValueError(f"{path}: silhouettes do not form row {row_index}")
        crops = []
        for x, y, width, height, _, _ in row:
            padding = 8
            crops.append(image.crop((max(0, x-padding), max(0, y-padding), min(image.width, x+width+padding), min(image.height, y+height+padding))))
        result.append(crops)
    return result


def opaque_height(image: Image.Image) -> int:
    box = image.getchannel("A").getbbox()
    return box[3] - box[1] if box else 0


def normalize(image: Image.Image, scale: float, label: str) -> Image.Image:
    width, height = max(1, round(image.width * scale)), max(1, round(image.height * scale))
    resized = image.resize((width, height), Image.Resampling.NEAREST)
    require_hard_alpha(resized, label)
    alpha = np.asarray(resized.getchannel("A"))
    ys, xs = np.nonzero(alpha)
    if not len(xs):
        raise ValueError(f"{label}: empty frame")
    min_y, max_y = int(ys.min()), int(ys.max())
    upper_limit = min_y + max(1, int((max_y - min_y) * 0.55))
    anchor_x = int(round(float(np.median(xs[ys <= upper_limit]))))
    offset_x, offset_y = CELL // 2 - anchor_x, FOOT_BASELINE - max_y
    if offset_x + int(xs.min()) < 0 or offset_x + int(xs.max()) >= CELL or offset_y + min_y < 0:
        raise ValueError(f"{label}: scaled silhouette would be clipped")
    canvas = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    canvas.alpha_composite(resized, (offset_x, offset_y))
    box = canvas.getchannel("A").getbbox()
    if box is None or box[3] - 1 != FOOT_BASELINE:
        raise ValueError(f"{label}: invalid foot baseline {box}")
    return canvas


def rgba_hash(image: Image.Image) -> str:
    return hashlib.sha256(image.tobytes()).hexdigest()


def load_approved_override(repo: Path, path: Path) -> Image.Image | None:
    relative = path.relative_to(repo).as_posix()
    expected_sha = APPROVED_FRAME_OVERRIDES.get(relative)
    if expected_sha is None:
        return None
    if not path.is_file():
        raise FileNotFoundError(path)
    actual_sha = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    if actual_sha != expected_sha:
        raise ValueError(f"{path}: approved override SHA mismatch {actual_sha}")
    with Image.open(path) as loaded:
        image = loaded.convert("RGBA")
    require_hard_alpha(image, str(path))
    box = image.getchannel("A").getbbox()
    if image.size != (CELL, CELL) or box is None or box[3] != FOOT_BASELINE + 1 or box[3] - box[1] != 228:
        raise ValueError(f"{path}: approved override canvas/bounds failure {image.size} {box}")
    return image


def save_or_compare(path: Path, image: Image.Image, verify_only: bool) -> None:
    if verify_only:
        if not path.is_file():
            raise FileNotFoundError(path)
        with Image.open(path) as loaded:
            existing = loaded.convert("RGBA")
        if existing.tobytes() != image.tobytes():
            raise ValueError(f"{path}: does not match approved source split")
    else:
        image.save(path, format="PNG", compress_level=9)


def make_contact(frames: dict[tuple[str, str, int], Image.Image], clip: str) -> Image.Image:
    phases = 8 if clip == "transition" else 6
    canvas = Image.new("RGBA", (phases * 128, 8 * 128), (240, 246, 244, 255))
    for row, direction in enumerate(DIRECTIONS):
        keys = ([(direction, "sit_down", i) for i in range(4)] + [(direction, "stand_up", i) for i in range(4)]) if clip == "transition" else [(direction, "sit_work", i) for i in range(6)]
        for column, key in enumerate(keys):
            canvas.alpha_composite(frames[key].resize((128, 128), Image.Resampling.NEAREST), (column * 128, row * 128))
    return canvas


def make_work_gif(path: Path, frames: dict[tuple[str, str, int], Image.Image]) -> None:
    gif_frames = []
    for phase in range(6):
        canvas = Image.new("RGBA", (8 * 128, 128), (240, 246, 244, 255))
        for column, direction in enumerate(DIRECTIONS):
            canvas.alpha_composite(frames[(direction, "sit_work", phase)].resize((128, 128), Image.Resampling.NEAREST), (column * 128, 0))
        gif_frames.append(canvas.convert("RGB"))
    gif_frames[0].save(path, save_all=True, append_images=gif_frames[1:], duration=140, loop=0, disposal=2)


def validate_frame_meta(path: Path) -> None:
    text = Path(f"{path}.meta").read_text(encoding="utf-8")
    required = ("enableMipMap: 0", "filterMode: 0", "spriteMode: 1", "alignment: 9", "spritePivot: {x: 0.5, y: 0}", "spritePixelsToUnits: 180", "alphaIsTransparency: 1")
    if any(item not in text for item in required) or "textureCompression: 1" in text:
        raise ValueError(f"{path}.meta: importer contract failure")


def split_character(repo: Path, spec: CharacterSpec, verify_only: bool, template: str) -> tuple[int, dict[str, float]]:
    root, high_motion = repo/spec.seating_root, repo/spec.high_motion_root
    source, frame_dir, qa_dir = root/"Source", root/"Frames", root/"QA"
    if not verify_only:
        frame_dir.mkdir(parents=True, exist_ok=True)
        qa_dir.mkdir(parents=True, exist_ok=True)
        for folder in (root, source, frame_dir, qa_dir):
            ensure_folder_meta(repo, folder)
        for source_asset in source.glob("*.png"):
            ensure_default_meta(repo, source_asset)
    for source_asset in source.glob("*.png"):
        if not Path(f"{source_asset}.meta").is_file():
            raise FileNotFoundError(f"{source_asset}.meta")

    transition_rows, work_rows = [], []
    for part in ("a", "b"):
        transition_rows.extend(extract_rows(source/f"{spec.character_id}_office_seating_transition_{part}_v1.png", 4, 4))
        work_rows.extend(extract_rows(source/f"{spec.character_id}_office_seating_work_{part}_v1.png", 6, 4))
    for row in transition_rows:
        if opaque_height(row[0]) < opaque_height(row[-1]):
            row.reverse()

    old_heights = [opaque_height(Image.open(path).convert("RGBA")) for path in (high_motion/"Frames").glob("*.png")]
    if len(old_heights) != 48:
        raise ValueError(f"{high_motion}/Frames: expected 48 canonical walk frames")
    target_height = statistics.median(old_heights)
    standing_height = statistics.median(opaque_height(row[0]) for row in transition_rows)
    desired_scale = target_height / standing_height
    tallest_source_frame = max(opaque_height(image) for row in transition_rows + work_rows for image in row)
    fit_scale = (FOOT_BASELINE - 1) / tallest_source_frame
    scale = min(desired_scale, fit_scale)
    work_a_height = statistics.median(opaque_height(image) for row in work_rows[:4] for image in row)
    work_b_height = statistics.median(opaque_height(image) for row in work_rows[4:] for image in row)
    work_a_density_scale = min(1.0, work_b_height / work_a_height)
    work_b_density_scale = min(1.0, work_a_height / work_b_height)
    if not 0.65 <= scale <= 1.35:
        raise ValueError(f"{spec.character_id}: unsafe scale {scale:.3f}")

    frames: dict[tuple[str, str, int], Image.Image] = {}
    primary_hashes: dict[str, str] = {}
    written = 0
    for row_index, direction in enumerate(DIRECTIONS):
        sit = [normalize(image, scale, f"{spec.character_id}/{direction}/sit_down/{phase}") for phase, image in enumerate(transition_rows[row_index])]
        work_density_scale = work_a_density_scale if row_index < 4 else work_b_density_scale
        work = [normalize(image, scale * work_density_scale, f"{spec.character_id}/{direction}/sit_work/{phase}") for phase, image in enumerate(work_rows[row_index])]
        for phase in range(len(work)):
            override_path = frame_dir/f"{spec.character_id}_{direction}_sit_work_{phase}.png"
            approved_override = load_approved_override(repo, override_path)
            if approved_override is not None:
                work[phase] = approved_override
        clips = {"sit_down": sit, "sit_work": work, "stand_up": list(reversed(sit))}
        for clip, images in clips.items():
            hashes = [rgba_hash(image) for image in images]
            if len(hashes) != len(set(hashes)):
                raise ValueError(f"{spec.character_id}/{direction}/{clip}: duplicate frames")
            for phase, image in enumerate(images):
                key = (direction, clip, phase)
                path = frame_dir/f"{spec.character_id}_{direction}_{clip}_{phase}.png"
                is_override = path.relative_to(repo).as_posix() in APPROVED_FRAME_OVERRIDES
                if not is_override:
                    save_or_compare(path, image, verify_only)
                if not verify_only and not is_override:
                    write_frame_meta(repo, path, template)
                validate_frame_meta(path)
                frames[key] = image
                written += 1
                if clip != "stand_up":
                    digest = rgba_hash(image)
                    if digest in primary_hashes:
                        raise ValueError(f"primary-frame hash collision: {primary_hashes[digest]} and {path}")
                    primary_hashes[digest] = str(path)
        for phase in range(4):
            if frames[(direction, "stand_up", phase)].tobytes() != frames[(direction, "sit_down", 3-phase)].tobytes():
                raise ValueError(f"{spec.character_id}/{direction}: stand_up is not exact reverse")

    if written != 112 or len(primary_hashes) != 80:
        raise AssertionError(f"{spec.character_id}: frames={written}, unique-primary={len(primary_hashes)}")
    if not verify_only:
        for kind in ("transition", "work"):
            path = qa_dir/f"{spec.character_id}_office_seating_{kind}_8dir_contact_v1.png"
            make_contact(frames, kind).save(path, format="PNG", compress_level=9)
            ensure_default_meta(repo, path)
        gif = qa_dir/f"{spec.character_id}_office_seating_work8dir_preview_v1.gif"
        make_work_gif(gif, frames)
        ensure_default_meta(repo, gif)
    expected_names = {f"{spec.character_id}_{direction}_{clip}_{phase}.png" for direction in DIRECTIONS for clip, count in (("sit_down",4),("sit_work",6),("stand_up",4)) for phase in range(count)}
    actual_names = {path.name for path in frame_dir.glob("*.png")}
    if actual_names != expected_names:
        raise ValueError(f"{frame_dir}: frame set mismatch missing={expected_names-actual_names} extra={actual_names-expected_names}")
    return written, {"scale": scale, "desired_scale": desired_scale, "work_a_density_scale": work_a_density_scale, "work_b_density_scale": work_b_density_scale, "target_height": target_height, "source_standing_height": standing_height}


def main() -> None:
    args = parse_args()
    repo = args.repo_root.resolve()
    template_path = repo/"Assets/Art/Characters/Player/Pixel/Frames/player_south_a.png.meta"
    template = template_path.read_text(encoding="utf-8")
    total = 0
    for spec in CHARACTERS:
        count, stats = split_character(repo, spec, args.verify_only, template)
        total += count
        print(f"{spec.character_id}: PASS frames={count} scale={stats['scale']:.3f} desiredScale={stats['desired_scale']:.3f} workDensityA/B={stats['work_a_density_scale']:.3f}/{stats['work_b_density_scale']:.3f} targetHeight={stats['target_height']:.1f}")
    if total != 448:
        raise AssertionError(f"expected 448 frames, found {total}")
    action = "verified" if args.verify_only else "wrote"
    print(f"OFFICE_SEATING_SPLIT: PASS {action}=448 contacts=8 gifs=4 directions=8 clips=4+6+4")


if __name__ == "__main__":
    main()
