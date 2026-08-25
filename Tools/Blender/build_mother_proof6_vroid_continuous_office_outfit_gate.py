"""Build a static Mother approval gate from the owned Mika identity and VRoid surfaces.

The owned Mika-derived face, brows, eye/mouth surface, chestnut hair and exact
native three-digit hands remain untouched.  A continuous VRoid Body_0 supplies
skin beneath one continuous Body_1 garment.  That single garment is shaded as
the 2D-authority dusty-peach cardigan, cream blouse and dark-teal calf A-line
skirt, so no cardigan panel, collar plate, waistband box or skirt wedge can
float away from the outfit.  Body_3 supplies fitted dark-brown shoes.

Static user-review candidate only.  No rig transfer, GIF, Unity or production
promotion is claimed by this script.
"""

from __future__ import annotations

import argparse
from collections import defaultdict, deque
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--vroid-obj", required=True)
    parser.add_argument("--mika-fbx", required=True)
    parser.add_argument("--yuuka-fbx", required=True)
    parser.add_argument("--yuuka-neck-blend", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
VROID_OBJ = Path(ARGS.vroid_obj).resolve()
MIKA_FBX = Path(ARGS.mika_fbx).resolve()
YUUKA_FBX = Path(ARGS.yuuka_fbx).resolve()
YUUKA_NECK_BLEND = Path(ARGS.yuuka_neck_blend).resolve()
REFERENCE = Path(ARGS.reference).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
if not VROID_OBJ.is_file() or not MIKA_FBX.is_file() or not YUUKA_FBX.is_file() or not YUUKA_NECK_BLEND.is_file() or not REFERENCE.is_file():
    raise RuntimeError("Mother Proof6 requires the owned Mika/Yuuka sources, approved Yuuka neck blend, VRoid OBJ and 2D authority")

scene = bpy.context.scene
owned_body = bpy.data.objects.get("CH0069_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("MotherTF_ReviewCamera") or scene.camera
if owned_body is None or armature is None or camera is None:
    raise RuntimeError("Expected Mother AdultMorph4 owned Mika identity scene")
if len(armature.data.bones) != 151:
    raise RuntimeError(f"Expected exact owned Mika 151-bone rig, got {len(armature.data.bones)}")


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def coordinate_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        co = obj.data.vertices[index].co
        digest.update(f"{index}:{co.x:.9f},{co.y:.9f},{co.z:.9f};".encode())
    return digest.hexdigest()


def weight_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        digest.update(f"{index}:".encode())
        for group_index, weight in sorted(
            (group.group, group.weight) for group in obj.data.vertices[index].groups
        ):
            digest.update(f"{group_index}:{weight:.9f},".encode())
        digest.update(b";")
    return digest.hexdigest()


def connected_components(mesh):
    vertex_polygons = defaultdict(list)
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            vertex_polygons[vertex_index].append(polygon.index)
    neighbors = [set() for _ in mesh.polygons]
    for polygon_indices in vertex_polygons.values():
        for polygon_index in polygon_indices:
            neighbors[polygon_index].update(polygon_indices)
    component_vertices = defaultdict(set)
    component_polygons = defaultdict(list)
    component_for_polygon = [-1] * len(mesh.polygons)
    for seed in range(len(mesh.polygons)):
        if component_for_polygon[seed] >= 0:
            continue
        component_id = len(component_polygons)
        queue = deque([seed])
        component_for_polygon[seed] = component_id
        while queue:
            polygon_index = queue.popleft()
            polygon = mesh.polygons[polygon_index]
            component_polygons[component_id].append(polygon_index)
            component_vertices[component_id].update(polygon.vertices)
            for neighbor in neighbors[polygon_index]:
                if component_for_polygon[neighbor] < 0:
                    component_for_polygon[neighbor] = component_id
                    queue.append(neighbor)
    return component_vertices, component_polygons


def solid_material(name, color, roughness=0.82, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.06
    return material


MAT_SKIN = solid_material("MotherProof6ConnectedSkin", (0.91, 0.75, 0.70), 0.84)
MAT_SHOE = solid_material("MotherProof6DarkBrownLoafer", (0.024, 0.006, 0.003), 0.78)
MAT_PEARL = solid_material("MotherProof6Pearl", (0.94, 0.90, 0.82), 0.38)
MAT_BUTTON = solid_material("MotherProof6CardiganButton", (0.72, 0.53, 0.34), 0.66)
MAT_WATCH_BAND = solid_material("MotherProof6WatchBand", (0.080, 0.021, 0.012), 0.72)
MAT_WATCH_FACE = solid_material("MotherProof6WatchFace", (0.86, 0.79, 0.65), 0.42, 0.08)
MAT_GRAY = solid_material("MotherProof6QAGray", (0.43, 0.45, 0.49), 0.84)
hidden_material = bpy.data.materials.get("MotherTF_WholeComponentHidden")
if hidden_material is None:
    raise RuntimeError("Expected Mother whole-component hidden material")


def mother_outfit_material():
    material = bpy.data.materials.get("MotherProof6ContinuousOfficeOutfit")
    if material is None:
        material = bpy.data.materials.new("MotherProof6ContinuousOfficeOutfit")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.86
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.055
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    separate = tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    absolute_x = tree.nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])

    front = tree.nodes.new("ShaderNodeMath")
    front.operation = "LESS_THAN"
    front.inputs[1].default_value = -0.048
    tree.links.new(separate.outputs["Y"], front.inputs[0])
    # The blouse opening follows the authority: narrow where it tucks into the
    # skirt and gently widening toward the collar.  A constant-width rectangle
    # read as a pasted-on board in the previous diagnostic.
    blouse_width_scale = tree.nodes.new("ShaderNodeMath")
    blouse_width_scale.operation = "MULTIPLY"
    blouse_width_scale.inputs[1].default_value = 0.090
    tree.links.new(separate.outputs["Z"], blouse_width_scale.inputs[0])
    blouse_width_threshold = tree.nodes.new("ShaderNodeMath")
    blouse_width_threshold.operation = "ADD"
    blouse_width_threshold.inputs[1].default_value = -0.006
    tree.links.new(blouse_width_scale.outputs[0], blouse_width_threshold.inputs[0])
    blouse_width = tree.nodes.new("ShaderNodeMath")
    blouse_width.operation = "LESS_THAN"
    tree.links.new(absolute_x.outputs[0], blouse_width.inputs[0])
    tree.links.new(blouse_width_threshold.outputs[0], blouse_width.inputs[1])
    blouse_low = tree.nodes.new("ShaderNodeMath")
    blouse_low.operation = "GREATER_THAN"
    blouse_low.inputs[1].default_value = 0.735
    tree.links.new(separate.outputs["Z"], blouse_low.inputs[0])
    blouse_front_width = tree.nodes.new("ShaderNodeMath")
    blouse_front_width.operation = "MULTIPLY"
    tree.links.new(front.outputs[0], blouse_front_width.inputs[0])
    tree.links.new(blouse_width.outputs[0], blouse_front_width.inputs[1])
    blouse_mask = tree.nodes.new("ShaderNodeMath")
    blouse_mask.operation = "MULTIPLY"
    tree.links.new(blouse_front_width.outputs[0], blouse_mask.inputs[0])
    tree.links.new(blouse_low.outputs[0], blouse_mask.inputs[1])

    collar_width = tree.nodes.new("ShaderNodeMath")
    collar_width.operation = "LESS_THAN"
    collar_width.inputs[1].default_value = 0.110
    tree.links.new(absolute_x.outputs[0], collar_width.inputs[0])
    collar_low = tree.nodes.new("ShaderNodeMath")
    collar_low.operation = "GREATER_THAN"
    collar_low.inputs[1].default_value = 1.095
    tree.links.new(separate.outputs["Z"], collar_low.inputs[0])
    collar_high = tree.nodes.new("ShaderNodeMath")
    collar_high.operation = "LESS_THAN"
    collar_high.inputs[1].default_value = 1.170
    tree.links.new(separate.outputs["Z"], collar_high.inputs[0])
    collar_height = tree.nodes.new("ShaderNodeMath")
    collar_height.operation = "MULTIPLY"
    tree.links.new(collar_low.outputs[0], collar_height.inputs[0])
    tree.links.new(collar_high.outputs[0], collar_height.inputs[1])
    collar_front = tree.nodes.new("ShaderNodeMath")
    collar_front.operation = "MULTIPLY"
    tree.links.new(front.outputs[0], collar_front.inputs[0])
    tree.links.new(collar_width.outputs[0], collar_front.inputs[1])
    collar_mask = tree.nodes.new("ShaderNodeMath")
    collar_mask.operation = "MULTIPLY"
    tree.links.new(collar_front.outputs[0], collar_mask.inputs[0])
    tree.links.new(collar_height.outputs[0], collar_mask.inputs[1])
    cream_mask = tree.nodes.new("ShaderNodeMath")
    cream_mask.operation = "MAXIMUM"
    tree.links.new(blouse_mask.outputs[0], cream_mask.inputs[0])
    tree.links.new(collar_mask.outputs[0], cream_mask.inputs[1])

    peach_to_cream = tree.nodes.new("ShaderNodeMixRGB")
    peach_to_cream.inputs[1].default_value = (0.34, 0.095, 0.070, 1.0)
    peach_to_cream.inputs[2].default_value = (0.62, 0.47, 0.32, 1.0)
    tree.links.new(cream_mask.outputs[0], peach_to_cream.inputs[0])

    skirt_height = tree.nodes.new("ShaderNodeMath")
    skirt_height.operation = "LESS_THAN"
    skirt_height.inputs[1].default_value = 0.750
    tree.links.new(separate.outputs["Z"], skirt_height.inputs[0])
    garment_body_width = tree.nodes.new("ShaderNodeMath")
    garment_body_width.operation = "LESS_THAN"
    garment_body_width.inputs[1].default_value = 0.300
    tree.links.new(absolute_x.outputs[0], garment_body_width.inputs[0])
    skirt = tree.nodes.new("ShaderNodeMath")
    skirt.operation = "MULTIPLY"
    tree.links.new(skirt_height.outputs[0], skirt.inputs[0])
    tree.links.new(garment_body_width.outputs[0], skirt.inputs[1])
    upper_to_skirt = tree.nodes.new("ShaderNodeMixRGB")
    upper_to_skirt.inputs[2].default_value = (0.006, 0.043, 0.060, 1.0)
    tree.links.new(peach_to_cream.outputs[0], upper_to_skirt.inputs[1])
    tree.links.new(skirt.outputs[0], upper_to_skirt.inputs[0])

    band_low = tree.nodes.new("ShaderNodeMath")
    band_low.operation = "GREATER_THAN"
    band_low.inputs[1].default_value = 0.728
    tree.links.new(separate.outputs["Z"], band_low.inputs[0])
    band_high = tree.nodes.new("ShaderNodeMath")
    band_high.operation = "LESS_THAN"
    band_high.inputs[1].default_value = 0.765
    tree.links.new(separate.outputs["Z"], band_high.inputs[0])
    band_height = tree.nodes.new("ShaderNodeMath")
    band_height.operation = "MULTIPLY"
    tree.links.new(band_low.outputs[0], band_height.inputs[0])
    tree.links.new(band_high.outputs[0], band_height.inputs[1])
    band_mask = tree.nodes.new("ShaderNodeMath")
    band_mask.operation = "MULTIPLY"
    tree.links.new(band_height.outputs[0], band_mask.inputs[0])
    tree.links.new(garment_body_width.outputs[0], band_mask.inputs[1])
    waistband = tree.nodes.new("ShaderNodeMixRGB")
    waistband.inputs[2].default_value = (0.003, 0.024, 0.035, 1.0)
    tree.links.new(upper_to_skirt.outputs[0], waistband.inputs[1])
    tree.links.new(band_mask.outputs[0], waistband.inputs[0])

    tree.links.new(waistband.outputs[0], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


MAT_OUTFIT = mother_outfit_material()

owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)
component_vertices, component_polygons = connected_components(owned_body.data)
identity_material_names = {
    "MotherTF_ChestnutHair_SourceUV",
    "MotherTF2_SourceFacePolished",
    "MotherTF2_SourceBrowMatureContrast",
    "MotherTF_EyeMouthAlpha",
}
skin_material_name = "MotherTF_SourceBodyFaithful"
hidden_slot = next(
    index for index, material in enumerate(owned_body.data.materials)
    if material and material.name == hidden_material.name
)
native_hand_components = []
native_hand_vertices = set()
for component_id, vertices in component_vertices.items():
    points = [owned_body.matrix_world @ owned_body.data.vertices[index].co for index in vertices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    material_names = {
        owned_body.data.materials[owned_body.data.polygons[index].material_index].name
        for index in component_polygons[component_id]
        if owned_body.data.materials[owned_body.data.polygons[index].material_index]
    }
    if (
        skin_material_name in material_names
        and max(abs(lo.x), abs(hi.x)) > 0.40
        and lo.z > 0.64
        and hi.z < 0.94
        and (hi - lo).z < 0.24
    ):
        native_hand_components.append(component_id)
        native_hand_vertices.update(vertices)
if not native_hand_components or not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x > 0 for i in native_hand_vertices) or not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x < 0 for i in native_hand_vertices):
    raise RuntimeError("Could not isolate exact native Mika hands on both sides")
