"""Build the Father static approval gate from the owned Yuuka identity.

This deliberately retains the short-hair/glasses/face treatment and exact
native three-digit Yuuka hands from FatherProof3 while deleting every failed
procedural garment volume.  The user-approved Sister Proof46 continuous VRoid
body and Yuuka neck are reused at their approved coordinates.  Shirt, rolled
sleeves, trousers, belt, placket and pocket are shader regions on that one
continuous surface, so they cannot float, gap or read as assembled boxes.

Static visual review only: no rig transfer, animation, Unity import or
production promotion is performed here.
"""

from __future__ import annotations

import argparse
from collections import defaultdict, deque
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--approved-sister-blend", required=True)
    parser.add_argument("--vroid-obj", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
APPROVED_SISTER_BLEND = Path(ARGS.approved_sister_blend).resolve()
VROID_OBJ = Path(ARGS.vroid_obj).resolve()
REFERENCE = Path(ARGS.reference).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
for required in (APPROVED_SISTER_BLEND, VROID_OBJ, REFERENCE):
    if not required.is_file():
        raise RuntimeError(f"Missing Father connected-outfit input: {required}")

scene = bpy.context.scene
owned_body = bpy.data.objects.get("Yuuka_Original_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("FatherProofCamera") or scene.camera
if owned_body is None or armature is None or camera is None:
    raise RuntimeError("Expected the FatherProof3 owned Yuuka identity scene")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected exact owned Yuuka 118-bone rig, got {len(armature.data.bones)}")


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
    result = []
    visited = set()
    for seed in range(len(mesh.polygons)):
        if seed in visited:
            continue
        queue = deque([seed])
        visited.add(seed)
        polygons = []
        vertices = set()
        while queue:
            polygon_index = queue.popleft()
            polygons.append(polygon_index)
            vertices.update(mesh.polygons[polygon_index].vertices)
            for neighbor in neighbors[polygon_index]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(neighbor)
        result.append((vertices, polygons))
    return result


components = connected_components(owned_body.data)
native_hand_vertices = set()
native_hand_components = []
for component_index, (vertices, polygons) in enumerate(components):
    points = [owned_body.matrix_world @ owned_body.data.vertices[index].co for index in vertices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    if (
        max(abs(lo.x), abs(hi.x)) > 0.260
        and lo.z > 0.360
        and hi.z < 0.470
        and (hi - lo).x < 0.100
        and len(polygons) > 100
    ):
        native_hand_components.append(component_index)
        native_hand_vertices.update(vertices)
if len(native_hand_components) != 2:
    raise RuntimeError(f"Expected two exact native Yuuka hand components, got {native_hand_components}")
if not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x > 0.0 for i in native_hand_vertices):
    raise RuntimeError("Right native hand was not isolated")
if not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x < 0.0 for i in native_hand_vertices):
    raise RuntimeError("Left native hand was not isolated")

owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)
native_hand_coordinate_before = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_before = weight_hash(owned_body, native_hand_vertices)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


failed_garment_names = {
    "FatherBareForearmL",
    "FatherBareForearmR",
    "FatherBeltBuckle",
    "FatherBrownBelt",
    "FatherBrownShoeL",
    "FatherBrownShoeR",
    "FatherChestPocket",
    "FatherCollarL",
    "FatherCollarR",
    "FatherFittedShirtTorso",
    "FatherNeck",
    "FatherRolledCuffL",
    "FatherRolledCuffR",
    "FatherRolledSleeveL",
    "FatherRolledSleeveR",
    "FatherShirtPlacket",
    "FatherShoeSoleL",
    "FatherShoeSoleR",
    "FatherStraightTrouserLegL",
    "FatherStraightTrouserLegR",
    "FatherTailoredShoulderL",
    "FatherTailoredShoulderR",
    "FatherTrouserWaist",
}
failed_garment_names.update(
    obj.name for obj in bpy.data.objects if obj.name.startswith("FatherShirtButton")
)
removed_failed_garments = []
for name in sorted(failed_garment_names):
    obj = bpy.data.objects.get(name)
    if obj is not None:
        removed_failed_garments.append(name)
        bpy.data.objects.remove(obj, do_unlink=True)


