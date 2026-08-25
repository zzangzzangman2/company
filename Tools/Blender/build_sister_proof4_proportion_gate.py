"""Build an Older Sister proportion/surface gate from the rejected Proof3 blend.

The user-owned Yuuka face, eyes, hair, original three-digit stylized hands and
118-bone rig remain untouched.  Rejected procedural torso/limb/foot pieces are
replaced with slimmer fitted surfaces.  This is a static internal QA gate only:
it does not create a GIF, export to Unity, or claim production readiness.
"""

import argparse
import hashlib
import json
import math
import os
import sys

import bmesh
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


def coordinate_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(("%.9f,%.9f,%.9f;" % tuple(vertex.co)).encode("ascii"))
    return digest.hexdigest().upper()


armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProofCamera")
mouth = bpy.data.objects.get("NormalizedMouthCurve")
if armature is None or body is None or camera is None or mouth is None:
    raise RuntimeError("Expected rejected Sister Proof3 rig/body/camera/mouth")
if len(armature.data.bones) != 118:
    raise RuntimeError("Owned Yuuka rig must remain exactly 118 bones")

body_hash_before = coordinate_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)

# Remove only the rejected procedural pieces.  The owned donor body contains
# the retained head, face, eyes, hair and original three-digit hands.
remove_prefixes = (
    "SisterTankTorsoProof3",
    "SisterTankStrapProof3",
    "SisterContinuousArmProof3",
    "SisterShortsPelvisFittedProof3",
    "SisterShortsLegFittedProof3",
    "SisterContinuousLegProof3",
    "SisterBareFoot",
    "SisterToe",
)
for obj in list(bpy.data.objects):
    if obj.name.startswith(remove_prefixes):
        bpy.data.objects.remove(obj, do_unlink=True)


def material(name, color, roughness=0.68):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return result


MAT_SKIN = bpy.data.materials.get("SisterSkin") or material("SisterProof4Skin", (0.91, 0.70, 0.62))
MAT_TANK = bpy.data.materials.get("SisterBlackTank") or material("SisterProof4Tank", (0.035, 0.031, 0.047))
MAT_SHORTS = bpy.data.materials.get("SisterNavyShorts") or material("SisterProof4Shorts", (0.035, 0.10, 0.24))
MAT_PIPING = bpy.data.materials.get("SisterWhitePiping") or material("SisterProof4Piping", (0.88, 0.91, 0.96))
MAT_GRAY = material("SisterProof4QAGray", (0.58, 0.61, 0.66), 0.82)


def normalized(mapping):
    total = sum(max(value, 0.0) for value in mapping.values())
    if total <= 0.0:
        return {}
    return {name: max(value, 0.0) / total for name, value in mapping.items() if value > 0.0}


def finish_mesh(name, vertices, faces, materials, weights, face_materials=None, subdivision=1):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for item in materials:
        obj.data.materials.append(item)
    if face_materials is not None:
        for polygon, index in zip(obj.data.polygons, face_materials):
            polygon.material_index = index
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
        modifier = obj.modifiers.new("SisterProof4Smooth", "SUBSURF")
        modifier.subdivision_type = "CATMULL_CLARK"
        modifier.levels = subdivision
        modifier.render_levels = subdivision
    rig = obj.modifiers.new("OwnedYuuka118BoneRig", "ARMATURE")
    rig.object = armature
    rig.use_vertex_groups = True
    obj["proofRevision"] = "SisterProof4ProportionGate"
    obj["candidateClaim"] = False
    return obj


