from __future__ import annotations

import argparse
import csv
import math
from pathlib import Path
import shutil
from statistics import median

from PIL import Image, ImageDraw, ImageFont


BAR_COLOR = (51, 198, 145)
PANEL_BACKGROUND = (18, 30, 34)
TEXT = (235, 244, 242)
MUTED = (157, 188, 183)
PLAYER_ACCENT = (108, 194, 255)
FATHER_ACCENT = (255, 175, 104)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    name = "consolab.ttf" if bold else "consola.ttf"
    path = Path("C:/Windows/Fonts") / name
    if path.exists():
        return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def teal_components(image: Image.Image) -> list[tuple[int, int, int, int, int]]:
    rgb = image.convert("RGB")
    pixels = rgb.load()
    points = {
        (x, y)
        for y in range(rgb.height)
        for x in range(rgb.width)
        if pixels[x, y] == BAR_COLOR
    }
    components: list[tuple[int, int, int, int, int]] = []
    while points:
        seed = points.pop()
        stack = [seed]
        xs: list[int] = []
        ys: list[int] = []
        while stack:
            x, y = stack.pop()
            xs.append(x)
            ys.append(y)
            for neighbour in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbour in points:
                    points.remove(neighbour)
                    stack.append(neighbour)
        if len(xs) >= 30:
            components.append((min(xs), min(ys), max(xs), max(ys), len(xs)))
    return sorted(components)


def actor_bar_candidates(image: Image.Image) -> list[tuple[int, int]]:
    dynamic = []
    for left, top, right, bottom, count in teal_components(image):
        centre_x = (left + right) // 2
        centre_y = (top + bottom) // 2
        if 300 <= centre_x <= 980 and count >= 50:
            dynamic.append((centre_x, centre_y))
    dynamic.sort(key=lambda item: item[0])
    return dynamic


def father_shirt_bar_centre(image: Image.Image) -> tuple[int, int] | None:
    rgb = image.convert("RGB")
    pixels = rgb.load()
    points = {
        (x, y)
        for y in range(120, min(rgb.height, 600))
        for x in range(300, min(rgb.width, 981))
        if 35 <= pixels[x, y][1] <= 170
        and pixels[x, y][1] > pixels[x, y][0] + 15
        and pixels[x, y][2] > pixels[x, y][0] + 8
        and pixels[x, y] != BAR_COLOR
    }
    components: list[tuple[int, int, int, int, int]] = []
    while points:
        seed = points.pop()
        stack = [seed]
        xs: list[int] = []
        ys: list[int] = []
        while stack:
            x, y = stack.pop()
            xs.append(x)
            ys.append(y)
            for neighbour in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbour in points:
                    points.remove(neighbour)
                    stack.append(neighbour)
        if len(xs) >= 80:
            components.append((min(xs), min(ys), max(xs), max(ys), len(xs)))
    if not components:
        return None
    left, top, right, _, _ = max(components, key=lambda item: item[4])
    return (left + right) // 2, top - 31


def tracked_actor_bar_centres(
    frames: list[Image.Image],
) -> list[tuple[tuple[int, int], tuple[int, int]]]:
    tracked: list[tuple[tuple[int, int], tuple[int, int]]] = []
    previous: tuple[tuple[int, int], tuple[int, int]] | None = None
    for frame in frames:
        candidates = actor_bar_candidates(frame)
        if len(candidates) >= 2:
            current = (candidates[0], candidates[-1])
        elif len(candidates) == 1 and previous is not None:
            candidate = candidates[0]
            player_distance = abs(candidate[0] - previous[0][0]) + abs(candidate[1] - previous[0][1])
            father_distance = abs(candidate[0] - previous[1][0]) + abs(candidate[1] - previous[1][1])
            current = (candidate, previous[1]) if player_distance <= father_distance else (previous[0], candidate)
        elif not candidates and previous is not None:
            current = previous
        elif len(candidates) == 1:
            father = father_shirt_bar_centre(frame)
            if father is None or father[0] <= candidates[0][0]:
                raise RuntimeError(f"Cannot initialize two actor centres from {candidates}")
            current = (candidates[0], father)
        else:
            raise RuntimeError(f"Cannot initialize two actor health bars from {candidates}")
        tracked.append(current)
        previous = current
    return tracked


