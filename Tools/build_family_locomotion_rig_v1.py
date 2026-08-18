from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw
from PIL.PngImagePlugin import PngInfo


REPO = Path(__file__).resolve().parents[1]
SOURCE_ROOT = REPO / "ArtSources" / "FamilyLocomotionRigV1"
ATLAS_ROOT = SOURCE_ROOT
PLAYER_EAST_RAW = SOURCE_ROOT / "player_east_rig_parts_raw_v1.png"
MANIFEST_PATH = SOURCE_ROOT / "rig_manifest_v1.json"
OUTPUT = REPO / "Artifacts" / "CharacterLocomotionGenerationV1"
SIZE = 256
GROUND = 247
ROOT_STEP = (0.99380799 / 6.0) / (1.55 / 180.0)
CANONICAL_DIRECTIONS = ("south", "north", "east", "southeast", "northeast")
ALL_DIRECTIONS = (
    "south", "southwest", "west", "northwest",
    "north", "northeast", "east", "southeast",
)
DIRECTIONS_A = ("south", "southwest", "west", "northwest")
DIRECTIONS_B = ("north", "northeast", "east", "southeast")
ASSET_FOLDERS = {
    "player": "Player",
    "older_sister": "OlderSister",
    "father": "Father",
    "mother": "Mother",
}
LAYOUT_METADATA_KEY = "familyCompanyHighMotionLayout"
GRID_LAYOUT_MARKER = "grid-4x6-v1"
RUNTIME_ANCHOR_RESOURCE = (
    REPO / "Assets" / "FamilyCompany" / "Content" / "Resources" / "HighMotion" /
    "FamilyLocomotionFootAnchorsV1.json"
)
VECTORS = {
    "south": np.array((0.0, 1.0), np.float64),
    "north": np.array((0.0, -1.0), np.float64),
    "east": np.array((1.0, 0.0), np.float64),
    "southeast": np.array((math.sqrt(0.5), math.sqrt(0.5)), np.float64),
    "northeast": np.array((math.sqrt(0.5), -math.sqrt(0.5)), np.float64),
}
MARKER_COLORS = {
    "left": (0, 235, 255),
    "right": (255, 35, 195),
}
BODY_OFFSETS = (
    np.array((0.0, 0.0), np.float64),
    np.array((0.0, 1.0), np.float64),
    np.array((0.0, 0.0), np.float64),
    np.array((0.0, 0.0), np.float64),
    np.array((0.0, 1.0), np.float64),
    np.array((0.0, 0.0), np.float64),
)


@dataclass(frozen=True)
class Profile:
    character: str
    atlas_name: str
    atlas_rows: tuple[str, ...]
    leg_start_y: int
    leg_left_x: int
    leg_right_x: int
    lower_clear_y: int
    hip_y: float
    thigh_length: float
    shin_length: float


PROFILES = (
    Profile("player", "player_other_directions_raw_v1.png", ("south", "north", "southeast", "northeast"), 179, 100, 157, 190, 178.0, 34.0, 42.0),
    Profile("older_sister", "older_sister_five_directions_raw_v1.png", CANONICAL_DIRECTIONS, 171, 102, 153, 180, 169.0, 38.0, 47.0),
    Profile("father", "father_five_directions_raw_v1.png", CANONICAL_DIRECTIONS, 166, 100, 155, 181, 164.0, 40.0, 48.0),
    Profile("mother", "mother_five_directions_raw_v1.png", CANONICAL_DIRECTIONS, 180, 98, 145, 194, 176.0, 36.0, 45.0),
)


def validate_manifest() -> dict[str, object]:
    payload = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if payload.get("contract") != "FC-FAMILY-LOCOMOTION-RIG-V1":
        raise ValueError("FamilyLocomotionRigV1 manifest contract mismatch")
    expected = payload.get("sourceSha256", {})
    actual = {
        path.name: hashlib.sha256(path.read_bytes()).hexdigest().upper()
        for path in sorted(SOURCE_ROOT.glob("*.png"))
    }
    if actual != expected:
        raise ValueError(f"FamilyLocomotionRigV1 raw source SHA mismatch: expected={expected} actual={actual}")
    return payload


def hard_alpha(array: np.ndarray) -> np.ndarray:
    result = array.copy()
    result[:, :, 3] = np.where(result[:, :, 3] >= 128, 255, 0).astype(np.uint8)
    result[result[:, :, 3] == 0, :3] = 0
    return result


