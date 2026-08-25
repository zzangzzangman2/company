"""Read-only component audit for MotherTextureFirst3AdultCleanGate.

Imports the user-owned Mika FBX, changes only temporary review materials, and
renders whole-component hair/skirt candidates before the production builder is
allowed to hide anything.  No source file, Unity asset or character geometry is
written or edited.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from collections import defaultdict, deque
from pathlib import Path

import bpy
from mathutils import Vector


def args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


ARGS = args()
REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / "Artifacts/ExternalReferenceStudy/UserProvided_BlueArchive_OriginalMeshes_2026-08-24/Mika/CH0069_Mesh/CH0069_Mesh.fbx"
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)


def components(mesh, material_index):
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
        found = {seed}
        while todo:
            polygon = polygon_map[todo.popleft()]
            for vertex in polygon.vertices:
                for neighbor in by_vertex[vertex]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        found.add(neighbor)
                        todo.append(neighbor)
        result.append(sorted(found))
    return sorted(result, key=len, reverse=True)


def component_vertices(mesh, polygon_indices):
    return sorted({vertex for polygon in polygon_indices for vertex in mesh.polygons[polygon].vertices})


def material(name, color, transparent=False):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    nodes = result.node_tree.nodes
    links = result.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    if transparent:
        shader = nodes.new("ShaderNodeBsdfTransparent")
        links.new(shader.outputs["BSDF"], output.inputs["Surface"])
        if hasattr(result, "surface_render_method"):
            result.surface_render_method = "DITHERED"
        result.diffuse_color = (0, 0, 0, 0)
    else:
        shader = nodes.new("ShaderNodeBsdfPrincipled")
        shader.inputs["Base Color"].default_value = (*color, 1.0)
        shader.inputs["Roughness"].default_value = 0.76
        links.new(shader.outputs["BSDF"], output.inputs["Surface"])
        result.diffuse_color = (*color, 1.0)
    return result


def bounds(obj, vertex_indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in vertex_indices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return {
        "min": [round(float(value), 6) for value in lo],
        "max": [round(float(value), 6) for value in hi],
        "dimensions": [round(float(value), 6) for value in (hi - lo)],
        "center": [round(float(value), 6) for value in ((lo + hi) * 0.5)],
    }


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE))
armature = bpy.data.objects["Armature"]
body = bpy.data.objects["CH0069_Body"]
armature.scale = (1.8, 1.8, 1.8)
bpy.context.view_layer.update()
weapon = bpy.data.objects.get("CH0069_Weapon")
if weapon:
    bpy.data.objects.remove(weapon, do_unlink=True)


def slot(prefix):
    return next(index for index, value in enumerate(body.data.materials) if value and value.name.startswith(prefix))


body_slot = slot("CH0069_Body")
hair_slot = slot("CH0069_Hair")
hair_components = components(body.data, hair_slot)
body_components = components(body.data, body_slot)


def group_fractions(vertex_indices):
    scores = defaultdict(float)
    for vertex_index in vertex_indices:
        for membership in body.data.vertices[vertex_index].groups:
            name = body.vertex_groups[membership.group].name.lower()
            weight = float(membership.weight)
            if "bone_wing" in name:
                scores["wing"] += weight
            if "bone_cape" in name:
                scores["cape"] += weight
            if "bone_ribbon" in name:
                scores["ribbon"] += weight
            if "bone_skirt" in name:
                scores["skirt"] += weight
            scores["total"] += weight
    total = max(scores["total"], 1e-9)
    return {key: scores[key] / total for key in scores if key != "total"}


hair_records = []
for index, item in enumerate(hair_components):
    vertices = component_vertices(body.data, item)
    record = {"id": index, "polygons": len(item), "vertices": len(vertices), **bounds(body, vertices)}
    hair_records.append(record)

body_records = []
for index, item in enumerate(body_components):
    vertices = component_vertices(body.data, item)
    body_records.append({
        "id": index,
        "polygons": len(item),
        "vertices": len(vertices),
        "fractions": {key: round(value, 6) for key, value in group_fractions(vertices).items()},
        **bounds(body, vertices),
    })

gray = material("AdultCleanAudit_Gray", (0.50, 0.53, 0.57))
hair_mat = material("AdultCleanAudit_Hair", (0.16, 0.055, 0.028))
teal = material("AdultCleanAudit_Teal", (0.01, 0.10, 0.12))
hidden = material("AdultCleanAudit_Hidden", (0, 0, 0), transparent=True)
slots = {}
for name, mat in (("gray", gray), ("hair", hair_mat), ("teal", teal), ("hidden", hidden)):
    slots[name] = len(body.data.materials)
    body.data.materials.append(mat)

hair_by_polygon = {polygon: index for index, item in enumerate(hair_components) for polygon in item}
body_by_polygon = {polygon: index for index, item in enumerate(body_components) for polygon in item}
dominant_skirt = {
    record["id"] for record in body_records if record["fractions"].get("skirt", 0.0) > 0.10
}
fantasy_body = {
    record["id"] for record in body_records
    if record["fractions"].get("wing", 0.0) > 0.08
    or record["fractions"].get("cape", 0.0) > 0.30
    or record["fractions"].get("ribbon", 0.0) > 0.24
}

hair_safe_keep = {0, 1, 3, 4, 16, 17, 21, 23}
hair_safe_hide = set(range(len(hair_components))) - hair_safe_keep
variants = {
    "hair-all": {"hairKeep": set(range(len(hair_components))), "skirtKeep": dominant_skirt},
    "hair-safe-medium-attempt": {"hairKeep": hair_safe_keep, "skirtKeep": dominant_skirt},
    "hair-aggressive-without-scalp0": {"hairKeep": hair_safe_keep - {0}, "skirtKeep": dominant_skirt},
    "skirt-45-only": {"hairKeep": set(range(len(hair_components))), "skirtKeep": {4, 5}},
    "skirt-1215-only": {"hairKeep": set(range(len(hair_components))), "skirtKeep": {12, 13, 14, 15}},
    "skirt-45-1215": {"hairKeep": set(range(len(hair_components))), "skirtKeep": {4, 5, 12, 13, 14, 15}},
    "skirt-1015": {"hairKeep": set(range(len(hair_components))), "skirtKeep": {10, 11, 12, 13, 14, 15}},
}

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 760
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
if scene.world is None:
    scene.world = bpy.data.worlds.new("AdultCleanAuditWorld")
scene.world.color = (0.012, 0.016, 0.023)
for name, location, energy, size in (
    ("Key", (-3.2, -4.0, 4.0), 850, 3.0),
    ("Fill", (3.2, -2.0, 2.7), 520, 3.0),
    ("Rim", (0, 4.0, 3.0), 680, 2.8),
):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.size = size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0, 0, 0.9)) - obj.location).to_track_quat("-Z", "Y").to_euler()
camera_data = bpy.data.cameras.new("AuditCameraData")
camera = bpy.data.objects.new("AuditCamera", camera_data)
scene.collection.objects.link(camera)
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.02
scene.camera = camera
views = {
    "front": ((0, -4.1, .94), (0, 0, .87)),
    "three-quarter": ((2.8, -3.35, 1.0), (0, 0, .88)),
    "side": ((4.1, 0, .96), (0, 0, .88)),
    "back": ((0, 4.1, .94), (0, 0, .87)),
}


def apply_variant(spec):
    for polygon in body.data.polygons:
        if polygon.index in hair_by_polygon:
            polygon.material_index = slots["hair"] if hair_by_polygon[polygon.index] in spec["hairKeep"] else slots["hidden"]
        elif polygon.index in body_by_polygon:
            component_id = body_by_polygon[polygon.index]
            if component_id in fantasy_body or (component_id in dominant_skirt and component_id not in spec["skirtKeep"]):
                polygon.material_index = slots["hidden"]
            elif component_id in dominant_skirt:
                polygon.material_index = slots["teal"]
            else:
                polygon.material_index = slots["gray"]
        else:
            polygon.material_index = slots["gray"]
        polygon.use_smooth = True


rendered = {}
for variant, spec in variants.items():
    apply_variant(spec)
    rendered[variant] = []
    for view, (location, target) in views.items():
        camera.location = location
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = OUTPUT / f"mother-adult-clean-audit-{variant}-{view}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        rendered[variant].append(str(path))

receipt = {
    "schema": "family-company.mother-adult-clean-component-audit.v1",
    "source": str(SOURCE),
    "test3OrSakurakoUsed": False,
    "sourceModified": False,
    "unityModified": False,
    "newCharacterGeometry": 0,
    "hairComponents": hair_records,
    "bodyComponents": body_records,
    "dominantSkirtComponents": sorted(dominant_skirt),
    "fantasyWeightedBodyComponents": sorted(fantasy_body),
    "candidateRules": {
        "hairSafeKeep": sorted(hair_safe_keep),
        "hairSafeHide": sorted(hair_safe_hide),
        "hairComponent0": "must remain unless back/crown coverage is visually proven by the aggressive audit; it owns crown plus connected waist-length rear mass",
        "skirtVariants": {name: sorted(spec["skirtKeep"]) for name, spec in variants.items() if name.startswith("skirt-")},
    },
    "renders": rendered,
}
(OUTPUT / "mother-adult-clean-component-audit.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(OUTPUT / "mother-adult-clean-component-audit.json")
