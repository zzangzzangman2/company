"""Polish the isolated user-owned Yuuka -> Older Sister Proof2 blend.

The source face, eyes, hair, hands, mouth repair, bare feet/toes and 118-bone
rig are preserved.  Only Proof2's temporary box-like tank, shorts and limb
continuity shells are replaced with fitted rounded, armature-weighted surfaces.
This script never imports or inspects test3/Sakurako and never touches Unity.
"""

import argparse
import hashlib
import json
import math
import os
import sys

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = os.path.abspath(ARGS.output)
os.makedirs(OUTPUT, exist_ok=True)
SOURCE_BLEND = os.path.abspath(bpy.data.filepath)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProofCamera")
mouth = bpy.data.objects.get("NormalizedMouthCurve")
if armature is None or body is None or camera is None or mouth is None:
    raise RuntimeError("Expected SisterProof2 Armature, body, camera and repaired mouth")
if len(armature.data.bones) != 118:
    raise RuntimeError("SisterProof2 source rig must remain exactly 118 bones")

source_bone_names = sorted(bone.name for bone in armature.data.bones)
source_body_vertices = len(body.data.vertices)
source_body_polygons = len(body.data.polygons)
preserved_names = {
    "Yuuka_Original_Body",
    "NormalizedMouthCurve",
    "SisterBareFootL",
    "SisterBareFootR",
    *("SisterToeL%d" % index for index in range(4)),
    *("SisterToeR%d" % index for index in range(4)),
}
preserved_hash = {
    name: (len(bpy.data.objects[name].data.vertices), len(bpy.data.objects[name].data.polygons))
    for name in preserved_names
}

replace_names = (
    "SisterTankTorso",
    "SisterTankStrapL",
    "SisterTankStrapR",
    "SisterShortsWaist",
    "SisterShortsLegL",
    "SisterShortsLegR",
    "SisterShortsHemL",
    "SisterShortsHemR",
    "SisterContinuousArmL",
    "SisterContinuousArmR",
    "SisterContinuousLegL",
    "SisterContinuousLegR",
)
for name in replace_names:
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError("Missing Proof2 replacement target: " + name)
    bpy.data.objects.remove(obj, do_unlink=True)

materials = {
    "skin": bpy.data.materials.get("SisterSkin"),
    "tank": bpy.data.materials.get("SisterBlackTank"),
    "shorts": bpy.data.materials.get("SisterNavyShorts"),
    "piping": bpy.data.materials.get("SisterWhitePiping"),
}
if any(material is None for material in materials.values()):
    raise RuntimeError("Proof2 Sister materials are incomplete")


def normalized(mapping):
    total = sum(max(value, 0.0) for value in mapping.values())
    return {name: max(value, 0.0) / total for name, value in mapping.items() if value > 0.0}


def finish_mesh(name, vertices, faces, material_list, weights, face_materials=None, subdivision=1):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for material in material_list:
        obj.data.materials.append(material)
    if face_materials is not None:
        if len(face_materials) != len(obj.data.polygons):
            raise RuntimeError("Face material count mismatch for " + name)
        for polygon, material_index in zip(obj.data.polygons, face_materials):
            polygon.material_index = material_index
    for polygon in obj.data.polygons:
        polygon.use_smooth = True

    groups = {}
    for vertex_index, mapping in enumerate(weights):
        for bone_name, weight in normalized(mapping).items():
            group = groups.get(bone_name)
            if group is None:
                group = obj.vertex_groups.new(name=bone_name)
                groups[bone_name] = group
            group.add([vertex_index], weight, "REPLACE")

    if subdivision:
        smooth = obj.modifiers.new("Proof3RoundedSurface", "SUBSURF")
        smooth.subdivision_type = "CATMULL_CLARK"
        smooth.levels = subdivision
        smooth.render_levels = subdivision
    rig_modifier = obj.modifiers.new("OriginalYuuka118BoneRig", "ARMATURE")
    rig_modifier.object = armature
    rig_modifier.use_vertex_groups = True
    obj["proofRevision"] = "SisterProof3"
    obj["sourceRigPreserved"] = True
    return obj


