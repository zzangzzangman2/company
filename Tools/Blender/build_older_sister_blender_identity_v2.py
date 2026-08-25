"""Build the isolated Older Sister V2 Blender identity candidate.

This is a fresh procedural model authored from measurements taken from the
approved runtime HighMotion sprites and neutral identity art.  Those images are
hash-locked visual references only: no image pixels, meshes, UVs, decals,
billboards, traces, or motion are imported.  The rejected V1 character and the
retired turnaround have no authority and are never opened by this builder.

Final4 is a clean-room polish of this script's own Final3 topology language.
Mika/Yuuka measurements inform only abstract proportion sanity checks; no
external mesh, topology, UV, texture, material, rig, weight, or silhouette part
is imported, sampled, or copied.
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
    parser.add_argument("--runtime-frame-dir", required=True)
    parser.add_argument("--runtime-sheet-a", required=True)
    parser.add_argument("--runtime-sheet-b", required=True)
    parser.add_argument("--neutral-reference", required=True)
    parser.add_argument("--quality", choices=("draft", "final"), default="draft")
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = os.path.abspath(ARGS.output)
FRAME_DIR = os.path.abspath(ARGS.runtime_frame_dir)
SHEET_A = os.path.abspath(ARGS.runtime_sheet_a)
SHEET_B = os.path.abspath(ARGS.runtime_sheet_b)
NEUTRAL_REFERENCE = os.path.abspath(ARGS.neutral_reference)
QUALITY = ARGS.quality
ITERATION = "Final4"
os.makedirs(OUTPUT, exist_ok=True)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def runtime_frame_manifest():
    directions = (
        "south", "southwest", "west", "northwest",
        "north", "northeast", "east", "southeast",
    )
    records = []
    for direction in directions:
        for frame in range(6):
            path = os.path.join(FRAME_DIR, "older_sister_%s_walk_%d.png" % (direction, frame))
            if not os.path.isfile(path):
                raise RuntimeError("Missing runtime identity frame: %s" % path)
            records.append({"path": path, "sha256": sha256(path)})
    joined = "".join("%d|%s|%s" % (i, r["path"], r["sha256"]) for i, r in enumerate(records))
    return records, hashlib.sha256(joined.encode("utf-8")).hexdigest().upper()


for required_path in (SHEET_A, SHEET_B, NEUTRAL_REFERENCE):
    if not os.path.isfile(required_path):
        raise RuntimeError("Missing visual identity reference: %s" % required_path)
RUNTIME_FRAMES, RUNTIME_FRAME_MANIFEST_SHA = runtime_frame_manifest()


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


# Flat, clean runtime-derived palette.  No procedural grain or PBR microdetail.
PALETTE = {
    "skin": (0.940, 0.650, 0.560),
    "skin_shadow": (0.720, 0.335, 0.275),
    "hair": (0.048, 0.034, 0.052),
    "hair_mid": (0.105, 0.070, 0.105),
    "hair_highlight": (0.205, 0.132, 0.180),
    "bow": (0.060, 0.033, 0.058),
    "top": (0.050, 0.042, 0.058),
    "top_edge": (0.125, 0.100, 0.128),
    "navy": (0.030, 0.052, 0.120),
    "navy_light": (0.065, 0.085, 0.160),
    "piping": (0.915, 0.925, 0.905),
    "eye_white": (0.965, 0.955, 0.920),
    "iris": (0.035, 0.480, 0.455),
    "iris_light": (0.115, 0.825, 0.765),
    "pupil": (0.002, 0.010, 0.014),
    "mouth": (0.345, 0.028, 0.040),
}
ATLAS_KEYS = tuple(PALETTE.keys())
ATLAS_COLS = 4
ATLAS_ROWS = 4
ATLAS_SIZE = 512 if QUALITY == "final" else 256
ATLAS_PATH = os.path.join(OUTPUT, "older-sister-blender-identity-v2-atlas.png")


def make_atlas():
    image = bpy.data.images.new(
        "OlderSisterV2IdentityAtlas",
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
            color = PALETTE[key]
            offset = (y * ATLAS_SIZE + x) * 4
            pixels[offset + 0] = color[0]
            pixels[offset + 1] = color[1]
            pixels[offset + 2] = color[2]
            pixels[offset + 3] = 1.0
    image.pixels.foreach_set(pixels)
    image.filepath_raw = ATLAS_PATH
    image.file_format = "PNG"
    image.save()
    return image


ATLAS_IMAGE = make_atlas()


def build_character_material():
    material = bpy.data.materials.new("M_OlderSisterV2_CleanToonAtlas")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    shader_to_rgb = nodes.new("ShaderNodeShaderToRGB")
    ramp = nodes.new("ShaderNodeValToRGB")
    multiply = nodes.new("ShaderNodeMixRGB")
    emission = nodes.new("ShaderNodeEmission")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = ATLAS_IMAGE
    texture.interpolation = "Closest"
    texture.extension = "EXTEND"
    bsdf.inputs["Base Color"].default_value = (0.82, 0.82, 0.82, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.94
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.045
    ramp.color_ramp.interpolation = "EASE"
    ramp.color_ramp.elements[0].position = 0.24
    ramp.color_ramp.elements[0].color = (0.68, 0.68, 0.72, 1.0)
    ramp.color_ramp.elements[1].position = 0.72
    ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    emission.inputs["Strength"].default_value = 0.86
    links.new(bsdf.outputs["BSDF"], shader_to_rgb.inputs["Shader"])
    links.new(shader_to_rgb.outputs["Color"], ramp.inputs["Fac"])
    links.new(texture.outputs["Color"], multiply.inputs[1])
    links.new(ramp.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


CHARACTER_MATERIAL = build_character_material()
CHARACTER_PARTS = []


def assign_solid_tile(obj, key):
    layer = obj.data.uv_layers.new(name="OlderSisterV2AtlasUV")
    index = ATLAS_KEYS.index(key)
    col = index % ATLAS_COLS
    row = index // ATLAS_COLS
    uv = ((col + 0.5) / ATLAS_COLS, (row + 0.5) / ATLAS_ROWS)
    for loop in obj.data.loops:
        layer.data[loop.index].uv = uv


def assign_polygon_tiles(obj, keys):
    """Assign one solid atlas tile per polygon without adding materials."""
    if len(keys) != len(obj.data.polygons):
        raise RuntimeError("Polygon tile count mismatch for %s" % obj.name)
    layer = obj.data.uv_layers.get("OlderSisterV2AtlasUV")
    if layer is None:
        raise RuntimeError("Missing atlas UV layer on %s" % obj.name)
    for polygon, key in zip(obj.data.polygons, keys):
        index = ATLAS_KEYS.index(key)
        col = index % ATLAS_COLS
        row = index // ATLAS_COLS
        uv = ((col + 0.5) / ATLAS_COLS, (row + 0.5) / ATLAS_ROWS)
        for loop_index in polygon.loop_indices:
            layer.data[loop_index].uv = uv


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
    assign_solid_tile(obj, palette_key)
    assign_weights(obj, weights)
    obj["identityPart"] = name
    obj["paletteKey"] = palette_key
    CHARACTER_PARTS.append(obj)
    return obj


def ellipsoid(name, center, radii, palette_key, weights, segments=28, rings=18, deform=None, smooth=True):
    center = Vector(center)
    vertices = []
    for ring in range(rings + 1):
        theta = math.pi * ring / rings
        z_norm = math.cos(theta)
        radial = math.sin(theta)
        for segment in range(segments):
            phi = 2.0 * math.pi * segment / segments
            local = Vector((
                radii[0] * radial * math.cos(phi),
                radii[1] * radial * math.sin(phi),
                radii[2] * z_norm,
            ))
            if deform:
                local = deform(local, Vector((radial * math.cos(phi), radial * math.sin(phi), z_norm)))
            vertices.append(tuple(center + local))
    faces = []
    for ring in range(rings):
        for segment in range(segments):
            nxt = (segment + 1) % segments
            a = ring * segments + segment
            b = ring * segments + nxt
            c = (ring + 1) * segments + nxt
            d = (ring + 1) * segments + segment
            faces.append((a, b, c, d))
    return create_mesh(name, vertices, faces, palette_key, weights, smooth)


def loft(name, rings, palette_key, ring_weights, segments=28, exponent=2.5, cap=True, smooth=True):
    """Create a vertical superellipse loft; each ring is (z, cx, cy, rx, ry)."""
    vertices = []
    weights = []
    for ring, mapping in zip(rings, ring_weights):
        z, cx, cy, rx, ry = ring
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            ca = math.cos(angle)
            sa = math.sin(angle)
            px = math.copysign(abs(ca) ** (2.0 / exponent), ca)
            py = math.copysign(abs(sa) ** (2.0 / exponent), sa)
            vertices.append((cx + rx * px, cy + ry * py, z))
            weights.append(mapping)
    faces = []
    for ring_index in range(len(rings) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, start + nxt, next_start + nxt, next_start + segment))
    if cap:
        bottom_index = len(vertices)
        bottom = rings[0]
        vertices.append((bottom[1], bottom[2], bottom[0]))
        weights.append(ring_weights[0])
        top_index = len(vertices)
        top = rings[-1]
        vertices.append((top[1], top[2], top[0]))
        weights.append(ring_weights[-1])
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((bottom_index, nxt, segment))
            last = (len(rings) - 1) * segments
            faces.append((top_index, last + segment, last + nxt))
    return create_mesh(name, vertices, faces, palette_key, weights, smooth)


def path_frame(tangent):
    tangent = tangent.normalized()
    reference = Vector((0.0, 1.0, 0.0))
    if abs(tangent.dot(reference)) > 0.88:
        reference = Vector((0.0, 0.0, 1.0))
    normal_a = tangent.cross(reference).normalized()
    normal_b = tangent.cross(normal_a).normalized()
    return normal_a, normal_b


def tube(name, points, radii, palette_key, ring_weights, segments=18, cap=True, smooth=True):
    points = [Vector(point) for point in points]
    if len(radii) != len(points) or len(ring_weights) != len(points):
        raise RuntimeError("Tube argument mismatch: %s" % name)
    vertices = []
    weights = []
    for index, point in enumerate(points):
        if index == 0:
            tangent = points[1] - point
        elif index == len(points) - 1:
            tangent = point - points[index - 1]
        else:
            tangent = points[index + 1] - points[index - 1]
        axis_a, axis_b = path_frame(tangent)
        radius_a, radius_b = radii[index] if isinstance(radii[index], tuple) else (radii[index], radii[index])
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertex = point + axis_a * (math.cos(angle) * radius_a) + axis_b * (math.sin(angle) * radius_b)
            vertices.append(tuple(vertex))
            weights.append(ring_weights[index])
    faces = []
    for ring_index in range(len(points) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, start + nxt, next_start + nxt, next_start + segment))
    if cap:
        first_center = len(vertices)
        vertices.append(tuple(points[0]))
        weights.append(ring_weights[0])
        last_center = len(vertices)
        vertices.append(tuple(points[-1]))
        weights.append(ring_weights[-1])
        last_start = (len(points) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first_center, nxt, segment))
            faces.append((last_center, last_start + segment, last_start + nxt))
    return create_mesh(name, vertices, faces, palette_key, weights, smooth)


def leaf(name, knot, direction, length, width, depth, palette_key="bow", bone="Head"):
    knot = Vector(knot)
    direction = Vector(direction).normalized()
    sideways = Vector((-direction.z, 0.0, direction.x)).normalized()
    centers = (knot, knot + direction * length * 0.52, knot + direction * length)
    widths = (width * 0.12, width, width * 0.10)
    vertices = []
    for y_offset in (-depth, depth):
        for center, half_width in zip(centers, widths):
            vertices.append(tuple(center + sideways * half_width + Vector((0.0, y_offset, 0.0))))
            vertices.append(tuple(center - sideways * half_width + Vector((0.0, y_offset, 0.0))))
    faces = []
    # Front and back strips.
    for layer in (0, 6):
        faces.extend(((layer + 0, layer + 1, layer + 3, layer + 2),
                      (layer + 2, layer + 3, layer + 5, layer + 4)))
    # Close the boundary.
    boundary = (0, 2, 4, 5, 3, 1)
    for i in range(len(boundary)):
        a = boundary[i]
        b = boundary[(i + 1) % len(boundary)]
        faces.append((a, b, b + 6, a + 6))
    return create_mesh(name, vertices, faces, palette_key, bone, smooth=True)


def face_ribbon(name, points, widths, palette_key, bone="Head", y=-0.348):
    vertices = []
    for (x, z), width in zip(points, widths):
        vertices.extend(((x - width, y, z), (x + width, y, z)))
    faces = []
    for index in range(len(points) - 1):
        a = index * 2
        faces.append((a, a + 1, a + 3, a + 2))
    return create_mesh(name, vertices, faces, palette_key, bone, smooth=False)


def asymmetric_head(name, rings, palette_key, bone="Head", segments=36, y_center=0.0):
    """Wide anime cheek silhouette with independent front/back depth per ring."""
    vertices = []
    weights = []
    for z, radius_x, front_depth, back_depth in rings:
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            cosine = math.cos(angle)
            sine = math.sin(angle)
            depth = front_depth if sine < 0.0 else back_depth
            vertices.append((radius_x * cosine, y_center + depth * sine, z))
            weights.append({bone: 1.0})
    faces = []
    for ring_index in range(len(rings) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, start + nxt, next_start + nxt, next_start + segment))
    bottom_center = len(vertices)
    vertices.append((0.0, y_center, rings[0][0]))
    weights.append({bone: 1.0})
    top_center = len(vertices)
    vertices.append((0.0, y_center, rings[-1][0]))
    weights.append({bone: 1.0})
    last_start = (len(rings) - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom_center, segment, nxt))
        faces.append((top_center, last_start + nxt, last_start + segment))
    return create_mesh(name, vertices, faces, palette_key, weights, smooth=True)


def bare_foot_shell(name, x_center, side):
    """Compact complete wedge-like barefoot shell without carved toe grooves."""
    sections = (
        (0.018, 0.066, 0.205, 0.061),
        (-0.060, 0.074, 0.157, 0.064),
        (-0.155, 0.086, 0.116, 0.057),
        (-0.250, 0.096, 0.091, 0.048),
        (-0.292, 0.078, 0.083, 0.039),
    )
    segments = 20
    vertices = []
    weights = []
    for index, (y, half_width, z_center, z_radius) in enumerate(sections):
        mapping = ({side + "LowerLeg": 0.30, side + "Foot": 0.70}
                   if index == 0 else {side + "Foot": 1.0})
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertices.append((x_center + half_width * math.cos(angle), y,
                             z_center + z_radius * math.sin(angle)))
            weights.append(mapping)
    faces = []
    for section in range(len(sections) - 1):
        a0 = section * segments
        b0 = (section + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((a0 + segment, a0 + nxt, b0 + nxt, b0 + segment))
    for end, ring_start, mapping, reverse in (
        ((x_center, sections[0][0], sections[0][2]), 0, weights[0], True),
        ((x_center, sections[-1][0], sections[-1][2]), (len(sections) - 1) * segments,
         {side + "Foot": 0.78, side + "Toes": 0.22}, False),
    ):
        center_index = len(vertices)
        vertices.append(end)
        weights.append(mapping)
        for segment in range(segments):
            nxt = (segment + 1) % segments
            face = (center_index, ring_start + nxt, ring_start + segment)
            faces.append(face if reverse else tuple(reversed(face)))
    return create_mesh(name, vertices, faces, "skin", weights, smooth=True)


def cubic_point(p0, p1, p2, p3, t):
    omt = 1.0 - t
    return (p0 * (omt ** 3) + p1 * (3.0 * omt * omt * t) +
            p2 * (3.0 * omt * t * t) + p3 * (t ** 3))


def curved_hair_clump(name, controls, root_width, bulge_width, palette_key, bone="Head", samples=10):
    controls = [Vector(point) for point in controls]
    points = []
    radii = []
    for index in range(samples):
        t = index / float(samples - 1)
        points.append(tuple(cubic_point(controls[0], controls[1], controls[2], controls[3], t)))
        width = max(0.004, (1.0 - t) * root_width + math.sin(math.pi * t) * bulge_width + t * 0.004)
        radii.append((width, max(0.0035, width * 0.62)))
    return tube(name, points, tuple(radii), palette_key, ({bone: 1.0},) * samples, 20)


def fabric_loop(name, knot, direction, length, width, palette_key="bow", bone="Head"):
    """Rounded closed cloth loop tangent to a bow knot."""
    knot = Vector(knot)
    direction = Vector(direction).normalized()
    perpendicular = Vector((-direction.z, 0.0, direction.x)).normalized()
    center = knot + direction * (length * 0.52)
    points = []
    steps = 18
    for index in range(steps + 1):
        angle = math.pi + 2.0 * math.pi * index / steps
        point = center + direction * (math.cos(angle) * length * 0.52) + perpendicular * (math.sin(angle) * width)
        # A shallow cloth-plane fold keeps the loop readable from side/rear
        # instead of collapsing to an antenna-like line outside the front view.
        point += Vector((0.0, math.sin(angle) * width * 0.65, 0.0))
        points.append(tuple(point))
    return tube(name, points, ((0.021, 0.015),) * len(points), palette_key,
                ({bone: 1.0},) * len(points), 12, cap=False)


def dolphin_shorts_surface():
    """One continuous shorts surface with atlas-white trim embedded in its faces.

    Final2 used front-offset tubes for the white trim.  Those tubes detached in
    strict side and rear views.  Final3 keeps the original shorts silhouette but
    assigns piping atlas tiles directly to surface faces, so there is no
    independent wire geometry that can float, vanish, or separate in 360 review.
    """
    profiles = (
        (1.340, 0.004, 0.340, 0.182),
        (1.480, 0.005, 0.355, 0.188),
        (1.660, 0.000, 0.315, 0.168),
    )

    def profile_at(z):
        for index in range(len(profiles) - 1):
            lower = profiles[index]
            upper = profiles[index + 1]
            if z <= upper[0] + 1.0e-8:
                t = (z - lower[0]) / (upper[0] - lower[0])
                return tuple(lower[item] * (1.0 - t) + upper[item] * t for item in range(1, 4))
        return profiles[-1][1:]

    segments = 64
    exponent = 2.7
    hem_centers = []
    for segment in range(segments):
        angle = 2.0 * math.pi * segment / segments
        x_normalized = abs(math.cos(angle)) ** (2.0 / exponent)
        frontness = max(0.0, -math.sin(angle)) ** 1.7
        if x_normalized <= 0.50:
            front_hem = 1.380 - 0.050 * x_normalized
        else:
            front_hem = 1.355 + 0.080 * (x_normalized - 0.50)
        hem_centers.append(1.360 * (1.0 - frontness) + front_hem * frontness)

    # Every layer shares the same angular vertices.  The two hem layers follow
    # a smooth dolphin curve but remain topologically connected around all 360
    # degrees; the two waist layers are level and equally continuous.
    layer_count = 6
    vertices = []
    weights = []
    for layer_index in range(layer_count):
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            if layer_index == 0:
                z = 1.340
            elif layer_index == 1:
                z = hem_centers[segment] - 0.012
            elif layer_index == 2:
                z = hem_centers[segment] + 0.012
            elif layer_index == 3:
                z = 1.480
            elif layer_index == 4:
                z = 1.635
            else:
                z = 1.660
            cy, rx, ry = profile_at(z)
            ca = math.cos(angle)
            sa = math.sin(angle)
            px = math.copysign(abs(ca) ** (2.0 / exponent), ca)
            py = math.copysign(abs(sa) ** (2.0 / exponent), sa)
            vertices.append((rx * px, cy + ry * py, z))
            weights.append({"Hips": 1.0})

    faces = []
    palette_keys = []
    layer_palette = ("navy", "piping", "navy", "navy", "piping")
    for layer_index in range(layer_count - 1):
        start = layer_index * segments
        next_start = (layer_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, start + nxt, next_start + nxt, next_start + segment))
            palette_keys.append(layer_palette[layer_index])

    bottom_center = len(vertices)
    vertices.append((0.0, profiles[0][1], profiles[0][0]))
    weights.append({"Hips": 1.0})
    top_center = len(vertices)
    vertices.append((0.0, profiles[-1][1], profiles[-1][0]))
    weights.append({"Hips": 1.0})
    top_start = (layer_count - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom_center, nxt, segment))
        palette_keys.append("navy")
        faces.append((top_center, top_start + segment, top_start + nxt))
        palette_keys.append("piping")

    obj = create_mesh("DolphinShortsSurfacePiped", vertices, faces, "navy", weights, smooth=True)
    assign_polygon_tiles(obj, palette_keys)
    obj["pipingStyle"] = "atlas-face-filled-flush-surface-band"
    obj["independentPipingGeometryCount"] = 0
    return obj


# --- Fresh V2 body topology -------------------------------------------------

# Seamless hip-to-ankle taper and compact complete barefoot silhouettes.
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    x = 0.170 * sign
    prefix = side
    tube(
        "ContinuousLeg." + side,
        ((x, 0.010, 1.47), (x + 0.004 * sign, 0.007, 1.30),
         (x + 0.002 * sign, 0.003, 1.06), (x, 0.000, 0.86),
         (x - 0.004 * sign, 0.005, 0.70), (x - 0.006 * sign, 0.012, 0.48),
         (x - 0.006 * sign, 0.018, 0.245)),
        ((0.150, 0.139), (0.142, 0.132), (0.122, 0.114), (0.098, 0.093),
         (0.110, 0.103), (0.084, 0.079), (0.060, 0.057)),
        "skin",
        ({prefix + "UpperLeg": 1.0}, {prefix + "UpperLeg": 1.0},
         {prefix + "UpperLeg": 0.78, prefix + "LowerLeg": 0.22},
         {prefix + "UpperLeg": 0.34, prefix + "LowerLeg": 0.66},
         {prefix + "LowerLeg": 1.0}, {prefix + "LowerLeg": 1.0},
         {prefix + "LowerLeg": 0.70, prefix + "Foot": 0.30}),
        24,
    )
    bare_foot_shell("BareFoot." + side, x, side)


# The upper chest rises through a rounded shoulder dome into the neck instead of
# ending in a flat, skin-coloured shelf.  The arm roots deliberately overlap the
# wide middle rings so the shoulder-to-arm silhouette stays continuous.
loft(
    "UpperChestSkin",
    ((2.060, 0.0, 0.000, 0.315, 0.174), (2.095, 0.0, 0.001, 0.328, 0.178),
     (2.125, 0.0, 0.002, 0.335, 0.179), (2.150, 0.0, 0.003, 0.326, 0.176),
     (2.175, 0.0, 0.004, 0.292, 0.166), (2.200, 0.0, 0.006, 0.235, 0.145),
     (2.222, 0.0, 0.007, 0.165, 0.112), (2.242, 0.0, 0.008, 0.085, 0.073)),
    "skin",
    ({"Chest": 0.45, "UpperChest": 0.55}, {"UpperChest": 1.0}, {"UpperChest": 1.0},
     {"UpperChest": 1.0}, {"UpperChest": 1.0}, {"UpperChest": 0.90, "Neck": 0.10},
     {"UpperChest": 0.76, "Neck": 0.24}, {"UpperChest": 0.62, "Neck": 0.38}),
    32,
    2.35,
)
loft(
    "FittedSleevelessTank",
    ((1.52, 0.0, -0.004, 0.320, 0.182), (1.75, 0.0, -0.003, 0.285, 0.174),
     (1.98, 0.0, -0.002, 0.328, 0.188), (2.105, 0.0, 0.000, 0.340, 0.190)),
    "top",
    ({"Hips": 0.70, "Spine": 0.30}, {"Spine": 0.72, "Chest": 0.28},
     {"Chest": 0.68, "UpperChest": 0.32}, {"UpperChest": 1.0}),
    36,
    2.6,
)
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    face_ribbon("TankStrapFront." + side,
                ((0.245 * sign, 2.080), (0.265 * sign, 2.125), (0.255 * sign, 2.165)),
                (0.044, 0.043, 0.038), "top", "UpperChest", y=-0.168)
    face_ribbon("TankStrapBack." + side,
                ((0.245 * sign, 2.080), (0.265 * sign, 2.125), (0.255 * sign, 2.165)),
                (0.044, 0.043, 0.038), "top", "UpperChest", y=0.158)
face_ribbon("ScoopNeckEdge", ((-0.205, 2.105), (-0.135, 2.085), (0.0, 2.075),
                                         (0.135, 2.085), (0.205, 2.105)),
            (0.008, 0.008, 0.008, 0.008, 0.008), "top_edge", "UpperChest", y=-0.178)

dolphin_shorts_surface()
# Small skin-colored front notch separates the two short legs.
face_ribbon("ShortsCrotchNotch", ((0.0, 1.397), (0.0, 1.430)), (0.012, 0.004), "skin", "Hips", y=-0.194)


# Rounded low A-pose arms use an uninterrupted eased shoulder-to-wrist taper.
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    shoulder = Vector((0.318 * sign, -0.002, 2.152))
    elbow = Vector((0.418 * sign, -0.014, 1.802))
    wrist = Vector((0.438 * sign, -0.026, 1.520))
    tube(
        "ContinuousArm." + side,
        ((0.300 * sign, -0.001, 2.145), (0.333 * sign, -0.004, 2.105),
         (0.358 * sign, -0.007, 2.045), (0.386 * sign, -0.010, 1.935),
         elbow, (0.429 * sign, -0.018, 1.700), (0.435 * sign, -0.022, 1.600), wrist),
        ((0.102, 0.096), (0.098, 0.092), (0.090, 0.084), (0.079, 0.074),
         (0.068, 0.064), (0.058, 0.054), (0.050, 0.047), (0.042, 0.040)),
        "skin",
        ({side + "UpperArm": 1.0}, {side + "UpperArm": 1.0}, {side + "UpperArm": 1.0},
         {side + "UpperArm": 0.82, side + "LowerArm": 0.18},
         {side + "UpperArm": 0.55, side + "LowerArm": 0.45},
         {side + "UpperArm": 0.34, side + "LowerArm": 0.66}, {side + "LowerArm": 1.0},
         {side + "LowerArm": 0.55, side + "Hand": 0.45}),
        24,
        cap=False,
    )
    palm_center = Vector((0.442 * sign, -0.034, 1.407))
    tube(
        "MittenHand." + side,
        ((wrist.x, wrist.y, wrist.z), (0.441 * sign, -0.032, 1.455),
         (0.444 * sign, -0.038, 1.390), (0.442 * sign, -0.043, 1.335)),
        ((0.039, 0.037), (0.044, 0.039), (0.046, 0.038), (0.030, 0.025)),
        "skin",
        ({side + "LowerArm": 0.30, side + "Hand": 0.70}, {side + "Hand": 1.0},
         {side + "Hand": 1.0}, {side + "Hand": 1.0}),
        18,
    )
    tube(
        "Thumb." + side,
        ((palm_center.x + 0.034 * sign, -0.047, 1.414),
         (palm_center.x + 0.050 * sign, -0.051, 1.382)),
        ((0.011, 0.010), (0.005, 0.005)),
        "skin",
        ({side + "Hand": 1.0}, {side + "Hand": 1.0}),
        12,
    )


# Most of this wider neck is overlapped by the upper chest and lowered head;
# only about 0.06 units remain visible in the final silhouette.
loft("Neck", ((2.180, 0.0, 0.010, 0.066, 0.060), (2.252, 0.0, 0.010, 0.069, 0.062),
              (2.324, 0.0, 0.006, 0.071, 0.064)), "skin",
     ({"UpperChest": 0.72, "Neck": 0.28}, {"Neck": 1.0}, {"Neck": 0.72, "Head": 0.28}), 24, 2.4)
HAIR_BACK = asymmetric_head(
    "HairBackCap",
    ((2.390, 0.070, 0.020, 0.045), (2.420, 0.165, 0.035, 0.105),
     (2.470, 0.275, 0.045, 0.180), (2.560, 0.345, 0.050, 0.230),
     (2.680, 0.395, 0.060, 0.265), (2.820, 0.415, 0.085, 0.280),
     (2.950, 0.415, 0.125, 0.275), (3.055, 0.385, 0.145, 0.250),
     (3.140, 0.300, 0.120, 0.195), (3.195, 0.190, 0.075, 0.120),
     (3.220, 0.055, 0.025, 0.035)),
    "hair", "Head", 40, y_center=0.040,
)
FACE = asymmetric_head(
    "AdultAnimeFace",
    ((2.330, 0.035, 0.020, 0.017), (2.348, 0.085, 0.045, 0.029),
     (2.372, 0.145, 0.080, 0.046), (2.405, 0.205, 0.115, 0.065),
     (2.445, 0.260, 0.150, 0.086), (2.490, 0.305, 0.178, 0.106),
     (2.535, 0.335, 0.196, 0.118), (2.585, 0.355, 0.207, 0.126),
     (2.680, 0.372, 0.220, 0.138),
     (2.780, 0.378, 0.225, 0.142), (2.880, 0.368, 0.210, 0.140),
     (2.970, 0.340, 0.180, 0.128), (3.045, 0.292, 0.140, 0.105),
     (3.100, 0.215, 0.090, 0.070), (3.135, 0.060, 0.025, 0.025)),
    "skin", "Head", 40, y_center=-0.035,
)

# Ears remain small and are partly covered by framing locks.
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    ellipsoid("Ear." + side, (0.360 * sign, -0.005, 2.725), (0.035, 0.022, 0.065),
              "skin", "Head", 18, 12)

# Layered pointed bang clumps and slim side locks.
face_ribbon("FringeBase", ((0.0, 3.120), (0.0, 3.015), (0.0, 2.925)),
            (0.305, 0.330, 0.245), "hair_mid", "Head", y=-0.242)
leaf("Bang.Center", (0.0, -0.248, 3.145), (-0.05, 0.0, -1.0), 0.385, 0.095, 0.014, "hair_mid")
leaf("Bang.InnerLeft", (-0.090, -0.246, 3.150), (-0.22, 0.0, -0.98), 0.355, 0.090, 0.014, "hair")
leaf("Bang.InnerRight", (0.090, -0.246, 3.150), (0.22, 0.0, -0.98), 0.355, 0.090, 0.014, "hair")
leaf("Bang.OuterLeft", (-0.175, -0.238, 3.125), (-0.40, 0.0, -0.92), 0.330, 0.080, 0.014, "hair_mid")
leaf("Bang.OuterRight", (0.175, -0.238, 3.125), (0.40, 0.0, -0.92), 0.330, 0.080, 0.014, "hair_mid")
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    leaf("TempleFringe." + side, (0.235 * sign, -0.228, 3.105),
         (0.34 * sign, 0.0, -0.94), 0.265, 0.070, 0.012, "hair_mid")
    tube("FaceLock." + side,
         ((0.300 * sign, -0.140, 3.020), (0.330 * sign, -0.165, 2.835),
          (0.335 * sign, -0.170, 2.665), (0.295 * sign, -0.140, 2.515)),
         ((0.032, 0.025), (0.037, 0.027), (0.030, 0.023), (0.008, 0.007)),
         "hair", ({"Head": 1.0},) * 4, 16)
face_ribbon("BangHighlight", ((-0.060, 3.095), (-0.095, 2.940), (-0.115, 2.835)),
            (0.014, 0.012, 0.005), "hair_highlight", y=-0.269)

# Large teal anime eyes: iris occupies almost all eye height and sclera is only
# a thin perimeter, preventing the V1/Player-style bug-eye read.
EYE_CENTER_X = 0.188
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    x = EYE_CENTER_X * sign
    ellipsoid("EyeWhite." + side, (x, -0.258, 2.735), (0.108, 0.006, 0.070),
              "eye_white", "Head", 28, 16)
    ellipsoid("Iris." + side, (x, -0.266, 2.731), (0.078, 0.005, 0.068),
              "iris", "Head", 24, 14)
    ellipsoid("IrisLowerGlow." + side, (x, -0.272, 2.706), (0.044, 0.004, 0.021),
              "iris_light", "Head", 20, 10)
    ellipsoid("Pupil." + side, (x, -0.276, 2.730), (0.024, 0.004, 0.045),
              "pupil", "Head", 20, 12)
    ellipsoid("EyeGlint." + side, (x - 0.020 * sign, -0.281, 2.758), (0.016, 0.004, 0.018),
              "eye_white", "Head", 16, 10)
    face_ribbon("UpperLash." + side, ((x - 0.098, 2.779), (x, 2.790), (x + 0.098, 2.779)),
                (0.006, 0.007, 0.006), "pupil", "Head", y=-0.283)
    outer_x = x + 0.105 * sign
    face_ribbon("OuterLashWing." + side, ((outer_x, 2.780), (outer_x + 0.030 * sign, 2.792)),
                (0.006, 0.002), "pupil", "Head", y=-0.284)
    face_ribbon("Brow." + side, ((x - 0.062, 2.844), (x, 2.847), (x + 0.060, 2.844)),
                (0.0035, 0.004, 0.0035), "hair_mid", "Head", y=-0.279)

face_ribbon("NoseMark", ((0.004, 2.645), (-0.004, 2.637)), (0.004, 0.003),
            "skin_shadow", "Head", y=-0.244)
face_ribbon("SmallMouth", ((-0.038, 2.563), (0.0, 2.555), (0.038, 2.563)),
            (0.004, 0.005, 0.004), "mouth", "Head", y=-0.237)

# Each high twin tail is a fan of smooth cubic clumps with near-zero-width tips.
for sign, side in ((1.0, "Left"), (-1.0, "Right")):
    curves = (
        ("Inner", ((0.405, 0.060, 3.035), (0.470, 0.085, 2.810),
                   (0.455, 0.130, 2.060), (0.355, 0.165, 1.770)), 0.045, 0.045, "hair_mid"),
        ("Center", ((0.410, 0.065, 3.040), (0.585, 0.090, 2.780),
                    (0.615, 0.145, 2.020), (0.500, 0.185, 1.550)), 0.050, 0.055, "hair"),
        ("Ribbon", ((0.410, 0.055, 3.035), (0.635, 0.080, 2.775),
                    (0.665, 0.145, 2.155), (0.575, 0.185, 1.840)), 0.028, 0.030, "hair_mid"),
        ("Outer", ((0.415, 0.075, 3.030), (0.675, 0.105, 2.795),
                   (0.710, 0.170, 2.180), (0.625, 0.205, 1.880)), 0.040, 0.045, "hair_mid"),
    )
    for clump_name, controls, root_width, bulge, color in curves:
        mirrored = tuple((x * sign, y, z) for x, y, z in controls)
        curved_hair_clump("TwinTail%s.%s" % (clump_name, side), mirrored,
                          root_width, bulge, color)
    highlight_controls = tuple((x * sign, y, z) for x, y, z in
                               ((0.430, -0.015, 3.000), (0.555, 0.000, 2.780),
                                (0.585, 0.045, 2.220), (0.525, 0.080, 1.880)))
    curved_hair_clump("TwinTailHighlight." + side, highlight_controls,
                      0.008, 0.012, "hair_highlight", samples=9)

    bow_root = Vector((0.405 * sign, -0.095, 3.005))
    fabric_loop("BowLoopUpper." + side, bow_root, (0.88 * sign, 0.0, 0.48),
                0.205, 0.064, "bow")
    fabric_loop("BowLoopLower." + side, bow_root, (0.90 * sign, 0.0, -0.44),
                0.185, 0.058, "bow")
    ellipsoid("BowKnot." + side, bow_root, (0.052, 0.038, 0.052),
              "hair_highlight", "Head", 20, 12)

# Final4 removes the detached back-cap highlight tubes.  They read as antennae
# at side angles; the curved tail highlights already supply enough separation.

# Lower every head-attached element together.  This shortens the exposed neck
# while preserving all facial and hair relationships authored above.
HEAD_LOWERING = 0.075
HEAD_SCALE_X = 1.060
HEAD_SCALE_Y = 1.350
HEAD_SCALE_Z = 1.033
HEAD_SCALE_PIVOT = Vector((0.0, 0.020, 2.300))
for part in CHARACTER_PARTS:
    if part.name != "Neck" and part.vertex_groups.get("Head") is not None:
        for vertex in part.data.vertices:
            vertex.co.z -= HEAD_LOWERING
            delta = vertex.co - HEAD_SCALE_PIVOT
            vertex.co = Vector((
                HEAD_SCALE_PIVOT.x + delta.x * HEAD_SCALE_X,
                HEAD_SCALE_PIVOT.y + delta.y * HEAD_SCALE_Y,
                HEAD_SCALE_PIVOT.z + delta.z * HEAD_SCALE_Z,
            ))


def object_bounds(objects):
    points = [obj.matrix_world @ vertex.co for obj in objects for vertex in obj.data.vertices]
    return {
        "min": [min(point[axis] for point in points) for axis in range(3)],
        "max": [max(point[axis] for point in points) for axis in range(3)],
    }


CRANIUM_BOUNDS = object_bounds((HAIR_BACK, FACE))
FACE_BOUNDS = object_bounds((FACE,))
CRANIUM_WIDTH = CRANIUM_BOUNDS["max"][0] - CRANIUM_BOUNDS["min"][0]
CRANIUM_DEPTH = CRANIUM_BOUNDS["max"][1] - CRANIUM_BOUNDS["min"][1]
VISUAL_HEAD_HEIGHT = CRANIUM_BOUNDS["max"][2] - FACE_BOUNDS["min"][2]
EYE_CENTER_SPACING = 2.0 * EYE_CENTER_X * HEAD_SCALE_X


def build_armature():
    data = bpy.data.armatures.new("OlderSisterV2_HumanoidArmature")
    rig = bpy.data.objects.new("OlderSisterV2_HumanoidRig", data)
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

    add("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.18), deform=False)
    add("Hips", (0.0, 0.0, 1.47), (0.0, 0.0, 1.65), "Root")
    add("Spine", (0.0, 0.0, 1.65), (0.0, 0.0, 1.90), "Hips", connected=True)
    add("Chest", (0.0, 0.0, 1.90), (0.0, 0.0, 2.06), "Spine", connected=True)
    add("UpperChest", (0.0, 0.0, 2.06), (0.0, 0.0, 2.18), "Chest", connected=True)
    add("Neck", (0.0, 0.0, 2.18), (0.0, 0.0, 2.285), "UpperChest", connected=True)
    add("Head", (0.0, 0.0, 2.285), (0.0, 0.0, 3.180), "Neck", connected=True)

    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        x = 0.170 * sign
        add(side + "UpperLeg", (x, 0.0, 1.47), (x, 0.0, 0.86), "Hips")
        add(side + "LowerLeg", (x, 0.0, 0.86), (x, 0.015, 0.245), side + "UpperLeg", connected=True)
        add(side + "Foot", (x, 0.015, 0.245), (x, -0.205, 0.105), side + "LowerLeg", connected=True)
        add(side + "Toes", (x, -0.205, 0.105), (x, -0.305, 0.075), side + "Foot", connected=True)

        add(side + "Shoulder", (0.07 * sign, 0.0, 2.14), (0.318 * sign, -0.002, 2.152), "UpperChest")
        add(side + "UpperArm", (0.318 * sign, -0.002, 2.152), (0.418 * sign, -0.014, 1.802),
            side + "Shoulder", connected=True)
        add(side + "LowerArm", (0.418 * sign, -0.014, 1.802), (0.438 * sign, -0.026, 1.520),
            side + "UpperArm", connected=True)
        add(side + "Hand", (0.438 * sign, -0.026, 1.520), (0.442 * sign, -0.043, 1.315),
            side + "LowerArm", connected=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    rig["humanoidContract"] = "FC-FAMILY-SHARED-HUMANOID-V1"
    rig["rootConvention"] = "BOTTOM_CENTER"
    rig["identityRole"] = "older_sister"
    rig["identityVersion"] = 2
    return rig


RIG = build_armature()

# Exactly one complete visible skinned mesh object and one atlas material.
bpy.ops.object.select_all(action="DESELECT")
for part in CHARACTER_PARTS:
    part.select_set(True)
bpy.context.view_layer.objects.active = FACE
bpy.ops.object.join()
BODY = bpy.context.object
BODY.name = "OlderSisterV2_CompleteSkinnedBody"
for polygon in BODY.data.polygons:
    polygon.material_index = 0
while len(BODY.data.materials) > 1:
    BODY.data.materials.pop(index=1)
BODY.parent = RIG
modifier = BODY.modifiers.new(name="OlderSisterV2_HumanoidSkin", type="ARMATURE")
modifier.object = RIG
modifier.use_vertex_groups = True


def weight_audit(obj):
    unweighted = 0
    invalid_sum = 0
    max_influences = 0
    for vertex in obj.data.vertices:
        influences = [element.weight for element in vertex.groups if element.weight > 0.0001]
        max_influences = max(max_influences, len(influences))
        if not influences:
            unweighted += 1
        elif abs(sum(influences) - 1.0) > 0.002:
            invalid_sum += 1
    return unweighted, invalid_sum, max_influences


UNWEIGHTED, INVALID_WEIGHT_SUM, MAX_INFLUENCES = weight_audit(BODY)
if UNWEIGHTED or INVALID_WEIGHT_SUM:
    raise RuntimeError("Skin audit failed: unweighted=%d invalidSum=%d" % (UNWEIGHTED, INVALID_WEIGHT_SUM))


def simple_material(name, color, roughness=0.95):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.03
    return material


floor_material = simple_material("M_V2ReviewFloor", (0.235, 0.255, 0.295))
bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0.0, 0.0, 0.0))
FLOOR = bpy.context.object
FLOOR.name = "V2ReviewFloor"
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
    obj.rotation_euler = (Vector((0.0, 0.0, 1.65)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


add_area("V2Key", (-4.0, -5.0, 6.2), 920.0, (1.0, 0.88, 0.78), 4.5)
add_area("V2Fill", (4.5, -3.3, 4.8), 620.0, (0.76, 0.88, 1.0), 4.2)
add_area("V2Rim", (0.0, 4.8, 5.5), 780.0, (0.88, 0.78, 1.0), 3.8)
add_area("V2Top", (0.0, 0.0, 7.0), 360.0, (1.0, 0.96, 0.90), 3.0)

camera_data = bpy.data.cameras.new("OlderSisterV2ReviewCamera")
CAMERA = bpy.data.objects.new("OlderSisterV2ReviewCamera", camera_data)
bpy.context.collection.objects.link(CAMERA)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 3.75
scene.camera = CAMERA


def point_camera(location, target=(0.0, 0.0, 1.64)):
    CAMERA.location = location
    CAMERA.rotation_euler = (Vector(target) - CAMERA.location).to_track_quat("-Z", "Y").to_euler()


try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 960 if QUALITY == "final" else 600
scene.render.resolution_y = 1440 if QUALITY == "final" else 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.145, 0.175, 0.220, 1.0)
background.inputs["Strength"].default_value = 0.65
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass


def clear_pose():
    for bone in RIG.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def apply_walk_contact(left_forward=True):
    clear_pose()
    lead = 1.0 if left_forward else -1.0
    RIG.pose.bones["LeftUpperLeg"].rotation_euler.x = math.radians(18.0 * lead)
    RIG.pose.bones["RightUpperLeg"].rotation_euler.x = math.radians(-18.0 * lead)
    RIG.pose.bones["LeftLowerLeg"].rotation_euler.x = math.radians(-5.0 if left_forward else 24.0)
    RIG.pose.bones["RightLowerLeg"].rotation_euler.x = math.radians(24.0 if left_forward else -5.0)
    RIG.pose.bones["LeftFoot"].rotation_euler.x = math.radians(-7.0 * lead)
    RIG.pose.bones["RightFoot"].rotation_euler.x = math.radians(7.0 * lead)
    RIG.pose.bones["LeftUpperArm"].rotation_euler.x = math.radians(-14.0 * lead)
    RIG.pose.bones["RightUpperArm"].rotation_euler.x = math.radians(14.0 * lead)
    bpy.context.view_layer.update()


STATIC_VIEWS = {
    "front": (0.0, -8.0, 1.64),
    "side": (8.0, 0.0, 1.64),
    "back": (0.0, 8.0, 1.64),
    "three-quarter": (5.7, -5.7, 1.68),
}
STATIC_RENDER_PATHS = []
clear_pose()
for view_name, camera_location in STATIC_VIEWS.items():
    point_camera(camera_location)
    path = os.path.join(OUTPUT, "older-sister-blender-v2-%s-%s.png" % (view_name, QUALITY))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    STATIC_RENDER_PATHS.append(path)

DEFORMATION_RENDER_PATHS = []
TURNTABLE_RENDER_PATHS = []
if QUALITY == "final":
    point_camera((5.7, -5.7, 1.68))
    for pose_name, left_forward in (("p0-left-contact", True), ("p3-right-contact", False)):
        apply_walk_contact(left_forward)
        path = os.path.join(OUTPUT, "older-sister-blender-v2-deform-%s.png" % pose_name)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        DEFORMATION_RENDER_PATHS.append(path)
    clear_pose()

    turntable_directory = os.path.join(OUTPUT, "turntable")
    os.makedirs(turntable_directory, exist_ok=True)
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    camera_data.ortho_scale = 3.80
    for index in range(24):
        angle = 2.0 * math.pi * index / 24.0
        radius = 8.1
        point_camera((math.sin(angle) * radius, -math.cos(angle) * radius, 1.65))
        path = os.path.join(turntable_directory, "older-sister-v2-turntable-%02d.png" % index)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        TURNTABLE_RENDER_PATHS.append(path)

clear_pose()
scene.render.resolution_x = 960 if QUALITY == "final" else 600
scene.render.resolution_y = 1440 if QUALITY == "final" else 900
camera_data.ortho_scale = 3.75

# Review export only; no production/default Unity path is touched.
bpy.ops.object.select_all(action="DESELECT")
BODY.select_set(True)
RIG.select_set(True)
bpy.context.view_layer.objects.active = RIG
FBX_PATH = os.path.join(OUTPUT, "older-sister-blender-humanoid-v2.fbx")
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
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

BLEND_PATH = os.path.join(OUTPUT, "older-sister-blender-identity-v2.blend")
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

world_bounds = [BODY.matrix_world @ Vector(corner) for corner in BODY.bound_box]
bounds_min = [min(point[index] for point in world_bounds) for index in range(3)]
bounds_max = [max(point[index] for point in world_bounds) for index in range(3)]
standing_height = bounds_max[2] - bounds_min[2]
measured_visual_heads = standing_height / VISUAL_HEAD_HEIGHT
face_width = FACE_BOUNDS["max"][0] - FACE_BOUNDS["min"][0]
eye_width = 2.0 * 0.108 * HEAD_SCALE_X
receipt = {
    "contract": "FC-OLDER-SISTER-BLENDER-IDENTITY-V2-FINAL4",
    "status": "CANDIDATE_VISUAL_REVIEW_REQUIRED_DO_NOT_PROMOTE",
    "quality": QUALITY,
    "iteration": ITERATION,
    "identity": {
        "role": "older_sister",
        "age": 20,
        "style": "runtime-2d-anime-toon-flat-clean",
        "proportionTargetHeadsRange": [3.4, 3.6],
        "measuredVisualHeads": measured_visual_heads,
        "hair": "high-long-black-twin-tails-with-large-black-bows",
        "eyes": "large-teal-iris-thin-sclera",
        "outfit": "charcoal-sleeveless-tank-and-navy-dolphin-shorts-with-white-piping",
        "feet": "two-compact-complete-bare-foot-wedges-toes-implied-without-grooves",
    },
    "sourcePolicy": {
        "newTopology": True,
        "runtime2DVisualReferenceOnly": True,
        "runtimePixelsImported": False,
        "runtimePixelsCopied": False,
        "runtimeGeometryTraceUsed": False,
        "runtimeMotionDonorUsed": False,
        "retiredTurnaroundAuthority": False,
        "retiredTurnaroundOpened": False,
        "rejectedV1Imported": False,
        "rejectedV1PixelsOpened": False,
        "legacyMeshImported": False,
        "textureDonorUsed": False,
        "decalUsed": False,
        "billboardUsed": False,
        "micrograinStrength": 0.0,
        "productionMutation": False,
        "externalReferenceGeometryImported": False,
        "externalReferenceTopologyCopied": False,
        "externalReferenceTextureOrUvCopied": False,
        "externalReferenceRigOrWeightsCopied": False,
        "externalReferenceSilhouettePartCopied": False,
    },
    "visualReferences": {
        "runtimeFrameDirectory": FRAME_DIR,
        "runtimeFrameCount": len(RUNTIME_FRAMES),
        "runtimeFrameManifestSha256": RUNTIME_FRAME_MANIFEST_SHA,
        "runtimeSheetA": {"path": SHEET_A, "sha256": sha256(SHEET_A)},
        "runtimeSheetB": {"path": SHEET_B, "sha256": sha256(SHEET_B)},
        "neutral": {"path": NEUTRAL_REFERENCE, "sha256": sha256(NEUTRAL_REFERENCE)},
    },
    "blenderVersion": bpy.app.version_string,
    "bodyObject": BODY.name,
    "skinnedMeshObjectCount": 1,
    "vertexCount": len(BODY.data.vertices),
    "polygonCount": len(BODY.data.polygons),
    "materialCount": len(BODY.data.materials),
    "textureAtlas": ATLAS_PATH,
    "atlasInterpolation": "Closest",
    "materialRoughness": 0.94,
    "materialSpecularIorLevel": 0.045,
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
    "standingHeight": standing_height,
    "rootConvention": "BOTTOM_CENTER",
    "final4ProportionAudit": {
        "headScaleXYZ": [HEAD_SCALE_X, HEAD_SCALE_Y, HEAD_SCALE_Z],
        "headLowering": HEAD_LOWERING,
        "visualHeadHeight": VISUAL_HEAD_HEIGHT,
        "measuredVisualHeads": measured_visual_heads,
        "targetVisualHeadsRange": [3.4, 3.6],
        "craniumWidth": CRANIUM_WIDTH,
        "craniumDepth": CRANIUM_DEPTH,
        "craniumDepthToWidth": CRANIUM_DEPTH / CRANIUM_WIDTH,
        "targetCraniumDepthToWidthRange": [0.85, 0.95],
        "faceWidth": face_width,
        "eyeCenterSpacing": EYE_CENTER_SPACING,
        "eyeCenterSpacingToFaceWidth": EYE_CENTER_SPACING / face_width,
        "singleEyeWidth": eye_width,
        "singleEyeWidthToFaceWidth": eye_width / face_width,
        "targetEyeCenterSpacingToFaceWidth": 0.50,
        "targetSingleEyeWidthToFaceWidthRange": [0.28, 0.33],
        "neckStyle": "short-wide-embedded",
        "shoulderStyle": "rounded-continuous",
        "limbStyle": "continuous-proximal-to-distal-taper",
        "handStyle": "small-wedge-mitten",
        "footStyle": "compact-complete-barefoot-wedge",
    },
    "anatomyAudit": {
        "armCount": 2,
        "handCount": 2,
        "handStyle": "small-wedge-mitten-with-thumb-no-finger-grooves",
        "impliedFingerCountPerHand": 5,
        "visibleFingerGrooveCountPerHand": 0,
        "legCount": 2,
        "bareFootCount": 2,
        "footStyle": "compact-complete-barefoot-wedge-no-toe-grooves",
        "impliedToeCountPerFoot": 5,
        "visibleToeGrooveCountPerFoot": 0,
    },
    "shortsPipingAudit": {
        "style": "atlas-face-filled-flush-surface-band",
        "independentWireGeometryCount": 0,
        "surfaceSharedWithShorts": True,
        "coverage": "continuous-360-waist-and-hem",
        "targetSeparationCount24Views": 0,
        "targetDiscontinuityCount24Views": 0,
    },
    "outputs": {
        "blend": BLEND_PATH,
        "fbx": FBX_PATH,
        "atlas": ATLAS_PATH,
        "staticRenders": STATIC_RENDER_PATHS,
        "deformationRenders": DEFORMATION_RENDER_PATHS,
        "turntableRenders": TURNTABLE_RENDER_PATHS,
    },
    "productionEligible": False,
}
RECEIPT_PATH = os.path.join(OUTPUT, "build-receipt.json")
with open(RECEIPT_PATH, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("OLDER_SISTER_V2_BUILD: PASS")
print(json.dumps(receipt, ensure_ascii=False, indent=2))