native_hand_coordinate_before = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_before = weight_hash(owned_body, native_hand_vertices)

identity_vertices = {
    vertex_index
    for polygon in owned_body.data.polygons
    if owned_body.data.materials[polygon.material_index]
    and owned_body.data.materials[polygon.material_index].name in identity_material_names
    for vertex_index in polygon.vertices
}
head_identity = owned_body.copy()
head_identity.data = owned_body.data.copy()
scene.collection.objects.link(head_identity)
head_identity.name = "MotherProof14OwnedMikaHeadFittedToShoulders"
head_identity.parent = None
for modifier in list(head_identity.modifiers):
    head_identity.modifiers.remove(modifier)
head_identity.animation_data_clear()
head_drop_world = 0.035
head_identity.matrix_world = Matrix.Translation((0.0, 0.0, -head_drop_world)) @ owned_body.matrix_world
head_identity_coordinate_before = coordinate_hash(head_identity, identity_vertices)
head_identity_weight_before = weight_hash(head_identity, identity_vertices)
head_hidden_slot = next(
    index for index, material in enumerate(head_identity.data.materials)
    if material and material.name == hidden_material.name
)
hidden_rear_lower_face_polygons = 0
hide_rear_lower_face_shell = True
# Keep the exact same rear-lower source-face cut when the whole owned head is
# fitted lower.  Basing this on the initial -0.075 fit prevents the extra head
# drop from accidentally deleting the ear, cheek or front chin.
rear_lower_face_cut_world_z = 1.315 - (head_drop_world - 0.075)
for polygon in head_identity.data.polygons:
    material = head_identity.data.materials[polygon.material_index]
    if material and material.name in identity_material_names:
        if material.name == "MotherTF2_SourceFacePolished":
            center = head_identity.matrix_world @ polygon.center
            if hide_rear_lower_face_shell and center.z < rear_lower_face_cut_world_z and center.y > -0.055:
                polygon.material_index = head_hidden_slot
                polygon.use_smooth = True
                hidden_rear_lower_face_polygons += 1
                continue
        polygon.use_smooth = True
        continue
    polygon.material_index = head_hidden_slot
    polygon.use_smooth = True
