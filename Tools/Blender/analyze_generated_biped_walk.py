"""Measure an untouched generated humanoid walk action in Blender background mode."""

from __future__ import annotations

import argparse
import json
import math
import os
import statistics
import sys

import bpy
from mathutils import Vector


TRACKED_BONES = (
    "Hips",
    "Spine",
    "Spine01",
    "Spine02",
    "neck",
    "Head",
    "LeftShoulder",
    "LeftArm",
    "LeftForeArm",
    "LeftHand",
    "RightShoulder",
    "RightArm",
    "RightForeArm",
    "RightHand",
    "LeftUpLeg",
    "LeftLeg",
    "LeftFoot",
    "LeftToeBase",
    "RightUpLeg",
    "RightLeg",
    "RightFoot",
    "RightToeBase",
)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(argv)


def correlation(left, right):
    left_mean = statistics.fmean(left)
    right_mean = statistics.fmean(right)
    numerator = sum(
        (left_value - left_mean) * (right_value - right_mean)
        for left_value, right_value in zip(left, right)
    )
    left_power = sum((value - left_mean) ** 2 for value in left)
    right_power = sum((value - right_mean) ** 2 for value in right)
    denominator = math.sqrt(left_power * right_power)
    return numerator / denominator if denominator > 1.0e-12 else None


def angle_degrees(left, right):
    denominator = max(left.length * right.length, 1.0e-12)
    cosine = max(-1.0, min(1.0, left.dot(right) / denominator))
    return math.degrees(math.acos(cosine))


args = parse_args()
glb_path = os.path.abspath(args.glb)
receipt_path = os.path.abspath(args.receipt)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
scene.render.fps_base = 1.0
bpy.ops.import_scene.gltf(filepath=glb_path)
armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
if len(armatures) != 1:
    raise RuntimeError(f"Expected one armature, found {len(armatures)}")
armature = armatures[0]
actions = list(bpy.data.actions)
if len(actions) != 1:
    raise RuntimeError(f"Expected one action, found {[action.name for action in actions]}")
action = actions[0]
if armature.animation_data is None:
    armature.animation_data_create()
armature.animation_data.action = action

missing = [name for name in TRACKED_BONES if name not in armature.pose.bones]
if missing:
    raise RuntimeError(f"Missing tracked bones: {missing}")

frame_start = int(math.floor(action.frame_range[0]))
frame_end = int(math.ceil(action.frame_range[1]))
frames = list(range(frame_start, frame_end + 1))
fps = float(scene.render.fps) / float(scene.render.fps_base)
samples = []
for frame in frames:
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    positions = {
        name: armature.matrix_world @ armature.pose.bones[name].head
        for name in TRACKED_BONES
    }
    hips = positions["Hips"]
    samples.append(
        {
            "frame": frame,
            "world": positions,
            "relative": {name: value - hips for name, value in positions.items()},
        }
    )


def recurrence_error(period):
    squared = []
    maximum = 0.0
    for index in range(len(samples) - period):
        for name in TRACKED_BONES:
            delta = samples[index]["relative"][name] - samples[index + period]["relative"][name]
            squared.append(delta.length_squared)
            maximum = max(maximum, delta.length)
    return math.sqrt(statistics.fmean(squared)), maximum


minimum_period = max(2, int(round(fps * 0.55)))
maximum_period = min(len(samples) // 2, int(round(fps * 2.2)))
period_errors = []
for period in range(minimum_period, maximum_period + 1):
    rms, maximum = recurrence_error(period)
    period_errors.append({"period": period, "rms": rms, "maximum": maximum})
period_errors.sort(key=lambda item: (item["rms"], item["period"]))
best_period = period_errors[0]["period"]

foot_relative = {
    side: [sample["relative"][f"{side}Foot"] for sample in samples]
    for side in ("Left", "Right")
}
axis_variance = []
for axis in (0, 1):
    values = [value[axis] for side in foot_relative.values() for value in side]
    axis_variance.append(statistics.pvariance(values))
forward_axis = 0 if axis_variance[0] >= axis_variance[1] else 1
side_axis = 1 - forward_axis

left_foot_forward = [value[forward_axis] for value in foot_relative["Left"]]
right_foot_forward = [value[forward_axis] for value in foot_relative["Right"]]
left_hand_forward = [
    sample["relative"]["LeftHand"][forward_axis] for sample in samples
]
right_hand_forward = [
    sample["relative"]["RightHand"][forward_axis] for sample in samples
]

torso_lean = []
for sample in samples:
    torso = sample["world"]["Spine02"] - sample["world"]["Hips"]
    torso_lean.append(angle_degrees(torso, Vector((0.0, 0.0, 1.0))))

foot_heights = {
    side: [sample["world"][f"{side}Foot"].z for sample in samples]
    for side in ("Left", "Right")
}
contact_frames = {}
for side, values in foot_heights.items():
    low = min(values)
    high = max(values)
    threshold = low + (high - low) * 0.08
    contact_frames[side] = [
        frame for frame, value in zip(frames, values) if value <= threshold
    ]

receipt = {
    "contract": "FC-GENERATED-BIPED-WALK-MEASUREMENT-V1",
    "glb": glb_path,
    "action": action.name,
    "frameStart": frame_start,
    "frameEnd": frame_end,
    "frameCount": len(frames),
    "fps": fps,
    "bestRepeatedPeriodFrames": best_period,
    "bestRepeatedPeriodSeconds": best_period / fps,
    "bestRepeatedPeriodRms": period_errors[0]["rms"],
    "bestRepeatedPeriodMaximum": period_errors[0]["maximum"],
    "nextBestPeriodCandidates": period_errors[:10],
    "forwardAxisIndex": forward_axis,
    "sideAxisIndex": side_axis,
    "footForwardCorrelation": correlation(left_foot_forward, right_foot_forward),
    "handForwardCorrelation": correlation(left_hand_forward, right_hand_forward),
    "leftFootForwardRange": max(left_foot_forward) - min(left_foot_forward),
    "rightFootForwardRange": max(right_foot_forward) - min(right_foot_forward),
    "leftHandForwardRange": max(left_hand_forward) - min(left_hand_forward),
    "rightHandForwardRange": max(right_hand_forward) - min(right_hand_forward),
    "leftFootHeightRange": max(foot_heights["Left"]) - min(foot_heights["Left"]),
    "rightFootHeightRange": max(foot_heights["Right"]) - min(foot_heights["Right"]),
    "contactFrames": contact_frames,
    "torsoLeanDegrees": {
        "minimum": min(torso_lean),
        "maximum": max(torso_lean),
        "mean": statistics.fmean(torso_lean),
    },
    "visualFullCycleStillRequired": True,
    "productionEligible": False,
}
os.makedirs(os.path.dirname(receipt_path), exist_ok=True)
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
    handle.write("\n")

print(json.dumps(receipt, ensure_ascii=False, indent=2))