def crop_with_padding(
    image: Image.Image,
    box: tuple[int, int, int, int],
    fill: tuple[int, int, int] = PANEL_BACKGROUND,
) -> Image.Image:
    left, top, right, bottom = box
    result = Image.new("RGB", (right - left, bottom - top), fill)
    source_box = (
        max(0, left),
        max(0, top),
        min(image.width, right),
        min(image.height, bottom),
    )
    if source_box[2] > source_box[0] and source_box[3] > source_box[1]:
        result.paste(
            image.crop(source_box).convert("RGB"),
            (source_box[0] - left, source_box[1] - top),
        )
    return result


def actor_crop(image: Image.Image, centre: tuple[int, int], shoes: bool = False) -> Image.Image:
    x, bar_y = centre
    if shoes:
        return crop_with_padding(image, (x - 58, bar_y + 55, x + 58, bar_y + 130))
    return crop_with_padding(image, (x - 80, bar_y - 6, x + 80, bar_y + 134))


def labelled_panel(
    image: Image.Image,
    label: str,
    accent: tuple[int, int, int],
    scale: int = 2,
    frame_number: int | None = None,
) -> Image.Image:
    resized = image.resize(
        (image.width * scale, image.height * scale),
        Image.Resampling.NEAREST,
    )
    header = 42
    panel = Image.new("RGB", (resized.width, resized.height + header), PANEL_BACKGROUND)
    panel.paste(resized, (0, header))
    draw = ImageDraw.Draw(panel)
    draw.rectangle((0, 0, panel.width, 5), fill=accent)
    text = label if frame_number is None else f"{label}  frame {frame_number:03d}"
    draw.text((12, 10), text, fill=TEXT, font=font(18, True))
    return panel


def build_gifs(frames: list[Image.Image], output: Path) -> None:
    map_frames = [
        frame.resize((960, 540), Image.Resampling.LANCZOS).quantize(colors=128)
        for frame in frames
    ]
    map_frames[0].save(
        output / "player-father-independent-full-map.gif",
        save_all=True,
        append_images=map_frames[1:],
        duration=42,
        loop=0,
        disposal=1,
        optimize=True,
    )

    zoom_frames: list[Image.Image] = []
    centres = tracked_actor_bar_centres(frames)
    for index, frame in enumerate(frames):
        player_centre, father_centre = centres[index]
        player = labelled_panel(actor_crop(frame, player_centre), "PLAYER", PLAYER_ACCENT, 2, index)
        father = labelled_panel(actor_crop(frame, father_centre), "FATHER", FATHER_ACCENT, 2, index)
        canvas = Image.new(
            "RGB",
            (player.width + father.width + 24, max(player.height, father.height)),
            PANEL_BACKGROUND,
        )
        canvas.paste(player, (0, 0))
        canvas.paste(father, (player.width + 24, 0))
        zoom_frames.append(canvas.quantize(colors=128))
    zoom_frames[0].save(
        output / "player-father-independent-zoom-walk.gif",
        save_all=True,
        append_images=zoom_frames[1:],
        duration=42,
        loop=0,
        disposal=1,
        optimize=True,
    )