head_identity["fitPolicy"] = (
    "owned Mika face, eyes, brows and hair moved together as one rigid head fit; local mesh coordinates unchanged"
)
head_identity["worldDrop"] = head_drop_world
head_identity["hiddenRearLowerFacePolygons"] = hidden_rear_lower_face_polygons
head_identity["candidateClaim"] = False
head_identity["test3SakurakoExcluded"] = True

for component_id, polygon_indices in component_polygons.items():
    for polygon_index in polygon_indices:
        polygon = owned_body.data.polygons[polygon_index]
        material = owned_body.data.materials[polygon.material_index]
        if material and material.name in identity_material_names:
            polygon.material_index = hidden_slot
            polygon.use_smooth = True
            continue
        if component_id in native_hand_components:
            polygon.use_smooth = True
            continue
        polygon.material_index = hidden_slot
        polygon.use_smooth = True


def smoothstep(edge0, edge1, value):
    t = max(0.0, min(1.0, (value - edge0) / max(edge1 - edge0, 1e-9)))
    return t * t * (3.0 - 2.0 * t)


SCALE = 0.88
Y_OFFSET = -0.040
Z_OFFSET = 0.0
ARM_ANGLE = math.radians(39.0)


def deform_point(source):
    x, y, z = map(float, source)
    source_z = z
    absolute_x = abs(x)
    sign = 1.0 if x >= 0.0 else -1.0
    torso_factor = 1.0
    if 0.64 <= z <= 1.32:
        torso_factor += 0.18 * (1.0 - smoothstep(0.12, 0.24, absolute_x))
        torso_factor = 1.0 + (torso_factor - 1.0) * smoothstep(0.64, 0.76, z)
    base_x = x * torso_factor
    base_y = y * (1.12 if 0.62 <= z <= 1.30 else 1.0)
    arm_weight = 0.0
    if z >= 1.02 and absolute_x >= 0.105:
        arm_weight = smoothstep(0.105, 0.24, absolute_x)
    if arm_weight > 0.0:
        pivot_x = 0.15 * sign
        pivot_z = 1.24
        dx = (x - pivot_x) * 1.25
        dz = z - pivot_z
        angle = -sign * ARM_ANGLE
        rotated_x = pivot_x + dx * math.cos(angle) - dz * math.sin(angle)
        rotated_z = pivot_z + dx * math.sin(angle) + dz * math.cos(angle)
        x = base_x * (1.0 - arm_weight) + rotated_x * arm_weight
        z = z * (1.0 - arm_weight) + rotated_z * arm_weight
        y = base_y * (1.0 - arm_weight) + y * 1.10 * arm_weight
    else:
        x, y = base_x, base_y
    leg_height_weight = 1.0 - smoothstep(0.58, 0.79, z)
    leg_side_weight = smoothstep(0.001, 0.012, absolute_x)
    leg_weight = leg_height_weight * leg_side_weight
    if leg_weight > 0.0:
        straight_leg_x = sign * (0.120 + (absolute_x - 0.045) * 1.18)
        x = x * (1.0 - leg_weight) + straight_leg_x * leg_weight
        y = y * (1.0 - leg_weight) + (-0.002 + (y + 0.002) * 1.08) * leg_weight
    final_z = z * SCALE + Z_OFFSET
    # The owned Mika chin sits higher than the raw VRoid shoulder/neck stump.
    # Lift only the torso centre and collar region; sleeve ends stay fixed on
    # the untouched native hands.  This removes the giraffe-neck read while
    # retaining the overlap bridge under the chin.
    torso_center_weight = 1.0 - smoothstep(0.10, 0.25, absolute_x)
    upper_torso_weight = smoothstep(1.02, 1.27, source_z)
    final_z += 0.045 * torso_center_weight * upper_torso_weight
    if z < 0.35:
        # Let the calves overlap the shoe collars.  The enclosed foot faces are
        # still removed below, so this closes the visible ankle gap without
        # allowing skin to poke through the toe or instep.
        final_z -= 0.036 * (1.0 - smoothstep(0.175, 0.35, z))
    return Vector((x * SCALE, y * SCALE + Y_OFFSET, final_z))