def loft(name, rings, materials, ring_weights, segments=48, material_for_strip=None, cap=True):
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
            face_materials.append(material_for_strip.get(ring_index, 0) if material_for_strip else 0)
    if cap:
        bottom = len(vertices)
        vertices.append((rings[0][1], rings[0][2], rings[0][0]))
        weights.append(ring_weights[0])
        top = len(vertices)
        vertices.append((rings[-1][1], rings[-1][2], rings[-1][0]))
        weights.append(ring_weights[-1])
        last = (len(rings) - 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((bottom, nxt, segment))
            face_materials.append(0)
            faces.append((top, last + segment, last + nxt))
            face_materials.append(0)
    return finish_mesh(name, vertices, faces, materials, weights, face_materials, subdivision=1)


def path_frame(tangent):
    tangent = tangent.normalized()
    reference = Vector((0.0, 1.0, 0.0))
    if abs(tangent.dot(reference)) > 0.90:
        reference = Vector((0.0, 0.0, 1.0))
    axis_a = tangent.cross(reference).normalized()
    axis_b = tangent.cross(axis_a).normalized()
    return axis_a, axis_b


def tube(name, points, radii, material_item, ring_weights, segments=36, subdivision=1):
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
        for segment in range(segments):
            angle = math.tau * segment / segments
            point_on_ring = point + axis_a * (math.cos(angle) * radius[0]) + axis_b * (math.sin(angle) * radius[1])
            if index >= len(points) - 3:
                point_on_ring.z = max(point_on_ring.z, 0.003)
            vertices.append(tuple(point_on_ring))
            weights.append(mapping)
    faces = []
    for ring_index in range(len(points) - 1):
        first = ring_index * segments
        second = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
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
    return finish_mesh(name, vertices, faces, [material_item], weights, subdivision=subdivision)


def voxel_union(name, construction, voxel_size, materials, weight_function):
    raw = []
    for index, (vertices, faces) in enumerate(construction):
        mesh = bpy.data.meshes.new("QAConstructionMesh%02d" % index)
        mesh.from_pydata(vertices, [], faces)
        mesh.update(calc_edges=True)
        obj = bpy.data.objects.new("QAConstruction_%s_%02d" % (name, index), mesh)
        bpy.context.collection.objects.link(obj)
        raw.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in raw:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = raw[0]
    bpy.ops.object.join()
    joined = raw[0]
    joined.data.remesh_voxel_size = voxel_size
    joined.data.remesh_voxel_adaptivity = 0.0
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.voxel_remesh()
    edit = bmesh.new()
    edit.from_mesh(joined.data)
    bmesh.ops.smooth_vert(edit, verts=list(edit.verts), factor=0.24, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    edit.to_mesh(joined.data)
    edit.free()
    vertices = [tuple(joined.matrix_world @ vertex.co) for vertex in joined.data.vertices]
    faces = [tuple(polygon.vertices) for polygon in joined.data.polygons]
    bpy.data.objects.remove(joined, do_unlink=True)
    weights = [weight_function(Vector(vertex)) for vertex in vertices]
    result = finish_mesh(name, vertices, faces, materials, weights, subdivision=0)
    for polygon in result.data.polygons:
        polygon.material_index = 0
    return result


def cage(rings, segments=40):
    vertices = []
    for z, cx, cy, rx, ry in rings:
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle), z))
    faces = []
    for ring_index in range(len(rings) - 1):
        for segment in range(segments):
            nxt = (segment + 1) % segments
            a = ring_index * segments + segment
            b = ring_index * segments + nxt
            c = (ring_index + 1) * segments + nxt
            d = (ring_index + 1) * segments + segment
            faces.append((a, b, c, d))
    bottom = len(vertices)
    vertices.append((rings[0][1], rings[0][2], rings[0][0]))
    top = len(vertices)
    vertices.append((rings[-1][1], rings[-1][2], rings[-1][0]))
    last = (len(rings) - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom, nxt, segment))
        faces.append((top, last + segment, last + nxt))
    return vertices, faces


# A single fitted tank surface. Neckline and shoulder strap graphics are
# deferred to material/texture so no separate strap tubes can float.
tank_rings = (
    (0.414, 0.0, 0.000, 0.123, 0.062),
    (0.445, 0.0, 0.000, 0.111, 0.060),
    (0.486, 0.0, 0.000, 0.105, 0.063),
    (0.530, 0.0, 0.000, 0.124, 0.070),
    (0.574, 0.0, 0.000, 0.134, 0.073),
    (0.612, 0.0, 0.000, 0.124, 0.068),
    (0.638, 0.0, 0.000, 0.104, 0.060),
)
tank_weights = (
    {"Bip001 Pelvis": 0.30, "Bip001 Spine": 0.70},
    {"Bip001 Spine": 1.0},
    {"Bip001 Spine": 0.85, "Bip001 Spine1": 0.15},
    {"Bip001 Spine": 0.55, "Bip001 Spine1": 0.45},
    {"Bip001 Spine": 0.25, "Bip001 Spine1": 0.75},
    {"Bip001 Spine1": 1.0},
    {"Bip001 Spine1": 1.0},
)
tank = loft("SisterProof4_Tank_OneFittedSurface", tank_rings, [MAT_TANK], tank_weights)


