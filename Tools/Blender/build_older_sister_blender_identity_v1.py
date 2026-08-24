"""Build the isolated Older Sister V1 Blender identity candidate.

The model is authored from new procedural topology.  It does not import or
inspect any legacy 2D family asset, Styloo proxy, rejected Player blend/FBX,
R-series candidate, or other character mesh.  The committed four-view sheet is
recorded only as the locked visual identity reference.
"""

import argparse
import hashlib
import json
import math
import os
import sys
from collections import defaultdict

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--quality", choices=("draft", "final"), default="draft")
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = os.path.abspath(ARGS.output)
REFERENCE = os.path.abspath(ARGS.reference)
QUALITY = ARGS.quality
os.makedirs(OUTPUT, exist_ok=True)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


clear_scene()
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0


PALETTE = {
    "skin": (0.94, 0.58, 0.43),
    "skin_light": (1.00, 0.72, 0.57),
    "skin_blush": (0.94, 0.36, 0.34),
    "hair": (0.018, 0.014, 0.021),
    "hair_mid": (0.065, 0.052, 0.073),
    "hair_highlight": (0.145, 0.112, 0.135),
    "bow": (0.009, 0.013, 0.024),
    "top": (0.035, 0.038, 0.050),
    "top_highlight": (0.105, 0.108, 0.125),
    "navy": (0.018, 0.038, 0.105),
    "navy_highlight": (0.045, 0.075, 0.165),
    "piping": (0.86, 0.88, 0.88),
    "eye_white": (0.985, 0.980, 0.955),
    "iris": (0.005, 0.52, 0.56),
    "pupil": (0.003, 0.012, 0.016),
    "mouth": (0.39, 0.038, 0.048),
}
ATLAS_KEYS = tuple(PALETTE.keys())
ATLAS_COLS = 4
ATLAS_ROWS = 4
ATLAS_SIZE = 512 if QUALITY == "final" else 256
ATLAS_PATH = os.path.join(OUTPUT, "older-sister-blender-identity-v1-atlas.png")


def clamp01(value):
    return max(0.0, min(1.0, value))


def make_atlas():
    image = bpy.data.images.new(
        "OlderSisterV1IdentityAtlas",
        width=ATLAS_SIZE,
        height=ATLAS_SIZE,
        alpha=True,
    )
    pixels = [0.0] * (ATLAS_SIZE * ATLAS_SIZE * 4)
    cell_w = ATLAS_SIZE // ATLAS_COLS
    cell_h = ATLAS_SIZE // ATLAS_ROWS
    for y in range(ATLAS_SIZE):
        row = min(y // cell_h, ATLAS_ROWS - 1)
        for x in range(ATLAS_SIZE):
            col = min(x // cell_w, ATLAS_COLS - 1)
            index = row * ATLAS_COLS + col
            key = ATLAS_KEYS[index] if index < len(ATLAS_KEYS) else "piping"
            base = PALETTE[key]
            lx = x % cell_w
            ly = y % cell_h
            grain = (((x * 73856093) ^ (y * 19349663)) & 255) / 255.0 - 0.5
            weave = math.sin(lx * 0.43) * math.sin(ly * 0.51)
            diagonal = math.sin((lx * 0.18) + (ly * 0.055))
            strength = 0.006
            if key in ("top", "top_highlight", "navy", "navy_highlight", "piping"):
                strength = 0.022
                grain = grain * 0.36 + weave * 0.64
            elif key in ("hair", "hair_mid", "hair_highlight", "bow"):
                strength = 0.030
                grain = grain * 0.18 + diagonal * 0.82
            elif key.startswith("skin"):
                strength = 0.006
            offset = (y * ATLAS_SIZE + x) * 4
            pixels[offset + 0] = clamp01(base[0] + grain * strength)
            pixels[offset + 1] = clamp01(base[1] + grain * strength)
            pixels[offset + 2] = clamp01(base[2] + grain * strength)
            pixels[offset + 3] = 1.0
    image.pixels.foreach_set(pixels)
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


ATLAS_IMAGE = make_atlas()


def build_character_material():
    material = bpy.data.materials.new("M_OlderSisterV1_IdentityAtlas")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = ATLAS_IMAGE
    texture.interpolation = "Linear"
    texture.extension = "EXTEND"
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.56
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.26
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


CHARACTER_MATERIAL = build_character_material()
CHARACTER_PARTS = []


def palette_tile(key):
    index = ATLAS_KEYS.index(key)
    return index % ATLAS_COLS, index // ATLAS_COLS


def assign_uv_tile(obj, key):
    layer = obj.data.uv_layers.new(name="OlderSisterV1AtlasUV")
    col, row = palette_tile(key)
    tile_w = 1.0 / ATLAS_COLS
    tile_h = 1.0 / ATLAS_ROWS
    padding_u = tile_w * 0.13
    padding_v = tile_h * 0.13
    coordinates = [vertex.co for vertex in obj.data.vertices]
    mins = [min(co[i] for co in coordinates) for i in range(3)]
    maxs = [max(co[i] for co in coordinates) for i in range(3)]
    extents = [maxs[i] - mins[i] for i in range(3)]
    axes = sorted(range(3), key=lambda axis: extents[axis], reverse=True)[:2]
    for loop in obj.data.loops:
        co = obj.data.vertices[loop.vertex_index].co
        nu = (co[axes[0]] - mins[axes[0]]) / max(extents[axes[0]], 1.0e-6)
        nv = (co[axes[1]] - mins[axes[1]]) / max(extents[axes[1]], 1.0e-6)
        layer.data[loop.index].uv = (
            col * tile_w + padding_u + nu * (tile_w - 2.0 * padding_u),
            row * tile_h + padding_v + nv * (tile_h - 2.0 * padding_v),
        )


def assign_weights(obj, weights):
    if isinstance(weights, str):
        weights = [{weights: 1.0} for _ in obj.data.vertices]
    elif isinstance(weights, dict):
        weights = [weights for _ in obj.data.vertices]
    if len(weights) != len(obj.data.vertices):
        raise RuntimeError("Weight count mismatch for %s" % obj.name)
    buckets = defaultdict(list)
    for vertex_index, mapping in enumerate(weights):
        total = sum(max(0.0, value) for value in mapping.values())
        if total <= 0.0:
            raise RuntimeError("Unweighted vertex %d in %s" % (vertex_index, obj.name))
        for bone, value in mapping.items():
            normalized = max(0.0, value) / total
            if normalized > 0.0001:
                buckets[(bone, round(normalized, 6))].append(vertex_index)
    groups = {}
    for bone, _ in buckets:
        if bone not in groups:
            groups[bone] = obj.vertex_groups.new(name=bone)
    for (bone, value), indices in buckets.items():
        groups[bone].add(indices, value, "REPLACE")


def create_mesh(name, vertices, faces, palette_key, weights, smooth=True):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(CHARACTER_MATERIAL)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    assign_uv_tile(obj, palette_key)
    assign_weights(obj, weights)
    obj["identityPart"] = name
    obj["paletteKey"] = palette_key
    CHARACTER_PARTS.append(obj)
    return obj


def ellipsoid(name, center, radii, palette_key, weights, segments=40, rings=24, deform=None):
    center = Vector(center)
    vertices = [tuple(center + Vector((0.0, 0.0, radii[2])))]
    faces = []
    for ring in range(1, rings):
        theta = math.pi * ring / rings
        nz = math.cos(theta)
        radial = math.sin(theta)
        for segment in range(segments):
            phi = 2.0 * math.pi * segment / segments
            normal = Vector((radial * math.cos(phi), radial * math.sin(phi), nz))
            point = Vector((normal.x * radii[0], normal.y * radii[1], normal.z * radii[2]))
            if deform:
                point = Vector(deform(normal, point))
            vertices.append(tuple(center + point))
    bottom_index = len(vertices)
    vertices.append(tuple(center - Vector((0.0, 0.0, radii[2]))))
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((0, 1 + segment, 1 + nxt))
    for ring in range(rings - 2):
        lower = 1 + ring * segments
        upper = lower + segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((lower + segment, upper + segment, upper + nxt, lower + nxt))
    last = 1 + (rings - 2) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom_index, last + nxt, last + segment))
    return create_mesh(name, vertices, faces, palette_key, weights)