def garment_point(source):
    result = deform_point(source)
    z = float(source.z)
    if z < 0.82:
        normalized = max(0.0, min(1.0, (z - 0.205) / (0.82 - 0.205)))
        result.z = 0.335 + normalized * (0.735 - 0.335)
        hem_weight = 1.0 - smoothstep(0.205, 0.82, z)
        # A skirt is not a pair of legs.  Applying the straight-leg correction
        # to it created the peach side wedges seen in Proof 7.  Preserve the
        # VRoid dress cross-section directly, with only a gentle authority-like
        # A-line expansion toward the calf-length hem.
        result.x = float(source.x) * SCALE * (1.15 - 0.15 * (1.0 - hem_weight))
        result.y = Y_OFFSET + float(source.y) * SCALE * (1.06 - 0.06 * (1.0 - hem_weight))
    return result


def shoe_point(source):
    result = deform_point(source)
    result.z = 0.012 + max(0.0, float(source.z) - 0.0005) * SCALE * 0.72
    result.x *= 1.04
    result.y = -0.040 + (result.y + 0.040) * 1.07
    return result


def new_mesh_object(name, source_coords, source_faces, material, point_fn):
    used = sorted({index for face in source_faces for index in face})
    remap = {source_index: new_index for new_index, source_index in enumerate(used)}
    vertices = [tuple(point_fn(source_coords[index])) for index in used]
    faces = [tuple(remap[index] for index in face) for face in source_faces]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(material)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


