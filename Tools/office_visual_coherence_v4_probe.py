"""Office Visual Coherence V4 - Unity-free composite probe.

Reproduces the Starter Office runtime composite from the same semantic sources Unity uses
(StarterOfficeV1.asset, OfficeFurnitureVisualCatalog.asset, OfficeCharacterSeatPoseCatalog.asset,
the runtime PNGs) so seated-composite geometry can be measured and reviewed on a machine that
has no Unity 6000.3.21f1 install.

Mirrored runtime rules
  OfficeGridTilemapPresenter : isometric basis, cell 320x160 px, PPU 180
  OfficeGridFurniturePresenter: sprite pivot == groundAnchorPx, VisualRoot scale == uniformScale
  OfficeGridCharacterMover    : UniformVisualScale, sortingOrder = 5000 - round(worldY * 100)
  OfficeRuntimeAgent          : VisualRoot offset pins the seat contact onto the chair cushion
  WorkstationService          : occupant sorts one step in front of its chair, furniture untouched

Nothing here writes to the project; it only reads assets and writes Artifacts/OfficeVisualCoherenceV4.

    python Tools/office_visual_coherence_v4_probe.py
"""
from __future__ import annotations

import hashlib
import math
import os
import re
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Art")
AUTHORING = os.path.join(
    ROOT, "Assets", "FamilyCompany", "Presentation.Unity", "OfficeGrid", "Authoring")
LAYOUT_ASSET = os.path.join(
    ROOT, "Assets", "FamilyCompany", "Content", "Resources", "OfficeLayouts", "StarterOfficeV1.asset")
OUT = os.path.join(ROOT, "Artifacts", "OfficeVisualCoherenceV4")

PPU = 180.0
TILE_W, TILE_H = 320.0, 160.0
BASIS_X = (TILE_W / 2.0, TILE_H / 2.0)
BASIS_Y = (-TILE_W / 2.0, TILE_H / 2.0)
RUNTIME_CHARACTER_SCALE = 1.69          # the pre-V4 constant, kept to render the "before" state
SHIPPED_CHARACTER_SCALE = 1.55          # OfficeGridCharacterMover.UniformVisualScale
MEMBERS = ("player", "older_sister", "father", "mother")
CHARACTER_CANVAS = 256
MODEL_UPPER_BODY_REDRAW = True
PROTECTION_BELOW_PELVIS_PX = 12
UNMAPPED_FURNITURE_KINDS = set()
_UPPER_BODY_CACHE = {}

SEAT_DIRECTION = "northwest"            # SafeStaticWork direction 3

# Stage A (blind) anatomy read straight off the PNG silhouettes with no desk/chair vector in view.
# These are PROPOSALS awaiting human approval - nothing here is written back into the catalog.
PROPOSED_ANATOMY = {
    #            seatContact (butt underside)   primaryHandContact
    "player": ((148.0, 62.0), (80.0, 101.0)),
    "older_sister": ((148.0, 76.0), (75.0, 108.0)),
    "father": ((146.0, 63.0), (80.0, 104.0)),
    "mother": ((143.0, 52.0), (72.0, 88.0)),
}
CHARACTER_DIR = {
    "player": os.path.join(ART, "Characters", "Player", "Pixel", "OfficeSeatingV1", "Frames"),
    "older_sister": os.path.join(ART, "Characters", "Family", "OlderSister", "Pixel", "OfficeSeatingV1", "Frames"),
    "father": os.path.join(ART, "Characters", "Family", "Father", "Pixel", "OfficeSeatingV1", "Frames"),
    "mother": os.path.join(ART, "Characters", "Family", "Mother", "Pixel", "OfficeSeatingV1", "Frames"),
}
FLOOR_TILES = [
    os.path.join(ART, "Office", "Tiles", "Floor", "office_floor_wood_a_v1.png"),
    os.path.join(ART, "Office", "Tiles", "Floor", "office_floor_wood_b_v1.png"),
    os.path.join(ART, "Office", "Tiles", "Floor", "office_floor_wood_c_v1.png"),
]
FURNITURE_PNG = {
    "desk_with_pc": "office_workstation_v4.png",
    "swivel_chair": "office_swivel_chair_v3.png",
    "reception_counter": "office_reception_counter_v2.png",
    "meeting_table": "office_meeting_table_v2.png",
    "document_bookcase": "office_document_bookcase_v2.png",
    "fax_copier": "office_fax_copier_v2.png",
    "water_dispenser": "office_water_dispenser_v2.png",
    "sofa": "office_sofa_v2.png",
    "coffee_table": "office_coffee_table_v2.png",
    "potted_plant": "office_potted_plant_v2.png",
    "partition": "office_partition_v2.png",
    "filing_cabinet": "office_filing_cabinet_v2.png",
}
FURNITURE_FRONT_PNG = {
    "desk_with_pc": "office_workstation_front_v4.png",
    "swivel_chair": "office_swivel_chair_front_v3.png",
}
FURNITURE_ROOT = os.path.join(ART, "Office", "Tiles", "Furniture", "Runtime")


