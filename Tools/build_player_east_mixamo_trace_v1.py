from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


GREEN = (0, 255, 0, 255)
LEFT_COLOR = (0, 220, 255, 255)
RIGHT_COLOR = (255, 158, 0, 255)
JOINT_COLOR = (255, 255, 255, 255)
GROUND_COLOR = (70, 255, 100, 255)
PHASE_NAMES = [
    "left contact / right toe",
    "left load / right recovery",
    "left terminal / right low pass",
    "right contact / left toe",
    "right load / left recovery",
    "right terminal / left low pass",
]

STRIDE_WORLD = 0.99380799
SPRITE_PIXELS_PER_UNIT = 180.0
VISUAL_SCALE = 1.55
ROOT_ADVANCE_SOURCE_PX = STRIDE_WORLD / 6.0 * SPRITE_PIXELS_PER_UNIT / VISUAL_SCALE
CANONICAL_PELVIS_X = 128
GROUND_Y = 233

# Owner-neutral six-phase template. Left uses q[p], right uses q[(p + 3) % 6].
# q0/q1 lock the heel, q1/q2/q3 lock the toe, q4 is recovery, and q5 is
# the low forward pass. X values are rasterized from the production
# 19.234993 px/phase root advance; the resulting contact drift is <= 0.766 px.
PHASE_TEMPLATE = [
    {"knee": (143, 198), "ankle": (154, 222), "heel": (150, 233), "toe": (164, 230)},
    {"knee": (140, 199), "ankle": (135, 223), "heel": (131, 233), "toe": (145, 233)},
    {"knee": (134, 198), "ankle": (118, 219), "heel": (112, 226), "toe": (126, 233)},
    {"knee": (112, 198), "ankle": (98, 218), "heel": (92, 225), "toe": (106, 233)},
    {"knee": (128, 200), "ankle": (106, 213), "heel": (102, 219), "toe": (116, 222)},
    {"knee": (145, 194), "ankle": (139, 219), "heel": (135, 226), "toe": (149, 229)},
]


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


def lower_body_mask(frame: Image.Image, cut_y: int = 171) -> np.ndarray:
    alpha = np.asarray(frame.convert("RGBA"))[:, :, 3]
    binary = alpha > 0
    binary[:cut_y] = False
    mask = np.zeros_like(binary)
    for component in connected_components(binary):
        if len(component) < 20 or max(y for _, y in component) < 218:
            continue
        for x, y in component:
            mask[y, x] = True
    return mask


def waist_center(mask: np.ndarray) -> float:
    _, xs = np.nonzero(mask[171:180])
    if len(xs) == 0:
        return 128.0
    return float(xs.mean())


def shift_image(image: Image.Image, dx: int, dy: int = 0, fill=GREEN) -> Image.Image:
    shifted = Image.new("RGBA", image.size, fill)
    shifted.alpha_composite(image.convert("RGBA"), (dx, dy))
    return shifted


def clear_source_lower(frame: Image.Image) -> Image.Image:
    rgba = np.asarray(frame.convert("RGBA")).copy()
    mask = lower_body_mask(frame)
    center = waist_center(mask)
    flattened = Image.new("RGBA", frame.size, GREEN)
    flattened.alpha_composite(frame.convert("RGBA"))
    pixels = np.asarray(flattened).copy()
    pixels[mask] = GREEN
    aligned = shift_image(
        Image.fromarray(pixels, "RGBA"),
        round(CANONICAL_PELVIS_X - center),
    )
    return aligned


def screen_xy(pose: dict, key: str) -> tuple[float, float]:
    value = pose[key]["eastScreen"]
    return float(value["x"]), float(value["y"])


def target_points(pose: dict) -> dict[str, tuple[int, int]]:
    phase = int(pose["pose"])
    hip_y = [174, 175, 173, 174, 175, 173][phase]

    points: dict[str, tuple[int, int]] = {
        "pelvis": (CANONICAL_PELVIS_X, hip_y),
        "leftHip": (CANONICAL_PELVIS_X - 2, hip_y + 1),
        "rightHip": (CANONICAL_PELVIS_X + 2, hip_y + 1),
    }

    for owner, template_phase in (("left", phase), ("right", (phase + 3) % 6)):
        template = PHASE_TEMPLATE[template_phase]
        for joint in ("knee", "ankle", "heel", "toe"):
            points[owner + joint.capitalize()] = template[joint]

    return points