# Copy the exact neck object from the user-approved Yuuka sister candidate.
# Preserve its 40-segment topology and tapered cross-sections.  Scale X/Y to
# the Mika scene and compress only its vertical span so it overlaps the blouse
# and chin without recreating the rejected long-neck silhouette.
with bpy.data.libraries.load(str(YUUKA_NECK_BLEND), link=False) as (data_from, data_to):
    if "SisterProof46SmoothNeckBridge" not in data_from.objects:
        raise RuntimeError("Approved Yuuka blend is missing SisterProof46SmoothNeckBridge")
    data_to.objects = ["SisterProof46SmoothNeckBridge"]
approved_yuuka_neck = data_to.objects[0]
if len(approved_yuuka_neck.data.vertices) != 160 or len(approved_yuuka_neck.data.polygons) != 120:
    raise RuntimeError("Approved Yuuka neck topology changed")
yuuka_neck_coords = []
for vertex in approved_yuuka_neck.data.vertices:
    source = vertex.co
    target_z = 1.105 + (source.z - 0.620) * (0.120 / 0.140)
    yuuka_neck_coords.append(Vector((source.x * 1.80, source.y * 1.80, target_z)))
yuuka_neck_faces = [tuple(polygon.vertices) for polygon in approved_yuuka_neck.data.polygons]
yuuka_neck_source_vertices = list(range(len(yuuka_neck_coords)))
yuuka_neck_coordinate_digest = hashlib.sha256(
    "".join(
        f"{index}:{point.x:.9f},{point.y:.9f},{point.z:.9f};"
        for index, point in zip(yuuka_neck_source_vertices, yuuka_neck_coords)
    ).encode()
).hexdigest()
bpy.data.objects.remove(approved_yuuka_neck, do_unlink=True)


before_import = set(bpy.data.objects)
bpy.ops.wm.obj_import(
    filepath=str(VROID_OBJ),
    forward_axis="NEGATIVE_Z",
    up_axis="Y",
    use_split_groups=True,
    use_split_objects=True,
)
imported = [obj for obj in bpy.data.objects if obj not in before_import and obj.type == "MESH"]
for obj in imported:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
body_source = next((obj for obj in imported if obj.name.startswith("Body_0")), None)
outfit_source = next((obj for obj in imported if obj.name.startswith("Body_1")), None)
shoe_source = next((obj for obj in imported if obj.name.startswith("Body_3")), None)
if body_source is None or outfit_source is None or shoe_source is None:
    raise RuntimeError("Expected VRoid Body_0, Body_1 and Body_3 submeshes")


def source_data(obj):
    return (
        [vertex.co.copy() for vertex in obj.data.vertices],
        [tuple(polygon.vertices) for polygon in obj.data.polygons],
    )


body_coords, body_faces_all = source_data(body_source)
outfit_coords, outfit_faces = source_data(outfit_source)
shoe_coords, shoe_faces = source_data(shoe_source)


def remove_body_face(face):
    coords = [body_coords[index] for index in face]
    if any(co.z > 1.320 and abs(co.x) < 0.18 for co in coords):
        return True
    if all(co.z > 0.98 and abs(co.x) > 0.550 for co in coords):
        return True
    # The fitted Body_3 shoe surface owns the visible feet below the ankle.
    # Removing the enclosed skin prevents toe/instep poke-through that reads
    # like white laces on the dark-brown loafer.
    if all(co.z < 0.175 for co in coords):
        return True
    return False


