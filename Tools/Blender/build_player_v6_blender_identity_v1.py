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


def uv(name, location, scale, mat, bone, segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
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


# Head and face: large but not oversized relative to the approved V6 turnaround.
uv("Head", (0.0, -0.012, 2.68), (0.36, 0.31, 0.41), MAT["skin"], "Head", 32, 20)
uv("Ear.L", (0.355, -0.005, 2.67), (0.075, 0.045, 0.105), MAT["skin"], "Head", 20, 12)
uv("Ear.R", (-0.355, -0.005, 2.67), (0.075, 0.045, 0.105), MAT["skin"], "Head", 20, 12)
uv("Nose", (0.0, -0.322, 2.615), (0.025, 0.018, 0.030), MAT["skin_light"], "Head", 16, 10)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.135 * sign
    uv("EyeWhite." + side, (x, -0.300, 2.705), (0.112, 0.025, 0.150), MAT["eye_white"], "Head", 24, 16)
    uv("Iris." + side, (x, -0.326, 2.700), (0.070, 0.015, 0.102), MAT["iris"], "Head", 20, 14)
    uv("Pupil." + side, (x, -0.340, 2.700), (0.036, 0.010, 0.062), MAT["pupil"], "Head", 18, 12)
    uv("EyeGlint." + side, (x - 0.018 * sign, -0.351, 2.742), (0.014, 0.006, 0.021), MAT["eye_white"], "Head", 12, 8)
    curve_stroke(
        "Brow." + side,
        [(x - 0.070 * sign, -0.327, 2.845), (x, -0.344, 2.860), (x + 0.070 * sign, -0.327, 2.842)],
        0.012,
        MAT["hair"],
        "Head",
    )
    uv("Cheek." + side, (0.215 * sign, -0.315, 2.575), (0.052, 0.008, 0.022), MAT["cheek"], "Head", 16, 10)

curve_stroke(
    "Smile",
    [(-0.085, -0.326, 2.535), (0.0, -0.340, 2.515), (0.085, -0.326, 2.535)],
    0.009,
    MAT["mouth"],
    "Head",
)


# Hair cap and layered tapered locks.
uv("HairCap", (0.0, 0.025, 2.855), (0.385, 0.335, 0.335), MAT["hair"], "Head", 32, 20)
hair_tufts = [
    ((-0.29, -0.18, 2.99), (-0.31, -0.28, 2.70), 0.105),
    ((-0.18, -0.25, 3.02), (-0.20, -0.31, 2.73), 0.110),
    ((-0.06, -0.27, 3.03), (-0.07, -0.32, 2.75), 0.105),
    ((0.07, -0.27, 3.03), (0.03, -0.32, 2.76), 0.105),
    ((0.19, -0.24, 3.01), (0.15, -0.31, 2.72), 0.110),
    ((0.30, -0.17, 2.98), (0.28, -0.28, 2.69), 0.100),
    ((0.34, -0.03, 2.96), (0.44, -0.08, 2.82), 0.100),
    ((0.35, 0.08, 2.91), (0.46, 0.10, 2.78), 0.095),
    ((0.34, 0.18, 2.84), (0.43, 0.24, 2.68), 0.090),
    ((-0.34, -0.03, 2.96), (-0.44, -0.08, 2.82), 0.100),
    ((-0.35, 0.08, 2.91), (-0.46, 0.10, 2.78), 0.095),
    ((-0.34, 0.18, 2.84), (-0.43, 0.24, 2.68), 0.090),
    ((-0.25, 0.27, 2.92), (-0.31, 0.36, 2.70), 0.100),
    ((-0.10, 0.31, 2.98), (-0.12, 0.39, 2.69), 0.105),
    ((0.08, 0.31, 2.98), (0.08, 0.39, 2.68), 0.105),
    ((0.24, 0.27, 2.92), (0.30, 0.36, 2.70), 0.100),
    ((-0.16, 0.02, 3.13), (-0.21, -0.02, 3.28), 0.080),
    ((-0.02, 0.03, 3.16), (-0.01, 0.01, 3.34), 0.078),
    ((0.12, 0.04, 3.13), (0.18, 0.00, 3.29), 0.076),
    ((0.23, 0.08, 3.08), (0.33, 0.05, 3.20), 0.070),
]
for index, (base, tip, radius) in enumerate(hair_tufts):
    cone_between("HairTuft.%02d" % index, base, tip, radius, MAT["hair_light" if index < 6 else "hair"], "Head")


# Torso and striped shirt visible through the open hoodie.
uv("StripedShirt", (0.0, -0.005, 1.93), (0.355, 0.235, 0.50), MAT["navy"], "Chest", 28, 18)
for idx, z in enumerate((1.67, 1.86, 2.05)):
    rounded_box("ShirtStripe.%d" % idx, (0.0, -0.245, z), (0.56, 0.030, 0.095), MAT["yellow"], "Chest", 0.018)
rounded_box("ShirtCollarRed", (0.0, -0.255, 2.255), (0.40, 0.035, 0.065), MAT["red"], "UpperChest", 0.018)
rounded_box("ShirtCollarNavy", (0.0, -0.258, 2.205), (0.43, 0.035, 0.050), MAT["navy"], "UpperChest", 0.016)

# Jacket side/back shell and two open front panels.
rounded_box("HoodieBack", (0.0, 0.100, 1.93), (0.86, 0.28, 1.04), MAT["white"], "Chest", 0.16)
rounded_box("HoodieFront.L", (0.275, -0.190, 1.92), (0.31, 0.13, 1.03), MAT["white"], "Chest", 0.070)
rounded_box("HoodieFront.R", (-0.275, -0.190, 1.92), (0.31, 0.13, 1.03), MAT["white"], "Chest", 0.070)
rounded_box("ZipperEdge.L", (0.105, -0.268, 1.91), (0.030, 0.028, 0.93), MAT["navy"], "Chest", 0.010)
rounded_box("ZipperEdge.R", (-0.105, -0.268, 1.91), (0.030, 0.028, 0.93), MAT["navy"], "Chest", 0.010)

# Front badge blocks and back shoulder stripe.
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.275 * sign
    rounded_box("FrontNavy." + side, (x, -0.262, 2.105), (0.24, 0.026, 0.095), MAT["navy"], "UpperChest", 0.015)
    rounded_box("FrontRed." + side, (x, -0.266, 2.185), (0.24, 0.026, 0.070), MAT["red"], "UpperChest", 0.014)
rounded_box("BackNavyStripe", (0.0, 0.255, 2.08), (0.78, 0.025, 0.100), MAT["navy"], "UpperChest", 0.015)
rounded_box("BackRedStripe", (0.0, 0.258, 2.165), (0.78, 0.025, 0.070), MAT["red"], "UpperChest", 0.014)

# Hoodie hem, pockets, hood, collar and drawstrings.
rounded_box("HoodieHemFront.L", (0.275, -0.255, 1.430), (0.34, 0.045, 0.085), MAT["navy"], "Spine", 0.020)
rounded_box("HoodieHemFront.R", (-0.275, -0.255, 1.430), (0.34, 0.045, 0.085), MAT["navy"], "Spine", 0.020)
rounded_box("HoodieHemBack", (0.0, 0.235, 1.430), (0.83, 0.045, 0.085), MAT["navy"], "Spine", 0.020)
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.315 * sign
    curve_stroke("PocketEdge." + side, [(x - 0.055 * sign, -0.268, 1.68), (x, -0.280, 1.78), (x + 0.055 * sign, -0.268, 1.66)], 0.014, MAT["navy"], "Spine")
uv("Hood", (0.0, 0.215, 2.30), (0.42, 0.18, 0.265), MAT["white"], "UpperChest", 28, 18)
torus("Collar", (0.0, 0.00, 2.305), 0.225, 0.052, MAT["navy"], "UpperChest")
for sign, side in ((1.0, "L"), (-1.0, "R")):
    x = 0.145 * sign
    curve_stroke("Drawstring." + side, [(x, -0.257, 2.30), (x + 0.01 * sign, -0.278, 2.05), (x + 0.02 * sign, -0.275, 1.91)], 0.010, MAT["navy"], "Chest")
    rounded_box("DrawstringTip." + side, (x + 0.02 * sign, -0.280, 1.88), (0.030, 0.025, 0.075), MAT["red"], "Chest", 0.008)


# Sleeved arms in a relaxed A-pose, with overlapping elbow volume and intact hands.
arm_points = {
    "L": ((0.43, 0.0, 2.17), (0.69, -0.005, 1.84), (0.81, -0.010, 1.53)),
    "R": ((-0.43, 0.0, 2.17), (-0.69, -0.005, 1.84), (-0.81, -0.010, 1.53)),
}
for side, (shoulder, elbow, wrist) in arm_points.items():
    prefix = "Left" if side == "L" else "Right"
    cylinder_between("SleeveUpper." + side, shoulder, elbow, 0.155, MAT["white"], prefix + "UpperArm")
    uv("SleeveElbow." + side, elbow, (0.155, 0.155, 0.155), MAT["white"], prefix + "LowerArm", 20, 12)
    cylinder_between("SleeveLower." + side, elbow, wrist, 0.142, MAT["white"], prefix + "LowerArm")
    cuff_start = Vector(wrist) + (Vector(elbow) - Vector(wrist)).normalized() * 0.05
    cuff_end = Vector(wrist) - (Vector(elbow) - Vector(wrist)).normalized() * 0.08
    cylinder_between("CuffNavy." + side, cuff_start, cuff_end, 0.152, MAT["navy"], prefix + "LowerArm")
    cuff2_start = cuff_start + (Vector(elbow) - Vector(wrist)).normalized() * 0.025
    cuff2_end = cuff_start + (Vector(wrist) - Vector(elbow)).normalized() * 0.020
    cylinder_between("CuffWhite." + side, cuff2_start, cuff2_end, 0.156, MAT["white"], prefix + "LowerArm")
    hand_center = (wrist[0] + (0.018 if side == "L" else -0.018), -0.010, 1.405)
    uv("Palm." + side, hand_center, (0.105, 0.073, 0.145), MAT["skin"], prefix + "Hand", 20, 12)
    for finger_index in range(4):
        finger_x = hand_center[0] + ((finger_index - 1.5) * 0.032)
        cylinder_between(
            "Finger.%s.%d" % (side, finger_index),
            (finger_x, -0.022, 1.35),
            (finger_x, -0.030, 1.22 + abs(finger_index - 1.5) * 0.010),
            0.014,
            MAT["skin"],
            prefix + "Hand",
            vertices=12,
        )
    thumb_sign = 1.0 if side == "L" else -1.0
    cylinder_between("Thumb." + side, (hand_center[0] - 0.065 * thumb_sign, -0.025, 1.40), (hand_center[0] - 0.105 * thumb_sign, -0.035, 1.31), 0.020, MAT["skin"], prefix + "Hand", 12)


# Jeans and cuffs: both complete legs are modeled independently from hip to ankle.
rounded_box("JeansHips", (0.0, 0.0, 1.48), (0.66, 0.38, 0.36), MAT["denim"], "Hips", 0.12)
leg_data = {
    "L": (0.19, "Left"),
    "R": (-0.19, "Right"),
}
for side, (x, prefix) in leg_data.items():
    cylinder_between("JeansUpper." + side, (x, 0.0, 1.51), (x, 0.0, 1.03), 0.205, MAT["denim"], prefix + "UpperLeg", 24)
    uv("JeansKnee." + side, (x, 0.0, 1.02), (0.205, 0.190, 0.200), MAT["denim"], prefix + "LowerLeg", 20, 12)
    cylinder_between("JeansLower." + side, (x, 0.0, 1.02), (x, 0.0, 0.43), 0.185, MAT["denim"], prefix + "LowerLeg", 24)
    cylinder_between("JeansCuff." + side, (x, 0.0, 0.50), (x, 0.0, 0.37), 0.195, MAT["denim_light"], prefix + "LowerLeg", 24)

    rounded_box("Sole." + side, (x, -0.075, 0.070), (0.35, 0.55, 0.12), MAT["sole"], prefix + "Foot", 0.060)
    rounded_box("ShoeUpper." + side, (x, -0.090, 0.205), (0.33, 0.48, 0.22), MAT["white_trim"], prefix + "Foot", 0.080)
    rounded_box("ShoeTongue." + side, (x, -0.292, 0.245), (0.15, 0.055, 0.19), MAT["navy"], prefix + "Foot", 0.025)
    for lace_index in range(3):
        z = 0.205 + lace_index * 0.045
        rounded_box("Lace.%s.%d" % (side, lace_index), (x, -0.326, z), (0.22, 0.025, 0.018), MAT["white_trim"], prefix + "Foot", 0.008)
    stripe_x = x + (0.174 if side == "L" else -0.174)
    rounded_box("ShoeNavySide." + side, (stripe_x, -0.08, 0.205), (0.025, 0.25, 0.100), MAT["navy"], prefix + "Foot", 0.008)
    rounded_box("ShoeRedSide." + side, (stripe_x + (0.004 if side == "L" else -0.004), -0.03, 0.205), (0.028, 0.075, 0.100), MAT["red"], prefix + "Foot", 0.008)


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
camera_data.ortho_scale = 3.65
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
    scene.render.filepath = os.path.join(OUTPUT, "player-v6-blender-%s-v1.png" % name)
    bpy.ops.render.render(write_still=True)


# Export only the complete body and Humanoid rig.
bpy.ops.object.select_all(action="DESELECT")
BODY.select_set(True)
RIG.select_set(True)
bpy.context.view_layer.objects.active = RIG
fbx_path = os.path.join(OUTPUT, "player-v6-blender-humanoid-v1.fbx")
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

blend_path = os.path.join(OUTPUT, "player-v6-blender-identity-v1.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

world_bounds = []
for corner in BODY.bound_box:
    world_bounds.append(BODY.matrix_world @ Vector(corner))
mins = [min(point[index] for point in world_bounds) for index in range(3)]
maxs = [max(point[index] for point in world_bounds) for index in range(3)]
receipt = {
    "contract": "FC-PLAYER-V6-BLENDER-IDENTITY-V1",
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
        "renders": [os.path.join(OUTPUT, "player-v6-blender-%s-v1.png" % name) for name in views],
    },
    "productionEligible": False,
}
with open(os.path.join(OUTPUT, "build-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print(json.dumps(receipt, ensure_ascii=False, indent=2))
