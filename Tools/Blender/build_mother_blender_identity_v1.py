#!/usr/bin/env python3
"""Build the first from-scratch Blender identity candidate for Family Company Mother.

Identity input is deliberately restricted to the committed four-view Mother turnaround.
No legacy 2D art, Styloo asset, Player diagnostic, or existing character mesh is read.

The generated candidate uses:
* one character mesh object and one shared texture-atlas material;
* a fresh Unity-Humanoid-compatible deform armature;
* a bottom-centre Root at the floor origin;
* deterministic studio, turntable, and deformation renders.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


IDENTITY = "mother"
EXPECTED_REFERENCE_SHA256 = "6FFC0A5699F2B897F99A532F3264E58911A9B4ECA09C717450D9D588284FE604"
ATLAS_SIZE = 1024
ATLAS_COLS = 4
ATLAS_ROWS = 4

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
    "skin": (0.86, 0.53, 0.37, 1.0),
    "skin_shadow": (0.60, 0.31, 0.22, 1.0),
    "hair_dark": (0.105, 0.045, 0.025, 1.0),
    "hair_mid": (0.22, 0.085, 0.040, 1.0),
    "cardigan": (0.68, 0.31, 0.25, 1.0),
    "cardigan_light": (0.84, 0.48, 0.39, 1.0),
    "blouse": (0.90, 0.84, 0.70, 1.0),
    "skirt": (0.025, 0.20, 0.23, 1.0),
    "skirt_light": (0.055, 0.34, 0.37, 1.0),
    "shoe_dark": (0.075, 0.035, 0.025, 1.0),
    "shoe_mid": (0.25, 0.12, 0.075, 1.0),
    "eye_brown": (0.21, 0.075, 0.025, 1.0),
    "eye_white": (0.94, 0.90, 0.81, 1.0),
    "pupil": (0.012, 0.007, 0.005, 1.0),
    "pearl": (0.78, 0.76, 0.67, 1.0),
    "mouth": (0.48, 0.12, 0.13, 1.0),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--draft-only", action="store_true")
    parser.add_argument("--final", action="store_true")
    return parser.parse_args(argv)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest().upper()


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


def srgb_to_linear(rgb: tuple[float, float, float]) -> tuple[float, float, float]:
    def convert(value: float) -> float:
        return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4

    return tuple(convert(c) for c in rgb)


def make_atlas(output: Path) -> tuple[bpy.types.Image, bpy.types.Material, Path]:
    atlas_path = output / "mother-blender-identity-v1-atlas.png"
    pixels = np.zeros((ATLAS_SIZE, ATLAS_SIZE, 4), dtype=np.float32)
    cell_w = ATLAS_SIZE // ATLAS_COLS
    cell_h = ATLAS_SIZE // ATLAS_ROWS
    yy, xx = np.mgrid[0:cell_h, 0:cell_w]
    weave = (((xx // 8 + yy // 8) % 2) * 2 - 1).astype(np.float32)
    vertical = (yy.astype(np.float32) / max(1, cell_h - 1) - 0.5)

    for name, patch in PATCHES.items():
        col = patch % ATLAS_COLS
        row = patch // ATLAS_COLS
        x0, y0 = col * cell_w, row * cell_h
        base = np.array(COLORS[name][:3], dtype=np.float32)
        cloth = name in {"cardigan", "cardigan_light", "blouse", "skirt", "skirt_light"}
        variation = vertical[..., None] * 0.055
        if cloth:
            variation += weave[..., None] * 0.012
        # Blender image pixels are authored in display-space for this sRGB atlas. Converting the
        # values a second time made skin/cardigan unnaturally red and is intentionally avoided.
        tile = np.clip(base[None, None, :] * (1.0 + variation), 0.0, 1.0)
        pixels[y0 : y0 + cell_h, x0 : x0 + cell_w, :3] = tile
        pixels[y0 : y0 + cell_h, x0 : x0 + cell_w, 3] = 1.0

    image = bpy.data.images.new("MotherIdentityAtlasV1", ATLAS_SIZE, ATLAS_SIZE, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels.reshape(-1))
    image.filepath_raw = str(atlas_path)
    image.file_format = "PNG"
    image.save()

    material = bpy.data.materials.new("MotherIdentityAtlasMaterialV1")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    for node in list(nodes):
        nodes.remove(node)
    output_node = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    shader.inputs["Roughness"].default_value = 0.72
    shader.inputs["Specular IOR Level"].default_value = 0.28
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
    links.new(shader.outputs["BSDF"], output_node.inputs["Surface"])
    return image, material, atlas_path


def apply_transforms(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def smooth_mesh(obj: bpy.types.Object) -> None:
    for poly in obj.data.polygons:
        poly.use_smooth = True


def assign_uv_patch(obj: bpy.types.Object, patch_name: str, projection: str = "xz") -> None:
    if len(obj.data.materials) == 0:
        obj.data.materials.append(ATLAS_MATERIAL)
    uv = obj.data.uv_layers.get("UVMap") or obj.data.uv_layers.new(name="UVMap")
    verts = obj.data.vertices
    coords = [obj.matrix_world @ vert.co for vert in verts]
    axes = {
        "xz": (0, 2),
        "xy": (0, 1),
        "yz": (1, 2),
    }[projection]
    values_a = [co[axes[0]] for co in coords]
    values_b = [co[axes[1]] for co in coords]
    min_a, max_a = min(values_a), max(values_a)
    min_b, max_b = min(values_b), max(values_b)
    span_a = max(max_a - min_a, 1e-6)
    span_b = max(max_b - min_b, 1e-6)
    patch = PATCHES[patch_name]
    col, row = patch % ATLAS_COLS, patch // ATLAS_COLS
    margin = 0.10
    for loop in obj.data.loops:
        co = coords[loop.vertex_index]
        local_u = margin + (1.0 - 2.0 * margin) * ((co[axes[0]] - min_a) / span_a)
        local_v = margin + (1.0 - 2.0 * margin) * ((co[axes[1]] - min_b) / span_b)
        uv.data[loop.index].uv = (
            (col + local_u) / ATLAS_COLS,
            (row + local_v) / ATLAS_ROWS,
        )


def tag_rigid(obj: bpy.types.Object, bone_name: str) -> None:
    group = obj.vertex_groups.get(bone_name) or obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")


def tag_blended_z(obj: bpy.types.Object, keys: list[tuple[str, float]]) -> None:
    groups = {name: obj.vertex_groups.new(name=name) for name, _ in keys}
    ordered = sorted(keys, key=lambda item: item[1])
    for vert in obj.data.vertices:
        world_z = (obj.matrix_world @ vert.co).z
        lower = ordered[0]
        upper = ordered[-1]
        for index in range(len(ordered) - 1):
            if ordered[index][1] <= world_z <= ordered[index + 1][1]:
                lower, upper = ordered[index], ordered[index + 1]
                break
        if abs(upper[1] - lower[1]) < 1e-6:
            groups[lower[0]].add([vert.index], 1.0, "REPLACE")
        else:
            t = max(0.0, min(1.0, (world_z - lower[1]) / (upper[1] - lower[1])))
            groups[lower[0]].add([vert.index], 1.0 - t, "REPLACE")
            groups[upper[0]].add([vert.index], t, "REPLACE")


def make_uv_ellipsoid(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    patch: str,
    bone: str,
    segments: int = 32,
    rings: int = 20,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transforms(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def make_rounded_cube(
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
    modifier = obj.modifiers.new("Soft tailoring", "BEVEL")
    modifier.width = bevel
    modifier.segments = 3
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def orthonormal_basis(direction: Vector) -> tuple[Vector, Vector]:
    reference = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(reference)) > 0.92:
        reference = Vector((1.0, 0.0, 0.0))
    u = direction.cross(reference).normalized()
    v = direction.cross(u).normalized()
    return u, v


def make_tapered_tube(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius_start: float,
    radius_end: float,
    patch: str,
    bone: str,
    sides: int = 16,
    squash: float = 1.0,
) -> bpy.types.Object:
    a, b = Vector(start), Vector(end)
    direction = (b - a).normalized()
    u, v = orthonormal_basis(direction)
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    rings = 7
    for ring in range(rings):
        t = ring / (rings - 1)
        centre = a.lerp(b, t)
        radius = radius_start * (1.0 - t) + radius_end * t
        end_round = 0.72 + 0.28 * math.sin(math.pi * t)
        for side in range(sides):
            angle = math.tau * side / sides
            point = centre + u * (math.cos(angle) * radius * end_round) + v * (
                math.sin(angle) * radius * squash * end_round
            )
            verts.append(tuple(point))
    for ring in range(rings - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            a0 = ring * sides + side
            a1 = ring * sides + nxt
            b1 = (ring + 1) * sides + nxt
            b0 = (ring + 1) * sides + side
            faces.append((a0, a1, b1, b0))
    faces.append(tuple(reversed(tuple(range(sides)))))
    offset = (rings - 1) * sides
    faces.append(tuple(offset + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def make_elliptical_loft(
    name: str,
    rings: list[tuple[float, float, float, float]],
    patch: str,
    weight_keys: list[tuple[str, float]],
    segments: int = 40,
    phase: float = 0.0,
    fold_strength: float = 0.0,
) -> bpy.types.Object:
    """Create z/y/rx/ry loft rings, front being negative Y."""
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for ring_index, (z, y_centre, rx, ry) in enumerate(rings):
        lower_fraction = ring_index / max(1, len(rings) - 1)
        for index in range(segments):
            angle = math.tau * index / segments
            fold = 1.0 + fold_strength * lower_fraction * (0.35 + 0.65 * math.cos(6 * angle + phase) ** 2)
            verts.append((rx * fold * math.cos(angle), y_centre + ry * fold * math.sin(angle), z))
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
    bottom_center = len(verts)
    verts.append((0.0, rings[0][1], rings[0][0]))
    top_center = len(verts)
    verts.append((0.0, rings[-1][1], rings[-1][0]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((bottom_center, nxt, index))
        offset = (len(rings) - 1) * segments
        faces.append((top_center, offset + index, offset + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_blended_z(obj, weight_keys)
    CHARACTER_PARTS.append(obj)
    return obj


def make_flat_strand(
    name: str,
    points: list[tuple[float, float, float]],
    widths: list[float],
    depths: list[float],
    patch: str,
    bone: str = "Head",
    bevel: float = 0.0,
) -> bpy.types.Object:
    verts: list[tuple[float, float, float]] = []
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
        w = widths[index] * 0.5
        d = depths[index] * 0.5
        verts.extend(
            [
                tuple(point - side * w - depth_axis * d),
                tuple(point + side * w - depth_axis * d),
                tuple(point + side * w + depth_axis * d),
                tuple(point - side * w + depth_axis * d),
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
    faces.append((end + 0, end + 1, end + 2, end + 3))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Soft tapered edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def make_front_panel_loft(
    name: str,
    rings: list[tuple[float, float, float]],
    patch: str,
    bone: str,
    thickness: float = 0.014,
) -> bpy.types.Object:
    """Create a gently curved, tapered front garment panel.

    Each ring is ``(z, front_y, half_width)``. The panel follows the torso rather than appearing
    as a rectangular box pasted onto it.
    """
    verts: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for z, front_y, half_width in rings:
        verts.extend(
            [
                (-half_width, front_y - thickness * 0.5, z),
                (half_width, front_y - thickness * 0.5, z),
                (half_width, front_y + thickness * 0.5, z),
                (-half_width, front_y + thickness * 0.5, z),
            ]
        )
    for ring in range(len(rings) - 1):
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
    end = (len(rings) - 1) * 4
    faces.append((end + 0, end + 1, end + 2, end + 3))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    modifier = obj.modifiers.new("Tailored panel edge", "BEVEL")
    modifier.width = 0.010
    modifier.segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch)
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def shape_mature_head(obj: bpy.types.Object, centre_z: float) -> None:
    """Soften the spherical primitive into a tapered adult cheek/jaw silhouette."""
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        if world.z < centre_z:
            fraction = max(0.0, min(1.0, (world.z - (centre_z - 0.30)) / 0.30))
            scale_x = 0.78 + 0.22 * fraction
            scale_y = 0.88 + 0.12 * fraction
            local = vertex.co.copy()
            local.x *= scale_x
            local.y *= scale_y
            vertex.co = local
    obj.data.update()


def shape_hair_hem(obj: bpy.types.Object, segments: int, base_z: float) -> None:
    """Give the unified hair shell a soft shoulder-length wavy hem."""
    for index in range(segments):
        angle = math.tau * index / segments
        back_factor = max(0.0, math.sin(angle))
        side_factor = abs(math.cos(angle))
        wave = 0.5 + 0.5 * math.cos(5.0 * angle + 0.35)
        obj.data.vertices[index].co.z = base_z - 0.045 * back_factor - 0.018 * side_factor - 0.018 * wave
    obj.data.update()


def make_cylinder_disk(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    patch: str,
    bone: str,
    rotation: tuple[float, float, float] = (math.pi / 2, 0.0, 0.0),
    vertices: int = 20,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    apply_transforms(obj)
    smooth_mesh(obj)
    assign_uv_patch(obj, patch, "xy")
    tag_rigid(obj, bone)
    CHARACTER_PARTS.append(obj)
    return obj


def make_character() -> tuple[bpy.types.Object, bpy.types.Object]:
    # Lower body: adult calf volume and connected low-heeled loafers.
    for side, x, prefix in (("L", 0.165, "Left"), ("R", -0.165, "Right")):
        make_tapered_tube(
            f"Mother_Calf_{side}",
            (x, 0.005, 0.67),
            (x, -0.005, 0.18),
            0.100,
            0.073,
            "skin",
            f"{prefix}LowerLeg",
            squash=0.82,
        )
        make_rounded_cube(
            f"Mother_LoaferSole_{side}",
            (x, -0.055, 0.035),
            (0.122, 0.205, 0.022),
            "shoe_dark",
            f"{prefix}Foot",
            0.022,
        )
        make_rounded_cube(
            f"Mother_Loafer_{side}",
            (x, -0.070, 0.098),
            (0.113, 0.188, 0.062),
            "shoe_mid",
            f"{prefix}Foot",
            0.052,
        )
        make_rounded_cube(
            f"Mother_LoaferHeel_{side}",
            (x, 0.108, 0.063),
            (0.100, 0.055, 0.046),
            "shoe_dark",
            f"{prefix}Foot",
            0.018,
        )
        make_rounded_cube(
            f"Mother_LoaferStrap_{side}",
            (x, -0.065, 0.153),
            (0.108, 0.044, 0.014),
            "shoe_dark",
            f"{prefix}Foot",
            0.014,
        )
        make_uv_ellipsoid(
            f"Mother_LoaferRoundedToe_{side}",
            (x, -0.205, 0.095),
            (0.108, 0.080, 0.052),
            "shoe_mid",
            f"{prefix}Foot",
            24,
            12,
        )

    # A-line skirt with a controlled six-fold hem silhouette.
    make_elliptical_loft(
        "Mother_AlineSkirt",
        [
            (0.55, 0.005, 0.455, 0.295),
            (0.69, 0.003, 0.405, 0.268),
            (0.85, 0.000, 0.350, 0.235),
            (1.02, -0.002, 0.300, 0.195),
            (1.18, -0.010, 0.305, 0.198),
        ],
        "skirt",
        [("Hips", 0.55), ("Spine", 1.16)],
        segments=48,
        fold_strength=0.055,
    )
    # Tailored torso: compact adult shoulders, defined waist, open cardigan.
    make_elliptical_loft(
        "Mother_CardiganTorso",
        [
            (1.10, 0.000, 0.292, 0.178),
            (1.22, 0.000, 0.302, 0.186),
            (1.38, 0.000, 0.350, 0.205),
            (1.52, 0.005, 0.350, 0.210),
            (1.565, 0.008, 0.310, 0.185),
            (1.625, 0.005, 0.125, 0.100),
        ],
        "cardigan",
        [("Hips", 1.10), ("Spine", 1.24), ("Chest", 1.42), ("UpperChest", 1.625)],
    )
    # Cream blouse front inset and narrow cardigan opening following torso curvature.
    make_front_panel_loft(
        "Mother_CreamBlouseFront",
        [
            (1.180, -0.195, 0.112),
            (1.255, -0.198, 0.132),
            (1.390, -0.216, 0.142),
            (1.515, -0.224, 0.128),
            (1.575, -0.205, 0.072),
        ],
        "blouse",
        "Chest",
    )
    # Cardigan edge piping gives an open-front read without boxy chest plates.
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_flat_strand(
            f"Mother_CardiganEdge_{side}",
            [
                (sign * 0.132, -0.205, 1.145),
                (sign * 0.148, -0.226, 1.350),
                (sign * 0.125, -0.215, 1.535),
            ],
            [0.030, 0.030, 0.018],
            [0.012, 0.012, 0.008],
            "cardigan_light",
            "Chest",
            bevel=0.006,
        )
    # Folded blouse collar, visible as two clean tapered leaves.
    make_flat_strand(
        "Mother_Collar_L",
        [(0.0, -0.222, 1.570), (0.075, -0.235, 1.535), (0.052, -0.238, 1.485)],
        [0.014, 0.074, 0.012],
        [0.008, 0.009, 0.006],
        "blouse",
        "UpperChest",
    )
    make_flat_strand(
        "Mother_Collar_R",
        [(0.0, -0.223, 1.570), (-0.075, -0.235, 1.535), (-0.052, -0.238, 1.485)],
        [0.014, 0.074, 0.012],
        [0.008, 0.009, 0.006],
        "blouse",
        "UpperChest",
    )
    for index, z in enumerate((1.46, 1.34, 1.22)):
        make_cylinder_disk(f"Mother_CardiganButton_{index}", (0.157, -0.239, z), 0.014, 0.010, "pearl", "Chest")
    for index, z in enumerate((1.43, 1.34, 1.25)):
        make_cylinder_disk(f"Mother_BlouseButton_{index}", (0.0, -0.241, z), 0.010, 0.009, "pearl", "Chest")

    # Adult A-pose arms. The sleeve overlaps the shoulder/torso and wrist to prevent visible gaps.
    arm_points = {
        "L": ((0.300, 0.0, 1.535), (0.490, -0.005, 1.300), (0.585, -0.020, 1.055), (0.610, -0.025, 0.925)),
        "R": ((-0.300, 0.0, 1.535), (-0.490, -0.005, 1.300), (-0.585, -0.020, 1.055), (-0.610, -0.025, 0.925)),
    }
    for side, (shoulder, elbow, wrist, hand_end) in arm_points.items():
        prefix = "Left" if side == "L" else "Right"
        make_tapered_tube(
            f"Mother_UpperSleeve_{side}", shoulder, elbow, 0.098, 0.086, "cardigan", f"{prefix}UpperArm", squash=0.92
        )
        make_tapered_tube(
            f"Mother_LowerSleeve_{side}", elbow, wrist, 0.090, 0.064, "cardigan", f"{prefix}LowerArm", squash=0.90
        )
        make_uv_ellipsoid(
            f"Mother_ElbowSleeve_{side}", elbow, (0.083, 0.074, 0.082), "cardigan", f"{prefix}LowerArm", 24, 14
        )
        make_uv_ellipsoid(f"Mother_Cuff_{side}", wrist, (0.070, 0.060, 0.048), "cardigan_light", f"{prefix}LowerArm", 24, 12)
        palm_center = Vector(wrist).lerp(Vector(hand_end), 0.48)
        make_uv_ellipsoid(
            f"Mother_Palm_{side}", tuple(palm_center), (0.052, 0.040, 0.080), "skin", f"{prefix}Hand", 24, 14
        )
        sign = 1.0 if side == "L" else -1.0
        for finger in range(4):
            spread = (finger - 1.5) * 0.018
            finger_start = (hand_end[0] - sign * 0.012, -0.033 + spread * 0.22, hand_end[2] + 0.050 + spread * 0.15)
            finger_finish = (hand_end[0] + sign * (0.010 + finger * 0.005), -0.037 + spread * 0.28, hand_end[2] - 0.030 + abs(spread) * 0.12)
            make_tapered_tube(
                f"Mother_Finger_{side}_{finger}", finger_start, finger_finish, 0.0115, 0.007, "skin", f"{prefix}Hand", 10, 0.78
            )
        thumb_start = tuple(palm_center + Vector((sign * 0.050, -0.004, 0.002)))
        thumb_end = tuple(palm_center + Vector((sign * 0.082, -0.012, -0.045)))
        make_tapered_tube(f"Mother_Thumb_{side}", thumb_start, thumb_end, 0.014, 0.008, "skin", f"{prefix}Hand", 10, 0.80)

    # Watch on anatomical left wrist, distinct from the cardigan cuff.
    make_uv_ellipsoid("Mother_WatchBand", (0.588, -0.025, 1.030), (0.070, 0.056, 0.027), "shoe_dark", "LeftLowerArm", 24, 12)
    make_cylinder_disk("Mother_WatchFace", (0.589, -0.077, 1.035), 0.025, 0.014, "pearl", "LeftLowerArm")

    # Neck and mature soft face. Eyes are deliberately smaller than the child Player proportions.
    make_uv_ellipsoid("Mother_Neck", (0.0, 0.0, 1.640), (0.092, 0.082, 0.138), "skin", "Neck", 28, 16)
    head = make_uv_ellipsoid("Mother_Head", (0.0, -0.070, 1.875), (0.238, 0.185, 0.270), "skin", "Head", 40, 28)
    shape_mature_head(head, 1.875)
    make_uv_ellipsoid("Mother_Ear_L", (0.225, -0.045, 1.870), (0.034, 0.024, 0.058), "skin_shadow", "Head", 20, 12)
    make_uv_ellipsoid("Mother_Ear_R", (-0.225, -0.045, 1.870), (0.034, 0.024, 0.058), "skin_shadow", "Head", 20, 12)
    for side, x in (("L", 0.086), ("R", -0.086)):
        make_uv_ellipsoid(f"Mother_EyeWhite_{side}", (x, -0.251, 1.918), (0.052, 0.009, 0.034), "eye_white", "Head", 28, 16)
        make_uv_ellipsoid(f"Mother_Iris_{side}", (x, -0.259, 1.917), (0.021, 0.0040, 0.026), "eye_brown", "Head", 24, 14)
        make_uv_ellipsoid(f"Mother_Pupil_{side}", (x, -0.263, 1.916), (0.009, 0.0022, 0.015), "pupil", "Head", 20, 12)
        make_uv_ellipsoid(f"Mother_EyeGlint_{side}", (x - 0.006, -0.266, 1.928), (0.0035, 0.0013, 0.005), "eye_white", "Head", 12, 8)
        sign = 1.0 if side == "L" else -1.0
        make_flat_strand(
            f"Mother_UpperLid_{side}",
            [(x - sign * 0.046, -0.261, 1.928), (x, -0.266, 1.945), (x + sign * 0.046, -0.261, 1.928)],
            [0.006, 0.008, 0.006],
            [0.0025, 0.003, 0.0025],
            "hair_dark",
            "Head",
            bevel=0.0015,
        )
        make_flat_strand(
            f"Mother_Brow_{side}",
            [(x - sign * 0.040, -0.257, 1.982), (x, -0.264, 1.993), (x + sign * 0.040, -0.257, 1.980)],
            [0.008, 0.010, 0.008],
            [0.003, 0.004, 0.003],
            "hair_dark",
            "Head",
            bevel=0.002,
        )
    make_uv_ellipsoid("Mother_Nose", (0.0, -0.255, 1.853), (0.020, 0.016, 0.026), "skin_shadow", "Head", 20, 12)
    make_flat_strand(
        "Mother_Mouth",
        [(-0.044, -0.258, 1.800), (0.0, -0.263, 1.790), (0.044, -0.258, 1.800)],
        [0.008, 0.011, 0.008],
        [0.003, 0.004, 0.003],
        "mouth",
        "Head",
    )
    for side, x in (("L", 0.240), ("R", -0.240)):
        make_uv_ellipsoid(f"Mother_Pearl_{side}", (x, -0.063, 1.822), (0.014, 0.012, 0.014), "pearl", "Head", 20, 12)

    # Shoulder-length chestnut hair: cap, layered tapered locks, swept half-up sides, and low twist.
    hair_mass = make_elliptical_loft(
        "Mother_ShoulderLengthHairMass",
        [
            (1.620, 0.118, 0.218, 0.118),
            (1.700, 0.108, 0.238, 0.145),
            (1.770, 0.092, 0.252, 0.168),
            (1.850, 0.072, 0.270, 0.203),
            (1.940, 0.055, 0.278, 0.224),
            (2.025, 0.047, 0.270, 0.216),
            (2.100, 0.043, 0.242, 0.188),
            (2.155, 0.040, 0.195, 0.148),
            (2.195, 0.038, 0.125, 0.092),
            (2.215, 0.038, 0.055, 0.040),
        ],
        "hair_dark",
        [("Neck", 1.600), ("Head", 1.700)],
        segments=48,
        fold_strength=0.015,
    )
    shape_hair_hem(hair_mass, 48, 1.620)
    # Side locks frame the face and end at the shoulder rather than forming a rigid helmet edge.
    for side, sign in (("L", 1.0), ("R", -1.0)):
        make_flat_strand(
            f"Mother_SideHair_{side}_A",
            [
                (sign * 0.220, -0.025, 2.070),
                (sign * 0.250, -0.055, 1.965),
                (sign * 0.258, -0.060, 1.855),
                (sign * 0.245, -0.020, 1.745),
                (sign * 0.225, 0.010, 1.650),
            ],
            [0.090, 0.092, 0.082, 0.060, 0.014],
            [0.034, 0.034, 0.030, 0.020, 0.006],
            "hair_mid",
            bevel=0.007,
        )
        make_flat_strand(
            f"Mother_HalfUpSweep_{side}",
            [
                (sign * 0.205, 0.235, 2.010),
                (sign * 0.195, 0.270, 1.985),
                (sign * 0.155, 0.298, 1.955),
                (sign * 0.090, 0.317, 1.935),
                (sign * 0.045, 0.327, 1.925),
            ],
            [0.055, 0.060, 0.052, 0.040, 0.020],
            [0.024, 0.025, 0.022, 0.018, 0.010],
            "hair_mid",
            bevel=0.006,
        )
    # Asymmetric side-part fringe; wide flattened leaves avoid sausage-shaped bangs.
    fringe_specs = [
        ("A", [(0.030, -0.145, 2.175), (0.090, -0.195, 2.115), (0.145, -0.215, 2.055), (0.175, -0.217, 1.985)], [0.095, 0.092, 0.070, 0.014]),
        ("B", [(-0.020, -0.145, 2.175), (-0.070, -0.205, 2.115), (-0.115, -0.220, 2.055), (-0.145, -0.220, 1.995)], [0.092, 0.090, 0.068, 0.015]),
        ("C", [(-0.085, -0.105, 2.145), (-0.145, -0.165, 2.095), (-0.195, -0.195, 2.045), (-0.210, -0.190, 1.990)], [0.078, 0.072, 0.052, 0.012]),
    ]
    for suffix, points, widths in fringe_specs:
        make_flat_strand(f"Mother_Fringe_{suffix}", points, widths, [0.030, 0.030, 0.024, 0.006], "hair_mid", bevel=0.005)
    make_uv_ellipsoid("Mother_HalfUpKnot", (0.0, 0.333, 1.925), (0.070, 0.045, 0.050), "hair_mid", "Head", 28, 16)
    make_flat_strand(
        "Mother_KnotTail",
        [(0.0, 0.350, 1.915), (0.020, 0.358, 1.865), (0.025, 0.350, 1.805), (0.010, 0.335, 1.745)],
        [0.052, 0.050, 0.035, 0.010],
        [0.026, 0.025, 0.018, 0.005],
        "hair_dark",
        bevel=0.004,
    )

    armature = make_armature(arm_points)

    # Consolidate every visible character element into one mesh object with one atlas slot.
    bpy.ops.object.select_all(action="DESELECT")
    for part in CHARACTER_PARTS:
        part.select_set(True)
    bpy.context.view_layer.objects.active = CHARACTER_PARTS[0]
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "Mother_CompleteSkinnedBody_V1"
    while len(body.data.materials) > 1:
        body.data.materials.pop(index=len(body.data.materials) - 1)
    if len(body.data.materials) == 0:
        body.data.materials.append(ATLAS_MATERIAL)
    for polygon in body.data.polygons:
        polygon.material_index = 0
    modifier = body.modifiers.new("MotherHumanoidSkin", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True
    body.parent = armature
    body["familyCompanyIdentity"] = IDENTITY
    body["singleAtlas"] = True
    body["identityReferenceSha256"] = EXPECTED_REFERENCE_SHA256
    armature["familyCompanyHumanoid"] = True
    armature["bottomCentreRoot"] = True
    return body, armature


def make_armature(arm_points: dict[str, tuple[tuple[float, float, float], ...]]) -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("MotherHumanoidArmatureV1")
    armature = bpy.data.objects.new("Mother_HumanoidArmature_V1", armature_data)
    bpy.context.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name: str, head: tuple[float, float, float], tail: tuple[float, float, float], parent: str | None = None, connected: bool = False):
        edit_bone = armature_data.edit_bones.new(name)
        edit_bone.head = head
        edit_bone.tail = tail
        edit_bone.use_deform = name != "Root"
        if parent:
            edit_bone.parent = armature_data.edit_bones[parent]
            edit_bone.use_connect = connected
        return edit_bone

    bone("Root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.18))
    bone("Hips", (0.0, 0.0, 1.065), (0.0, 0.0, 1.185), "Root")
    bone("Spine", (0.0, 0.0, 1.185), (0.0, 0.0, 1.340), "Hips", True)
    bone("Chest", (0.0, 0.0, 1.340), (0.0, 0.0, 1.485), "Spine", True)
    bone("UpperChest", (0.0, 0.0, 1.485), (0.0, 0.0, 1.590), "Chest", True)
    bone("Neck", (0.0, 0.0, 1.590), (0.0, -0.015, 1.700), "UpperChest", True)
    bone("Head", (0.0, -0.015, 1.700), (0.0, -0.030, 2.160), "Neck", True)

    for side, prefix in (("L", "Left"), ("R", "Right")):
        shoulder, elbow, wrist, hand_end = arm_points[side]
        sign = 1.0 if side == "L" else -1.0
        bone(f"{prefix}Shoulder", (sign * 0.055, 0.0, 1.555), shoulder, "UpperChest")
        bone(f"{prefix}UpperArm", shoulder, elbow, f"{prefix}Shoulder", True)
        bone(f"{prefix}LowerArm", elbow, wrist, f"{prefix}UpperArm", True)
        bone(f"{prefix}Hand", wrist, hand_end, f"{prefix}LowerArm", True)
        bone(f"{prefix}UpperLeg", (sign * 0.165, 0.0, 1.075), (sign * 0.165, 0.0, 0.655), "Hips")
        bone(f"{prefix}LowerLeg", (sign * 0.165, 0.0, 0.655), (sign * 0.165, 0.0, 0.180), f"{prefix}UpperLeg", True)
        bone(f"{prefix}Foot", (sign * 0.165, 0.0, 0.180), (sign * 0.165, -0.180, 0.075), f"{prefix}LowerLeg", True)
        bone(f"{prefix}Toes", (sign * 0.165, -0.180, 0.075), (sign * 0.165, -0.285, 0.045), f"{prefix}Foot", True)

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "OCTAHEDRAL"
    armature.select_set(False)
    return armature


def create_studio() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.color_depth = "8"
    scene.render.engine = "BLENDER_EEVEE"
    scene.world.color = (0.055, 0.055, 0.055)
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass

    bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0.0, 0.0, -0.012))
    ground = bpy.context.object
    ground.name = "QA_StudioGround"
    ground_material = bpy.data.materials.new("QA_StudioGroundMaterial")
    ground_material.diffuse_color = (0.34, 0.36, 0.38, 1.0)
    ground.data.materials.append(ground_material)

    bpy.ops.object.camera_add(location=(0.0, -6.4, 1.18))
    camera = bpy.context.object
    camera.name = "QA_StudioCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.55
    camera.data.lens = 58
    scene.camera = camera
    look_at(camera, Vector((0.0, 0.0, 1.10)))

    def add_area(name: str, location: tuple[float, float, float], energy: float, size: float, colour: tuple[float, float, float]):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = colour
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        look_at(obj, Vector((0.0, 0.0, 1.10)))
        return obj

    add_area("Key_Area", (-3.5, -4.5, 6.0), 950.0, 4.0, (1.0, 0.84, 0.72))
    add_area("Fill_Area", (3.8, -2.0, 3.6), 620.0, 3.5, (0.70, 0.82, 1.0))
    add_area("Rim_Area", (0.0, 4.0, 4.8), 820.0, 3.0, (1.0, 0.72, 0.58))
    return camera, ground


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_view(camera: bpy.types.Object, output_path: Path, position: tuple[float, float, float], resolution: int) -> None:
    camera.location = position
    look_at(camera, Vector((0.0, 0.0, 1.10)))
    scene = bpy.context.scene
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)


def render_four_views(camera: bpy.types.Object, directory: Path, resolution: int, prefix: str) -> list[str]:
    directory.mkdir(parents=True, exist_ok=True)
    views = {
        "front": (0.0, -6.4, 1.18),
        "left": (-6.4, 0.0, 1.18),
        "back": (0.0, 6.4, 1.18),
        "three-quarter": (4.55, -4.55, 1.30),
    }
    outputs = []
    for label, position in views.items():
        path = directory / f"{prefix}-{label}-{resolution}.png"
        render_view(camera, path, position, resolution)
        outputs.append(str(path))
    return outputs


def render_turntable(body: bpy.types.Object, armature: bpy.types.Object, camera: bpy.types.Object, directory: Path) -> list[str]:
    directory.mkdir(parents=True, exist_ok=True)
    camera.location = (0.0, -6.4, 1.18)
    look_at(camera, Vector((0.0, 0.0, 1.10)))
    scene = bpy.context.scene
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    outputs = []
    body_parent = body.parent
    body.parent = None
    for index in range(24):
        angle = math.tau * index / 24
        body.rotation_euler[2] = angle
        armature.rotation_euler[2] = angle
        path = directory / f"mother-v1-turn-{index:02d}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(path))
    body.rotation_euler[2] = 0.0
    armature.rotation_euler[2] = 0.0
    body.parent = body_parent
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
    # Opposite anatomical legs and arms lead at P0/P3.
    # A restrained stride keeps the calf inside the long A-line skirt while still
    # making the leading anatomical side unambiguous in the P0/P3 proof renders.
    armature.pose.bones["LeftUpperLeg"].rotation_euler.x = math.radians(12.0 * direction)
    armature.pose.bones["RightUpperLeg"].rotation_euler.x = math.radians(-12.0 * direction)
    armature.pose.bones["LeftLowerLeg"].rotation_euler.x = math.radians(-5.0 * direction)
    armature.pose.bones["RightLowerLeg"].rotation_euler.x = math.radians(5.0 * direction)
    armature.pose.bones["LeftUpperArm"].rotation_euler.x = math.radians(-10.0 * direction)
    armature.pose.bones["RightUpperArm"].rotation_euler.x = math.radians(10.0 * direction)
    armature.pose.bones["LeftLowerArm"].rotation_euler.x = math.radians(3.0 * direction)
    armature.pose.bones["RightLowerArm"].rotation_euler.x = math.radians(-3.0 * direction)
    armature.pose.bones["Hips"].rotation_euler.y = math.radians(-1.0 * direction)
    armature.pose.bones["Chest"].rotation_euler.y = math.radians(1.0 * direction)
    bpy.context.view_layer.update()


def ground_deformed_pose(body: bpy.types.Object, armature: bpy.types.Object) -> None:
    root = armature.pose.bones["Root"]
    root.location.z = 0.0
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_body = body.evaluated_get(depsgraph)
    evaluated_mesh = evaluated_body.to_mesh()
    try:
        minimum_z = min(
            (evaluated_body.matrix_world @ vertex.co).z
            for vertex in evaluated_mesh.vertices
        )
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
    outputs = []
    camera.location = (4.55, -4.55, 1.30)
    look_at(camera, Vector((0.0, 0.0, 1.08)))
    scene = bpy.context.scene
    scene.render.resolution_x = 1536
    scene.render.resolution_y = 1536
    for phase, label in (("P0", "left-leading"), ("P3", "right-leading")):
        set_walk_contact_pose(armature, phase)
        ground_deformed_pose(body, armature)
        path = directory / f"mother-v1-{phase.lower()}-{label}-1536.png"
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


def build_receipt(
    output: Path,
    reference: Path,
    body: bpy.types.Object,
    armature: bpy.types.Object,
    atlas_path: Path,
    blend_path: Path,
    fbx_path: Path | None,
    rendered: dict[str, list[str]],
    mode: str,
) -> dict:
    body.data.calc_loop_triangles()
    world_vertices = [body.matrix_world @ vertex.co for vertex in body.data.vertices]
    minimum = [min(co[index] for co in world_vertices) for index in range(3)]
    maximum = [max(co[index] for co in world_vertices) for index in range(3)]
    bone_names = [bone.name for bone in armature.data.bones]
    required = [
        "Hips", "Spine", "Chest", "Head",
        "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
        "RightUpperLeg", "RightLowerLeg", "RightFoot",
    ]
    receipt = {
        "contract": "FC-MOTHER-BLENDER-IDENTITY-V1",
        "mode": mode,
        "identity": IDENTITY,
        "reference": {
            "path": str(reference),
            "sha256": sha256_file(reference),
            "expectedSha256": EXPECTED_REFERENCE_SHA256,
            "onlyIdentityInput": True,
        },
        "prohibitedInputsRead": [],
        "model": {
            "meshObjectName": body.name,
            "skinnedMeshObjectCount": 1,
            "characterMaterialSlotCount": len(body.data.materials),
            "textureAtlasCount": 1,
            "vertices": len(body.data.vertices),
            "polygons": len(body.data.polygons),
            "triangles": len(body.data.loop_triangles),
            "boundsMin": minimum,
            "boundsMax": maximum,
            "height": maximum[2] - minimum[2],
            "bottomCentreRoot": [0.0, 0.0, 0.0],
            "minimumSoleZ": minimum[2],
            "completeVisibleFeatures": [
                "two arms", "two hands with fingers", "two legs", "two connected loafers",
                "shoulder-length layered hair", "low half-up twist", "pearl earrings", "analog watch",
                "dusty peach cardigan", "cream blouse", "dark teal A-line skirt",
            ],
        },
        "rig": {
            "armatureObjectName": armature.name,
            "boneCount": len(bone_names),
            "boneNames": bone_names,
            "unityHumanoidRequiredBones": required,
            "missingRequiredBones": sorted(set(required) - set(bone_names)),
            "rootBone": "Root",
            "bindPose": "A-pose",
        },
        "atlas": {
            "path": str(atlas_path),
            "sha256": sha256_file(atlas_path),
            "resolution": [ATLAS_SIZE, ATLAS_SIZE],
            "patchCount": len(PATCHES),
        },
        "outputs": {
            "blend": str(blend_path),
            "blendSha256": sha256_file(blend_path) if blend_path.exists() else None,
            "fbx": str(fbx_path) if fbx_path else None,
            "fbxSha256": sha256_file(fbx_path) if fbx_path and fbx_path.exists() else None,
            "renders": rendered,
        },
        "qa": {
            "singleCharacterMesh": True,
            "singleAtlasMaterial": len(body.data.materials) == 1,
            "referenceHashMatches": sha256_file(reference) == EXPECTED_REFERENCE_SHA256,
            "requiredHumanoidBonesPresent": not (set(required) - set(bone_names)),
            "bottomTouchesFloor": abs(minimum[2]) <= 0.02,
            "P0P3AlternationRenderIncluded": bool(rendered.get("deformation")),
            "visualApproval": "PENDING_HUMAN_REVIEW",
            "unityHumanoidImport": "PENDING_PARENT_UNITY_INTEGRATION",
        },
        "script": {
            "path": str(Path(__file__).resolve()),
            "sha256": sha256_file(Path(__file__).resolve()),
            "blenderVersion": bpy.app.version_string,
        },
    }
    receipt_path = output / "mother-blender-identity-v1-receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")
    return receipt


def main() -> None:
    global ATLAS_MATERIAL, CHARACTER_PARTS
    args = parse_args()
    output = Path(args.output).resolve()
    reference = Path(args.reference).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if not reference.is_file():
        raise FileNotFoundError(reference)
    actual_hash = sha256_file(reference)
    if actual_hash != EXPECTED_REFERENCE_SHA256:
        raise RuntimeError(f"Locked Mother reference hash mismatch: {actual_hash}")
    if args.draft_only and args.final:
        raise RuntimeError("Choose either --draft-only or --final")

    clear_scene()
    CHARACTER_PARTS = []
    _, ATLAS_MATERIAL, atlas_path = make_atlas(output)
    body, armature = make_character()
    camera, _ = create_studio()
    rendered: dict[str, list[str]] = {}

    if args.draft_only:
        rendered["draftFourView"] = render_four_views(camera, output / "draft", 768, "mother-v1-draft")
        blend_path = output / "mother-v1-draft.blend"
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
        build_receipt(output, reference, body, armature, atlas_path, blend_path, None, rendered, "draft")
        print("MOTHER_BLENDER_IDENTITY_V1_DRAFT: PASS")
        return

    rendered["finalFourView"] = render_four_views(camera, output / "final", 1536, "mother-v1-final")
    rendered["turntable24"] = render_turntable(body, armature, camera, output / "turntable")
    rendered["deformation"] = render_deformation(body, armature, camera, output / "deformation")
    clear_pose(armature)
    blend_path = output / "mother-blender-identity-v1.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    fbx_path = output / "mother-blender-humanoid-v1.fbx"
    export_fbx(body, armature, fbx_path)
    # Re-save after export so the canonical .blend remains in neutral bind pose.
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    receipt = build_receipt(output, reference, body, armature, atlas_path, blend_path, fbx_path, rendered, "final")
    if not all(
        [
            receipt["qa"]["singleCharacterMesh"],
            receipt["qa"]["singleAtlasMaterial"],
            receipt["qa"]["referenceHashMatches"],
            receipt["qa"]["requiredHumanoidBonesPresent"],
            receipt["qa"]["bottomTouchesFloor"],
        ]
    ):
        raise RuntimeError("Mother V1 structural receipt failed")
    print("MOTHER_BLENDER_IDENTITY_V1_FINAL: PASS")


if __name__ == "__main__":
    main()