body_faces = [face for face in body_faces_all if not remove_body_face(face)]
for obj in imported:
    bpy.data.objects.remove(obj, do_unlink=True)

continuous_skin = new_mesh_object(
    "MotherProof6ContinuousVRoidBody", body_coords, body_faces, MAT_SKIN, deform_point
)


def merge_owned_yuuka_neck_into_skin(obj, source_vertices, source_faces):
    """Merge Yuuka's exact owned neck surface into the continuous skin object."""
    old_mesh = obj.data
    vertices = [tuple(vertex.co) for vertex in old_mesh.vertices]
    faces = [tuple(polygon.vertices) for polygon in old_mesh.polygons]
    offset = len(vertices)
    vertices.extend(tuple(point) for point in source_vertices)
    faces.extend(tuple(offset + index for index in face) for face in source_faces)

    replacement = bpy.data.meshes.new(old_mesh.name + "WithOwnedYuukaNeck")
    replacement.from_pydata(vertices, [], faces)
    replacement.update(calc_edges=True)
    replacement.materials.append(MAT_SKIN)
    for polygon in replacement.polygons:
        polygon.use_smooth = True
    obj.data = replacement
    bpy.data.meshes.remove(old_mesh)
    obj["neckSurfacePolicy"] = (
        "approved Yuuka sister 120-polygon tapered neck copied into continuous skin; no new procedural substitute"
    )
    obj["neckSourcePolygonCount"] = len(source_faces)
    obj["neckSourceVertexCount"] = len(source_vertices)
    obj["neckSourceCoordinateSha256"] = yuuka_neck_coordinate_digest
    return len(source_faces)


neck_source_polygon_count = merge_owned_yuuka_neck_into_skin(
    continuous_skin, yuuka_neck_coords, yuuka_neck_faces
)
continuous_outfit = new_mesh_object(
    "MotherProof6ContinuousCardiganBlouseSkirt", outfit_coords, outfit_faces, MAT_OUTFIT, garment_point
)


def merge_high_blouse_collar_into_outfit(obj):
    """Merge a rounded high-neck collar shell into the continuous garment object."""
    old_mesh = obj.data
    vertices = [tuple(vertex.co) for vertex in old_mesh.vertices]
    faces = [tuple(polygon.vertices) for polygon in old_mesh.polygons]
    segments = 64
    # Ordered around the collar cross-section: outer wall, top lip, inner wall,
    # lower overlap.  The last loop closes inside the existing blouse surface.
    loop_specs = (
        (1.115, 0.083, 0.070, -0.005),
        (1.195, 0.070, 0.061, 0.003),
        (1.193, 0.057, 0.048, 0.003),
        (1.125, 0.065, 0.057, -0.005),
    )
    loops = []
    for z, radius_x, radius_y, center_y in loop_specs:
        loop = []
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            loop.append(len(vertices))
            vertices.append((
                radius_x * math.cos(angle),
                center_y + radius_y * math.sin(angle),
                z,
            ))
        loops.append(loop)
    for loop_index in range(len(loops)):
        current = loops[loop_index]
        following = loops[(loop_index + 1) % len(loops)]
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((
                current[segment],
                current[next_segment],
                following[next_segment],
                following[segment],
            ))
    replacement = bpy.data.meshes.new(old_mesh.name + "WithMergedHighCollar")
    replacement.from_pydata(vertices, [], faces)
    replacement.update(calc_edges=True)
    replacement.materials.append(MAT_OUTFIT)
    for polygon in replacement.polygons:
        polygon.use_smooth = True
    obj.data = replacement
    bpy.data.meshes.remove(old_mesh)
    obj["highCollarPolicy"] = "rounded cream collar shell merged into the continuous outfit object"
    obj["highCollarSegments"] = segments
    obj["highCollarTopZ"] = 1.195
    return segments


# A raised procedural collar was tested but read as another rectangular neck
# block in profile.  Preserve the fitted continuous authority garment instead.
high_collar_segments = 0
fitted_shoes = new_mesh_object(
    "MotherProof6FittedDarkBrownShoes", shoe_coords, shoe_faces, MAT_SHOE, shoe_point
)
continuous_skin["surfacePolicy"] = (
    "one merged skin object with an overlap-closed neck, torso, arms, pelvis, legs, ankles and feet"
)
continuous_outfit["surfacePolicy"] = "one continuous VRoid garment shaded as cardigan, blouse and calf A-line skirt"
fitted_shoes["surfacePolicy"] = "paired VRoid shoe surface deformed with exact same feet"


