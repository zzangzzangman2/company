"""Build the Family Company Father V2 anime-toon 3D candidate.

Identity is judged from the canonical Father 2D runtime HighMotion frames and
the canonical neutral illustration.  Those PNG files are only hashed as audit
inputs: pixels are never loaded into Blender, projected, copied, or converted
into geometry or textures.  Geometry, the flat atlas, skinning, and rig are
authored procedurally by this file from an empty Blender scene.

Example:

    blender.exe --background --python Tools/Blender/build_father_blender_identity_v2.py -- \
      --output Artifacts/Family3DBlenderFatherV2/Final \
      --neutral Assets/Art/Characters/Father/father_office_neutral_v1.png \
      --runtime-dir Assets/Art/Characters/Father/Pixel/HighMotion/Frames \
      --candidate-dir Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV2 \
      --stage final
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


SCRIPT_VERSION = "FC-FATHER-BLENDER-IDENTITY-V2"
ATLAS_SIZE = 1024
ATLAS_GRID = 8


# Deliberately compact, clean colors sampled/normalized from the 2D canon.
# Values are sRGB-like Blender inputs.  No skin or fabric micrograin is added.
PALETTE = {
    "Outline": (0.141, 0.122, 0.137, 1.0),
    "Skin": (0.961, 0.784, 0.608, 1.0),
    "SkinShadow": (0.910, 0.616, 0.396, 1.0),
    "SkinLight": (1.000, 0.855, 0.675, 1.0),
    "EyeWhite": (0.972, 0.955, 0.914, 1.0),
    "EyeBrown": (0.290, 0.184, 0.133, 1.0),
    "Hair": (0.157, 0.141, 0.161, 1.0),
    "HairMid": (0.190, 0.174, 0.204, 1.0),
    "HairHighlight": (0.235, 0.216, 0.249, 1.0),
    "TempleGray": (0.390, 0.392, 0.402, 1.0),
    "Shirt": (0.275, 0.465, 0.525, 1.0),
    "ShirtShadow": (0.192, 0.357, 0.412, 1.0),
    "ShirtLight": (0.424, 0.600, 0.655, 1.0),
    "Trouser": (0.282, 0.282, 0.298, 1.0),
    "TrouserShadow": (0.184, 0.188, 0.200, 1.0),
    "TrouserLight": (0.365, 0.361, 0.376, 1.0),
    "Leather": (0.349, 0.251, 0.212, 1.0),
    "LeatherLight": (0.553, 0.400, 0.286, 1.0),
    "LeatherDark": (0.188, 0.125, 0.106, 1.0),
    "Silver": (0.720, 0.718, 0.690, 1.0),
    "SilverDark": (0.286, 0.302, 0.310, 1.0),
    "WatchFace": (0.865, 0.847, 0.796, 1.0),
    "Mouth": (0.431, 0.196, 0.165, 1.0),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--neutral", required=True)
    parser.add_argument("--runtime-dir", required=True)
    parser.add_argument("--candidate-dir")
    parser.add_argument("--stage", choices=("draft", "final"), default="final")
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def runtime_manifest(runtime_dir: Path) -> tuple[str, list[dict[str, str]]]:
    rows = []
    for path in sorted(runtime_dir.glob("father_*_walk_*.png")):
        rows.append({"name": path.name, "sha256": sha256(path)})
    if len(rows) != 48:
        raise RuntimeError(f"expected 48 Father runtime frames, got {len(rows)}")
    payload = "".join(f"{row['name']}\t{row['sha256']}\n" for row in rows)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest().upper(), rows


def clean_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    scene.render.fps = 30
    scene.view_settings.look = "AgX - Medium Low Contrast"
    scene.world = bpy.data.worlds.new("FatherV2_ToonWorld")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.865, 0.880, 0.910, 1.0)
    bg.inputs["Strength"].default_value = 0.78


def make_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.96
    bsdf.inputs["Specular IOR Level"].default_value = 0.06
    bsdf.inputs["Metallic"].default_value = 0.0
    return material


def apply_transforms(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.append(material)


def smooth(obj: bpy.types.Object, angle: float = 0.70) -> None:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.set_sharp_from_angle(angle=angle)


def bevel(obj: bpy.types.Object, width: float, segments: int = 2) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("GraphicEdgeSoftening", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(28.0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def mesh_object(
    name: str,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material: bpy.types.Material,
    do_smooth: bool = False,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    if do_smooth:
        smooth(obj)
    return obj


def add_rounded_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    bevel_width: float = 0.008,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    apply_transforms(obj)
    if bevel_width > 0:
        bevel(obj, bevel_width, 2)
    assign_material(obj, material)
    return obj


def add_ellipsoid(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    segments: int = 20,
    rings: int = 12,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    do_smooth: bool = True,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transforms(obj)
    assign_material(obj, material)
    if do_smooth:
        smooth(obj, 0.75)
    return obj


def vertical_loft(
    name: str,
    rings: list[tuple[float, float, float, float]],
    material: bpy.types.Material,
    segments: int = 16,
    do_smooth: bool = False,
) -> bpy.types.Object:
    """Rings are (z, center_y, radius_x, radius_y)."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for z, cy, rx, ry in rings:
        for index in range(segments):
            phi = 2.0 * math.pi * index / segments
            vertices.append((rx * math.cos(phi), cy + ry * math.sin(phi), z))
    for ring_index in range(len(rings) - 1):
        a = ring_index * segments
        b = (ring_index + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, a + nxt, b + nxt, b + index))
    bottom = len(vertices)
    vertices.append((0.0, rings[0][1], rings[0][0]))
    top = len(vertices)
    vertices.append((0.0, rings[-1][1], rings[-1][0]))
    last = (len(rings) - 1) * segments
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((bottom, nxt, index))
        faces.append((top, last + index, last + nxt))
    return mesh_object(name, vertices, faces, material, do_smooth)


def segment_loft(
    name: str,
    centers: list[tuple[float, float, float]],
    radii: list[tuple[float, float]],
    material: bpy.types.Material,
    segments: int = 14,
    do_smooth: bool = False,
) -> bpy.types.Object:
    if len(centers) != len(radii):
        raise ValueError("centers/radii mismatch")
    points = [Vector(value) for value in centers]
    depth = Vector((0.0, 1.0, 0.0))
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring_index, point in enumerate(points):
        if ring_index == 0:
            tangent = (points[1] - points[0]).normalized()
        elif ring_index == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[ring_index + 1] - points[ring_index - 1]).normalized()
        side = depth.cross(tangent)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        local_depth = tangent.cross(side).normalized()
        rx, ry = radii[ring_index]
        for index in range(segments):
            phi = 2.0 * math.pi * index / segments
            vertex = point + side * (math.cos(phi) * rx) + local_depth * (math.sin(phi) * ry)
            vertices.append(tuple(vertex))
    for ring_index in range(len(points) - 1):
        a = ring_index * segments
        b = (ring_index + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, a + nxt, b + nxt, b + index))
    start = len(vertices)
    vertices.append(tuple(points[0]))
    end = len(vertices)
    vertices.append(tuple(points[-1]))
    last = (len(points) - 1) * segments
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((start, nxt, index))
        faces.append((end, last + index, last + nxt))
    return mesh_object(name, vertices, faces, material, do_smooth)


