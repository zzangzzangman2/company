"""MotherDonorReshape2: direct reshaping of owned Mika fitted surfaces.

This pass intentionally creates no character primitive, cage, curve, loft, or
replacement accessory.  It maps connected source components from the owned
CH0069 body, deletes whole fantasy islands, and moves only retained donor
vertices.  Original face/eyes, original three-digit hands, vertex groups and
the 151-bone rig are preserved.  Output is static grayscale gate material.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict, deque
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DOwnedBaseConversion/MotherDonorReshape2",
    )
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


ARGS = parse_args()
REPO = Path(__file__).resolve().parents[2]
OUTPUT = (REPO / ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCE_DIR = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Mika"
    / "CH0069_Mesh"
)
SOURCE_FBX = SOURCE_DIR / "CH0069_Mesh.fbx"
TEXTURE_FIRST_BLEND = (
    REPO
    / "Artifacts"
    / "Family3DOwnedBaseConversion"
    / "MotherTextureFirst1"
    / "mother-texture-first1.blend"
)
AUTHORITY_NEUTRAL = REPO / "Assets/Art/Characters/Mother/mother_office_neutral_v1.png"
AUTHORITY_PIXEL = (
    REPO
    / "Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_a_v1.png"
)


def sha256(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(points):
    payload = json.dumps(
        sorted(tuple(round(float(value), 7) for value in point) for point in points),
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest().upper()


def weight_hash(obj, indices):
    records = []
    group_names = {group.index: group.name for group in obj.vertex_groups}
    for index in indices:
        vertex = obj.data.vertices[index]
        groups = sorted(
            (group_names[item.group], round(float(item.weight), 7))
            for item in vertex.groups
        )
        records.append((tuple(round(float(value), 7) for value in vertex.co), groups))
    payload = json.dumps(sorted(records, key=lambda item: item[0]), separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest().upper()


def gray_material(name, value, roughness=0.76):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (value, value, value, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.14
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (value, value, value, 1.0)
    return material


def components_for_material(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    polygon_map = {polygon.index: polygon for polygon in polygons}
    by_vertex = defaultdict(list)
    for polygon in polygons:
        for vertex in polygon.vertices:
            by_vertex[vertex].append(polygon.index)
    remaining = set(polygon_map)
    result = []
    while remaining:
        seed = remaining.pop()
        todo = deque([seed])
        component = {seed}
        while todo:
            polygon = polygon_map[todo.popleft()]
            for vertex in polygon.vertices:
                for neighbor in by_vertex[vertex]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        todo.append(neighbor)
        result.append(sorted(component))
    return sorted(result, key=len, reverse=True)


def component_vertices(mesh, component):
    return sorted({vertex for polygon in component for vertex in mesh.polygons[polygon].vertices})


def world_bounds(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return {
        "min": [round(float(value), 6) for value in lo],
        "max": [round(float(value), 6) for value in hi],
        "dimensions": [round(float(value), 6) for value in (hi - lo)],
        "center": [round(float(value), 6) for value in ((lo + hi) * 0.5)],
    }


def recalc_normals(obj):
    edit = bmesh.new()
    edit.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(obj.data)
    edit.free()
    obj.data.update()


def delete_unkept_polygons(obj, kept):
    edit = bmesh.new()
    edit.from_mesh(obj.data)
    edit.faces.ensure_lookup_table()
    targets = [face for face in edit.faces if face.index not in kept]
    bmesh.ops.delete(edit, geom=targets, context="FACES")
    loose = [vertex for vertex in edit.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(edit, geom=loose, context="VERTS")
    edit.to_mesh(obj.data)
    edit.free()
    obj.data.update()


def component_edge_boundary_vertices(mesh, component):
    edge_counts = Counter()
    for polygon_index in component:
        polygon = mesh.polygons[polygon_index]
        vertices = list(polygon.vertices)
        for index, left in enumerate(vertices):
            right = vertices[(index + 1) % len(vertices)]
            edge_counts[tuple(sorted((left, right)))] += 1
    return {
        vertex
        for edge, count in edge_counts.items()
        if count == 1
        for vertex in edge
    }


def mesh_topology_stats(mesh):
    """Return final surface connectivity evidence without changing topology."""
    edge_face_counts = Counter()
    polygon_neighbors = defaultdict(set)
    edge_polygons = defaultdict(list)
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for index, left in enumerate(vertices):
            edge = tuple(sorted((left, vertices[(index + 1) % len(vertices)])))
            edge_face_counts[edge] += 1
            edge_polygons[edge].append(polygon.index)
    for polygons in edge_polygons.values():
        for left in polygons:
            polygon_neighbors[left].update(right for right in polygons if right != left)
    remaining = {polygon.index for polygon in mesh.polygons}
    components = 0
    while remaining:
        components += 1
        todo = [remaining.pop()]
        while todo:
            for neighbor in polygon_neighbors[todo.pop()]:
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    todo.append(neighbor)
    return {
        "connectedSurfaceComponents": components,
        "boundaryEdges": sum(1 for count in edge_face_counts.values() if count == 1),
        "nonManifoldEdges": sum(1 for count in edge_face_counts.values() if count != 2),
        "closedManifold": bool(edge_face_counts) and all(count == 2 for count in edge_face_counts.values()),
    }


def material_component_record(obj, material_name, component_id, component, action, role, deformation):
    vertices = component_vertices(obj.data, component)
    boundary_vertices = component_edge_boundary_vertices(obj.data, component)
    return {
        "sourceMaterial": material_name,
        "componentId": component_id,
        "sourcePolygons": len(component),
        "sourceVertices": len(vertices),
        "action": action,
        "role": role,
        "deformation": deformation,
        "sourceBoundaryVertices": len(boundary_vertices),
        "boundsBefore": world_bounds(obj, vertices),
    }


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
armature = bpy.data.objects.get("Armature")
source = bpy.data.objects.get("CH0069_Body")
weapon = bpy.data.objects.get("CH0069_Weapon")
if armature is None or source is None:
    raise RuntimeError("Owned Mika import failed")
if len(armature.data.bones) != 151:
    raise RuntimeError(f"Expected 151 source bones, got {len(armature.data.bones)}")
if weapon:
    bpy.data.objects.remove(weapon, do_unlink=True)
armature.name = "MotherDonorReshape2_Rig_Mika151"
armature.scale = tuple(value * 180.0 for value in armature.scale)
bpy.context.view_layer.update()

source.name = "MotherDonorReshape2_OriginalWeightReference_Hidden"
source.hide_render = True
source.hide_set(True)
working = source.copy()
working.data = source.data.copy()
working.name = "MotherDonorReshape2_DirectlyReshapedMikaSurfaces"
bpy.context.collection.objects.link(working)
working.hide_render = False
working.hide_set(False)

material_names = [material.name if material else "" for material in working.data.materials]


def slot(prefix):
    return next(index for index, name in enumerate(material_names) if name.startswith(prefix))


hair_slot = slot("CH0069_Hair")
face_slot = slot("CH0069_Face")
brow_slot = slot("CH0069_Eyebrow")
eye_slot = slot("CH0069_EyeMouth")
body_slot = slot("CH0069_Body")
body_components = components_for_material(working.data, body_slot)
hair_components = components_for_material(working.data, hair_slot)
eye_components = components_for_material(working.data, eye_slot)
if len(body_components) != 389:
    raise RuntimeError(f"Expected 389 Mika Body material components, got {len(body_components)}")
if len(hair_components) < 22:
    raise RuntimeError(f"Expected at least 22 Mika Hair components, got {len(hair_components)}")
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError("Mika opaque EyeMouth background component changed")
mouth_polygons = set(mouth_components[0])

# Source component map. No CT1 object or generated shape is opened or reused.
TORSO_COMPONENTS = {10, 11}
SLEEVE_COMPONENTS = {18, 19}
SKIRT_COMPONENTS = {12, 13, 14, 15}
LEG_COMPONENTS = {0, 1}
SHOE_COMPONENTS = {22, 23}
HAIR_COMPONENTS = {0, 1, 2, 3, 4, 21}

# Main three-digit hands only. Whole wrist ornaments are explicitly rejected.
def hand_weight_fractions(vertices):
    hand = arm = total = 0.0
    for vertex_index in vertices:
        for membership in working.data.vertices[vertex_index].groups:
            name = working.vertex_groups[membership.group].name.lower()
            weight = float(membership.weight)
            if " hand" in name or " finger" in name:
                hand += weight
            if any(token in name for token in ("upperarm", "forearm", "wrist")):
                arm += weight
            total += weight
    return hand / max(total, 1.0e-9), arm / max(total, 1.0e-9)


HAND_COMPONENTS = set()
hand_component_audit = []
for component_id, component in enumerate(body_components):
    vertices = component_vertices(working.data, component)
    bounds = world_bounds(working, vertices)
    center = Vector(bounds["center"])
    dimensions = Vector(bounds["dimensions"])
    hand_fraction, arm_fraction = hand_weight_fractions(vertices)
    wrist_ornament = (
        0.395 < abs(center.x) < 0.495
        and 0.72 < center.z < 0.91
        and dimensions.x < 0.135
        and hand_fraction + arm_fraction > 0.35
    )
    keep = hand_fraction > 0.30 and not wrist_ornament
    if keep:
        HAND_COMPONENTS.add(component_id)
    if keep or wrist_ornament:
        hand_component_audit.append(
            {
                "componentId": component_id,
                "polygons": len(component),
                "handWeightFraction": round(hand_fraction, 6),
                "armWeightFraction": round(arm_fraction, 6),
                "kept": keep,
                "wholeWristOrnamentDeleted": wrist_ornament,
            }
        )

hand_polygons = set().union(*(set(body_components[index]) for index in HAND_COMPONENTS))
if len(hand_polygons) != 454:
    raise RuntimeError(f"Expected 454 ornament-free hand polygons, got {len(hand_polygons)}")

head_polygons = {
    polygon.index
    for polygon in working.data.polygons
    if polygon.material_index in {face_slot, brow_slot, eye_slot}
    and polygon.index not in mouth_polygons
}
head_vertices = sorted({vertex for polygon in head_polygons for vertex in working.data.polygons[polygon].vertices})
hand_vertices = sorted({vertex for polygon in hand_polygons for vertex in working.data.polygons[polygon].vertices})
head_points_before = [working.matrix_world @ working.data.vertices[index].co for index in head_vertices]
hand_points_before = [working.matrix_world @ working.data.vertices[index].co for index in hand_vertices]
head_coord_hash_before = coordinate_hash(head_points_before)
hand_coord_hash_before = coordinate_hash(hand_points_before)
head_weight_hash_before = weight_hash(working, head_vertices)
hand_weight_hash_before = weight_hash(working, hand_vertices)

component_map = []
for component_id, role, deformation in (
    *[(index, "cardigan/blouse torso donor", "body-fit elliptical reprojection; shared surface tone regions") for index in sorted(TORSO_COMPONENTS)],
    *[(index, "cardigan sleeve donor", "minor depth relaxation only; original fitted arm surface") for index in sorted(SLEEVE_COMPONENTS)],
    *[(index, "clean midi skirt donor panel", "direct waist-to-below-knee A-line reprojection; ruffle components not retained") for index in sorted(SKIRT_COMPONENTS)],
    *[(index, "lower leg/foot under-surface", "coordinated 1.22x lower-body/rest-bone lengthening") for index in sorted(LEG_COMPONENTS)],
    *[(index, "loafer donor shell", "direct low-profile foot-enclosing shell reshape") for index in sorted(SHOE_COMPONENTS)],
    *[(index, "original 3-digit stylized hand retained", "none") for index in sorted(HAND_COMPONENTS)],
):
    component_map.append(
        material_component_record(
            working,
            "CH0069_Body",
            component_id,
            body_components[component_id],
            "retain_and_directly_reshape" if deformation != "none" else "retain_exact",
            role,
            deformation,
        )
    )
for component_id in sorted(HAIR_COMPONENTS):
    role = {
        0: "main scalp/back donor mass",
        1: "front bangs donor",
        2: "back inner donor layer",
        3: "front-side lock donor",
        4: "front-side lock donor",
        21: "embedded low half-up donor section",
    }[component_id]
    component_map.append(
        material_component_record(
            working,
            "CH0069_Hair",
            component_id,
            hair_components[component_id],
            "retain_and_directly_reshape",
            role,
            "compress donor lengths to shoulder level; central back vertices form embedded low gather",
        )
    )

def set_world_vertex(obj, index, point):
    obj.data.vertices[index].co = obj.matrix_world.inverted() @ Vector(point)


# Direct torso donor reshape. The two original skinned dress torso surfaces are
# fitted to a soft adult cardigan/blouse envelope; no replacement torso exists.
torso_vertices = sorted(set().union(*(set(component_vertices(working.data, body_components[index])) for index in TORSO_COMPONENTS)))
torso_points = {index: working.matrix_world @ working.data.vertices[index].co for index in torso_vertices}
torso_z_min = min(point.z for point in torso_points.values())
torso_z_max = max(point.z for point in torso_points.values())
radials = {
    index: math.sqrt((point.x / 0.45) ** 2 + ((point.y - 0.01) / 0.30) ** 2)
    for index, point in torso_points.items()
}
radial_min, radial_max = min(radials.values()), max(radials.values())
for index, point in torso_points.items():
    t = max(0.0, min(1.0, (point.z - torso_z_min) / max(1.0e-8, torso_z_max - torso_z_min)))
    eased = t * t * (3.0 - 2.0 * t)
    radius_x = 0.215 + 0.065 * eased - 0.012 * math.sin(math.pi * t)
    radius_y = 0.145 + 0.024 * eased - 0.006 * math.sin(math.pi * t)
    theta = math.atan2(point.x, point.y - 0.01)
    layer = 0.975 + 0.050 * ((radials[index] - radial_min) / max(1.0e-8, radial_max - radial_min))
    target = Vector(
        (
            radius_x * layer * math.sin(theta),
            0.006 + radius_y * layer * math.cos(theta),
            0.675 + 0.435 * t,
        )
    )
    set_world_vertex(working, index, target)

# Source sleeves already conform to Mika's arm rig. Remove the fantasy flare
# only by a shallow depth relaxation, leaving their fitted topology and weights.
sleeve_vertices = sorted(set().union(*(set(component_vertices(working.data, body_components[index])) for index in SLEEVE_COMPONENTS)))
for index in sleeve_vertices:
    point = working.matrix_world @ working.data.vertices[index].co
    point.y = -0.002 + (point.y + 0.002) * 0.88
    set_world_vertex(working, index, point)

# Directly reshape four existing front/back skirt panels. The source ruffle
# components 4/5 and fantasy over-panels 10/11-as-skirt are not retained as
# lower garment islands. All lower boundary vertices converge to a clean hem.
skirt_vertices = sorted(set().union(*(set(component_vertices(working.data, body_components[index])) for index in SKIRT_COMPONENTS)))
skirt_points = {index: working.matrix_world @ working.data.vertices[index].co for index in skirt_vertices}
skirt_z_min = min(point.z for point in skirt_points.values())
skirt_z_max = max(point.z for point in skirt_points.values())
skirt_radials = {
    index: math.sqrt((point.x / 0.30) ** 2 + ((point.y - 0.01) / 0.23) ** 2)
    for index, point in skirt_points.items()
}
skirt_radial_min, skirt_radial_max = min(skirt_radials.values()), max(skirt_radials.values())
skirt_hem_boundary = set()
for component_id in SKIRT_COMPONENTS:
    component = body_components[component_id]
    boundary = component_edge_boundary_vertices(working.data, component)
    local_min = min(skirt_points[index].z for index in component_vertices(working.data, component))
    local_max = max(skirt_points[index].z for index in component_vertices(working.data, component))
    skirt_hem_boundary.update(
        index
        for index in boundary
        if (skirt_points[index].z - local_min) / max(1.0e-8, local_max - local_min) < 0.36
    )
for index, point in skirt_points.items():
    t = max(0.0, min(1.0, (point.z - skirt_z_min) / max(1.0e-8, skirt_z_max - skirt_z_min)))
    curve = 0.28 * t + 0.72 * (t ** 1.55)
    radius_x = 0.335 * (1.0 - curve) + 0.205 * curve
    radius_y = 0.225 * (1.0 - curve) + 0.145 * curve
    theta = math.atan2(point.x, point.y - 0.01)
    layer = 0.985 + 0.030 * ((skirt_radials[index] - skirt_radial_min) / max(1.0e-8, skirt_radial_max - skirt_radial_min))
    z = 0.120 + (0.700 - 0.120) * t
    if index in skirt_hem_boundary:
        z = 0.120
    target = Vector(
        (
            radius_x * layer * math.sin(theta),
            0.010 + radius_y * layer * math.cos(theta),
            z,
        )
    )
    set_world_vertex(working, index, target)

# Coordinated adult lower-body lengthening: only the retained leg under-surfaces
# and corresponding rest bones change. Face, torso, arms and hands are excluded.
LOWER_FACTOR = 1.22
LOWER_PIVOT_WORLD_Z = 0.665
leg_vertices = sorted(set().union(*(set(component_vertices(working.data, body_components[index])) for index in LEG_COMPONENTS)))
for index in leg_vertices:
    point = working.matrix_world @ working.data.vertices[index].co
    point.z = LOWER_PIVOT_WORLD_Z + (point.z - LOWER_PIVOT_WORLD_Z) * LOWER_FACTOR
    set_world_vertex(working, index, point)

# Each donor foot shell becomes a low, asymmetrical loafer that encloses the
# stretched foot/ankle. No sole, strap or toe primitive is added.
shoe_vertices_by_component = {}
for component_id in SHOE_COMPONENTS:
    vertices = component_vertices(working.data, body_components[component_id])
    shoe_vertices_by_component[component_id] = vertices
    points = {index: working.matrix_world @ working.data.vertices[index].co for index in vertices}
    lo = Vector(tuple(min(point[axis] for point in points.values()) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points.values()) for axis in range(3)))
    center = (lo + hi) * 0.5
    half = (hi - lo) * 0.5
    side = 1.0 if center.x > 0.0 else -1.0
    target_center_x = 0.160 * side
    for index, point in points.items():
        nx = max(-1.0, min(1.0, (point.x - center.x) / max(half.x, 1.0e-8)))
        ny = max(-1.0, min(1.0, (point.y - center.y) / max(half.y, 1.0e-8)))
        nz = max(-1.0, min(1.0, (point.z - center.z) / max(half.z, 1.0e-8)))
        target = Vector(
            (
                target_center_x + 0.090 * nx * (1.0 - 0.05 * max(0.0, -ny)),
                -0.040 + (0.155 if ny < 0.0 else 0.105) * ny,
                -0.092 + 0.067 * nz,
            )
        )
        if nz < -0.62:
            target.z = -0.158 + 0.009 * ((nz + 1.0) / 0.38)
        set_world_vertex(working, index, target)

# Crop and reshape only retained donor hair components. The source bangs stay
# fitted; long back/side surfaces compress to a tapered shoulder hem. A small
# central-back displacement of existing component 21 supplies an embedded low
# half-up read without a bun/sphere or separate part.
hair_vertices_by_component = {
    component_id: component_vertices(working.data, hair_components[component_id])
    for component_id in HAIR_COMPONENTS
}
for component_id, vertices in hair_vertices_by_component.items():
    points = {index: working.matrix_world @ working.data.vertices[index].co for index in vertices}
    z_min = min(point.z for point in points.values())
    for index, point in points.items():
        if component_id == 1:
            continue
        if point.z < 1.300:
            ratio = max(0.0, min(1.0, (point.z - z_min) / max(1.0e-8, 1.300 - z_min)))
            point.z = 1.020 + 0.280 * ratio
            taper = 0.72 + 0.28 * ratio
            point.x *= taper
            point.y = 0.075 + (point.y - 0.075) * (0.62 + 0.38 * ratio)
        if component_id == 21:
            gather = max(0.0, 1.0 - abs(point.x) / 0.19)
            point.x *= 0.86
            point.y += 0.030 * gather
            point.z += 0.018 * gather
        elif component_id in {0, 2} and point.y > 0.16 and abs(point.x) < 0.16 and 1.24 < point.z < 1.50:
            gather = (1.0 - abs(point.x) / 0.16) * (1.0 - abs(point.z - 1.37) / 0.13)
            point.y += 0.026 * max(0.0, gather)
        set_world_vertex(working, index, point)

# Rest-bone lengthening follows the same lower pivot in armature local Y.
pelvis_y = armature.data.bones["Bip001 Pelvis"].head_local.y
bone_tokens = ("thigh", "calf", " foot", " toe", "bone_skirt", "bone_foot_ribbon")
bone_records = []
bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for bone in armature.data.edit_bones:
    low = bone.name.lower()
    if not any(token in low for token in bone_tokens):
        continue
    before_head = tuple(bone.head)
    before_tail = tuple(bone.tail)
    bone.head.y = pelvis_y + (bone.head.y - pelvis_y) * LOWER_FACTOR
    bone.tail.y = pelvis_y + (bone.tail.y - pelvis_y) * LOWER_FACTOR
    bone_records.append(
        {
            "name": bone.name,
            "headBefore": [round(float(value), 7) for value in before_head],
            "tailBefore": [round(float(value), 7) for value in before_tail],
            "headAfter": [round(float(value), 7) for value in bone.head],
            "tailAfter": [round(float(value), 7) for value in bone.tail],
        }
    )
bpy.ops.object.mode_set(mode="OBJECT")
armature.select_set(False)
bpy.context.view_layer.update()
working.data.update()

# Capture each retained donor component after its direct surface deformation,
# while the original component polygon/vertex indices are still intact. This
# makes the component-map receipt independently auditable after unused fantasy
# islands are removed and BMesh compacts the final mesh indices.
for record in component_map:
    source_components = body_components if record["sourceMaterial"] == "CH0069_Body" else hair_components
    vertices = component_vertices(working.data, source_components[record["componentId"]])
    record["boundsAfter"] = world_bounds(working, vertices)

# Grayscale role materials on retained donor polygons only.
MAT_FACE = gray_material("MotherDR2_FaceGray", 0.72)
MAT_EYE = gray_material("MotherDR2_EyeGray", 0.16, 0.62)
MAT_BROW = gray_material("MotherDR2_BrowGray", 0.12, 0.66)
MAT_HAND_SKIN = gray_material("MotherDR2_HandSkinGray", 0.69)
MAT_LEG_SKIN = gray_material("MotherDR2_LegSkinGray", 0.66)
MAT_CARDIGAN = gray_material("MotherDR2_CardiganGray", 0.38, 0.82)
MAT_BLOUSE = gray_material("MotherDR2_BlouseGray", 0.77, 0.80)
MAT_SKIRT = gray_material("MotherDR2_MidiSkirtGray", 0.29, 0.84)
MAT_HAIR = gray_material("MotherDR2_HairGray", 0.19, 0.78)
MAT_LOAFER = gray_material("MotherDR2_LoaferGray", 0.13, 0.68)
role_materials = {
    "face": MAT_FACE,
    "eye": MAT_EYE,
    "brow": MAT_BROW,
    "hand": MAT_HAND_SKIN,
    "leg": MAT_LEG_SKIN,
    "cardigan": MAT_CARDIGAN,
    "blouse": MAT_BLOUSE,
    "skirt": MAT_SKIRT,
    "hair": MAT_HAIR,
    "loafer": MAT_LOAFER,
}
role_slots = {}
for role, material in role_materials.items():
    role_slots[role] = len(working.data.materials)
    working.data.materials.append(material)

kept_polygons = set(head_polygons) | set(hand_polygons)
for component_id in TORSO_COMPONENTS | SLEEVE_COMPONENTS | SKIRT_COMPONENTS | LEG_COMPONENTS | SHOE_COMPONENTS:
    kept_polygons.update(body_components[component_id])
for component_id in HAIR_COMPONENTS:
    kept_polygons.update(hair_components[component_id])

for polygon in working.data.polygons:
    if polygon.index not in kept_polygons:
        continue
    if polygon.index in head_polygons:
        if polygon.material_index == eye_slot:
            polygon.material_index = role_slots["eye"]
        elif polygon.material_index == brow_slot:
            polygon.material_index = role_slots["brow"]
        else:
            polygon.material_index = role_slots["face"]
    elif polygon.index in hand_polygons:
        polygon.material_index = role_slots["hand"]
    elif any(polygon.index in body_components[index] for index in LEG_COMPONENTS):
        polygon.material_index = role_slots["leg"]
    elif any(polygon.index in body_components[index] for index in SHOE_COMPONENTS):
        polygon.material_index = role_slots["loafer"]
    elif any(polygon.index in body_components[index] for index in SKIRT_COMPONENTS):
        polygon.material_index = role_slots["skirt"]
    elif any(polygon.index in body_components[index] for index in SLEEVE_COMPONENTS):
        polygon.material_index = role_slots["cardigan"]
    elif any(polygon.index in body_components[index] for index in TORSO_COMPONENTS):
        center = working.matrix_world @ polygon.center
        if center.y < -0.075 and abs(center.x) < 0.115 and center.z > 0.70:
            polygon.material_index = role_slots["blouse"]
        else:
            polygon.material_index = role_slots["cardigan"]
    else:
        polygon.material_index = role_slots["hair"]
    polygon.use_smooth = True

head_points_after_deform = [working.matrix_world @ working.data.vertices[index].co for index in head_vertices]
hand_points_after_deform = [working.matrix_world @ working.data.vertices[index].co for index in hand_vertices]
head_coord_hash_after = coordinate_hash(head_points_after_deform)
hand_coord_hash_after = coordinate_hash(hand_points_after_deform)
head_weight_hash_after = weight_hash(working, head_vertices)
hand_weight_hash_after = weight_hash(working, hand_vertices)
if head_coord_hash_before != head_coord_hash_after or head_weight_hash_before != head_weight_hash_after:
    raise RuntimeError("Original face/eyes changed during donor reshape")
if hand_coord_hash_before != hand_coord_hash_after or hand_weight_hash_before != hand_weight_hash_after:
    raise RuntimeError("Original three-digit hands changed during donor reshape")

delete_unkept_polygons(working, kept_polygons)
recalc_normals(working)
for polygon in working.data.polygons:
    polygon.use_smooth = True
final_topology = mesh_topology_stats(working.data)

visible_points = [working.matrix_world @ vertex.co for vertex in working.data.vertices]
visible_lo = Vector(tuple(min(point[axis] for point in visible_points) for axis in range(3)))
visible_hi = Vector(tuple(max(point[axis] for point in visible_points) for axis in range(3)))
face_lo = Vector(tuple(min(point[axis] for point in head_points_after_deform) for axis in range(3)))
face_hi = Vector(tuple(max(point[axis] for point in head_points_after_deform) for axis in range(3)))
face_height = face_hi.z - face_lo.z
adult_head_count = (visible_hi.z - visible_lo.z) / max(face_height, 1.0e-8)

# Review scene. Character geometry count remains one copied donor mesh.
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 1400
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.world = bpy.data.worlds.new("MotherDR2_GrayWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.038, 0.047, 0.060, 1.0)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.34
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.10
except TypeError:
    pass


def add_area(name, location, energy, size):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 0.82)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


review_lights = [
    add_area("MotherDR2_Key", (-3.2, -4.2, 3.8), 560.0, 3.4),
    add_area("MotherDR2_Fill", (3.5, -2.2, 2.8), 320.0, 3.0),
    add_area("MotherDR2_Rim", (0.0, 3.8, 3.3), 430.0, 3.0),
]
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, visible_lo.z - 0.004))
ground = bpy.context.object
ground.name = "MotherDR2_ReviewGround_NotCharacter"
ground.data.materials.append(gray_material("MotherDR2_GroundGray", 0.055, 0.88))

camera_data = bpy.data.cameras.new("MotherDR2_ReviewCameraData")
camera = bpy.data.objects.new("MotherDR2_ReviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.data.type = "ORTHO"
scene.camera = camera


def point_camera(location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale


center_z = (visible_lo.z + visible_hi.z) * 0.5
ortho_scale = (visible_hi.z - visible_lo.z) * 1.11
views = {
    "front": ((0.0, -4.4, center_z), (0.0, 0.0, center_z), ortho_scale),
    "three-quarter": ((3.15, -3.25, center_z + 0.02), (0.0, 0.0, center_z), ortho_scale),
    "side": ((4.4, 0.0, center_z), (0.0, 0.0, center_z), ortho_scale),
    "back": ((0.0, 4.4, center_z), (0.0, 0.0, center_z), ortho_scale),
}
solid_paths = []
for label, (location, target, scale) in views.items():
    point_camera(location, target, scale)
    path = OUTPUT / f"mother-donor-reshape2-gray-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    solid_paths.append(path)

# Review-only donor-surface wire copies, removed before saving.
def role_wire_copy(name, material_indices):
    obj = working.copy()
    obj.data = working.data.copy()
    obj.name = name
    bpy.context.collection.objects.link(obj)
    keep = {polygon.index for polygon in obj.data.polygons if polygon.material_index in material_indices}
    delete_unkept_polygons(obj, keep)
    obj.data.materials.clear()
    wire_material = gray_material(name + "_Material", 0.012, 0.90)
    obj.data.materials.append(wire_material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    modifier = obj.modifiers.new("QA_ActualDonorTopologyWire", "WIREFRAME")
    modifier.thickness = 0.0017
    modifier.use_replace = True
    modifier.use_even_offset = True
    return obj


wire_groups = {
    "upper-cardigan-blouse": {role_slots["cardigan"], role_slots["blouse"]},
    "midi-skirt-panels": {role_slots["skirt"]},
    "rear-shoulder-hair": {role_slots["hair"]},
    "loafer-shells": {role_slots["loafer"]},
}
wire_objects = {label: role_wire_copy("QA_Wire_" + label, indices) for label, indices in wire_groups.items()}
for obj in wire_objects.values():
    obj.hide_render = True
wire_views = {
    "upper-cardigan-blouse": ((2.5, -3.7, 0.94), (0.0, 0.0, 0.93), 1.05),
    "midi-skirt-panels": ((2.4, -3.8, 0.42), (0.0, 0.0, 0.41), 0.88),
    "rear-shoulder-hair": ((2.5, 3.8, 1.42), (0.0, 0.07, 1.40), 1.02),
    "loafer-shells": ((1.8, -3.2, -0.08), (0.0, -0.03, -0.08), 0.48),
}
wire_paths = []
for label, (location, target, scale) in wire_views.items():
    for obj in wire_objects.values():
        obj.hide_render = True
    wire_objects[label].hide_render = False
    point_camera(location, target, scale)
    path = OUTPUT / f"mother-donor-reshape2-seam-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    wire_paths.append(path)
for obj in wire_objects.values():
    bpy.data.objects.remove(obj, do_unlink=True)

blend_path = OUTPUT / "mother-donor-reshape2-gray-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.mother-donor-reshape2.v1",
    "status": "DIAGNOSTIC_ONLY_DONOR_SURFACE_LIMIT",
    "candidate": False,
    "claimScope": "source-only donor mapping/reshape feasibility diagnostic; not a Mother candidate",
    "source": {
        "ownedMikaFbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "ownedMikaSha256": sha256(SOURCE_FBX),
        "textureFirstStudyConsulted": str(TEXTURE_FIRST_BLEND.relative_to(REPO)).replace("\\", "/"),
        "textureFirstStudyExists": TEXTURE_FIRST_BLEND.exists(),
        "mother2DAuthority": str(AUTHORITY_NEUTRAL.relative_to(REPO)).replace("\\", "/"),
        "pixelAuthority": str(AUTHORITY_PIXEL.relative_to(REPO)).replace("\\", "/"),
        "ct1CageGeometryUsed": False,
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
        "unityModified": False,
        "docsModified": False,
    },
    "constructionPolicy": {
        "visibleCharacterMeshObjects": [working.name],
        "visibleCharacterPrimitiveCageCurveLoftObjects": 0,
        "newCharacterVertices": 0,
        "retainedDonorVertices": len(working.data.vertices),
        "retainedDonorPolygons": len(working.data.polygons),
        "wholeFantasyIslandsDeleted": True,
        "reviewOnlyGroundCameraLightsExcludedFromCharacter": True,
        "colorOrGifGenerated": False,
        "finalRetainedSurfaceTopology": final_topology,
    },
    "preservation": {
        "faceEyes": "original Mika face/eyes retained exactly; only gray review material",
        "faceEyeCoordinateHashBefore": head_coord_hash_before,
        "faceEyeCoordinateHashAfter": head_coord_hash_after,
        "faceEyeWeightHashBefore": head_weight_hash_before,
        "faceEyeWeightHashAfter": head_weight_hash_after,
        "faceEyesExact": head_coord_hash_before == head_coord_hash_after and head_weight_hash_before == head_weight_hash_after,
        "opaqueMouthPlatePolygonsRemoved": len(mouth_polygons),
        "newMouthGeometry": 0,
        "hands": "original 3-digit stylized hand retained",
        "handCoordinateHashBefore": hand_coord_hash_before,
        "handCoordinateHashAfter": hand_coord_hash_after,
        "handWeightHashBefore": hand_weight_hash_before,
        "handWeightHashAfter": hand_weight_hash_after,
        "handsExact": hand_coord_hash_before == hand_coord_hash_after and hand_weight_hash_before == hand_weight_hash_after,
        "handPolygons": len(hand_polygons),
        "handComponentAudit": hand_component_audit,
        "rigBones": len(armature.data.bones),
        "vertexGroupVocabulary": len(working.vertex_groups),
        "armatureModifier": [modifier.name for modifier in working.modifiers if modifier.type == "ARMATURE"],
    },
    "componentMap": component_map,
    "explicitDeletedDonorGroups": {
        "bodySkirtRuffles": [4, 5],
        "bodyFantasyAccessories": "all CH0069_Body components outside the mapped torso/sleeves/skirt/legs/shoes/hands sets",
        "hairSideBunAndLongAccessoryIslands": sorted(set(range(len(hair_components))) - HAIR_COMPONENTS),
        "weapon": "whole CH0069_Weapon object deleted",
    },
    "directReshape": {
        "torso": "CH0069_Body components 10/11 fitted to one shared soft envelope; front center is blouse-tone on same donor surface",
        "sleeves": "CH0069_Body components 18/19 retain fitted arm topology; shallow depth relaxation only",
        "midiSkirt": "CH0069_Body components 12-15 directly reprojected to a clean below-knee A-line; source boundary vertices form clean hem",
        "hair": "CH0069_Hair components 0/1/2/3/4/21 retained; donor lengths compressed to shoulder level and component 21 embedded as a low central gather",
        "loafers": "CH0069_Body foot-shell components 22/23 directly reshaped to low-profile foot-enclosing shells",
        "floatingReplacementParts": 0,
    },
    "visualGate": {
        "result": "FAIL",
        "reasons": [
            "source torso and sleeve islands leave open horizontal and underarm gaps after fantasy components are removed",
            "four one-sided skirt donor panels cannot read as one continuous fitted midi skirt without bridging/welding or replacement retopology",
            "compressed long-hair donor sheets flare into a ring and do not form a coherent shoulder-length half-up mass",
            "low-poly donor foot islands remain angular/open and do not read as foot-enclosing loafers",
            "3.55 preserved-face-height metric is numerical only; disconnected body regions prevent an adult 44-year-old silhouette read",
        ],
        "stopReason": "Meeting the Mother authority requires destructive retopology/bridging beyond direct donor-surface reshaping; no primitive fallback was used.",
    },
    "adultProportion": {
        "lowerBodyFactor": LOWER_FACTOR,
        "pivotWorldZ": LOWER_PIVOT_WORLD_Z,
        "restBonesEdited": len(bone_records),
        "restBoneRecords": bone_records,
        "visibleBounds": {
            "min": [round(float(value), 6) for value in visible_lo],
            "max": [round(float(value), 6) for value in visible_hi],
            "dimensions": [round(float(value), 6) for value in (visible_hi - visible_lo)],
        },
        "preservedFaceHeight": round(float(face_height), 6),
        "visibleHeightInPreservedFaceHeights": round(float(adult_head_count), 6),
        "targetRange": [3.5, 4.0],
        "withinTarget": 3.5 <= adult_head_count <= 4.0,
        "claim": "coordinated lower-body mesh/rest-bone lengthening only; animation deformation remains a later gate",
    },
    "proofs": {
        "grayViews": [str(path.relative_to(REPO)).replace("\\", "/") for path in solid_paths],
        "donorSurfaceSeamCloseups": [str(path.relative_to(REPO)).replace("\\", "/") for path in wire_paths],
        "gif": None,
    },
    "excludedClaims": [
        "final color/material fidelity",
        "face age differentiation",
        "animation deformation approval",
        "production readiness",
    ],
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
receipt_path = OUTPUT / "mother-donor-reshape2-component-map-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps(receipt, indent=2, ensure_ascii=False))