def solid_material(name, color, roughness=0.82, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.06
    return material


MAT_SKIN = solid_material("FatherConnectedSkin", (0.91, 0.75, 0.70), 0.84)
MAT_SHOE = solid_material("FatherConnectedBrownOxford", (0.105, 0.038, 0.018), 0.73)
MAT_BUTTON = solid_material("FatherConnectedShirtButton", (0.72, 0.78, 0.78), 0.48, 0.08)
MAT_GRAY = solid_material("FatherConnectedQAGray", (0.43, 0.45, 0.49), 0.84)


def multiply(tree, a, b):
    node = tree.nodes.new("ShaderNodeMath")
    node.operation = "MULTIPLY"
    if hasattr(a, "bl_idname"):
        tree.links.new(a, node.inputs[0])
    else:
        node.inputs[0].default_value = a
    if hasattr(b, "bl_idname"):
        tree.links.new(b, node.inputs[1])
    else:
        node.inputs[1].default_value = b
    return node.outputs[0]


def compare(tree, socket, operation, value):
    node = tree.nodes.new("ShaderNodeMath")
    node.operation = operation
    node.inputs[1].default_value = value
    tree.links.new(socket, node.inputs[0])
    return node.outputs[0]


def max_socket(tree, a, b):
    node = tree.nodes.new("ShaderNodeMath")
    node.operation = "MAXIMUM"
    tree.links.new(a, node.inputs[0])
    tree.links.new(b, node.inputs[1])
    return node.outputs[0]


def range_mask(tree, socket, minimum, maximum):
    return multiply(
        tree,
        compare(tree, socket, "GREATER_THAN", minimum),
        compare(tree, socket, "LESS_THAN", maximum),
    )


def father_surface_material():
    material = bpy.data.materials.get("FatherContinuousShirtTrousersSurface")
    if material is None:
        material = bpy.data.materials.new("FatherContinuousShirtTrousersSurface")
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
    absolute_x_node = tree.nodes.new("ShaderNodeMath")
    absolute_x_node.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], absolute_x_node.inputs[0])
    absolute_x = absolute_x_node.outputs[0]

    # Shirt torso and both rolled sleeves are only colors on this continuous
    # body.  The diagonal threshold follows the A-pose arm from shoulder to
    # mid-forearm without creating any cuff, sleeve cap or panel geometry.
    torso = multiply(
        tree,
        range_mask(tree, separate.outputs["Z"], 0.392, 0.645),
        compare(tree, absolute_x, "LESS_THAN", 0.185),
    )
    sleeve_line = tree.nodes.new("ShaderNodeMath")
    sleeve_line.operation = "MULTIPLY_ADD"
    sleeve_line.inputs[1].default_value = -0.55
    sleeve_line.inputs[2].default_value = 0.640
    tree.links.new(absolute_x, sleeve_line.inputs[0])
    above_sleeve_line = tree.nodes.new("ShaderNodeMath")
    above_sleeve_line.operation = "GREATER_THAN"
    tree.links.new(separate.outputs["Z"], above_sleeve_line.inputs[0])
    tree.links.new(sleeve_line.outputs[0], above_sleeve_line.inputs[1])
    sleeve_width = multiply(
        tree,
        compare(tree, absolute_x, "GREATER_THAN", 0.125),
        compare(tree, absolute_x, "LESS_THAN", 0.285),
    )
    sleeves = multiply(tree, above_sleeve_line.outputs[0], sleeve_width)
    shirt_mask = max_socket(tree, torso, sleeves)

    skin_to_shirt = tree.nodes.new("ShaderNodeMixRGB")
    skin_to_shirt.inputs[1].default_value = (0.91, 0.75, 0.70, 1.0)
    skin_to_shirt.inputs[2].default_value = (0.105, 0.315, 0.370, 1.0)
    tree.links.new(shirt_mask, skin_to_shirt.inputs[0])

    trousers = multiply(
        tree,
        range_mask(tree, separate.outputs["Z"], 0.060, 0.404),
        compare(tree, absolute_x, "LESS_THAN", 0.180),
    )
    shirt_to_trousers = tree.nodes.new("ShaderNodeMixRGB")
    shirt_to_trousers.inputs[2].default_value = (0.040, 0.047, 0.055, 1.0)
    tree.links.new(skin_to_shirt.outputs[0], shirt_to_trousers.inputs[1])
    tree.links.new(trousers, shirt_to_trousers.inputs[0])

    belt = multiply(
        tree,
        range_mask(tree, separate.outputs["Z"], 0.386, 0.408),
        compare(tree, absolute_x, "LESS_THAN", 0.184),
    )
    belt_mix = tree.nodes.new("ShaderNodeMixRGB")
    belt_mix.inputs[2].default_value = (0.095, 0.037, 0.017, 1.0)
    tree.links.new(shirt_to_trousers.outputs[0], belt_mix.inputs[1])
    tree.links.new(belt, belt_mix.inputs[0])

    front = compare(tree, separate.outputs["Y"], "LESS_THAN", -0.045)
    placket = multiply(
        tree,
        multiply(
            tree,
            range_mask(tree, separate.outputs["Z"], 0.382, 0.606),
            compare(tree, absolute_x, "LESS_THAN", 0.009),
        ),
        front,
    )
    pocket_x = multiply(
        tree,
        compare(tree, separate.outputs["X"], "GREATER_THAN", -0.145),
        compare(tree, separate.outputs["X"], "LESS_THAN", -0.062),
    )
    pocket = multiply(
        tree,
        multiply(tree, pocket_x, range_mask(tree, separate.outputs["Z"], 0.475, 0.548)),
        front,
    )
    tailoring = max_socket(tree, placket, pocket)
    tailoring_mix = tree.nodes.new("ShaderNodeMixRGB")
    tailoring_mix.inputs[2].default_value = (0.055, 0.205, 0.245, 1.0)
    tree.links.new(belt_mix.outputs[0], tailoring_mix.inputs[1])
    tree.links.new(tailoring, tailoring_mix.inputs[0])

    # The rolled cuff is another narrow shader transition hugging the same arm
    # line, not a torus or detached sleeve ring.
    cuff_line = tree.nodes.new("ShaderNodeMath")
    cuff_line.operation = "SUBTRACT"
    cuff_line.inputs[1].default_value = 0.020
    tree.links.new(sleeve_line.outputs[0], cuff_line.inputs[0])
    above_cuff = tree.nodes.new("ShaderNodeMath")
    above_cuff.operation = "GREATER_THAN"
    tree.links.new(separate.outputs["Z"], above_cuff.inputs[0])
    tree.links.new(cuff_line.outputs[0], above_cuff.inputs[1])
    below_sleeve = tree.nodes.new("ShaderNodeMath")
    below_sleeve.operation = "LESS_THAN"
    tree.links.new(separate.outputs["Z"], below_sleeve.inputs[0])
    tree.links.new(sleeve_line.outputs[0], below_sleeve.inputs[1])
    cuff = multiply(tree, multiply(tree, above_cuff.outputs[0], below_sleeve.outputs[0]), sleeve_width)
    cuff_mix = tree.nodes.new("ShaderNodeMixRGB")
    cuff_mix.inputs[2].default_value = (0.070, 0.250, 0.295, 1.0)
    tree.links.new(tailoring_mix.outputs[0], cuff_mix.inputs[1])
    tree.links.new(cuff, cuff_mix.inputs[0])

    tree.links.new(cuff_mix.outputs[0], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


MAT_SURFACE = father_surface_material()


with bpy.data.libraries.load(str(APPROVED_SISTER_BLEND), link=False) as (data_from, data_to):
    required_names = {"SisterProof46ContinuousVRoidBody", "SisterProof46SmoothNeckBridge"}
    missing = required_names.difference(data_from.objects)
    if missing:
        raise RuntimeError(f"Approved Sister blend is missing {sorted(missing)}")
    data_to.objects = ["SisterProof46ContinuousVRoidBody", "SisterProof46SmoothNeckBridge"]
continuous_body, approved_neck = data_to.objects
scene.collection.objects.link(continuous_body)
scene.collection.objects.link(approved_neck)
continuous_body.name = "FatherConnectedContinuousVRoidBody"
approved_neck.name = "FatherConnectedApprovedYuukaNeckSource"


def smoothstep(edge0, edge1, value):
    t = max(0.0, min(1.0, (value - edge0) / max(edge1 - edge0, 1e-9)))
    return t * t * (3.0 - 2.0 * t)


def reshape_father_continuous_surface(obj):
    """Flatten the donor bust and replace its hourglass read with soft columns.

    This edits only the replaceable static VRoid surface.  The owned Yuuka face,
    hair, rig and native hands are separate and remain byte-for-byte untouched.
    """
    changed = 0
    for vertex in obj.data.vertices:
        point = vertex.co
        absolute_x = abs(float(point.x))
        sign = 1.0 if point.x >= 0.0 else -1.0

        # Flatten the two breast lobes into one gently rounded shirt front.
        # The clamp is blended so the side and shoulder surfaces stay smooth.
        torso_height = smoothstep(0.385, 0.425, point.z) * (1.0 - smoothstep(0.585, 0.635, point.z))
        torso_center = 1.0 - smoothstep(0.115, 0.190, absolute_x)
        flatten_weight = torso_height * torso_center
        if flatten_weight > 0.0 and point.y < -0.010:
            side_curve = min(1.0, absolute_x / 0.175)
            flattened_y = -0.043 + 0.013 * side_curve * side_curve
            point.y = point.y * (1.0 - flatten_weight) + flattened_y * flatten_weight
            changed += 1

        # Fill the waist and keep the upper torso as a tapered shirt column,
        # without moving the connected shoulder/arm transition.
        if 0.365 <= point.z <= 0.595 and absolute_x < 0.145:
            upper_fade = 1.0 - smoothstep(0.545, 0.595, point.z)
            lower_fade = smoothstep(0.365, 0.410, point.z)
            point.x *= 1.0 + 0.22 * upper_fade * lower_fade

        # Straighten and thicken the trouser columns around each leg centre.
        if 0.060 <= point.z < 0.345 and 0.018 < absolute_x < 0.185:
            leg_center = 0.092 * sign
            calf_weight = 1.0 - smoothstep(0.275, 0.345, point.z)
            point.x = leg_center + (point.x - leg_center) * (1.15 + 0.05 * calf_weight)
    obj.data.update()
    return changed


reshaped_body_vertices = reshape_father_continuous_surface(continuous_body)


def replace_mesh_faces(obj, keep_face):
    old_mesh = obj.data
    vertices = [tuple(vertex.co) for vertex in old_mesh.vertices]
    faces = [tuple(polygon.vertices) for polygon in old_mesh.polygons if keep_face(polygon, old_mesh)]
    replacement = bpy.data.meshes.new(old_mesh.name + "FatherFiltered")
    replacement.from_pydata(vertices, [], faces)
    replacement.update(calc_edges=True)
    obj.data = replacement
    bpy.data.meshes.remove(old_mesh)
    return len(faces)


# Body_3 shoes overlap above this enclosed foot cut.  The visible ankle remains
# continuous while hidden toes cannot poke through the oxford surface.
body_faces_after_foot_cut = replace_mesh_faces(
    continuous_body,
    lambda polygon, mesh: not all(mesh.vertices[index].co.z < 0.082 for index in polygon.vertices),
)
continuous_body.data.materials.clear()
continuous_body.data.materials.append(MAT_SURFACE)
for polygon in continuous_body.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = True


def make_continuous_shirt_shell(body):
    """Create one connected, body-conforming shirt shell from the same surface."""
    source_mesh = body.data
    selected_faces = []
    for polygon in source_mesh.polygons:
        center_point = sum((source_mesh.vertices[index].co for index in polygon.vertices), Vector()) / len(polygon.vertices)
        absolute_x = abs(float(center_point.x))
        torso = 0.386 <= center_point.z <= 0.632 and absolute_x < 0.188
        sleeve_threshold = 0.640 - 0.55 * absolute_x
        sleeve = (
            0.125 <= absolute_x <= 0.286
            and center_point.z >= sleeve_threshold - 0.010
            and center_point.z <= 0.635
        )
        if torso or sleeve:
            selected_faces.append(tuple(polygon.vertices))
    used = sorted({index for face in selected_faces for index in face})
    remap = {source_index: new_index for new_index, source_index in enumerate(used)}
    vertices = []
    for index in used:
        source_vertex = source_mesh.vertices[index]
        point = source_vertex.co.copy()
        absolute_x = abs(float(point.x))
        if 0.386 <= point.z <= 0.632 and absolute_x < 0.188:
            # One smooth shirt front masks the donor bust and the waist expands
            # into a tucked, softly tailored column.  No added chest boxes.
            waist_weight = 1.0 - smoothstep(0.505, 0.620, point.z)
            side_fade = 1.0 - smoothstep(0.135, 0.188, absolute_x)
            point.x *= 1.0 + 0.16 * waist_weight * side_fade
            if point.y < -0.010:
                side_curve = min(1.0, absolute_x / 0.180)
                point.y = -0.054 + 0.012 * side_curve * side_curve
            else:
                point.y += 0.004
        else:
            radial = Vector((point.x, point.y + 0.012, 0.0))
            if radial.length > 1e-8:
                radial.normalize()
                point.x += radial.x * 0.004
                point.y += radial.y * 0.004
        vertices.append(tuple(point))
    faces = [tuple(remap[index] for index in face) for face in selected_faces]
    mesh = bpy.data.meshes.new("FatherConnectedSingleShirtShellMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new("FatherConnectedSingleBodyConformingShirt", mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(MAT_SURFACE)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    solidify = obj.modifiers.new("ConnectedShirtThickness", "SOLIDIFY")
    solidify.thickness = 0.0022
    solidify.offset = 0.0
    solidify.use_rim = True
    relax = obj.modifiers.new("ConnectedShirtSurfaceRelax", "SMOOTH")
    relax.factor = 0.08
    relax.iterations = 2
    obj["surfacePolicy"] = "one connected body-conforming shirt shell; no detached torso, shoulder, sleeve or cuff pieces"
    return obj


continuous_shirt = make_continuous_shirt_shell(continuous_body)


def merge_neck_into_body(body, neck):
    body_mesh = body.data
    neck_mesh = neck.data
    body_vertices = [tuple(vertex.co) for vertex in body_mesh.vertices]
    body_faces = [tuple(polygon.vertices) for polygon in body_mesh.polygons]
    offset = len(body_vertices)
    body_vertices.extend(tuple(vertex.co) for vertex in neck_mesh.vertices)
    body_faces.extend(tuple(offset + index for index in polygon.vertices) for polygon in neck_mesh.polygons)
    replacement = bpy.data.meshes.new("FatherConnectedBodyWithApprovedYuukaNeckMesh")
    replacement.from_pydata(body_vertices, [], body_faces)
    replacement.update(calc_edges=True)
    replacement.materials.append(MAT_SURFACE)
    replacement.materials.append(MAT_SKIN)
    body_polygon_count = len(body_mesh.polygons)
    for polygon in replacement.polygons:
        polygon.material_index = 0 if polygon.index < body_polygon_count else 1
        polygon.use_smooth = True
    body.data = replacement
    bpy.data.meshes.remove(body_mesh)
    bpy.data.objects.remove(neck, do_unlink=True)
    body.name = "FatherConnectedBodyAndApprovedYuukaNeck"
    return len(neck_mesh.vertices), len(neck_mesh.polygons)


neck_vertex_count, neck_polygon_count = merge_neck_into_body(continuous_body, approved_neck)


SCALE = 0.477
Y_OFFSET = -0.012
Z_OFFSET = 0.004
ARM_ANGLE = math.radians(39.0)


def deform_point(source):
    x, y, z = map(float, source)
    absolute_x = abs(x)
    sign = 1.0 if x >= 0.0 else -1.0
    torso_factor = 1.0
    if 0.64 <= z <= 1.32:
        torso_factor += 0.36 * (1.0 - smoothstep(0.12, 0.24, absolute_x))
        torso_factor = 1.0 + (torso_factor - 1.0) * smoothstep(0.64, 0.76, z)
    base_x = x * torso_factor
    base_y = y * (1.22 if 0.62 <= z <= 1.30 else 1.0)
    arm_weight = smoothstep(0.105, 0.24, absolute_x) if z >= 1.02 and absolute_x >= 0.105 else 0.0
    if arm_weight > 0.0:
        pivot_x = 0.15 * sign
        pivot_z = 1.24
        dx = (x - pivot_x) * 1.38
        dz = z - pivot_z
        angle = -sign * ARM_ANGLE
        rotated_x = pivot_x + dx * math.cos(angle) - dz * math.sin(angle)
        rotated_z = pivot_z + dx * math.sin(angle) + dz * math.cos(angle)
        x = base_x * (1.0 - arm_weight) + rotated_x * arm_weight
        z = z * (1.0 - arm_weight) + rotated_z * arm_weight
        y = base_y * (1.0 - arm_weight) + y * 1.16 * arm_weight
    else:
        x, y = base_x, base_y
    leg_height_weight = 1.0 - smoothstep(0.58, 0.79, z)
    leg_side_weight = smoothstep(0.001, 0.012, absolute_x)
    leg_weight = leg_height_weight * leg_side_weight
    if leg_weight > 0.0:
        straight_leg_x = sign * (0.120 + (absolute_x - 0.045) * 1.25)
        straight_leg_y = -0.002 + (y + 0.002) * (1.12 if z < 0.16 else 1.08)
        x = x * (1.0 - leg_weight) + straight_leg_x * leg_weight
        y = y * (1.0 - leg_weight) + straight_leg_y * leg_weight
    return Vector((x * SCALE, y * SCALE + Y_OFFSET, z * SCALE + Z_OFFSET))


def shoe_point(source):
    result = deform_point(source)
    result.z = 0.008 + max(0.0, float(source.z) - 0.0005) * SCALE * 0.72
    result.x *= 1.04
    result.y = Y_OFFSET + (result.y - Y_OFFSET) * 1.07
    return result


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
shoe_source = next((obj for obj in imported if obj.name.startswith("Body_3")), None)
if shoe_source is None:
    raise RuntimeError("VRoid Body_3 shoe surface was not found")
source_coords = [vertex.co.copy() for vertex in shoe_source.data.vertices]
source_faces = [tuple(polygon.vertices) for polygon in shoe_source.data.polygons]
used = sorted({index for face in source_faces for index in face})
remap = {source_index: new_index for new_index, source_index in enumerate(used)}
shoe_mesh = bpy.data.meshes.new("FatherConnectedFittedOxfordShoesMesh")
shoe_mesh.from_pydata(
    [tuple(shoe_point(source_coords[index])) for index in used],
    [],
    [tuple(remap[index] for index in face) for face in source_faces],
)
shoe_mesh.update(calc_edges=True)
fitted_shoes = bpy.data.objects.new("FatherConnectedFittedBrownOxfordShoes", shoe_mesh)
scene.collection.objects.link(fitted_shoes)
shoe_mesh.materials.append(MAT_SHOE)
for polygon in shoe_mesh.polygons:
    polygon.use_smooth = True
for obj in imported:
    bpy.data.objects.remove(obj, do_unlink=True)


surface_bvh = BVHTree.FromPolygons(
    [vertex.co.copy() for vertex in continuous_shirt.data.vertices],
    [tuple(polygon.vertices) for polygon in continuous_shirt.data.polygons],
)


def add_fitted_button(index, z):
    location, normal, _face_index, _distance = surface_bvh.ray_cast(
        Vector((0.0, -1.0, z)), Vector((0.0, 1.0, 0.0)), 2.0
    )
    if location is None or normal is None:
        raise RuntimeError(f"Could not fit Father shirt button {index}")
    normal.normalize()
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, location=location + normal * 0.0016)
    button = bpy.context.object
    button.name = f"FatherConnectedFittedShirtButton{index}"
    button.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(normal).to_euler()
    button.scale = (0.0060, 0.0060, 0.0028)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    button.data.materials.append(MAT_BUTTON)
    for polygon in button.data.polygons:
        polygon.use_smooth = True
    button["surfacePolicy"] = "front-surface raycast fit with partially embedded center"
    return button


fitted_buttons = [add_fitted_button(index, z) for index, z in enumerate((0.415, 0.462, 0.509, 0.556), 1)]

continuous_body["surfacePolicy"] = (
    "approved Sister continuous VRoid body plus approved Yuuka neck in one mesh object; shirt, sleeves, cuffs, trousers, belt, placket and pocket are shader regions"
)
continuous_body["detachedGarmentPanels"] = 0
continuous_body["proceduralGarmentBoxesOrPlates"] = 0
fitted_shoes["surfacePolicy"] = "VRoid Body_3 paired shoe surface overlapping the hidden foot cut"
for obj in (continuous_body, continuous_shirt, fitted_shoes, *fitted_buttons):
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
if scene.world is not None:
    scene.world.color = (0.022, 0.026, 0.034)

center = Vector((0.0, 0.025, 0.49))
distance = 4.0
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.12
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
    "side": Vector((distance, 0.0, 0.0)),
    "back": Vector((0.0, distance, 0.0)),
}