def shoe_loft(
    name: str,
    x_center: float,
    sections: list[tuple[float, float, float, float]],
    material: bpy.types.Material,
    segments: int = 18,
) -> bpy.types.Object:
    """Level-soled Y-axis loft; sections are y, z, half-width, half-height."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for y, z, radius_x, radius_z in sections:
        for index in range(segments):
            phi = 2.0 * math.pi * index / segments
            vertices.append((x_center + math.cos(phi) * radius_x, y, z + math.sin(phi) * radius_z))
    for section_index in range(len(sections) - 1):
        a = section_index * segments
        b = (section_index + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, a + nxt, b + nxt, b + index))
    rear = len(vertices)
    vertices.append((x_center, sections[0][0], sections[0][1]))
    front = len(vertices)
    vertices.append((x_center, sections[-1][0], sections[-1][1]))
    last = (len(sections) - 1) * segments
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((rear, nxt, index))
        faces.append((front, last + index, last + nxt))
    return mesh_object(name, vertices, faces, material, True)


def add_poly_tube(
    name: str,
    points: list[tuple[float, float, float]],
    radius: float,
    material: bpy.types.Material,
    cyclic: bool = False,
    resolution: int = 0,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name + "Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for item, point in zip(spline.points, points):
        item.co = (*point, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    assign_material(obj, material)
    obj.select_set(False)
    return obj


def rounded_rectangle_points(
    center_x: float,
    y: float,
    center_z: float,
    half_width: float,
    half_height: float,
    corner_radius: float,
    steps_per_corner: int = 4,
) -> list[tuple[float, float, float]]:
    """Clockwise rounded rectangle used by the thin anime glasses frames."""
    radius = min(corner_radius, half_width, half_height)
    points: list[tuple[float, float, float]] = []
    corners = (
        (half_width - radius, half_height - radius, 0.0),
        (-half_width + radius, half_height - radius, 90.0),
        (-half_width + radius, -half_height + radius, 180.0),
        (half_width - radius, -half_height + radius, 270.0),
    )
    for offset_x, offset_z, start_degrees in corners:
        for step in range(steps_per_corner + 1):
            angle = math.radians(start_degrees + 90.0 * step / steps_per_corner)
            points.append(
                (
                    center_x + offset_x + radius * math.cos(angle),
                    y,
                    center_z + offset_z + radius * math.sin(angle),
                )
            )
    return points


def add_hair_ribbon(
    name: str,
    points: list[tuple[float, float, float]],
    widths: list[float],
    material: bpy.types.Material,
    thickness: float = 0.010,
    normal_hint: tuple[float, float, float] = (0.0, -1.0, 0.12),
) -> bpy.types.Object:
    pts = [Vector(item) for item in points]
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for index, (point, width) in enumerate(zip(pts, widths)):
        if index == 0:
            tangent = (pts[1] - pts[0]).normalized()
        elif index == len(pts) - 1:
            tangent = (pts[-1] - pts[-2]).normalized()
        else:
            tangent = (pts[index + 1] - pts[index - 1]).normalized()
        normal = Vector(normal_hint).normalized()
        side = tangent.cross(normal)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        lateral = side * width * 0.5
        depth = normal * thickness * 0.5
        vertices.extend(
            [
                tuple(point - lateral - depth),
                tuple(point + lateral - depth),
                tuple(point + lateral + depth),
                tuple(point - lateral + depth),
            ]
        )
    for ring in range(len(pts) - 1):
        a = ring * 4
        b = (ring + 1) * 4
        faces.extend(
            [
                (a, b, b + 1, a + 1),
                (a + 1, b + 1, b + 2, a + 2),
                (a + 2, b + 2, b + 3, a + 3),
                (a + 3, b + 3, b, a),
            ]
        )
    faces.append((0, 1, 2, 3))
    end = (len(pts) - 1) * 4
    faces.append((end + 3, end + 2, end + 1, end))
    obj = mesh_object(name, vertices, faces, material, False)
    bevel(obj, 0.003, 1)
    return obj


def create_hair_cap(material: bpy.types.Material) -> bpy.types.Object:
    segments = 32
    rings = 12
    center = Vector((0.0, 0.018, 1.704))
    # Preserve the short side-part silhouette while giving the adult skull a
    # little more front/back volume instead of a paper-thin profile.
    rx, ry, rz = 0.242, 0.204, 0.278
    vertices = [tuple(center + Vector((0.0, 0.0, rz)))]
    faces: list[tuple[int, ...]] = []
    for ring in range(1, rings + 1):
        t = ring / rings
        for index in range(segments):
            phi = 2.0 * math.pi * index / segments
            frontness = max(0.0, -math.sin(phi))
            # The base stays inside the visible pointed fringe.  A wavy lower
            # boundary and a small asymmetric crown keep it from reading as a
            # smooth helmet while preserving the Father's neat short haircut.
            theta_max = 2.04 - 0.49 * frontness + 0.085 * math.sin(phi * 4.0 + 0.35) * (1.0 - 0.70 * frontness)
            theta = theta_max * t
            # Keep the crown itself clean and round.  Character comes from the
            # lower edge and the separate pointed fringe, not silhouette spikes.
            clump_scale = 1.0 + 0.006 * math.sin(phi * 5.0 + 0.6) * (t * t)
            crown_shift = 0.0
            point = center + Vector(
                (
                    rx * clump_scale * math.sin(theta) * math.cos(phi) + crown_shift,
                    ry * clump_scale * math.sin(theta) * math.sin(phi),
                    rz * math.cos(theta),
                )
            )
            vertices.append(tuple(point))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((0, 1 + index, 1 + nxt))
    for ring in range(rings - 1):
        a = 1 + ring * segments
        b = 1 + (ring + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, b + index, b + nxt, a + nxt))
    return mesh_object("Hair_LayeredBase", vertices, faces, material, True)


def add_weight(obj: bpy.types.Object, bone: str, weight: float = 1.0) -> None:
    group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), weight, "REPLACE")


def add_blended_weights_by_z(obj: bpy.types.Object, controls: list[tuple[float, str]]) -> None:
    controls = sorted(controls)
    groups = {
        name: obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        for _, name in controls
    }
    for vertex in obj.data.vertices:
        z = (obj.matrix_world @ vertex.co).z
        if z <= controls[0][0]:
            groups[controls[0][1]].add([vertex.index], 1.0, "REPLACE")
            continue
        if z >= controls[-1][0]:
            groups[controls[-1][1]].add([vertex.index], 1.0, "REPLACE")
            continue
        for index in range(len(controls) - 1):
            z0, bone0 = controls[index]
            z1, bone1 = controls[index + 1]
            if z0 <= z <= z1:
                factor = (z - z0) / max(z1 - z0, 1e-6)
                groups[bone0].add([vertex.index], 1.0 - factor, "REPLACE")
                groups[bone1].add([vertex.index], factor, "REPLACE")
                break


def add_segment_weights(
    obj: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    first_bone: str,
    second_bone: str,
    split: float = 0.5,
    blend: float = 0.12,
) -> None:
    a = Vector(start)
    axis = Vector(end) - a
    denom = axis.length_squared
    first = obj.vertex_groups.get(first_bone) or obj.vertex_groups.new(name=first_bone)
    second = obj.vertex_groups.get(second_bone) or obj.vertex_groups.new(name=second_bone)
    lo = split - blend * 0.5
    hi = split + blend * 0.5
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        t = max(0.0, min(1.0, (point - a).dot(axis) / max(denom, 1e-8)))
        if t <= lo:
            first.add([vertex.index], 1.0, "REPLACE")
        elif t >= hi:
            second.add([vertex.index], 1.0, "REPLACE")
        else:
            factor = (t - lo) / max(hi - lo, 1e-6)
            first.add([vertex.index], 1.0 - factor, "REPLACE")
            second.add([vertex.index], factor, "REPLACE")


def create_armature() -> bpy.types.Object:
    data = bpy.data.armatures.new("FatherV2_Humanoid_ArmatureData")
    armature = bpy.data.objects.new("FatherV2_Humanoid_Armature", data)
    bpy.context.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(
        name: str,
        head: tuple[float, float, float],
        tail: tuple[float, float, float],
        parent: str | None = None,
        connected: bool = False,
        deform: bool = True,
    ) -> None:
        item = data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.use_deform = deform
        if parent:
            item.parent = data.edit_bones[parent]
            item.use_connect = connected

    bone("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.10), deform=False)
    bone("Hips", (0.0, 0.0, 0.82), (0.0, 0.0, 0.93), "Root")
    bone("Spine", (0.0, 0.0, 0.93), (0.0, 0.0, 1.09), "Hips", True)
    bone("Chest", (0.0, 0.0, 1.09), (0.0, 0.0, 1.23), "Spine", True)
    bone("UpperChest", (0.0, 0.0, 1.23), (0.0, 0.0, 1.34), "Chest", True)
    bone("Neck", (0.0, 0.0, 1.35), (0.0, 0.0, 1.41), "UpperChest", True)
    bone("Head", (0.0, 0.0, 1.41), (0.0, 0.0, 1.91), "Neck", True)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        bone(side + "Shoulder", (0.10 * sign, 0.0, 1.30), (0.245 * sign, 0.0, 1.265), "UpperChest")
        bone(side + "UpperArm", (0.245 * sign, 0.0, 1.265), (0.297 * sign, 0.0, 1.055), side + "Shoulder", True)
        bone(side + "LowerArm", (0.297 * sign, 0.0, 1.055), (0.323 * sign, 0.0, 0.875), side + "UpperArm", True)
        bone(side + "Hand", (0.323 * sign, 0.0, 0.875), (0.328 * sign, -0.005, 0.780), side + "LowerArm", True)
        bone(side + "UpperLeg", (0.120 * sign, 0.0, 0.82), (0.120 * sign, 0.0, 0.47), "Hips")
        bone(side + "LowerLeg", (0.120 * sign, 0.0, 0.47), (0.120 * sign, 0.0, 0.145), side + "UpperLeg", True)
        bone(side + "Foot", (0.120 * sign, 0.0, 0.145), (0.120 * sign, -0.145, 0.075), side + "LowerLeg", True)
        bone(side + "Toes", (0.120 * sign, -0.145, 0.075), (0.120 * sign, -0.255, 0.058), side + "Foot", True)

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "STICK"
    armature.select_set(False)
    return armature


def build_character(materials: dict[str, bpy.types.Material]) -> tuple[bpy.types.Object, bpy.types.Object]:
    parts: list[bpy.types.Object] = []

    # Adult curved trapezoid torso: broad enough to read as Father without the
    # old square-shouldered Roblox silhouette.
    torso = vertical_loft(
        "Shirt_GraphicTorso",
        [
            (0.885, 0.000, 0.265, 0.145),
            (0.965, 0.000, 0.280, 0.153),
            (1.080, 0.000, 0.296, 0.162),
            (1.185, 0.000, 0.310, 0.171),
            (1.255, 0.000, 0.308, 0.174),
            (1.285, -0.001, 0.302, 0.172),
            (1.315, -0.002, 0.282, 0.166),
            (1.345, -0.003, 0.242, 0.150),
        ],
        materials["Shirt"],
        20,
        True,
    )
    bevel(torso, 0.010, 1)
    add_blended_weights_by_z(torso, [(0.88, "Hips"), (1.02, "Spine"), (1.18, "Chest"), (1.34, "UpperChest")])
    parts.append(torso)

    pelvis = vertical_loft(
        "Trouser_Pelvis",
        [(0.735, 0.005, 0.225, 0.140), (0.820, 0.000, 0.245, 0.150), (0.910, 0.000, 0.255, 0.155)],
        materials["Trouser"],
        20,
        True,
    )
    bevel(pelvis, 0.008, 1)
    add_weight(pelvis, "Hips")
    parts.append(pelvis)

    belt = vertical_loft(
        "Brown_Belt",
        [(0.872, 0.0, 0.258, 0.158), (0.918, 0.0, 0.260, 0.159)],
        materials["Leather"],
        18,
        False,
    )
    add_weight(belt, "Hips")
    parts.append(belt)
    buckle = add_rounded_box("Belt_SquareBuckle", (0.0, -0.173, 0.895), (0.078, 0.018, 0.058), materials["Silver"], 0.005)
    add_weight(buckle, "Hips")
    parts.append(buckle)
    buckle_inner = add_rounded_box("Belt_BuckleInner", (0.0, -0.185, 0.895), (0.046, 0.010, 0.028), materials["LeatherDark"], 0.003)
    add_weight(buckle_inner, "Hips")
    parts.append(buckle_inner)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        x = 0.120 * sign
        leg = segment_loft(
            side + "_StraightSlacks",
            [(x, 0.0, 0.80), (x, 0.0, 0.63), (x, 0.0, 0.47), (x, 0.0, 0.30), (x, 0.0, 0.135)],
            [(0.115, 0.105), (0.109, 0.101), (0.101, 0.095), (0.094, 0.088), (0.085, 0.080)],
            materials["Trouser"],
            18,
            True,
        )
        bevel(leg, 0.006, 1)
        add_segment_weights(leg, (x, 0.0, 0.82), (x, 0.0, 0.145), side + "UpperLeg", side + "LowerLeg", 0.52, 0.14)
        parts.append(leg)

        crease = add_poly_tube(
            side + "_TrouserCrease",
            [(x, -0.112, 0.75), (x, -0.105, 0.48), (x, -0.090, 0.19)],
            0.004,
            materials["TrouserLight"],
        )
        add_segment_weights(crease, (x, 0.0, 0.82), (x, 0.0, 0.145), side + "UpperLeg", side + "LowerLeg", 0.52, 0.10)
        parts.append(crease)

        sole = add_rounded_box(side + "_DressShoeSole", (x, -0.060, 0.030), (0.180, 0.310, 0.050), materials["LeatherDark"], 0.025)
        add_segment_weights(sole, (x, 0.10, 0.08), (x, -0.25, 0.08), side + "Foot", side + "Toes", 0.68, 0.14)
        parts.append(sole)
        upper = shoe_loft(
            side + "_CompactOxford",
            x,
            [(0.050, 0.090, 0.070, 0.052), (-0.035, 0.105, 0.086, 0.060), (-0.145, 0.086, 0.082, 0.050), (-0.220, 0.065, 0.062, 0.032)],
            materials["Leather"],
            18,
        )
        bevel(upper, 0.004, 1)
        add_segment_weights(upper, (x, 0.10, 0.10), (x, -0.25, 0.08), side + "Foot", side + "Toes", 0.68, 0.14)
        parts.append(upper)
        toe_line = add_poly_tube(side + "_OxfordToeLine", [(x - 0.065, -0.165, 0.115), (x, -0.195, 0.126), (x + 0.065, -0.165, 0.115)], 0.0035, materials["LeatherLight"])
        add_weight(toe_line, side + "Foot")
        parts.append(toe_line)
        for lace_index in range(2):
            lace_y = -0.045 - lace_index * 0.035
            lace = add_poly_tube(side + f"_Lace{lace_index}", [(x - 0.040, lace_y, 0.143), (x + 0.040, lace_y, 0.143)], 0.0030, materials["LeatherDark"])
            add_weight(lace, side + "Foot")
            parts.append(lace)

    neck = vertical_loft("Neck", [(1.330, 0.0, 0.100, 0.086), (1.410, 0.0, 0.110, 0.092)], materials["Skin"], 14, False)
    bevel(neck, 0.005, 1)
    add_blended_weights_by_z(neck, [(1.330, "UpperChest"), (1.410, "Neck")])
    parts.append(neck)

    # Soft mature anime face.  Extra loft rings and radial resolution remove
    # the angular jaw while keeping a short, adult lower face.
    head = vertical_loft(
        "Head_AnimeAdult",
        [
            (1.385, -0.006, 0.110, 0.092),
            (1.410, -0.006, 0.150, 0.123),
            (1.445, -0.006, 0.192, 0.157),
            (1.490, -0.007, 0.224, 0.180),
            (1.550, -0.007, 0.242, 0.194),
            (1.620, -0.006, 0.250, 0.199),
            (1.690, -0.004, 0.246, 0.194),
            (1.750, 0.000, 0.232, 0.184),
            (1.805, 0.004, 0.202, 0.166),
            (1.845, 0.004, 0.154, 0.131),
        ],
        materials["Skin"],
        32,
        True,
    )
    add_weight(head, "Head")
    parts.append(head)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        ear = add_ellipsoid(side + "_Ear", (0.238 * sign, -0.002, 1.590), (0.030, 0.025, 0.052), materials["Skin"], 14, 8, do_smooth=True)
        add_weight(ear, "Head")
        parts.append(ear)
        inner = add_ellipsoid(side + "_EarInner", (0.243 * sign, -0.024, 1.590), (0.010, 0.005, 0.025), materials["SkinShadow"], 12, 6, do_smooth=False)
        add_weight(inner, "Head")
        parts.append(inner)

    # Calm mature anime eyes: smooth almond-like volumes, vertically biased
    # brown irises, narrow sclera and soft upper/lower lid lines.  The former
    # hexagonal white planes read as a startled fixed stare.
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        # At the eye line the face is about 0.50 m wide.  An outer-to-outer
        # span of about 0.246 m (49%) with each 0.082 m eye keeps the anime
        # readability while returning the 46-year-old Father's mature spacing.
        cx = 0.082 * sign
        eye_outline = add_ellipsoid(
            side + "_EyeSoftOutline",
            (cx, -0.204, 1.611),
            (0.041, 0.0040, 0.0295),
            materials["Outline"],
            24,
            12,
            do_smooth=True,
        )
        add_weight(eye_outline, "Head")
        parts.append(eye_outline)
        eye_white = add_ellipsoid(
            side + "_EyeNarrowSclera",
            (cx, -0.209, 1.610),
            (0.0365, 0.0035, 0.0250),
            materials["EyeWhite"],
            24,
            12,
            do_smooth=True,
        )
        add_weight(eye_white, "Head")
        parts.append(eye_white)
        iris_x = cx
        iris = add_ellipsoid(side + "_VerticalBrownIris", (iris_x, -0.214, 1.608), (0.024, 0.0038, 0.0240), materials["EyeBrown"], 20, 10, do_smooth=True)
        add_weight(iris, "Head")
        parts.append(iris)
        pupil = add_ellipsoid(side + "_Pupil", (iris_x, -0.218, 1.608), (0.008, 0.002, 0.0135), materials["Outline"], 14, 8, do_smooth=True)
        add_weight(pupil, "Head")
        parts.append(pupil)
        shine = add_ellipsoid(side + "_EyeShine", (iris_x - 0.0065 * sign, -0.221, 1.619), (0.0055, 0.0015, 0.0065), materials["EyeWhite"], 10, 6, do_smooth=True)
        add_weight(shine, "Head")
        parts.append(shine)
        upper_lid = add_poly_tube(
            side + "_SoftUpperLid",
            [(cx - 0.039, -0.220, 1.613), (cx, -0.223, 1.636), (cx + 0.039, -0.220, 1.613)],
            0.0036,
            materials["Outline"],
        )
        add_weight(upper_lid, "Head")
        parts.append(upper_lid)
        lower_lid = add_poly_tube(
            side + "_SoftLowerLid",
            [(cx - 0.034, -0.219, 1.600), (cx, -0.220, 1.587), (cx + 0.034, -0.219, 1.600)],
            0.0018,
            materials["SkinShadow"],
        )
        add_weight(lower_lid, "Head")
        parts.append(lower_lid)
        brow = add_poly_tube(side + "_MatureBrow", [(0.025 * sign, -0.200, 1.674), (0.082 * sign, -0.207, 1.681), (0.138 * sign, -0.199, 1.671)], 0.0035, materials["Outline"])
        add_weight(brow, "Head")
        parts.append(brow)

    nose = mesh_object(
        "Nose_GraphicWedge",
        [(-0.008, -0.202, 1.555), (0.008, -0.202, 1.555), (0.0, -0.211, 1.540), (-0.010, -0.202, 1.537), (0.010, -0.202, 1.537)],
        [(0, 1, 2), (0, 2, 3), (1, 4, 2), (3, 2, 4)],
        materials["SkinShadow"],
        False,
    )
    add_weight(nose, "Head")
    parts.append(nose)
    mouth = add_poly_tube("SmallCalmSmile", [(-0.038, -0.196, 1.462), (0.0, -0.201, 1.454), (0.038, -0.196, 1.462)], 0.004, materials["Mouth"])
    add_weight(mouth, "Head")
    parts.append(mouth)

    # Thin dark-silver rounded-square glasses, sized for runtime readability.
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        lens_x = 0.082 * sign
        frame = add_poly_tube(
            side + "_GlassesRoundedSquare",
            rounded_rectangle_points(lens_x, -0.220, 1.611, 0.061, 0.050, 0.016, 5),
            0.0022,
            materials["SilverDark"],
            True,
        )
        add_weight(frame, "Head")
        parts.append(frame)
    bridge = add_poly_tube("Glasses_Bridge", [(-0.014, -0.221, 1.615), (0.0, -0.224, 1.621), (0.014, -0.221, 1.615)], 0.0020, materials["SilverDark"])
    add_weight(bridge, "Head")
    parts.append(bridge)
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        temple = add_poly_tube(side + "_GlassesTemple", [(0.143 * sign, -0.217, 1.627), (0.218 * sign, -0.105, 1.626), (0.234 * sign, -0.010, 1.607)], 0.0020, materials["SilverDark"])
        add_weight(temple, "Head")
        parts.append(temple)

    hair_cap = create_hair_cap(materials["Hair"])
    add_weight(hair_cap, "Head")
    parts.append(hair_cap)
    hair_front_underlay = add_ellipsoid(
        "Hair_FrontContinuousUnderlay",
        (0.0, -0.131, 1.842),
        (0.216, 0.058, 0.128),
        materials["Hair"],
        28,
        14,
        do_smooth=True,
    )
    add_weight(hair_front_underlay, "Head")
    parts.append(hair_front_underlay)
    # Layered S-sweep from the subject-left part toward screen-left.
    hair_ribbons = [
        ("HairSweep_OuterLeft", [(0.055, -0.075, 1.955), (-0.035, -0.125, 1.925), (-0.145, -0.176, 1.855), (-0.235, -0.205, 1.735)], [0.030, 0.042, 0.036, 0.006], "HairMid"),
        ("HairSweep_Left", [(0.082, -0.095, 1.948), (0.005, -0.145, 1.900), (-0.095, -0.188, 1.830), (-0.188, -0.210, 1.710)], [0.026, 0.038, 0.034, 0.006], "HairHighlight"),
        ("HairSweep_Main", [(0.105, -0.120, 1.928), (0.045, -0.158, 1.880), (-0.038, -0.207, 1.800), (-0.126, -0.216, 1.696)], [0.026, 0.040, 0.036, 0.005], "HairMid"),
        ("HairSweep_Center", [(0.120, -0.135, 1.910), (0.080, -0.180, 1.850), (0.018, -0.215, 1.778), (-0.055, -0.222, 1.696)], [0.022, 0.034, 0.028, 0.005], "HairHighlight"),
        ("HairPart_RightA", [(0.135, -0.112, 1.932), (0.160, -0.166, 1.865), (0.180, -0.207, 1.790), (0.196, -0.210, 1.698)], [0.022, 0.030, 0.026, 0.005], "HairMid"),
        ("HairPart_RightB", [(0.164, -0.132, 1.910), (0.198, -0.184, 1.842), (0.220, -0.205, 1.755), (0.236, -0.196, 1.670)], [0.020, 0.028, 0.024, 0.005], "HairHighlight"),
    ]
    for name, points, widths, material_name in hair_ribbons:
        ribbon = add_hair_ribbon(name, points, widths, materials[material_name])
        add_weight(ribbon, "Head")
        parts.append(ribbon)
    part_line = add_poly_tube("Hair_SidePartLine", [(0.108, -0.120, 1.925), (0.124, -0.163, 1.875), (0.140, -0.190, 1.825)], 0.0035, materials["Outline"])
    add_weight(part_line, "Head")
    parts.append(part_line)
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        gray = add_ellipsoid(side + "_TempleGrayPatch", (0.226 * sign, -0.090, 1.665), (0.014, 0.009, 0.042), materials["TempleGray"], 12, 7, rotation=(0.0, math.radians(10.0 * sign), math.radians(-8.0 * sign)), do_smooth=True)
        add_weight(gray, "Head")
        parts.append(gray)

    # Graphic collar, pocket and button placket.
    collar_band = vertical_loft("Shirt_CollarBand", [(1.315, 0.0, 0.140, 0.108), (1.375, 0.0, 0.145, 0.110)], materials["ShirtShadow"], 14, False)
    add_weight(collar_band, "UpperChest")
    parts.append(collar_band)
    left_collar = mesh_object("Left_Collar", [(-0.150, -0.176, 1.330), (-0.025, -0.202, 1.270), (-0.100, -0.195, 1.205), (-0.150, -0.155, 1.330), (-0.025, -0.175, 1.270), (-0.100, -0.170, 1.205)], [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)], materials["ShirtLight"], False)
    right_collar = mesh_object("Right_Collar", [(0.150, -0.176, 1.330), (0.025, -0.202, 1.270), (0.100, -0.195, 1.205), (0.150, -0.155, 1.330), (0.025, -0.175, 1.270), (0.100, -0.170, 1.205)], [(0, 2, 1), (5, 3, 4), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], materials["ShirtLight"], False)
    for collar in (left_collar, right_collar):
        bevel(collar, 0.004, 1)
        add_weight(collar, "UpperChest")
        parts.append(collar)
    placket = add_rounded_box("Shirt_ButtonPlacket", (0.0, -0.191, 1.075), (0.020, 0.012, 0.340), materials["ShirtShadow"], 0.003)
    add_weight(placket, "Chest")
    parts.append(placket)
    for index, z in enumerate((1.205, 1.105, 1.005, 0.925)):
        button = add_ellipsoid(f"Shirt_Button_{index}", (0.0, -0.202, z), (0.012, 0.006, 0.012), materials["SilverDark"], 10, 5, do_smooth=False)
        add_weight(button, "Chest" if z > 1.05 else "Spine")
        parts.append(button)
    # The previous tube outline sat on one flat Y plane and visibly detached
    # from the curved torso at oblique angles.  Keep the pocket as a shallow
    # filled cloth panel sampled directly from the shirt ellipse instead.
    pocket_rows = [
        (1.190, 0.310, 0.171, (0.140, 0.165, 0.190, 0.215, 0.240)),
        (1.145, 0.305, 0.168, (0.142, 0.166, 0.190, 0.214, 0.238)),
        (1.100, 0.299, 0.164, (0.145, 0.168, 0.190, 0.212, 0.235)),
        (1.055, 0.293, 0.160, (0.150, 0.170, 0.190, 0.210, 0.230)),
    ]
    pocket_vertices: list[tuple[float, float, float]] = []
    for z, radius_x, radius_y, row_xs in pocket_rows:
        for x in row_xs:
            surface_y = -radius_y * math.sqrt(max(0.0, 1.0 - (x / radius_x) ** 2))
            pocket_vertices.append((x, surface_y - 0.0005, z))
    pocket_faces: list[tuple[int, ...]] = []
    pocket_columns = len(pocket_rows[0][3])
    for row_index in range(len(pocket_rows) - 1):
        row_start = row_index * pocket_columns
        next_start = (row_index + 1) * pocket_columns
        for column in range(pocket_columns - 1):
            pocket_faces.append(
                (
                    row_start + column,
                    row_start + column + 1,
                    next_start + column + 1,
                    next_start + column,
                )
            )
    pocket = mesh_object("Shirt_LeftChestPocket_FlushPanel", pocket_vertices, pocket_faces, materials["ShirtShadow"], False)
    add_weight(pocket, "Chest")
    parts.append(pocket)

    # Rolled sleeves flow continuously out of the torso into tapered forearms;
    # the small mitten hands use enough rings to avoid faceted diamond ends.
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        shoulder = (0.245 * sign, 0.0, 1.265)
        elbow = (0.297 * sign, 0.0, 1.055)
        wrist = (0.323 * sign, 0.0, 0.875)
        hand_end = (0.327 * sign, -0.006, 0.806)
        sleeve = segment_loft(
            side + "_RolledShirtSleeve",
            [shoulder, (0.252 * sign, 0.0, 1.235), (0.264 * sign, 0.0, 1.190), (0.281 * sign, 0.0, 1.120), elbow],
            [(0.058, 0.057), (0.064, 0.062), (0.065, 0.062), (0.061, 0.058), (0.056, 0.053)],
            materials["Shirt"],
            24,
            True,
        )
        bevel(sleeve, 0.004, 1)
        add_segment_weights(sleeve, shoulder, elbow, side + "Shoulder", side + "UpperArm", 0.16, 0.10)
        parts.append(sleeve)
        cuff = segment_loft(side + "_RolledCuff", [(0.292 * sign, 0.0, 1.085), (0.304 * sign, 0.0, 1.025)], [(0.058, 0.055), (0.054, 0.051)], materials["ShirtLight"], 24, True)
        add_weight(cuff, side + "UpperArm")
        parts.append(cuff)
        forearm = segment_loft(side + "_Forearm", [elbow, (0.309 * sign, 0.0, 0.970), wrist], [(0.053, 0.050), (0.047, 0.044), (0.038, 0.035)], materials["Skin"], 22, True)
        add_segment_weights(forearm, elbow, wrist, side + "LowerArm", side + "LowerArm", 0.5, 0.0)
        parts.append(forearm)
        hand = segment_loft(
            side + "_MittenHand",
            [(0.323 * sign, 0.0, 0.875), (0.324 * sign, -0.002, 0.852), (0.325 * sign, -0.004, 0.830), hand_end, (0.328 * sign, -0.007, 0.782), (0.329 * sign, -0.008, 0.760)],
            [(0.038, 0.035), (0.037, 0.034), (0.038, 0.034), (0.039, 0.035), (0.032, 0.029), (0.016, 0.014)],
            materials["Skin"],
            24,
            True,
        )
        bevel(hand, 0.0025, 1)
        add_weight(hand, side + "Hand")
        parts.append(hand)
        thumb = segment_loft(side + "_Thumb", [(0.320 * sign, -0.028, 0.817), (0.343 * sign, -0.032, 0.795)], [(0.011, 0.010), (0.006, 0.005)], materials["Skin"], 16, True)
        add_weight(thumb, side + "Hand")
        parts.append(thumb)
        if side == "Left":
            watch_band = segment_loft("AnalogWatch_BrownBand", [(0.307, 0.0, 0.945), (0.318, 0.0, 0.900)], [(0.047, 0.044), (0.041, 0.038)], materials["Leather"], 18, True)
            add_weight(watch_band, "LeftLowerArm")
            parts.append(watch_band)
            watch_case = add_ellipsoid("AnalogWatch_SilverCase", (0.312, -0.048, 0.925), (0.024, 0.010, 0.026), materials["Silver"], 16, 10, do_smooth=True)
            add_weight(watch_case, "LeftLowerArm")
            parts.append(watch_case)
            watch_face = add_ellipsoid("AnalogWatch_Face", (0.312, -0.057, 0.925), (0.018, 0.004, 0.019), materials["WatchFace"], 14, 8, do_smooth=True)
            add_weight(watch_face, "LeftLowerArm")
            parts.append(watch_face)
            hand_mark = add_poly_tube("Watch_Hands", [(0.312, -0.063, 0.925), (0.317, -0.064, 0.934)], 0.0015, materials["SilverDark"])
            add_weight(hand_mark, "LeftLowerArm")
            parts.append(hand_mark)

    armature = create_armature()

    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = torso
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "FatherV2_Complete_SkinnedMesh"
    body["identity"] = "father"
    body["age"] = 46
    body["source_contract"] = SCRIPT_VERSION
    body["proportion_lock"] = "3.2-3.6 heads; target 3.4"
    body["single_skinned_body"] = True
    modifier = body.modifiers.new("FatherV2_Humanoid_Skin", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True
    body.parent = armature
    return body, armature


def build_atlas(output_dir: Path, body: bpy.types.Object) -> tuple[Path, dict[str, tuple[float, float]]]:
    ordered = list(PALETTE.keys())
    image = bpy.data.images.new("FatherV2IdentityAtlas", width=ATLAS_SIZE, height=ATLAS_SIZE, alpha=True)
    pixels = np.zeros((ATLAS_SIZE, ATLAS_SIZE, 4), dtype=np.float32)
    pixels[:, :, :] = np.array((0.08, 0.08, 0.09, 1.0), dtype=np.float32)
    cell = ATLAS_SIZE // ATLAS_GRID
    uv_centers: dict[str, tuple[float, float]] = {}
    for index, name in enumerate(ordered):
        col = index % ATLAS_GRID
        row = index // ATLAS_GRID
        x0 = col * cell
        y0 = row * cell
        color = np.array(PALETTE[name], dtype=np.float32)
        pixels[y0 : y0 + cell, x0 : x0 + cell, :] = color
        uv_centers[name] = ((x0 + cell * 0.5) / ATLAS_SIZE, (y0 + cell * 0.5) / ATLAS_SIZE)
    image.pixels.foreach_set(pixels.ravel())
    atlas_path = output_dir / "father-blender-identity-v2-atlas.png"
    image.filepath_raw = str(atlas_path)
    image.file_format = "PNG"
    image.save()

    material_names = [slot.material.name for slot in body.material_slots]
    while len(body.data.uv_layers) > 0:
        body.data.uv_layers.remove(body.data.uv_layers[0])
    uv_layer = body.data.uv_layers.new(name="FatherV2AtlasUV")
    for polygon in body.data.polygons:
        material_name = material_names[polygon.material_index]
        if material_name not in uv_centers:
            raise RuntimeError(f"unexpected material {material_name}")
        uv = uv_centers[material_name]
        for loop_index in polygon.loop_indices:
            uv_layer.data[loop_index].uv = uv

    material = bpy.data.materials.new("FatherV2_IdentityAtlas_Material")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output = nodes.new("ShaderNodeOutputMaterial")
    output.name = "FatherV2_MaterialOutput"
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.name = "FatherV2_ExportPrincipled"
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "FatherV2_AtlasTexture"
    texture.image = image
    texture.interpolation = "Closest"
    texture.extension = "CLIP"
    bsdf.inputs["Roughness"].default_value = 0.96
    bsdf.inputs["Specular IOR Level"].default_value = 0.06
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], bsdf.inputs["Alpha"])

    # A clean three-band diffuse response is used only for QA rendering.  The
    # export step reconnects the atlas to the Principled node so Unity receives
    # a conventional one-texture material.
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    diffuse.inputs["Roughness"].default_value = 1.0
    shader_to_rgb = nodes.new("ShaderNodeShaderToRGB")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "EASE"
    ramp.color_ramp.elements[0].position = 0.28
    ramp.color_ramp.elements[0].color = (0.56, 0.59, 0.64, 1.0)
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
    middle = ramp.color_ramp.elements.new(0.52)
    middle.color = (0.82, 0.84, 0.88, 1.0)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 0.92
    links.new(diffuse.outputs["BSDF"], shader_to_rgb.inputs["Shader"])
    links.new(shader_to_rgb.outputs["Color"], ramp.inputs["Fac"])
    links.new(texture.outputs["Color"], multiply.inputs[1])
    links.new(ramp.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    body.data.materials.clear()
    body.data.materials.append(material)
    for polygon in body.data.polygons:
        polygon.material_index = 0
    return atlas_path, uv_centers


def configure_atlas_material_for_export(body: bpy.types.Object) -> None:
    material = body.data.materials[0]
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    output = nodes.get("FatherV2_MaterialOutput")
    bsdf = nodes.get("FatherV2_ExportPrincipled")
    if output is None or bsdf is None:
        raise RuntimeError("atlas export nodes missing")
    for link in list(output.inputs["Surface"].links):
        links.remove(link)
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])


def add_render_stage() -> bpy.types.Object:
    floor_material = bpy.data.materials.new("FatherV2_QA_FloorMaterial")
    floor_material.use_nodes = True
    floor_bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = (0.670, 0.690, 0.725, 1.0)
    floor_bsdf.inputs["Roughness"].default_value = 1.0
    floor_bsdf.inputs["Specular IOR Level"].default_value = 0.0
    floor = add_rounded_box("FatherV2_QA_Floor", (0.0, 0.0, -0.025), (3.4, 3.4, 0.05), floor_material, 0.025)

    bpy.ops.object.light_add(type="AREA", location=(-3.0, -4.0, 5.0))
    key = bpy.context.object
    key.name = "FatherV2_QA_Key"
    key.data.energy = 780
    key.data.shape = "DISK"
    key.data.size = 4.0
    key.rotation_euler = (math.radians(25), 0.0, math.radians(-35))
    bpy.ops.object.light_add(type="AREA", location=(3.0, -2.0, 3.2))
    fill = bpy.context.object
    fill.name = "FatherV2_QA_Fill"
    fill.data.energy = 500
    fill.data.size = 4.0
    fill.rotation_euler = (math.radians(60), 0.0, math.radians(120))
    bpy.ops.object.light_add(type="AREA", location=(0.0, 3.0, 4.0))
    rim = bpy.context.object
    rim.name = "FatherV2_QA_Rim"
    rim.data.energy = 600
    rim.data.size = 3.0
    rim.rotation_euler = (math.radians(-45), 0.0, math.radians(180))

    bpy.ops.object.camera_add(location=(0.0, -5.5, 1.0))
    camera = bpy.context.object
    camera.name = "FatherV2_QA_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.17
    bpy.context.scene.camera = camera
    return camera


def point_camera(camera: bpy.types.Object, position: tuple[float, float, float]) -> None:
    camera.location = position
    target = Vector((0.0, 0.0, 0.95))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_view(camera: bpy.types.Object, path: Path, position: tuple[float, float, float], resolution: int) -> None:
    scene = bpy.context.scene
    point_camera(camera, position)
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def reset_pose(armature: bpy.types.Object) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)


def set_walk_pose(armature: bpy.types.Object, phase: int) -> None:
    reset_pose(armature)
    sign = 1.0 if phase == 0 else -1.0
    radians = math.radians
    armature.pose.bones["LeftUpperLeg"].rotation_euler.x = radians(-22.0 * sign)
    armature.pose.bones["RightUpperLeg"].rotation_euler.x = radians(22.0 * sign)
    armature.pose.bones["LeftLowerLeg"].rotation_euler.x = radians(12.0 * sign + 10.0)
    armature.pose.bones["RightLowerLeg"].rotation_euler.x = radians(-12.0 * sign + 10.0)
    armature.pose.bones["LeftFoot"].rotation_euler.x = radians(8.0 * sign)
    armature.pose.bones["RightFoot"].rotation_euler.x = radians(-8.0 * sign)
    armature.pose.bones["LeftUpperArm"].rotation_euler.x = radians(15.0 * sign)
    armature.pose.bones["RightUpperArm"].rotation_euler.x = radians(-15.0 * sign)
    armature.pose.bones["LeftLowerArm"].rotation_euler.x = radians(-6.0 * sign)
    armature.pose.bones["RightLowerArm"].rotation_euler.x = radians(6.0 * sign)
    armature.pose.bones["Hips"].rotation_euler.z = radians(-2.0 * sign)
    armature.pose.bones["Chest"].rotation_euler.z = radians(1.5 * sign)


def render_all(output_dir: Path, camera: bpy.types.Object, armature: bpy.types.Object, stage: str) -> list[Path]:
    outputs: list[Path] = []
    views_dir = output_dir / ("draft-views" if stage == "draft" else "final-views")
    views_dir.mkdir(parents=True, exist_ok=True)
    resolution = 768 if stage == "draft" else 1536
    views = {
        "front": (0.0, -5.5, 1.0),
        "left": (-5.5, 0.0, 1.0),
        "back": (0.0, 5.5, 1.0),
        "three-quarter": (-3.90, -3.90, 1.02),
    }
    reset_pose(armature)
    for name, position in views.items():
        path = views_dir / f"father-v2-{name}-{resolution}.png"
        render_view(camera, path, position, resolution)
        outputs.append(path)
    if stage == "final":
        turntable_dir = output_dir / "turntable-24"
        turntable_dir.mkdir(parents=True, exist_ok=True)
        radius = 5.5
        for index in range(24):
            angle = math.radians(index * 15.0)
            position = (radius * math.sin(angle), -radius * math.cos(angle), 1.0)
            path = turntable_dir / f"father-v2-turn-{index:02d}-{index * 15:03d}deg.png"
            render_view(camera, path, position, 768)
            outputs.append(path)
        deformation_dir = output_dir / "deformation"
        deformation_dir.mkdir(parents=True, exist_ok=True)
        for phase in (0, 3):
            set_walk_pose(armature, phase)
            path = deformation_dir / f"father-v2-P{phase}-deformation-1536.png"
            render_view(camera, path, (-3.90, -3.90, 1.02), 1536)
            outputs.append(path)
        reset_pose(armature)
    return outputs


REQUIRED_BONES = {
    "Root", "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
    "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
    "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
    "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
    "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
}


def validate_character(body: bpy.types.Object, armature: bpy.types.Object) -> dict[str, object]:
    missing = sorted(REQUIRED_BONES - set(armature.data.bones.keys()))
    if missing:
        raise RuntimeError(f"missing Humanoid bones: {missing}")
    if len(body.data.materials) != 1:
        raise RuntimeError("candidate must use one atlas material")
    if len(body.data.uv_layers) != 1 or body.data.uv_layers.get("FatherV2AtlasUV") is None:
        raise RuntimeError("candidate must have FatherV2AtlasUV as sole UV0")
    if body.modifiers.get("FatherV2_Humanoid_Skin") is None:
        raise RuntimeError("armature modifier missing")
    if Vector(armature.data.bones["Root"].head_local).length > 1e-6:
        raise RuntimeError("Root is not bottom-centre")
    groups = set(group.name for group in body.vertex_groups)
    missing_groups = sorted((REQUIRED_BONES - {"Root"}) - groups)
    if missing_groups:
        raise RuntimeError(f"missing vertex groups: {missing_groups}")
    min_z = min(vertex.co.z for vertex in body.data.vertices)
    max_z = max(vertex.co.z for vertex in body.data.vertices)
    return {
        "mesh_objects": 1,
        "armatures": 1,
        "materials": len(body.data.materials),
        "vertices": len(body.data.vertices),
        "edges": len(body.data.edges),
        "polygons": len(body.data.polygons),
        "uv_layers": len(body.data.uv_layers),
        "active_uv0": body.data.uv_layers.active.name,
        "vertex_groups": len(body.vertex_groups),
        "bones_total": len(armature.data.bones),
        "deform_bones": sum(1 for bone in armature.data.bones if bone.use_deform),
        "required_bones_missing": missing,
        "root_head": list(armature.data.bones["Root"].head_local),
        "bound_min_z": min_z,
        "bound_max_z": max_z,
        "body_height": max_z - min_z,
        "head_height_lock": 0.60,
        "computed_heads_tall": (max_z - min_z) / 0.60,
        "shoulder_width_lock": 0.62,
        "shoulder_head_width_ratio": 0.62 / 0.49,
    }


def export_final(output_dir: Path, body: bpy.types.Object, armature: bpy.types.Object) -> tuple[Path, Path]:
    reset_pose(armature)
    configure_atlas_material_for_export(body)
    blend_path = output_dir / "father-blender-identity-v2.blend"
    fbx_path = output_dir / "father-blender-humanoid-v2.fbx"
    for obj in list(bpy.context.scene.objects):
        if obj not in (body, armature):
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
    )
    return blend_path, fbx_path


def verify_fbx_roundtrip(fbx_path: Path) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1 or len(armatures) != 1:
        raise RuntimeError(f"FBX round-trip expected 1 mesh/1 armature, got {len(meshes)}/{len(armatures)}")
    mesh = meshes[0]
    armature = armatures[0]
    missing = sorted(REQUIRED_BONES - set(armature.data.bones.keys()))
    if missing:
        raise RuntimeError(f"FBX round-trip missing bones: {missing}")
    if len(mesh.data.uv_layers) != 1:
        raise RuntimeError(f"FBX round-trip expected one UV layer, got {len(mesh.data.uv_layers)}")
    return {
        "status": "PASS",
        "mesh_objects": len(meshes),
        "armatures": len(armatures),
        "vertices": len(mesh.data.vertices),
        "polygons": len(mesh.data.polygons),
        "materials": len(mesh.data.materials),
        "uv_layers": len(mesh.data.uv_layers),
        "uv0": mesh.data.uv_layers.active.name,
        "bones": len(armature.data.bones),
        "required_bones_missing": missing,
    }


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output).resolve()
    neutral_path = Path(args.neutral).resolve()
    runtime_dir = Path(args.runtime_dir).resolve()
    if not neutral_path.is_file():
        raise FileNotFoundError(neutral_path)
    if not runtime_dir.is_dir():
        raise FileNotFoundError(runtime_dir)
    if "Artifacts" not in output_dir.parts or "Family3DBlenderFatherV2" not in output_dir.parts:
        raise RuntimeError("output must remain under Artifacts/Family3DBlenderFatherV2")
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_sha, frame_rows = runtime_manifest(runtime_dir)

    clean_scene()
    materials = {name: make_material(name, color) for name, color in PALETTE.items()}
    body, armature = build_character(materials)
    atlas_path, uv_centers = build_atlas(output_dir, body)
    metrics = validate_character(body, armature)
    camera = add_render_stage()
    rendered = render_all(output_dir, camera, armature, args.stage)

    fbx_path: Path | None = None
    roundtrip: dict[str, object] | None = None
    if args.stage == "final":
        blend_path, fbx_path = export_final(output_dir, body, armature)
        roundtrip = verify_fbx_roundtrip(fbx_path)
    else:
        blend_path = output_dir / "father-blender-identity-v2-draft.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    output_files = [atlas_path, blend_path, *rendered]
    if fbx_path is not None:
        output_files.append(fbx_path)
    output_hashes = {
        str(path.relative_to(output_dir)).replace("\\", "/"): sha256(path)
        for path in output_files
    }
    receipt = {
        "contract": SCRIPT_VERSION,
        "status": "CANDIDATE_NOT_PRODUCTION",
        "productionEligible": False,
        "stage": args.stage,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "reference_policy": {
            "identity_priority": ["runtime HighMotion Frames", "father_office_neutral_v1.png"],
            "neutral": {"path": str(neutral_path), "sha256": sha256(neutral_path)},
            "runtime_frames": {"path": str(runtime_dir), "count": len(frame_rows), "manifest_sha256": manifest_sha, "files": frame_rows},
            "family_identity_turnaround_v1_used": False,
            "pixels_loaded_into_blender": False,
            "mesh_texture_decal_billboard_motion_donor_used": False,
        },
        "identity_lock": {
            "role": "father",
            "age": 46,
            "style": "2D anime-toon translated to complete 3D geometry",
            "proportion": "3.2-3.6 heads tall, target 3.4; softly tapered adult shoulders and readable limbs",
            "face": "mature soft rounded face with short lower plane; no beard or moustache",
            "hair": "short charcoal side-part S-sweep, layered crown, light gray only at temples",
            "glasses": "thin dark-silver rounded rectangular",
            "outfit": "muted teal rolled-sleeve shirt, charcoal slacks, brown belt and compact lace-up dress shoes, left analog watch",
            "render": "flat clean palette, near-zero specular, no micrograin or realistic doll/clay treatment",
        },
        "provenance": {
            "geometry": "new procedural/manual-style V2 topology authored from an empty scene",
            "texture": "new single flat-color V2 atlas authored by this script",
            "rig": "new Unity-Humanoid-compatible armature authored by this script",
            "father_v1_mesh_atlas_texture_used": False,
            "external_mesh_or_texture_used": False,
        },
        "metrics": metrics,
        "atlas": {"path": atlas_path.name, "size": [ATLAS_SIZE, ATLAS_SIZE], "grid": [ATLAS_GRID, ATLAS_GRID], "swatches": uv_centers, "material_count": 1},
        "qa": {
            "four_view_resolution": 768 if args.stage == "draft" else 1536,
            "four_views": ["front", "left", "back", "three-quarter"],
            "turntable_views": 0 if args.stage == "draft" else 24,
            "deformation_views": [] if args.stage == "draft" else ["P0", "P3"],
            "fbx_roundtrip": roundtrip,
            "production_promoted": False,
        },
        "outputs_sha256": output_hashes,
    }
    receipt_path = output_dir / "father-blender-identity-v2-receipt.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")

    if args.stage == "final" and args.candidate_dir:
        candidate_dir = Path(args.candidate_dir).resolve()
        if "Assets" not in candidate_dir.parts or "Candidates" not in candidate_dir.parts or "FatherV2" not in candidate_dir.parts:
            raise RuntimeError("candidate-dir must be isolated under Assets/.../Candidates/FatherV2")
        candidate_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(atlas_path, candidate_dir / atlas_path.name)
        shutil.copy2(fbx_path, candidate_dir / fbx_path.name)

    print(
        f"FAMILY_COMPANY_FATHER_BLENDER_IDENTITY_V2: PASS | stage={args.stage} "
        f"vertices={metrics['vertices']} polygons={metrics['polygons']} bones={metrics['bones_total']} "
        f"heads={metrics['computed_heads_tall']:.3f} rendered={len(rendered)}"
    )


if __name__ == "__main__":
    main()