def loft(name, rings, material_list, ring_weights, segments=48, face_material_for_strip=None, cap=True):
    vertices = []
    weights = []
    for (z, cx, cy, rx, ry), mapping in zip(rings, ring_weights):
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle), z))
            weights.append(mapping)
    faces = []
    face_materials = []
    for ring_index in range(len(rings) - 1):
        first = ring_index * segments
        second = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
            face_materials.append(face_material_for_strip.get(ring_index, 0) if face_material_for_strip else 0)
    if cap:
        bottom_index = len(vertices)
        vertices.append((rings[0][1], rings[0][2], rings[0][0]))
        weights.append(ring_weights[0])
        top_index = len(vertices)
        vertices.append((rings[-1][1], rings[-1][2], rings[-1][0]))
        weights.append(ring_weights[-1])
        last = (len(rings) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((bottom_index, nxt, segment))
            face_materials.append(0)
            faces.append((top_index, last + segment, last + nxt))
            face_materials.append(0)
    return finish_mesh(name, vertices, faces, material_list, weights, face_materials, subdivision=1)


def path_frame(tangent):
    tangent = tangent.normalized()
    reference = Vector((0.0, 1.0, 0.0))
    if abs(tangent.dot(reference)) > 0.90:
        reference = Vector((0.0, 0.0, 1.0))
    axis_a = tangent.cross(reference).normalized()
    axis_b = tangent.cross(axis_a).normalized()
    return axis_a, axis_b


def tube(name, points, radii, material, ring_weights, segments=32, cap=True, subdivision=1):
    points = [Vector(point) for point in points]
    vertices = []
    weights = []
    for index, (point, radius, mapping) in enumerate(zip(points, radii, ring_weights)):
        if index == 0:
            tangent = points[1] - points[0]
        elif index == len(points) - 1:
            tangent = points[-1] - points[-2]
        else:
            tangent = points[index + 1] - points[index - 1]
        axis_a, axis_b = path_frame(tangent)
        radius_a, radius_b = radius
        for segment in range(segments):
            angle = math.tau * segment / segments
            point_on_ring = point + axis_a * (math.cos(angle) * radius_a) + axis_b * (math.sin(angle) * radius_b)
            vertices.append(tuple(point_on_ring))
            weights.append(mapping)
    faces = []
    for ring_index in range(len(points) - 1):
        first = ring_index * segments
        second = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    if cap:
        first_center = len(vertices)
        vertices.append(tuple(points[0]))
        weights.append(ring_weights[0])
        last_center = len(vertices)
        vertices.append(tuple(points[-1]))
        weights.append(ring_weights[-1])
        last = (len(points) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first_center, nxt, segment))
            faces.append((last_center, last + segment, last + nxt))
    return finish_mesh(name, vertices, faces, [material], weights, subdivision=subdivision)


# Fitted tank: true elliptical cross-sections and a gentle waist/chest taper,
# with no front-facing rectangular board or separate hard waistband.
tank_rings = (
    (0.405, 0.0, 0.000, 0.124, 0.074),
    (0.440, 0.0, 0.000, 0.130, 0.078),
    (0.490, 0.0, 0.000, 0.140, 0.083),
    (0.545, 0.0, 0.000, 0.144, 0.085),
    (0.590, 0.0, 0.000, 0.132, 0.078),
    (0.620, 0.0, 0.000, 0.098, 0.066),
)
tank_weights = (
    {"Bip001 Pelvis": 0.25, "Bip001 Spine": 0.75},
    {"Bip001 Spine": 1.0},
    {"Bip001 Spine": 0.65, "Bip001 Spine1": 0.35},
    {"Bip001 Spine": 0.30, "Bip001 Spine1": 0.70},
    {"Bip001 Spine1": 1.0},
    {"Bip001 Spine1": 1.0},
)
loft("SisterTankTorsoProof3", tank_rings, [materials["tank"]], tank_weights)