# --------------------------------------------------------------------------- asset readers
def _vec(text):
    m = re.search(r"\{x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+)\}", text)
    return (float(m.group(1)), float(m.group(2))) if m else None


def read_furniture_catalog():
    path = os.path.join(AUTHORING, "OfficeFurnitureVisualCatalog.asset")
    text = open(path, encoding="utf-8").read()
    blocks = re.split(r"\n  - kindId: ", text)[1:]
    result = {}
    for block in blocks:
        kind = block.splitlines()[0].strip()
        poly = [_vec(line) for line in re.findall(r"    - \{x:.*?\}", block)]
        entry = dict(
            kindId=kind,
            facing=int(re.search(r"facing: (-?\d+)", block).group(1)),
            ground=_vec(re.search(r"groundAnchorPx: .*", block).group(0)),
            sort=_vec(re.search(r"sortAnchorPx: .*", block).group(0)),
            seat=_vec(re.search(r"seatAnchorPx: .*", block).group(0)),
            work=_vec(re.search(r"workSurfaceAnchorPx: .*", block).group(0)),
            opseat=_vec(re.search(r"operatorSeatSocketPx: .*", block).group(0)),
            scale=float(re.search(r"uniformScale: ([\d.]+)", block).group(1)),
            has_seat=re.search(r"hasSeatAnchor: (\d)", block).group(1) == "1",
            has_work=re.search(r"hasWorkSurfaceAnchor: (\d)", block).group(1) == "1",
            has_opseat=re.search(r"hasOperatorSeatSocket: (\d)", block).group(1) == "1",
            front_when_occupied=re.search(r"frontOverlayWhenOccupied: (\d)", block).group(1) == "1",
            footprint=poly,
        )
        result[kind] = entry
    return result


def read_pose_catalog():
    path = os.path.join(AUTHORING, "OfficeCharacterSeatPoseCatalog.asset")
    text = open(path, encoding="utf-8").read()
    blocks = re.split(r"\n  - memberId: ", text)[1:]
    result = {}
    for block in blocks:
        member = block.splitlines()[0].strip()
        clip = int(re.search(r"clip: (\d+)", block).group(1))
        frame = int(re.search(r"frameIndex: (\d+)", block).group(1))
        # This diagnostic renders one representative Work frame.  The catalog
        # contains SitDown, Work, and StandUp rows for every member; allowing a
        # later StandUp row to overwrite the Work calibration made the reported
        # Sprite hash say NO even when the catalog was correct.
        if clip != 1 or frame != 3:
            continue
        result[member] = dict(
            direction=int(re.search(r"directionIndex: (\d+)", block).group(1)),
            clip=clip,
            frame=frame,
            pelvis=_vec(re.search(r"pelvisAnchorPx: .*", block).group(0)),
            hand=_vec(re.search(r"deskInteractionAnchorPx: .*", block).group(0)),
            scale=float(re.search(r"uniformScale: ([\d.]+)", block).group(1)),
            approved=re.search(r"humanApproved: (\d)", block).group(1) == "1",
            sha=re.search(r"sourceSpriteSha256: (\w+)", block).group(1),
        )
    missing = sorted(set(MEMBERS) - set(result))
    if missing:
        raise RuntimeError("Missing representative Work[3] pose profiles: " + ", ".join(missing))
    return result