def add_fitted_button(name, x, z):
    surface = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in continuous_outfit.data.vertices],
        [tuple(polygon.vertices) for polygon in continuous_outfit.data.polygons],
    )
    location, normal, _face_index, _distance = surface.ray_cast(
        Vector((x, -1.0, z)), Vector((0.0, 1.0, 0.0)), 2.0
    )
    if location is None or normal is None or normal.y > -0.18:
        raise RuntimeError(f"Could not fit cardigan button {name} to the front surface")
    normal.normalize()
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=20,
        ring_count=12,
        location=location + normal * 0.0022,
    )
    button = bpy.context.object
    button.name = name
    button.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(normal).to_euler()
    button.scale = (0.0090, 0.0090, 0.0036)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    button.data.materials.append(MAT_BUTTON)
    for polygon in button.data.polygons:
        polygon.use_smooth = True
    button["surfacePolicy"] = "front-surface raycast fit; button center partially embedded"
    button["surfaceContactDistance"] = 0.0022
    return button


fitted_buttons = [
    add_fitted_button(f"MotherProof6FittedCardiganButton{index}", 0.125, z)
    for index, z in enumerate((0.825, 0.905, 0.985, 1.065), start=1)
]


def add_fitted_watch():
    # The authority wears the watch on her left wrist (camera-right in the
    # canonical front view).  This center sits in the intentional overlap of
    # the connected sleeve cuff and the untouched native hand component.
    wrist_center = Vector((0.510, -0.058, 0.755))
    arm_axis = Vector((0.58, 0.0, -0.81)).normalized()
    bpy.ops.mesh.primitive_torus_add(
        major_segments=32,
        minor_segments=12,
        major_radius=0.031,
        minor_radius=0.0055,
        location=wrist_center,
    )
    band = bpy.context.object
    band.name = "MotherProof6FittedLeftWristWatchBand"
    band.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(arm_axis).to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    band.data.materials.append(MAT_WATCH_BAND)
    for polygon in band.data.polygons:
        polygon.use_smooth = True
    band["surfacePolicy"] = "wraps the sleeve-hand wrist overlap; no hand coordinate or weight edits"

    face_normal = Vector((0.0, -1.0, 0.0))
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.014,
        depth=0.006,
        location=wrist_center + face_normal * 0.034,
    )
    face = bpy.context.object
    face.name = "MotherProof6FittedLeftWristWatchFace"
    face.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(face_normal).to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    face.data.materials.append(MAT_WATCH_FACE)
    for polygon in face.data.polygons:
        polygon.use_smooth = True
    face["surfacePolicy"] = "watch face intersects fitted wrist band by 0.003"
    return band, face


watch_band, watch_face = add_fitted_watch()


def add_pearl(name, x):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=(x, -0.182, 1.395))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (0.011, 0.009, 0.011)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(MAT_PEARL)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


generated_objects = [
    continuous_skin,
    continuous_outfit,
    fitted_shoes,
    *fitted_buttons,
    watch_band,
    watch_face,
]
for obj in generated_objects:
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.10
views = {
    "front": ((0.0, -4.25, 0.95), (0.0, 0.0, 0.91)),
    "three-quarter": ((2.95, -3.50, 1.01), (0.0, 0.0, 0.92)),
    "side": ((4.25, 0.0, 0.98), (0.0, 0.0, 0.92)),
    "back": ((0.0, 4.25, 0.95), (0.0, 0.0, 0.91)),
}


def render_set(prefix):
    paths = []
    for label, (location, target) in views.items():
        camera.location = location
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = OUTPUT / f"{prefix}-{label}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path))
    return paths


color_paths = render_set("mother-proof6-continuous-office-outfit-color")
owned_material_backup = list(owned_body.data.materials)
owned_index_backup = [polygon.material_index for polygon in owned_body.data.polygons]
head_material_backup = list(head_identity.data.materials)
head_index_backup = [polygon.material_index for polygon in head_identity.data.polygons]
generated_material_backups = {obj.name: list(obj.data.materials) for obj in generated_objects}
owned_body.data.materials.clear()
owned_body.data.materials.append(MAT_GRAY)
owned_body.data.materials.append(hidden_material)
for polygon, previous_index in zip(owned_body.data.polygons, owned_index_backup):
    previous = owned_material_backup[previous_index]
    polygon.material_index = 1 if previous == hidden_material else 0
head_identity.data.materials.clear()
head_identity.data.materials.append(MAT_GRAY)
head_identity.data.materials.append(hidden_material)
for polygon, previous_index in zip(head_identity.data.polygons, head_index_backup):
    previous = head_material_backup[previous_index]
    polygon.material_index = 1 if previous == hidden_material else 0
for obj in generated_objects:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
gray_paths = render_set("mother-proof6-continuous-office-outfit-gray")
owned_body.data.materials.clear()
for material in owned_material_backup:
    owned_body.data.materials.append(material)
for polygon, index in zip(owned_body.data.polygons, owned_index_backup):
    polygon.material_index = index
head_identity.data.materials.clear()
for material in head_material_backup:
    head_identity.data.materials.append(material)
for polygon, index in zip(head_identity.data.polygons, head_index_backup):
    polygon.material_index = index
for obj in generated_objects:
    obj.data.materials.clear()
    for material in generated_material_backups[obj.name]:
        obj.data.materials.append(material)