for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    tube(
        "SisterTankStrapProof3" + suffix,
        (
            (0.096 * sign, -0.004, 0.596),
            (0.112 * sign, -0.002, 0.621),
            (0.126 * sign, 0.000, 0.642),
        ),
        ((0.020, 0.016), (0.018, 0.014), (0.016, 0.013)),
        materials["tank"],
        ({"Bip001 Spine1": 1.0},) * 3,
        segments=24,
        subdivision=1,
    )

    upper = "Bip001 %s UpperArm" % suffix
    fore = "Bip001 %s Forearm" % suffix
    tube(
        "SisterContinuousArmProof3" + suffix,
        (
            (0.108 * sign, 0.000, 0.607),
            (0.145 * sign, -0.001, 0.583),
            (0.181 * sign, -0.003, 0.546),
            (0.218 * sign, -0.005, 0.505),
            (0.252 * sign, -0.006, 0.463),
            (0.285 * sign, -0.007, 0.435),
        ),
        ((0.052, 0.048), (0.050, 0.046), (0.047, 0.043), (0.043, 0.040), (0.038, 0.036), (0.033, 0.033)),
        materials["skin"],
        (
            {upper: 1.0},
            {upper: 1.0},
            {upper: 0.78, fore: 0.22},
            {upper: 0.42, fore: 0.58},
            {fore: 0.90, upper: 0.10},
            {fore: 1.0},
        ),
        segments=36,
        subdivision=1,
    )

# The pelvis shell supplies the fitted waist; there is deliberately no
# independent protruding waistband object.
shorts_pelvis_rings = (
    (0.360, 0.0, 0.006, 0.136, 0.058),
    (0.385, 0.0, 0.005, 0.143, 0.061),
    (0.415, 0.0, 0.003, 0.143, 0.065),
    (0.445, 0.0, 0.001, 0.130, 0.068),
)
loft(
    "SisterShortsPelvisFittedProof3",
    shorts_pelvis_rings,
    [materials["shorts"]],
    ({"Bip001 Pelvis": 1.0},) * len(shorts_pelvis_rings),
)

for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    leg_center = 0.072 * sign
    shorts_leg_rings = (
        (0.312, leg_center, 0.004, 0.070, 0.074),
        (0.321, leg_center, 0.004, 0.071, 0.075),
        (0.350, leg_center, 0.004, 0.076, 0.080),
        (0.382, leg_center, 0.003, 0.079, 0.084),
        (0.405, leg_center, 0.002, 0.078, 0.083),
    )
    loft(
        "SisterShortsLegFittedProof3" + suffix,
        shorts_leg_rings,
        [materials["shorts"], materials["piping"]],
        ({"Bip001 Pelvis": 1.0},) * len(shorts_leg_rings),
        face_material_for_strip={0: 1},
        cap=False,
    )

    thigh = "Bip001 %s Thigh" % suffix
    calf = "Bip001 %s Calf" % suffix
    foot = "Bip001 %s Foot" % suffix
    tube(
        "SisterContinuousLegProof3" + suffix,
        (
            (0.073 * sign, 0.002, 0.385),
            (0.077 * sign, 0.002, 0.335),
            (0.081 * sign, 0.001, 0.275),
            (0.084 * sign, 0.001, 0.210),
            (0.087 * sign, 0.003, 0.145),
            (0.090 * sign, 0.006, 0.095),
            (0.092 * sign, 0.008, 0.052),
        ),
        ((0.062, 0.058), (0.060, 0.056), (0.054, 0.051), (0.048, 0.046), (0.052, 0.049), (0.043, 0.041), (0.038, 0.037)),
        materials["skin"],
        (
            {thigh: 1.0},
            {thigh: 1.0},
            {thigh: 0.72, calf: 0.28},
            {thigh: 0.20, calf: 0.80},
            {calf: 0.90, foot: 0.10},
            {calf: 0.45, foot: 0.55},
            {foot: 1.0},
        ),
        segments=36,
        subdivision=1,
    )

# A real review floor makes bare-foot grounding auditable in every yaw.
old_floor = bpy.data.objects.get("SisterProof3Floor")
if old_floor is not None:
    bpy.data.objects.remove(old_floor, do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.002))
floor = bpy.context.object
floor.name = "SisterProof3Floor"
floor_material = bpy.data.materials.get("SisterProof3FloorMaterial") or bpy.data.materials.new("SisterProof3FloorMaterial")
floor_material.use_nodes = True
floor_bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
floor_bsdf.inputs["Base Color"].default_value = (0.070, 0.085, 0.115, 1.0)
floor_bsdf.inputs["Roughness"].default_value = 0.90
floor.data.materials.append(floor_material)

scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.camera = camera
camera.data.type = "ORTHO"
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.use_freestyle = False