# Slimmer continuous shoulder-to-wrist skin surfaces. They overlap the donor
# hand wrist envelope so the original three-digit hand does not appear pasted on.
arms = []
for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    upper = "Bip001 %s UpperArm" % suffix
    fore = "Bip001 %s Forearm" % suffix
    arms.append(
        tube(
            "SisterProof4_Arm_%s_Continuous" % suffix,
            (
                (0.112 * sign, 0.000, 0.615),
                (0.148 * sign, -0.002, 0.587),
                (0.184 * sign, -0.004, 0.551),
                (0.221 * sign, -0.006, 0.510),
                (0.254 * sign, -0.007, 0.470),
                (0.286 * sign, -0.007, 0.435),
            ),
            ((0.041, 0.038), (0.040, 0.037), (0.037, 0.034), (0.035, 0.033), (0.034, 0.032), (0.032, 0.030)),
            MAT_SKIN,
            (
                {upper: 1.0},
                {upper: 1.0},
                {upper: 0.78, fore: 0.22},
                {upper: 0.40, fore: 0.60},
                {fore: 0.90, upper: 0.10},
                {fore: 1.0},
            ),
        )
    )


# Shorts are one voxel-welded manifold surface: pelvis, crotch bridge and both
# leg openings. White piping is a material-face band on the same mesh.
shorts_parts = []
shorts_parts.append(cage(((0.372, 0.0, 0.004, 0.129, 0.066), (0.397, 0.0, 0.003, 0.134, 0.068), (0.427, 0.0, 0.001, 0.120, 0.063))))
for sign in (1.0, -1.0):
    center = 0.064 * sign
    shorts_parts.append(cage(((0.330, center, 0.004, 0.064, 0.070), (0.356, center, 0.004, 0.068, 0.074), (0.397, center, 0.003, 0.071, 0.076), (0.422, center, 0.002, 0.069, 0.072))))


def shorts_weights(point):
    if point.z > 0.385:
        return {"Bip001 Pelvis": 1.0}
    suffix = "L" if point.x >= 0.0 else "R"
    return {"Bip001 Pelvis": 0.62, "Bip001 %s Thigh" % suffix: 0.38}


shorts = voxel_union(
    "SisterProof4_Shorts_OneConnectedSurface",
    shorts_parts,
    0.0065,
    [MAT_SHORTS],
    shorts_weights,
)


# Each leg and bare foot is a single bent surface. Toes are texture-only; the
# rejected separate toe capsules are intentionally absent.
legs = []
for sign, suffix in ((1.0, "L"), (-1.0, "R")):
    thigh = "Bip001 %s Thigh" % suffix
    calf = "Bip001 %s Calf" % suffix
    foot = "Bip001 %s Foot" % suffix
    toe = "Bip001 %s Toe0" % suffix
    legs.append(
        tube(
            "SisterProof4_LegFoot_%s_OneContinuousSurface" % suffix,
            (
                (0.066 * sign, 0.003, 0.392),
                (0.069 * sign, 0.003, 0.342),
                (0.072 * sign, 0.002, 0.286),
                (0.075 * sign, 0.002, 0.228),
                (0.078 * sign, 0.004, 0.170),
                (0.081 * sign, 0.007, 0.112),
                (0.083 * sign, 0.010, 0.067),
                (0.083 * sign, -0.010, 0.038),
                (0.083 * sign, -0.054, 0.022),
                (0.083 * sign, -0.092, 0.018),
            ),
            ((0.048, 0.044), (0.047, 0.043), (0.043, 0.040), (0.037, 0.035), (0.039, 0.037), (0.034, 0.032), (0.030, 0.029), (0.033, 0.022), (0.038, 0.018), (0.036, 0.014)),
            MAT_SKIN,
            (
                {thigh: 1.0},
                {thigh: 1.0},
                {thigh: 0.78, calf: 0.22},
                {thigh: 0.28, calf: 0.72},
                {calf: 1.0},
                {calf: 0.92, foot: 0.08},
                {calf: 0.35, foot: 0.65},
                {foot: 1.0},
                {foot: 0.72, toe: 0.28},
                {toe: 1.0},
            ),
            segments=40,
        )
    )


# The old mouth line was too wide. Shrink it in its own local X axis without
# changing the owned face mesh or creating a new mouth plate.
mouth.scale.x *= 0.58
mouth["proof4MouthAdjustment"] = "existing surface-attached curve width reduced only"


def topology_stats(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    components = 0
    unseen = set(bm.verts)
    while unseen:
        components += 1
        stack = [unseen.pop()]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
    result = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "polygons": len(bm.faces),
        "connectedComponents": components,
        "boundaryEdges": sum(1 for edge in bm.edges if len(edge.link_faces) == 1),
        "nonManifoldEdges": sum(1 for edge in bm.edges if len(edge.link_faces) != 2),
        "nonQuadPolygons": sum(1 for face in bm.faces if len(face.verts) != 4),
    }
    bm.free()
    evaluated.to_mesh_clear()
    return result


new_surfaces = [tank, shorts, *arms, *legs]
stats = {obj.name: topology_stats(obj) for obj in new_surfaces}


