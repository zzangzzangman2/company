"""Build the Family Company Father V1 3D identity candidate.

This file intentionally starts from procedural geometry.  It does not import any
legacy family mesh, sprite, texture, rig, or rejected Blender diagnostic.  The
locked father turnaround is used only as a human visual reference and receipt
input.

Blender 5.2 LTS invocation:

    blender.exe --background --python Tools/Blender/build_father_blender_identity_v1.py -- \
      --output Artifacts/Family3DBlenderFatherV1/Final \
      --reference Assets/FamilyCompany/Experimental/Family3DPrototype/References/FamilyIdentityTurnaroundsV1/father-3d-identity-turnaround-v1.png \
      --stage final
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


SCRIPT_VERSION = "FC-FATHER-BLENDER-IDENTITY-V1"
ATLAS_SIZE = 1024
ATLAS_GRID = 8


PALETTE = {
    "Skin": (0.74, 0.43, 0.27, 1.0),
    "SkinLight": (0.90, 0.63, 0.45, 1.0),
    "SkinShadow": (0.54, 0.28, 0.18, 1.0),
    "EyeWhite": (0.93, 0.92, 0.87, 1.0),
    "EyeBrown": (0.20, 0.095, 0.045, 1.0),
    "Pupil": (0.025, 0.018, 0.015, 1.0),
    "Brow": (0.075, 0.060, 0.052, 1.0),
    "Hair": (0.055, 0.050, 0.048, 1.0),
    "HairMid": (0.105, 0.092, 0.083, 1.0),
    "HairHighlight": (0.095, 0.085, 0.078, 1.0),
    "TempleGray": (0.22, 0.22, 0.21, 1.0),
    "Shirt": (0.16, 0.35, 0.39, 1.0),
    "ShirtLight": (0.22, 0.43, 0.47, 1.0),
    "ShirtDark": (0.075, 0.22, 0.25, 1.0),
    "Trouser": (0.12, 0.125, 0.14, 1.0),
    "TrouserLight": (0.20, 0.205, 0.22, 1.0),
    "TrouserDark": (0.055, 0.058, 0.067, 1.0),
    "Leather": (0.20, 0.095, 0.044, 1.0),
    "LeatherLight": (0.28, 0.125, 0.052, 1.0),
    "LeatherDark": (0.085, 0.040, 0.020, 1.0),
    "Metal": (0.53, 0.56, 0.58, 1.0),
    "MetalDark": (0.21, 0.23, 0.24, 1.0),
    "WatchFace": (0.82, 0.79, 0.68, 1.0),
    "Mouth": (0.28, 0.105, 0.075, 1.0),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--stage", choices=("draft", "final"), default="final")
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def ensure_clean_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    scene.render.image_settings.color_depth = "8"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.fps = 30
    scene.world = bpy.data.worlds.new("Father_QA_World")
    scene.world.color = (0.16, 0.18, 0.21)
    scene.world.use_nodes = True
    world_background = scene.world.node_tree.nodes.get("Background")
    world_background.inputs["Color"].default_value = (0.16, 0.18, 0.21, 1.0)
    world_background.inputs["Strength"].default_value = 0.52
    scene.view_settings.look = "AgX - Medium High Contrast"


def make_preview_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.58
    bsdf.inputs["Specular IOR Level"].default_value = 0.27
    if name.startswith("Metal"):
        bsdf.inputs["Metallic"].default_value = 0.72
        bsdf.inputs["Roughness"].default_value = 0.28
    elif name in ("EyeBrown", "Pupil"):
        bsdf.inputs["Roughness"].default_value = 0.22
    return material


def apply_all_transforms(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.append(material)


def smooth(obj: bpy.types.Object, angle: float = 0.75) -> None:
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=angle)


def bevel(obj: bpy.types.Object, width: float, segments: int = 2) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("EdgeSoftening", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(26.0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def add_uv_ellipsoid(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    segments: int = 28,
    rings: int = 18,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
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
    apply_all_transforms(obj)
    assign_material(obj, material)
    smooth(obj)
    return obj


def add_rounded_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    bevel_width: float,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    apply_all_transforms(obj)
    bevel(obj, bevel_width, 3)
    assign_material(obj, material)
    smooth(obj, 0.55)
    return obj


def mesh_object(
    name: str,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material: bpy.types.Material,
    do_smooth: bool = True,
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


def create_vertical_loft(
    name: str,
    rings: list[tuple[float, float, float, float]],
    material: bpy.types.Material,
    segments: int = 28,
) -> bpy.types.Object:
    """rings are (z, center_y, radius_x, radius_y)."""
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for z, cy, rx, ry in rings:
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            verts.append((rx * math.cos(angle), cy + ry * math.sin(angle), z))
    for ring in range(len(rings) - 1):
        offset = ring * segments
        next_offset = (ring + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((offset + index, offset + nxt, next_offset + nxt, next_offset + index))
    bottom = len(verts)
    verts.append((0.0, rings[0][1], rings[0][0]))
    top = len(verts)
    verts.append((0.0, rings[-1][1], rings[-1][0]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((bottom, nxt, index))
        top_offset = (len(rings) - 1) * segments
        faces.append((top, top_offset + index, top_offset + nxt))
    return mesh_object(name, verts, faces, material)


def create_segment_loft(
    name: str,
    centers: list[tuple[float, float, float]],
    radii: list[tuple[float, float]],
    material: bpy.types.Material,
    segments: int = 20,
) -> bpy.types.Object:
    if len(centers) != len(radii):
        raise ValueError("centers/radii mismatch")
    pts = [Vector(item) for item in centers]
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    depth = Vector((0.0, 1.0, 0.0))
    for ring, point in enumerate(pts):
        if ring == 0:
            tangent = (pts[1] - pts[0]).normalized()
        elif ring == len(pts) - 1:
            tangent = (pts[-1] - pts[-2]).normalized()
        else:
            tangent = (pts[ring + 1] - pts[ring - 1]).normalized()
        side = depth.cross(tangent)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        local_depth = tangent.cross(side).normalized()
        rx, ry = radii[ring]
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            vertex = point + side * (math.cos(angle) * rx) + local_depth * (math.sin(angle) * ry)
            verts.append(tuple(vertex))
    for ring in range(len(pts) - 1):
        offset = ring * segments
        next_offset = (ring + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((offset + index, offset + nxt, next_offset + nxt, next_offset + index))
    start = len(verts)
    verts.append(tuple(pts[0]))
    end = len(verts)
    verts.append(tuple(pts[-1]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((start, nxt, index))
        offset = (len(pts) - 1) * segments
        faces.append((end, offset + index, offset + nxt))
    return mesh_object(name, verts, faces, material)


def create_forward_shoe_loft(
    name: str,
    x_center: float,
    sections: list[tuple[float, float, float, float]],
    material: bpy.types.Material,
    segments: int = 24,
) -> bpy.types.Object:
    """Create a fixed-frame X/Z shoe loft along Y without cross-section twist.

    Each section is (y, z, half_width_x, half_height_z).  Keeping the section
    frame fixed is important here: the generic limb loft follows its tangent,
    while an Oxford shoe must retain a level sole and symmetric vamp.
    """
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for y, z, radius_x, radius_z in sections:
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            verts.append(
                (
                    x_center + math.cos(angle) * radius_x,
                    y,
                    z + math.sin(angle) * radius_z,
                )
            )
    for section in range(len(sections) - 1):
        a = section * segments
        b = (section + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, a + nxt, b + nxt, b + index))
    rear = len(verts)
    verts.append((x_center, sections[0][0], sections[0][1]))
    front = len(verts)
    verts.append((x_center, sections[-1][0], sections[-1][1]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((rear, index, nxt))
        offset = (len(sections) - 1) * segments
        faces.append((front, offset + nxt, offset + index))
    return mesh_object(name, verts, faces, material)


def add_tube(
    name: str,
    points: list[tuple[float, float, float]],
    radius: float,
    material: bpy.types.Material,
    resolution: int = 1,
    bevel_resolution: int = 2,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name + "Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_resolution
    curve.resolution_u = 2
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bp, co in zip(spline.bezier_points, points):
        bp.co = co
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    assign_material(obj, material)
    smooth(obj)
    obj.select_set(False)
    return obj


def add_tri_prism(
    name: str,
    points_xz: list[tuple[float, float]],
    front_y: float,
    back_y: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    verts = [(x, front_y, z) for x, z in points_xz] + [(x, back_y, z) for x, z in points_xz]
    faces = [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
    obj = mesh_object(name, verts, faces, material, do_smooth=False)
    bevel(obj, 0.008, 2)
    return obj


def create_hair_cap(material: bpy.types.Material) -> bpy.types.Object:
    segments = 36
    rings = 12
    center = Vector((0.0, 0.015, 2.045))
    rx, ry, rz = 0.305, 0.275, 0.335
    verts: list[tuple[float, float, float]] = [tuple(center + Vector((0.0, 0.0, rz)))]
    faces: list[tuple[int, ...]] = []
    for ring in range(1, rings + 1):
        t = ring / rings
        for index in range(segments):
            phi = 2.0 * math.pi * index / segments
            theta_max = 1.70 + 0.45 * math.sin(phi)
            theta = theta_max * t
            point = center + Vector(
                (
                    rx * math.sin(theta) * math.cos(phi),
                    ry * math.sin(theta) * math.sin(phi),
                    rz * math.cos(theta),
                )
            )
            verts.append(tuple(point))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((0, 1 + index, 1 + nxt))
    for ring in range(rings - 1):
        a = 1 + ring * segments
        b = 1 + (ring + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, b + index, b + nxt, a + nxt))
    return mesh_object("Hair_ScalpCap", verts, faces, material)


def create_hair_lock(
    name: str,
    points: list[tuple[float, float, float]],
    widths: list[float],
    material: bpy.types.Material,
    thickness: float = 0.014,
) -> bpy.types.Object:
    pts = [Vector(item) for item in points]
    center = Vector((0.0, 0.015, 2.045))
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for index, (point, width) in enumerate(zip(pts, widths)):
        if index == 0:
            tangent = (pts[1] - pts[0]).normalized()
        elif index == len(pts) - 1:
            tangent = (pts[-1] - pts[-2]).normalized()
        else:
            tangent = (pts[index + 1] - pts[index - 1]).normalized()
        normal = (point - center).normalized()
        side = tangent.cross(normal)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        outward = normal * (thickness * 0.5)
        lateral = side * (width * 0.5)
        verts.extend(
            [
                tuple(point - lateral - outward),
                tuple(point + lateral - outward),
                tuple(point + lateral + outward),
                tuple(point - lateral + outward),
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
    obj = mesh_object(name, verts, faces, material, do_smooth=False)
    bevel(obj, 0.004, 2)
    return obj


def add_weight(obj: bpy.types.Object, bone: str, weight: float = 1.0) -> None:
    group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), weight, "REPLACE")


def add_blended_weights_by_z(
    obj: bpy.types.Object,
    controls: list[tuple[float, str]],
) -> None:
    groups = {name: obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name) for _, name in controls}
    controls = sorted(controls)
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
    b = Vector(end)
    axis = b - a
    denom = axis.length_squared
    first = obj.vertex_groups.get(first_bone) or obj.vertex_groups.new(name=first_bone)
    second = obj.vertex_groups.get(second_bone) or obj.vertex_groups.new(name=second_bone)
    lo = split - blend * 0.5
    hi = split + blend * 0.5
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        t = max(0.0, min(1.0, (point - a).dot(axis) / denom))
        if t <= lo:
            first.add([vertex.index], 1.0, "REPLACE")
        elif t >= hi:
            second.add([vertex.index], 1.0, "REPLACE")
        else:
            factor = (t - lo) / max(hi - lo, 1e-6)
            first.add([vertex.index], 1.0 - factor, "REPLACE")
            second.add([vertex.index], factor, "REPLACE")


def create_armature() -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("Father_Humanoid_ArmatureData")
    armature = bpy.data.objects.new("Father_Humanoid_Armature", armature_data)
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
        item = armature_data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.use_deform = deform
        if parent:
            item.parent = armature_data.edit_bones[parent]
            item.use_connect = connected

    bone("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.18), deform=False)
    bone("Hips", (0.0, 0.0, 0.91), (0.0, 0.0, 1.10), "Root")
    bone("Spine", (0.0, 0.0, 1.10), (0.0, 0.0, 1.34), "Hips", True)
    bone("Chest", (0.0, 0.0, 1.34), (0.0, 0.0, 1.54), "Spine", True)
    bone("UpperChest", (0.0, 0.0, 1.54), (0.0, 0.0, 1.70), "Chest", True)
    bone("Neck", (0.0, 0.0, 1.70), (0.0, 0.0, 1.86), "UpperChest", True)
    bone("Head", (0.0, 0.0, 1.86), (0.0, 0.0, 2.23), "Neck", True)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        bone(
            side + "Shoulder",
            (0.13 * sign, 0.0, 1.66),
            (0.49 * sign, 0.0, 1.62),
            "UpperChest",
        )
        bone(
            side + "UpperArm",
            (0.49 * sign, 0.0, 1.62),
            (0.73 * sign, 0.0, 1.39),
            side + "Shoulder",
            True,
        )
        bone(
            side + "LowerArm",
            (0.73 * sign, 0.0, 1.39),
            (0.91 * sign, 0.0, 1.14),
            side + "UpperArm",
            True,
        )
        bone(
            side + "Hand",
            (0.91 * sign, 0.0, 1.14),
            (0.97 * sign, -0.005, 0.98),
            side + "LowerArm",
            True,
        )
        bone(
            side + "UpperLeg",
            (0.19 * sign, 0.0, 0.98),
            (0.19 * sign, 0.0, 0.57),
            "Hips",
        )
        bone(
            side + "LowerLeg",
            (0.19 * sign, 0.0, 0.57),
            (0.19 * sign, 0.0, 0.17),
            side + "UpperLeg",
            True,
        )
        bone(
            side + "Foot",
            (0.19 * sign, 0.0, 0.17),
            (0.19 * sign, -0.17, 0.085),
            side + "LowerLeg",
            True,
        )
        bone(
            side + "Toes",
            (0.19 * sign, -0.17, 0.085),
            (0.19 * sign, -0.31, 0.065),
            side + "Foot",
            True,
        )

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "STICK"
    armature.select_set(False)
    return armature


def build_character(materials: dict[str, bpy.types.Material]) -> tuple[bpy.types.Object, bpy.types.Object]:
    parts: list[bpy.types.Object] = []

    torso = create_vertical_loft(
        "Shirt_Torso",
        [
            (0.99, 0.0, 0.335, 0.205),
            (1.08, 0.0, 0.355, 0.217),
            (1.18, 0.0, 0.385, 0.230),
            (1.31, 0.0, 0.425, 0.245),
            (1.44, 0.0, 0.465, 0.258),
            (1.56, 0.0, 0.495, 0.268),
            (1.64, 0.0, 0.505, 0.272),
            (1.70, -0.004, 0.440, 0.248),
            (1.735, -0.006, 0.355, 0.210),
        ],
        materials["Shirt"],
        32,
    )
    bevel(torso, 0.018, 2)
    add_blended_weights_by_z(
        torso,
        [(0.98, "Hips"), (1.19, "Spine"), (1.45, "Chest"), (1.70, "UpperChest")],
    )
    parts.append(torso)

    pants_pelvis = create_vertical_loft(
        "Trouser_Pelvis",
        [
            (0.82, 0.01, 0.33, 0.20),
            (0.92, 0.0, 0.37, 0.225),
            (1.04, 0.0, 0.375, 0.225),
            (1.075, 0.0, 0.36, 0.215),
        ],
        materials["Trouser"],
        28,
    )
    bevel(pants_pelvis, 0.012, 2)
    add_weight(pants_pelvis, "Hips")
    parts.append(pants_pelvis)

    belt = create_vertical_loft(
        "Brown_Leather_Belt",
        [(1.035, 0.0, 0.378, 0.228), (1.085, 0.0, 0.38, 0.229)],
        materials["Leather"],
        32,
    )
    add_weight(belt, "Hips")
    parts.append(belt)
    buckle = add_rounded_box(
        "Belt_Silver_Buckle",
        (0.0, -0.234, 1.06),
        (0.095, 0.025, 0.068),
        materials["Metal"],
        0.012,
    )
    add_weight(buckle, "Hips")
    parts.append(buckle)
    buckle_inner = add_rounded_box(
        "Belt_Buckle_Inner",
        (0.0, -0.251, 1.06),
        (0.058, 0.012, 0.034),
        materials["LeatherDark"],
        0.008,
    )
    add_weight(buckle_inner, "Hips")
    parts.append(buckle_inner)

    for x in (-0.25, -0.10, 0.10, 0.25):
        loop = add_rounded_box(
            "Belt_Loop",
            (x, -0.219, 1.062),
            (0.027, 0.018, 0.085),
            materials["TrouserLight"],
            0.006,
        )
        add_weight(loop, "Hips")
        parts.append(loop)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        x = 0.19 * sign
        leg = create_segment_loft(
            side + "_TrouserLeg",
            [(x, 0.0, 0.96), (x, 0.004, 0.76), (x, 0.008, 0.57), (x, 0.008, 0.35), (x, 0.002, 0.16)],
            [(0.175, 0.158), (0.158, 0.144), (0.145, 0.132), (0.132, 0.122), (0.12, 0.108)],
            materials["Trouser"],
            24,
        )
        bevel(leg, 0.008, 2)
        add_segment_weights(
            leg,
            (x, 0.0, 0.98),
            (x, 0.0, 0.17),
            side + "UpperLeg",
            side + "LowerLeg",
            split=0.51,
            blend=0.13,
        )
        parts.append(leg)
        crease = add_tube(
            side + "_TrouserFrontCrease",
            [(x, -0.137, 0.91), (x, -0.124, 0.62), (x, -0.11, 0.22)],
            0.007,
            materials["TrouserLight"],
            bevel_resolution=1,
        )
        add_segment_weights(
            crease,
            (x, 0.0, 0.98),
            (x, 0.0, 0.17),
            side + "UpperLeg",
            side + "LowerLeg",
            split=0.51,
            blend=0.10,
        )
        parts.append(crease)

        sole = add_rounded_box(
            side + "_ShoeSole",
            (x, -0.065, 0.0275),
            (0.31, 0.46, 0.055),
            materials["LeatherDark"],
            0.035,
        )
        add_segment_weights(
            sole,
            (x, 0.15, 0.08),
            (x, -0.31, 0.08),
            side + "Foot",
            side + "Toes",
            split=0.72,
            blend=0.12,
        )
        parts.append(sole)
        shoe = create_forward_shoe_loft(
            side + "_OxfordUpper",
            x,
            [
                (0.12, 0.115, 0.132, 0.092),
                (0.01, 0.132, 0.145, 0.100),
                (-0.13, 0.118, 0.150, 0.086),
                (-0.245, 0.090, 0.115, 0.055),
            ],
            materials["Leather"],
            24,
        )
        bevel(shoe, 0.008, 2)
        add_segment_weights(
            shoe,
            (x, 0.15, 0.12),
            (x, -0.31, 0.12),
            side + "Foot",
            side + "Toes",
            split=0.72,
            blend=0.12,
        )
        parts.append(shoe)
        toe_seam = add_tube(
            side + "_ToeCapSeam",
            [
                (x - 0.125, -0.205, 0.145),
                (x, -0.235, 0.17),
                (x + 0.125, -0.205, 0.145),
            ],
            0.008,
            materials["LeatherDark"],
            bevel_resolution=1,
        )
        add_weight(toe_seam, side + "Foot")
        parts.append(toe_seam)
        for lace_index in range(3):
            y = -0.055 - lace_index * 0.042
            lace = add_tube(
                side + "_ShoeLace",
                [(x - 0.065, y, 0.205 - lace_index * 0.012), (x + 0.065, y, 0.205 - lace_index * 0.012)],
                0.006,
                materials["LeatherDark"],
                bevel_resolution=1,
            )
            add_weight(lace, side + "Foot")
            parts.append(lace)

    neck = create_vertical_loft(
        "Neck_Skin",
        [(1.67, 0.0, 0.125, 0.115), (1.86, -0.005, 0.135, 0.12)],
        materials["Skin"],
        24,
    )
    bevel(neck, 0.007, 2)
    add_blended_weights_by_z(neck, [(1.67, "UpperChest"), (1.86, "Neck")])
    parts.append(neck)
    collar_band = create_vertical_loft(
        "Shirt_Collar_Band",
        [(1.665, -0.002, 0.166, 0.138), (1.735, -0.006, 0.172, 0.142)],
        materials["ShirtDark"],
        24,
    )
    bevel(collar_band, 0.005, 2)
    add_weight(collar_band, "UpperChest")
    parts.append(collar_band)

    head = create_vertical_loft(
        "Head_Integrated",
        [
            (1.76, -0.010, 0.065, 0.060),
            (1.80, -0.015, 0.135, 0.115),
            (1.84, -0.022, 0.195, 0.165),
            (1.89, -0.028, 0.232, 0.202),
            (1.96, -0.026, 0.254, 0.226),
            (2.04, -0.020, 0.266, 0.238),
            (2.12, -0.010, 0.264, 0.245),
            (2.19, -0.002, 0.246, 0.236),
            (2.25, 0.003, 0.214, 0.212),
            (2.30, 0.007, 0.160, 0.168),
            (2.34, 0.010, 0.070, 0.080),
        ],
        materials["Skin"],
        36,
    )
    bevel(head, 0.007, 2)
    add_weight(head, "Head")
    parts.append(head)

    for sign in (-1.0, 1.0):
        ear = add_uv_ellipsoid(
            "Ear",
            (0.283 * sign, 0.003, 2.015),
            (0.040, 0.058, 0.073),
            materials["Skin"],
            20,
            12,
        )
        add_weight(ear, "Head")
        parts.append(ear)
        ear_inner = add_uv_ellipsoid(
            "EarInner",
            (0.305 * sign, -0.012, 2.015),
            (0.017, 0.035, 0.043),
            materials["SkinShadow"],
            16,
            10,
        )
        add_weight(ear_inner, "Head")
        parts.append(ear_inner)

    for sign in (-1.0, 1.0):
        eye = add_uv_ellipsoid(
            "EyeWhite",
            (0.095 * sign, -0.247, 2.055),
            (0.043, 0.013, 0.019),
            materials["EyeWhite"],
            24,
            14,
        )
        add_weight(eye, "Head")
        parts.append(eye)
        iris = add_uv_ellipsoid(
            "IrisBrown",
            (0.095 * sign, -0.262, 2.053),
            (0.012, 0.005, 0.012),
            materials["EyeBrown"],
            20,
            12,
        )
        add_weight(iris, "Head")
        parts.append(iris)
        pupil = add_uv_ellipsoid(
            "Pupil",
            (0.095 * sign, -0.268, 2.053),
            (0.0045, 0.003, 0.006),
            materials["Pupil"],
            16,
            10,
        )
        add_weight(pupil, "Head")
        parts.append(pupil)
        eyebrow = add_tube(
            "Eyebrow",
            [
                (0.145 * sign, -0.253, 2.112),
                (0.095 * sign, -0.264, 2.122),
                (0.045 * sign, -0.257, 2.115),
            ],
            0.007,
            materials["Brow"],
            bevel_resolution=2,
        )
        add_weight(eyebrow, "Head")
        parts.append(eyebrow)
        upper_lid = add_tube(
            "UpperEyelid",
            [
                (0.137 * sign, -0.264, 2.061),
                (0.095 * sign, -0.270, 2.074),
                (0.053 * sign, -0.264, 2.061),
            ],
            0.0045,
            materials["Brow"],
            bevel_resolution=1,
        )
        add_weight(upper_lid, "Head")
        parts.append(upper_lid)

    nose_bridge = add_uv_ellipsoid(
        "NoseBridge",
        (0.0, -0.247, 2.012),
        (0.024, 0.020, 0.058),
        materials["SkinLight"],
        20,
        12,
    )
    add_weight(nose_bridge, "Head")
    parts.append(nose_bridge)
    nose_tip = add_uv_ellipsoid(
        "NoseTip",
        (0.0, -0.267, 1.985),
        (0.032, 0.026, 0.025),
        materials["SkinLight"],
        20,
        12,
    )
    add_weight(nose_tip, "Head")
    parts.append(nose_tip)
    mouth = add_tube(
        "MouthLine",
        [(-0.052, -0.238, 1.914), (0.0, -0.246, 1.908), (0.052, -0.238, 1.914)],
        0.005,
        materials["Mouth"],
        bevel_resolution=2,
    )
    add_weight(mouth, "Head")
    parts.append(mouth)

    # Thin silver rectangular glasses with a bridge and real side temples.
    for sign in (-1.0, 1.0):
        cx = 0.095 * sign
        half_x = 0.064
        half_z = 0.044
        y = -0.271
        frame_points = [
            (cx - half_x, y, 2.055 - half_z),
            (cx - half_x, y, 2.055 + half_z),
            (cx + half_x, y, 2.055 + half_z),
            (cx + half_x, y, 2.055 - half_z),
            (cx - half_x, y, 2.055 - half_z),
        ]
        frame = add_tube("Silver_Glasses_Frame", frame_points, 0.004, materials["Metal"], bevel_resolution=2)
        add_weight(frame, "Head")
        parts.append(frame)
        temple = add_tube(
            "Silver_Glasses_Temple",
            [
                (cx + half_x * sign, y + 0.004, 2.075),
                (0.238 * sign, -0.17, 2.071),
                (0.294 * sign, -0.015, 2.045),
            ],
            0.0035,
            materials["Metal"],
            bevel_resolution=2,
        )
        add_weight(temple, "Head")
        parts.append(temple)
    bridge = add_tube(
        "Silver_Glasses_Bridge",
        [(-0.031, -0.274, 2.064), (0.0, -0.279, 2.071), (0.031, -0.274, 2.064)],
        0.004,
        materials["Metal"],
        bevel_resolution=2,
    )
    add_weight(bridge, "Head")
    parts.append(bridge)

    cap = create_hair_cap(materials["Hair"])
    add_weight(cap, "Head")
    parts.append(cap)

    # Layered, tapered blade locks.  They intersect the cap and taper to pointed
    # ends, avoiding the rigid crown/sausage look of rejected diagnostics.
    front_ends = [
        (-0.230, -0.222, 2.180),
        (-0.175, -0.248, 2.190),
        (-0.115, -0.265, 2.198),
        (-0.052, -0.272, 2.196),
        (0.015, -0.271, 2.187),
        (0.078, -0.261, 2.174),
        (0.137, -0.246, 2.160),
        (0.190, -0.224, 2.147),
        (0.230, -0.192, 2.137),
    ]
    for index, end in enumerate(front_ends):
        start_x = -0.105 + index * 0.012
        middle = ((start_x + end[0]) * 0.46, -0.165, 2.305 - abs(end[0]) * 0.13)
        material = materials["HairHighlight"] if index in (1, 5) else materials["HairMid"]
        lock = create_hair_lock(
            "Front_Tapered_HairLock",
            [(start_x, -0.035, 2.365), middle, end],
            [0.072, 0.086, 0.024],
            material,
        )
        add_weight(lock, "Head")
        parts.append(lock)

    for sign in (-1.0, 1.0):
        for index in range(5):
            end = (
                sign * (0.255 + index * 0.010),
                -0.105 + index * 0.052,
                2.120 - index * 0.025,
            )
            start = (sign * (0.02 + index * 0.028), 0.005 + index * 0.015, 2.35 - index * 0.012)
            middle = (sign * (0.19 + index * 0.012), end[1] * 0.45, (start[2] + end[2]) * 0.5 + 0.025)
            if index in (1, 3):
                material = materials["HairHighlight"]
            else:
                material = materials["HairMid"]
            lock = create_hair_lock(
                "Side_Tapered_HairLock",
                [start, middle, end],
                [0.058, 0.068, 0.016],
                material,
            )
            add_weight(lock, "Head")
            parts.append(lock)
        gray_lock = create_hair_lock(
            "Temple_Gray_Accent",
            [
                (0.245 * sign, -0.180, 2.155),
                (0.264 * sign, -0.162, 2.126),
                (0.274 * sign, -0.140, 2.100),
            ],
            [0.012, 0.009, 0.003],
            materials["TempleGray"],
            thickness=0.010,
        )
        add_weight(gray_lock, "Head")
        parts.append(gray_lock)

    for index in range(7):
        x = -0.20 + index * (0.40 / 6.0)
        start = (x * 0.35, 0.045, 2.355 - abs(x) * 0.08)
        middle = (x * 0.75, 0.20, 2.22)
        end = (x, 0.27 - abs(x) * 0.04, 1.92 + abs(x) * 0.13)
        material = materials["HairHighlight"] if index in (1, 5) else materials["HairMid"]
        lock = create_hair_lock(
            "Back_Tapered_HairLock",
            [start, middle, end],
            [0.052, 0.060, 0.012],
            material,
        )
        add_weight(lock, "Head")
        parts.append(lock)

    # Shirt collar, placket, buttons, chest pocket, and back yoke.
    left_collar = add_tri_prism(
        "Left_Shirt_Collar",
        [(-0.015, 1.705), (-0.185, 1.685), (-0.082, 1.565)],
        -0.258,
        -0.215,
        materials["ShirtDark"],
    )
    right_collar = add_tri_prism(
        "Right_Shirt_Collar",
        [(0.015, 1.705), (0.185, 1.685), (0.082, 1.565)],
        -0.258,
        -0.215,
        materials["ShirtDark"],
    )
    for collar in (left_collar, right_collar):
        add_weight(collar, "UpperChest")
        parts.append(collar)
    placket = add_rounded_box(
        "Shirt_Front_Placket",
        (0.0, -0.270, 1.36),
        (0.026, 0.014, 0.59),
        materials["ShirtDark"],
        0.006,
    )
    add_blended_weights_by_z(placket, [(1.05, "Spine"), (1.42, "Chest"), (1.67, "UpperChest")])
    parts.append(placket)
    for z in (1.18, 1.36, 1.54, 1.65):
        button = add_uv_ellipsoid(
            "Shirt_Button",
            (0.0, -0.291, z),
            (0.016, 0.008, 0.016),
            materials["MetalDark"],
            14,
            8,
        )
        add_blended_weights_by_z(button, [(1.05, "Spine"), (1.42, "Chest"), (1.70, "UpperChest")])
        parts.append(button)
    pocket = add_rounded_box(
        "Shirt_Chest_Pocket",
        (0.235, -0.264, 1.43),
        (0.185, 0.009, 0.185),
        materials["ShirtLight"],
        0.018,
    )
    add_weight(pocket, "Chest")
    parts.append(pocket)
    pocket_seam = add_tube(
        "Pocket_Seam",
        [(0.145, -0.285, 1.52), (0.245, -0.292, 1.50), (0.345, -0.285, 1.52)],
        0.006,
        materials["ShirtDark"],
        bevel_resolution=1,
    )
    add_weight(pocket_seam, "Chest")
    parts.append(pocket_seam)
    back_yoke = add_tube(
        "Shirt_Back_Yoke",
        [(-0.39, 0.242, 1.55), (0.0, 0.274, 1.53), (0.39, 0.242, 1.55)],
        0.006,
        materials["ShirtDark"],
        bevel_resolution=1,
    )
    add_weight(back_yoke, "Chest")
    parts.append(back_yoke)

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        shoulder = (0.47 * sign, 0.0, 1.61)
        elbow = (0.73 * sign, 0.0, 1.39)
        wrist = (0.91 * sign, 0.0, 1.14)
        hand_end = (0.94 * sign, -0.008, 1.035)
        shoulder_blend = add_uv_ellipsoid(
            side + "_ShirtShoulderBlend",
            (0.47 * sign, 0.0, 1.61),
            (0.135, 0.126, 0.132),
            materials["Shirt"],
            24,
            14,
        )
        add_segment_weights(
            shoulder_blend,
            (0.34 * sign, 0.0, 1.64),
            elbow,
            "UpperChest",
            side + "UpperArm",
            split=0.34,
            blend=0.22,
        )
        parts.append(shoulder_blend)
        sleeve = create_segment_loft(
            side + "_RolledSleeveUpperArm",
            [shoulder, ((shoulder[0] + elbow[0]) * 0.5, 0.0, 1.51), elbow],
            [(0.155, 0.135), (0.135, 0.118), (0.115, 0.105)],
            materials["Shirt"],
            24,
        )
        bevel(sleeve, 0.007, 2)
        add_segment_weights(
            sleeve,
            shoulder,
            elbow,
            side + "Shoulder",
            side + "UpperArm",
            split=0.16,
            blend=0.16,
        )
        parts.append(sleeve)
        cuff_start = Vector(elbow) + (Vector(shoulder) - Vector(elbow)).normalized() * 0.055
        cuff_end = Vector(elbow) + (Vector(wrist) - Vector(elbow)).normalized() * 0.035
        cuff = create_segment_loft(
            side + "_RolledCuff",
            [tuple(cuff_start), tuple(cuff_end)],
            [(0.126, 0.114), (0.122, 0.11)],
            materials["ShirtDark"],
            24,
        )
        add_segment_weights(
            cuff,
            shoulder,
            wrist,
            side + "UpperArm",
            side + "LowerArm",
            split=0.53,
            blend=0.20,
        )
        parts.append(cuff)
        forearm = create_segment_loft(
            side + "_Forearm_Skin",
            [elbow, ((elbow[0] + wrist[0]) * 0.5, 0.0, 1.27), (0.925 * sign, 0.0, 1.10)],
            [(0.103, 0.093), (0.089, 0.080), (0.074, 0.068)],
            materials["Skin"],
            22,
        )
        bevel(forearm, 0.005, 2)
        add_weight(forearm, side + "LowerArm")
        parts.append(forearm)
        hand = add_uv_ellipsoid(
            side + "_CompleteHand",
            hand_end,
            (0.078, 0.070, 0.145),
            materials["Skin"],
            22,
            14,
            rotation=(0.0, math.radians(21.0 * sign), math.radians(-8.0 * sign)),
        )
        add_weight(hand, side + "Hand")
        parts.append(hand)
        thumb_start = Vector(hand_end) + Vector((-0.025 * sign, -0.045, 0.02))
        thumb_end = thumb_start + Vector((0.038 * sign, -0.012, -0.07))
        thumb = create_segment_loft(
            side + "_Thumb",
            [tuple(thumb_start), tuple(thumb_end)],
            [(0.027, 0.025), (0.020, 0.019)],
            materials["Skin"],
            14,
        )
        add_weight(thumb, side + "Hand")
        parts.append(thumb)
        if side == "Left":
            band_start = Vector(wrist) + (Vector(elbow) - Vector(wrist)).normalized() * 0.015
            band_end = Vector(wrist) + (Vector(elbow) - Vector(wrist)).normalized() * 0.080
            band = create_segment_loft(
                "AnalogWatch_LeatherBand",
                [tuple(band_start), tuple(band_end)],
                [(0.080, 0.073), (0.082, 0.075)],
                materials["Leather"],
                20,
            )
            add_weight(band, "LeftLowerArm")
            parts.append(band)
            watch = add_uv_ellipsoid(
                "AnalogWatch_SilverCase",
                (wrist[0] - 0.015, -0.077, wrist[2] + 0.018),
                (0.046, 0.022, 0.048),
                materials["Metal"],
                20,
                12,
            )
            add_weight(watch, "LeftLowerArm")
            parts.append(watch)
            face = add_uv_ellipsoid(
                "AnalogWatch_Face",
                (wrist[0] - 0.015, -0.096, wrist[2] + 0.018),
                (0.034, 0.008, 0.035),
                materials["WatchFace"],
                18,
                10,
            )
            add_weight(face, "LeftLowerArm")
            parts.append(face)
            hour_hand = add_tube(
                "WatchHourHand",
                [(wrist[0] - 0.015, -0.105, wrist[2] + 0.018), (wrist[0] - 0.004, -0.106, wrist[2] + 0.037)],
                0.0025,
                materials["MetalDark"],
                bevel_resolution=1,
            )
            add_weight(hour_hand, "LeftLowerArm")
            parts.append(hour_hand)

    armature = create_armature()

    # Join every visible character surface into exactly one mesh object.  Bone
    # groups and per-part palette material indices survive the join.
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = torso
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "Father_Complete_SkinnedMesh"
    smooth(body)
    body["identity"] = "father"
    body["age"] = 46
    body["source_contract"] = SCRIPT_VERSION
    body["single_skinned_body"] = True

    modifier = body.modifiers.new("Father_Humanoid_Skin", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True
    body.parent = armature

    return body, armature


def build_atlas(
    output_dir: Path,
    body: bpy.types.Object,
    material_lookup: dict[str, bpy.types.Material],
) -> tuple[Path, bpy.types.Material, dict[str, tuple[float, float]]]:
    ordered = list(PALETTE.keys())
    if len(ordered) > ATLAS_GRID * ATLAS_GRID:
        raise RuntimeError("palette exceeds atlas grid")
    image = bpy.data.images.new("FatherIdentityAtlas", width=ATLAS_SIZE, height=ATLAS_SIZE, alpha=True)
    pixels = np.zeros((ATLAS_SIZE, ATLAS_SIZE, 4), dtype=np.float32)
    pixels[:, :, :] = np.array((0.04, 0.04, 0.04, 1.0), dtype=np.float32)
    cell = ATLAS_SIZE // ATLAS_GRID
    uv_centers: dict[str, tuple[float, float]] = {}
    for index, name in enumerate(ordered):
        col = index % ATLAS_GRID
        row = index // ATLAS_GRID
        x0 = col * cell
        y0 = row * cell
        color = np.array(PALETTE[name], dtype=np.float32)
        pixels[y0 : y0 + cell, x0 : x0 + cell, :] = color
        # Add a narrow lighter top stripe and darker bottom stripe while keeping
        # the UV center in the flat base region.  This makes the atlas auditable
        # as a real texture rather than a one-pixel palette.
        pixels[y0 + int(cell * 0.82) : y0 + cell, x0 : x0 + cell, :3] = np.minimum(color[:3] * 1.10, 1.0)
        pixels[y0 : y0 + int(cell * 0.16), x0 : x0 + cell, :3] = color[:3] * 0.72
        uv_centers[name] = ((x0 + cell * 0.5) / ATLAS_SIZE, (y0 + cell * 0.5) / ATLAS_SIZE)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(output_dir / "father-blender-identity-v1-atlas.png")
    image.file_format = "PNG"
    image.save()

    material_names = [slot.material.name for slot in body.material_slots]
    # Joined primitives retain a generated UVMap. Unity samples FBX UV0, so leaving the atlas
    # coordinates in a second layer makes the whole character read from one arbitrary swatch.
    # Remove every inherited layer and make the identity atlas the sole, deterministic UV0.
    while len(body.data.uv_layers) > 0:
        body.data.uv_layers.remove(body.data.uv_layers[0])
    uv_layer = body.data.uv_layers.new(name="IdentityAtlasUV")
    for poly in body.data.polygons:
        material_name = material_names[poly.material_index]
        if material_name not in uv_centers:
            raise RuntimeError(f"unexpected preview material in joined mesh: {material_name}")
        u, v = uv_centers[material_name]
        for loop_index in poly.loop_indices:
            uv_layer.data[loop_index].uv = (u, v)

    atlas_material = bpy.data.materials.new("Father_IdentityAtlas_Material")
    atlas_material.use_nodes = True
    nodes = atlas_material.node_tree.nodes
    links = atlas_material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    texture.extension = "CLIP"
    bsdf.inputs["Roughness"].default_value = 0.56
    bsdf.inputs["Specular IOR Level"].default_value = 0.30
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], bsdf.inputs["Alpha"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    body.data.materials.clear()
    body.data.materials.append(atlas_material)
    for poly in body.data.polygons:
        poly.material_index = 0
    return Path(image.filepath_raw), atlas_material, uv_centers


def add_render_stage() -> tuple[bpy.types.Object, list[bpy.types.Object], bpy.types.Object]:
    floor_mat = bpy.data.materials.new("QA_Floor_Material")
    floor_mat.diffuse_color = (0.12, 0.135, 0.16, 1.0)
    floor_mat.use_nodes = True
    floor_bsdf = floor_mat.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = (0.12, 0.135, 0.16, 1.0)
    floor_bsdf.inputs["Roughness"].default_value = 0.78
    floor = add_rounded_box("QA_Floor", (0.0, 0.0, -0.035), (4.5, 4.5, 0.07), floor_mat, 0.04)

    bpy.ops.object.light_add(type="AREA", location=(-3.4, -4.2, 5.8))
    key = bpy.context.object
    key.name = "QA_Key_Light"
    key.data.energy = 1050
    key.data.shape = "DISK"
    key.data.size = 4.0
    key.rotation_euler = (math.radians(24), 0.0, math.radians(-38))

    bpy.ops.object.light_add(type="AREA", location=(3.5, -1.7, 3.6))
    fill = bpy.context.object
    fill.name = "QA_Fill_Light"
    fill.data.energy = 650
    fill.data.size = 3.0
    fill.rotation_euler = (math.radians(64), 0.0, math.radians(124))

    bpy.ops.object.light_add(type="AREA", location=(0.0, 3.8, 4.1))
    rim = bpy.context.object
    rim.name = "QA_Rim_Light"
    rim.data.energy = 900
    rim.data.size = 2.6
    rim.rotation_euler = (math.radians(-42), 0.0, math.radians(180))

    bpy.ops.object.camera_add(location=(0.0, -6.4, 1.35))
    camera = bpy.context.object
    camera.name = "QA_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.72
    camera.data.lens = 55
    bpy.context.scene.camera = camera
    return camera, [key, fill, rim], floor


def point_camera(camera: bpy.types.Object, position: tuple[float, float, float]) -> None:
    camera.location = position
    target = Vector((0.0, 0.0, 1.19))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_view(
    camera: bpy.types.Object,
    path: Path,
    position: tuple[float, float, float],
    resolution: int,
) -> None:
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
    degrees = math.radians
    armature.pose.bones["LeftUpperLeg"].rotation_euler.x = degrees(-23.0 * sign)
    armature.pose.bones["RightUpperLeg"].rotation_euler.x = degrees(23.0 * sign)
    armature.pose.bones["LeftLowerLeg"].rotation_euler.x = degrees(13.0 * sign + 12.0)
    armature.pose.bones["RightLowerLeg"].rotation_euler.x = degrees(-13.0 * sign + 12.0)
    armature.pose.bones["LeftFoot"].rotation_euler.x = degrees(8.0 * sign)
    armature.pose.bones["RightFoot"].rotation_euler.x = degrees(-8.0 * sign)
    armature.pose.bones["LeftUpperArm"].rotation_euler.x = degrees(16.0 * sign)
    armature.pose.bones["RightUpperArm"].rotation_euler.x = degrees(-16.0 * sign)
    armature.pose.bones["LeftLowerArm"].rotation_euler.x = degrees(-8.0 * sign)
    armature.pose.bones["RightLowerArm"].rotation_euler.x = degrees(8.0 * sign)
    armature.pose.bones["Hips"].rotation_euler.z = degrees(-2.5 * sign)
    armature.pose.bones["Chest"].rotation_euler.z = degrees(2.0 * sign)


def render_all(
    output_dir: Path,
    camera: bpy.types.Object,
    armature: bpy.types.Object,
    stage: str,
) -> list[Path]:
    outputs: list[Path] = []
    views_dir = output_dir / ("draft-views" if stage == "draft" else "final-views")
    views_dir.mkdir(parents=True, exist_ok=True)
    resolution = 768 if stage == "draft" else 1536
    views = {
        "front": (0.0, -6.4, 1.34),
        "left": (-6.4, 0.0, 1.34),
        "back": (0.0, 6.4, 1.34),
        "three-quarter": (-4.55, -4.55, 1.42),
    }
    reset_pose(armature)
    for name, position in views.items():
        path = views_dir / f"father-{name}-{resolution}.png"
        render_view(camera, path, position, resolution)
        outputs.append(path)

    if stage == "final":
        turntable_dir = output_dir / "turntable-24"
        turntable_dir.mkdir(parents=True, exist_ok=True)
        radius = 6.4
        for index in range(24):
            angle = math.radians(index * 15.0)
            position = (radius * math.sin(angle), -radius * math.cos(angle), 1.38)
            path = turntable_dir / f"father-turn-{index:02d}-{index * 15:03d}deg.png"
            render_view(camera, path, position, 768)
            outputs.append(path)

        deformation_dir = output_dir / "deformation"
        deformation_dir.mkdir(parents=True, exist_ok=True)
        for phase in (0, 3):
            set_walk_pose(armature, phase)
            path = deformation_dir / f"father-P{phase}-deformation-1536.png"
            render_view(camera, path, (-4.55, -4.55, 1.42), 1536)
            outputs.append(path)
        reset_pose(armature)
    return outputs


def validate_character(body: bpy.types.Object, armature: bpy.types.Object) -> dict[str, object]:
    required_bones = {
        "Root",
        "Hips",
        "Spine",
        "Chest",
        "UpperChest",
        "Neck",
        "Head",
        "LeftShoulder",
        "LeftUpperArm",
        "LeftLowerArm",
        "LeftHand",
        "RightShoulder",
        "RightUpperArm",
        "RightLowerArm",
        "RightHand",
        "LeftUpperLeg",
        "LeftLowerLeg",
        "LeftFoot",
        "LeftToes",
        "RightUpperLeg",
        "RightLowerLeg",
        "RightFoot",
        "RightToes",
    }
    bones = set(armature.data.bones.keys())
    missing = sorted(required_bones - bones)
    if missing:
        raise RuntimeError(f"missing Humanoid bones: {missing}")
    if len(body.data.materials) != 1:
        raise RuntimeError(f"expected one atlas material, got {len(body.data.materials)}")
    if body.data.uv_layers.get("IdentityAtlasUV") is None:
        raise RuntimeError("atlas UV layer missing")
    if len(body.data.uv_layers) != 1:
        raise RuntimeError(f"expected one atlas UV layer, got {len(body.data.uv_layers)}")
    if body.modifiers.get("Father_Humanoid_Skin") is None:
        raise RuntimeError("armature modifier missing")
    if Vector(armature.data.bones["Root"].head_local).length > 1e-6:
        raise RuntimeError("Root is not bottom-centre")
    vertex_group_names = set(group.name for group in body.vertex_groups)
    missing_groups = sorted((required_bones - {"Root"}) - vertex_group_names)
    if missing_groups:
        raise RuntimeError(f"missing deform groups: {missing_groups}")

    return {
        "mesh_objects": 1,
        "armatures": 1,
        "materials": len(body.data.materials),
        "vertices": len(body.data.vertices),
        "edges": len(body.data.edges),
        "polygons": len(body.data.polygons),
        "uv_layers": len(body.data.uv_layers),
        "vertex_groups": len(body.vertex_groups),
        "bones_total": len(armature.data.bones),
        "deform_bones": sum(1 for bone in armature.data.bones if bone.use_deform),
        "required_bones_missing": missing,
        "root_head": list(armature.data.bones["Root"].head_local),
        "bound_min_z": min(vertex.co.z for vertex in body.data.vertices),
        "bound_max_z": max(vertex.co.z for vertex in body.data.vertices),
        "body_height": max(vertex.co.z for vertex in body.data.vertices)
        - min(vertex.co.z for vertex in body.data.vertices),
    }


def export_final(
    output_dir: Path,
    body: bpy.types.Object,
    armature: bpy.types.Object,
) -> tuple[Path, Path]:
    reset_pose(armature)
    blend_path = output_dir / "father-blender-identity-v1.blend"
    fbx_path = output_dir / "father-blender-humanoid-v1.fbx"

    # Keep only the character and armature in the authored .blend.
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


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output).resolve()
    reference_path = Path(args.reference).resolve()
    if not reference_path.is_file():
        raise FileNotFoundError(reference_path)
    if "Artifacts" not in output_dir.parts or "Family3DBlenderFatherV1" not in output_dir.parts:
        raise RuntimeError("output must remain under Artifacts/Family3DBlenderFatherV1")
    output_dir.mkdir(parents=True, exist_ok=True)

    ensure_clean_scene()
    materials = {name: make_preview_material(name, color) for name, color in PALETTE.items()}
    body, armature = build_character(materials)
    atlas_path, _atlas_material, uv_centers = build_atlas(output_dir, body, materials)
    metrics = validate_character(body, armature)
    camera, _lights, _floor = add_render_stage()
    rendered = render_all(output_dir, camera, armature, args.stage)

    blend_path: Path | None = None
    fbx_path: Path | None = None
    if args.stage == "final":
        blend_path, fbx_path = export_final(output_dir, body, armature)
    else:
        blend_path = output_dir / "father_blender_identity_v1_draft.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    output_files = [atlas_path, blend_path, *rendered]
    if fbx_path is not None:
        output_files.append(fbx_path)
    hashes = {str(path.relative_to(output_dir)).replace("\\", "/"): sha256(path) for path in output_files}
    receipt = {
        "contract": SCRIPT_VERSION,
        "status": "CANDIDATE_NOT_PRODUCTION",
        "stage": args.stage,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "blender_version": bpy.app.version_string,
        "reference": {
            "path": str(reference_path),
            "sha256": sha256(reference_path),
            "role": "father",
            "identity": [
                "46-year-old mature adult",
                "broad shoulders",
                "short neat charcoal side-parted hair with light temple gray",
                "dark-brown eyes",
                "thin silver rectangular glasses",
                "muted teal rolled-sleeve shirt",
                "charcoal slacks",
                "brown belt and oxford shoes",
                "analog wristwatch",
            ],
        },
        "provenance": {
            "geometry": "new procedural/manual-style topology authored by this script",
            "texture": "new single flat-color texture atlas authored by this script",
            "rig": "new Unity-Humanoid-compatible armature authored by this script",
            "legacy_2d_used": False,
            "styloo_used": False,
            "player_v1_v2_mesh_used": False,
            "external_mesh_or_texture_used": False,
        },
        "metrics": metrics,
        "atlas": {
            "path": atlas_path.name,
            "size": [ATLAS_SIZE, ATLAS_SIZE],
            "grid": [ATLAS_GRID, ATLAS_GRID],
            "swatches": uv_centers,
            "material_count": 1,
        },
        "qa": {
            "four_view_resolution": 768 if args.stage == "draft" else 1536,
            "four_views": ["front", "left", "back", "three-quarter"],
            "turntable_views": 0 if args.stage == "draft" else 24,
            "deformation_views": [] if args.stage == "draft" else ["P0", "P3"],
            "production_promoted": False,
        },
        "outputs_sha256": hashes,
    }
    receipt_path = output_dir / "father-blender-identity-v1-receipt.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        "FAMILY_COMPANY_FATHER_BLENDER_IDENTITY_V1: PASS "
        f"| stage={args.stage} vertices={metrics['vertices']} polygons={metrics['polygons']} "
        f"bones={metrics['bones_total']} material=1 rendered={len(rendered)}"
    )


if __name__ == "__main__":
    main()