def superellipse(value, exponent):
    return math.copysign(abs(value) ** (2.0 / exponent), value)


def loft(name, ring_specs, palette_key, ring_weights, segments=36, exponent=2.0, cap=True):
    vertices = []
    weights = []
    faces = []
    for ring_index, spec in enumerate(ring_specs):
        center = Vector(spec[0])
        radius_x, radius_y = spec[1], spec[2]
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            x = radius_x * superellipse(math.cos(angle), exponent)
            y = radius_y * superellipse(math.sin(angle), exponent)
            vertices.append(tuple(center + Vector((x, y, 0.0))))
            weights.append(dict(ring_weights[ring_index]))
    for ring in range(len(ring_specs) - 1):
        first = ring * segments
        second = (ring + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    if cap:
        bottom = len(vertices)
        vertices.append(tuple(Vector(ring_specs[0][0])))
        weights.append(dict(ring_weights[0]))
        top = len(vertices)
        vertices.append(tuple(Vector(ring_specs[-1][0])))
        weights.append(dict(ring_weights[-1]))
        first = 0
        last = (len(ring_specs) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((bottom, first + nxt, first + segment))
            faces.append((top, last + segment, last + nxt))
    return create_mesh(name, vertices, faces, palette_key, weights)


def tank_shell(name, ring_specs, ring_weights, segments=48):
    """Fitted sleeveless shell with a real scoop opening and side straps."""
    vertices = []
    weights = []
    faces = []
    face_keys = []
    for ring_index, spec in enumerate(ring_specs):
        center = Vector(spec[0])
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertices.append(
                tuple(
                    center
                    + Vector(
                        (
                            spec[1] * superellipse(math.cos(angle), 2.3),
                            spec[2] * superellipse(math.sin(angle), 2.3),
                            0.0,
                        )
                    )
                )
            )
            weights.append(dict(ring_weights[ring_index]))
    for ring in range(len(ring_specs) - 1):
        first = ring * segments
        second = (ring + 1) * segments
        upper_transition = ring == len(ring_specs) - 2
        for segment in range(segments):
            angle = 2.0 * math.pi * (segment + 0.5) / segments
            nxt = (segment + 1) % segments
            # The underlayer supplies the open neck area.  Only the narrow
            # lateral arcs remain dark in the final transition, so they read as
            # integrated tank straps from both front and back.  Central faces
            # are genuinely open instead of skin-coloured garment polygons,
            # preventing a duplicate surface and its triangular intersection.
            side_strap = abs(math.cos(angle)) > 0.68
            if upper_transition and not side_strap:
                continue
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
            face_keys.append("top_highlight" if ring == 1 and math.sin(angle) >= -0.10 else "top")
    obj = create_mesh(name, vertices, faces, "top", weights)
    uv = obj.data.uv_layers.active
    for polygon, key in zip(obj.data.polygons, face_keys):
        if key == "top":
            continue
        col, row = palette_tile(key)
        center = ((col + 0.5) / ATLAS_COLS, (row + 0.5) / ATLAS_ROWS)
        for loop_index in polygon.loop_indices:
            uv.data[loop_index].uv = center
    return obj


def frame_for_tangent(tangent):
    tangent = Vector(tangent).normalized()
    reference = Vector((0.0, 1.0, 0.0))
    if abs(tangent.dot(reference)) > 0.94:
        reference = Vector((0.0, 0.0, 1.0))
    axis_a = (reference - tangent * reference.dot(tangent)).normalized()
    axis_b = tangent.cross(axis_a).normalized()
    return axis_a, axis_b


def tube(name, points, radii, palette_key, ring_weights, segments=20, flatten=1.0, cap=True):
    points = [Vector(point) for point in points]
    vertices = []
    weights = []
    faces = []
    for index, point in enumerate(points):
        if index == 0:
            tangent = points[1] - points[0]
        elif index == len(points) - 1:
            tangent = points[-1] - points[-2]
        else:
            tangent = points[index + 1] - points[index - 1]
        axis_a, axis_b = frame_for_tangent(tangent)
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            offset = axis_a * (math.cos(angle) * radii[index] * flatten)
            offset += axis_b * (math.sin(angle) * radii[index])
            vertices.append(tuple(point + offset))
            weights.append(dict(ring_weights[index]))
    for ring in range(len(points) - 1):
        first = ring * segments
        second = (ring + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    if cap:
        start = len(vertices)
        vertices.append(tuple(points[0]))
        weights.append(dict(ring_weights[0]))
        end = len(vertices)
        vertices.append(tuple(points[-1]))
        weights.append(dict(ring_weights[-1]))
        first = 0
        last = (len(points) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start, first + nxt, first + segment))
            faces.append((end, last + segment, last + nxt))
    return create_mesh(name, vertices, faces, palette_key, weights)


def cubic(a, b, c, d, t):
    omt = 1.0 - t
    return a * (omt ** 3) + b * (3.0 * omt * omt * t) + c * (3.0 * omt * t * t) + d * (t ** 3)


def hair_lock(name, root, control_a, control_b, tip, width, palette_key="hair", bone="Head", flatten=0.42):
    root = Vector(root)
    control_a = Vector(control_a)
    control_b = Vector(control_b)
    tip = Vector(tip)
    samples = 9
    points = [cubic(root, control_a, control_b, tip, index / (samples - 1)) for index in range(samples)]
    radii = []
    for index in range(samples):
        t = index / (samples - 1)
        taper = max(0.055, (1.0 - t) ** 0.74)
        belly = 0.86 + math.sin(math.pi * t) * 0.32
        radii.append(width * taper * belly)
    return tube(name, points, radii, palette_key, [{bone: 1.0}] * samples, 12, flatten)


def elliptical_band(name, center_x, z0, z1, center_y, radius_x, radius_y, palette_key, weights, segments=40):
    vertices = []
    faces = []
    vertex_weights = []
    for z, mapping in ((z0, weights[0]), (z1, weights[1])):
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            vertices.append((center_x + radius_x * math.cos(angle), center_y + radius_y * math.sin(angle), z))
            vertex_weights.append(dict(mapping))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((index, nxt, segments + nxt, segments + index))
    return create_mesh(name, vertices, faces, palette_key, vertex_weights)


def eye_patch(name, center, width, height, depth, palette_key, bone="Head", segments=32):
    center = Vector(center)
    vertices = [tuple(center - Vector((0.0, depth, 0.0)))]
    faces = []
    for index in range(segments):
        angle = 2.0 * math.pi * index / segments
        x = math.cos(angle) * width
        z = math.sin(angle) * height
        y = -depth * (0.80 + 0.20 * abs(math.cos(angle)))
        vertices.append(tuple(center + Vector((x, y, z))))
    for index in range(segments):
        faces.append((0, 1 + index, 1 + ((index + 1) % segments)))
    return create_mesh(name, vertices, faces, palette_key, bone)


def bow_leaf(name, center, direction, length, width, thickness, palette_key="bow"):
    center = Vector(center)
    axis_l = Vector(direction).normalized()
    # Bow directions live mostly in XZ.  Keeping thickness on Y makes the full
    # petal width visible in front/back views instead of collapsing to a stick.
    axis_t = Vector((0.0, 1.0, 0.0))
    axis_w = axis_t.cross(axis_l).normalized()
    sections = (-1.0, -0.48, 0.0, 0.48, 1.0)
    vertices = []
    weights = []
    faces = []
    for section in sections:
        envelope = max(0.04, math.sin((section + 1.0) * math.pi * 0.5))
        p = center + axis_l * (section * length * 0.5)
        half_w = width * envelope * 0.5
        vertices.extend(
            (
                tuple(p - axis_w * half_w - axis_t * thickness * 0.5),
                tuple(p + axis_w * half_w - axis_t * thickness * 0.5),
                tuple(p - axis_w * half_w + axis_t * thickness * 0.5),
                tuple(p + axis_w * half_w + axis_t * thickness * 0.5),
            )
        )
        weights.extend(({"Head": 1.0},) * 4)
    for section in range(len(sections) - 1):
        a = section * 4
        b = (section + 1) * 4
        faces.extend(
            (
                (a, b, b + 1, a + 1),
                (a + 3, b + 3, b + 2, a + 2),
                (a, a + 2, b + 2, b),
                (a + 1, b + 1, b + 3, a + 3),
            )
        )
    faces.extend(((0, 1, 3, 2), (16, 18, 19, 17)))
    return create_mesh(name, vertices, faces, palette_key, weights, smooth=False)


def foot_shell(name, x_center, bone):
    slices = (
        (0.175, 0.125, 0.035, 0.360),
        (0.090, 0.145, 0.034, 0.285),
        (-0.070, 0.162, 0.032, 0.190),
        (-0.235, 0.178, 0.030, 0.145),
        (-0.360, 0.160, 0.029, 0.095),
    )
    profile = (
        (-0.80, 0.00),
        (-1.00, 0.18),
        (-0.88, 0.60),
        (-0.50, 0.91),
        (0.00, 1.00),
        (0.50, 0.91),
        (0.88, 0.60),
        (1.00, 0.18),
        (0.80, 0.00),
    )
    vertices = []
    weights = []
    faces = []
    for y, half_width, base_z, height in slices:
        for px, pz in profile:
            vertices.append((x_center + px * half_width, y, base_z + pz * height))
            weights.append({bone: 1.0})
    count = len(profile)
    for slice_index in range(len(slices) - 1):
        first = slice_index * count
        second = (slice_index + 1) * count
        for index in range(count - 1):
            faces.append((first + index, second + index, second + index + 1, first + index + 1))
        faces.append((first + count - 1, second + count - 1, second, first))
    faces.append(tuple(reversed(tuple(range(count)))))
    last = (len(slices) - 1) * count
    faces.append(tuple(last + index for index in range(count)))
    return create_mesh(name, vertices, faces, "skin", weights)


def head_deform(normal, point):
    nz = normal.z
    cheek = 1.0 + 0.075 * math.exp(-((nz + 0.18) / 0.31) ** 2)
    chin = 1.0 - 0.28 * max(0.0, -nz) ** 1.55
    point.x *= cheek * chin
    point.y *= 1.0 - 0.08 * max(0.0, -nz)
    if normal.y < 0.0:
        point.y *= 1.0 + 0.045 * math.exp(-((nz + 0.10) / 0.42) ** 2)
    point.z -= 0.022 * max(0.0, -nz)
    return point


# -------------------------------------------------------------------------
# New adult body topology

HEAD = ellipsoid(
    "OlderSisterHeadSkin",
    (0.0, -0.018, 3.225),
    (0.385, 0.322, 0.445),
    "skin",
    "Head",
    52,
    32,
    head_deform,
)
ellipsoid("Ear.L", (0.385, 0.000, 3.205), (0.056, 0.040, 0.090), "skin", "Head", 24, 16)
ellipsoid("Ear.R", (-0.385, 0.000, 3.205), (0.056, 0.040, 0.090), "skin", "Head", 24, 16)

for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    x = 0.143 * sign
    eye_patch("EyeSclera." + suffix, (x, -0.329, 3.245), 0.085, 0.102, 0.006, "eye_white")
    eye_patch("Iris." + suffix, (x, -0.338, 3.241), 0.046, 0.064, 0.004, "iris")
    eye_patch("Pupil." + suffix, (x, -0.344, 3.241), 0.022, 0.041, 0.003, "pupil")
    eye_patch("EyeGlint." + suffix, (x - 0.015 * sign, -0.356, 3.273), 0.009, 0.015, 0.002, "eye_white", segments=18)
    tube(
        "UpperLid." + suffix,
        ((x - 0.078 * sign, -0.342, 3.277), (x, -0.354, 3.326), (x + 0.079 * sign, -0.341, 3.281)),
        (0.008, 0.011, 0.006),
        "hair",
        [{"Head": 1.0}] * 3,
        10,
        0.55,
    )
    tube(
        "Brow." + suffix,
        ((x - 0.072 * sign, -0.332, 3.375), (x, -0.345, 3.393), (x + 0.072 * sign, -0.333, 3.378)),
        (0.006, 0.009, 0.005),
        "hair",
        [{"Head": 1.0}] * 3,
        9,
        0.55,
    )
    ellipsoid("Cheek." + suffix, (0.232 * sign, -0.332, 3.128), (0.047, 0.009, 0.020), "skin_blush", "Head", 20, 12)

ellipsoid("Nose", (0.0, -0.357, 3.166), (0.018, 0.014, 0.026), "skin_light", "Head", 20, 12)
tube("Smile", ((-0.067, -0.354, 3.075), (0.0, -0.364, 3.058), (0.068, -0.354, 3.075)), (0.005, 0.007, 0.004), "mouth", [{"Head": 1.0}] * 3, 10, 0.52)


def hair_cap():
    segments = 52
    rings = 20
    vertices = []
    faces = []
    weights = []
    for ring in range(rings + 1):
        t = ring / rings
        for segment in range(segments):
            phi = 2.0 * math.pi * segment / segments
            frontness = max(0.0, -math.sin(phi))
            theta_max = 2.32 - 0.78 * frontness
            theta = 0.045 + (theta_max - 0.045) * t
            point = Vector(
                (
                    0.420 * math.sin(theta) * math.cos(phi),
                    0.354 * math.sin(theta) * math.sin(phi) + 0.018,
                    3.265 + 0.470 * math.cos(theta),
                )
            )
            vertices.append(tuple(point))
            weights.append({"Head": 1.0})
    for ring in range(rings):
        first = ring * segments
        second = (ring + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    return create_mesh("HairScalpShell", vertices, faces, "hair", weights)


hair_cap()

# Fine, tapered adult fringe; it remains integrated with the scalp rather than
# becoming a ring of rigid crown spikes.
front_roots = (-0.305, -0.210, -0.112, -0.010, 0.100, 0.210, 0.305)
front_tips = (-0.275, -0.175, -0.082, 0.018, 0.112, 0.205, 0.280)
for index, (root_x, tip_x) in enumerate(zip(front_roots, front_tips)):
    distance = abs(index - 3)
    hair_lock(
        "HairBang.%02d" % index,
        (root_x * 0.70, -0.223, 3.648 - distance * 0.014),
        (root_x + (0.025 if index % 2 == 0 else -0.020), -0.330, 3.575),
        (tip_x, -0.392, 3.430),
        (tip_x, -0.358, 3.292 + distance * 0.018),
        0.054 if index in (2, 3, 4) else 0.047,
        "hair_highlight" if index in (1, 5) else "hair",
        flatten=0.24,
    )

for sign, side in ((1.0, "L"), (-1.0, "R")):
    # Temple framing locks.
    for index in range(4):
        hair_lock(
            "Temple%s.%02d" % (side, index),
            ((0.25 + index * 0.025) * sign, -0.105 + index * 0.025, 3.580 - index * 0.055),
            ((0.39 + index * 0.018) * sign, -0.120, 3.430 - index * 0.045),
            ((0.43 + index * 0.020) * sign, -0.070, 3.140 - index * 0.045),
            ((0.40 + index * 0.022) * sign, -0.015 + index * 0.025, 2.890 - index * 0.060),
            0.055 - index * 0.003,
            "hair_highlight" if index == 1 else "hair",
            flatten=0.34,
        )

    # Broad twin-tail mass made from overlapping tapered locks.  Tips vary in
    # length and heading so the silhouette reads as hair, not two sausages.
    root_x = 0.455 * sign
    for index in range(11):
        lateral = (index - 5) * 0.032
        back = ((index % 3) - 1) * 0.030
        tip_spread = (index - 5) * 0.050
        tip_z = 1.86 + abs(index - 5) * 0.055 + (index % 2) * 0.050
        hair_lock(
            "TwinTail%s.%02d" % (side, index),
            (root_x + lateral * sign * 0.35, 0.035 + back, 3.520 - abs(index - 5) * 0.010),
            ((0.62 + abs(lateral) * 0.25) * sign, 0.090 + back, 3.270),
            ((0.70 + tip_spread * 0.18) * sign, 0.145 + back, 2.520 + (index % 3) * 0.055),
            ((0.57 + tip_spread) * sign, 0.100 + back * 1.5, tip_z),
            0.115 - abs(index - 5) * 0.006,
            "hair_highlight" if index in (2, 7) else ("hair_mid" if index in (4, 8) else "hair"),
            flatten=0.32,
        )

    # Four-leaf black bow plus knot at each tail base.
    knot_x = 0.470 * sign
    knot = (knot_x, -0.010, 3.535)
    ellipsoid("BowKnot." + side, knot, (0.082, 0.050, 0.074), "bow", "Head", 24, 16)
    bow_leaf("BowUpper." + side, Vector(knot) + Vector((0.025 * sign, 0.010, 0.105)), (0.65 * sign, 0.04, 0.76), 0.30, 0.18, 0.030)
    bow_leaf("BowOuter." + side, Vector(knot) + Vector((0.105 * sign, 0.010, 0.015)), (0.94 * sign, 0.02, 0.34), 0.31, 0.18, 0.030)
    bow_leaf("BowLower." + side, Vector(knot) + Vector((0.040 * sign, 0.015, -0.100)), (0.60 * sign, 0.02, -0.80), 0.30, 0.17, 0.030)
    bow_leaf("BowInner." + side, Vector(knot) + Vector((-0.070 * sign, 0.014, 0.000)), (-0.88 * sign, 0.02, 0.30), 0.25, 0.15, 0.028)

# Back crown locks cover the tail roots and keep the rear scalp layered.
for index, angle_degrees in enumerate((18, 42, 66, 90, 114, 138, 162)):
    angle = math.radians(angle_degrees)
    x = 0.28 * math.cos(angle)
    y = 0.15 + 0.12 * math.sin(angle)
    hair_lock(
        "HairBack.%02d" % index,
        (x * 0.52, 0.10, 3.690),
        (x, y, 3.620),
        (x * 1.25, y + 0.055, 3.335),
        (x * 1.16, y + 0.035, 3.025 + 0.040 * abs(index - 3)),
        0.065,
        "hair_highlight" if index in (1, 5) else "hair",
        flatten=0.35,
    )

for index, root_x in enumerate((-0.265, -0.175, -0.085, 0.0, 0.085, 0.175, 0.265)):
    hair_lock(
        "HairNape.%02d" % index,
        (root_x * 0.72, 0.255, 3.265),
        (root_x, 0.340, 3.145),
        (root_x * 1.08, 0.325, 2.955),
        (root_x * 1.02, 0.250, 2.775 + 0.018 * abs(index - 3)),
        0.053,
        "hair_mid" if index in (1, 5) else "hair",
        flatten=0.30,
    )

# Back-most overlapping nape locks close the small skin wedges visible between
# the first tapered layer.  Their narrow tips preserve a hair silhouette (not a
# solid bun) and they do not change the locked body proportions.
for index, root_x in enumerate((-0.225, -0.150, -0.075, 0.0, 0.075, 0.150, 0.225)):
    hair_lock(
        "HairNapeOccluder.%02d" % index,
        (root_x * 0.70, 0.345, 3.245),
        (root_x * 0.90, 0.380, 3.175),
        (root_x, 0.385, 3.030),
        (root_x * 1.08, 0.360, 2.890 + 0.012 * abs(index - 3)),
        0.076,
        "hair",
        flatten=0.28,
    )
ellipsoid(
    "HairNapeGapFill",
    (0.0, 0.348, 2.950),
    (0.230, 0.022, 0.052),
    "hair",
    {"Head": 0.68, "Neck": 0.32},
    36,
    16,
)

# Neck and adult torso.  A skin underbody gives the tank's scoop and armholes a
# real surface instead of painting skin onto a solid garment.
ellipsoid("NeckSkin", (0.0, 0.0, 2.795), (0.112, 0.098, 0.145), "skin", {"Neck": 0.82, "Head": 0.18}, 28, 18)
skin_torso_specs = (
    ((0.0, 0.012, 1.905), 0.405, 0.250),
    ((0.0, 0.010, 2.140), 0.360, 0.230),
    ((0.0, 0.004, 2.410), 0.445, 0.270),
    ((0.0, 0.000, 2.645), 0.515, 0.275),
    ((0.0, 0.000, 2.740), 0.360, 0.205),
    ((0.0, 0.000, 2.790), 0.250, 0.155),
    ((0.0, 0.000, 2.825), 0.125, 0.108),
)
skin_torso_weights = (
    {"Hips": 0.72, "Spine": 0.28},
    {"Spine": 0.78, "Hips": 0.22},
    {"Chest": 0.65, "Spine": 0.35},
    {"UpperChest": 0.72, "Chest": 0.28},
    {"UpperChest": 1.0},
    {"UpperChest": 0.82, "Neck": 0.18},
    {"Neck": 0.72, "UpperChest": 0.28},
)
loft("TorsoSkinUnderlayer", skin_torso_specs, "skin", skin_torso_weights, 44, exponent=2.38)

tank_specs = (
    ((0.0, 0.010, 1.920), 0.425, 0.268),
    ((0.0, 0.008, 2.150), 0.382, 0.250),
    ((0.0, 0.002, 2.415), 0.470, 0.292),
    ((0.0, -0.002, 2.620), 0.528, 0.295),
    ((0.0, 0.000, 2.730), 0.410, 0.242),
)
tank_shell("FittedSleevelessTop", tank_specs, skin_torso_weights, 48)
tube(
    "NecklinePiping",
    ((-0.300, -0.258, 2.710), (-0.215, -0.306, 2.615), (0.0, -0.318, 2.550), (0.215, -0.306, 2.615), (0.300, -0.258, 2.710)),
    (0.012, 0.012, 0.013, 0.012, 0.012),
    "piping",
    ({"UpperChest": 1.0}, {"UpperChest": 0.82, "Chest": 0.18}, {"Chest": 0.48, "UpperChest": 0.52}, {"UpperChest": 0.82, "Chest": 0.18}, {"UpperChest": 1.0}),
    10,
    0.55,
)
elliptical_band("TopHem", 0.0, 1.912, 1.944, 0.010, 0.416, 0.258, "top_highlight", ({"Hips": 0.62, "Spine": 0.38}, {"Hips": 0.55, "Spine": 0.45}), 44)

# Bare arms and complete hands in an A-pose.
for sign, side, prefix in ((1.0, "L", "Left"), (-1.0, "R", "Right")):
    shoulder = Vector((0.525 * sign, 0.0, 2.625))
    upper_mid = Vector((0.725 * sign, -0.006, 2.505))
    elbow = Vector((0.930 * sign, -0.012, 2.335))
    fore_mid = Vector((1.090 * sign, -0.018, 2.155))
    wrist = Vector((1.235 * sign, -0.022, 1.975))
    tube(
        "BareArm." + side,
        (shoulder, upper_mid, elbow, fore_mid, wrist),
        (0.158, 0.140, 0.118, 0.110, 0.082),
        "skin",
        (
            {prefix + "UpperArm": 1.0},
            {prefix + "UpperArm": 1.0},
            {prefix + "UpperArm": 0.48, prefix + "LowerArm": 0.52},
            {prefix + "LowerArm": 1.0},
            {prefix + "LowerArm": 0.82, prefix + "Hand": 0.18},
        ),
        26,
        0.94,
        cap=False,
    )
    ellipsoid(
        "ShoulderBlend." + side,
        (0.525 * sign, 0.0, 2.635),
        (0.170, 0.145, 0.172),
        "skin",
        {"UpperChest": 0.36, prefix + "UpperArm": 0.64},
        30,
        20,
    )
    palm = Vector((1.292 * sign, -0.028, 1.885))
    ellipsoid("Palm." + side, palm, (0.080, 0.058, 0.123), "skin", prefix + "Hand", 30, 20)
    for finger_index, local_x in enumerate((-0.047, -0.016, 0.016, 0.047)):
        finger_x = palm.x + local_x * sign
        top = Vector((finger_x, -0.036, 1.850))
        length = 0.124 - abs(finger_index - 1.5) * 0.010
        bottom = Vector((finger_x + 0.006 * sign, -0.040, 1.850 - length))
        tube(
            "Finger.%s.%02d" % (side, finger_index),
            (top, (top + bottom) * 0.5, bottom),
            (0.013, 0.012, 0.007),
            "skin",
            [{prefix + "Hand": 1.0}] * 3,
            12,
            0.78,
        )
    thumb_root = Vector((palm.x - 0.054 * sign, -0.040, 1.905))
    thumb_tip = Vector((palm.x - 0.105 * sign, -0.052, 1.815))
    tube("Thumb." + side, (thumb_root, (thumb_root + thumb_tip) * 0.5 + Vector((0.0, -0.007, 0.014)), thumb_tip), (0.018, 0.015, 0.008), "skin", [{prefix + "Hand": 1.0}] * 3, 12, 0.82)

# Dolphin shorts: a fitted waist plus independently shaped left/right openings,
# side piping and a clear central seam.  No Player trousers or donor geometry.
shorts_waist_specs = (
    ((0.0, 0.008, 1.820), 0.470, 0.290),
    ((0.0, 0.006, 1.915), 0.485, 0.295),
    ((0.0, 0.006, 1.985), 0.460, 0.283),
)
loft("ShortsWaist", shorts_waist_specs, "navy", ({"Hips": 1.0}, {"Hips": 1.0}, {"Hips": 0.82, "Spine": 0.18}), 44, exponent=2.55)
for sign, side, prefix in ((1.0, "L", "Left"), (-1.0, "R", "Right")):
    x = 0.225 * sign
    tube(
        "ShortLeg." + side,
        ((x, 0.008, 1.890), (x, 0.004, 1.760), (x, -0.002, 1.625)),
        (0.285, 0.292, 0.272),
        "navy",
        ({"Hips": 1.0}, {"Hips": 0.48, prefix + "UpperLeg": 0.52}, {prefix + "UpperLeg": 1.0}),
        34,
        0.88,
    )
    elliptical_band(
        "ShortHem." + side,
        x,
        1.600,
        1.635,
        -0.002,
        0.278,
        0.242,
        "piping",
        ({prefix + "UpperLeg": 1.0}, {prefix + "UpperLeg": 1.0}),
        36,
    )
    tube(
        "ShortSidePiping." + side,
        ((0.468 * sign, -0.032, 1.943), (0.498 * sign, -0.040, 1.820), (0.500 * sign, -0.030, 1.690), (0.472 * sign, -0.015, 1.620)),
        (0.012, 0.013, 0.013, 0.011),
        "piping",
        ({"Hips": 1.0}, {"Hips": 0.72, prefix + "UpperLeg": 0.28}, {"Hips": 0.32, prefix + "UpperLeg": 0.68}, {prefix + "UpperLeg": 1.0}),
        10,
        0.60,
    )
tube("ShortCenterSeam", ((0.0, -0.296, 1.935), (0.0, -0.306, 1.785), (0.0, -0.275, 1.630)), (0.006, 0.008, 0.004), "navy_highlight", [{"Hips": 1.0}] * 3, 9, 0.55)

# Adult legs and complete bare feet.  Overlapping joint loops keep the knees and
# ankles connected under deformation; every foot includes a heel, arch and five
# toe volumes.
for sign, side, prefix in ((1.0, "L", "Left"), (-1.0, "R", "Right")):
    x = 0.245 * sign
    hip = Vector((x, 0.004, 1.785))
    upper_thigh = Vector((x, 0.003, 1.665))
    thigh_mid = Vector((x, 0.002, 1.495))
    knee = Vector((x, 0.000, 1.165))
    calf_upper = Vector((x, 0.001, 0.965))
    calf_mid = Vector((x, 0.000, 0.780))
    shin = Vector((x, -0.002, 0.575))
    ankle = Vector((x, -0.005, 0.390))
    tube(
        "BareLeg." + side,
        (hip, upper_thigh, thigh_mid, knee, calf_upper, calf_mid, shin, ankle),
        (0.242, 0.237, 0.225, 0.184, 0.185, 0.174, 0.145, 0.108),
        "skin",
        (
            {"Hips": 0.70, prefix + "UpperLeg": 0.30},
            {"Hips": 0.34, prefix + "UpperLeg": 0.66},
            {prefix + "UpperLeg": 1.0},
            {prefix + "UpperLeg": 0.48, prefix + "LowerLeg": 0.52},
            {prefix + "LowerLeg": 1.0},
            {prefix + "LowerLeg": 1.0},
            {prefix + "LowerLeg": 1.0},
            {prefix + "LowerLeg": 0.72, prefix + "Foot": 0.28},
        ),
        30,
        0.96,
        cap=False,
    )
    tube(
        "AnkleFootBridge." + side,
        ((x, -0.002, 0.445), (x, 0.030, 0.340), (x, 0.075, 0.255)),
        (0.112, 0.116, 0.122),
        "skin",
        ({prefix + "LowerLeg": 0.68, prefix + "Foot": 0.32}, {prefix + "LowerLeg": 0.34, prefix + "Foot": 0.66}, {prefix + "Foot": 1.0}),
        24,
        0.92,
        cap=False,
    )
    foot_shell("BareFoot." + side, x, prefix + "Foot")
    toe_offsets = (-0.105, -0.052, 0.004, 0.057, 0.105)
    toe_sizes = (0.044, 0.052, 0.058, 0.054, 0.045)
    toe_lengths = (0.058, 0.072, 0.086, 0.078, 0.064)
    for toe_index, (offset, radius, length) in enumerate(zip(toe_offsets, toe_sizes, toe_lengths)):
        toe_x = x + offset * sign
        toe_y = -0.365 - (0.010 if toe_index in (2, 3) else 0.0)
        ellipsoid(
            "Toe.%s.%02d" % (side, toe_index),
            (toe_x, toe_y, 0.071),
            (radius, length, 0.047),
            "skin",
            prefix + "Toes",
            22,
            14,
        )


def build_armature():
    data = bpy.data.armatures.new("OlderSisterV1_HumanoidArmature")
    rig = bpy.data.objects.new("OlderSisterV1_HumanoidRig", data)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit = {}

    def add(name, head, tail, parent=None, deform=True, connected=False):
        bone = data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = deform
        if parent:
            bone.parent = edit[parent]
            bone.use_connect = connected
        edit[name] = bone
        return bone

    add("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.20), deform=False)
    add("Hips", (0.0, 0.0, 1.865), (0.0, 0.0, 2.060), "Root")
    add("Spine", (0.0, 0.0, 2.060), (0.0, 0.0, 2.315), "Hips", connected=True)
    add("Chest", (0.0, 0.0, 2.315), (0.0, 0.0, 2.545), "Spine", connected=True)
    add("UpperChest", (0.0, 0.0, 2.545), (0.0, 0.0, 2.740), "Chest", connected=True)
    add("Neck", (0.0, 0.0, 2.740), (0.0, 0.0, 2.875), "UpperChest", connected=True)
    add("Head", (0.0, 0.0, 2.875), (0.0, 0.0, 3.720), "Neck", connected=True)

    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        x = 0.245 * sign
        add(side + "UpperLeg", (x, 0.0, 1.865), (x, 0.0, 1.165), "Hips")
        add(side + "LowerLeg", (x, 0.0, 1.165), (x, -0.005, 0.390), side + "UpperLeg", connected=True)
        add(side + "Foot", (x, -0.005, 0.390), (x, -0.215, 0.155), side + "LowerLeg", connected=True)
        add(side + "Toes", (x, -0.215, 0.155), (x, -0.435, 0.105), side + "Foot", connected=True)

        add(side + "Shoulder", (0.090 * sign, 0.0, 2.670), (0.500 * sign, 0.0, 2.640), "UpperChest")
        add(side + "UpperArm", (0.500 * sign, 0.0, 2.640), (0.930 * sign, -0.012, 2.335), side + "Shoulder", connected=True)
        add(side + "LowerArm", (0.930 * sign, -0.012, 2.335), (1.235 * sign, -0.022, 1.975), side + "UpperArm", connected=True)
        add(side + "Hand", (1.235 * sign, -0.022, 1.975), (1.300 * sign, -0.030, 1.730), side + "LowerArm", connected=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    rig["humanoidContract"] = "FC-FAMILY-SHARED-HUMANOID-V1"
    rig["rootConvention"] = "BOTTOM_CENTER"
    rig["identityRole"] = "older_sister"
    return rig


RIG = build_armature()

# One visible, complete skinned mesh object and one atlas material.
bpy.ops.object.select_all(action="DESELECT")
for part in CHARACTER_PARTS:
    part.select_set(True)
bpy.context.view_layer.objects.active = HEAD
bpy.ops.object.join()
BODY = bpy.context.object
BODY.name = "OlderSisterV1_CompleteSkinnedBody"
for polygon in BODY.data.polygons:
    polygon.material_index = 0
while len(BODY.data.materials) > 1:
    BODY.data.materials.pop(index=1)
BODY.parent = RIG
armature_modifier = BODY.modifiers.new(name="OlderSisterV1_HumanoidSkin", type="ARMATURE")
armature_modifier.object = RIG
armature_modifier.use_vertex_groups = True


def weight_audit(obj):
    unweighted = 0
    max_influences = 0
    invalid_sum = 0
    for vertex in obj.data.vertices:
        influences = [group.weight for group in vertex.groups if group.weight > 0.0001]
        max_influences = max(max_influences, len(influences))
        if not influences:
            unweighted += 1
        elif abs(sum(influences) - 1.0) > 0.002:
            invalid_sum += 1
    return unweighted, max_influences, invalid_sum


UNWEIGHTED, MAX_INFLUENCES, INVALID_WEIGHT_SUM = weight_audit(BODY)
if UNWEIGHTED or INVALID_WEIGHT_SUM:
    raise RuntimeError("Skin audit failed: unweighted=%d invalidSum=%d" % (UNWEIGHTED, INVALID_WEIGHT_SUM))


def simple_material(name, color, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


floor_material = simple_material("M_ReviewFloor", (0.135, 0.145, 0.165), 0.86)
bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0.0, 0.0, 0.0))
FLOOR = bpy.context.object
FLOOR.name = "ReviewFloor"
FLOOR.data.materials.append(floor_material)


def add_area(name, location, energy, color, size):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 1.90)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


add_area("KeySoftbox", (-4.5, -5.8, 7.0), 1120.0, (1.0, 0.84, 0.72), 4.8)
add_area("FillSoftbox", (4.8, -3.8, 5.0), 760.0, (0.70, 0.84, 1.0), 4.2)
add_area("RimSoftbox", (0.0, 5.2, 6.2), 980.0, (1.0, 0.76, 0.66), 3.8)
add_area("TopSoftbox", (0.0, 0.0, 8.0), 470.0, (1.0, 0.95, 0.88), 3.2)

camera_data = bpy.data.cameras.new("OlderSisterIdentityReviewCamera")
CAMERA = bpy.data.objects.new("OlderSisterIdentityReviewCamera", camera_data)
bpy.context.collection.objects.link(CAMERA)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 4.35
scene.camera = CAMERA


def point_camera(location, target=(0.0, 0.0, 1.90)):
    CAMERA.location = location
    CAMERA.rotation_euler = (Vector(target) - CAMERA.location).to_track_quat("-Z", "Y").to_euler()


try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1024 if QUALITY == "final" else 640
scene.render.resolution_y = 1440 if QUALITY == "final" else 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.world.use_nodes = True
world_background = scene.world.node_tree.nodes.get("Background")
world_background.inputs["Color"].default_value = (0.052, 0.058, 0.074, 1.0)
world_background.inputs["Strength"].default_value = 0.36
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass


def clear_pose():
    for pose_bone in RIG.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def apply_walk_contact(left_forward=True):
    clear_pose()
    lead = 1.0 if left_forward else -1.0
    RIG.pose.bones["LeftUpperLeg"].rotation_euler.x = math.radians(23.0 * lead)
    RIG.pose.bones["RightUpperLeg"].rotation_euler.x = math.radians(-23.0 * lead)
    RIG.pose.bones["LeftLowerLeg"].rotation_euler.x = math.radians(-7.0 if left_forward else 29.0)
    RIG.pose.bones["RightLowerLeg"].rotation_euler.x = math.radians(29.0 if left_forward else -7.0)
    RIG.pose.bones["LeftFoot"].rotation_euler.x = math.radians(-9.0 * lead)
    RIG.pose.bones["RightFoot"].rotation_euler.x = math.radians(9.0 * lead)
    RIG.pose.bones["LeftUpperArm"].rotation_euler.x = math.radians(-18.0 * lead)
    RIG.pose.bones["RightUpperArm"].rotation_euler.x = math.radians(18.0 * lead)
    RIG.pose.bones["LeftLowerArm"].rotation_euler.x = math.radians(-5.0 * lead)
    RIG.pose.bones["RightLowerArm"].rotation_euler.x = math.radians(5.0 * lead)
    bpy.context.view_layer.update()


STATIC_VIEWS = {
    "front": (0.0, -8.5, 1.92),
    "left": (8.5, 0.0, 1.92),
    "back": (0.0, 8.5, 1.92),
    "three-quarter": (6.0, -6.2, 2.00),
}
STATIC_RENDER_PATHS = []
clear_pose()
for view_name, camera_location in STATIC_VIEWS.items():
    point_camera(camera_location)
    path = os.path.join(OUTPUT, "older-sister-blender-%s-v1-%s.png" % (view_name, QUALITY))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    STATIC_RENDER_PATHS.append(path)

DEFORMATION_RENDER_PATHS = []
TURNTABLE_RENDER_PATHS = []
if QUALITY == "final":
    point_camera((6.0, -6.2, 2.00))
    for pose_name, left_forward in (("p0-left-contact", True), ("p3-right-contact", False)):
        apply_walk_contact(left_forward)
        path = os.path.join(OUTPUT, "older-sister-blender-deform-%s-v1.png" % pose_name)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        DEFORMATION_RENDER_PATHS.append(path)
    clear_pose()

    turntable_directory = os.path.join(OUTPUT, "turntable")
    os.makedirs(turntable_directory, exist_ok=True)
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    camera_data.ortho_scale = 4.40
    for index in range(24):
        angle = 2.0 * math.pi * index / 24.0
        radius = 8.6
        point_camera((math.sin(angle) * radius, -math.cos(angle) * radius, 1.95))
        path = os.path.join(turntable_directory, "older-sister-turntable-%02d.png" % index)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        TURNTABLE_RENDER_PATHS.append(path)

clear_pose()
scene.render.resolution_x = 1024 if QUALITY == "final" else 640
scene.render.resolution_y = 1440 if QUALITY == "final" else 900
camera_data.ortho_scale = 4.35

# Export review candidate only; no production/default Unity path is touched.
bpy.ops.object.select_all(action="DESELECT")
BODY.select_set(True)
RIG.select_set(True)
bpy.context.view_layer.objects.active = RIG
fbx_path = os.path.join(OUTPUT, "older-sister-blender-humanoid-v1.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    use_armature_deform_only=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    path_mode="COPY",
    embed_textures=True,
)

blend_path = os.path.join(OUTPUT, "older-sister-blender-identity-v1.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

world_bounds = [BODY.matrix_world @ Vector(corner) for corner in BODY.bound_box]
bounds_min = [min(point[index] for point in world_bounds) for index in range(3)]
bounds_max = [max(point[index] for point in world_bounds) for index in range(3)]
receipt = {
    "contract": "FC-OLDER-SISTER-BLENDER-IDENTITY-V1",
    "status": "CANDIDATE_VISUAL_REVIEW_REQUIRED_DO_NOT_PROMOTE",
    "quality": QUALITY,
    "identity": {
        "role": "older_sister",
        "age": 20,
        "hair": "long-black-twin-tails-with-black-bows",
        "eyes": "teal",
        "outfit": "fitted-dark-sleeveless-top-white-piping-and-navy-dolphin-shorts",
        "feet": "two-complete-bare-feet",
    },
    "sourcePolicy": {
        "newTopology": True,
        "rejectedPlayerMeshImported": False,
        "stylooMeshImported": False,
        "legacy2DAssetUsed": False,
        "rSeriesAssetUsed": False,
        "turnaroundUsedAsDecalOrTexture": False,
    },
    "reference": {"path": REFERENCE, "sha256": sha256(REFERENCE)},
    "blenderVersion": bpy.app.version_string,
    "bodyObject": BODY.name,
    "skinnedMeshObjectCount": 1,
    "vertexCount": len(BODY.data.vertices),
    "polygonCount": len(BODY.data.polygons),
    "materialCount": len(BODY.data.materials),
    "textureAtlas": ATLAS_PATH,
    "armatureObject": RIG.name,
    "boneCount": len(RIG.data.bones),
    "boneNames": sorted(bone.name for bone in RIG.data.bones),
    "skinAudit": {
        "unweightedVertexCount": UNWEIGHTED,
        "invalidNormalizedWeightSumCount": INVALID_WEIGHT_SUM,
        "maxInfluencesPerVertex": MAX_INFLUENCES,
    },
    "boundsMin": bounds_min,
    "boundsMax": bounds_max,
    "standingHeight": bounds_max[2] - bounds_min[2],
    "rootConvention": "BOTTOM_CENTER",
    "outputs": {
        "blend": blend_path,
        "fbx": fbx_path,
        "atlas": ATLAS_PATH,
        "staticRenders": STATIC_RENDER_PATHS,
        "deformationRenders": DEFORMATION_RENDER_PATHS,
        "turntableRenders": TURNTABLE_RENDER_PATHS,
    },
    "productionEligible": False,
}
receipt_path = os.path.join(OUTPUT, "build-receipt.json")
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("OLDER_SISTER_V1_BUILD: PASS")
print(json.dumps(receipt, ensure_ascii=False, indent=2))