owned_coordinate_after = coordinate_hash(owned_body)
owned_weight_after = weight_hash(owned_body)
native_hand_coordinate_after = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_after = weight_hash(owned_body, native_hand_vertices)
head_identity_coordinate_after = coordinate_hash(head_identity, identity_vertices)
head_identity_weight_after = weight_hash(head_identity, identity_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if owned_coordinate_before != owned_coordinate_after or owned_weight_before != owned_weight_after:
    raise RuntimeError("Mother Proof6 changed owned Mika coordinates or weights")
if native_hand_coordinate_before != native_hand_coordinate_after or native_hand_weight_before != native_hand_weight_after:
    raise RuntimeError("Mother Proof6 changed exact native Mika hand coordinates or weights")
if head_identity_coordinate_before != head_identity_coordinate_after or head_identity_weight_before != head_identity_weight_after:
    raise RuntimeError("Mother Proof6 changed owned Mika head local coordinates or weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Mother Proof6 changed owned Mika rig bone names")

blend_path = OUTPUT / "mother-proof6-continuous-office-outfit-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
receipt = {
    "schema": "family-company.mother-proof6-continuous-office-outfit-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING_ROOT_REVIEW",
    "candidateClaim": False,
    "promotionBlocked": True,
    "identitySource": "user-owned Mika-derived face, brows, eye/mouth, chestnut hair and native hands; user-owned Yuuka native neck",
    "outfitAuthority": str(REFERENCE),
    "outfitAuthoritySha256": sha256(REFERENCE),
    "vroidSource": {
        "mesh": "VRoid Studio 2.14.0 built-in Body_0, Body_1 and Body_3",
        "officialGuidelines": "https://vroid.com/en/studio/guidelines",
        "obj": str(VROID_OBJ),
        "objSha256": sha256(VROID_OBJ),
    },
    "yuukaNeckSource": {
        "ownedFbx": str(YUUKA_FBX),
        "ownedFbxSha256": sha256(YUUKA_FBX),
        "approvedBlend": str(YUUKA_NECK_BLEND),
        "approvedBlendSha256": sha256(YUUKA_NECK_BLEND),
        "object": "SisterProof46SmoothNeckBridge",
        "polygonCount": neck_source_polygon_count,
        "vertexCount": len(yuuka_neck_coords),
        "coordinateSha256": yuuka_neck_coordinate_digest,
    },
    "test3SakurakoExcluded": True,
    "retained": {
        "ownedCoordinatesExact": owned_coordinate_before == owned_coordinate_after,
        "ownedWeightsExact": owned_weight_before == owned_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "fittedHeadObject": head_identity.name,
        "headLocalCoordinatesExact": head_identity_coordinate_before == head_identity_coordinate_after,
        "headLocalWeightsExact": head_identity_weight_before == head_identity_weight_after,
        "headRigidWorldDrop": head_drop_world,
        "hiddenRearLowerFacePolygons": hidden_rear_lower_face_polygons,
        "nativeHandComponents": sorted(native_hand_components),
        "nativeHandsExact": native_hand_coordinate_before == native_hand_coordinate_after and native_hand_weight_before == native_hand_weight_after,
        "nativeHandDescription": "original 3-digit stylized hand",
    },
    "surfacePolicy": {
        "continuousSkin": continuous_skin.name,
        "neckVisuallyConnectedByOverlap": True,
        "neckMergedIntoContinuousSkinObject": True,
        "neckSource": "approved Yuuka sister SmoothNeckBridge topology and taper",
        "neckProceduralSubstitute": False,
        "neckSourcePolygonCount": neck_source_polygon_count,
        "neckSourceCoordinateSha256": yuuka_neck_coordinate_digest,
        "neckSourceBounds": {
            "min": [min(point[axis] for point in yuuka_neck_coords) for axis in range(3)],
            "max": [max(point[axis] for point in yuuka_neck_coords) for axis in range(3)],
        },
        "highBlouseCollarMergedIntoOutfit": False,
        "highBlouseCollarSegments": high_collar_segments,
        "headLoweredWithFaceEyesHairTogether": True,
        "continuousOutfit": continuous_outfit.name,
        "outfitSingleMeshObject": True,
        "cardiganBlouseSkirtSameSurface": True,
        "fittedShoes": fitted_shoes.name,
        "fittedCardiganButtons": [button.name for button in fitted_buttons],
        "buttonContactDistance": 0.0022,
        "fittedWatch": {"band": watch_band.name, "face": watch_face.name},
        "watchPreservesNativeHand": True,
        "detachedGarmentPanels": 0,
        "proceduralGarmentBoxesOrPlates": 0,
        "pearlAccessoryObjects": [],
    },
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static visual gate only; no GIF, rig transfer, Unity, motion or production claim",
        "user visual approval is required before motion work",
        "VRoid body and garment are not yet transferred to the owned Mika rig",
    ],
}
(OUTPUT / "mother-proof6-continuous-office-outfit-gate-receipt.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("MOTHER_PROOF6_CONTINUOUS_OFFICE_OUTFIT_GATE_RENDERED")