def draw_chain(
    draw: ImageDraw.ImageDraw,
    points: dict[str, tuple[int, int]],
    owner: str,
    color: tuple[int, int, int, int],
) -> None:
    chain = [
        points[owner + "Hip"],
        points[owner + "Knee"],
        points[owner + "Ankle"],
        points[owner + "Toe"],
    ]
    draw.line(chain, fill=color, width=8, joint="curve")
    for x, y in chain:
        draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=JOINT_COLOR, outline=color)
    heel = points[owner + "Heel"]
    toe = points[owner + "Toe"]
    draw.line((heel, toe), fill=color, width=5)
    draw.ellipse((heel[0] - 2, heel[1] - 2, heel[0] + 2, heel[1] + 2), fill=JOINT_COLOR)


def draw_locked_guide(
    source_frame: Image.Image,
    pose: dict,
) -> tuple[Image.Image, dict[str, tuple[int, int]]]:
    guide = clear_source_lower(source_frame)
    points = target_points(pose)
    draw = ImageDraw.Draw(guide)
    draw.line((76, GROUND_Y, 188, GROUND_Y), fill=GROUND_COLOR, width=1)
    draw_chain(draw, points, "right", RIGHT_COLOR)
    draw_chain(draw, points, "left", LEFT_COLOR)
    px, py = points["pelvis"]
    draw.ellipse((px - 4, py - 4, px + 4, py + 4), fill=JOINT_COLOR, outline=(0, 0, 0, 255))

    phase = int(pose["pose"])
    for owner, template_phase in (("left", phase), ("right", (phase + 3) % 6)):
        if template_phase == 0:
            contact = points[owner + "Heel"]
        elif template_phase in (1, 2, 3):
            contact = points[owner + "Toe"]
        else:
            continue
        draw.ellipse(
            (contact[0] - 3, GROUND_Y - 2, contact[0] + 3, GROUND_Y + 1),
            fill=GROUND_COLOR,
        )
    draw.line((206, 188, 228, 188), fill=(255, 255, 255, 255), width=2)
    draw.polygon([(228, 188), (220, 184), (220, 192)], fill=(255, 255, 255, 255))
    return guide, points


def raw_skeleton_panel(pose: dict) -> Image.Image:
    panel = Image.new("RGBA", (256, 256), (18, 22, 28, 255))
    draw = ImageDraw.Draw(panel)
    scale = 105.0
    hip = (128, 115)

    def raw(key: str) -> tuple[int, int]:
        x, y = screen_xy(pose, key)
        return round(hip[0] + x * scale), round(hip[1] - y * scale)

    for owner, color in (("right", RIGHT_COLOR), ("left", LEFT_COLOR)):
        chain = [hip, raw(owner + "Knee"), raw(owner + "Ankle"), raw(owner + "Toe")]
        draw.line(chain, fill=color, width=5, joint="curve")
        for x, y in chain:
            draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=JOINT_COLOR)
    draw.line((45, 230, 211, 230), fill=(90, 100, 115, 255), width=1)
    sample_index = pose.get("kshopResampledIndex24", pose.get("sourceSample24"))
    draw.text((8, 8), f"P{pose['pose']} K{sample_index}", fill=(255, 255, 255, 255))
    draw.text((8, 25), PHASE_NAMES[int(pose["pose"])], fill=(190, 200, 215, 255))
    return panel