def contact_sheet(
    panels: list[Image.Image],
    title: str,
    output_path: Path,
    columns: int = 5,
) -> None:
    rows = (len(panels) + columns - 1) // columns
    header = 54
    gap = 8
    panel_width = max(panel.width for panel in panels)
    panel_height = max(panel.height for panel in panels)
    sheet = Image.new(
        "RGB",
        (
            columns * panel_width + (columns + 1) * gap,
            header + rows * panel_height + (rows + 1) * gap,
        ),
        PANEL_BACKGROUND,
    )
    draw = ImageDraw.Draw(sheet)
    draw.text((14, 13), title, fill=TEXT, font=font(22, True))
    for index, panel in enumerate(panels):
        x = gap + (index % columns) * (panel_width + gap)
        y = header + gap + (index // columns) * (panel_height + gap)
        sheet.paste(panel, (x, y))
    sheet.save(output_path, optimize=True)


def build_all_frame_sheets(frames: list[Image.Image], output: Path) -> None:
    centres = tracked_actor_bar_centres(frames)
    for start in range(0, len(frames), 15):
        subset = frames[start : start + 15]
        end = start + len(subset) - 1
        suffix = f"{start:03d}-{end:03d}"
        map_panels = []
        player_panels = []
        father_panels = []
        shoe_panels = []
        for offset, frame in enumerate(subset):
            index = start + offset
            player_centre, father_centre = centres[index]
            map_thumb = frame.resize((320, 180), Image.Resampling.LANCZOS)
            map_panels.append(labelled_panel(map_thumb, "MAP", MUTED, 1, index))
            player_panels.append(
                labelled_panel(actor_crop(frame, player_centre), "PLAYER", PLAYER_ACCENT, 2, index)
            )
            father_panels.append(
                labelled_panel(actor_crop(frame, father_centre), "FATHER", FATHER_ACCENT, 2, index)
            )
            player_shoe = actor_crop(frame, player_centre, shoes=True).resize(
                (232, 150), Image.Resampling.NEAREST
            )
            father_shoe = actor_crop(frame, father_centre, shoes=True).resize(
                (232, 150), Image.Resampling.NEAREST
            )
            shoe_pair = Image.new("RGB", (472, 150), PANEL_BACKGROUND)
            shoe_pair.paste(player_shoe, (0, 0))
            shoe_pair.paste(father_shoe, (240, 0))
            shoe_panels.append(labelled_panel(shoe_pair, "PLAYER SHOES | FATHER SHOES", MUTED, 1, index))
        contact_sheet(map_panels, f"FULL MAP · ALL FRAMES {suffix}", output / f"map-all-{suffix}.png")
        contact_sheet(
            player_panels,
            f"PLAYER CLOSE · ALL FRAMES {suffix}",
            output / f"player-close-all-{suffix}.png",
        )
        contact_sheet(
            father_panels,
            f"FATHER CLOSE · ALL FRAMES {suffix}",
            output / f"father-close-all-{suffix}.png",
        )
        contact_sheet(
            shoe_panels,
            f"ACTUAL RENDERED SHOES + TILE LINES · ALL FRAMES {suffix}",
            output / f"shoes-all-{suffix}.png",
            columns=3,
        )


def build_turn_media(artifact: Path, output: Path) -> None:
    paths = sorted((artifact / "turn-frames").glob("turn-*.png"))
    if not paths:
        return
    frames = [Image.open(path).convert("RGB") for path in paths]
    centres: list[tuple[int, int]] = []
    for frame in frames:
        rgb = frame.convert("RGB")
        pixels = rgb.load()
        points = {
            (x, y)
            for y in range(120, min(rgb.height, 600))
            for x in range(300, min(rgb.width, 981))
            if 35 <= pixels[x, y][1] <= 170
            and pixels[x, y][1] > pixels[x, y][0] + 15
            and pixels[x, y][2] > pixels[x, y][0] + 8
        }
        clothing_components: list[tuple[int, int, int, int, int]] = []
        while points:
            seed = points.pop()
            stack = [seed]
            xs: list[int] = []
            ys: list[int] = []
            while stack:
                x, y = stack.pop()
                xs.append(x)
                ys.append(y)
                for neighbour in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if neighbour in points:
                        points.remove(neighbour)
                        stack.append(neighbour)
            if len(xs) >= 100:
                clothing_components.append(
                    (min(xs), min(ys), max(xs), max(ys), len(xs))
                )
        if not clothing_components:
            raise RuntimeError("Turn frame has no Father teal-shirt component")
        left, top, right, _, _ = max(clothing_components, key=lambda item: item[4])
        centres.append(((left + right) // 2, top - 34))

    map_frames = [
        frame.resize((960, 540), Image.Resampling.LANCZOS).quantize(colors=128)
        for frame in frames
    ]
    map_frames[0].save(
        output / "father-whole-body-turn-full-map.gif",
        save_all=True,
        append_images=map_frames[1:],
        duration=42,
        loop=0,
        disposal=1,
        optimize=True,
    )
    close_frames = [
        labelled_panel(actor_crop(frame, centre), "FATHER WHOLE-BODY TURN", FATHER_ACCENT, 3, index)
        .quantize(colors=128)
        for index, (frame, centre) in enumerate(zip(frames, centres))
    ]
    close_frames[0].save(
        output / "father-whole-body-turn-close.gif",
        save_all=True,
        append_images=close_frames[1:],
        duration=42,
        loop=0,
        disposal=1,
        optimize=True,
    )
    for start in range(0, len(frames), 16):
        subset = frames[start : start + 16]
        subset_centres = centres[start : start + 16]
        end = start + len(subset) - 1
        suffix = f"{start:03d}-{end:03d}"
        map_panels = [
            labelled_panel(
                frame.resize((320, 180), Image.Resampling.LANCZOS),
                "TURN MAP",
                MUTED,
                1,
                start + offset,
            )
            for offset, frame in enumerate(subset)
        ]
        close_panels = [
            labelled_panel(
                actor_crop(frame, centre),
                "FATHER TURN",
                FATHER_ACCENT,
                2,
                start + offset,
            )
            for offset, (frame, centre) in enumerate(zip(subset, subset_centres))
        ]
        contact_sheet(
            map_panels,
            f"WHOLE-BODY TURN MAP · ALL FRAMES {suffix}",
            output / f"turn-map-all-{suffix}.png",
            columns=4,
        )
        contact_sheet(
            close_panels,
            f"WHOLE-BODY TURN CLOSE · ALL FRAMES {suffix}",
            output / f"turn-close-all-{suffix}.png",
            columns=4,
        )


def read_ratio(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            result[key] = value
    return result


def median_range(rows: list[dict[str, str]], field: str) -> str:
    values = sorted(float(row[field]) for row in rows)
    mid = median(values)
    if all(value.is_integer() for value in values):
        return f"{int(values[0])}/{int(mid)}/{int(values[-1])}"
    return f"{values[0]:.4f}/{mid:.4f}/{values[-1]:.4f}"


def central_actor_crop(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGB")
    centres = [
        ((left + right) // 2, (top + bottom) // 2)
        for left, top, right, bottom, count in teal_components(image)
        if 480 <= (left + right) // 2 <= 800 and count >= 50
    ]
    if len(centres) != 1:
        raise RuntimeError(f"Expected one central ratio actor in {path.name}, got {centres}")
    return actor_crop(image, centres[0]).resize((480, 420), Image.Resampling.NEAREST)


def read_result(path: Path) -> dict[str, str]:
    """Key/value lines of the runtime receipt (player-father-3d-interaction-result.txt)."""
    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            values[key.strip()] = value.strip()
    return values


def split_pair(value: str, count: int) -> list[str]:
    parts = value.split("/")
    if len(parts) != count:
        raise RuntimeError(f"Expected {count} slash-separated values, got {value!r}")
    return parts


def build_ratio_sheet(artifact: Path, output: Path) -> None:
    ratio = read_ratio(artifact / "father-player-same-tile-pixel-ratio.txt")
    with (artifact / "player-father-rendered-shoe-pixel-tile-trace.csv").open(
        encoding="utf-8", newline=""
    ) as handle:
        trace = list(csv.DictReader(handle))
    player_rows = [row for row in trace if row["actor"] == "player"]
    father_rows = [row for row in trace if row["actor"] == "father"]
    result = read_result(artifact / "player-father-3d-interaction-result.txt")
    mesh_clearance = split_pair(result["walkGroundClearanceMeshY"], 4)
    foot_error = split_pair(result["footMidpointTilePixelErrorMedianMax"], 4)
    line_touches = split_pair(result["plantedShoeTileLineTouchFrames"], 2)
    contact_samples = split_pair(result["plantedFootContactSamples"], 2)
    player_crop = central_actor_crop(artifact / "ratio-player-same-tile.png")
    father_crop = central_actor_crop(artifact / "ratio-father-same-tile.png")
    width = 1280
    height = 1200
    sheet = Image.new("RGB", (width, height), PANEL_BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    draw.text((32, 24), "PLAYER / FATHER · SAME CAMERA · SAME LIGHT · SAME TILE", fill=TEXT, font=font(28, True))
    draw.text((32, 62), "1280×720 source pixels · productionEligible=false · user approval required", fill=MUTED, font=font(18))
    sheet.paste(player_crop, (70, 112))
    sheet.paste(father_crop, (730, 112))
    draw.rectangle((70, 102, 550, 110), fill=PLAYER_ACCENT)
    draw.rectangle((730, 102, 1210, 110), fill=FATHER_ACCENT)
    draw.text((82, 124), "PLAYER", fill=TEXT, font=font(24, True))
    draw.text((742, 124), "FATHER", fill=TEXT, font=font(24, True))

    columns = [60, 470, 710, 950]
    headers = ["MEASUREMENT", "PLAYER", "FATHER", "FATHER / PLAYER"]
    for x, header in zip(columns, headers):
        draw.text((x, 570), header, fill=MUTED, font=font(18, True))
    rows = [
        ("same-tile total H", ratio["playerRenderedBounds"].split("x")[1], ratio["fatherRenderedBounds"].split("x")[1], ratio["fatherToPlayerHeightRatio"]),
        ("same-tile head W×H", ratio["playerHeadBounds"], ratio["fatherHeadBounds"], f"H {ratio['fatherToPlayerHeadHeightRatio']} / W {ratio['fatherToPlayerHeadWidthRatio']}"),
        ("head : total H", ratio["playerHeadToHeightRatio"], ratio["fatherHeadToHeightRatio"], "—"),
        ("shoulder / torso W", ratio["playerShoulderTorsoWidths"], ratio["fatherShoulderTorsoWidths"], f"{ratio['fatherToPlayerShoulderWidthRatio']} / {ratio['fatherToPlayerTorsoWidthRatio']}"),
        ("leg W×H", ratio["playerLegBounds"], ratio["fatherLegBounds"], f"H {ratio['fatherToPlayerLegHeightRatio']}"),
        ("shoe W×H / pixels", ratio["playerShoeBoundsPixels"], ratio["fatherShoeBoundsPixels"], ratio["fatherToPlayerShoeAreaRatio"]),
        ("walk lowest sole height med/min", f"{mesh_clearance[0]}/{mesh_clearance[1]}", f"{mesh_clearance[2]}/{mesh_clearance[3]}", f"Δ {float(mesh_clearance[2]) - float(mesh_clearance[0]):+.4f}"),
        ("walk foot-midpoint tile error med/max px", f"{foot_error[0]}/{foot_error[1]}", f"{foot_error[2]}/{foot_error[3]}", "—"),
        ("planted frames touching a tile line", f"{line_touches[0]}/{contact_samples[0]}", f"{line_touches[1]}/{contact_samples[1]}", "—"),
        ("static shoe px centroid (info)", ratio["playerShoeCentroidPx"], ratio["fatherShoeCentroidPx"], f"Δ {ratio['sameTileShoeCentroidDeltaPx']}"),
        ("silhouette pixels", ratio["playerSilhouettePixels"], ratio["fatherSilhouettePixels"], ratio["fatherToPlayerSilhouetteAreaRatio"]),
        ("screen occupation %", ratio["playerScreenOccupationPercent"], ratio["fatherScreenOccupationPercent"], "—"),
        ("walk total H min/med/max", median_range(player_rows, "rendered_height"), median_range(father_rows, "rendered_height"), "—"),
        ("walk head H min/med/max", median_range(player_rows, "head_height"), median_range(father_rows, "head_height"), "—"),
        ("walk head W min/med/max", median_range(player_rows, "head_width"), median_range(father_rows, "head_width"), "—"),
        ("walk torso W min/med/max", median_range(player_rows, "torso_width"), median_range(father_rows, "torso_width"), "—"),
        ("walk leg H min/med/max", median_range(player_rows, "leg_height"), median_range(father_rows, "leg_height"), "—"),
        ("walk silhouette min/med/max", median_range(player_rows, "silhouette_pixels"), median_range(father_rows, "silhouette_pixels"), "—"),
    ]
    y = 610
    for label, player, father, ratio_value in rows:
        draw.line((50, y - 8, 1230, y - 8), fill=(48, 72, 75), width=1)
        draw.text((columns[0], y), label, fill=TEXT, font=font(17))
        draw.text((columns[1], y), player, fill=PLAYER_ACCENT, font=font(17))
        draw.text((columns[2], y), father, fill=FATHER_ACCENT, font=font(17))
        draw.text((columns[3], y), ratio_value, fill=TEXT, font=font(17))
        y += 35
    sheet.save(output / "father-player-same-tile-ratio-sheet.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--artifact", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    frame_paths = sorted((args.artifact / "approach-frames").glob("approach-*.png"))
    if len(frame_paths) < 24:
        raise RuntimeError(f"Expected a full walk, found {len(frame_paths)} frames")
    frames = [Image.open(path).convert("RGB") for path in frame_paths]
    build_gifs(frames, args.output)
    build_all_frame_sheets(frames, args.output)
    build_turn_media(args.artifact, args.output)
    build_ratio_sheet(args.artifact, args.output)
    for evidence_name in (
        "father-player-same-tile-pixel-ratio.txt",
        "player-father-3d-interaction-final.txt",
        "player-father-3d-interaction-result.txt",
        "player-father-rendered-shoe-pixel-tile-trace.csv",
    ):
        source = args.artifact / evidence_name
        if not source.is_file():
            raise RuntimeError(f"Missing required evidence file: {source}")
        shutil.copy2(source, args.output / evidence_name)
    print(f"frames={len(frames)}")
    print(f"output={args.output}")


if __name__ == "__main__":
    main()
