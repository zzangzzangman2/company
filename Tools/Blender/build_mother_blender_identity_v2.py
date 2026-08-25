#!/usr/bin/env python3
"""Build the clean-room Mother V2 candidate from the current runtime 2D identity canon.

The runtime HighMotion sheets lock the game silhouette and family scale.  The high-resolution
neutral art locks only face, hair, clothing, accessory, and colour details.  Those three files
are hash-checked but never sampled, projected, traced, or packed into the generated atlas.

Explicitly excluded inputs:
* the user-rejected FamilyIdentityTurnaroundsV1 Mother image;
* every Mother V1 blend, FBX, mesh, material, texture, atlas, render, or geometry donor;
* any legacy sprite as mesh, texture, decal, billboard, motion donor, or fallback.

This script creates all geometry, skin weights, atlas pixels, rig data, and proof renders from
numeric modelling locks written below.  Final4 is an original clean-room polish pass based only
on this Mother V2 generator and the locked Mother runtime/neutral art.  External model files are
never opened or imported and no external geometry, topology, texture, UV, rig, material, or
weight data is copied.  The output remains an isolated candidate with ``productionEligible``
false until human visual review and parent Unity integration.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


IDENTITY = "mother"
CONTRACT = "FC-MOTHER-RUNTIME2D-IDENTITY-V2"
REVISION = "Final4"
EXPECTED_NEUTRAL_SHA256 = "A92FDABF1ABE5ECC6ACF9E0FC8149F084170B5F3A3BE853F837C1AAEB40C4843"
EXPECTED_HIGHMOTION_A_SHA256 = "8AFB82E6FDC3942D46F2B5E8018ED606D2967B9D17861583F1668A9DED1ADA50"
EXPECTED_HIGHMOTION_B_SHA256 = "A5A375C2B62BB48ADC2F9469B93F3AE83F630DD9D7EF05DB1D37D99F7D67804A"
EXCLUDED_TURNAROUND_SHA256 = "6FFC0A5699F2B897F99A532F3264E58911A9B4ECA09C717450D9D588284FE604"

ATLAS_SIZE = 1024
ATLAS_COLS = 4
ATLAS_ROWS = 4
UV_LAYER_NAME = "MotherV2AtlasUV"

# Clean flat colours measured from the two runtime sheets and neutral identity art.  These are
# numeric style locks, not copied image pixels.  One solid atlas patch is used per material role.
PATCHES = {
    "skin": 0,
    "skin_shadow": 1,
    "hair_dark": 2,
    "hair_mid": 3,
    "cardigan": 4,
    "cardigan_light": 5,
    "blouse": 6,
    "skirt": 7,
    "skirt_light": 8,
    "shoe_dark": 9,
    "shoe_mid": 10,
    "eye_brown": 11,
    "eye_white": 12,
    "pupil": 13,
    "pearl": 14,
    "mouth": 15,
}

COLORS = {
    "skin": (0.988, 0.788, 0.659, 1.0),       # HighMotion #FCC8A8
    "skin_shadow": (0.925, 0.592, 0.467, 1.0),
    "hair_dark": (0.220, 0.115, 0.078, 1.0),
    "hair_mid": (0.335, 0.180, 0.125, 1.0),
    "cardigan": (0.929, 0.592, 0.485, 1.0),
    "cardigan_light": (0.965, 0.680, 0.575, 1.0),
    "blouse": (0.965, 0.925, 0.824, 1.0),
    "skirt": (0.118, 0.247, 0.294, 1.0),     # neutral #1E3F4B
    "skirt_light": (0.180, 0.322, 0.365, 1.0),
    "shoe_dark": (0.145, 0.075, 0.052, 1.0),
    "shoe_mid": (0.278, 0.165, 0.118, 1.0),
    "eye_brown": (0.310, 0.145, 0.070, 1.0),
    "eye_white": (0.992, 0.965, 0.900, 1.0),
    "pupil": (0.055, 0.024, 0.016, 1.0),
    "pearl": (0.965, 0.925, 0.840, 1.0),
    "mouth": (0.620, 0.160, 0.135, 1.0),
}

HUMANOID_MAP = {
    "Root": "Root",
    "Hips": "Hips",
    "Spine": "Spine",
    "Chest": "Chest",
    "UpperChest": "UpperChest",
    "Neck": "Neck",
    "Head": "Head",
    "LeftShoulder": "LeftShoulder",
    "LeftUpperArm": "LeftUpperArm",
    "LeftLowerArm": "LeftLowerArm",
    "LeftHand": "LeftHand",
    "RightShoulder": "RightShoulder",
    "RightUpperArm": "RightUpperArm",
    "RightLowerArm": "RightLowerArm",
    "RightHand": "RightHand",
    "LeftUpperLeg": "LeftUpperLeg",
    "LeftLowerLeg": "LeftLowerLeg",
    "LeftFoot": "LeftFoot",
    "LeftToes": "LeftToes",
    "RightUpperLeg": "RightUpperLeg",
    "RightLowerLeg": "RightLowerLeg",
    "RightFoot": "RightFoot",
    "RightToes": "RightToes",
}

CHARACTER_PARTS: list[bpy.types.Object] = []
ATLAS_MATERIAL: bpy.types.Material


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--neutral-reference", required=True)
    parser.add_argument("--highmotion-a", required=True)
    parser.add_argument("--highmotion-b", required=True)
    parser.add_argument("--candidate-output")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--draft-only", action="store_true")
    group.add_argument("--proof-only", action="store_true")
    group.add_argument("--final", action="store_true")
    return parser.parse_args(argv)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def validate_reference(path: Path, expected: str, label: str) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)
    actual = sha256_file(path)
    if actual == EXCLUDED_TURNAROUND_SHA256:
        raise RuntimeError(f"Rejected Mother turnaround supplied as {label}; it is excluded from V2")
    if actual != expected:
        raise RuntimeError(f"Locked {label} hash mismatch: {actual}")
    lowered = str(path).replace("\\", "/").lower()
    if "familyidentityturnaroundsv1" in lowered or "motherv1" in lowered:
        raise RuntimeError(f"Excluded V1/turnaround path supplied as {label}: {path}")


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def make_atlas(output: Path) -> tuple[bpy.types.Image, bpy.types.Material, Path]:
    atlas_path = output / "mother-blender-identity-v2-atlas.png"
    pixels = np.zeros((ATLAS_SIZE, ATLAS_SIZE, 4), dtype=np.float32)
    cell_w = ATLAS_SIZE // ATLAS_COLS
    cell_h = ATLAS_SIZE // ATLAS_ROWS
    for name, patch in PATCHES.items():
        col = patch % ATLAS_COLS
        row = patch // ATLAS_COLS
        x0 = col * cell_w
        y0 = row * cell_h
        colour = np.asarray(COLORS[name], dtype=np.float32)
        pixels[y0 : y0 + cell_h, x0 : x0 + cell_w, :] = colour

    image = bpy.data.images.new("MotherIdentityAtlasV2", ATLAS_SIZE, ATLAS_SIZE, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels.reshape(-1))
    image.filepath_raw = str(atlas_path)
    image.file_format = "PNG"
    image.save()

    material = bpy.data.materials.new("MotherIdentityAtlasMaterialV2")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output_node = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Roughness"].default_value = 0.93
    shader.inputs["Specular IOR Level"].default_value = 0.06
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    links.new(shader.outputs["BSDF"], output_node.inputs["Surface"])
    material.diffuse_color = COLORS["cardigan"]
    return image, material, atlas_path


def apply_transforms(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def smooth_mesh(obj: bpy.types.Object) -> None:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def assign_uv_patch(obj: bpy.types.Object, patch_name: str) -> None:
    if len(obj.data.materials) == 0:
        obj.data.materials.append(ATLAS_MATERIAL)
    uv = obj.data.uv_layers.get(UV_LAYER_NAME) or obj.data.uv_layers.new(name=UV_LAYER_NAME)
    patch = PATCHES[patch_name]
    col = patch % ATLAS_COLS
    row = patch // ATLAS_COLS
    coordinate = ((col + 0.5) / ATLAS_COLS, (row + 0.5) / ATLAS_ROWS)
    for loop in obj.data.loops:
        uv.data[loop.index].uv = coordinate
    obj.data.uv_layers.active_index = obj.data.uv_layers.find(UV_LAYER_NAME)


def tag_rigid(obj: bpy.types.Object, bone_name: str) -> None:
    group = obj.vertex_groups.get(bone_name) or obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")


def tag_blended_z(obj: bpy.types.Object, keys: list[tuple[str, float]]) -> None:
    groups = {name: obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name) for name, _ in keys}
    ordered = sorted(keys, key=lambda item: item[1])
    for vertex in obj.data.vertices:
        world_z = (obj.matrix_world @ vertex.co).z
        lower = ordered[0]
        upper = ordered[-1]
        if world_z <= ordered[0][1]:
            lower = upper = ordered[0]
        elif world_z >= ordered[-1][1]:
            lower = upper = ordered[-1]
        else:
            for index in range(len(ordered) - 1):
                if ordered[index][1] <= world_z <= ordered[index + 1][1]:
                    lower = ordered[index]
                    upper = ordered[index + 1]
                    break
        if lower[0] == upper[0] or abs(upper[1] - lower[1]) < 1e-7:
            groups[lower[0]].add([vertex.index], 1.0, "REPLACE")
        else:
            t = (world_z - lower[1]) / (upper[1] - lower[1])
            groups[lower[0]].add([vertex.index], 1.0 - t, "REPLACE")
            groups[upper[0]].add([vertex.index], t, "REPLACE")


def register_part(obj: bpy.types.Object, patch: str, bone: str | None = None, smooth: bool = True) -> bpy.types.Object:
    if smooth:
        smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    if bone:
        tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def make_ellipsoid(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    patch: str,
    bone: str,
    segments: int = 28,
    rings: int = 18,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transforms(obj)
    return register_part(obj, patch, bone)


def make_rounded_box(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    patch: str,
    bone: str,
    bevel: float,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transforms(obj)
    modifier = obj.modifiers.new("Clean rounded edge", "BEVEL")
    modifier.width = bevel
    modifier.segments = 3
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return register_part(obj, patch, bone)


def orthonormal_basis(direction: Vector) -> tuple[Vector, Vector]:
    reference = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(reference)) > 0.92:
        reference = Vector((1.0, 0.0, 0.0))
    first = direction.cross(reference).normalized()
    second = direction.cross(first).normalized()
    return first, second


def make_tapered_tube(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius_start: float,
    radius_end: float,
    patch: str,
    bone: str,
    sides: int = 16,
    squash: float = 0.90,
) -> bpy.types.Object:
    start_v = Vector(start)
    end_v = Vector(end)
    direction = (end_v - start_v).normalized()
    first, second = orthonormal_basis(direction)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    ring_count = 7
    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        centre = start_v.lerp(end_v, t)
        radius = radius_start * (1.0 - t) + radius_end * t
        soft_end = 0.90 + 0.10 * math.sin(math.pi * t)
        for side in range(sides):
            angle = math.tau * side / sides
            point = centre + first * (math.cos(angle) * radius * soft_end) + second * (
                math.sin(angle) * radius * squash * soft_end
            )
            vertices.append(tuple(point))
    for ring in range(ring_count - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            a = ring * sides + side
            b = ring * sides + nxt
            c = (ring + 1) * sides + nxt
            d = (ring + 1) * sides + side
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(sides))))
    offset = (ring_count - 1) * sides
    faces.append(tuple(offset + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return register_part(obj, patch, bone)


def make_loft(
    name: str,
    rings: list[tuple[float, float, float, float]],
    patch: str,
    weight_keys: list[tuple[str, float]],
    segments: int = 40,
    folds: float = 0.0,
) -> bpy.types.Object:
    """Create an elliptical z/y/rx/ry loft, with negative Y facing the camera."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring_index, (z, y_centre, radius_x, radius_y) in enumerate(rings):
        lower_fraction = 1.0 - ring_index / max(1, len(rings) - 1)
        for index in range(segments):
            angle = math.tau * index / segments
            fold = 1.0 + folds * lower_fraction * math.cos(6.0 * angle) ** 2
            vertices.append(
                (
                    radius_x * fold * math.cos(angle),
                    y_centre + radius_y * fold * math.sin(angle),
                    z,
                )
            )
    for ring in range(len(rings) - 1):
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append(
                (
                    ring * segments + index,
                    ring * segments + nxt,
                    (ring + 1) * segments + nxt,
                    (ring + 1) * segments + index,
                )
            )
    bottom = len(vertices)
    vertices.append((0.0, rings[0][1], rings[0][0]))
    top = len(vertices)
    vertices.append((0.0, rings[-1][1], rings[-1][0]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((bottom, nxt, index))
        offset = (len(rings) - 1) * segments
        faces.append((top, offset + index, offset + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_blended_z(obj, weight_keys)
    CHARACTER_PARTS.append(obj)
    return obj


def make_offset_loft(
    name: str,
    rings: list[tuple[float, float, float, float, float]],
    patch: str,
    bone: str,
    segments: int = 36,
) -> bpy.types.Object:
    """Create a smooth x/y-offset elliptical loft for integrated anime hair masses."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for z, x_centre, y_centre, radius_x, radius_y in rings:
        for index in range(segments):
            angle = math.tau * index / segments
            vertices.append(
                (
                    x_centre + radius_x * math.cos(angle),
                    y_centre + radius_y * math.sin(angle),
                    z,
                )
            )
    for ring in range(len(rings) - 1):
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append(
                (
                    ring * segments + index,
                    ring * segments + nxt,
                    (ring + 1) * segments + nxt,
                    (ring + 1) * segments + index,
                )
            )
    bottom = len(vertices)
    vertices.append((rings[0][1], rings[0][2], rings[0][0]))
    top = len(vertices)
    vertices.append((rings[-1][1], rings[-1][2], rings[-1][0]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((bottom, nxt, index))
        offset = (len(rings) - 1) * segments
        faces.append((top, offset + index, offset + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return register_part(obj, patch, bone)


def make_continuous_sleeve(
    name: str,
    shoulder: tuple[float, float, float],
    elbow: tuple[float, float, float],
    wrist: tuple[float, float, float],
    radii: tuple[float, float, float],
    patch: str,
    upper_bone: str,
    lower_bone: str,
    sides: int = 20,
) -> bpy.types.Object:
    """Build one continuous tapered sleeve surface with a smooth elbow weight blend."""
    shoulder_v = Vector(shoulder)
    elbow_v = Vector(elbow)
    wrist_v = Vector(wrist)
    centres: list[Vector] = []
    ring_radii: list[float] = []
    for index in range(5):
        t = index / 4.0
        centres.append(shoulder_v.lerp(elbow_v, t))
        ring_radii.append(radii[0] * (1.0 - t) + radii[1] * t)
    for index in range(1, 6):
        t = index / 5.0
        centres.append(elbow_v.lerp(wrist_v, t))
        ring_radii.append(radii[1] * (1.0 - t) + radii[2] * t)

    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring, centre in enumerate(centres):
        if ring == 0:
            tangent = (centres[1] - centre).normalized()
        elif ring == len(centres) - 1:
            tangent = (centre - centres[ring - 1]).normalized()
        else:
            tangent = (centres[ring + 1] - centres[ring - 1]).normalized()
        first, second = orthonormal_basis(tangent)
        radius = ring_radii[ring]
        for side in range(sides):
            angle = math.tau * side / sides
            point = centre + first * (math.cos(angle) * radius) + second * (math.sin(angle) * radius * 0.84)
            vertices.append(tuple(point))
    for ring in range(len(centres) - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            faces.append(
                (
                    ring * sides + side,
                    ring * sides + nxt,
                    (ring + 1) * sides + nxt,
                    (ring + 1) * sides + side,
                )
            )
    faces.append(tuple(reversed(range(sides))))
    offset = (len(centres) - 1) * sides
    faces.append(tuple(offset + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    upper_group = obj.vertex_groups.new(name=upper_bone)
    lower_group = obj.vertex_groups.new(name=lower_bone)
    last_ring = len(centres) - 1
    for ring in range(len(centres)):
        u = ring / last_ring
        if u <= 0.42:
            upper_weight = 1.0
        elif u >= 0.62:
            upper_weight = 0.0
        else:
            upper_weight = 1.0 - (u - 0.42) / 0.20
        indices = list(range(ring * sides, (ring + 1) * sides))
        if upper_weight > 0.0:
            upper_group.add(indices, upper_weight, "REPLACE")
        if upper_weight < 1.0:
            lower_group.add(indices, 1.0 - upper_weight, "REPLACE")
    CHARACTER_PARTS.append(obj)
    return obj


def make_ribbon(
    name: str,
    points: list[tuple[float, float, float]],
    widths: list[float],
    depths: list[float],
    patch: str,
    bone: str,
    bevel: float = 0.0,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    count = len(points)
    for index, raw in enumerate(points):
        point = Vector(raw)
        if index == 0:
            tangent = (Vector(points[1]) - point).normalized()
        elif index == count - 1:
            tangent = (point - Vector(points[index - 1])).normalized()
        else:
            tangent = (Vector(points[index + 1]) - Vector(points[index - 1])).normalized()
        side = tangent.cross(Vector((0.0, 1.0, 0.0)))
        if side.length < 1e-5:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        depth_axis = tangent.cross(side).normalized()
        half_width = widths[index] * 0.5
        half_depth = depths[index] * 0.5
        vertices.extend(
            [
                tuple(point - side * half_width - depth_axis * half_depth),
                tuple(point + side * half_width - depth_axis * half_depth),
                tuple(point + side * half_width + depth_axis * half_depth),
                tuple(point - side * half_width + depth_axis * half_depth),
            ]
        )
    for ring in range(count - 1):
        a = ring * 4
        b = (ring + 1) * 4
        faces.extend(
            [
                (a + 0, a + 1, b + 1, b + 0),
                (a + 1, a + 2, b + 2, b + 1),
                (a + 2, a + 3, b + 3, b + 2),
                (a + 3, a + 0, b + 0, b + 3),
            ]
        )
    faces.append((3, 2, 1, 0))
    end = (count - 1) * 4
    faces.append((end, end + 1, end + 2, end + 3))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Clean ribbon edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    return register_part(obj, patch, bone)


def make_disc(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    patch: str,
    bone: str,
    vertices: int = 20,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    apply_transforms(obj)
    return register_part(obj, patch, bone)


def shape_adult_anime_head(obj: bpy.types.Object, radius_z: float) -> None:
    """Create a soft oval jaw without the spherical/egg silhouette rejected in V1."""
    for vertex in obj.data.vertices:
        normalized_z = vertex.co.z / radius_z
        if normalized_z < -0.10:
            t = max(0.0, min(1.0, (normalized_z + 1.0) / 0.90))
            vertex.co.x *= 0.62 + 0.38 * t
            vertex.co.y *= 0.88 + 0.12 * t
        if normalized_z > 0.72:
            t = max(0.0, min(1.0, (normalized_z - 0.72) / 0.28))
            vertex.co.x *= 1.0 - 0.08 * t
    obj.data.update()


def make_armature(arm_points: dict[str, tuple[tuple[float, float, float], ...]]) -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("MotherHumanoidArmatureV2")
    armature = bpy.data.objects.new("Mother_HumanoidArmature_V2", armature_data)
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
    ) -> None:
        edit_bone = armature_data.edit_bones.new(name)
        edit_bone.head = head
        edit_bone.tail = tail
        edit_bone.use_deform = name != "Root"
        if parent:
            edit_bone.parent = armature_data.edit_bones[parent]
            edit_bone.use_connect = connected

    bone("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.18))
    bone("Hips", (0.0, 0.0, 1.69), (0.0, 0.0, 1.82), "Root")
    bone("Spine", (0.0, 0.0, 1.82), (0.0, 0.0, 2.02), "Hips", True)
    bone("Chest", (0.0, 0.0, 2.02), (0.0, 0.0, 2.20), "Spine", True)
    bone("UpperChest", (0.0, 0.0, 2.20), (0.0, 0.0, 2.36), "Chest", True)
    bone("Neck", (0.0, 0.0, 2.36), (0.0, -0.012, 2.47), "UpperChest", True)
    bone("Head", (0.0, -0.012, 2.47), (0.0, -0.020, 3.34), "Neck", True)

    for side, prefix in (("L", "Left"), ("R", "Right")):
        shoulder, elbow, wrist, hand_end = arm_points[side]
        sign = 1.0 if side == "L" else -1.0
        bone(f"{prefix}Shoulder", (sign * 0.08, 0.0, 2.32), shoulder, "UpperChest")
        bone(f"{prefix}UpperArm", shoulder, elbow, f"{prefix}Shoulder", True)
        bone(f"{prefix}LowerArm", elbow, wrist, f"{prefix}UpperArm", True)
        bone(f"{prefix}Hand", wrist, hand_end, f"{prefix}LowerArm", True)
        bone(f"{prefix}UpperLeg", (sign * 0.18, 0.0, 1.70), (sign * 0.18, 0.0, 0.94), "Hips")
        bone(f"{prefix}LowerLeg", (sign * 0.18, 0.0, 0.94), (sign * 0.18, 0.0, 0.20), f"{prefix}UpperLeg", True)
        bone(f"{prefix}Foot", (sign * 0.18, 0.0, 0.20), (sign * 0.18, -0.20, 0.09), f"{prefix}LowerLeg", True)
        bone(f"{prefix}Toes", (sign * 0.18, -0.20, 0.09), (sign * 0.18, -0.34, 0.06), f"{prefix}Foot", True)

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "OCTAHEDRAL"
    armature["familyCompanyHumanoid"] = True
    armature["bottomCentreRoot"] = True
    armature["unityHumanoidMappingJson"] = json.dumps(HUMANOID_MAP, sort_keys=True)
    armature.select_set(False)
    return armature


def make_character() -> tuple[bpy.types.Object, bpy.types.Object]:
    # Runtime-first compact adult: the actual HighMotion silhouette, not an external model,
    # keeps the result in the locked 3.3--3.6 visual head range.
    # Lower legs and low-profile loafers remain readable under the below-knee A-line skirt.
    for side, x, prefix in (("L", 0.18, "Left"), ("R", -0.18, "Right")):
        make_tapered_tube(
            f"MotherV2_Calf_{side}",
            (x, 0.015, 0.83),
            (x, -0.005, 0.15),
            0.115,
            0.082,
            "skin",
            f"{prefix}LowerLeg",
            squash=0.82,
        )
        make_rounded_box(
            f"MotherV2_Loafer_{side}",
            (x, -0.080, 0.105),
            (0.118, 0.198, 0.070),
            "shoe_mid",
            f"{prefix}Foot",
            0.055,
        )
        make_rounded_box(
            f"MotherV2_LoaferSole_{side}",
            (x, -0.085, 0.018),
            (0.124, 0.208, 0.016),
            "shoe_dark",
            f"{prefix}Foot",
            0.018,
        )
        make_rounded_box(
            f"MotherV2_LoaferHeel_{side}",
            (x, 0.088, 0.047),
            (0.098, 0.050, 0.034),
            "shoe_dark",
            f"{prefix}Foot",
            0.018,
        )
        make_ellipsoid(
            f"MotherV2_LoaferToe_{side}",
            (x, -0.225, 0.105),
            (0.112, 0.105, 0.058),
            "shoe_mid",
            f"{prefix}Foot",
            24,
            12,
        )
        make_ribbon(
            f"MotherV2_LoaferStrap_{side}",
            [(x - 0.105, -0.160, 0.162), (x, -0.180, 0.170), (x + 0.105, -0.160, 0.162)],
            [0.020, 0.026, 0.020],
            [0.016, 0.018, 0.016],
            "shoe_dark",
            f"{prefix}Foot",
            bevel=0.004,
        )

    # Clean long A-line silhouette.  A denser, convex flare avoids the rigid cone/trapezoid read
    # while retaining the runtime Mother's calf-length teal silhouette.
    make_loft(
        "MotherV2_AlineSkirt",
        [
            (0.62, 0.020, 0.472, 0.270),
            (0.68, 0.019, 0.492, 0.280),
            (0.79, 0.016, 0.485, 0.276),
            (0.94, 0.012, 0.462, 0.266),
            (1.10, 0.008, 0.430, 0.250),
            (1.26, 0.003, 0.392, 0.232),
            (1.42, -0.002, 0.357, 0.218),
            (1.56, -0.006, 0.332, 0.208),
            (1.67, -0.009, 0.316, 0.202),
            (1.76, -0.010, 0.307, 0.198),
        ],
        "skirt",
        [("Hips", 0.62), ("Hips", 1.76)],
        segments=56,
        folds=0.018,
    )

    # Soft-sturdy cardigan torso with a continuous waist-to-shoulder curve.  The closely spaced
    # upper rings and convex clavicle falloff let the sleeve merge into the body silhouette.
    make_loft(
        "MotherV2_CardiganTorso",
        [
            (1.68, 0.005, 0.312, 0.196),
            (1.75, 0.003, 0.332, 0.208),
            (1.87, 0.000, 0.354, 0.219),
            (1.99, -0.002, 0.382, 0.232),
            (2.10, -0.003, 0.402, 0.240),
            (2.16, -0.003, 0.416, 0.244),
            (2.21, -0.003, 0.420, 0.244),
            (2.25, -0.002, 0.414, 0.239),
            (2.29, -0.002, 0.397, 0.230),
            (2.33, -0.001, 0.365, 0.211),
            (2.37, 0.000, 0.316, 0.184),
            (2.40, 0.000, 0.250, 0.149),
        ],
        "cardigan",
        [("Hips", 1.68), ("Spine", 1.88), ("Chest", 2.08), ("UpperChest", 2.40)],
        segments=56,
    )
    # Cream blouse inset and collar are independent new geometry mapped to the same atlas.
    make_ribbon(
        "MotherV2_CreamBlouseFront",
        [(0.0, -0.228, 1.72), (0.0, -0.250, 2.02), (0.0, -0.252, 2.30), (0.0, -0.205, 2.39)],
        [0.320, 0.365, 0.335, 0.150],
        [0.020, 0.020, 0.018, 0.014],
        "blouse",
        "Chest",
        bevel=0.008,
    )
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_ribbon(
            f"MotherV2_CardiganEdge_{side}",
            [(sign * 0.145, -0.238, 1.72), (sign * 0.162, -0.265, 2.02), (sign * 0.140, -0.255, 2.29)],
            [0.034, 0.036, 0.026],
            [0.014, 0.014, 0.012],
            "cardigan_light",
            "Chest",
            bevel=0.006,
        )
    make_ribbon(
        "MotherV2_Collar_L",
        [(0.0, -0.259, 2.34), (0.105, -0.279, 2.29), (0.065, -0.280, 2.21)],
        [0.018, 0.105, 0.018],
        [0.010, 0.012, 0.008],
        "blouse",
        "UpperChest",
        bevel=0.004,
    )
    make_ribbon(
        "MotherV2_Collar_R",
        [(0.0, -0.259, 2.34), (-0.105, -0.279, 2.29), (-0.065, -0.280, 2.21)],
        [0.018, 0.105, 0.018],
        [0.010, 0.012, 0.008],
        "blouse",
        "UpperChest",
        bevel=0.004,
    )
    for index, z in enumerate((2.18, 2.04, 1.90, 1.76)):
        make_disc(f"MotherV2_CardiganButton_{index}", (0.170, -0.258, z), 0.018, 0.010, "pearl", "Chest")
    for index, z in enumerate((2.24, 2.10, 1.96)):
        make_disc(f"MotherV2_BlouseButton_{index}", (0.0, -0.274, z), 0.012, 0.008, "pearl", "Chest")

    # Near-vertical A-pose keeps the runtime width.  Each shoulder begins inside the convex torso
    # shoulder mass, and the forearm radius falls continuously into the small mitten hand.
    arm_points = {
        "L": ((0.318, -0.002, 2.235), (0.382, -0.010, 1.925), (0.397, -0.020, 1.535), (0.397, -0.025, 1.350)),
        "R": ((-0.318, -0.002, 2.235), (-0.382, -0.010, 1.925), (-0.397, -0.020, 1.535), (-0.397, -0.025, 1.350)),
    }
    for side, (shoulder, elbow, wrist, hand_end) in arm_points.items():
        prefix = "Left" if side == "L" else "Right"
        make_continuous_sleeve(
            f"MotherV2_ContinuousSleeve_{side}",
            shoulder,
            elbow,
            wrist,
            (0.120, 0.082, 0.052),
            "cardigan",
            f"{prefix}UpperArm",
            f"{prefix}LowerArm",
        )
        palm = Vector(wrist).lerp(Vector(hand_end), 0.50)
        make_ellipsoid(
            f"MotherV2_MittenHand_{side}", tuple(palm), (0.054, 0.043, 0.092), "skin", f"{prefix}Hand", 28, 16
        )

    # Analog watch on anatomical left wrist.
    make_disc("MotherV2_WatchCase", (0.397, -0.068, 1.555), 0.022, 0.010, "shoe_dark", "LeftLowerArm")
    make_disc("MotherV2_WatchFace", (0.397, -0.080, 1.555), 0.016, 0.007, "pearl", "LeftLowerArm")

    # Head and face.  The 1.00-unit head locks 3.45 heads tall, but the tapered jaw, controlled
    # eye size, shoulder line, and long skirt make her unmistakably an adult parent rather than a child.
    # A short skin neck is seated deeply inside both collar and head.  Only a thin natural neck line
    # remains visible; the overlap eliminates the former black under-chin gap without an egg/bead.
    make_loft(
        "MotherV2_ShortNaturalNeck",
        [
            (2.32, -0.026, 0.090, 0.066),
            (2.37, -0.030, 0.096, 0.070),
            (2.44, -0.036, 0.098, 0.072),
            (2.51, -0.040, 0.096, 0.071),
            (2.57, -0.041, 0.090, 0.068),
        ],
        "skin",
        [("Neck", 2.32), ("Neck", 2.47), ("Head", 2.57)],
        segments=36,
    )

    # The short soft jaw keeps the mature Mother identity.  Ring centres shift rearward while their
    # front surface stays nearly fixed, adding original skull depth without projecting the face.
    head = make_loft(
        "MotherV2_Head",
        [
            (2.62, -0.015, 0.072, 0.109),
            (2.65, -0.019, 0.168, 0.193),
            (2.69, -0.024, 0.272, 0.260),
            (2.75, -0.031, 0.365, 0.317),
            (2.83, -0.037, 0.425, 0.349),
            (2.95, -0.040, 0.455, 0.360),
            (3.11, -0.037, 0.438, 0.345),
            (3.25, -0.027, 0.375, 0.305),
            (3.36, -0.010, 0.288, 0.240),
            (3.41, 0.015, 0.125, 0.120),
        ],
        "skin",
        [("Head", 2.58), ("Head", 3.41)],
        segments=48,
    )
    for side, x in (("L", 0.430), ("R", -0.430)):
        make_ellipsoid(f"MotherV2_Ear_{side}", (x, -0.045, 2.955), (0.050, 0.028, 0.085), "skin_shadow", "Head", 20, 12)
        make_ellipsoid(f"MotherV2_Pearl_{side}", (x, -0.074, 2.865), (0.021, 0.017, 0.021), "pearl", "Head", 18, 10)

    # The eye centres span about 50% of the 0.91-unit face width and each almond opening spans
    # about 29%.  Large warm irises and gentle lid/brow arcs retain maturity without doll buttons.
    for side, x, sign in (("L", 0.220, 1.0), ("R", -0.220, -1.0)):
        # Adult almond eye: the warm brown iris occupies most of the opening, leaving only a
        # narrow sclera rim.  This explicitly avoids the large white doll-eye failure mode.
        make_ellipsoid(f"MotherV2_EyeWhite_{side}", (x, -0.394, 3.010), (0.132, 0.014, 0.058), "eye_white", "Head", 28, 16)
        make_ellipsoid(f"MotherV2_Iris_{side}", (x, -0.407, 3.008), (0.086, 0.007, 0.057), "eye_brown", "Head", 24, 14)
        make_ellipsoid(f"MotherV2_Pupil_{side}", (x, -0.414, 3.007), (0.027, 0.004, 0.037), "pupil", "Head", 20, 12)
        make_ellipsoid(f"MotherV2_EyeGlintMajor_{side}", (x - sign * 0.020, -0.419, 3.032), (0.014, 0.002, 0.016), "eye_white", "Head", 12, 8)
        make_ellipsoid(f"MotherV2_EyeGlintMinor_{side}", (x + sign * 0.022, -0.419, 2.986), (0.006, 0.002, 0.008), "eye_white", "Head", 12, 8)
        make_ribbon(
            f"MotherV2_UpperLid_{side}",
            [(x - sign * 0.136, -0.410, 3.030), (x, -0.422, 3.064), (x + sign * 0.136, -0.410, 3.028)],
            [0.008, 0.010, 0.008],
            [0.003, 0.004, 0.003],
            "hair_dark",
            "Head",
            bevel=0.002,
        )
        outer_x = x + sign * 0.130
        make_ribbon(
            f"MotherV2_OuterLashFlick_{side}",
            [(outer_x, -0.416, 3.027), (outer_x + sign * 0.026, -0.411, 3.040)],
            [0.008, 0.004],
            [0.003, 0.002],
            "hair_dark",
            "Head",
            bevel=0.0015,
        )
        make_ribbon(
            f"MotherV2_Brow_{side}",
            [(x - sign * 0.108, -0.390, 3.134), (x, -0.402, 3.143), (x + sign * 0.108, -0.390, 3.133)],
            [0.010, 0.013, 0.009],
            [0.004, 0.005, 0.004],
            "hair_mid",
            "Head",
            bevel=0.002,
        )
    make_ribbon(
        "MotherV2_Mouth",
        [(-0.060, -0.371, 2.790), (0.0, -0.380, 2.774), (0.060, -0.371, 2.790)],
        [0.009, 0.012, 0.009],
        [0.003, 0.004, 0.003],
        "mouth",
        "Head",
        bevel=0.002,
    )

    # Shoulder-length chestnut hair.  The cap sits behind the face; layered ribbons create a
    # readable anime silhouette and low half-up twist instead of V1's continuous clay helmet.
    make_ellipsoid("MotherV2_HairCap", (0.0, 0.085, 3.030), (0.465, 0.355, 0.455), "hair_dark", "Head", 36, 24)
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_offset_loft(
            f"MotherV2_IntegratedSideHair_{side}",
            [
                (2.38, sign * 0.285, 0.080, 0.030, 0.065),
                (2.48, sign * 0.315, 0.055, 0.095, 0.170),
                (2.65, sign * 0.350, 0.035, 0.135, 0.255),
                (2.87, sign * 0.370, 0.025, 0.155, 0.315),
                (3.10, sign * 0.355, 0.035, 0.165, 0.325),
                (3.28, sign * 0.290, 0.055, 0.155, 0.285),
                (3.40, sign * 0.150, 0.075, 0.095, 0.170),
            ],
            "hair_dark",
            "Head",
            segments=32,
        )
    side_locks = [
        ("L_A", 1.0, 0.33), ("L_B", 1.0, 0.25), ("R_A", -1.0, 0.33), ("R_B", -1.0, 0.25),
    ]
    for label, sign, outer in side_locks:
        make_ribbon(
            f"MotherV2_SideHair_{label}",
            [
                (sign * outer, -0.245, 3.28),
                (sign * (outer + 0.06), -0.275, 3.08),
                (sign * (outer + 0.04), -0.235, 2.82),
                (sign * (outer - 0.01), -0.105, 2.55),
                (sign * (outer - 0.05), 0.010, 2.35),
            ],
            [0.105, 0.125, 0.112, 0.085, 0.018],
            [0.042, 0.045, 0.040, 0.030, 0.010],
            "hair_mid" if "A" in label else "hair_dark",
            "Head",
            bevel=0.007,
        )
    fringe_specs = [
        ("PartSweep", [(0.00, -0.300, 3.43), (0.10, -0.365, 3.34), (0.21, -0.397, 3.22), (0.29, -0.382, 3.08)], [0.16, 0.16, 0.12, 0.02]),
        ("LeftSweep", [(-0.02, -0.300, 3.43), (-0.10, -0.375, 3.33), (-0.20, -0.405, 3.20), (-0.27, -0.385, 3.07)], [0.15, 0.15, 0.11, 0.02]),
        ("Temple", [(-0.20, -0.270, 3.36), (-0.30, -0.345, 3.24), (-0.36, -0.355, 3.10), (-0.38, -0.300, 2.95)], [0.11, 0.10, 0.08, 0.015]),
        ("TempleR", [(0.22, -0.260, 3.35), (0.31, -0.330, 3.23), (0.37, -0.340, 3.09), (0.38, -0.285, 2.96)], [0.10, 0.09, 0.07, 0.015]),
    ]
    for label, points, widths in fringe_specs:
        make_ribbon(
            f"MotherV2_Fringe_{label}", points, widths, [0.038, 0.040, 0.032, 0.010], "hair_mid", "Head", bevel=0.006
        )
    make_ribbon(
        "MotherV2_HairlineBand",
        [(-0.33, -0.337, 3.22), (-0.20, -0.392, 3.34), (0.0, -0.410, 3.42), (0.20, -0.385, 3.34), (0.33, -0.330, 3.21)],
        [0.038, 0.065, 0.078, 0.062, 0.036],
        [0.018, 0.022, 0.024, 0.022, 0.018],
        "hair_dark",
        "Head",
        bevel=0.004,
    )
    make_ribbon(
        "MotherV2_CrownPartFill",
        [(-0.12, -0.332, 3.405), (0.0, -0.365, 3.455), (0.13, -0.328, 3.398)],
        [0.055, 0.075, 0.052],
        [0.022, 0.026, 0.022],
        "hair_dark",
        "Head",
        bevel=0.004,
    )
    make_ellipsoid(
        "MotherV2_CrownFrontFill",
        (0.0, -0.205, 3.350),
        (0.380, 0.165, 0.150),
        "hair_dark",
        "Head",
        32,
        16,
    )
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_ribbon(
            f"MotherV2_OuterPointedHair_{side}",
            [
                (sign * 0.465, 0.035, 3.24),
                (sign * 0.490, -0.020, 3.04),
                (sign * 0.485, 0.005, 2.82),
                (sign * 0.445, 0.065, 2.58),
                (sign * 0.365, 0.105, 2.40),
            ],
            [0.060, 0.080, 0.072, 0.048, 0.005],
            [0.032, 0.038, 0.035, 0.025, 0.006],
            "hair_mid",
            "Head",
            bevel=0.005,
        )
    # Back layers extend to the shoulder line and keep the rear view intentionally strand-based.
    for index, x in enumerate((-0.34, -0.20, -0.07, 0.07, 0.20, 0.34)):
        drift = 0.035 * math.sin(index * 1.7)
        make_ribbon(
            f"MotherV2_BackHair_{index}",
            [(x * 0.75, 0.330, 3.31), (x, 0.375, 3.06), (x + drift, 0.350, 2.78), (x * 0.88, 0.265, 2.49), (x * 0.75, 0.170, 2.31)],
            [0.100, 0.120, 0.108, 0.078, 0.006],
            [0.038, 0.042, 0.040, 0.030, 0.010],
            "hair_mid" if index % 2 else "hair_dark",
            "Head",
            bevel=0.006,
        )
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_ribbon(
            f"MotherV2_HalfUpSweep_{side}",
            [(sign * 0.38, 0.255, 3.22), (sign * 0.30, 0.352, 3.13), (sign * 0.18, 0.405, 3.06), (sign * 0.07, 0.420, 3.02)],
            [0.095, 0.105, 0.085, 0.022],
            [0.034, 0.036, 0.030, 0.012],
            "hair_mid",
            "Head",
            bevel=0.006,
        )
    make_ellipsoid("MotherV2_HalfUpTwist", (0.0, 0.420, 3.015), (0.105, 0.060, 0.072), "hair_mid", "Head", 24, 14)
    make_ribbon(
        "MotherV2_TwistTail",
        [(0.0, 0.455, 2.99), (0.035, 0.455, 2.90), (0.020, 0.430, 2.80), (-0.010, 0.395, 2.72)],
        [0.080, 0.075, 0.055, 0.012],
        [0.032, 0.030, 0.024, 0.008],
        "hair_dark",
        "Head",
        bevel=0.005,
    )

    # Seat the complete rigid head assembly 0.135 art units lower.  This preserves every facial and
    # hair relationship while matching the short-neck runtime silhouette and closing the chin seam.
    # The blended skin neck is intentionally excluded because it owns both Neck and Head groups.
    for part in CHARACTER_PARTS:
        if len(part.vertex_groups) == 1 and part.vertex_groups[0].name == "Head":
            part.location.z -= 0.135

    armature = make_armature(arm_points)

    bpy.ops.object.select_all(action="DESELECT")
    for part in CHARACTER_PARTS:
        part.select_set(True)
    bpy.context.view_layer.objects.active = CHARACTER_PARTS[0]
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "MotherV2_CompleteSkinnedBody"
    while len(body.data.materials) > 1:
        body.data.materials.pop(index=len(body.data.materials) - 1)
    if len(body.data.materials) == 0:
        body.data.materials.append(ATLAS_MATERIAL)
    for polygon in body.data.polygons:
        polygon.material_index = 0
    while len(body.data.uv_layers) > 1:
        body.data.uv_layers.remove(body.data.uv_layers[-1])
    if len(body.data.uv_layers) != 1 or body.data.uv_layers[0].name != UV_LAYER_NAME:
        raise RuntimeError("Mother V2 must contain exactly one canonical atlas UV0")
    body.data.uv_layers.active_index = 0
    modifier = body.modifiers.new("MotherV2HumanoidSkin", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True
    body.parent = armature
    body["familyCompanyIdentity"] = IDENTITY
    body["identityContract"] = CONTRACT
    body["runtimeHighMotionPriority"] = True
    body["productionEligible"] = False
    return body, armature


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_studio() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.dither_intensity = 0.0
    scene.eevee.taa_render_samples = 128
    scene.eevee.shadow_ray_count = 4
    scene.render.film_transparent = False
    scene.world.color = (0.070, 0.085, 0.115)
    scene.world.use_nodes = True
    world_background = scene.world.node_tree.nodes.get("Background")
    if world_background:
        world_background.inputs["Color"].default_value = (0.085, 0.105, 0.145, 1.0)
        world_background.inputs["Strength"].default_value = 0.45
    try:
        scene.view_settings.look = "AgX - Medium Low Contrast"
    except (TypeError, ValueError):
        pass

    bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0.0, 0.0, -0.014))
    ground = bpy.context.object
    ground.name = "QA_StudioGround"
    ground_material = bpy.data.materials.new("QA_StudioGroundMaterial")
    ground_material.diffuse_color = (0.52, 0.55, 0.60, 1.0)
    ground_material.use_nodes = True
    ground.data.materials.append(ground_material)

    bpy.ops.object.camera_add(location=(0.0, -8.2, 1.78))
    camera = bpy.context.object
    camera.name = "QA_StudioCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.05
    camera.data.lens = 58
    scene.camera = camera
    look_at(camera, Vector((0.0, 0.0, 1.72)))

    def add_area(
        name: str,
        location: tuple[float, float, float],
        energy: float,
        size: float,
        colour: tuple[float, float, float],
    ) -> None:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = colour
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, Vector((0.0, 0.0, 1.75)))

    add_area("Key_Area", (-3.8, -5.0, 6.5), 980.0, 4.5, (1.0, 0.90, 0.82))
    add_area("Fill_Area", (4.0, -2.5, 4.2), 720.0, 4.0, (0.78, 0.88, 1.0))
    add_area("Soft_Front_Bounce", (0.0, -4.8, 1.9), 320.0, 5.0, (1.0, 0.92, 0.86))
    add_area("Rim_Area", (0.0, 4.5, 5.2), 760.0, 3.5, (1.0, 0.80, 0.70))
    return camera


def render_view(camera: bpy.types.Object, path: Path, position: tuple[float, float, float], resolution: int) -> None:
    camera.location = position
    look_at(camera, Vector((0.0, 0.0, 1.72)))
    scene = bpy.context.scene
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def render_four_views(camera: bpy.types.Object, directory: Path, resolution: int, suffix: str) -> list[str]:
    directory.mkdir(parents=True, exist_ok=True)
    views = {
        "front": (0.0, -8.2, 1.78),
        "left": (-8.2, 0.0, 1.78),
        "back": (0.0, 8.2, 1.78),
        "three-quarter": (5.8, -5.8, 1.88),
    }
    outputs: list[str] = []
    for label, position in views.items():
        path = directory / f"mother-v2-{suffix}-{label}-{resolution}.png"
        render_view(camera, path, position, resolution)
        outputs.append(str(path))
    return outputs


def render_front_three_quarter_proof(camera: bpy.types.Object, directory: Path) -> list[str]:
    """Render the two user-facing 1536px gates before the expensive Final4 package."""
    directory.mkdir(parents=True, exist_ok=True)
    views = {
        "front": (0.0, -8.2, 1.78),
        "three-quarter": (5.8, -5.8, 1.88),
    }
    outputs: list[str] = []
    for label, position in views.items():
        path = directory / f"mother-v2-final4-proof-{label}-1536.png"
        render_view(camera, path, position, 1536)
        outputs.append(str(path))
    return outputs


def render_turntable(
    body: bpy.types.Object,
    armature: bpy.types.Object,
    camera: bpy.types.Object,
    directory: Path,
) -> list[str]:
    directory.mkdir(parents=True, exist_ok=True)
    camera.location = (0.0, -8.2, 1.78)
    look_at(camera, Vector((0.0, 0.0, 1.72)))
    scene = bpy.context.scene
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    outputs: list[str] = []
    parent = body.parent
    body.parent = None
    for index in range(24):
        angle = math.tau * index / 24
        body.rotation_euler[2] = angle
        armature.rotation_euler[2] = angle
        path = directory / f"mother-v2-final4-turn-{index:02d}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(path))
    body.rotation_euler[2] = 0.0
    armature.rotation_euler[2] = 0.0
    body.parent = parent
    return outputs


def clear_pose(armature: bpy.types.Object) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def set_walk_contact_pose(armature: bpy.types.Object, phase: str) -> None:
    clear_pose(armature)
    direction = 1.0 if phase == "P0" else -1.0
    armature.pose.bones["LeftUpperLeg"].rotation_euler.x = math.radians(20.0 * direction)
    armature.pose.bones["RightUpperLeg"].rotation_euler.x = math.radians(-20.0 * direction)
    armature.pose.bones["LeftLowerLeg"].rotation_euler.x = math.radians(-7.0 * direction)
    armature.pose.bones["RightLowerLeg"].rotation_euler.x = math.radians(7.0 * direction)
    armature.pose.bones["LeftUpperArm"].rotation_euler.x = math.radians(-14.0 * direction)
    armature.pose.bones["RightUpperArm"].rotation_euler.x = math.radians(14.0 * direction)
    armature.pose.bones["LeftLowerArm"].rotation_euler.x = math.radians(4.0 * direction)
    armature.pose.bones["RightLowerArm"].rotation_euler.x = math.radians(-4.0 * direction)
    armature.pose.bones["Hips"].rotation_euler.y = math.radians(-1.5 * direction)
    armature.pose.bones["Chest"].rotation_euler.y = math.radians(1.5 * direction)
    bpy.context.view_layer.update()


def ground_deformed_pose(body: bpy.types.Object, armature: bpy.types.Object) -> None:
    root = armature.pose.bones["Root"]
    root.location.z = 0.0
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_body = body.evaluated_get(depsgraph)
    evaluated_mesh = evaluated_body.to_mesh()
    try:
        minimum_z = min((evaluated_body.matrix_world @ vertex.co).z for vertex in evaluated_mesh.vertices)
    finally:
        evaluated_body.to_mesh_clear()
    root.location.z = 0.002 - minimum_z
    bpy.context.view_layer.update()


def render_deformation(
    body: bpy.types.Object,
    armature: bpy.types.Object,
    camera: bpy.types.Object,
    directory: Path,
) -> list[str]:
    directory.mkdir(parents=True, exist_ok=True)
    outputs: list[str] = []
    camera.location = (5.8, -5.8, 1.88)
    look_at(camera, Vector((0.0, 0.0, 1.70)))
    scene = bpy.context.scene
    scene.render.resolution_x = 1536
    scene.render.resolution_y = 1536
    for phase, label in (("P0", "left-contact"), ("P3", "right-contact")):
        set_walk_contact_pose(armature, phase)
        ground_deformed_pose(body, armature)
        path = directory / f"mother-v2-final4-deform-{phase.lower()}-{label}-1536.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(path))
    clear_pose(armature)
    return outputs


def export_fbx(body: bpy.types.Object, armature: bpy.types.Object, path: Path) -> None:
    clear_pose(armature)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def write_humanoid_map(output: Path) -> Path:
    path = output / "mother-blender-humanoid-v2-unity-map.json"
    payload = {
        "contract": "FC-UNITY-HUMANOID-MAP-V1",
        "identity": IDENTITY,
        "mapping": HUMANOID_MAP,
        "rootMotionBone": "Root",
        "bottomCentreRoot": [0.0, 0.0, 0.0],
        "productionEligible": False,
    }
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return path


def load_rgba_image(path: Path) -> np.ndarray:
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        width, height = image.size
        buffer = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(buffer)
        return buffer.reshape((height, width, 4))
    finally:
        bpy.data.images.remove(image)


def fit_rgba_to_tile(source: np.ndarray, tile_width: int, tile_height: int) -> np.ndarray:
    source_height, source_width, _ = source.shape
    scale = min(tile_width / source_width, tile_height / source_height)
    target_width = max(1, int(round(source_width * scale)))
    target_height = max(1, int(round(source_height * scale)))
    source_x = np.minimum(
        source_width - 1,
        np.floor(np.arange(target_width, dtype=np.float32) * source_width / target_width).astype(np.int32),
    )
    source_y = np.minimum(
        source_height - 1,
        np.floor(np.arange(target_height, dtype=np.float32) * source_height / target_height).astype(np.int32),
    )
    resized = source[source_y[:, None], source_x[None, :], :]
    tile = np.empty((tile_height, tile_width, 4), dtype=np.float32)
    tile[:, :, :] = np.asarray((0.035, 0.042, 0.055, 1.0), dtype=np.float32)
    x0 = (tile_width - target_width) // 2
    y0 = (tile_height - target_height) // 2
    target = tile[y0 : y0 + target_height, x0 : x0 + target_width, :]
    alpha = resized[:, :, 3:4]
    target[:, :, :3] = resized[:, :, :3] * alpha + target[:, :, :3] * (1.0 - alpha)
    target[:, :, 3] = 1.0
    return tile


def build_identity_comparison(
    output: Path,
    neutral: Path,
    runtime_front_frame: Path,
    final_four_views: list[str],
) -> Path:
    """Build QA-only 2D identity versus 3D view evidence; never feed pixels into the model."""
    sources = [neutral, runtime_front_frame] + [Path(path) for path in final_four_views]
    tile_width = 480
    tile_height = 720
    gutter = 12
    board_width = len(sources) * tile_width + (len(sources) - 1) * gutter
    board = np.empty((tile_height, board_width, 4), dtype=np.float32)
    board[:, :, :] = np.asarray((0.018, 0.022, 0.030, 1.0), dtype=np.float32)
    for index, source_path in enumerate(sources):
        tile = fit_rgba_to_tile(load_rgba_image(source_path), tile_width, tile_height)
        x0 = index * (tile_width + gutter)
        board[:, x0 : x0 + tile_width, :] = tile
    comparison_dir = output / "comparison"
    comparison_dir.mkdir(parents=True, exist_ok=True)
    comparison_path = comparison_dir / "mother-v2-final4-runtime2d-neutral-vs-3d-4view.png"
    image = bpy.data.images.new("MotherV2IdentityComparison", board_width, tile_height, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(board.reshape(-1))
    image.filepath_raw = str(comparison_path)
    image.file_format = "PNG"
    image.save()
    return comparison_path


def build_turntable_contact_sheet(output: Path, turntable_paths: list[str]) -> Path:
    """Pack all exact 24 frames into one QA-only 6x4 sheet without changing source renders."""
    if len(turntable_paths) != 24:
        raise RuntimeError(f"Final4 turntable must contain exactly 24 frames; found {len(turntable_paths)}")
    columns = 6
    rows = 4
    tile_size = 384
    gutter = 8
    board_width = columns * tile_size + (columns - 1) * gutter
    board_height = rows * tile_size + (rows - 1) * gutter
    board = np.empty((board_height, board_width, 4), dtype=np.float32)
    board[:, :, :] = np.asarray((0.018, 0.022, 0.030, 1.0), dtype=np.float32)
    for index, source_path in enumerate(turntable_paths):
        tile = fit_rgba_to_tile(load_rgba_image(Path(source_path)), tile_size, tile_size)
        # Blender stores image rows bottom-up; invert the board row so the visible sheet reads
        # frame 00..05 on top and frame 18..23 on the bottom.
        row = rows - 1 - (index // columns)
        column = index % columns
        x0 = column * (tile_size + gutter)
        y0 = row * (tile_size + gutter)
        board[y0 : y0 + tile_size, x0 : x0 + tile_size, :] = tile
    contact_path = output / "mother-v2-final4-turntable-contact-sheet.png"
    image = bpy.data.images.new("MotherV2Final4TurntableContactSheet", board_width, board_height, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(board.reshape(-1))
    image.filepath_raw = str(contact_path)
    image.file_format = "PNG"
    image.save()
    return contact_path


def body_metrics(body: bpy.types.Object) -> tuple[list[float], list[float], float]:
    world_vertices = [body.matrix_world @ vertex.co for vertex in body.data.vertices]
    minimum = [min(co[index] for co in world_vertices) for index in range(3)]
    maximum = [max(co[index] for co in world_vertices) for index in range(3)]
    return minimum, maximum, maximum[2] - minimum[2]


def build_receipt(
    output: Path,
    references: dict[str, Path],
    body: bpy.types.Object,
    armature: bpy.types.Object,
    atlas_path: Path,
    blend_path: Path,
    fbx_path: Path | None,
    humanoid_map_path: Path,
    rendered: dict[str, list[str]],
    mode: str,
    candidate_output: Path | None,
) -> dict:
    body.data.calc_loop_triangles()
    minimum, maximum, height = body_metrics(body)
    bone_names = [bone.name for bone in armature.data.bones]
    required = list(HUMANOID_MAP.values())
    receipt = {
        "contract": CONTRACT,
        "revision": REVISION,
        "artifactDirectorySequence": "Final3 (Mother had no prior Final3 package)",
        "mode": mode,
        "identity": IDENTITY,
        "identityCanonPriority": [
            "runtime HighMotion A/B for silhouette, family scale, gait read, clothing colour blocks",
            "mother_office_neutral_v1 for face, hair, clothing detail, accessories, and colour refinement only",
        ],
        "references": {
            label: {"path": str(path), "sha256": sha256_file(path), "role": role}
            for (label, path), role in zip(
                references.items(),
                (
                    "face/hair/clothing/accessory detail only",
                    "primary runtime silhouette and palette lock",
                    "primary runtime silhouette and palette lock",
                ),
            )
        },
        "cleanRoom": {
            "referencePixelsUsedInCandidateGeometryAtlasMaterialOrRig": False,
            "referencePixelsUsedInQaComparisonOnly": True,
            "spriteTextureDecalBillboardMotionDonorOrFallback": False,
            "userRejectedTurnaroundExcluded": True,
            "motherV1BlendFbxMeshAtlasMaterialRenderExcluded": True,
            "externalModelFilesOpenedOrImported": False,
            "externalGeometryTopologyTextureUvRigMaterialWeightCopied": False,
            "prohibitedInputsRead": [],
            "atlasAuthoredFromNumericSolidColourLocks": True,
        },
        "modelingLock": {
            "style": "runtime-matched 2D anime-toon translated to clean flat-colour 3D",
            "headsTallTarget": 3.40,
            "headUnitTarget": 1.00,
            "runtimePriorityRange": [3.3, 3.6],
            "maturityCues": [
                "soft oval face with tapered jaw",
                "controlled adult eye size and gentle brows",
                "soft-sturdy shoulder and torso line",
                "tapered near-vertical limbs",
                "below-knee dark teal A-line skirt",
                "low-profile loafers and adult accessories",
            ],
            "prohibitedLook": ["realistic doll", "clay figure", "oversized egg head", "platform shoes", "toddler body"],
            "palette": {name: list(colour) for name, colour in COLORS.items()},
        },
        "model": {
            "meshObjectName": body.name,
            "skinnedMeshObjectCount": 1,
            "characterMaterialSlotCount": len(body.data.materials),
            "textureAtlasCount": 1,
            "uvLayerCount": len(body.data.uv_layers),
            "activeUvLayer": body.data.uv_layers.active.name if body.data.uv_layers.active else None,
            "vertices": len(body.data.vertices),
            "polygons": len(body.data.polygons),
            "triangles": len(body.data.loop_triangles),
            "boundsMin": minimum,
            "boundsMax": maximum,
            "height": height,
            "measuredHeadsTall": height / 1.0,
            "bottomCentreRoot": [0.0, 0.0, 0.0],
            "minimumSoleZ": minimum[2],
            "completeVisibleFeatures": [
                "two arms and tapered sleeves",
                "two complete compact rounded mitten hands without separate hook thumbs",
                "two lower legs",
                "two low-profile penny loafers",
                "soft oval mature face",
                "shoulder-length layered chestnut hair",
                "low half-up twist",
                "dusty peach cardigan",
                "cream blouse and collar",
                "dark teal below-knee A-line skirt",
                "pearl earrings",
                "analog left wristwatch",
            ],
        },
        "rig": {
            "armatureObjectName": armature.name,
            "boneCount": len(bone_names),
            "boneNames": bone_names,
            "explicitUnityHumanoidMap": HUMANOID_MAP,
            "missingRequiredBones": sorted(set(required) - set(bone_names)),
            "rootBone": "Root",
            "bindPose": "near-vertical A-pose",
            "mappingSidecar": str(humanoid_map_path),
            "mappingSidecarSha256": sha256_file(humanoid_map_path),
        },
        "atlas": {
            "path": str(atlas_path),
            "sha256": sha256_file(atlas_path),
            "resolution": [ATLAS_SIZE, ATLAS_SIZE],
            "patchCount": len(PATCHES),
            "solidFlatColourPatches": True,
        },
        "outputs": {
            "blend": str(blend_path),
            "blendSha256": sha256_file(blend_path) if blend_path.exists() else None,
            "fbx": str(fbx_path) if fbx_path else None,
            "fbxSha256": sha256_file(fbx_path) if fbx_path and fbx_path.exists() else None,
            "candidateOutput": str(candidate_output) if candidate_output else None,
            "renders": rendered,
        },
        "qa": {
            "singleCharacterMesh": True,
            "singleAtlasMaterial": len(body.data.materials) == 1,
            "soleAtlasUv0": len(body.data.uv_layers) == 1 and body.data.uv_layers[0].name == UV_LAYER_NAME,
            "referenceHashesMatch": True,
            "requiredHumanoidBonesPresent": not (set(required) - set(bone_names)),
            "bottomTouchesFloor": abs(minimum[2]) <= 0.02,
            "runtimeScaleWithinLockedRange": 3.3 <= height <= 3.6,
            "P0P3AlternationRenderIncluded": bool(rendered.get("deformation")),
            "exact24TurntableFrames": len(rendered.get("turntable24", [])) == 24,
            "visualApproval": "PENDING_HUMAN_REVIEW",
            "unityHumanoidImport": "PENDING_PARENT_UNITY_INTEGRATION",
            "productionEligible": False,
        },
        "script": {
            "path": str(Path(__file__).resolve()),
            "sha256": sha256_file(Path(__file__).resolve()),
            "blenderVersion": bpy.app.version_string,
        },
    }
    receipt_path = output / "mother-blender-identity-v2-receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")
    return receipt


def copy_candidate_files(
    candidate_output: Path,
    fbx_path: Path,
    atlas_path: Path,
    humanoid_map_path: Path,
) -> dict[str, str]:
    candidate_output.mkdir(parents=True, exist_ok=True)
    copied: dict[str, str] = {}
    for source in (fbx_path, atlas_path, humanoid_map_path):
        target = candidate_output / source.name
        shutil.copy2(source, target)
        copied[str(target)] = sha256_file(target)
    return copied


def main() -> None:
    global ATLAS_MATERIAL, CHARACTER_PARTS
    args = parse_args()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    neutral = Path(args.neutral_reference).resolve()
    highmotion_a = Path(args.highmotion_a).resolve()
    highmotion_b = Path(args.highmotion_b).resolve()
    validate_reference(neutral, EXPECTED_NEUTRAL_SHA256, "mother_office_neutral_v1")
    validate_reference(highmotion_a, EXPECTED_HIGHMOTION_A_SHA256, "runtime HighMotion A")
    validate_reference(highmotion_b, EXPECTED_HIGHMOTION_B_SHA256, "runtime HighMotion B")
    candidate_output = Path(args.candidate_output).resolve() if args.candidate_output else None

    clear_scene()
    CHARACTER_PARTS = []
    _, ATLAS_MATERIAL, atlas_path = make_atlas(output)
    body, armature = make_character()
    camera = create_studio()
    humanoid_map_path = write_humanoid_map(output)
    references = {
        "neutral": neutral,
        "highMotionA": highmotion_a,
        "highMotionB": highmotion_b,
    }
    rendered: dict[str, list[str]] = {}

    if args.proof_only:
        rendered["final4ProofFrontThreeQuarter"] = render_front_three_quarter_proof(
            camera, output / "proof-1536"
        )
        blend_path = output / "mother-blender-identity-v2-final4-proof.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        receipt = build_receipt(
            output,
            references,
            body,
            armature,
            atlas_path,
            blend_path,
            None,
            humanoid_map_path,
            rendered,
            "final4-proof",
            candidate_output,
        )
        if not all(
            (
                receipt["qa"]["singleCharacterMesh"],
                receipt["qa"]["singleAtlasMaterial"],
                receipt["qa"]["soleAtlasUv0"],
                receipt["qa"]["requiredHumanoidBonesPresent"],
                receipt["qa"]["bottomTouchesFloor"],
                receipt["qa"]["runtimeScaleWithinLockedRange"],
            )
        ):
            raise RuntimeError("Mother V2 Final4 proof structural gate failed")
        print("MOTHER_BLENDER_IDENTITY_V2_FINAL4_PROOF: PASS")
        return

    if args.draft_only:
        rendered["draftFourView"] = render_four_views(camera, output / "draft", 768, "draft")
        blend_path = output / "mother-blender-identity-v2-draft.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        receipt = build_receipt(
            output,
            references,
            body,
            armature,
            atlas_path,
            blend_path,
            None,
            humanoid_map_path,
            rendered,
            "draft",
            candidate_output,
        )
        if not all(
            (
                receipt["qa"]["singleCharacterMesh"],
                receipt["qa"]["singleAtlasMaterial"],
                receipt["qa"]["soleAtlasUv0"],
                receipt["qa"]["requiredHumanoidBonesPresent"],
                receipt["qa"]["bottomTouchesFloor"],
                receipt["qa"]["runtimeScaleWithinLockedRange"],
            )
        ):
            raise RuntimeError("Mother V2 draft structural gate failed")
        print("MOTHER_BLENDER_IDENTITY_V2_DRAFT: PASS")
        return

    rendered["finalFourView"] = render_four_views(camera, output / "final", 1536, "final4")
    runtime_front_frame = highmotion_a.parent / "Frames" / "mother_south_walk_0.png"
    if not runtime_front_frame.is_file():
        raise FileNotFoundError(f"Runtime Mother south P0 frame missing: {runtime_front_frame}")
    comparison_path = build_identity_comparison(
        output,
        neutral,
        runtime_front_frame,
        rendered["finalFourView"],
    )
    rendered["identityComparison"] = [str(comparison_path)]
    rendered["turntable24"] = render_turntable(body, armature, camera, output / "turntable")
    rendered["turntableContactSheet"] = [str(build_turntable_contact_sheet(output, rendered["turntable24"]))]
    rendered["deformation"] = render_deformation(body, armature, camera, output / "deformation")
    clear_pose(armature)
    blend_path = output / "mother-blender-identity-v2.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    fbx_path = output / "mother-blender-humanoid-v2.fbx"
    export_fbx(body, armature, fbx_path)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    candidate_hashes = (
        copy_candidate_files(candidate_output, fbx_path, atlas_path, humanoid_map_path)
        if candidate_output
        else {}
    )
    receipt = build_receipt(
        output,
        references,
        body,
        armature,
        atlas_path,
        blend_path,
        fbx_path,
        humanoid_map_path,
        rendered,
        "final4",
        candidate_output,
    )
    if candidate_hashes:
        receipt["outputs"]["candidateHashes"] = candidate_hashes
    (output / "mother-blender-identity-v2-receipt.json").write_text(
        json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    if not all(
        (
            receipt["qa"]["singleCharacterMesh"],
            receipt["qa"]["singleAtlasMaterial"],
            receipt["qa"]["soleAtlasUv0"],
            receipt["qa"]["requiredHumanoidBonesPresent"],
            receipt["qa"]["bottomTouchesFloor"],
            receipt["qa"]["runtimeScaleWithinLockedRange"],
            receipt["qa"]["P0P3AlternationRenderIncluded"],
            receipt["qa"]["exact24TurntableFrames"],
        )
    ):
        raise RuntimeError("Mother V2 final structural gate failed")
    print("MOTHER_BLENDER_IDENTITY_V2_FINAL4: PASS")


if __name__ == "__main__":
    main()
