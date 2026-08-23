import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Vector


def parse_args():
    args = sys.argv
    args = args[args.index("--") + 1 :] if "--" in args else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--reference", required=True)
    return parser.parse_args(args)


ARGS = parse_args()
OUTPUT = os.path.abspath(ARGS.output)
REFERENCE = os.path.abspath(ARGS.reference)
os.makedirs(OUTPUT, exist_ok=True)


def clear_scene():
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
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


clear_scene()


def material(name, color, roughness=0.62, metallic=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.28
    return mat


MAT = {
    "skin": material("M_SkinWarm", (0.94, 0.57, 0.37), 0.58),
    "skin_light": material("M_SkinHighlight", (1.0, 0.72, 0.52), 0.56),
    "hair": material("M_HairDarkBrown", (0.095, 0.035, 0.015), 0.42),
    "hair_light": material("M_HairWarmBrown", (0.20, 0.075, 0.032), 0.42),
    "eye_white": material("M_EyeWhite", (0.98, 0.98, 0.96), 0.28),
    "iris": material("M_IrisBrown", (0.30, 0.105, 0.025), 0.26),
    "pupil": material("M_Pupil", (0.012, 0.007, 0.004), 0.22),
    "white": material("M_HoodieWhite", (0.93, 0.94, 0.94), 0.78),
    "white_trim": material("M_ShoeWhite", (0.98, 0.98, 0.96), 0.55),
    "navy": material("M_Navy", (0.018, 0.055, 0.115), 0.70),
    "denim": material("M_Denim", (0.025, 0.075, 0.145), 0.82),
    "denim_light": material("M_DenimCuff", (0.050, 0.12, 0.21), 0.78),
    "red": material("M_AccentRed", (0.55, 0.018, 0.020), 0.62),
    "yellow": material("M_ShirtYellow", (0.95, 0.50, 0.035), 0.68),
    "mouth": material("M_Mouth", (0.35, 0.035, 0.025), 0.56),
    "cheek": material("M_Cheek", (0.95, 0.32, 0.25), 0.62),
    "sole": material("M_Sole", (0.86, 0.87, 0.86), 0.82),
    "floor": material("M_ReviewFloor", (0.17, 0.18, 0.20), 0.92),
}


BODY_PARTS = []


def finish_mesh(obj, mat, bone=None, smooth=True):
    if mat:
        obj.data.materials.append(mat)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bone:
        group = obj.vertex_groups.new(name=bone)
        group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    BODY_PARTS.append(obj)
    return obj


def uv(name, location, scale, mat, bone, segments=24, rings=16, rotation=None):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
        rotation=rotation or (0.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_mesh(obj, mat, bone)


def rounded_box(name, location, dimensions, mat, bone, bevel=0.06, rotation=None):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation or (0.0, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new(name="SoftEdges", type="BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish_mesh(obj, mat, bone)


def cylinder_between(name, start, end, radius, mat, bone, vertices=20):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=direction.length,
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return finish_mesh(obj, mat, bone)


def cone_between(name, base, tip, radius, mat, bone, vertices=10):
    base = Vector(base)
    tip = Vector(tip)
    direction = tip - base
    midpoint = (base + tip) * 0.5
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius,
        radius2=max(radius * 0.12, 0.008),
        depth=direction.length,
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return finish_mesh(obj, mat, bone)


def torus(name, location, major_radius, minor_radius, mat, bone, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=32,
        minor_segments=10,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, mat, bone)


def curve_stroke(name, points, radius, mat, bone, cyclic=False):
    curve_data = bpy.data.curves.new(name=name + "Curve", type="CURVE")
    curve_data.dimensions = "3D"
    curve_data.resolution_u = 2
    curve_data.bevel_depth = radius
    curve_data.bevel_resolution = 3
    spline = curve_data.splines.new(type="BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bezier_point, point in zip(spline.bezier_points, points):
        bezier_point.co = point
        bezier_point.handle_left_type = "AUTO"
        bezier_point.handle_right_type = "AUTO"
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    return finish_mesh(obj, mat, bone)


def build_armature():
    data = bpy.data.armatures.new("PlayerV6_Humanoid")
    armature = bpy.data.objects.new("PlayerV6_Rig", data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = {}

    def bone(name, head, tail, parent=None, deform=True):
        item = data.edit_bones.new(name)
        item.head = head
        item.tail = tail
        item.use_deform = deform
        if parent:
            item.parent = edit_bones[parent]
        edit_bones[name] = item
        return item

    bone("Root", (0, 0, 0.0), (0, 0, 0.18), deform=False)
    bone("Hips", (0, 0, 1.52), (0, 0, 1.72), "Root")
    bone("Spine", (0, 0, 1.72), (0, 0, 1.94), "Hips")
    bone("Chest", (0, 0, 1.94), (0, 0, 2.15), "Spine")
    bone("UpperChest", (0, 0, 2.15), (0, 0, 2.29), "Chest")
    bone("Neck", (0, 0, 2.29), (0, 0, 2.42), "UpperChest")
    bone("Head", (0, 0, 2.42), (0, 0, 3.02), "Neck")

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        x = 0.18 * sign
        bone(side + "UpperLeg", (x, 0, 1.60), (x, 0, 1.04), "Hips")
        bone(side + "LowerLeg", (x, 0, 1.04), (x, 0, 0.43), side + "UpperLeg")
        bone(side + "Foot", (x, 0, 0.43), (x, -0.22, 0.20), side + "LowerLeg")
        bone(side + "Toes", (x, -0.22, 0.20), (x, -0.45, 0.16), side + "Foot")

        shoulder_x = 0.08 * sign
        upper_x = 0.46 * sign
        elbow_x = 0.70 * sign
        wrist_x = 0.82 * sign
        hand_x = 0.84 * sign
        bone(side + "Shoulder", (shoulder_x, 0, 2.23), (upper_x, 0, 2.17), "UpperChest")
        bone(side + "UpperArm", (upper_x, 0, 2.17), (elbow_x, 0, 1.84), side + "Shoulder")
        bone(side + "LowerArm", (elbow_x, 0, 1.84), (wrist_x, -0.005, 1.53), side + "UpperArm")
        bone(side + "Hand", (wrist_x, -0.005, 1.53), (hand_x, -0.015, 1.30), side + "LowerArm")

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


RIG = build_armature()


# Head and face: compact oval proportions matched to the turnaround rather than a block mascot.
uv("Head", (0.0, -0.012, 2.68), (0.325, 0.285, 0.375), MAT["skin"], "Head", 40, 24)
uv("Ear.L", (0.320, -0.002, 2.67), (0.060, 0.038, 0.086), MAT["skin"], "Head", 20, 12)
uv("Ear.R", (-0.320, -0.002, 2.67), (0.060, 0.038, 0.086), MAT["skin"], "Head", 20, 12)
uv("Nose", (0.0, -0.294, 2.620), (0.018, 0.012, 0.024), MAT["skin_light"], "Head", 16, 10)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.122 * sign
    uv("EyeWhite." + side, (x, -0.279, 2.705), (0.092, 0.021, 0.128), MAT["eye_white"], "Head", 28, 18)
    uv("Iris." + side, (x, -0.301, 2.700), (0.057, 0.013, 0.084), MAT["iris"], "Head", 22, 14)
    uv("Pupil." + side, (x, -0.313, 2.700), (0.027, 0.008, 0.050), MAT["pupil"], "Head", 18, 12)
    uv("EyeGlint." + side, (x - 0.014 * sign, -0.321, 2.737), (0.010, 0.005, 0.016), MAT["eye_white"], "Head", 12, 8)
    curve_stroke(
        "Brow." + side,
        [(x - 0.060 * sign, -0.295, 2.826), (x, -0.307, 2.840), (x + 0.060 * sign, -0.295, 2.824)],
        0.009,
        MAT["hair"],
        "Head",
    )
    uv("Cheek." + side, (0.195 * sign, -0.291, 2.582), (0.040, 0.006, 0.016), MAT["cheek"], "Head", 16, 10)

curve_stroke(
    "Smile",
    [(-0.072, -0.299, 2.548), (0.0, -0.309, 2.532), (0.072, -0.299, 2.548)],
    0.007,
    MAT["mouth"],
    "Head",
)


# Hair cap plus rounded, overlapping locks. No cone crown or helmet rim is permitted.
uv("HairCap", (0.0, 0.025, 2.880), (0.350, 0.305, 0.290), MAT["hair"], "Head", 40, 24)
front_locks = [
    (-0.245, -0.258, 2.845, -28),
    (-0.160, -0.285, 2.860, -18),
    (-0.070, -0.295, 2.865, -9),
    (0.020, -0.298, 2.865, 5),
    (0.110, -0.288, 2.855, 14),
    (0.205, -0.265, 2.835, 25),
]
for index, (x, y, z, tilt) in enumerate(front_locks):
    uv(
        "FrontLock.%02d" % index,
        (x, y, z),
        (0.072, 0.050, 0.165 + (0.015 if index in (1, 2, 3, 4) else 0.0)),
        MAT["hair_light"],
        "Head",
        24,
        14,
        (math.radians(8), math.radians(tilt), 0.0),
    )
side_back_locks = [
    ((0.315, -0.055, 2.82), (0.085, 0.070, 0.165), 28),
    ((0.325, 0.085, 2.80), (0.080, 0.075, 0.160), 38),
    ((0.280, 0.220, 2.79), (0.085, 0.070, 0.165), 26),
    ((-0.315, -0.055, 2.82), (0.085, 0.070, 0.165), -28),
    ((-0.325, 0.085, 2.80), (0.080, 0.075, 0.160), -38),
    ((-0.280, 0.220, 2.79), (0.085, 0.070, 0.165), -26),
    ((0.155, 0.275, 2.80), (0.090, 0.065, 0.170), 15),
    ((0.000, 0.300, 2.79), (0.095, 0.060, 0.180), 0),
    ((-0.155, 0.275, 2.80), (0.090, 0.065, 0.170), -15),
]
for index, (location, scale, tilt) in enumerate(side_back_locks):
    uv("SideBackLock.%02d" % index, location, scale, MAT["hair"], "Head", 22, 14, (0.0, math.radians(tilt), 0.0))
for index, (location, tilt) in enumerate((((-0.10, 0.0, 3.150), -22), ((0.01, 0.0, 3.175), 2), ((0.12, 0.02, 3.135), 24))):
    uv("CrownLock.%02d" % index, location, (0.050, 0.040, 0.120), MAT["hair"], "Head", 20, 12, (0.0, math.radians(tilt), 0.0))


# Torso and striped shirt visible through the open hoodie.
uv("NeckSkin", (0.0, -0.005, 2.32), (0.105, 0.090, 0.125), MAT["skin"], "Neck", 24, 14)
uv("StripedShirt", (0.0, -0.005, 1.93), (0.315, 0.205, 0.475), MAT["navy"], "Chest", 32, 20)
for idx, z in enumerate((1.67, 1.86, 2.05)):
    rounded_box("ShirtStripe.%d" % idx, (0.0, -0.214, z), (0.49, 0.024, 0.082), MAT["yellow"], "Chest", 0.014)
rounded_box("ShirtCollarRed", (0.0, -0.221, 2.255), (0.35, 0.026, 0.055), MAT["red"], "UpperChest", 0.014)
rounded_box("ShirtCollarNavy", (0.0, -0.223, 2.212), (0.38, 0.026, 0.043), MAT["navy"], "UpperChest", 0.012)

# Jacket shell and open front panels use soft ellipsoids instead of stacked cubes.
uv("HoodieBack", (0.0, 0.095, 1.93), (0.385, 0.220, 0.505), MAT["white"], "Chest", 36, 22)
uv("HoodieFront.L", (0.245, -0.135, 1.92), (0.175, 0.105, 0.495), MAT["white"], "Chest", 30, 18)
uv("HoodieFront.R", (-0.245, -0.135, 1.92), (0.175, 0.105, 0.495), MAT["white"], "Chest", 30, 18)
rounded_box("ZipperEdge.L", (0.085, -0.225, 1.91), (0.022, 0.020, 0.90), MAT["navy"], "Chest", 0.008)
rounded_box("ZipperEdge.R", (-0.085, -0.225, 1.91), (0.022, 0.020, 0.90), MAT["navy"], "Chest", 0.008)

# Front badge blocks and back shoulder stripe.
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.245 * sign
    rounded_box("FrontNavy." + side, (x, -0.231, 2.105), (0.205, 0.020, 0.080), MAT["navy"], "UpperChest", 0.012)
    rounded_box("FrontRed." + side, (x, -0.233, 2.172), (0.205, 0.020, 0.055), MAT["red"], "UpperChest", 0.011)
rounded_box("BackNavyStripe", (0.0, 0.296, 2.08), (0.70, 0.020, 0.085), MAT["navy"], "UpperChest", 0.012)
rounded_box("BackRedStripe", (0.0, 0.298, 2.15), (0.70, 0.020, 0.055), MAT["red"], "UpperChest", 0.011)

# Hoodie hem, pockets, hood, collar and drawstrings.
rounded_box("HoodieHemFront.L", (0.245, -0.220, 1.465), (0.30, 0.035, 0.070), MAT["navy"], "Spine", 0.016)
rounded_box("HoodieHemFront.R", (-0.245, -0.220, 1.465), (0.30, 0.035, 0.070), MAT["navy"], "Spine", 0.016)
rounded_box("HoodieHemBack", (0.0, 0.292, 1.465), (0.74, 0.035, 0.070), MAT["navy"], "Spine", 0.016)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.285 * sign
    curve_stroke("PocketEdge." + side, [(x - 0.045 * sign, -0.230, 1.70), (x, -0.240, 1.78), (x + 0.045 * sign, -0.230, 1.69)], 0.010, MAT["navy"], "Spine")
uv("Hood", (0.0, 0.230, 2.30), (0.360, 0.155, 0.230), MAT["white"], "UpperChest", 32, 20)
torus("Collar", (0.0, 0.00, 2.305), 0.205, 0.040, MAT["navy"], "UpperChest")
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.145 * sign
    curve_stroke("Drawstring." + side, [(x, -0.225, 2.30), (x + 0.01 * sign, -0.242, 2.08), (x + 0.02 * sign, -0.240, 1.96)], 0.008, MAT["navy"], "Chest")
    rounded_box("DrawstringTip." + side, (x + 0.02 * sign, -0.243, 1.93), (0.024, 0.020, 0.060), MAT["red"], "Chest", 0.006)


# Sleeved arms in a relaxed A-pose, with overlapping elbow volume and intact hands.
arm_points = {
    "L": ((0.39, 0.0, 2.16), (0.65, -0.010, 1.86), (0.77, -0.018, 1.56)),
    "R": ((-0.39, 0.0, 2.16), (-0.65, -0.010, 1.86), (-0.77, -0.018, 1.56)),
}
for side, (shoulder, elbow, wrist) in arm_points.items():
    prefix = "Left" if side == "L" else "Right"
    uv("SleeveShoulder." + side, shoulder, (0.135, 0.130, 0.140), MAT["white"], prefix + "UpperArm", 24, 14)
    cylinder_between("SleeveUpper." + side, shoulder, elbow, 0.126, MAT["white"], prefix + "UpperArm", 24)
    uv("SleeveElbow." + side, elbow, (0.126, 0.125, 0.126), MAT["white"], prefix + "LowerArm", 22, 14)
    cylinder_between("SleeveLower." + side, elbow, wrist, 0.115, MAT["white"], prefix + "LowerArm", 24)
    cuff_start = Vector(wrist) + (Vector(elbow) - Vector(wrist)).normalized() * 0.05
    cuff_end = Vector(wrist) - (Vector(elbow) - Vector(wrist)).normalized() * 0.08
    cylinder_between("CuffNavy." + side, cuff_start, cuff_end, 0.124, MAT["navy"], prefix + "LowerArm")
    cuff2_start = cuff_start + (Vector(elbow) - Vector(wrist)).normalized() * 0.025
    cuff2_end = cuff_start + (Vector(wrist) - Vector(elbow)).normalized() * 0.020
    cylinder_between("CuffWhite." + side, cuff2_start, cuff2_end, 0.128, MAT["white"], prefix + "LowerArm")
    hand_center = (wrist[0] + (0.012 if side == "L" else -0.012), -0.018, 1.435)
    uv("Palm." + side, hand_center, (0.082, 0.060, 0.118), MAT["skin"], prefix + "Hand", 22, 14)
    for finger_index in range(4):
        finger_x = hand_center[0] + ((finger_index - 1.5) * 0.024)
        cylinder_between(
            "Finger.%s.%d" % (side, finger_index),
            (finger_x, -0.025, 1.39),
            (finger_x, -0.032, 1.29 + abs(finger_index - 1.5) * 0.008),
            0.010,
            MAT["skin"],
            prefix + "Hand",
            vertices=12,
        )
    thumb_sign = 1.0 if side == "L" else -1.0
    cylinder_between("Thumb." + side, (hand_center[0] - 0.050 * thumb_sign, -0.025, 1.43), (hand_center[0] - 0.083 * thumb_sign, -0.035, 1.36), 0.014, MAT["skin"], prefix + "Hand", 12)


# Jeans and cuffs: two slimmer complete legs meet the shoes with no ankle gap.
rounded_box("JeansHips", (0.0, 0.0, 1.50), (0.56, 0.32, 0.28), MAT["denim"], "Hips", 0.10)
leg_data = {
    "L": (0.165, "Left"),
    "R": (-0.165, "Right"),
}
for side, (x, prefix) in leg_data.items():
    cylinder_between("JeansUpper." + side, (x, 0.0, 1.48), (x, 0.0, 1.03), 0.165, MAT["denim"], prefix + "UpperLeg", 28)
    uv("JeansKnee." + side, (x, 0.0, 1.02), (0.165, 0.155, 0.170), MAT["denim"], prefix + "LowerLeg", 24, 14)
    cylinder_between("JeansLower." + side, (x, 0.0, 1.02), (x, 0.0, 0.40), 0.148, MAT["denim"], prefix + "LowerLeg", 28)
    cylinder_between("JeansCuff." + side, (x, 0.0, 0.47), (x, 0.0, 0.32), 0.158, MAT["denim_light"], prefix + "LowerLeg", 28)

    rounded_box("Sole." + side, (x, -0.075, 0.060), (0.30, 0.51, 0.10), MAT["sole"], prefix + "Foot", 0.050)
    rounded_box("ShoeUpper." + side, (x, -0.075, 0.235), (0.285, 0.44, 0.25), MAT["white_trim"], prefix + "Foot", 0.075)
    rounded_box("ShoeTongue." + side, (x, -0.285, 0.255), (0.13, 0.045, 0.17), MAT["navy"], prefix + "Foot", 0.020)
    for lace_index in range(3):
        z = 0.215 + lace_index * 0.040
        rounded_box("Lace.%s.%d" % (side, lace_index), (x, -0.307, z), (0.19, 0.018, 0.014), MAT["white_trim"], prefix + "Foot", 0.006)
    stripe_x = x + (0.145 if side == "L" else -0.145)
    rounded_box("ShoeNavySide." + side, (stripe_x, -0.065, 0.215), (0.020, 0.22, 0.085), MAT["navy"], prefix + "Foot", 0.006)
    rounded_box("ShoeRedSide." + side, (stripe_x + (0.003 if side == "L" else -0.003), -0.02, 0.215), (0.022, 0.065, 0.085), MAT["red"], prefix + "Foot", 0.006)


# Join every character surface into one skinned mesh object while retaining material slots and weights.
bpy.ops.object.select_all(action="DESELECT")
for obj in BODY_PARTS:
    obj.select_set(True)
bpy.context.view_layer.objects.active = BODY_PARTS[0]
bpy.ops.object.join()
BODY = bpy.context.object
BODY.name = "PlayerV6_CompleteBody"
for modifier in list(BODY.modifiers):
    BODY.modifiers.remove(modifier)
armature_modifier = BODY.modifiers.new(name="PlayerV6_HumanoidSkin", type="ARMATURE")
armature_modifier.object = RIG
BODY.parent = RIG


# Review set, floor and lighting.
bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0.0, 0.0, 0.0))
FLOOR = bpy.context.object
FLOOR.name = "ReviewFloor"
FLOOR.data.materials.append(MAT["floor"])


def add_area(name, location, energy, color, size):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    direction = Vector((0.0, 0.0, 1.55)) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return obj


add_area("Key", (-4.0, -5.0, 6.0), 900.0, (1.0, 0.86, 0.72), 4.0)
add_area("Fill", (4.0, -3.0, 4.0), 650.0, (0.72, 0.84, 1.0), 3.5)
add_area("Rim", (0.0, 4.0, 5.0), 800.0, (1.0, 0.82, 0.68), 3.0)

camera_data = bpy.data.cameras.new("IdentityReviewCamera")
CAMERA = bpy.data.objects.new("IdentityReviewCamera", camera_data)
bpy.context.collection.objects.link(CAMERA)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 3.45
bpy.context.scene.camera = CAMERA


def point_camera(location, target=(0.0, 0.0, 1.58)):
    CAMERA.location = location
    direction = Vector(target) - CAMERA.location
    CAMERA.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 600
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.filepath = os.path.join(OUTPUT, "unused.png")
scene.world.color = (0.035, 0.040, 0.050)
scene.view_settings.look = "AgX - Medium High Contrast"

views = {
    "front": (0.0, -7.0, 1.62),
    "left": (7.0, 0.0, 1.62),
    "back": (0.0, 7.0, 1.62),
    "three_quarter": (4.8, -5.2, 1.72),
}
for name, location in views.items():
    point_camera(location)
    scene.render.filepath = os.path.join(OUTPUT, "player-v6-blender-%s-v2.png" % name)
    bpy.ops.render.render(write_still=True)


# Export only the complete body and Humanoid rig.
bpy.ops.object.select_all(action="DESELECT")
BODY.select_set(True)
RIG.select_set(True)
bpy.context.view_layer.objects.active = RIG
fbx_path = os.path.join(OUTPUT, "player-v6-blender-humanoid-v2.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
)

blend_path = os.path.join(OUTPUT, "player-v6-blender-identity-v2.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

world_bounds = []
for corner in BODY.bound_box:
    world_bounds.append(BODY.matrix_world @ Vector(corner))
mins = [min(point[index] for point in world_bounds) for index in range(3)]
maxs = [max(point[index] for point in world_bounds) for index in range(3)]
receipt = {
    "contract": "FC-PLAYER-V6-BLENDER-IDENTITY-V2",
    "status": "VISUAL_REVIEW_REQUIRED_DO_NOT_IMPORT_TO_UNITY_YET",
    "reference": REFERENCE,
    "blenderVersion": bpy.app.version_string,
    "bodyObject": BODY.name,
    "meshObjectCount": 1,
    "vertexCount": len(BODY.data.vertices),
    "polygonCount": len(BODY.data.polygons),
    "materialCount": len(BODY.data.materials),
    "armatureObject": RIG.name,
    "boneCount": len(RIG.data.bones),
    "boneNames": sorted(bone.name for bone in RIG.data.bones),
    "boundsMin": mins,
    "boundsMax": maxs,
    "standingHeight": maxs[2] - mins[2],
    "outputs": {
        "blend": blend_path,
        "fbx": fbx_path,
        "renders": [os.path.join(OUTPUT, "player-v6-blender-%s-v2.png" % name) for name in views],
    },
    "productionEligible": False,
}
with open(os.path.join(OUTPUT, "build-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print(json.dumps(receipt, ensure_ascii=False, indent=2))