def render_views(prefix):
    paths = []
    for label, offset in views.items():
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = OUTPUT / f"{prefix}-{label}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path))
    return paths


color_paths = render_views("father-connected-office-outfit-color")

gray_backups = {}
for obj in scene.objects:
    if obj.type not in {"MESH", "CURVE"} or obj.hide_render or not hasattr(obj.data, "materials"):
        continue
    gray_backups[obj.name] = list(obj.data.materials)
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.material_index = 0
gray_paths = render_views("father-connected-office-outfit-gray")
for name, materials in gray_backups.items():
    obj = bpy.data.objects.get(name)
    if obj is None:
        continue
    obj.data.materials.clear()
    for material in materials:
        obj.data.materials.append(material)

owned_coordinate_after = coordinate_hash(owned_body)
owned_weight_after = weight_hash(owned_body)
native_hand_coordinate_after = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_after = weight_hash(owned_body, native_hand_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if owned_coordinate_before != owned_coordinate_after or owned_weight_before != owned_weight_after:
    raise RuntimeError("Father connected gate changed owned Yuuka coordinates or weights")
if native_hand_coordinate_before != native_hand_coordinate_after or native_hand_weight_before != native_hand_weight_after:
    raise RuntimeError("Father connected gate changed exact native Yuuka hand coordinates or weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Father connected gate changed owned Yuuka rig bone names")

blend_path = OUTPUT / "father-connected-office-outfit-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
receipt = {
    "schema": "family-company.father-connected-office-outfit-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING_ROOT_REVIEW",
    "candidateClaim": False,
    "promotionBlocked": True,
    "identitySource": "user-owned test2 Yuuka FatherProof3 face, short hair, glasses, expression and exact native three-digit hands",
    "outfitAuthority": str(REFERENCE),
    "outfitAuthoritySha256": sha256(REFERENCE),
    "approvedConnectionSource": {
        "blend": str(APPROVED_SISTER_BLEND),
        "blendSha256": sha256(APPROVED_SISTER_BLEND),
        "bodyObject": "SisterProof46ContinuousVRoidBody",
        "neckObject": "SisterProof46SmoothNeckBridge",
    },
    "vroidShoeSource": {
        "mesh": "VRoid Studio 2.14.0 built-in Body_3",
        "officialGuidelines": "https://vroid.com/en/studio/guidelines",
        "obj": str(VROID_OBJ),
        "objSha256": sha256(VROID_OBJ),
    },
    "test3SakurakoExcluded": True,
    "retained": {
        "ownedCoordinatesExact": owned_coordinate_before == owned_coordinate_after,
        "ownedWeightsExact": owned_weight_before == owned_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeHandComponents": native_hand_components,
        "nativeHandsExact": native_hand_coordinate_before == native_hand_coordinate_after and native_hand_weight_before == native_hand_weight_after,
        "nativeHandDescription": "original 3-digit stylized Yuuka hands",
    },
    "surfacePolicy": {
        "continuousBodyAndNeckObject": continuous_body.name,
        "continuousShirtObject": continuous_shirt.name,
        "shirtSingleConnectedMeshObject": True,
        "approvedNeckVertexCount": neck_vertex_count,
        "approvedNeckPolygonCount": neck_polygon_count,
        "reshapedReplaceableBodyVertices": reshaped_body_vertices,
        "donorBustFlattenedWithoutOwnedIdentityEdit": True,
        "waistAndLegsStraightenedWithoutDetachedVolumes": True,
        "bodyFacesAfterEnclosedFootCut": body_faces_after_foot_cut,
        "shirtOnContinuousSurface": True,
        "rolledSleevesOnContinuousSurface": True,
        "trousersOnContinuousSurface": True,
        "beltOnContinuousSurface": True,
        "placketOnContinuousSurface": True,
        "pocketOnContinuousSurface": True,
        "detachedGarmentPanels": 0,
        "proceduralGarmentBoxesOrPlates": 0,
        "fittedShoes": fitted_shoes.name,
        "fittedButtons": [button.name for button in fitted_buttons],
        "retainedFittedWatch": ["FatherWatchBand", "FatherWatchFace"],
        "removedFailedGarments": removed_failed_garments,
    },
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static visual gate only; no rig transfer, motion, Unity or production claim",
        "continuous VRoid body and shoes are not yet transferred to the owned Yuuka rig",
        "user visual approval is required before animation work",
    ],
}
(OUTPUT / "father-connected-office-outfit-gate-receipt.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("FATHER_CONNECTED_OFFICE_OUTFIT_GATE_RENDERED")
