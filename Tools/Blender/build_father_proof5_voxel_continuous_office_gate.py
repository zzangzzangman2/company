"""Build a static Father approval candidate with one organic connected body.

The user-owned FatherProof3 Yuuka face, short hair, glasses, expression, rig and
exact native three-digit hands are retained.  Its failed torso, shoulder,
sleeve, forearm, pelvis and trouser volumes are used only as an overlapping
silhouette scaffold, then voxel-unioned and relaxed into one new continuous
surface.  The old pieces are gone from the result.  The approved Yuuka sister
neck is merged into that surface object and overlaps the lowered shirt neckline.

Static visual gate only.  No rig transfer, motion, Unity import or production
promotion is performed.
"""

from __future__ import annotations

import argparse
from collections import defaultdict, deque
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--approved-sister-blend", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
APPROVED_SISTER_BLEND = Path(ARGS.approved_sister_blend).resolve()
REFERENCE = Path(ARGS.reference).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
for required in (APPROVED_SISTER_BLEND, REFERENCE):
    if not required.is_file():
        raise RuntimeError(f"Missing Father Proof5 input: {required}")

scene = bpy.context.scene
owned_body = bpy.data.objects.get("Yuuka_Original_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("FatherProofCamera") or scene.camera
if owned_body is None or armature is None or camera is None:
    raise RuntimeError("Expected the user-owned FatherProof3 Yuuka identity scene")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected exact Yuuka 118-bone rig, got {len(armature.data.bones)}")


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
        point = obj.data.vertices[index].co
        digest.update(f"{index}:{point.x:.9f},{point.y:.9f},{point.z:.9f};".encode())
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
    components = []
    visited = set()
    for seed in range(len(mesh.polygons)):
        if seed in visited:
            continue
        queue = deque([seed])
        visited.add(seed)
        vertices = set()
        polygons = []
        while queue:
            polygon_index = queue.popleft()
            polygon = mesh.polygons[polygon_index]
            vertices.update(polygon.vertices)
            polygons.append(polygon_index)
            for neighbor in neighbors[polygon_index]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(neighbor)
        components.append((vertices, polygons))
    return components


native_hand_vertices = set()
native_hand_components = []
for component_index, (vertices, polygons) in enumerate(connected_components(owned_body.data)):
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
    raise RuntimeError(f"Expected two exact native Yuuka hands, got {native_hand_components}")

owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)
native_hand_coordinate_before = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_before = weight_hash(owned_body, native_hand_vertices)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