def read_layout():
    text = open(LAYOUT_ASSET, encoding="utf-8").read()
    width = int(re.search(r"\n  width: (\d+)", text).group(1))
    height = int(re.search(r"\n  height: (\d+)", text).group(1))
    furniture = []
    for block in re.split(r"\n  - furnitureId: ", text.split("\n  seats:")[0])[1:]:
        lines = block.splitlines()
        furniture.append(dict(
            id=lines[0].strip(),
            kind=re.search(r"kindId: (\S+)", block).group(1),
            x=int(re.search(r"originX: (-?\d+)", block).group(1)),
            y=int(re.search(r"originY: (-?\d+)", block).group(1)),
            w=int(re.search(r"\n    width: (\d+)", block).group(1)),
            h=int(re.search(r"\n    height: (\d+)", block).group(1)),
            px2=int(re.search(r"placementX2: (-?\d+)", block).group(1)),
            py2=int(re.search(r"placementY2: (-?\d+)", block).group(1)),
            facing=int(re.search(r"facing: (-?\d+)", block).group(1)),
            blocking=re.search(r"blocksMovement: (\d)", block).group(1) == "1",
        ))
    seats = []
    seat_text = text.split("\n  seats:")[1] if "\n  seats:" in text else ""
    for block in re.split(r"\n  - seatId: ", seat_text)[1:]:
        seats.append(dict(
            id=block.splitlines()[0].strip(),
            chair=re.search(r"chairFurnitureId: (\S+)", block).group(1),
            desk=re.search(r"workSurfaceFurnitureId: (\S*)", block).group(1),
            cell=(int(re.search(r"cellX: (-?\d+)", block).group(1)),
                  int(re.search(r"cellY: (-?\d+)", block).group(1))),
            approach=(int(re.search(r"approachX: (-?\d+)", block).group(1)),
                      int(re.search(r"approachY: (-?\d+)", block).group(1))),
            operator=(int(re.search(r"operatorX2: (-?\d+)", block).group(1)),
                      int(re.search(r"operatorY2: (-?\d+)", block).group(1))),
            facing=int(re.search(r"facing: (-?\d+)", block).group(1)),
        ))
    return dict(width=width, height=height, furniture=furniture, seats=seats)


# --------------------------------------------------------------------------- geometry
def subcell_px(x2, y2):
    return (BASIS_X[0] * x2 * 0.5 + BASIS_Y[0] * y2 * 0.5,
            BASIS_X[1] * x2 * 0.5 + BASIS_Y[1] * y2 * 0.5)


def cell_px(x, y):
    return subcell_px(x * 2, y * 2)


def sprite_point(root, pivot, point, scale=1.0):
    return (root[0] + (point[0] - pivot[0]) * scale, root[1] + (point[1] - pivot[1]) * scale)


def sorting_order(world_px_y):
    return 5000 - round((world_px_y / PPU) * 100)


class Scene:
    """World-px canvas with y-up world coordinates."""

    def __init__(self, bounds, background=(163, 209, 209, 255), pad=40):
        x0, y0, x1, y1 = bounds
        self.origin = (x0 - pad, y0 - pad)
        self.size = (int(x1 - x0 + pad * 2), int(y1 - y0 + pad * 2))
        self.im = Image.new("RGBA", self.size, background)
        self.layers = []

    def to_image(self, world):
        return (world[0] - self.origin[0], self.size[1] - (world[1] - self.origin[1]))

    def add(self, png, root, pivot, scale, order, key=0):
        self.layers.append((order, key, png, root, pivot, scale))

    def draw(self, only=None):
        for order, key, png, root, pivot, scale in sorted(self.layers, key=lambda t: (t[0], t[1])):
            if only is not None and png not in only:
                continue
            im = Image.open(png).convert("RGBA")
            if abs(scale - 1.0) > 1e-6:
                im = im.resize((round(im.width * scale), round(im.height * scale)), Image.NEAREST)
            bottom_left = (root[0] - pivot[0] * scale, root[1] - pivot[1] * scale)
            x, y = self.to_image(bottom_left)
            self.im.alpha_composite(im, (round(x), round(y) - im.height))
        return self.im

    def cross(self, world, color, label=None, size=12):
        d = ImageDraw.Draw(self.im)
        x, y = self.to_image(world)
        d.line([(x - size, y), (x + size, y)], fill=color, width=2)
        d.line([(x, y - size), (x, y + size)], fill=color, width=2)
        if label:
            d.text((x + size + 2, y - 6), label, fill=color)


# --------------------------------------------------------------------------- silhouette metrics
def opaque_rows(image):
    alpha = image.split()[-1].load()
    w, h = image.size
    return [[x for x in range(w) if alpha[x, h - 1 - y] > 8] for y in range(h)]


def silhouette(path):
    im = Image.open(path).convert("RGBA")
    rows = opaque_rows(im)
    occupied = [(y, xs) for y, xs in enumerate(rows) if xs]
    xs_all = [x for _, xs in occupied for x in xs]
    lowest, top = occupied[0][0], occupied[-1][0]
    x0, x1 = min(xs_all), max(xs_all)
    rear_from = x1 - round((x1 - x0) * 0.22)
    seat_candidate = None
    for y, xs in occupied:
        band = [x for x in xs if x >= rear_from]
        if band:
            seat_candidate = (sum(band) / len(band), y)
            break
    head_rows = occupied[-6:]
    head = (sum(sum(xs) / len(xs) for _, xs in head_rows) / len(head_rows), top)
    return dict(size=im.size, bbox=(x0, lowest, x1, top), area=sum(len(xs) for _, xs in occupied),
                seat_candidate=seat_candidate, head=head, rows=rows)


