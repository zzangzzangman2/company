"""Build the isolated Player V3 identity candidate from new procedural topology.

This script intentionally does not import, append, or inspect the rejected V1/V2
blend/FBX meshes.  The committed four-view turnaround is recorded as the identity
reference only; it is never projected onto the model as a decal or texture.
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
    "skin": (0.94, 0.58, 0.39),
    "skin_light": (1.00, 0.72, 0.53),
    "skin_blush": (0.96, 0.38, 0.30),
    "hair": (0.105, 0.040, 0.019),
    "hair_highlight": (0.235, 0.095, 0.045),
    "eye_white": (0.985, 0.982, 0.955),
    "iris": (0.38, 0.135, 0.028),
    "pupil": (0.012, 0.006, 0.004),
    "white": (0.93, 0.94, 0.94),
    "white_shadow": (0.72, 0.76, 0.80),
    "navy": (0.018, 0.050, 0.108),
    "denim": (0.018, 0.058, 0.120),
    "denim_highlight": (0.045, 0.105, 0.182),
    "red": (0.60, 0.018, 0.024),
    "yellow": (0.96, 0.52, 0.045),
    "mouth": (0.42, 0.045, 0.028),
    "sole": (0.80, 0.82, 0.83),
    "metal": (0.63, 0.67, 0.70),
}
ATLAS_KEYS = tuple(PALETTE.keys())
ATLAS_COLS = 5
ATLAS_ROWS = 4
ATLAS_SIZE = 640 if QUALITY == "final" else 320
ATLAS_PATH = os.path.join(OUTPUT, "player-v6-blender-identity-v3-atlas.png")


def clamp01(value):
    return max(0.0, min(1.0, value))


def make_atlas():
    image = bpy.data.images.new("PlayerV3IdentityAtlas", width=ATLAS_SIZE, height=ATLAS_SIZE, alpha=True)
    pixels = [0.0] * (ATLAS_SIZE * ATLAS_SIZE * 4)
    cell_w = ATLAS_SIZE // ATLAS_COLS
    cell_h = ATLAS_SIZE // ATLAS_ROWS
    for y in range(ATLAS_SIZE):
        row = min(y // cell_h, ATLAS_ROWS - 1)
        for x in range(ATLAS_SIZE):
            col = min(x // cell_w, ATLAS_COLS - 1)
            index = row * ATLAS_COLS + col
            key = ATLAS_KEYS[index] if index < len(ATLAS_KEYS) else "white"
            base = PALETTE[key]
            lx = x % cell_w
            ly = y % cell_h
            # Deterministic micro texture.  It gives fabric and hair a readable
            # surface without importing any legacy raster or identity donor.
            grain = (((x * 73856093) ^ (y * 19349663)) & 255) / 255.0 - 0.5
            weave = math.sin(lx * 0.44) * math.sin(ly * 0.49)
            diagonal = math.sin((lx + ly * 1.7) * 0.17)
            strength = 0.006
            if key in ("white", "white_shadow", "navy", "red", "yellow"):
                strength = 0.018
                grain = grain * 0.45 + weave * 0.55
            elif key in ("denim", "denim_highlight"):
                strength = 0.034
                grain = grain * 0.25 + diagonal * 0.75
            elif key in ("hair", "hair_highlight"):
                strength = 0.040
                grain = grain * 0.20 + math.sin((lx * 0.22) + (ly * 0.055)) * 0.80
            elif key.startswith("skin"):
                strength = 0.007
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
    material = bpy.data.materials.new("M_PlayerV3_IdentityAtlas")
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
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.61
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.28
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


CHARACTER_MATERIAL = build_character_material()
CHARACTER_PARTS = []


def palette_tile(key):
    index = ATLAS_KEYS.index(key)
    col = index % ATLAS_COLS
    row = index // ATLAS_COLS
    return col, row


def assign_uv_tile(obj, key):
    layer = obj.data.uv_layers.new(name="PlayerV3AtlasUV")
    col, row = palette_tile(key)
    tile_w = 1.0 / ATLAS_COLS
    tile_h = 1.0 / ATLAS_ROWS
    padding_u = tile_w * 0.12
    padding_v = tile_h * 0.12
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
    vertices = []
    faces = []
    vertices.append(tuple(center + Vector((0.0, 0.0, radii[2]))))
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


def loft(name, ring_specs, palette_key, ring_weights, segments=32, exponent=2.0, cap=True):
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


def hair_lock(name, root, control_a, control_b, tip, width, palette_key="hair", bone="Head", roll=0.0):
    root = Vector(root)
    control_a = Vector(control_a)
    control_b = Vector(control_b)
    tip = Vector(tip)
    samples = 8
    points = [cubic(root, control_a, control_b, tip, index / (samples - 1)) for index in range(samples)]
    radii = []
    for index in range(samples):
        t = index / (samples - 1)
        taper = max(0.08, (1.0 - t) ** 0.72)
        belly = 0.82 + math.sin(math.pi * t) * 0.34
        radii.append(width * taper * belly)
    obj = tube(name, points, radii, palette_key, [{bone: 1.0}] * samples, segments=12, flatten=0.47)
    if abs(roll) > 1.0e-5:
        # A subtle object-space roll varies the planar lock silhouettes.
        obj.rotation_euler[2] = roll
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        obj.select_set(False)
    return obj


def ribbon(name, rows, palette_key, weights, thickness=0.012):
    # rows: (z, half_width, front_y, x_center)
    vertices = []
    faces = []
    vertex_weights = []
    for index, row in enumerate(rows):
        z, half_width, y, x_center = row
        mapping = weights[index] if isinstance(weights, list) else weights
        vertices.extend(
            (
                (x_center - half_width, y - thickness * 0.5, z),
                (x_center + half_width, y - thickness * 0.5, z),
                (x_center - half_width, y + thickness * 0.5, z),
                (x_center + half_width, y + thickness * 0.5, z),
            )
        )
        vertex_weights.extend((dict(mapping), dict(mapping), dict(mapping), dict(mapping)))
    for row in range(len(rows) - 1):
        a = row * 4
        b = (row + 1) * 4
        faces.extend(
            (
                (a, b, b + 1, a + 1),
                (a + 3, b + 3, b + 2, a + 2),
                (a, a + 2, b + 2, b),
                (a + 1, b + 1, b + 3, a + 3),
            )
        )
    faces.extend(((0, 1, 3, 2), (len(vertices) - 4, len(vertices) - 2, len(vertices) - 1, len(vertices) - 3)))
    return create_mesh(name, vertices, faces, palette_key, vertex_weights, smooth=False)


def shoe_shell(name, x_center, slices, palette_key, bone):
    """Create a rounded sneaker with a flat sole and an ankle-height heel.

    Each slice is (y, half_width, base_z, height).  The eight-point perimeter
    keeps the underside flat while rounding the sidewall, vamp, and toe.
    """
    vertices = []
    faces = []
    weights = []
    arc_steps = 18
    profile = [(-0.82, 0.00)]
    profile.extend((math.cos(math.pi - math.pi * step / arc_steps), math.sin(math.pi - math.pi * step / arc_steps)) for step in range(arc_steps + 1))
    profile.append((0.82, 0.00))
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
    return create_mesh(name, vertices, faces, palette_key, weights)


def elliptical_band(name, z0, z1, center_y, radius_x, radius_y, palette_key, weights, start_angle=0.0, end_angle=2.0 * math.pi, segments=40):
    vertices = []
    faces = []
    vertex_weights = []
    closed = abs((end_angle - start_angle) - 2.0 * math.pi) < 1.0e-5
    count = segments if closed else segments + 1
    for z, mapping in ((z0, weights[0]), (z1, weights[1])):
        for index in range(count):
            denominator = segments if closed else max(1, count - 1)
            angle = start_angle + (end_angle - start_angle) * index / denominator
            vertices.append((radius_x * math.cos(angle), center_y + radius_y * math.sin(angle), z))
            vertex_weights.append(dict(mapping))
    for index in range(segments):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    return create_mesh(name, vertices, faces, palette_key, vertex_weights)


def eye_patch(name, center, width, height, depth, palette_key, bone="Head", segments=32):
    center = Vector(center)
    vertices = [tuple(center - Vector((0.0, depth, 0.0)))]
    faces = []
    for index in range(segments):
        angle = 2.0 * math.pi * index / segments
        x = math.cos(angle) * width
        z = math.sin(angle) * height
        # Convex but nearly flush lens; no stacked bulging eye balls.
        y = -depth * (0.78 + 0.22 * abs(math.cos(angle)))
        vertices.append(tuple(center + Vector((x, y, z))))
    for index in range(segments):
        faces.append((0, 1 + index, 1 + ((index + 1) % segments)))
    return create_mesh(name, vertices, faces, palette_key, bone)


def head_deform(normal, point):
    nz = normal.z
    cheek = 1.0 + 0.085 * math.exp(-((nz + 0.18) / 0.30) ** 2)
    chin = 1.0 - 0.24 * max(0.0, -nz) ** 1.65
    point.x *= cheek * chin
    point.y *= 1.0 - 0.08 * max(0.0, -nz)
    if normal.y < 0.0:
        point.y *= 1.0 + 0.055 * math.exp(-((nz + 0.10) / 0.42) ** 2)
    point.z -= 0.018 * max(0.0, -nz)
    return point


# --- New body topology -----------------------------------------------------

HEAD = ellipsoid("HeadSkin", (0.0, -0.015, 2.725), (0.338, 0.295, 0.388), "skin", "Head", 52, 32, head_deform)
ellipsoid("Ear.L", (0.341, 0.000, 2.690), (0.055, 0.040, 0.082), "skin", "Head", 24, 16)
ellipsoid("Ear.R", (-0.341, 0.000, 2.690), (0.055, 0.040, 0.082), "skin", "Head", 24, 16)
tube("EarFold.L", ((0.358, -0.031, 2.735), (0.372, -0.043, 2.695), (0.358, -0.035, 2.655)), (0.009, 0.012, 0.007), "skin_light", [{"Head": 1.0}] * 3, 10, 0.72)
tube("EarFold.R", ((-0.358, -0.031, 2.735), (-0.372, -0.043, 2.695), (-0.358, -0.035, 2.655)), (0.009, 0.012, 0.007), "skin_light", [{"Head": 1.0}] * 3, 10, 0.72)

for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    x = 0.126 * sign
    eye_patch("EyeSclera." + suffix, (x, -0.302, 2.735), 0.083, 0.105, 0.007, "eye_white")
    eye_patch("Iris." + suffix, (x, -0.311, 2.731), 0.043, 0.061, 0.004, "iris")
    eye_patch("Pupil." + suffix, (x, -0.317, 2.731), 0.021, 0.039, 0.003, "pupil")
    eye_patch("EyeGlint." + suffix, (x - 0.014 * sign, -0.329, 2.760), 0.008, 0.014, 0.002, "eye_white", segments=18)
    tube(
        "UpperLid." + suffix,
        ((x - 0.073 * sign, -0.316, 2.765), (x, -0.327, 2.815), (x + 0.075 * sign, -0.315, 2.770)),
        (0.008, 0.011, 0.006),
        "hair",
        [{"Head": 1.0}] * 3,
        10,
        0.55,
    )
    tube(
        "Brow." + suffix,
        ((x - 0.071 * sign, -0.310, 2.867), (x, -0.322, 2.885), (x + 0.069 * sign, -0.311, 2.866)),
        (0.006, 0.010, 0.005),
        "hair",
        [{"Head": 1.0}] * 3,
        9,
        0.55,
    )
    ellipsoid("Cheek." + suffix, (0.214 * sign, -0.307, 2.624), (0.045, 0.010, 0.020), "skin_blush", "Head", 20, 12)

ellipsoid("Nose", (0.0, -0.329, 2.655), (0.018, 0.014, 0.025), "skin_light", "Head", 20, 12)
tube("Smile", ((-0.066, -0.328, 2.574), (0.0, -0.338, 2.558), (0.067, -0.328, 2.574)), (0.005, 0.007, 0.004), "mouth", [{"Head": 1.0}] * 3, 10, 0.52)


def hair_cap():
    segments = 48
    rings = 18
    vertices = []
    faces = []
    weights = []
    for ring in range(rings + 1):
        t = ring / rings
        for segment in range(segments):
            phi = 2.0 * math.pi * segment / segments
            frontness = max(0.0, -math.sin(phi))
            theta_max = 2.22 - 0.82 * frontness
            theta = 0.055 + (theta_max - 0.055) * t
            point = Vector(
                (
                    0.365 * math.sin(theta) * math.cos(phi),
                    0.323 * math.sin(theta) * math.sin(phi) + 0.014,
                    2.765 + 0.410 * math.cos(theta),
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

# Layered, tapered locks.  Each lock is a curved pointed surface rather than a
# sphere/capsule, avoiding the rejected V2 sausage silhouette.
front_roots = (-0.265, -0.185, -0.095, 0.000, 0.095, 0.185, 0.265)
front_tips = (-0.238, -0.170, -0.085, 0.018, 0.105, 0.188, 0.250)
for index, (root_x, tip_x) in enumerate(zip(front_roots, front_tips)):
    root_z = 3.080 - 0.020 * abs(index - 3)
    tip_z = 2.770 + 0.022 * abs(index - 3)
    hair_lock(
        "HairBang.%02d" % index,
        (root_x * 0.70, -0.205, root_z),
        (root_x, -0.302, 3.020),
        (tip_x, -0.345, 2.885),
        (tip_x, -0.325, tip_z),
        0.060 if index in (2, 3, 4) else 0.052,
        "hair_highlight" if index in (1, 4) else "hair",
        roll=math.radians((index - 3) * 1.8),
    )

for side_sign, side in ((1.0, "L"), (-1.0, "R")):
    for index in range(5):
        z = 3.055 - index * 0.095
        root = (0.17 * side_sign, -0.02 + index * 0.020, 3.11 - index * 0.040)
        tip = ((0.365 + index * 0.010) * side_sign, -0.105 + index * 0.030, z - 0.18)
        hair_lock(
            "HairSide%s.%02d" % (side, index),
            root,
            ((0.27 + index * 0.010) * side_sign, -0.12, z + 0.02),
            ((0.39 + index * 0.008) * side_sign, -0.10 + index * 0.030, z - 0.08),
            tip,
            0.055 - index * 0.002,
            "hair_highlight" if index == 1 else "hair",
        )

back_angles = tuple(math.radians(value) for value in (20, 45, 70, 95, 120, 145, 170))
for index, angle in enumerate(back_angles):
    x = 0.24 * math.cos(angle)
    y = 0.18 + 0.12 * math.sin(angle)
    tip_x = 0.31 * math.cos(angle)
    tip_y = 0.22 + 0.12 * math.sin(angle)
    hair_lock(
        "HairBack.%02d" % index,
        (x * 0.55, 0.10, 3.09),
        (x, y, 3.03),
        (tip_x, tip_y, 2.79),
        (tip_x * 0.90, tip_y, 2.555 + 0.035 * abs(index - 3)),
        0.060,
        "hair_highlight" if index in (1, 5) else "hair",
    )

# Dark underlayer closes the nape behind the pointed locks so neither scalp nor
# neck can flash through from rear and three-quarter review angles.
loft(
    "HairNapeUnderlayer",
    (
        ((0.0, 0.125, 2.390), 0.105, 0.068),
        ((0.0, 0.145, 2.440), 0.205, 0.110),
        ((0.0, 0.155, 2.505), 0.305, 0.145),
        ((0.0, 0.130, 2.575), 0.335, 0.155),
    ),
    "hair",
    ({"Head": 1.0}, {"Head": 1.0}, {"Head": 1.0}, {"Head": 1.0}),
    36,
    exponent=2.0,
)

# A second staggered nape layer hides the scalp-shell boundary and produces the
# tapered back silhouette visible in the locked turnaround.
for index, x in enumerate((-0.285, -0.225, -0.165, -0.105, -0.035, 0.035, 0.105, 0.165, 0.225, 0.285)):
    stagger = 0.022 if index % 2 else -0.012
    hair_lock(
        "HairNape.%02d" % index,
        (x * 0.66, 0.205, 2.895 + stagger),
        (x * 0.88, 0.300, 2.800 + stagger),
        (x, 0.325, 2.660 + stagger),
        (x * 0.92, 0.330, 2.385 + stagger + 0.115 * abs(index - 4.5) / 4.5),
        0.056,
        "hair_highlight" if index in (2, 7) else "hair",
    )

for index, (root, a, b, tip, width) in enumerate(
    (
        ((-0.050, 0.005, 3.105), (-0.095, 0.000, 3.195), (-0.095, -0.010, 3.235), (-0.145, -0.010, 3.205), 0.034),
        ((0.015, 0.015, 3.125), (0.025, 0.010, 3.225), (0.075, 0.010, 3.255), (0.105, 0.005, 3.205), 0.036),
        ((0.105, 0.030, 3.110), (0.175, 0.035, 3.190), (0.225, 0.035, 3.205), (0.245, 0.020, 3.160), 0.031),
    )
):
    hair_lock("HairCrown.%02d" % index, root, a, b, tip, width, "hair_highlight" if index == 1 else "hair")

# Neck and tailored hoodie torso.
ellipsoid("NeckSkin", (0.0, 0.0, 2.325), (0.104, 0.092, 0.135), "skin", {"Neck": 0.8, "Head": 0.2}, 28, 18)
torso_specs = (
    ((0.0, 0.018, 1.455), 0.325, 0.195),
    ((0.0, 0.015, 1.525), 0.342, 0.202),
    ((0.0, 0.012, 1.720), 0.356, 0.211),
    ((0.0, 0.010, 1.940), 0.382, 0.218),
    ((0.0, 0.007, 2.105), 0.414, 0.218),
    ((0.0, 0.004, 2.205), 0.426, 0.202),
    ((0.0, 0.001, 2.270), 0.342, 0.166),
    ((0.0, 0.000, 2.290), 0.290, 0.145),
)
torso_weights = (
    {"Hips": 0.72, "Spine": 0.28},
    {"Hips": 0.45, "Spine": 0.55},
    {"Spine": 0.72, "Chest": 0.28},
    {"Spine": 0.55, "Chest": 0.45},
    {"Chest": 0.62, "UpperChest": 0.38},
    {"UpperChest": 0.74, "Chest": 0.26},
    {"UpperChest": 0.92, "Chest": 0.08},
    {"UpperChest": 1.0},
)
loft("HoodieTailoredTorso", torso_specs, "white", torso_weights, 40, exponent=2.05)

# Shirt is inset into the opening rather than being a second inflated torso.
shirt_rows = (
    (1.500, 0.105, -0.213, 0.0),
    (1.760, 0.112, -0.229, 0.0),
    (2.030, 0.118, -0.232, 0.0),
    (2.220, 0.098, -0.206, 0.0),
)
shirt_weights = (
    {"Spine": 1.0},
    {"Spine": 0.60, "Chest": 0.40},
    {"Chest": 0.70, "UpperChest": 0.30},
    {"UpperChest": 1.0},
)
ribbon("StripedShirtBase", shirt_rows, "navy", list(shirt_weights), thickness=0.016)
for stripe_index, z in enumerate((1.625, 1.815, 2.005)):
    mapping = {"Spine": 1.0} if stripe_index == 0 else ({"Chest": 1.0} if stripe_index == 1 else {"UpperChest": 0.35, "Chest": 0.65})
    ribbon(
        "ShirtYellowStripe.%02d" % stripe_index,
        ((z - 0.040, 0.124 + stripe_index * 0.004, -0.249, 0.0), (z + 0.040, 0.124 + stripe_index * 0.004, -0.249, 0.0)),
        "yellow",
        mapping,
        thickness=0.010,
    )
ribbon("ShirtRedCollar", ((2.195, 0.109, -0.234, 0.0), (2.245, 0.090, -0.221, 0.0)), "red", {"UpperChest": 1.0}, thickness=0.012)

# Open-front edges and metal zipper teeth.
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.124 * sign
    tube(
        "HoodieOpenEdge." + side,
        ((x, -0.232, 1.485), (x, -0.252, 1.840), (x, -0.235, 2.190), (0.105 * sign, -0.205, 2.275)),
        (0.012, 0.011, 0.011, 0.010),
        "navy",
        (
            {"Spine": 1.0},
            {"Spine": 0.45, "Chest": 0.55},
            {"UpperChest": 0.62, "Chest": 0.38},
            {"UpperChest": 1.0},
        ),
        12,
        0.66,
    )
    for tooth_index in range(11):
        z = 1.535 + tooth_index * 0.058
        bone = "Spine" if z < 1.80 else ("Chest" if z < 2.10 else "UpperChest")
        tube(
            "ZipperTooth.%s.%02d" % (side, tooth_index),
            ((x - 0.009 * sign, -0.242, z), (x + 0.009 * sign, -0.242, z)),
            (0.004, 0.004),
            "metal",
            [{bone: 1.0}, {bone: 1.0}],
            8,
            0.70,
        )

# Chest color blocks, back wrap bands, pockets, and layered hem.
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.276 * sign
    ribbon("ChestNavy." + side, ((2.055, 0.087, -0.245, x), (2.125, 0.087, -0.245, x)), "navy", {"UpperChest": 0.45, "Chest": 0.55}, 0.012)
    ribbon("ChestRed." + side, ((2.125, 0.087, -0.246, x), (2.178, 0.087, -0.242, x)), "red", {"UpperChest": 0.75, "Chest": 0.25}, 0.012)
    tube(
        "PocketPiping." + side,
        ((0.300 * sign, -0.244, 1.690), (0.275 * sign, -0.258, 1.765), (0.245 * sign, -0.250, 1.825)),
        (0.009, 0.011, 0.008),
        "navy",
        ({"Spine": 0.72, "Chest": 0.28}, {"Spine": 0.60, "Chest": 0.40}, {"Spine": 0.45, "Chest": 0.55}),
        10,
        0.58,
    )

elliptical_band("BackNavyBand", 2.045, 2.120, 0.010, 0.424, 0.237, "navy", ({"Chest": 0.60, "UpperChest": 0.40}, {"Chest": 0.35, "UpperChest": 0.65}), math.radians(12), math.radians(168), 30)
elliptical_band("BackRedBand", 2.120, 2.172, 0.010, 0.418, 0.238, "red", ({"Chest": 0.25, "UpperChest": 0.75}, {"UpperChest": 1.0}), math.radians(12), math.radians(168), 30)
elliptical_band("HoodieHemNavy", 1.455, 1.520, 0.018, 0.350, 0.211, "navy", ({"Hips": 0.66, "Spine": 0.34}, {"Hips": 0.52, "Spine": 0.48}), segments=40)
elliptical_band("HoodieHemWhite", 1.515, 1.548, 0.017, 0.355, 0.214, "white_shadow", ({"Hips": 0.48, "Spine": 0.52}, {"Hips": 0.42, "Spine": 0.58}), segments=40)

# Flattened draped hood with seam and dark inner collar.
hood_specs = (
    ((0.0, 0.145, 2.105), 0.170, 0.072),
    ((0.0, 0.195, 2.160), 0.258, 0.125),
    ((0.0, 0.220, 2.245), 0.320, 0.155),
    ((0.0, 0.195, 2.325), 0.300, 0.142),
    ((0.0, 0.125, 2.385), 0.215, 0.090),
)
loft("HoodDrapedShell", hood_specs, "white", ({"UpperChest": 0.65, "Chest": 0.35}, {"UpperChest": 0.80, "Chest": 0.20}, {"UpperChest": 1.0}, {"UpperChest": 1.0}, {"UpperChest": 1.0}), 40, exponent=2.05)
elliptical_band("HoodInnerCollar", 2.292, 2.326, -0.004, 0.190, 0.122, "navy", ({"UpperChest": 1.0}, {"UpperChest": 1.0}), segments=36)
tube("HoodCenterSeam", ((0.0, 0.320, 2.160), (0.0, 0.355, 2.245), (0.0, 0.285, 2.350)), (0.007, 0.009, 0.006), "white_shadow", [{"UpperChest": 1.0}] * 3, 10, 0.55)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.110 * sign
    tube("Drawstring." + side, ((x, -0.207, 2.310), (x + 0.008 * sign, -0.245, 2.130), (x + 0.015 * sign, -0.255, 1.995)), (0.008, 0.007, 0.006), "navy", ({"UpperChest": 1.0}, {"UpperChest": 0.65, "Chest": 0.35}, {"Chest": 1.0}), 10, 0.62)
    tube("DrawstringTip." + side, ((x + 0.015 * sign, -0.255, 2.010), (x + 0.015 * sign, -0.255, 1.960)), (0.010, 0.008), "red", [{"Chest": 1.0}] * 2, 10, 0.68)

# Sleeves use continuous multi-ring tubes with elbow blend loops.
for sign, side, prefix in ((1.0, "L", "Left"), (-1.0, "R", "Right")):
    shoulder = Vector((0.382 * sign, 0.000, 2.185))
    upper_mid = Vector((0.530 * sign, -0.006, 2.015))
    elbow = Vector((0.650 * sign, -0.012, 1.820))
    fore_mid = Vector((0.725 * sign, -0.017, 1.650))
    wrist = Vector((0.782 * sign, -0.020, 1.500))
    sleeve_weights = (
        {prefix + "UpperArm": 1.0},
        {prefix + "UpperArm": 1.0},
        {prefix + "UpperArm": 0.48, prefix + "LowerArm": 0.52},
        {prefix + "LowerArm": 1.0},
        {prefix + "LowerArm": 0.82, prefix + "Hand": 0.18},
    )
    ellipsoid("SleeveShoulder." + side, shoulder, (0.138, 0.126, 0.145), "white", {prefix + "UpperArm": 0.88, prefix + "Shoulder": 0.12}, 30, 18)
    tube("HoodieSleeve." + side, (shoulder, upper_mid, elbow, fore_mid, wrist), (0.132, 0.127, 0.121, 0.110, 0.101), "white", sleeve_weights, 26, 0.94)
    cuff_axis = (wrist - fore_mid).normalized()
    cuff_start = wrist - cuff_axis * 0.055
    cuff_end = wrist + cuff_axis * 0.095
    tube("CuffNavy." + side, (cuff_start, cuff_end), (0.111, 0.105), "navy", [{prefix + "LowerArm": 0.75, prefix + "Hand": 0.25}, {prefix + "LowerArm": 0.35, prefix + "Hand": 0.65}], 22, 0.95)
    white_start = cuff_start - cuff_axis * 0.030
    tube("CuffWhiteStripe." + side, (white_start, cuff_start), (0.113, 0.112), "white_shadow", [{prefix + "LowerArm": 1.0}] * 2, 20, 0.95)

    palm_center = Vector((0.808 * sign, -0.022, 1.398))
    ellipsoid("Palm." + side, palm_center, (0.080, 0.056, 0.124), "skin", prefix + "Hand", 28, 18)
    finger_x_offsets = (-0.043, -0.014, 0.015, 0.043)
    for finger_index, local_x in enumerate(finger_x_offsets):
        finger_x = palm_center.x + local_x * sign
        top = Vector((finger_x, -0.030, 1.365))
        length = 0.112 - abs(finger_index - 1.5) * 0.008
        bottom = Vector((finger_x + 0.005 * sign, -0.035, 1.365 - length))
        tube("Finger.%s.%02d" % (side, finger_index), (top, (top + bottom) * 0.5, bottom), (0.0140, 0.013, 0.008), "skin", [{prefix + "Hand": 1.0}] * 3, 12, 0.78)
    thumb_root = Vector((palm_center.x - 0.050 * sign, -0.032, 1.425))
    thumb_tip = Vector((palm_center.x - 0.098 * sign, -0.045, 1.345))
    tube("Thumb." + side, (thumb_root, (thumb_root + thumb_tip) * 0.5 + Vector((0.0, -0.006, 0.012)), thumb_tip), (0.017, 0.014, 0.008), "skin", [{prefix + "Hand": 1.0}] * 3, 12, 0.82)

# Trousers: full pelvis and deformation-friendly legs with knee/ankle blend loops.
pelvis_specs = (
    ((0.0, 0.008, 1.400), 0.275, 0.180),
    ((0.0, 0.004, 1.505), 0.300, 0.188),
    ((0.0, 0.000, 1.610), 0.285, 0.180),
)
loft("TrouserPelvis", pelvis_specs, "denim", ({"Hips": 1.0}, {"Hips": 1.0}, {"Hips": 0.82, "Spine": 0.18}), 36, exponent=2.55)
tube("FlySeam", ((0.0, -0.187, 1.575), (0.0, -0.195, 1.475), (0.0, -0.183, 1.410)), (0.005, 0.007, 0.004), "denim_highlight", [{"Hips": 1.0}] * 3, 9, 0.60)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    ribbon(
        "BackPocket." + side,
        ((1.425, 0.075, 0.187, 0.142 * sign), (1.525, 0.082, 0.193, 0.142 * sign)),
        "denim_highlight",
        {"Hips": 1.0},
        thickness=0.008,
    )

for sign, side, prefix in ((1.0, "L", "Left"), (-1.0, "R", "Right")):
    x = 0.158 * sign
    points = (
        (x, 0.002, 1.530),
        (x, 0.002, 1.285),
        (x, 0.000, 1.050),
        (x, -0.002, 0.805),
        (x, -0.006, 0.545),
        (x, -0.010, 0.350),
    )
    leg_weights = (
        {prefix + "UpperLeg": 0.82, "Hips": 0.18},
        {prefix + "UpperLeg": 1.0},
        {prefix + "UpperLeg": 0.48, prefix + "LowerLeg": 0.52},
        {prefix + "LowerLeg": 1.0},
        {prefix + "LowerLeg": 1.0},
        {prefix + "LowerLeg": 0.72, prefix + "Foot": 0.28},
    )
    tube("TrouserLeg." + side, points, (0.154, 0.150, 0.146, 0.142, 0.137, 0.136), "denim", leg_weights, 28, 0.92)
    tube("TrouserCuff." + side, ((x, -0.010, 0.420), (x, -0.012, 0.290)), (0.147, 0.145), "denim_highlight", ({prefix + "LowerLeg": 0.85, prefix + "Foot": 0.15}, {prefix + "LowerLeg": 0.48, prefix + "Foot": 0.52}), 26, 0.93)

    shoe_shell(
        "SneakerUpper." + side,
        x,
        ((0.165, 0.125, 0.045, 0.295), (0.075, 0.136, 0.045, 0.305), (-0.080, 0.150, 0.045, 0.260), (-0.235, 0.166, 0.045, 0.205), (-0.365, 0.158, 0.045, 0.150)),
        "white",
        prefix + "Foot",
    )
    shoe_shell(
        "SneakerSole." + side,
        x,
        ((0.175, 0.137, 0.018, 0.065), (0.020, 0.158, 0.018, 0.068), (-0.190, 0.174, 0.018, 0.070), (-0.390, 0.169, 0.018, 0.068)),
        "sole",
        prefix + "Foot",
    )
    # Tongue and laces sit flush to the upper rather than floating boxes.
    ribbon("ShoeTongue." + side, ((0.185, 0.072, -0.295, x), (0.318, 0.060, -0.055, x)), "navy", {prefix + "Foot": 1.0}, thickness=0.014)
    for lace_index in range(4):
        y = -0.255 + lace_index * 0.047
        z = 0.245 + lace_index * 0.018
        tube("ShoeLace.%s.%02d" % (side, lace_index), ((x - 0.085, y, z), (x + 0.085, y, z)), (0.008, 0.008), "white_shadow", [{prefix + "Foot": 1.0}] * 2, 9, 0.70)
    outer_x = x + 0.150 * sign
    tube("ShoeNavyMark." + side, ((outer_x, 0.035, 0.165), (outer_x, -0.100, 0.158), (outer_x, -0.235, 0.130)), (0.018, 0.022, 0.014), "navy", [{prefix + "Foot": 1.0}] * 3, 10, 0.52)
    tube("ShoeRedMark." + side, ((outer_x + 0.002 * sign, -0.105, 0.157), (outer_x + 0.002 * sign, -0.175, 0.145)), (0.020, 0.016), "red", [{prefix + "Foot": 1.0}] * 2, 10, 0.52)


def build_armature():
    data = bpy.data.armatures.new("PlayerV3_HumanoidArmature")
    rig = bpy.data.objects.new("PlayerV3_HumanoidRig", data)
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

    add("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.18), deform=False)
    add("Hips", (0.0, 0.0, 1.510), (0.0, 0.0, 1.700), "Root")
    add("Spine", (0.0, 0.0, 1.700), (0.0, 0.0, 1.930), "Hips", connected=True)
    add("Chest", (0.0, 0.0, 1.930), (0.0, 0.0, 2.145), "Spine", connected=True)
    add("UpperChest", (0.0, 0.0, 2.145), (0.0, 0.0, 2.290), "Chest", connected=True)
    add("Neck", (0.0, 0.0, 2.290), (0.0, 0.0, 2.410), "UpperChest", connected=True)
    add("Head", (0.0, 0.0, 2.410), (0.0, 0.0, 3.125), "Neck", connected=True)

    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        x = 0.158 * sign
        add(side + "UpperLeg", (x, 0.0, 1.535), (x, 0.0, 1.050), "Hips")
        add(side + "LowerLeg", (x, 0.0, 1.050), (x, -0.006, 0.420), side + "UpperLeg", connected=True)
        add(side + "Foot", (x, -0.006, 0.420), (x, -0.200, 0.170), side + "LowerLeg", connected=True)
        add(side + "Toes", (x, -0.200, 0.170), (x, -0.420, 0.135), side + "Foot", connected=True)

        add(side + "Shoulder", (0.070 * sign, 0.0, 2.235), (0.382 * sign, 0.0, 2.185), "UpperChest")
        add(side + "UpperArm", (0.382 * sign, 0.0, 2.185), (0.650 * sign, -0.012, 1.820), side + "Shoulder", connected=True)
        add(side + "LowerArm", (0.650 * sign, -0.012, 1.820), (0.782 * sign, -0.020, 1.500), side + "UpperArm", connected=True)
        add(side + "Hand", (0.782 * sign, -0.020, 1.500), (0.812 * sign, -0.025, 1.255), side + "LowerArm", connected=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    rig["humanoidContract"] = "FC-FAMILY-SHARED-HUMANOID-V1"
    rig["rootConvention"] = "BOTTOM_CENTER"
    return rig


RIG = build_armature()

# Consolidate every visible character surface into one skinned mesh object.
bpy.ops.object.select_all(action="DESELECT")
for part in CHARACTER_PARTS:
    part.select_set(True)
bpy.context.view_layer.objects.active = HEAD
bpy.ops.object.join()
BODY = bpy.context.object
BODY.name = "PlayerV3_CompleteSkinnedBody"
for polygon in BODY.data.polygons:
    polygon.material_index = 0
while len(BODY.data.materials) > 1:
    BODY.data.materials.pop(index=1)
BODY.parent = RIG
armature_modifier = BODY.modifiers.new(name="PlayerV3_HumanoidSkin", type="ARMATURE")
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


floor_material = simple_material("M_ReviewFloor", (0.145, 0.155, 0.175), 0.86)
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
    obj.rotation_euler = (Vector((0.0, 0.0, 1.60)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


add_area("KeySoftbox", (-4.0, -5.5, 6.2), 1020.0, (1.0, 0.84, 0.70), 4.5)
add_area("FillSoftbox", (4.5, -3.5, 4.3), 720.0, (0.70, 0.83, 1.0), 4.0)
add_area("RimSoftbox", (0.0, 4.8, 5.5), 920.0, (1.0, 0.78, 0.64), 3.5)
add_area("TopSoftbox", (0.0, 0.0, 7.0), 440.0, (1.0, 0.95, 0.88), 3.0)

camera_data = bpy.data.cameras.new("IdentityReviewCamera")
CAMERA = bpy.data.objects.new("IdentityReviewCamera", camera_data)
bpy.context.collection.objects.link(CAMERA)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 3.62
camera_data.lens = 58.0
scene.camera = CAMERA


def point_camera(location, target=(0.0, 0.0, 1.65)):
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
world_background.inputs["Color"].default_value = (0.060, 0.068, 0.082, 1.0)
world_background.inputs["Strength"].default_value = 0.34
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
    # Opposed arms and legs; moderate angles expose knee/ankle weighting without
    # turning the deformation audit into an exaggerated action pose.
    RIG.pose.bones["LeftUpperLeg"].rotation_euler.x = math.radians(22.0 * lead)
    RIG.pose.bones["RightUpperLeg"].rotation_euler.x = math.radians(-22.0 * lead)
    RIG.pose.bones["LeftLowerLeg"].rotation_euler.x = math.radians(-8.0 if left_forward else 28.0)
    RIG.pose.bones["RightLowerLeg"].rotation_euler.x = math.radians(28.0 if left_forward else -8.0)
    RIG.pose.bones["LeftFoot"].rotation_euler.x = math.radians(-10.0 * lead)
    RIG.pose.bones["RightFoot"].rotation_euler.x = math.radians(10.0 * lead)
    RIG.pose.bones["LeftUpperArm"].rotation_euler.x = math.radians(-18.0 * lead)
    RIG.pose.bones["RightUpperArm"].rotation_euler.x = math.radians(18.0 * lead)
    RIG.pose.bones["LeftLowerArm"].rotation_euler.x = math.radians(-4.0 * lead)
    RIG.pose.bones["RightLowerArm"].rotation_euler.x = math.radians(4.0 * lead)
    bpy.context.view_layer.update()


STATIC_VIEWS = {
    "front": (0.0, -7.4, 1.67),
    "left": (7.4, 0.0, 1.67),
    "back": (0.0, 7.4, 1.67),
    "three-quarter": (5.25, -5.55, 1.78),
}
STATIC_RENDER_PATHS = []
clear_pose()
for view_name, camera_location in STATIC_VIEWS.items():
    point_camera(camera_location)
    path = os.path.join(OUTPUT, "player-v6-blender-%s-v3-%s.png" % (view_name, QUALITY))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    STATIC_RENDER_PATHS.append(path)

DEFORMATION_RENDER_PATHS = []
TURNTABLE_RENDER_PATHS = []
if QUALITY == "final":
    camera_data.ortho_scale = 3.92
    point_camera((5.25, -5.55, 1.78))
    for pose_name, left_forward in (("p0-left-contact", True), ("p3-right-contact", False)):
        apply_walk_contact(left_forward)
        path = os.path.join(OUTPUT, "player-v6-blender-deform-%s-v3.png" % pose_name)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        DEFORMATION_RENDER_PATHS.append(path)
    clear_pose()

    turntable_directory = os.path.join(OUTPUT, "turntable")
    os.makedirs(turntable_directory, exist_ok=True)
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    camera_data.ortho_scale = 3.75
    for index in range(24):
        angle = 2.0 * math.pi * index / 24.0
        radius = 7.5
        point_camera((math.sin(angle) * radius, -math.cos(angle) * radius, 1.72))
        path = os.path.join(turntable_directory, "player-v6-turntable-%02d.png" % index)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        TURNTABLE_RENDER_PATHS.append(path)

clear_pose()
scene.render.resolution_x = 1024 if QUALITY == "final" else 640
scene.render.resolution_y = 1440 if QUALITY == "final" else 900
camera_data.ortho_scale = 3.62

# Export only the complete body and Humanoid rig.  Review geometry/lights are
# intentionally excluded and no production/default Unity asset is overwritten.
bpy.ops.object.select_all(action="DESELECT")
BODY.select_set(True)
RIG.select_set(True)
bpy.context.view_layer.objects.active = RIG
fbx_path = os.path.join(OUTPUT, "player-v6-blender-humanoid-v3.fbx")
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

blend_path = os.path.join(OUTPUT, "player-v6-blender-identity-v3.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

world_bounds = [BODY.matrix_world @ Vector(corner) for corner in BODY.bound_box]
bounds_min = [min(point[index] for point in world_bounds) for index in range(3)]
bounds_max = [max(point[index] for point in world_bounds) for index in range(3)]
receipt = {
    "contract": "FC-PLAYER-V6-BLENDER-IDENTITY-V3",
    "status": "CANDIDATE_VISUAL_REVIEW_REQUIRED_DO_NOT_PROMOTE",
    "quality": QUALITY,
    "sourcePolicy": {
        "newTopology": True,
        "rejectedV1V2MeshImported": False,
        "legacy2DAssetUsed": False,
        "turnaroundUsedAsDecalOrTexture": False,
    },
    "reference": {
        "path": REFERENCE,
        "sha256": sha256(REFERENCE),
    },
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

print("PLAYER_V3_BUILD: PASS")
print(json.dumps(receipt, ensure_ascii=False, indent=2))