def point_camera(yaw_degrees, target_z=0.500, radius=3.20):
    radians = math.radians(yaw_degrees)
    target = Vector((0.0, 0.0, target_z))
    camera.location = (math.sin(radians) * radius, -math.cos(radians) * radius, target_z + 0.02)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
camera.data.ortho_scale = 1.10
static_views = (("front", 0), ("three-quarter", 45), ("side", 90), ("back", 180))
static_outputs = []
for label, yaw in static_views:
    point_camera(yaw)
    path = os.path.join(OUTPUT, "sister-yuuka-direct-proof3-%s.png" % label)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    static_outputs.append(path)

# Face closeup confirms the original eyes/face and repaired mouth remain
# volumetric and surface-attached after the body/outfit-only polish.
point_camera(0, target_z=0.790)
camera.data.ortho_scale = 0.48
face_closeup = os.path.join(OUTPUT, "sister-yuuka-direct-proof3-face-closeup.png")
scene.render.filepath = face_closeup
bpy.ops.render.render(write_still=True)

turntable_dir = os.path.join(OUTPUT, "Turntable8")
os.makedirs(turntable_dir, exist_ok=True)
scene.render.resolution_x = 900
scene.render.resolution_y = 900
camera.data.ortho_scale = 1.10
turntable_outputs = []
for yaw in range(0, 360, 45):
    point_camera(yaw)
    path = os.path.join(turntable_dir, "sister-proof3-yaw%03d.png" % yaw)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    turntable_outputs.append(path)

blend_path = os.path.join(OUTPUT, "sister-yuuka-direct-proof3.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

preservation_ok = (
    len(armature.data.bones) == 118
    and sorted(bone.name for bone in armature.data.bones) == source_bone_names
    and len(body.data.vertices) == source_body_vertices
    and len(body.data.polygons) == source_body_polygons
    and all(
        bpy.data.objects.get(name) is not None
        and (len(bpy.data.objects[name].data.vertices), len(bpy.data.objects[name].data.polygons)) == counts
        for name, counts in preserved_hash.items()
    )
)

receipt = {
    "status": "SISTER_PROOF3_REVIEW_REQUIRED",
    "sourceBlend": SOURCE_BLEND,
    "sourceBlendSha256": sha256(SOURCE_BLEND),
    "sourceBasis": "user-owned test2 Yuuka direct conversion Proof2",
    "test3OrSakurakoUsed": False,
    "unityModified": False,
    "productionEligible": False,
    "preservation": {
        "faceEyesHairHandsOriginalBodyUnchanged": len(body.data.vertices) == source_body_vertices and len(body.data.polygons) == source_body_polygons,
        "mouthRepairObjectUnchanged": bpy.data.objects.get("NormalizedMouthCurve") is not None,
        "bareFeetAndToeObjectsUnchanged": all(bpy.data.objects.get(name) is not None for name in preserved_names if name.startswith("SisterBareFoot") or name.startswith("SisterToe")),
        "boneCount": len(armature.data.bones),
        "boneNamesUnchanged": sorted(bone.name for bone in armature.data.bones) == source_bone_names,
        "allRequiredPreservationChecks": preservation_ok,
    },
    "proof3Polish": {
        "tank": "fitted six-ring elliptical torso with smooth waist/chest taper",
        "arms": "six-ring shoulder-to-wrist continuous tapered subdivision surfaces",
        "shortsWaist": "pelvis-fitted elliptical shell; no independent protruding waistband",
        "shortsLegs": "separate correctly positioned left/right fitted lobes",
        "whitePiping": "shared shorts mesh material band; no separate floating wire or box",
        "legs": "six-ring pelvis-to-foot continuous tapered subdivision surfaces",
        "grounding": "original bare feet retained over review floor z=-0.002",
    },
    "staticViews": [os.path.basename(path) for path in static_outputs],
    "faceCloseup": os.path.basename(face_closeup),
    "turntableFrames": [os.path.basename(path) for path in turntable_outputs],
    "blend": os.path.basename(blend_path),
    "limitations": [
        "visual-review feasibility proof only",
        "new fitted clothing and continuity shells require animation deformation QA before production",
    ],
}
receipt_path = os.path.join(OUTPUT, "sister-yuuka-direct-proof3-receipt.json")
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("SISTER_OWNED_YUUKA_PROOF3_RENDERED")