def chroma_rgba(path: Path) -> tuple[np.ndarray, list[tuple[int, int, int, int, int, int, float, float]]]:
    rgb = np.asarray(Image.open(path).convert("RGB"), np.uint8)
    values = rgb.astype(np.int16)
    background = (
        (values[:, :, 1] >= values[:, :, 0] + 24)
        & (values[:, :, 1] >= values[:, :, 2] + 18)
        & (values[:, :, 1] >= 70)
    )
    foreground = (~background).astype(np.uint8)
    foreground = cv2.morphologyEx(foreground, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(foreground, 8)
    components = []
    for index in range(1, count):
        x, y, width, height, area = (int(value) for value in stats[index])
        if area >= 1000:
            components.append(
                (x, y, width, height, area, index, float(centroids[index, 0]), float(centroids[index, 1]))
            )
    rgba = np.dstack((rgb, foreground * 255))
    return rgba, components


def crop_component(rgba: np.ndarray, component) -> np.ndarray:
    x, y, width, height = component[:4]
    crop = rgba[y : y + height, x : x + width].copy()
    # Remove green spill without altering skin, blue trousers or teal identity pixels.
    visible = crop[:, :, 3] > 0
    rgb = crop[:, :, :3].astype(np.int16)
    maximum_other = np.maximum(rgb[:, :, 0], rgb[:, :, 2])
    spill = visible & (rgb[:, :, 1] > maximum_other + 18)
    crop[:, :, 1] = np.where(spill, np.minimum(rgb[:, :, 1], maximum_other + 12), rgb[:, :, 1]).astype(np.uint8)
    return hard_alpha(crop)


def group_atlas(path: Path, row_names: tuple[str, ...]) -> dict[str, tuple[np.ndarray, ...]]:
    rgba, components = chroma_rgba(path)
    expected = len(row_names) * 4
    if len(components) != expected:
        raise ValueError(f"{path}: expected {expected} rig components, found {len(components)}")
    components.sort(key=lambda item: (item[7], item[6]))
    result = {}
    for row_index, direction in enumerate(row_names):
        row = components[row_index * 4 : (row_index + 1) * 4]
        row.sort(key=lambda item: item[6])
        result[direction] = tuple(crop_component(rgba, item) for item in row)
    return result


def player_east_parts() -> tuple[np.ndarray, ...]:
    rgba, components = chroma_rgba(PLAYER_EAST_RAW)
    components.sort(key=lambda item: item[0])
    # component 0 is the generated torso, 1..4 are the binding left/right leg parts.
    if len(components) < 5:
        raise ValueError(f"{PLAYER_EAST_RAW}: missing player east rig components")
    return tuple(crop_component(rgba, item) for item in components[1:5])


def endpoint(part: np.ndarray, at_bottom: bool, band_fraction: float = 0.10) -> np.ndarray:
    alpha = part[:, :, 3] > 0
    rows, columns = np.nonzero(alpha)
    top, bottom = int(rows.min()), int(rows.max())
    band = max(4, int(round((bottom - top + 1) * band_fraction)))
    selected = rows >= bottom - band if at_bottom else rows <= top + band
    return np.array((float(np.median(columns[selected])), float(np.median(rows[selected]))), np.float64)


def foot_anchor(part: np.ndarray) -> np.ndarray:
    alpha = part[:, :, 3] > 0
    rows, columns = np.nonzero(alpha)
    bottom = int(rows.max())
    core = rows >= bottom - max(6, int(round(part.shape[0] * 0.055)))
    return np.array((float(columns[core].mean()), float(rows[core].mean())), np.float64)


def transform_two_points(
    part: np.ndarray,
    source_start: np.ndarray,
    source_end: np.ndarray,
    target_start: np.ndarray,
    target_end: np.ndarray,
) -> np.ndarray:
    source_vector = source_end - source_start
    target_vector = target_end - target_start
    scale = float(np.linalg.norm(target_vector) / max(np.linalg.norm(source_vector), 0.001))
    source_angle = math.atan2(source_vector[1], source_vector[0])
    target_angle = math.atan2(target_vector[1], target_vector[0])
    angle = target_angle - source_angle
    cosine, sine = math.cos(angle) * scale, math.sin(angle) * scale
    matrix2 = np.array(((cosine, -sine), (sine, cosine)), np.float64)
    translation = target_start - matrix2 @ source_start
    matrix = np.hstack((matrix2, translation[:, None])).astype(np.float32)
    warped = cv2.warpAffine(
        part,
        matrix,
        (SIZE, SIZE),
        flags=cv2.INTER_LANCZOS4,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=(0, 0, 0, 0),
    )
    return hard_alpha(warped)


def composite(base: np.ndarray, layer: np.ndarray) -> None:
    alpha = layer[:, :, 3:4].astype(np.float32) / 255.0
    base[:, :, :3] = np.rint(layer[:, :, :3] * alpha + base[:, :, :3] * (1.0 - alpha)).astype(np.uint8)
    base[:, :, 3:4] = np.maximum(base[:, :, 3:4], layer[:, :, 3:4])


def shift_image(image: np.ndarray, offset: np.ndarray) -> np.ndarray:
    matrix = np.array(((1.0, 0.0, offset[0]), (0.0, 1.0, offset[1])), np.float32)
    return cv2.warpAffine(
        image,
        matrix,
        (SIZE, SIZE),
        flags=cv2.INTER_NEAREST,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=(0, 0, 0, 0),
    )


def identity_upper(profile: Profile, direction: str) -> np.ndarray:
    path = REPO / "Tools/CharacterLocomotionIdentityV1" / profile.character / f"{profile.character}_{direction}_identity_v1.png"
    upper = np.asarray(Image.open(path).convert("RGBA"), np.uint8).copy()
    # Remove the canonical standing legs at their real garment seam while
    # retaining hands that hang a few pixels below the belt/shorts/skirt.
    upper[
        profile.leg_start_y :,
        profile.leg_left_x : profile.leg_right_x + 1,
        :,
    ] = 0
    upper[profile.lower_clear_y :, :, :] = 0
    return hard_alpha(upper)


def knee_candidates(hip: np.ndarray, foot: np.ndarray, thigh: float, shin: float) -> tuple[np.ndarray, np.ndarray]:
    delta = foot - hip
    raw_distance = float(np.linalg.norm(delta))
    direction = delta / max(raw_distance, 0.001)
    distance = min(max(raw_distance, abs(shin - thigh) + 0.01), thigh + shin - 0.01)
    along = (thigh * thigh - shin * shin + distance * distance) / (2.0 * distance)
    height = math.sqrt(max(0.0, thigh * thigh - along * along))
    base = hip + along * direction
    perpendicular = np.array((-direction[1], direction[0]))
    return base + height * perpendicular, base - height * perpendicular


def choose_knee(
    profile: Profile,
    direction: str,
    leg: str,
    support: bool,
    hip: np.ndarray,
    foot: np.ndarray,
) -> np.ndarray:
    vector = VECTORS[direction]
    if abs(vector[0]) < 0.25:
        # Front/back motion is depth motion. A fixed-length screen-plane IK chain
        # can only reach the nearer depth sample by throwing the knee sideways,
        # which looks bow-legged. Use deterministic foreshortening instead: the
        # projected thigh/shin shorten while the knee stays close to its anatomy.
        knee = hip * 0.52 + foot * 0.48
        anatomical_side = (1.0 if leg == "left" else -1.0) * (1.0 if direction == "south" else -1.0)
        knee[0] += anatomical_side * (5.0 if support else 3.0)
        return knee
    # Side/diagonal rows also use controlled projected foreshortening. A small
    # forward swing bend and a small backward support bend keep knees readable
    # without the diamond-shaped marionette pose produced by unconstrained IK.
    knee = hip * 0.52 + foot * 0.48
    motion_sign = math.copysign(1.0, vector[0])
    knee[0] += motion_sign * (8.0 if not support else -3.0)
    return knee


def marker_foot(marker: np.ndarray, color: tuple[int, int, int]) -> np.ndarray:
    mask = np.all(marker[:, :, :3] == np.asarray(color, np.uint8), axis=2) & (marker[:, :, 3] > 0)
    rows, columns = np.nonzero(mask)
    if len(columns) < 8:
        raise ValueError(f"marker foot disappeared for color={color}")
    bottom = int(rows.max())
    core = rows >= bottom - 13
    return np.array((float(columns[core].mean()), float(rows[core].mean())), np.float64)


def alpha_components(frame: np.ndarray) -> list[int]:
    count, _, stats, _ = cv2.connectedComponentsWithStats((frame[:, :, 3] > 0).astype(np.uint8), 8)
    return sorted((int(stats[index, cv2.CC_STAT_AREA]) for index in range(1, count)), reverse=True)


def initial_controls(profile: Profile, direction: str) -> list[dict[str, np.ndarray]]:
    vector = VECTORS[direction]
    base = np.array((128.0, GROUND - ROOT_STEP * abs(vector[1])), np.float64)
    forward = base + ROOT_STEP * vector
    middle = base.copy()
    rear = base - ROOT_STEP * vector
    controls = []
    for phase in range(6):
        local = phase % 3
        if local == 0:
            support, swing = forward.copy(), rear.copy()
        elif local == 1:
            support, swing = middle.copy(), middle + np.array((0.0, -10.0))
        else:
            support, swing = rear.copy(), forward + np.array((0.0, -5.0))
        if phase < 3:
            controls.append({"left": support, "right": swing})
        else:
            controls.append({"left": swing, "right": support})
    if direction in ("south", "north"):
        left_sign = 1.0 if direction == "south" else -1.0
        stance = 18.0 if profile.character == "mother" else 9.0
        # Keep anatomical legs separately visible in depth-facing rows. This is
        # especially wide for the mother's opaque skirt; otherwise the far leg
        # vanishes and the cycle reads as a one-legged glide.
        for phase in range(6):
            controls[phase]["left"][0] += left_sign * stance
            controls[phase]["right"][0] -= left_sign * stance
    return controls


def render_direction(
    profile: Profile,
    direction: str,
    parts: tuple[np.ndarray, ...],
    controls: list[dict[str, np.ndarray]],
) -> tuple[list[np.ndarray], list[np.ndarray]]:
    left_thigh, left_shin, right_thigh, right_shin = parts
    sources = {
        "left": (
            left_thigh, endpoint(left_thigh, False), endpoint(left_thigh, True),
            left_shin, endpoint(left_shin, False), foot_anchor(left_shin),
        ),
        "right": (
            right_thigh, endpoint(right_thigh, False), endpoint(right_thigh, True),
            right_shin, endpoint(right_shin, False), foot_anchor(right_shin),
        ),
    }
    base_hip = np.array((128.0, profile.hip_y), np.float64)
    canonical_upper = identity_upper(profile, direction)
    frames, markers = [], []
    for phase in range(6):
        body_offset = BODY_OFFSETS[phase]
        hip = base_hip + body_offset
        upper = shift_image(canonical_upper, body_offset)
        support_leg = "left" if phase < 3 else "right"
        layers = {}
        for leg in ("left", "right"):
            foot = controls[phase][leg]
            knee = choose_knee(profile, direction, leg, leg == support_leg, hip, foot)
            thigh_part, thigh_start, thigh_end, shin_part, shin_start, shin_end = sources[leg]
            layers[f"{leg}_upper"] = transform_two_points(thigh_part, thigh_start, thigh_end, hip, knee)
            layers[f"{leg}_lower"] = transform_two_points(shin_part, shin_start, shin_end, knee, foot)

        swing_leg = "right" if support_leg == "left" else "left"
        order = (
            layers[f"{swing_leg}_upper"], layers[f"{swing_leg}_lower"],
            layers[f"{support_leg}_upper"], layers[f"{support_leg}_lower"], upper,
        )
        frame = np.zeros((SIZE, SIZE, 4), np.uint8)
        for layer in order:
            composite(frame, layer)
        frames.append(hard_alpha(frame))

        marker = np.zeros((SIZE, SIZE, 4), np.uint8)
        for leg in (swing_leg, support_leg):
            for suffix in ("upper", "lower"):
                layer = layers[f"{leg}_{suffix}"]
                mask = layer[:, :, 3] > 0
                marker[mask, :3] = MARKER_COLORS[leg]
                marker[mask, 3] = 255
        composite(marker, upper)
        markers.append(hard_alpha(marker))
    return frames, markers


def calibrate_direction(profile: Profile, direction: str, parts: tuple[np.ndarray, ...]):
    controls = initial_controls(profile, direction)
    vector = VECTORS[direction]
    for iteration in range(12):
        frames, markers = render_direction(profile, direction, parts, controls)
        supports = []
        for phase in range(6):
            leg = "left" if phase < 3 else "right"
            try:
                supports.append(marker_foot(markers[phase], MARKER_COLORS[leg]))
            except ValueError as error:
                raise ValueError(
                    f"{profile.character}/{direction}/iteration={iteration}/P{phase}/{leg}: {error} "
                    f"control={controls[phase][leg]}"
                ) from error
        baseline = supports[0]
        baseline_projection = float(np.dot(baseline, vector))
        if iteration == 0:
            print(
                f"CALIBRATE_START {profile.character}/{direction} "
                f"supports={'/'.join(f'({p[0]:.1f},{p[1]:.1f})' for p in supports)}"
            )
        maximum = 0.0
        for phase in range(6):
            leg = "left" if phase < 3 else "right"
            local = phase if phase < 3 else phase - 3
            actual = supports[phase]
            desired_projection = baseline_projection - local * ROOT_STEP
            projected_error = desired_projection - float(np.dot(actual, vector))
            maximum = max(maximum, abs(projected_error))
            controls[phase][leg] = controls[phase][leg] + np.clip(projected_error, -8.0, 8.0) * vector
        if maximum <= 0.20:
            break
    frames, markers = render_direction(profile, direction, parts, controls)
    supports = []
    for phase in range(6):
        leg = "left" if phase < 3 else "right"
        supports.append(marker_foot(markers[phase], MARKER_COLORS[leg]))
    left_world = [supports[p] + p * ROOT_STEP * vector for p in range(3)]
    right_world = [supports[p] + p * ROOT_STEP * vector for p in range(3, 6)]
    drift_left = max(abs(float(np.dot(point - left_world[0], vector))) for point in left_world)
    drift_right = max(abs(float(np.dot(point - right_world[0], vector))) for point in right_world)
    print(
        f"CALIBRATE_END {profile.character}/{direction} drift={drift_left:.3f}/{drift_right:.3f} "
        f"supports={'/'.join(f'({p[0]:.1f},{p[1]:.1f})' for p in supports)}"
    )
    return frames, markers, controls, drift_left, drift_right


def save_direction(profile: Profile, direction: str, frames: list[np.ndarray], markers: list[np.ndarray]) -> None:
    frame_root = OUTPUT / "Candidate" / profile.character / "Frames"
    marker_root = OUTPUT / "Markers" / profile.character / "Frames"
    frame_root.mkdir(parents=True, exist_ok=True)
    marker_root.mkdir(parents=True, exist_ok=True)
    for phase in range(6):
        Image.fromarray(frames[phase], "RGBA").save(frame_root / f"{profile.character}_{direction}_walk_{phase}.png")
        Image.fromarray(markers[phase], "RGBA").save(marker_root / f"{profile.character}_{direction}_walk_{phase}.png")


def build_sheets(all_frames: dict[tuple[str, str], list[np.ndarray]]) -> None:
    for profile in PROFILES:
        for part, directions in (("a", DIRECTIONS_A), ("b", DIRECTIONS_B)):
            sheet = Image.new("RGBA", (SIZE * 6, SIZE * 4), (0, 0, 0, 0))
            for row, direction in enumerate(directions):
                for phase, frame in enumerate(all_frames[(profile.character, direction)]):
                    sheet.paste(Image.fromarray(frame, "RGBA"), (phase * SIZE, row * SIZE))
            metadata = PngInfo()
            metadata.add_text(LAYOUT_METADATA_KEY, GRID_LAYOUT_MARKER)
            path = OUTPUT / "Candidate" / profile.character / f"{profile.character}_pixel_walk8dir6_{part}_v1.png"
            path.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(path, format="PNG", compress_level=9, pnginfo=metadata)


def mirror_rows(frames: list[np.ndarray]) -> list[np.ndarray]:
    return [np.asarray(Image.fromarray(frame, "RGBA").transpose(Image.Transpose.FLIP_LEFT_RIGHT)) for frame in frames]


def build_contact_sheets(all_frames: dict[tuple[str, str], list[np.ndarray]]) -> None:
    evidence = OUTPUT / "Evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    for direction in ALL_DIRECTIONS:
        sheet = Image.new("RGB", (SIZE * 6, 36 + SIZE * len(PROFILES)), (235, 229, 215))
        draw = ImageDraw.Draw(sheet)
        draw.text((8, 10), direction.upper(), fill=(20, 25, 32))
        for row, profile in enumerate(PROFILES):
            draw.text((8, 36 + row * SIZE + 8), profile.character, fill=(20, 25, 32))
            for phase, frame in enumerate(all_frames[(profile.character, direction)]):
                image = Image.fromarray(frame, "RGBA")
                sheet.paste(image, (phase * SIZE, 36 + row * SIZE), image)
        sheet.save(evidence / f"family_{direction}_rig_contact.png")


def build_world_motion_gifs(
    all_frames: dict[tuple[str, str], list[np.ndarray]],
    all_markers: dict[tuple[str, str], list[np.ndarray]],
) -> None:
    evidence = OUTPUT / "Evidence"
    cell_width, cell_height = 420, 370
    placements = ((0, 0), (cell_width, 0), (0, cell_height), (cell_width, cell_height))
    for direction in ALL_DIRECTIONS:
        source_direction = {
            "west": "east", "southwest": "southeast", "northwest": "northeast"
        }.get(direction, direction)
        vector = VECTORS[source_direction].copy()
        if direction in ("west", "southwest", "northwest"):
            vector[0] *= -1.0
        clean_frames, trace_frames = [], []
        for phase in range(6):
            clean = Image.new("RGB", (cell_width * 2, cell_height * 2), (228, 218, 198))
            trace = clean.copy()
            for index, profile in enumerate(PROFILES):
                cell_x, cell_y = placements[index]
                for x in range(cell_x + 20, cell_x + cell_width, 32):
                    ImageDraw.Draw(clean).line((x, cell_y + 18, x, cell_y + cell_height - 18), fill=(204, 194, 176))
                    ImageDraw.Draw(trace).line((x, cell_y + 18, x, cell_y + cell_height - 18), fill=(204, 194, 176))
                for y in range(cell_y + 18, cell_y + cell_height, 32):
                    ImageDraw.Draw(clean).line((cell_x + 18, y, cell_x + cell_width - 18, y), fill=(204, 194, 176))
                    ImageDraw.Draw(trace).line((cell_x + 18, y, cell_x + cell_width - 18, y), fill=(204, 194, 176))
                root_center = np.array((cell_x + cell_width * 0.5, cell_y + 310.0))
                root_zero = root_center - 2.5 * ROOT_STEP * vector
                root = root_zero + phase * ROOT_STEP * vector
                paste = (round(root[0] - 128), round(root[1] - 256))
                frame = Image.fromarray(all_frames[(profile.character, direction)][phase], "RGBA")
                clean.paste(frame, paste, frame)
                trace.paste(frame, paste, frame)
                support_leg = "left" if phase < 3 else "right"
                marker = all_markers[(profile.character, direction)][phase]
                anchor = marker_foot(marker, MARKER_COLORS[support_leg])
                point = (round(paste[0] + anchor[0]), round(paste[1] + anchor[1]))
                color = MARKER_COLORS[support_leg]
                draw = ImageDraw.Draw(trace)
                draw.ellipse((point[0] - 4, point[1] - 4, point[0] + 4, point[1] + 4), outline=color, width=2)
                draw.text((cell_x + 12, cell_y + 10), f"{profile.character}  P{phase}  support={support_leg}", fill=(28, 33, 44))
                ImageDraw.Draw(clean).text((cell_x + 12, cell_y + 10), f"{profile.character}  P{phase}", fill=(28, 33, 44))
            clean_frames.append(clean)
            trace_frames.append(trace)
        clean_frames[0].save(
            evidence / f"family_{direction}_world_motion.gif",
            save_all=True, append_images=clean_frames[1:], duration=165, loop=0, disposal=2,
        )
        trace_frames[0].save(
            evidence / f"family_{direction}_support_trace.gif",
            save_all=True, append_images=trace_frames[1:], duration=165, loop=0, disposal=2,
        )
        contact = Image.new("RGB", (1260, 740), (28, 33, 44))
        for phase, image in enumerate(clean_frames):
            reduced = image.resize((420, 370), Image.Resampling.LANCZOS)
            contact.paste(reduced, ((phase % 3) * 420, (phase // 3) * 370))
        contact.save(evidence / f"family_{direction}_world_motion_contact.png")


def direction_vector(direction: str) -> np.ndarray:
    source = {
        "west": "east", "southwest": "southeast", "northwest": "northeast",
    }.get(direction, direction)
    vector = VECTORS[source].copy()
    if direction in ("west", "southwest", "northwest"):
        vector[0] *= -1.0
    return vector


def support_metrics(direction: str, markers: list[np.ndarray]) -> tuple[float, float, list[list[float]]]:
    vector = direction_vector(direction)
    anchors = []
    for phase, marker in enumerate(markers):
        support_leg = "left" if phase < 3 else "right"
        anchor = marker_foot(marker, MARKER_COLORS[support_leg])
        anchors.append([round(float(anchor[0]), 6), round(float(anchor[1]), 6)])
    points = [np.asarray(anchor, np.float64) for anchor in anchors]
    left_world = [points[phase] + phase * ROOT_STEP * vector for phase in range(3)]
    right_world = [points[phase] + phase * ROOT_STEP * vector for phase in range(3, 6)]
    drift_left = max(abs(float(np.dot(point - left_world[0], vector))) for point in left_world)
    drift_right = max(abs(float(np.dot(point - right_world[0], vector))) for point in right_world)
    return drift_left, drift_right, anchors


def build_runtime_anchor_catalog(
    all_frames: dict[tuple[str, str], list[np.ndarray]],
    all_markers: dict[tuple[str, str], list[np.ndarray]],
) -> dict[str, object]:
    rows = []
    asset_sha256 = {}
    for profile in PROFILES:
        for direction in ALL_DIRECTIONS:
            _, _, anchors = support_metrics(direction, all_markers[(profile.character, direction)])
            rows.append(
                {
                    "character": profile.character,
                    "direction": direction,
                    "supportLegs": ["left", "left", "left", "right", "right", "right"],
                    "supportAnchors": [{"x": point[0], "y": point[1]} for point in anchors],
                }
            )
            for phase, frame in enumerate(all_frames[(profile.character, direction)]):
                name = f"{profile.character}_{direction}_walk_{phase}.png"
                image = Image.fromarray(frame, "RGBA")
                # Hash the decoded RGBA payload, not encoder-specific PNG bytes.  The independent
                # verifier repeats this check before Unity imports the exact frames used by Player QA.
                asset_sha256[name] = hashlib.sha256(image.tobytes()).hexdigest().upper()
    return {
        "schemaVersion": 1,
        "contract": "FC-FAMILY-LOCOMOTION-FOOT-ANCHORS-V1",
        "pixelsPerUnit": 180.0,
        "visualScale": 1.55,
        "strideWorld": 0.99380799,
        "rootStepPixels": ROOT_STEP,
        "maximumAuthoredSupportDriftPixels": 1.0,
        "maximumPlayerSupportDriftPixels": 4.0,
        "bodyOffsetsPixels": [{"x": float(point[0]), "y": float(point[1])} for point in BODY_OFFSETS],
        "rows": rows,
        "assetRgbaSha256": asset_sha256,
    }


def build_candidate(output: Path = OUTPUT) -> dict[str, object]:
    global OUTPUT
    OUTPUT = output.resolve()
    validate_manifest()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    metrics = []
    all_frames: dict[tuple[str, str], list[np.ndarray]] = {}
    all_markers: dict[tuple[str, str], list[np.ndarray]] = {}
    for profile in PROFILES:
        atlas = group_atlas(ATLAS_ROOT / profile.atlas_name, profile.atlas_rows)
        if profile.character == "player":
            atlas["east"] = player_east_parts()
        for direction in CANONICAL_DIRECTIONS:
            frames, markers, controls, drift_left, drift_right = calibrate_direction(profile, direction, atlas[direction])
            if max(drift_left, drift_right) > 1.0:
                raise ValueError(
                    f"{profile.character}/{direction}: support drift {drift_left:.3f}/{drift_right:.3f}px"
                )
            for phase, frame in enumerate(frames):
                components = alpha_components(frame)
                if not components or (len(components) > 1 and sum(components[1:]) > 12):
                    raise ValueError(f"{profile.character}/{direction}/{phase}: detached alpha {components}")
            save_direction(profile, direction, frames, markers)
            all_frames[(profile.character, direction)] = frames
            all_markers[(profile.character, direction)] = markers
            metrics.append(
                {
                    "character": profile.character,
                    "direction": direction,
                    "leftSupportDriftPx": round(drift_left, 6),
                    "rightSupportDriftPx": round(drift_right, 6),
                    "controls": [
                        {leg: [round(float(value), 4) for value in point] for leg, point in phase.items()}
                        for phase in controls
                    ],
                }
            )

        for source, mirrored in (("east", "west"), ("southeast", "southwest"), ("northeast", "northwest")):
            frames = mirror_rows(all_frames[(profile.character, source)])
            markers = mirror_rows(all_markers[(profile.character, source)])
            save_direction(profile, mirrored, frames, markers)
            all_frames[(profile.character, mirrored)] = frames
            all_markers[(profile.character, mirrored)] = markers
            drift_left, drift_right, _ = support_metrics(mirrored, markers)
            metrics.append(
                {
                    "character": profile.character,
                    "direction": mirrored,
                    "leftSupportDriftPx": round(drift_left, 6),
                    "rightSupportDriftPx": round(drift_right, 6),
                    "mirroredFrom": source,
                }
            )

    build_sheets(all_frames)
    build_contact_sheets(all_frames)
    build_world_motion_gifs(all_frames, all_markers)
    anchor_catalog = build_runtime_anchor_catalog(all_frames, all_markers)
    (OUTPUT / "family_foot_anchors_v1.json").write_text(
        json.dumps(anchor_catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    payload = {
        "schemaVersion": 1,
        "contract": "FC-FAMILY-LOCOMOTION-RIG-V1",
        "rootStepPixels": ROOT_STEP,
        "characters": len(PROFILES),
        "directions": len(ALL_DIRECTIONS),
        "frames": len(PROFILES) * len(ALL_DIRECTIONS) * 6,
        "maximumSupportDriftPx": max(
            max(item["leftSupportDriftPx"], item["rightSupportDriftPx"]) for item in metrics
        ),
        "rows": metrics,
        "sourceSha256": {
            path.name: hashlib.sha256(path.read_bytes()).hexdigest().upper()
            for path in sorted(ATLAS_ROOT.glob("*.png"))
        },
    }
    (OUTPUT / "family_rig_metrics.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (OUTPUT / "generation-report.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        "FAMILY_LOCOMOTION_RIG_V1: PASS | "
        f"characters=4 directions=8 frames=192 maxSupportDriftPx={payload['maximumSupportDriftPx']:.3f}"
    )
    return payload


def publish(output: Path = OUTPUT) -> None:
    output = output.resolve()
    backup = output / "BeforeRigPublish"
    for profile in PROFILES:
        runtime_root = (
            REPO / "Assets" / "Art" / "Characters" / ASSET_FOLDERS[profile.character] /
            "Pixel" / "HighMotion"
        )
        runtime_frames = runtime_root / "Frames"
        for direction in ALL_DIRECTIONS:
            for phase in range(6):
                name = f"{profile.character}_{direction}_walk_{phase}.png"
                source = output / "Candidate" / profile.character / "Frames" / name
                destination = runtime_frames / name
                if not source.is_file() or not destination.is_file():
                    raise FileNotFoundError(source if not source.is_file() else destination)
                backup_path = backup / destination.relative_to(REPO)
                backup_path.parent.mkdir(parents=True, exist_ok=True)
                if not backup_path.exists():
                    shutil.copyfile(destination, backup_path)
                shutil.copyfile(source, destination)
        for part in ("a", "b"):
            name = f"{profile.character}_pixel_walk8dir6_{part}_v1.png"
            source = output / "Candidate" / profile.character / name
            destination = runtime_root / name
            if not source.is_file() or not destination.is_file():
                raise FileNotFoundError(source if not source.is_file() else destination)
            backup_path = backup / destination.relative_to(REPO)
            backup_path.parent.mkdir(parents=True, exist_ok=True)
            if not backup_path.exists():
                shutil.copyfile(destination, backup_path)
            shutil.copyfile(source, destination)
        print(f"PUBLISHED {profile.character}: frames=48 sheets=2 .meta-preserved")
    anchor_source = output / "family_foot_anchors_v1.json"
    if not anchor_source.is_file():
        raise FileNotFoundError(anchor_source)
    RUNTIME_ANCHOR_RESOURCE.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(anchor_source, RUNTIME_ANCHOR_RESOURCE)
    print(f"PUBLISHED runtime anchor contract: {RUNTIME_ANCHOR_RESOURCE.relative_to(REPO)}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--publish-existing", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.publish_existing:
        build_candidate(args.output)
    if args.write or args.publish_existing:
        publish(args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
