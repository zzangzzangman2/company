#!/usr/bin/env python3
"""Build the shared 12-character, 8-direction, 6-phase walk candidate.

The current identity-locked frame 0 is the stable head/torso authority for each
direction.  The repository's approved pre-coherence six-pose sheets are used only
as a lower-body motion donor.  This restores real contact/recoil/passing geometry
without redrawing faces, hair, upper clothing, or camera direction.

Default execution is non-destructive and writes a review candidate under
Artifacts/CharacterLocomotionGenerationV1.  ``--write`` is deliberately separate
and publishes only an already generated candidate while preserving every .meta.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from PIL.PngImagePlugin import PngInfo

from split_high_motion_sheets import extract_aligned_frames


REPO_ROOT = Path(__file__).resolve().parents[1]
PROFILE_PATH = REPO_ROOT / "Tools" / "character_locomotion_profiles_v1.json"
DONOR_ROOT = REPO_ROOT / "Assets" / "Art" / "Characters" / "BeforeCoherenceV1"
DEFAULT_OUTPUT = REPO_ROOT / "Artifacts" / "CharacterLocomotionGenerationV1"
FRAME_SIZE = 256
PHASE_COUNT = 6
GROUND_Y = 247
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"
DIRECTIONS_A = ("south", "southwest", "west", "northwest")
DIRECTIONS_B = ("north", "northeast", "east", "southeast")
DIRECTIONS = DIRECTIONS_A + DIRECTIONS_B


@dataclass(frozen=True)
class Character:
    character_id: str
    asset_folder: str

    @property
    def high_motion_root(self) -> Path:
        return REPO_ROOT / "Assets" / "Art" / "Characters" / self.asset_folder / "Pixel" / "HighMotion"

    @property
    def frame_root(self) -> Path:
        return self.high_motion_root / "Frames"

    @property
    def donor_root(self) -> Path:
        return DONOR_ROOT / self.character_id

    def frame_path(self, direction: str, phase: int) -> Path:
        return self.frame_root / f"{self.character_id}_{direction}_walk_{phase}.png"

    def sheet_path(self, part: str) -> Path:
        return self.high_motion_root / f"{self.character_id}_pixel_walk8dir6_{part}_v1.png"

    def donor_sheet_path(self, part: str) -> Path:
        return self.donor_root / f"{self.character_id}_pixel_walk8dir6_{part}_v1.png"


CHARACTERS = (
    Character("player", "Player"),
    Character("older_sister", "OlderSister"),
    Character("father", "Father"),
    Character("mother", "Mother"),
    Character("kim_seoa", "Employees/KimSeoa"),
    Character("lee_jian", "Employees/LeeJian"),
    Character("choi_iseo", "Employees/ChoiIseo"),
    Character("jung_arin", "Employees/JungArin"),
    Character("park_haeun", "Employees/ParkHaeun"),
    Character("han_sua", "Employees/HanSua"),
    Character("oh_jiwoo", "Employees/OhJiwoo"),
    Character("yoon_chaea", "Employees/YoonChaea"),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--write", action="store_true", help="Publish the generated candidate to stable runtime PNG paths.")
    parser.add_argument("--publish-existing", action="store_true", help="Publish an already generated candidate without rebuilding it.")
    return parser.parse_args()


def hard_alpha(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA")).copy()
    visible = rgba[:, :, 3] >= 128
    rgba[:, :, 3] = np.where(visible, 255, 0).astype(np.uint8)
    rgba[~visible, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def remove_tiny_islands(image: Image.Image, minimum_pixels: int = 6) -> Image.Image:
    rgba = np.asarray(hard_alpha(image), dtype=np.uint8).copy()
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        (rgba[:, :, 3] > 0).astype(np.uint8), 8
    )
    for index in range(1, count):
        if int(stats[index, cv2.CC_STAT_AREA]) < minimum_pixels:
            rgba[labels == index] = 0
    return Image.fromarray(rgba, "RGBA")


def alpha_bounds(array: np.ndarray) -> tuple[int, int, int, int]:
    rows, columns = np.nonzero(array[:, :, 3] > 0)
    if not len(columns):
        raise ValueError("empty character frame")
    return int(columns.min()), int(rows.min()), int(columns.max()), int(rows.max())


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    result = mask.copy()
    height, width = result.shape
    for _ in range(radius):
        padded = np.pad(result, 1, constant_values=False)
        result = np.logical_or.reduce(
            tuple(padded[dy : dy + height, dx : dx + width] for dy in range(3) for dx in range(3))
        )
    return result


def load_profiles() -> dict[str, dict[str, float | int]]:
    payload = json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != 1:
        raise ValueError(f"unsupported locomotion profile schema: {payload.get('schemaVersion')}")
    profiles = payload.get("characters", {})
    expected = {character.character_id for character in CHARACTERS}
    if set(profiles) != expected:
        raise ValueError(f"profile character mismatch: expected={sorted(expected)} actual={sorted(profiles)}")
    return profiles


def load_donor_rows(character: Character) -> dict[str, list[Image.Image]]:
    result: dict[str, list[Image.Image]] = {}
    for part, directions in (("a", DIRECTIONS_A), ("b", DIRECTIONS_B)):
        sheet_path = character.donor_sheet_path(part)
        if not sheet_path.is_file():
            raise FileNotFoundError(sheet_path)
        with Image.open(sheet_path) as loaded:
            sheet = hard_alpha(loaded)
        rows = extract_aligned_frames(sheet, sheet_path)
        for row, direction in enumerate(directions):
            result[direction] = [normalize_ground(remove_tiny_islands(frame)) for frame in rows[row]]
    return result


def normalize_ground(image: Image.Image) -> Image.Image:
    array = np.asarray(image.convert("RGBA"))
    _, _, _, bottom = alpha_bounds(array)
    dy = GROUND_Y - bottom
    if dy == 0:
        return hard_alpha(image)
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(image, (0, dy))
    return hard_alpha(canvas)


def motion_mask(donors: list[np.ndarray], seam_y: int, corridor_margin: int) -> np.ndarray:
    stack = np.stack(donors).astype(np.int16)
    alpha_union = np.any(stack[:, :, :, 3] > 0, axis=0)
    reference = stack[0]
    changed = np.any(np.max(np.abs(stack[:, :, :, :3] - reference[:, :, :3]), axis=3) >= 12, axis=0)
    changed |= np.any((stack[:, :, :, 3] > 0) != (reference[:, :, 3] > 0), axis=0)

    height, width = alpha_union.shape
    lower = np.zeros_like(alpha_union)
    lower[max(0, seam_y - 2) :] = True

    # The corridor is derived from every visible shoe/foot position across all six donor poses.
    # It excludes long hair and dangling sleeves while retaining the full stride envelope.
    foot_band = np.zeros_like(alpha_union)
    foot_band[max(seam_y, GROUND_Y - 40) : GROUND_Y + 1] = True
    foot_pixels = alpha_union & foot_band
    _, foot_x = np.nonzero(foot_pixels)
    if not len(foot_x):
        raise ValueError("lower-body donor has no measurable foot pixels")
    left = max(0, int(foot_x.min()) - corridor_margin)
    right = min(width - 1, int(foot_x.max()) + corridor_margin)
    corridor = np.zeros_like(alpha_union)
    corridor[:, left : right + 1] = True

    moving = dilate(changed & lower & corridor, 3)
    # Include all donor pixels connected to the moving envelope below the seam.  The extra dilation
    # keeps knee and skirt seams closed without authoring per-frame masks.
    moving |= dilate(alpha_union & lower & corridor, 1) & dilate(moving, 5)
    moving[: max(0, seam_y - 2)] = False
    return moving


def grounded_limb_mask(frame: np.ndarray, eligible: np.ndarray) -> np.ndarray:
    """Keep only donor components that are connected to a measurable shoe/foot.

    Some pre-coherence poses contain a dangling hand or hair below the nominal hip
    seam.  A rectangular lower-body cut admits those islands and creates a duplicate
    limb even though upper identity remains byte-stable.  Connectivity to the bottom
    34-pixel foot band is a character-independent semantic rule: both support and
    swing legs survive, while sleeves, hands, and hair cannot become locomotion donors.
    """
    visible = eligible & (frame[:, :, 3] > 0)
    count, labels = cv2.connectedComponents(visible.astype(np.uint8), 8)
    if count <= 1:
        raise ValueError("lower-body donor has no connected limb components")
    seed_rows = np.arange(FRAME_SIZE)[:, None] >= GROUND_Y - 34
    seeded_labels = np.unique(labels[visible & seed_rows])
    seeded_labels = seeded_labels[seeded_labels != 0]
    if not len(seeded_labels):
        raise ValueError("lower-body donor has no foot-connected component")
    return np.isin(labels, seeded_labels)


def shift_layer(layer: np.ndarray, dx: int, dy: int) -> np.ndarray:
    shifted = np.zeros_like(layer)
    height, width = layer.shape[:2]
    source_x0 = max(0, -dx)
    source_x1 = min(width, width - dx)
    source_y0 = max(0, -dy)
    source_y1 = min(height, height - dy)
    if source_x1 <= source_x0 or source_y1 <= source_y0:
        return shifted
    shifted[
        source_y0 + dy : source_y1 + dy,
        source_x0 + dx : source_x1 + dx,
    ] = layer[source_y0:source_y1, source_x0:source_x1]
    return shifted


def split_leg_layers(frame: np.ndarray, moving: np.ndarray) -> tuple[list[np.ndarray], list[tuple[float, float]]]:
    visible = moving & (frame[:, :, 3] > 0)
    yy, xx = np.indices(visible.shape)
    foot_sample = visible & (yy >= GROUND_Y - 46)
    rows, columns = np.nonzero(foot_sample)
    if len(columns) < 40:
        raise ValueError("contact donor has insufficient lower-limb pixels")
    centres = np.array([np.percentile(columns, 25), np.percentile(columns, 75)], dtype=np.float64)
    for _ in range(16):
        labels = np.argmin(np.abs(columns[:, None] - centres[None, :]), axis=1)
        updated = centres.copy()
        for index in range(2):
            selected = columns[labels == index]
            if len(selected):
                updated[index] = float(selected.mean())
        if np.allclose(updated, centres, atol=0.01):
            break
        centres = updated
    centres.sort()

    layers: list[np.ndarray] = []
    anchors: list[tuple[float, float]] = []
    moving_rows, moving_columns = np.nonzero(visible)
    for index, centre_x in enumerate(centres):
        if index == 0:
            selected = visible & (xx <= float(centres.mean()))
        else:
            selected = visible & (xx > float(centres.mean()))
        layer = np.zeros_like(frame)
        layer[selected] = frame[selected]
        foot_rows, foot_columns = np.nonzero(selected & (yy >= GROUND_Y - 34))
        if len(foot_columns) < 12:
            raise ValueError(f"contact donor leg {index} has no measurable foot")
        bottom = int(foot_rows.max())
        core = foot_rows >= max(GROUND_Y - 28, bottom - 16)
        anchors.append((float(foot_columns[core].mean()), float(foot_rows[core].mean())))
        layers.append(layer)
    return layers, anchors


def composite_contact(
    identity: np.ndarray,
    donor: np.ndarray,
    moving: np.ndarray,
    donor_limbs: np.ndarray,
) -> np.ndarray:
    output = identity.copy()
    output[moving] = 0
    visible = donor_limbs & (donor[:, :, 3] > 0)
    output[visible] = donor[visible]
    return np.asarray(hard_alpha(Image.fromarray(output, "RGBA")), dtype=np.uint8)


def composite_passing_phase(
    identity: np.ndarray,
    contact: np.ndarray,
    moving: np.ndarray,
    contact_limbs: np.ndarray,
    seam_y: int,
    source_layers: list[np.ndarray],
    source_anchors: list[tuple[float, float]],
    target_anchors: list[tuple[float, float]],
    support_index: int,
    support_progress: float,
    swing_progress: float,
    swing_lift: int,
    body_drop: int,
) -> np.ndarray:
    # A walk needs a small load response above the knees as well as moving shoes.  Keep the
    # approved pixels rigid (no redraw or deformation), but lower the identity layer by one pixel
    # in each support/down phase.  This preserves face, hair and clothing exactly while preventing
    # the legacy "frozen paper doll with swapped trousers" failure mode.
    rigid_body = identity.copy()
    rigid_body[moving] = 0
    output = shift_layer(rigid_body, 0, body_drop)
    identity_guard = dilate(identity[:, :, 3] > 0, 2)
    transition_band = np.zeros((FRAME_SIZE, FRAME_SIZE), dtype=bool)
    transition_band[max(0, seam_y - 2) : min(FRAME_SIZE, seam_y + 20)] = True
    swing_index = 1 - support_index
    order = (support_index, swing_index)
    for index in order:
        target_index = 1 - index
        progress = support_progress if index == support_index else swing_progress
        dx = int(round((target_anchors[target_index][0] - source_anchors[index][0]) * progress))
        layer_rows, _ = np.nonzero(source_layers[index][:, :, 3] > 0)
        if not len(layer_rows):
            raise ValueError("empty synthesized leg layer")
        ground_correction = GROUND_Y - int(layer_rows.max())
        dy = ground_correction if index == support_index else ground_correction - swing_lift
        shifted = shift_layer(source_layers[index], dx, dy)
        visible = shifted[:, :, 3] > 0
        # A lifted thigh must never cross into the identity-locked torso rows.  Preserve the
        # protected rows exactly; the unchanged seam overlay below closes the hip joint.
        visible[: max(0, seam_y - 2)] = False
        visible &= ~transition_band | identity_guard
        output[visible] = shifted[visible]

    # Keep a narrow, unchanged hip seam from the contact donor.  Only the actual legs below it
    # travel; this prevents a transparent crack without turning pants/skirt wobble into the gait.
    seam = contact_limbs.copy()
    seam[seam_y + 9 :] = False
    seam_visible = seam & (contact[:, :, 3] > 0)
    output[seam_visible] = contact[seam_visible]
    return np.asarray(hard_alpha(Image.fromarray(output, "RGBA")), dtype=np.uint8)


def build_direction(
    character: Character,
    direction: str,
    donor_images: list[Image.Image],
    profile: dict[str, float | int],
) -> tuple[list[Image.Image], dict[str, object]]:
    with Image.open(character.frame_path(direction, 0)) as loaded:
        identity = np.asarray(hard_alpha(loaded), dtype=np.uint8)
    donor_arrays = [np.asarray(hard_alpha(image), dtype=np.uint8) for image in donor_images]
    _, top, _, bottom = alpha_bounds(identity)
    lower_fraction = float(profile["lowerBodyStart"])
    seam_y = int(round(top + (bottom - top + 1) * lower_fraction))
    raw_moving = motion_mask(donor_arrays, seam_y, int(profile["footCorridorMarginPx"]))
    donor_limb_masks = [grounded_limb_mask(donor, raw_moving) for donor in donor_arrays]
    # Close to the hip seam, a true leg may change overlap but cannot appear as a new
    # lateral appendage.  Require donor pixels in this transition band to overlap a
    # two-pixel expansion of the approved identity silhouette.  Below the band the
    # complete authored stride envelope remains unconstrained.
    identity_guard = dilate(identity[:, :, 3] > 0, 2)
    transition_band = np.zeros((FRAME_SIZE, FRAME_SIZE), dtype=bool)
    transition_band[max(0, seam_y - 2) : min(FRAME_SIZE, seam_y + 20)] = True
    donor_limb_masks = [
        limb_mask & (~transition_band | identity_guard)
        for limb_mask in donor_limb_masks
    ]
    # Clear the complete leg excursion envelope from the identity frame, but only ever
    # insert pixels from the per-pose foot-connected masks above.
    moving = dilate(np.logical_or.reduce(donor_limb_masks), 1) & raw_moving

    contact_a = donor_arrays[0]
    contact_b = donor_arrays[3]
    contact_limbs_a = donor_limb_masks[0]
    contact_limbs_b = donor_limb_masks[3]
    layers_a, anchors_a = split_leg_layers(contact_a, contact_limbs_a)
    layers_b, anchors_b = split_leg_layers(contact_b, contact_limbs_b)
    generated_arrays = [
        composite_contact(identity, contact_a, moving, contact_limbs_a),
        composite_passing_phase(
            identity, contact_a, moving, contact_limbs_a, seam_y, layers_a, anchors_a, anchors_b,
            support_index=0, support_progress=0.12, swing_progress=0.32, swing_lift=1,
            body_drop=1,
        ),
        composite_passing_phase(
            identity, contact_a, moving, contact_limbs_a, seam_y, layers_a, anchors_a, anchors_b,
            support_index=0, support_progress=0.32, swing_progress=0.70, swing_lift=4,
            body_drop=0,
        ),
        composite_contact(identity, contact_b, moving, contact_limbs_b),
        composite_passing_phase(
            identity, contact_b, moving, contact_limbs_b, seam_y, layers_b, anchors_b, anchors_a,
            support_index=1, support_progress=0.12, swing_progress=0.32, swing_lift=1,
            body_drop=1,
        ),
        composite_passing_phase(
            identity, contact_b, moving, contact_limbs_b, seam_y, layers_b, anchors_b, anchors_a,
            support_index=1, support_progress=0.32, swing_progress=0.70, swing_lift=4,
            body_drop=0,
        ),
    ]

    outputs: list[Image.Image] = []
    for phase, output in enumerate(generated_arrays):
        output = output.copy()
        output[GROUND_Y + 1 :] = 0
        cleaned = np.asarray(remove_tiny_islands(Image.fromarray(output, "RGBA")), dtype=np.uint8).copy()
        protected_end = max(0, seam_y - 2)
        cleaned[:protected_end] = output[:protected_end]
        output = cleaned
        _, _, _, output_bottom = alpha_bounds(output)
        if output_bottom != GROUND_Y:
            raise ValueError(
                f"{character.character_id}/{direction}/{phase}: generated ground={output_bottom}, expected={GROUND_Y}"
            )
        outputs.append(Image.fromarray(output, "RGBA"))

    upper_end = max(0, seam_y - 2)
    body_drop_by_phase = (0, 1, 0, 0, 1, 0)
    for phase, output in enumerate(outputs):
        expected = shift_layer(identity, 0, body_drop_by_phase[phase])
        if np.asarray(output)[:upper_end].tobytes() != expected[:upper_end].tobytes():
            raise AssertionError(f"{character.character_id}/{direction}/{phase}: upper identity drift")
        unexpected_transition = (
            (np.asarray(output)[:, :, 3] > 0) & transition_band & ~identity_guard
        )
        if unexpected_transition.any():
            raise AssertionError(
                f"{character.character_id}/{direction}/{phase}: donor accessory crossed hip transition"
            )
    if len({image.tobytes() for image in outputs}) != PHASE_COUNT:
        raise ValueError(f"{character.character_id}/{direction}: generated frames are not six unique poses")

    report = {
        "seamY": seam_y,
        "movingPixels": int(moving.sum()),
        "upperIdentityRows": upper_end,
        "rigidBodyDropPx": list(body_drop_by_phase),
        "frameSha256": [hashlib.sha256(image.tobytes()).hexdigest().upper() for image in outputs],
    }
    return outputs, report


def candidate_frame_path(output: Path, character: Character, direction: str, phase: int) -> Path:
    return output / "Candidate" / character.character_id / "Frames" / f"{character.character_id}_{direction}_walk_{phase}.png"


def candidate_sheet_path(output: Path, character: Character, part: str) -> Path:
    return output / "Candidate" / character.character_id / f"{character.character_id}_pixel_walk8dir6_{part}_v1.png"


def build_sheet(frames: dict[str, list[Image.Image]], directions: tuple[str, ...]) -> Image.Image:
    sheet = Image.new("RGBA", (FRAME_SIZE * PHASE_COUNT, FRAME_SIZE * len(directions)), (0, 0, 0, 0))
    for row, direction in enumerate(directions):
        for phase, frame in enumerate(frames[direction]):
            sheet.paste(frame, (phase * FRAME_SIZE, row * FRAME_SIZE))
    return sheet


def save_candidate(output: Path) -> dict[str, object]:
    profiles = load_profiles()
    output.mkdir(parents=True, exist_ok=True)
    report: dict[str, object] = {"schemaVersion": 1, "characters": {}}
    all_frames: dict[str, dict[str, list[Image.Image]]] = {}
    for character in CHARACTERS:
        donors = load_donor_rows(character)
        character_frames: dict[str, list[Image.Image]] = {}
        character_report: dict[str, object] = {}
        for direction in DIRECTIONS:
            frames, direction_report = build_direction(
                character, direction, donors[direction], profiles[character.character_id]
            )
            character_frames[direction] = frames
            character_report[direction] = direction_report
            for phase, frame in enumerate(frames):
                path = candidate_frame_path(output, character, direction, phase)
                path.parent.mkdir(parents=True, exist_ok=True)
                frame.save(path, format="PNG", compress_level=9)
        for part, directions in (("a", DIRECTIONS_A), ("b", DIRECTIONS_B)):
            metadata = PngInfo()
            metadata.add_text(LAYOUT_METADATA_KEY, GRID_LAYOUT_MARKER)
            path = candidate_sheet_path(output, character, part)
            build_sheet(character_frames, directions).save(path, format="PNG", compress_level=9, pnginfo=metadata)
        all_frames[character.character_id] = character_frames
        report["characters"][character.character_id] = character_report
        print(f"BUILT {character.character_id}: directions=8 frames=48")
    render_contact_sheets(output, all_frames)
    (output / "generation-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n"
    )
    return report


def font(size: int) -> ImageFont.ImageFont:
    for path in (
        Path("C:/Windows/Fonts/malgun.ttf"),
        Path("C:/Windows/Fonts/arial.ttf"),
    ):
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def render_contact_sheets(
    output: Path,
    all_frames: dict[str, dict[str, list[Image.Image]]],
) -> None:
    evidence = output / "Evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    tile = 112
    label_height = 28
    header_width = 132
    face = font(15)
    small = font(12)
    for direction in DIRECTIONS:
        canvas = Image.new(
            "RGB",
            (header_width + tile * PHASE_COUNT, label_height + tile * len(CHARACTERS)),
            (28, 33, 44),
        )
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 6), direction.upper(), font=face, fill=(245, 245, 245))
        for phase in range(PHASE_COUNT):
            draw.text((header_width + phase * tile + 40, 6), f"P{phase}", font=face, fill=(245, 245, 245))
        for row, character in enumerate(CHARACTERS):
            y = label_height + row * tile
            draw.text((8, y + 45), character.character_id, font=small, fill=(235, 235, 235))
            for phase, frame in enumerate(all_frames[character.character_id][direction]):
                panel = Image.new("RGBA", (tile, tile), (239, 232, 219, 255))
                sprite = frame.resize((tile, tile), Image.Resampling.NEAREST)
                panel.alpha_composite(sprite)
                canvas.paste(panel.convert("RGB"), (header_width + phase * tile, y))
        canvas.save(evidence / f"all_characters_{direction}_6phase_contact_v1.png", quality=95)

    # Runtime-like loop evidence.  These are derived from the exact candidate frames, not a second
    # animation source, and make contact/support/pass timing visible without opening 576 files.
    player = next(character for character in CHARACTERS if character.character_id == "player")
    for direction in DIRECTIONS:
        gif_frames: list[Image.Image] = []
        for phase, frame in enumerate(all_frames[player.character_id][direction]):
            panel = Image.new("RGB", (384, 420), (239, 232, 219))
            sprite = frame.resize((384, 384), Image.Resampling.NEAREST)
            panel.paste(sprite, (0, 28), sprite)
            draw = ImageDraw.Draw(panel)
            draw.text((10, 6), f"PLAYER {direction.upper()}  P{phase}", font=face, fill=(28, 33, 44))
            gif_frames.append(panel)
        gif_frames[0].save(
            evidence / f"player_{direction}_walk_6phase_v1.gif",
            save_all=True,
            append_images=gif_frames[1:],
            duration=165,
            loop=0,
            disposal=2,
        )

    east_frames: list[Image.Image] = []
    for phase in range(PHASE_COUNT):
        canvas = Image.new("RGB", (356, 76 + 112 * len(CHARACTERS)), (28, 33, 44))
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 8), f"ALL CHARACTERS EAST  P{phase}", font=face, fill=(245, 245, 245))
        for row, character in enumerate(CHARACTERS):
            y = 48 + row * 112
            draw.text((8, y + 45), character.character_id, font=small, fill=(235, 235, 235))
            panel = Image.new("RGBA", (112, 112), (239, 232, 219, 255))
            sprite = all_frames[character.character_id]["east"][phase].resize(
                (112, 112), Image.Resampling.NEAREST
            )
            panel.alpha_composite(sprite)
            canvas.paste(panel.convert("RGB"), (236, y))
        east_frames.append(canvas)
    east_frames[0].save(
        evidence / "all_characters_east_walk_6phase_v1.gif",
        save_all=True,
        append_images=east_frames[1:],
        duration=165,
        loop=0,
        disposal=2,
    )


def publish(output: Path) -> None:
    backup = output / "BeforePublish"
    for character in CHARACTERS:
        for direction in DIRECTIONS:
            for phase in range(PHASE_COUNT):
                source = candidate_frame_path(output, character, direction, phase)
                destination = character.frame_path(direction, phase)
                if not source.is_file():
                    raise FileNotFoundError(source)
                backup_path = backup / destination.relative_to(REPO_ROOT)
                backup_path.parent.mkdir(parents=True, exist_ok=True)
                if not backup_path.exists():
                    shutil.copyfile(destination, backup_path)
                shutil.copyfile(source, destination)
        for part in ("a", "b"):
            source = candidate_sheet_path(output, character, part)
            destination = character.sheet_path(part)
            backup_path = backup / destination.relative_to(REPO_ROOT)
            backup_path.parent.mkdir(parents=True, exist_ok=True)
            if not backup_path.exists():
                shutil.copyfile(destination, backup_path)
            shutil.copyfile(source, destination)
        print(f"PUBLISHED {character.character_id}: preserved existing .meta files")


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    if not args.publish_existing:
        save_candidate(output)
    if args.write or args.publish_existing:
        publish(output)
    print(f"CHARACTER_LOCOMOTION_GENERATION_V1: CANDIDATE_READY output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