def solid_material(name, color, roughness=0.84, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.06
    return material


MAT_SHIRT = solid_material("FatherProof5MutedBlueShirt", (0.105, 0.315, 0.370), 0.88)
MAT_SHIRT_DETAIL = solid_material("FatherProof5BlueTailoring", (0.055, 0.205, 0.245), 0.88)
MAT_TROUSERS = solid_material("FatherProof5CharcoalTrousers", (0.040, 0.047, 0.055), 0.90)
MAT_SKIN = solid_material("FatherProof5ConnectedSkin", (0.91, 0.75, 0.70), 0.84)
MAT_BELT = solid_material("FatherProof5BrownBeltSurface", (0.095, 0.037, 0.017), 0.76)
MAT_SHOE = solid_material("FatherProof5BrownOxford", (0.105, 0.038, 0.018), 0.74)
MAT_BUTTON = solid_material("FatherProof5FittedButton", (0.72, 0.78, 0.78), 0.48, 0.08)
MAT_BUCKLE = solid_material("FatherProof5FittedBuckle", (0.55, 0.61, 0.64), 0.36, 0.58)
MAT_GRAY = solid_material("FatherProof5QAGray", (0.43, 0.45, 0.49), 0.84)


body_scaffold_names = [
    "FatherBareForearmL",
    "FatherBareForearmR",
    "FatherFittedShirtTorso",
    "FatherRolledSleeveL",
    "FatherRolledSleeveR",
    "FatherRolledCuffL",
    "FatherRolledCuffR",
    "FatherStraightTrouserLegL",
    "FatherStraightTrouserLegR",
    "FatherTrouserWaist",
]
body_scaffolds = []
for name in body_scaffold_names:
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Missing FatherProof3 silhouette scaffold {name}")
    body_scaffolds.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.select_all(action="DESELECT")
for obj in body_scaffolds:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body_scaffolds[0]
bpy.ops.object.join()
continuous_body = bpy.context.active_object
continuous_body.name = "FatherProof5VoxelUnifiedContinuousBody"
continuous_body.data.name = continuous_body.name + "Mesh"
continuous_body.data.remesh_voxel_size = 0.0045
continuous_body.data.remesh_voxel_adaptivity = 0.0
bpy.ops.object.voxel_remesh()
for polygon in continuous_body.data.polygons:
    polygon.use_smooth = True
relax = continuous_body.modifiers.new("OrganicUnionRelax", "SMOOTH")
relax.factor = 0.42
relax.iterations = 10
bpy.ops.object.modifier_apply(modifier=relax.name)


def reshape_connected_body(obj):
    changed = 0
    for vertex in obj.data.vertices:
        point = vertex.co
        absolute_x = abs(float(point.x))
        sign = 1.0 if point.x >= 0.0 else -1.0
        # Narrow the old rectangular torso into a soft shirt column.
        if 0.395 <= point.z <= 0.650 and absolute_x < 0.205:
            t = max(0.0, min(1.0, (absolute_x - 0.140) / 0.065))
            side_fade = t * t * (3.0 - 2.0 * t)
            center_weight = 1.0 - side_fade
            point.x *= 1.0 - 0.14 * center_weight
            point.y *= 1.0 - 0.32 * center_weight
            changed += 1
        # Remove the spherical shoulder-pad read while leaving one continuous
        # shoulder-to-sleeve bridge.  X stays untouched so the original sloped
        # A-pose arm and exact native-hand overlap do not kink at the elbow.
        if point.z > 0.535 and absolute_x >= 0.145:
            point.z = 0.535 + (point.z - 0.535) * 0.72
            point.y *= 0.72
            changed += 1
        # A shallow U neckline exposes the approved Yuuka neck instead of
        # allowing the shirt top to erase it.
        if point.z > 0.625 and absolute_x < 0.080:
            ratio = absolute_x / 0.080
            neckline = 0.625 + 0.017 * ratio * ratio
            point.z = min(point.z, neckline)
            changed += 1
        # Keep both trouser legs thick, straight and parallel.
        if 0.065 <= point.z < 0.355 and 0.004 < absolute_x < 0.150:
            leg_center = 0.070 * sign
            point.x = leg_center + (point.x - leg_center) * 1.04
    obj.data.update(calc_edges=True)
    return changed


reshaped_body_vertices = reshape_connected_body(continuous_body)

continuous_body.data.materials.clear()
for material in (MAT_SHIRT, MAT_TROUSERS, MAT_SKIN, MAT_BELT, MAT_SHIRT_DETAIL):
    continuous_body.data.materials.append(material)
for polygon in continuous_body.data.polygons:
    center = polygon.center
    absolute_x = abs(float(center.x))
    if 0.384 <= center.z <= 0.406 and absolute_x < 0.165:
        material_index = 3
    elif center.z < 0.395:
        material_index = 1
    elif absolute_x > 0.175 and center.z < 0.492:
        material_index = 2
    elif center.y < -0.052 and absolute_x < 0.010 and 0.420 <= center.z <= 0.595:
        material_index = 4
    else:
        material_index = 0
    polygon.material_index = material_index
    polygon.use_smooth = True


with bpy.data.libraries.load(str(APPROVED_SISTER_BLEND), link=False) as (data_from, data_to):
    if "SisterProof46SmoothNeckBridge" not in data_from.objects:
        raise RuntimeError("Approved Sister blend is missing SisterProof46SmoothNeckBridge")
    data_to.objects = ["SisterProof46SmoothNeckBridge"]
approved_neck = data_to.objects[0]
scene.collection.objects.link(approved_neck)
if len(approved_neck.data.vertices) != 160 or len(approved_neck.data.polygons) != 120:
    raise RuntimeError("Approved Yuuka neck topology changed")
neck_vertices = [tuple(vertex.co) for vertex in approved_neck.data.vertices]
neck_faces = [tuple(polygon.vertices) for polygon in approved_neck.data.polygons]
neck_coordinate_sha256 = hashlib.sha256(
    "".join(f"{i}:{p[0]:.9f},{p[1]:.9f},{p[2]:.9f};" for i, p in enumerate(neck_vertices)).encode()
).hexdigest()


def merge_approved_neck(body, neck):
    old_mesh = body.data
    vertices = [tuple(vertex.co) for vertex in old_mesh.vertices]
    faces = [tuple(polygon.vertices) for polygon in old_mesh.polygons]
    material_indices = [polygon.material_index for polygon in old_mesh.polygons]
    offset = len(vertices)
    vertices.extend(tuple(vertex.co) for vertex in neck.data.vertices)
    faces.extend(tuple(offset + index for index in polygon.vertices) for polygon in neck.data.polygons)
    material_indices.extend([2] * len(neck.data.polygons))
    replacement = bpy.data.meshes.new("FatherProof5BodyWithApprovedYuukaNeckMesh")
    replacement.from_pydata(vertices, [], faces)
    replacement.update(calc_edges=True)
    for material in (MAT_SHIRT, MAT_TROUSERS, MAT_SKIN, MAT_BELT, MAT_SHIRT_DETAIL):
        replacement.materials.append(material)
    for polygon, material_index in zip(replacement.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = True
    body.data = replacement
    bpy.data.meshes.remove(old_mesh)
    bpy.data.objects.remove(neck, do_unlink=True)
    body.name = "FatherProof5OneBodyWithApprovedYuukaNeck"


merge_approved_neck(continuous_body, approved_neck)

failed_objects = {
    "FatherBeltBuckle",
    "FatherBrownBelt",
    "FatherChestPocket",
    "FatherCollarL",
    "FatherCollarR",
    "FatherNeck",
    "FatherShirtPlacket",
    "FatherTailoredShoulderL",
    "FatherTailoredShoulderR",
}
failed_objects.update(obj.name for obj in bpy.data.objects if obj.name.startswith("FatherShirtButton"))
removed_failed_objects = []
for name in sorted(failed_objects):
    obj = bpy.data.objects.get(name)
    if obj is not None:
        removed_failed_objects.append(name)
        bpy.data.objects.remove(obj, do_unlink=True)


shoe_names = ["FatherBrownShoeL", "FatherBrownShoeR", "FatherShoeSoleL", "FatherShoeSoleR"]
shoes = []
for name in shoe_names:
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Missing FatherProof3 shoe scaffold {name}")
    shoes.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.select_all(action="DESELECT")
for obj in shoes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = shoes[0]
bpy.ops.object.join()
fitted_shoes = bpy.context.active_object
fitted_shoes.name = "FatherProof5UnifiedBrownOxfordPair"
fitted_shoes.data.name = fitted_shoes.name + "Mesh"
fitted_shoes.data.remesh_voxel_size = 0.0026
fitted_shoes.data.remesh_voxel_adaptivity = 0.0
bpy.ops.object.voxel_remesh()
shoe_relax = fitted_shoes.modifiers.new("UnifiedShoeRelax", "SMOOTH")
shoe_relax.factor = 0.25
shoe_relax.iterations = 5
bpy.ops.object.modifier_apply(modifier=shoe_relax.name)
fitted_shoes.data.materials.clear()
fitted_shoes.data.materials.append(MAT_SHOE)
for polygon in fitted_shoes.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = True


surface_bvh = BVHTree.FromPolygons(
    [vertex.co.copy() for vertex in continuous_body.data.vertices],
    [tuple(polygon.vertices) for polygon in continuous_body.data.polygons],
)


def fitted_surface_point(x, z):
    location, normal, _face_index, _distance = surface_bvh.ray_cast(
        Vector((x, -1.0, z)), Vector((0.0, 1.0, 0.0)), 2.0
    )
    if location is None or normal is None:
        raise RuntimeError(f"Could not ray-fit Father accessory at x={x}, z={z}")
    normal.normalize()
    return location, normal


fitted_buttons = []
for index, z in enumerate((0.435, 0.482, 0.529, 0.576), 1):
    location, normal = fitted_surface_point(0.0, z)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, location=location + normal * 0.0016)
    button = bpy.context.object
    button.name = f"FatherProof5FittedShirtButton{index}"
    button.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(normal).to_euler()
    button.scale = (0.0060, 0.0060, 0.0028)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    button.data.materials.append(MAT_BUTTON)
    for polygon in button.data.polygons:
        polygon.use_smooth = True
    fitted_buttons.append(button)

location, normal = fitted_surface_point(0.0, 0.395)
bpy.ops.mesh.primitive_cube_add(location=location + normal * 0.0015)
buckle = bpy.context.object
buckle.name = "FatherProof5FittedBeltBuckle"
buckle.scale = (0.019, 0.0032, 0.010)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bevel = buckle.modifiers.new("FittedBuckleSoftEdge", "BEVEL")
bevel.width = 0.0022
bevel.segments = 3
buckle.data.materials.append(MAT_BUCKLE)

continuous_body["surfacePolicy"] = (
    "all former torso, sleeve, cuff, forearm, pelvis and trouser scaffolds voxel-unioned into one relaxed continuous surface; approved Yuuka neck merged into the same object"
)
continuous_body["detachedGarmentPanels"] = 0
continuous_body["visibleAssemblyPieces"] = 0
continuous_body["candidateClaim"] = False
continuous_body["test3SakurakoExcluded"] = True
fitted_shoes["surfacePolicy"] = "both shoe and sole scaffolds unified and relaxed; overlaps straight trouser hems"
for obj in (fitted_shoes, *fitted_buttons, buckle):
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1050
scene.render.resolution_y = 1050
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.12
center = Vector((0.0, 0.025, 0.49))
distance = 4.0
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


color_paths = render_views("father-proof5-voxel-continuous-office-color")

gray_backups = {}
for obj in scene.objects:
    if obj.hide_render or obj.type not in {"MESH", "CURVE"} or not hasattr(obj.data, "materials"):
        continue
    gray_backups[obj.name] = {
        "materials": list(obj.data.materials),
        "indices": [polygon.material_index for polygon in obj.data.polygons] if obj.type == "MESH" else None,
    }
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.material_index = 0
gray_paths = render_views("father-proof5-voxel-continuous-office-gray")
for name, backup in gray_backups.items():
    obj = bpy.data.objects.get(name)
    if obj is None:
        continue
    obj.data.materials.clear()
    for material in backup["materials"]:
        obj.data.materials.append(material)
    if obj.type == "MESH":
        for polygon, material_index in zip(obj.data.polygons, backup["indices"]):
            polygon.material_index = material_index

owned_coordinate_after = coordinate_hash(owned_body)
owned_weight_after = weight_hash(owned_body)
native_hand_coordinate_after = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_after = weight_hash(owned_body, native_hand_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if owned_coordinate_before != owned_coordinate_after or owned_weight_before != owned_weight_after:
    raise RuntimeError("Father Proof5 changed owned Yuuka coordinates or weights")
if native_hand_coordinate_before != native_hand_coordinate_after or native_hand_weight_before != native_hand_weight_after:
    raise RuntimeError("Father Proof5 changed exact native Yuuka hand coordinates or weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Father Proof5 changed owned Yuuka rig bone names")

blend_path = OUTPUT / "father-proof5-voxel-continuous-office-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
receipt = {
    "schema": "family-company.father-proof5-voxel-continuous-office-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING_ROOT_REVIEW",
    "candidateClaim": False,
    "promotionBlocked": True,
    "identitySource": "user-owned test2 Yuuka FatherProof3 face, short hair, glasses, expression and exact native three-digit hands",
    "outfitAuthority": str(REFERENCE),
    "outfitAuthoritySha256": sha256(REFERENCE),
    "approvedNeckSource": {
        "blend": str(APPROVED_SISTER_BLEND),
        "blendSha256": sha256(APPROVED_SISTER_BLEND),
        "object": "SisterProof46SmoothNeckBridge",
        "vertexCount": len(neck_vertices),
        "polygonCount": len(neck_faces),
        "coordinateSha256": neck_coordinate_sha256,
    },
    "test3SakurakoExcluded": True,
    "retained": {
        "ownedCoordinatesExact": owned_coordinate_before == owned_coordinate_after,
        "ownedWeightsExact": owned_weight_before == owned_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeHandComponents": native_hand_components,
        "nativeHandsExact": native_hand_coordinate_before == native_hand_coordinate_after and native_hand_weight_before == native_hand_weight_after,
        "nativeHandDescription": "original user-owned Yuuka 3-digit stylized hands",
    },
    "surfacePolicy": {
        "continuousBodyAndNeckObject": continuous_body.name,
        "sourceScaffolds": body_scaffold_names,
        "sourceScaffoldsRetainedAsObjects": False,
        "voxelSize": 0.0045,
        "organicRelaxIterations": 10,
        "reshapedBodyVertices": reshaped_body_vertices,
        "shirtAndSleevesSameSurface": True,
        "forearmsSameSurface": True,
        "trousersSameSurface": True,
        "approvedNeckMergedIntoSameObject": True,
        "detachedGarmentPanels": 0,
        "visibleAssemblyPieces": 0,
        "unifiedShoes": fitted_shoes.name,
        "fittedButtons": [button.name for button in fitted_buttons],
        "fittedBuckle": buckle.name,
        "retainedFittedWatch": ["FatherWatchBand", "FatherWatchFace"],
        "removedFailedObjects": removed_failed_objects,
    },
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static visual gate only; no rig transfer, motion, Unity or production claim",
        "the new unified body is not yet transferred to the retained Yuuka rig",
        "user visual approval is required before animation work",
    ],
}
(OUTPUT / "father-proof5-voxel-continuous-office-gate-receipt.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("FATHER_PROOF5_VOXEL_CONTINUOUS_OFFICE_GATE_RENDERED")