# Review floor.
for old_name in ("SisterProof3Floor", "SisterProof4Floor"):
    old = bpy.data.objects.get(old_name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.001))
floor = bpy.context.object
floor.name = "SisterProof4Floor"
floor.data.materials.append(material("SisterProof4FloorMaterial", (0.040, 0.052, 0.072), 0.92))


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
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
camera.data.ortho_scale = 1.10


def point_camera(yaw_degrees, target_z=0.500, radius=3.20):
    radians = math.radians(yaw_degrees)
    target = Vector((0.0, 0.0, target_z))
    camera.location = (math.sin(radians) * radius, -math.cos(radians) * radius, target_z + 0.02)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


views = (("front", 0), ("three-quarter", 45), ("side", 90), ("back", 180))
static_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = os.path.join(OUTPUT, "sister-proof4-%s.png" % label)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    static_outputs.append(path)


# Neutral gray silhouette views.  These intentionally omit topology wires from
# the face/hair so the retained donor remains legible while garment fit is judged.
material_backup = {obj.name: list(obj.data.materials) for obj in new_surfaces}
for obj in new_surfaces:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
gray_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = os.path.join(OUTPUT, "sister-proof4-%s-gray-silhouette.png" % label)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    gray_outputs.append(path)
for obj in new_surfaces:
    obj.data.materials.clear()
    for item in material_backup[obj.name]:
        obj.data.materials.append(item)


body_hash_after = coordinate_hash(body)
if body_hash_after != body_hash_before:
    raise RuntimeError("Owned Yuuka body/head/hair/hand coordinates changed")
if sorted(bone.name for bone in armature.data.bones) != bone_names_before:
    raise RuntimeError("Owned Yuuka rig bone names changed")

blend_path = os.path.join(OUTPUT, "sister-proof4-proportion-gate.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

receipt = {
    "schema": "family-company.sister-proof4-proportion-gate.v1",
    "status": "DIAGNOSTIC_ONLY_STYLE_FAIL",
    "candidateClaim": False,
    "sourceBlend": SOURCE_BLEND,
    "sourceBlendSha256": sha256(SOURCE_BLEND),
    "sourceBasis": "user-owned test2 Yuuka Sister Proof3 diagnostic blend",
    "test3OrSakurakoUsed": False,
    "unityModified": False,
    "preservation": {
        "originalFaceEyesHairBodyCoordinateHashBefore": body_hash_before,
        "originalFaceEyesHairBodyCoordinateHashAfter": body_hash_after,
        "originalDonorBodyCoordinatesUnchanged": body_hash_before == body_hash_after,
        "handPolicy": "original 3-digit stylized hand retained",
        "boneCount": len(armature.data.bones),
        "boneNamesUnchanged": sorted(bone.name for bone in armature.data.bones) == bone_names_before,
    },
    "proof4Changes": {
        "tank": "one fitted continuous surface; neckline/straps texture-only",
        "shorts": "one voxel-welded pelvis/crotch/leg-opening surface; piping deferred to texture",
        "arms": "slimmer continuous shoulder-to-wrist fitted surfaces overlapping donor wrist envelope",
        "legsAndFeet": "each side one continuous thigh-to-toe surface; separate toe capsules removed",
        "mouth": "existing surface-attached curve width reduced; no mouth plate added",
        "adultReadIntent": "less toddler-like limb volume while retaining cute anime proportions",
    },
    "topology": stats,
    "staticViews": [os.path.basename(path) for path in static_outputs],
    "graySilhouetteViews": [os.path.basename(path) for path in gray_outputs],
    "gifCreated": False,
    "blend": os.path.basename(blend_path),
    "limitations": [
        "internal static proportion/surface QA only",
        "no visual age or production pass claimed",
        "animation deformation has not been tested",
    ],
    "rootStyleGate": {
        "result": "FAIL",
        "improved": [
            "original face/hair/three-digit hands remain recognizable",
            "legs are straighter and slimmer than rejected Proof3",
            "separate toe capsules and shorts piping geometry are absent",
            "all replacement surfaces are closed one-component meshes",
        ],
        "blockingVisualIssues": [
            "tank and shorts still read as rounded procedural volumes instead of tailored cloth",
            "shorts crotch notch is pinched and front silhouette remains slab-like",
            "arm overlap creates visible elbow/forearm assembly seams",
            "bare feet terminate as oval pads instead of polished anime feet",
            "body still lacks the shoulder/waist/hip/knee shaping needed for a clear age-20 read",
        ],
        "colorOrGifPromotionAllowed": False,
    },
}
with open(os.path.join(OUTPUT, "sister-proof4-proportion-gate-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("SISTER_PROOF4_PROPORTION_GATE_RENDERED")