def write_sheet(panels: list[Image.Image], path: Path) -> None:
    sheet = Image.new("RGBA", (768, 512), (0, 0, 0, 0))
    for index, panel in enumerate(panels):
        sheet.alpha_composite(panel, ((index % 3) * 256, (index // 3) * 256))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def flatten_on_green(frame: Image.Image) -> Image.Image:
    panel = Image.new("RGBA", frame.size, GREEN)
    panel.alpha_composite(frame.convert("RGBA"))
    return panel


def contact_lock_metrics(target_rows: list[dict]) -> dict:
    root_positions = [phase * ROOT_ADVANCE_SOURCE_PX for phase in range(7)]
    segments = {
        "leftHeelP0P1": [
            target_rows[0]["points"]["leftHeel"][0] + root_positions[0],
            target_rows[1]["points"]["leftHeel"][0] + root_positions[1],
        ],
        "leftToeP1P3": [
            target_rows[index]["points"]["leftToe"][0] + root_positions[index]
            for index in (1, 2, 3)
        ],
        "rightHeelP3P4": [
            target_rows[index]["points"]["rightHeel"][0] + root_positions[index]
            for index in (3, 4)
        ],
        "rightToeP4P6": [
            target_rows[index]["points"]["rightToe"][0] + root_positions[index]
            for index in (4, 5)
        ] + [target_rows[0]["points"]["rightToe"][0] + root_positions[6]],
    }
    drift_by_segment = {
        name: max(values) - min(values) for name, values in segments.items()
    }
    return {
        "worldContactXBySegment": segments,
        "driftPxBySegment": drift_by_segment,
        "maximumContactDriftPx": max(drift_by_segment.values()),
    }


def validate_contract(data: dict, target_rows: list[dict]) -> dict:
    if len(target_rows) != 6 or [row["pose"] for row in target_rows] != list(range(6)):
        raise ValueError("Expected exactly P0..P5 target rows")

    support_owners = [pose.get("expectedSupportLeg") for pose in data["poses"]]
    if support_owners != ["left", "left", "left", "right", "right", "right"]:
        raise ValueError(f"Unexpected support-owner order: {support_owners}")

    pelvis_x = [row["points"]["pelvis"][0] for row in target_rows]
    if max(pelvis_x) - min(pelvis_x) != 0 or pelvis_x[0] != CANONICAL_PELVIS_X:
        raise ValueError(f"Pelvis X is not locked: {pelvis_x}")

    for row in target_rows:
        for owner in ("left", "right"):
            points = row["points"]
            ankle_x = points[owner + "Ankle"][0]
            heel_x = points[owner + "Heel"][0]
            toe_x = points[owner + "Toe"][0]
            if not (toe_x > ankle_x and toe_x > heel_x):
                raise ValueError(
                    f"P{row['pose']} {owner} shoe does not face east: "
                    f"ankle={ankle_x}, heel={heel_x}, toe={toe_x}"
                )

    stance_left = [target_rows[index]["points"]["leftAnkle"][0] for index in (0, 1, 2, 3)]
    swing_left = [target_rows[index]["points"]["leftAnkle"][0] for index in (3, 4, 5, 0)]
    stance_right = [target_rows[index]["points"]["rightAnkle"][0] for index in (3, 4, 5, 0)]
    swing_right = [target_rows[index]["points"]["rightAnkle"][0] for index in (0, 1, 2, 3)]
    if not all(a > b for a, b in zip(stance_left, stance_left[1:])):
        raise ValueError(f"Left stance ankle is not monotonic -X: {stance_left}")
    if not all(a < b for a, b in zip(swing_left, swing_left[1:])):
        raise ValueError(f"Left swing ankle is not monotonic +X: {swing_left}")
    if not all(a > b for a, b in zip(stance_right, stance_right[1:])):
        raise ValueError(f"Right stance ankle is not monotonic -X: {stance_right}")
    if not all(a < b for a, b in zip(swing_right, swing_right[1:])):
        raise ValueError(f"Right swing ankle is not monotonic +X: {swing_right}")

    required_ground_points = (
        (0, "leftHeel"),
        (0, "rightToe"),
        (1, "leftHeel"),
        (1, "leftToe"),
        (2, "leftToe"),
        (3, "leftToe"),
        (3, "rightHeel"),
        (4, "rightHeel"),
        (4, "rightToe"),
        (5, "rightToe"),
    )
    for pose_index, point_name in required_ground_points:
        y = target_rows[pose_index]["points"][point_name][1]
        if y != GROUND_Y:
            raise ValueError(f"P{pose_index} {point_name} is not grounded: y={y}")

    metrics = contact_lock_metrics(target_rows)
    if metrics["maximumContactDriftPx"] > 1.0:
        raise ValueError(
            "Support-foot lock exceeds 1px: "
            f"{metrics['maximumContactDriftPx']:.6f}px"
        )
    metrics.update(
        {
            "pelvisXRangePx": max(pelvis_x) - min(pelvis_x),
            "leftStanceAnkleX": stance_left,
            "leftSwingAnkleX": swing_left,
            "rightStanceAnkleX": stance_right,
            "rightSwingAnkleX": swing_right,
        }
    )
    return metrics


def write_phase_table(
    path: Path,
    data: dict,
    target_rows: list[dict],
    validation: dict,
) -> None:
    maximum_contact_drift = validation["maximumContactDriftPx"]
    lines = [
        "# Player East Mixamo Trace — 6 Pose Contract",
        "",
        f"- Mixamo clip length: `{data['sourceClipLengthSeconds']:.6f}s`",
        f"- detected left-contact phase zero: `{data['phaseZeroSourceSeconds']:.6f}s`",
        "- KShopGo reference playback: `0.8s`, 30fps, samples `0/4/8/12/16/20`",
        "- left leg: cyan; right leg: orange; east: +X",
        f"- production stride: `{STRIDE_WORLD:.8f}` world unit; PPU `{SPRITE_PIXELS_PER_UNIT:.0f}`; visual scale `{VISUAL_SCALE:.2f}`",
        f"- root advance per pose: `{ROOT_ADVANCE_SOURCE_PX:.6f}` source px",
        f"- computed maximum heel/toe contact drift: `{maximum_contact_drift:.6f}px` (required `<=1px`)",
        "- q0->q1 locks the heel; q1->q2->q3 locks the toe; q4 recovery; q5 low pass",
        "",
        "| Pose | KShop ms | Support | Required event | Root px | Left H/A/T | Right H/A/T |",
        "| --- | ---: | --- | --- | ---: | --- | --- |",
    ]
    for pose, row in zip(data["poses"], target_rows):
        points = row["points"]
        lines.append(
            f"| P{pose['pose']} | {pose['kshopReferenceSeconds'] * 1000:.1f} | "
            f"{pose['expectedSupportLeg']} | {PHASE_NAMES[pose['pose']]} | "
            f"{pose['pose'] * ROOT_ADVANCE_SOURCE_PX:.3f} | "
            f"{points['leftHeel']}/{points['leftAnkle']}/{points['leftToe']} | "
            f"{points['rightHeel']}/{points['rightAnkle']}/{points['rightToe']} |"
        )
    lines.extend(
        [
            "",
            "Fail closed: no lower-body mirror, no shoe fragment move, no duplicated contact, "
            "and no art promotion before this owner/contact order passes in the east GIF.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def build(json_path: Path, source_dir: Path, output_dir: Path) -> dict:
    data = json.loads(json_path.read_text(encoding="utf-8"))
    if data.get("contract") != "FC-PLAYER-EAST-MIXAMO-MOTION-REFERENCE-V1":
        raise ValueError("Unexpected motion reference contract")
    poses = data["poses"]
    source_frames = [
        Image.open(source_dir / f"player_east_walk_{index}_v2.png").convert("RGBA")
        for index in range(6)
    ]

    guides: list[Image.Image] = []
    target_rows: list[dict] = []
    guides_dir = output_dir / "Guides"
    edit_guides_dir = output_dir / "EditGuides"
    guides_dir.mkdir(parents=True, exist_ok=True)
    edit_guides_dir.mkdir(parents=True, exist_ok=True)
    for index, (frame, pose) in enumerate(zip(source_frames, poses)):
        guide, points = draw_locked_guide(frame, pose)
        guide.save(guides_dir / f"player_east_walk_{index}_mixamo_guide.png")
        two_cell = Image.new("RGBA", (512, 256), GREEN)
        style_index = 0 if index in (0, 3) else 1
        two_cell.alpha_composite(flatten_on_green(source_frames[style_index]), (0, 0))
        two_cell.alpha_composite(guide, (256, 0))
        two_cell.save(edit_guides_dir / f"player_east_walk_{index}_two_cell_guide.png")
        guides.append(guide)
        target_rows.append(
            {
                "pose": index,
                "kshopResampledIndex24": index * 4,
                "rootUnwrappedSourcePx": index * ROOT_ADVANCE_SOURCE_PX,
                "points": points,
            }
        )

    validation = validate_contract(data, target_rows)

    write_sheet(guides, output_dir / "player-east-locked-skeleton-guide.png")
    write_sheet(
        [flatten_on_green(frame) for frame in source_frames],
        output_dir / "player-east-v3-style-reference.png",
    )
    write_sheet([raw_skeleton_panel(pose) for pose in poses], output_dir / "mixamo-raw-skeleton-6pose.png")
    write_phase_table(output_dir / "phase-contract.md", data, target_rows, validation)
    (output_dir / "target-joints.json").write_text(
        json.dumps(target_rows, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    return validation


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--json",
        type=Path,
        required=True,
        help="Unity exporter raw JSON; keep this input under ignored Artifacts, not ArtSources",
    )
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    validation = build(args.json, args.source_dir, args.output_dir)
    print(
        "PLAYER_EAST_MIXAMO_TRACE: PASS | "
        f"poses=6 rootAdvance={ROOT_ADVANCE_SOURCE_PX:.6f}px "
        f"maxContactDrift={validation['maximumContactDriftPx']:.6f}px"
    )


if __name__ == "__main__":
    main()