def sha256(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest().upper()


def frame_path(member, clip, frame):
    return os.path.join(CHARACTER_DIR[member], f"{member}_{SEAT_DIRECTION}_{clip}_{frame}.png")


def upper_body_frame_path(member, clip, frame, pelvis_anchor_y):
    """Mirror OfficeSeatedUpperBodyProtectionRules on the original full canvas."""
    cutoff_source_y = max(
        0,
        min(CHARACTER_CANVAS - 1, math.floor(pelvis_anchor_y) - PROTECTION_BELOW_PELVIS_PX),
    )
    key = (member, clip, frame, cutoff_source_y)
    cached = _UPPER_BODY_CACHE.get(key)
    if cached:
        return cached
    source = Image.open(frame_path(member, clip, frame)).convert("RGBA")
    pixels = source.load()
    first_lower_row = source.height - cutoff_source_y
    for y in range(first_lower_row, source.height):
        for x in range(source.width):
            pixels[x, y] = (0, 0, 0, 0)
    path = os.path.join(
        OUT,
        f"_runtime-upper-{member}-{clip}-{frame}-cutoff-{cutoff_source_y}.png",
    )
    source.save(path)
    _UPPER_BODY_CACHE[key] = path
    return path


def validate_protection_constant():
    path = os.path.join(
        ROOT,
        "Assets",
        "FamilyCompany",
        "Presentation.Unity",
        "OfficeRuntime",
        "OfficeSeatedUpperBodyProtectionRules.cs",
    )
    text = open(path, encoding="utf-8").read()
    match = re.search(r"ProtectionBelowPelvisPx\s*=\s*(\d+)", text)
    if not match:
        raise RuntimeError("Could not read ProtectionBelowPelvisPx from runtime source.")
    runtime_value = int(match.group(1))
    if runtime_value != PROTECTION_BELOW_PELVIS_PX:
        raise RuntimeError(
            "Probe/runtime ProtectionBelowPelvisPx drift: "
            f"{PROTECTION_BELOW_PELVIS_PX} != {runtime_value}"
        )



def depth_relation(a, b):
    """Mirror of Simulation/OfficeGrid/OfficeIsometricDepth.Compare."""
    a_past_x = a["minX"] > b["maxX"]
    b_past_x = b["minX"] > a["maxX"]
    a_past_y = a["minY"] > b["maxY"]
    b_past_y = b["minY"] > a["maxY"]
    if (a_past_x and b_past_y) or (b_past_x and a_past_y):
        return 0
    if a_past_x or a_past_y:
        return 1
    if b_past_x or b_past_y:
        return -1
    same = (a["minX"] == b["minX"] and a["maxX"] == b["maxX"] and
            a["minY"] == b["minY"] and a["maxY"] == b["maxY"])
    if same and a["priority"] != b["priority"]:
        return 1 if a["priority"] < b["priority"] else -1
    return 0


def footprint_sort(items):
    """Mirror of OfficeIsometricDepth.Sort - returns ids back to front."""
    order = sorted(
        range(len(items)),
        key=lambda i: (-(items[i]["maxX"] + items[i]["maxY"]),
                       -(items[i]["minX"] + items[i]["minY"]),
                       items[i]["priority"], items[i]["id"]))
    behind = [0] * len(items)
    ahead = [[] for _ in items]
    for a in range(len(items)):
        for b in range(a + 1, len(items)):
            relation = depth_relation(items[a], items[b])
            if relation > 0:
                ahead[a].append(b)
                behind[b] += 1
            elif relation < 0:
                ahead[b].append(a)
                behind[a] += 1
    result, emitted = [], [False] * len(items)
    while len(result) < len(items):
        chosen = next((i for i in order if not emitted[i] and behind[i] == 0),
                      next(i for i in order if not emitted[i]))
        emitted[chosen] = True
        result.append(items[chosen]["id"])
        for nxt in ahead[chosen]:
            if behind[nxt] > 0:
                behind[nxt] -= 1
    return result

# --------------------------------------------------------------------------- scene builders
def build_office(layout, catalog, poses, seated=True, character_scale=RUNTIME_CHARACTER_SCALE,
                 anatomy=None, bounds=None):
    seat_by_chair = {s["chair"]: s for s in layout["seats"]}
    occupied_chairs = {s["chair"] for s in layout["seats"]} if seated else set()
    occupied_desks = {s["desk"] for s in layout["seats"]} if seated else set()

    points = []
    for item in layout["furniture"]:
        root = subcell_px(item["px2"], item["py2"])
        points.append(root)
    if bounds is None:
        xs = [p[0] for p in points]
        ys = [p[1] for p in points]
        bounds = (min(xs) - 340, min(ys) - 220, max(xs) + 340, max(ys) + 420)
    scene = Scene(bounds)

    # floor
    for y in range(layout["height"]):
        for x in range(layout["width"]):
            tile = FLOOR_TILES[(x * 3 + y * 5) % 3]
            root = cell_px(x, y)
            scene.add(tile, root, (TILE_W / 2, 0.0), 1.0, -10000, key=y * 100 + x)

    marks = []
    for item in layout["furniture"]:
        definition = catalog[item["kind"]]
        root = subcell_px(item["px2"], item["py2"])
        png_name = FURNITURE_PNG.get(item["kind"])
        if not png_name:
            UNMAPPED_FURNITURE_KINDS.add(item["kind"])
            continue
        png = os.path.join(FURNITURE_ROOT, png_name)
        sort_world = sprite_point(root, definition["ground"], definition["sort"], definition["scale"])
        base_order = sorting_order(sort_world[1])
        # Shipped rule: every object keeps its own ground-anchor order. Nothing is re-sorted
        # around an occupant, which is what used to draw desk legs across a seated body.
        scene.add(png, root, definition["ground"], definition["scale"], base_order,
                  key=item["px2"] + item["py2"])
        front = FURNITURE_FRONT_PNG.get(item["kind"])
        if (front and definition["front_when_occupied"] and
                (item["id"] in occupied_chairs or item["id"] in occupied_desks)):
            scene.add(os.path.join(FURNITURE_ROOT, front), root, definition["ground"],
                      definition["scale"], base_order + 2)

    if seated:
        for seat in layout["seats"]:
            member = seat["id"].replace("seat_", "")
            chair = next(f for f in layout["furniture"] if f["id"] == seat["chair"])
            desk = next(f for f in layout["furniture"] if f["id"] == seat["desk"])
            chair_root = subcell_px(chair["px2"], chair["py2"])
            desk_root = subcell_px(desk["px2"], desk["py2"])
            chair_def, desk_def = catalog[chair["kind"]], catalog[desk["kind"]]
            seat_world = sprite_point(chair_root, chair_def["ground"], chair_def["seat"], chair_def["scale"])
            work_world = sprite_point(desk_root, desk_def["ground"], desk_def["work"], desk_def["scale"])
            opseat_world = sprite_point(desk_root, desk_def["ground"], desk_def["opseat"], desk_def["scale"])
            pose = (anatomy or {}).get(member) or (poses[member]["pelvis"], poses[member]["hand"])
            pivot = (CHARACTER_CANVAS / 2.0, 0.0)
            visual_root = (seat_world[0] - (pose[0][0] - pivot[0]) * character_scale,
                           seat_world[1] - (pose[0][1] - pivot[1]) * character_scale)
            hand_world = (visual_root[0] + (pose[1][0] - pivot[0]) * character_scale,
                          visual_root[1] + (pose[1][1] - pivot[1]) * character_scale)
            clip_name = "sit_work"
            frame = poses[member]["frame"]
            chair_sort_world = sprite_point(chair_root, chair_def["ground"], chair_def["sort"],
                                            chair_def["scale"])
            scene.add(frame_path(member, clip_name, frame), visual_root, pivot, character_scale,
                      sorting_order(chair_sort_world[1]) + 1)
            if MODEL_UPPER_BODY_REDRAW:
                scene.add(
                    upper_body_frame_path(member, clip_name, frame, pose[0][1]),
                    visual_root,
                    pivot,
                    character_scale,
                    sorting_order(chair_sort_world[1]) + 3,
                )
            marks.append(dict(member=member, seat=seat_world, work=work_world, opseat=opseat_world,
                              hand=hand_world, visual_root=visual_root, chair=chair_root, desk=desk_root))
    return scene, marks


def crop_to(scene_image, scene, world_centre, size):
    cx, cy = scene.to_image(world_centre)
    box = (round(cx - size[0] / 2), round(cy - size[1] * 0.55),
           round(cx + size[0] / 2), round(cy + size[1] * 0.45))
    return scene_image.crop(box)


# --------------------------------------------------------------------------- main
def main():
    os.makedirs(OUT, exist_ok=True)
    validate_protection_constant()
    catalog = read_furniture_catalog()
    poses = read_pose_catalog()
    layout = read_layout()
    report = []

    def say(line=""):
        report.append(line)
        print(line)

    say("OFFICE VISUAL COHERENCE V4 - offline composite probe")
    say(f"layout {layout['width']}x{layout['height']} furniture={len(layout['furniture'])} "
        f"seats={len(layout['seats'])}")
    say(f"character scale (runtime constant) = {RUNTIME_CHARACTER_SCALE}")
    say(f"MODEL_UPPER_BODY_REDRAW = {MODEL_UPPER_BODY_REDRAW}")
    say(f"PROTECTION_BELOW_PELVIS_PX = {PROTECTION_BELOW_PELVIS_PX} (runtime drift=0)")
    say("")

    # ---- furniture geometry facts
    chair, desk = catalog["swivel_chair"], catalog["desk_with_pc"]
    chair_seat_height = chair["seat"][1] - chair["ground"][1]
    desk_work_height = desk["work"][1] - desk["ground"][1]
    say("[furniture]")
    say(f"  chair seatAnchor  = {chair['seat']}  -> {chair_seat_height:.1f}px above its ground anchor")
    say(f"  desk workSocket   = {desk['work']}  -> {desk_work_height:.1f}px above its ground anchor")
    say(f"  desk opSeatSocket = {desk['opseat']}")
    say("")

    scene, marks = build_office(layout, catalog, poses)
    say("unmapped furniture skipped = " +
        (", ".join(sorted(UNMAPPED_FURNITURE_KINDS))
         if UNMAPPED_FURNITURE_KINDS else "none"))
    overview = scene.draw().convert("RGB")
    overview.save(os.path.join(OUT, "00-current-main-overview.png"))

    scene_marked, marks = build_office(layout, catalog, poses)
    scene_marked.draw()
    for m in marks:
        scene_marked.cross(m["seat"], (255, 60, 60, 255), "seat")
        scene_marked.cross(m["work"], (60, 200, 255, 255), "work")
        scene_marked.cross(m["hand"], (255, 220, 40, 255), "hand")
    scene_marked.im.convert("RGB").save(os.path.join(OUT, "03-current-main-anchor-debug.png"))

    tiles = []
    for m in marks:
        tiles.append(crop_to(scene_marked.im, scene_marked, m["seat"], (520, 560)))
    sheet = Image.new("RGB", (520 * 4, 560))
    for i, tile in enumerate(tiles):
        sheet.paste(tile.convert("RGB"), (i * 520, 0))
    sheet.save(os.path.join(OUT, "01-current-main-four-workstations.png"))

    # ---- per member numbers
    say("[current composite errors, world px at PPU 180]")
    say("| member | chairSeat<->deskOpSeat | hand<->deskWorkSocket | pelvisAnchor | handAnchor |")
    say("|---|---:|---:|---|---|")
    for m in marks:
        pose = poses[m["member"]]
        d_seat = math.dist(m["seat"], m["opseat"])
        d_hand = math.dist(m["hand"], m["work"])
        say(f"| {m['member']} | {d_seat:.3f}px | {d_hand:.3f}px | {pose['pelvis']} | {pose['hand']} |")
    say("")

    say("[authored hand-pelvis vectors]")
    for member in MEMBERS:
        pose = poses[member]
        vector = (pose["hand"][0] - pose["pelvis"][0], pose["hand"][1] - pose["pelvis"][1])
        say(f"  {member:<13} {vector}  x{RUNTIME_CHARACTER_SCALE} -> "
            f"({vector[0] * RUNTIME_CHARACTER_SCALE:.2f}, {vector[1] * RUNTIME_CHARACTER_SCALE:.2f}) screen px")
    chair_root = cell_px(2, 3)
    desk_root = subcell_px(5, 8)
    seat_world = sprite_point(chair_root, chair["ground"], chair["seat"])
    work_world = sprite_point(desk_root, desk["ground"], desk["work"])
    required = (work_world[0] - seat_world[0], work_world[1] - seat_world[1])
    say(f"  desk vector (seat->workSocket)   = ({required[0]:.2f}, {required[1]:.2f}) screen px")
    say("  -> the four authored vectors are the desk vector divided by the global scale.")
    say("")

    # ---- silhouette + sha per member
    say("[work frame silhouettes and SHA]")
    say("| member | frame | bbox | height | area | selected Work[3] catalog SHA matches |")
    say("|---|---:|---|---:|---:|---|")
    anatomy_auto = {}
    for member in MEMBERS:
        for frame in range(6):
            path = frame_path(member, "sit_work", frame)
            s = silhouette(path)
            matches = "-"
            if frame == poses[member]["frame"]:
                matches = "yes" if sha256(path) == poses[member]["sha"] else "NO"
                anatomy_auto[member] = s
            say(f"| {member} | {frame} | {s['bbox']} | {s['bbox'][3] - s['bbox'][1] + 1} | "
                f"{s['area']} | {matches} |")
    say("")

    say("[physical scale consistency, one uniform scale required]")
    say("| member | seatContact(auto) | headTop | sitting height px | scale to match furniture |")
    say("|---|---|---|---:|---:|")
    px_per_cm = desk_work_height and 0.0
    # furniture ruler: chair seat sits chair_seat_height px above the floor.
    for member in MEMBERS:
        s = anatomy_auto[member]
        seat_pt = s["seat_candidate"]
        head = s["head"]
        sitting = head[1] - seat_pt[1]
        leg = seat_pt[1] - s["bbox"][1]
        say(f"| {member} | ({seat_pt[0]:.0f},{seat_pt[1]:.0f}) | ({head[0]:.0f},{head[1]:.0f}) | "
            f"{sitting:.0f} | legScale={chair_seat_height / max(leg, 1):.2f} |")
    say("")

    # ---- contact sheets
    for index, member in enumerate(MEMBERS):
        S = 2
        sheet = Image.new("RGBA", (CHARACTER_CANVAS * S * 6, CHARACTER_CANVAS * S), (26, 28, 34, 255))
        for frame in range(6):
            im = Image.open(frame_path(member, "sit_work", frame)).convert("RGBA")
            sheet.alpha_composite(im.resize((CHARACTER_CANVAS * S,) * 2, Image.NEAREST),
                                  (frame * CHARACTER_CANVAS * S, 0))
            d = ImageDraw.Draw(sheet)
            d.text((frame * CHARACTER_CANVAS * S + 6, 6), f"{member} work {frame}", fill=(255, 255, 255, 255))
        sheet.convert("RGB").save(os.path.join(OUT, f"1{index}-work-frame-contact-sheet-{member.replace('_','-')}.png"))

    # ---- anatomy sheets with the authored anchors drawn on the sprite
    for index, member in enumerate(MEMBERS):
        pose = poses[member]
        im = Image.open(frame_path(member, "sit_work", pose["frame"])).convert("RGBA")
        S = 3
        bg = Image.new("RGBA", (CHARACTER_CANVAS * S,) * 2, (26, 28, 34, 255))
        bg.alpha_composite(im.resize((CHARACTER_CANVAS * S,) * 2, Image.NEAREST))
        d = ImageDraw.Draw(bg)
        for g in range(0, CHARACTER_CANVAS + 1, 20):
            d.line([(g * S, 0), (g * S, CHARACTER_CANVAS * S)], fill=(80, 82, 100, 255))
            d.line([(0, (CHARACTER_CANVAS - g) * S), (CHARACTER_CANVAS * S, (CHARACTER_CANVAS - g) * S)],
                   fill=(80, 82, 100, 255))
            d.text((g * S + 2, CHARACTER_CANVAS * S - 13), str(g), fill=(190, 190, 210, 255))
            d.text((2, (CHARACTER_CANVAS - g) * S + 2), str(g), fill=(190, 190, 210, 255))
        for name, point, colour in (("pelvisAnchor", pose["pelvis"], (255, 70, 70, 255)),
                                    ("handAnchor", pose["hand"], (255, 220, 40, 255)),
                                    ("headTop(auto)", anatomy_auto[member]["head"], (120, 255, 120, 255)),
                                    ("seatContact(auto)", anatomy_auto[member]["seat_candidate"], (255, 140, 220, 255))):
            x, y = point[0] * S, (CHARACTER_CANVAS - point[1]) * S
            d.line([(x - 16, y), (x + 16, y)], fill=colour, width=3)
            d.line([(x, y - 16), (x, y + 16)], fill=colour, width=3)
            d.text((x + 18, y - 7), f"{name} ({point[0]:.0f},{point[1]:.0f})", fill=colour)
        bg.convert("RGB").save(os.path.join(OUT, f"2{index}-character-anatomy-{member.replace('_','-')}.png"))

    # ---- layer breakdown for one workstation
    desk_png = os.path.join(FURNITURE_ROOT, FURNITURE_PNG["desk_with_pc"])
    chair_png = os.path.join(FURNITURE_ROOT, FURNITURE_PNG["swivel_chair"])
    desk_front_png = os.path.join(FURNITURE_ROOT, FURNITURE_FRONT_PNG["desk_with_pc"])
    father = next(m for m in marks if m["member"] == "father")
    stages = [("desk base", {desk_png}), ("+ chair base", {desk_png, chair_png}),
              ("+ character", {desk_png, chair_png, frame_path("father", "sit_work", 0)}),
              ("+ desk front", None)]
    tiles = []
    for label, only in stages:
        s2, _ = build_office(layout, catalog, poses)
        image = s2.draw(only=only)
        tile = crop_to(image, s2, father["seat"], (520, 560)).convert("RGB")
        ImageDraw.Draw(tile).text((8, 8), label, fill=(255, 255, 255))
        tiles.append(tile)
    breakdown = Image.new("RGB", (520 * len(tiles), 560))
    for i, tile in enumerate(tiles):
        breakdown.paste(tile, (i * 520, 0))
    breakdown.save(os.path.join(OUT, "02-current-main-layer-debug.png"))

    # ---- two-point similarity feasibility: can one uniform scale satisfy seat AND keyboard?
    say("[two-point feasibility: seatContact->chair seat AND hand->desk keyboard]")
    say("A rigid sprite only has translation + one uniform scale, so the character vector and the")
    say("furniture vector must share a direction and only differ by that scale.")
    keyboard_guess = (355.0, 233.0)      # visually read centre of the drawn keyboard on the desk sprite
    work_real = sprite_point(desk_root, desk["ground"], keyboard_guess)
    need_socket = (work_world[0] - seat_world[0], work_world[1] - seat_world[1])
    need_keys = (work_real[0] - seat_world[0], work_real[1] - seat_world[1])
    say(f"  authored workSocket target : ({need_socket[0]:.1f}, {need_socket[1]:.1f}) px "
        f"len={math.hypot(*need_socket):.1f} angle={math.degrees(math.atan2(*reversed(need_socket))):.1f}deg")
    say(f"  drawn keyboard target      : ({need_keys[0]:.1f}, {need_keys[1]:.1f}) px "
        f"len={math.hypot(*need_keys):.1f} angle={math.degrees(math.atan2(*reversed(need_keys))):.1f}deg")
    say("| member | proposed seat->hand (sprite px) | angle | scale for x | scale for y | verdict |")
    say("|---|---|---:|---:|---:|---|")
    for member in MEMBERS:
        seat_pt, hand_pt = PROPOSED_ANATOMY[member]
        vector = (hand_pt[0] - seat_pt[0], hand_pt[1] - seat_pt[1])
        angle = math.degrees(math.atan2(vector[1], vector[0]))
        sx = need_keys[0] / vector[0]
        sy = need_keys[1] / vector[1]
        verdict = "OK" if abs(sx - sy) / max(abs(sx), abs(sy)) <= 0.05 else "IMPOSSIBLE"
        say(f"| {member} | ({vector[0]:.0f},{vector[1]:.0f}) | {angle:.1f}deg | {sx:.2f} | {sy:.2f} | {verdict} |")
    say("")

    # ---- shipped model render (OfficeSeatedOccupantContract: seat contact -> chair cushion)
    s3, marks3 = build_office(layout, catalog, poses, character_scale=SHIPPED_CHARACTER_SCALE)
    image = s3.draw()
    image.convert("RGB").save(os.path.join(OUT, "34-final-starter-office-overview.png"))
    tiles = [crop_to(image, s3, m["seat"], (520, 560)).convert("RGB") for m in marks3]
    sheet = Image.new("RGB", (520 * 4, 560))
    for i, tile in enumerate(tiles):
        ImageDraw.Draw(tile).text((8, 8), f"{marks3[i]['member']} scale={SHIPPED_CHARACTER_SCALE}",
                                  fill=(255, 255, 255))
        sheet.paste(tile, (i * 520, 0))
    sheet.save(os.path.join(OUT, "30-final-static-four-workstations.png"))

    say("[shipped model] OfficeSeatedOccupantContract")
    say(f"  character scale {SHIPPED_CHARACTER_SCALE}; seat contact pinned to the chair cushion")
    say("  occupant sorting = chair ground order + 1, no furniture order is rewritten")
    say("  seat contacts are the catalog values, measured off the silhouettes:")
    for member in MEMBERS:
        say(f"    {member:<13} {poses[member]['pelvis']}")
    say("")

    open(os.path.join(OUT, "office-visual-coherence-v4-report.txt"), "w", encoding="utf-8").write(
        "\n".join(report) + "\n")
    print("\nartifacts ->", OUT)


if __name__ == "__main__":
    sys.exit(main())
