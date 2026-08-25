"""Read-only surface audit for the user-provided Yuuka and Mika FBX donors.

The script imports only the two explicitly allow-listed FBX files, measures
mesh topology/skinning, and writes JSON/Markdown reports.  It never saves a
Blender file and never opens the excluded test3/Sakurako material.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repo",
        default=r"C:\Users\godho\Documents\Codex\family_company_unity",
    )
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DPlayerHumanV5/Proof9/Audit",
    )
    return parser.parse_args(argv)


ARGS = parse_args()
REPO = Path(ARGS.repo).resolve()
OUTPUT = (REPO / ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)

DONOR_ROOT = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
)
SOURCES = {
    "Yuuka": DONOR_ROOT / "Yuuka" / "Yuuka_Original_Mesh" / "Yuuka_Original_Mesh.fbx",
    "Mika": DONOR_ROOT / "Mika" / "CH0069_Mesh" / "CH0069_Mesh.fbx",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def safe_source(path: Path) -> Path:
    resolved = path.resolve(strict=True)
    lowered = str(resolved).lower().replace("\\", "/")
    if "test3" in lowered or "sakurako" in lowered:
        raise RuntimeError(f"Forbidden source rejected: {resolved}")
    if resolved not in {item.resolve(strict=True) for item in SOURCES.values()}:
        raise RuntimeError(f"Source is not allow-listed: {resolved}")
    return resolved


def rounded(value: float, places: int = 6) -> float:
    return round(float(value), places)


def vec3(value: Vector) -> list[float]:
    return [rounded(value.x), rounded(value.y), rounded(value.z)]


def bbox_record(points: list[Vector]) -> dict:
    if not points:
        return {
            "min": [0.0, 0.0, 0.0],
            "max": [0.0, 0.0, 0.0],
            "center": [0.0, 0.0, 0.0],
            "dimensions": [0.0, 0.0, 0.0],
        }
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return {
        "min": vec3(minimum),
        "max": vec3(maximum),
        "center": vec3((minimum + maximum) * 0.5),
        "dimensions": vec3(maximum - minimum),
    }


class UnionFind:
    def __init__(self, count: int) -> None:
        self.parent = list(range(count))
        self.rank = [0] * count

    def find(self, item: int) -> int:
        root = item
        while self.parent[root] != root:
            root = self.parent[root]
        while self.parent[item] != item:
            parent = self.parent[item]
            self.parent[item] = root
            item = parent
        return root

    def union(self, left: int, right: int) -> None:
        a = self.find(left)
        b = self.find(right)
        if a == b:
            return
        if self.rank[a] < self.rank[b]:
            a, b = b, a
        self.parent[b] = a
        if self.rank[a] == self.rank[b]:
            self.rank[a] += 1


def material_record(material) -> dict:
    images = []
    if material and material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            image = getattr(node, "image", None)
            if image is not None:
                images.append(
                    {
                        "name": image.name,
                        "filepath": bpy.path.abspath(image.filepath) if image.filepath else "",
                    }
                )
    return {
        "name": material.name if material else None,
        "useNodes": bool(material and material.use_nodes),
        "blendMethod": getattr(material, "surface_render_method", None) if material else None,
        "images": images,
    }


def infer_role(
    object_name: str,
    material_names: list[str],
    top_bones: list[str],
    bounds: dict,
    character_bounds: dict,
) -> dict:
    name = object_name.lower()
    material_text = " ".join(material_names).lower()
    semantic_text = f"{name} {material_text}"
    bone_text = " ".join(top_bones).lower()
    if "eyemouth" in semantic_text or "eyebrow" in semantic_text or ("eye" in semantic_text and "mouth" in semantic_text):
        return {"role": "face_feature_decal", "confidence": "high", "basis": "dominant material/object name"}
    if "face" in material_text or "face" in name:
        return {"role": "face_skin", "confidence": "high", "basis": "dominant material/object name"}
    if "hair" in material_text or "hair" in name:
        return {"role": "hair", "confidence": "high", "basis": "dominant material/object name"}
    if "weapon" in semantic_text:
        return {"role": "weapon_accessory", "confidence": "high", "basis": "dominant material/object name"}
    if "halo" in semantic_text:
        return {"role": "halo_accessory", "confidence": "high", "basis": "dominant material/object name"}
    if "calculator" in semantic_text:
        return {"role": "calculator_accessory", "confidence": "high", "basis": "dominant material/object name"}

    if "body" in semantic_text:
        if any(token in bone_text for token in ("wing", "cape", "ribbon")):
            return {
                "role": "cape_wing_or_back_accessory",
                "confidence": "high",
                "basis": "dedicated accessory bone groups",
            }
        if any(token in bone_text for token in ("toe", "foot", "ankle")) and not any(
            token in bone_text for token in ("spine", "chest", "neck", "arm")
        ):
            role = "shoes_or_feet"
        elif any(token in bone_text for token in ("finger", "hand")) and not any(
            token in bone_text for token in ("spine", "leg", "thigh")
        ):
            role = "hands"
        elif any(token in bone_text for token in ("leg", "thigh", "knee", "calf")) and not any(
            token in bone_text for token in ("spine", "chest", "neck")
        ):
            role = "lower_body_or_pants"
        elif "skirt" in bone_text and not any(token in bone_text for token in ("spine", "arm", "shoulder")):
            role = "skirt_or_lower_clothes"
        elif any(token in bone_text for token in ("spine", "chest", "neck", "arm", "shoulder")):
            role = "torso_clothes_or_body"
        else:
            role = "body_clothes_composite"
        return {"role": role, "confidence": "medium", "basis": "body object and dominant bone groups"}

    char_min_z = character_bounds["min"][2]
    char_height = max(character_bounds["dimensions"][2], 1e-9)
    relative_z = (bounds["center"][2] - char_min_z) / char_height
    if relative_z > 0.75:
        return {"role": "head_accessory_or_hair", "confidence": "low", "basis": "vertical placement"}
    if relative_z < 0.25:
        return {"role": "feet_or_lower_accessory", "confidence": "low", "basis": "vertical placement"}
    return {"role": "unknown_mesh_surface", "confidence": "low", "basis": "no semantic object-name match"}


def analyze_mesh_object(obj, character_bounds: dict) -> dict:
    mesh = obj.data
    uf = UnionFind(len(mesh.vertices))
    for edge in mesh.edges:
        uf.union(edge.vertices[0], edge.vertices[1])

    component_vertices: dict[int, list[int]] = defaultdict(list)
    for vertex in mesh.vertices:
        component_vertices[uf.find(vertex.index)].append(vertex.index)
    ordered_roots = sorted(component_vertices, key=lambda root: min(component_vertices[root]))
    root_to_component = {root: index for index, root in enumerate(ordered_roots)}
    vertex_component = {vertex_index: root_to_component[root] for root, items in component_vertices.items() for vertex_index in items}

    component_edges: dict[int, list[int]] = defaultdict(list)
    for edge in mesh.edges:
        component_edges[vertex_component[edge.vertices[0]]].append(edge.index)
    component_polygons: dict[int, list[int]] = defaultdict(list)
    for polygon in mesh.polygons:
        if not polygon.vertices:
            continue
        component_polygons[vertex_component[polygon.vertices[0]]].append(polygon.index)

    group_names = {group.index: group.name for group in obj.vertex_groups}
    armature_modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
    armature_objects = [modifier.object for modifier in armature_modifiers if modifier.object]
    armature_bones = {
        bone.name
        for armature in armature_objects
        for bone in getattr(armature.data, "bones", [])
    }
    world_points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]

    object_weight_total: dict[str, float] = defaultdict(float)
    object_weight_vertices: dict[str, int] = defaultdict(int)
    object_weight_max: dict[str, float] = defaultdict(float)
    object_bone_influence_counts = []
    object_weighted_vertices = 0
    object_bone_weighted_vertices = 0
    for vertex in mesh.vertices:
        has_weight = False
        bone_count = 0
        for membership in vertex.groups:
            if membership.weight <= 1e-8:
                continue
            has_weight = True
            group_name = group_names.get(membership.group, f"group_{membership.group}")
            object_weight_total[group_name] += membership.weight
            object_weight_vertices[group_name] += 1
            object_weight_max[group_name] = max(object_weight_max[group_name], membership.weight)
            if group_name in armature_bones:
                bone_count += 1
        if has_weight:
            object_weighted_vertices += 1
        if bone_count:
            object_bone_weighted_vertices += 1
        object_bone_influence_counts.append(bone_count)

    vertex_group_stats = []
    for group in sorted(obj.vertex_groups, key=lambda item: item.index):
        total = object_weight_total.get(group.name, 0.0)
        count = object_weight_vertices.get(group.name, 0)
        vertex_group_stats.append(
            {
                "index": group.index,
                "name": group.name,
                "matchesArmatureBone": group.name in armature_bones,
                "influencedVertices": count,
                "totalWeight": rounded(total),
                "meanWeightOnInfluencedVertices": rounded(total / count) if count else 0.0,
                "maxWeight": rounded(object_weight_max.get(group.name, 0.0)),
            }
        )

    components = []
    for component_index, root in enumerate(ordered_roots):
        vertex_indices = component_vertices[root]
        edge_indices = component_edges.get(component_index, [])
        polygon_indices = component_polygons.get(component_index, [])
        points = [world_points[index] for index in vertex_indices]
        bounds = bbox_record(points)

        material_polygons: dict[str, int] = defaultdict(int)
        triangle_count = 0
        for polygon_index in polygon_indices:
            polygon = mesh.polygons[polygon_index]
            material_name = None
            if polygon.material_index < len(obj.material_slots):
                material = obj.material_slots[polygon.material_index].material
                material_name = material.name if material else None
            material_polygons[material_name or "<none>"] += 1
            triangle_count += max(0, len(polygon.vertices) - 2)

        weight_total: dict[str, float] = defaultdict(float)
        weight_vertices: dict[str, int] = defaultdict(int)
        weight_max: dict[str, float] = defaultdict(float)
        bone_influence_counts = []
        weighted_vertices = 0
        bone_weighted_vertices = 0
        for vertex_index in vertex_indices:
            vertex = mesh.vertices[vertex_index]
            vertex_has_weight = False
            vertex_bone_count = 0
            for membership in vertex.groups:
                group_name = group_names.get(membership.group, f"group_{membership.group}")
                if membership.weight <= 1e-8:
                    continue
                vertex_has_weight = True
                weight_total[group_name] += membership.weight
                weight_vertices[group_name] += 1
                weight_max[group_name] = max(weight_max[group_name], membership.weight)
                if group_name in armature_bones:
                    vertex_bone_count += 1
            if vertex_has_weight:
                weighted_vertices += 1
            if vertex_bone_count:
                bone_weighted_vertices += 1
            bone_influence_counts.append(vertex_bone_count)

        top_groups = []
        for name, total in sorted(weight_total.items(), key=lambda item: (-item[1], item[0]))[:16]:
            count = weight_vertices[name]
            top_groups.append(
                {
                    "name": name,
                    "totalWeight": rounded(total),
                    "influencedVertices": count,
                    "meanWeightOnInfluencedVertices": rounded(total / count) if count else 0.0,
                    "maxWeight": rounded(weight_max[name]),
                    "matchesArmatureBone": name in armature_bones,
                }
            )
        top_bones = [item["name"] for item in top_groups if item["matchesArmatureBone"]]

        materials_by_polygon = [
            {"name": name, "polygons": count}
            for name, count in sorted(material_polygons.items(), key=lambda item: (-item[1], item[0]))
        ]
        components.append(
            {
                "id": f"{obj.name}/c{component_index:03d}",
                "minimumVertexIndex": min(vertex_indices),
                "counts": {
                    "vertices": len(vertex_indices),
                    "edges": len(edge_indices),
                    "polygons": len(polygon_indices),
                    "triangles": triangle_count,
                },
                "worldBounds": bounds,
                "materialsByPolygon": materials_by_polygon,
                "skinning": {
                    "weightedVertices": weighted_vertices,
                    "unweightedVertices": len(vertex_indices) - weighted_vertices,
                    "armatureBoneWeightedVertices": bone_weighted_vertices,
                    "meanArmatureInfluencesPerVertex": rounded(sum(bone_influence_counts) / len(bone_influence_counts))
                    if bone_influence_counts
                    else 0.0,
                    "maxArmatureInfluencesPerVertex": max(bone_influence_counts, default=0),
                    "topVertexGroups": top_groups,
                },
                "semantic": infer_role(
                    obj.name,
                    [item["name"] for item in materials_by_polygon],
                    top_bones,
                    bounds,
                    character_bounds,
                ),
            }
        )

    material_polygon_counts: dict[str, int] = defaultdict(int)
    for polygon in mesh.polygons:
        material_name = "<none>"
        if polygon.material_index < len(obj.material_slots):
            material = obj.material_slots[polygon.material_index].material
            material_name = material.name if material else "<none>"
        material_polygon_counts[material_name] += 1

    component_polygon_histogram = {
        "0": sum(1 for item in components if item["counts"]["polygons"] == 0),
        "1-4": sum(1 for item in components if 1 <= item["counts"]["polygons"] <= 4),
        "5-16": sum(1 for item in components if 5 <= item["counts"]["polygons"] <= 16),
        "17-64": sum(1 for item in components if 17 <= item["counts"]["polygons"] <= 64),
        "65-255": sum(1 for item in components if 65 <= item["counts"]["polygons"] <= 255),
        "256+": sum(1 for item in components if item["counts"]["polygons"] >= 256),
    }

    return {
        "name": obj.name,
        "dataName": mesh.name,
        "counts": {
            "vertices": len(mesh.vertices),
            "edges": len(mesh.edges),
            "polygons": len(mesh.polygons),
            "triangles": sum(max(0, len(polygon.vertices) - 2) for polygon in mesh.polygons),
            "disconnectedComponents": len(components),
        },
        "worldBounds": bbox_record(world_points),
        "objectTransform": {
            "location": vec3(obj.location),
            "rotationEulerRadians": vec3(obj.rotation_euler),
            "scale": vec3(obj.scale),
        },
        "materialSlots": [material_record(slot.material) for slot in obj.material_slots],
        "materialsByPolygon": [
            {"name": name, "polygons": count}
            for name, count in sorted(material_polygon_counts.items(), key=lambda item: (-item[1], item[0]))
        ],
        "vertexGroups": len(obj.vertex_groups),
        "vertexGroupStats": vertex_group_stats,
        "boneInfluenceSummary": {
            "weightedVertices": object_weighted_vertices,
            "unweightedVertices": len(mesh.vertices) - object_weighted_vertices,
            "armatureBoneWeightedVertices": object_bone_weighted_vertices,
            "meanArmatureInfluencesPerVertex": rounded(sum(object_bone_influence_counts) / len(object_bone_influence_counts))
            if object_bone_influence_counts
            else 0.0,
            "maxArmatureInfluencesPerVertex": max(object_bone_influence_counts, default=0),
        },
        "componentPolygonHistogram": component_polygon_histogram,
        "armatureModifiers": [
            {"name": modifier.name, "armatureObject": modifier.object.name if modifier.object else None}
            for modifier in armature_modifiers
        ],
        "uvLayers": [layer.name for layer in mesh.uv_layers],
        "shapeKeys": [block.name for block in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
        "components": components,
    }


def analyze_donor(label: str, source_path: Path) -> dict:
    source_path = safe_source(source_path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source_path), use_anim=False)

    mesh_objects = sorted((obj for obj in bpy.data.objects if obj.type == "MESH"), key=lambda obj: obj.name)
    armature_objects = sorted((obj for obj in bpy.data.objects if obj.type == "ARMATURE"), key=lambda obj: obj.name)
    all_world_points = [obj.matrix_world @ vertex.co for obj in mesh_objects for vertex in obj.data.vertices]
    character_bounds = bbox_record(all_world_points)
    meshes = [analyze_mesh_object(obj, character_bounds) for obj in mesh_objects]
    return {
        "donor": label,
        "source": {
            "path": str(source_path),
            "bytes": source_path.stat().st_size,
            "sha256": sha256(source_path),
        },
        "characterWorldBounds": character_bounds,
        "counts": {
            "meshObjects": len(mesh_objects),
            "armatureObjects": len(armature_objects),
            "armatureBones": sum(len(obj.data.bones) for obj in armature_objects),
            "vertices": sum(item["counts"]["vertices"] for item in meshes),
            "edges": sum(item["counts"]["edges"] for item in meshes),
            "polygons": sum(item["counts"]["polygons"] for item in meshes),
            "triangles": sum(item["counts"]["triangles"] for item in meshes),
            "disconnectedComponents": sum(item["counts"]["disconnectedComponents"] for item in meshes),
        },
        "armatures": [
            {
                "name": obj.name,
                "bones": len(obj.data.bones),
                "boneNames": sorted(bone.name for bone in obj.data.bones),
            }
            for obj in armature_objects
        ],
        "meshes": meshes,
    }


def candidate_score(component: dict, target: str, donor: str) -> tuple[int, list[str]]:
    role = component["semantic"]["role"]
    bone_names = " ".join(
        group["name"].lower()
        for group in component["skinning"]["topVertexGroups"]
        if group["matchesArmatureBone"]
    )
    polygons = component["counts"]["polygons"]
    reasons = []
    score = 0
    if target in {"hoodie", "shirt"}:
        if role in {"torso_clothes_or_body", "body_clothes_composite"}:
            score += 45
            reasons.append("torso/body surface")
        if role in {"torso_clothes_or_body", "body_clothes_composite"} and any(
            token in bone_names for token in ("spine", "chest", "arm", "shoulder", "clavicle")
        ):
            score += 35
            reasons.append("upper-body bone influence")
    elif target == "pants":
        if role in {"lower_body_or_pants", "body_clothes_composite"}:
            score += 45
            reasons.append("lower-body surface")
        if any(token in bone_names for token in ("leg", "thigh", "knee", "calf", "hips", "pelvis")):
            score += 35
            reasons.append("pelvis/leg bone influence")
    elif target == "shoes":
        if role == "shoes_or_feet":
            score += 60
            reasons.append("foot/shoe surface")
        if any(token in bone_names for token in ("foot", "toe", "ankle")):
            score += 30
            reasons.append("foot/toe bone influence")
    elif target == "cap":
        if role == "hair":
            score += 20
            reasons.append("head-fitted surface, but it is donor hair")
        if role in {"head_accessory_or_hair", "halo_accessory"}:
            score += 10
            reasons.append("head-area accessory, not cap topology")
        if any(token in bone_names for token in ("head", "hair")):
            score += 10
            reasons.append("head/hair bone influence")
    if score and donor == "Yuuka":
        score += 10
        reasons.append("same donor/rig already used by Player Human V5")
    if score and polygons >= 100:
        score += 5
        reasons.append("enough polygons for reshaping")
    return min(score, 100), reasons


def build_reuse_assessment(donors: list[dict]) -> dict:
    targets = {}
    for target in ("hoodie", "shirt", "pants", "shoes", "cap"):
        ranked = []
        for donor in donors:
            for mesh in donor["meshes"]:
                for component in mesh["components"]:
                    score, reasons = candidate_score(component, target, donor["donor"])
                    if score:
                        ranked.append(
                            {
                                "donor": donor["donor"],
                                "component": component["id"],
                                "score": score,
                                "semanticRole": component["semantic"]["role"],
                                "counts": component["counts"],
                                "worldBounds": component["worldBounds"],
                                "reasons": reasons,
                            }
                        )
        ranked.sort(key=lambda item: (-item["score"], -item["counts"]["polygons"], item["donor"], item["component"]))
        top_score = ranked[0]["score"] if ranked else 0
        if target == "cap":
            feasibility = "low"
            conclusion = (
                "No donor contains a native cap surface. Hair/scalp components are useful as a fit envelope only; "
                "directly converting them would sacrifice retained hair topology and is not recommended."
            )
        elif top_score >= 80:
            feasibility = "high"
            conclusion = "Direct reshaping is feasible while retaining donor topology, UVs, armature modifier, and weights."
        elif top_score >= 55:
            feasibility = "medium"
            conclusion = "Direct reshaping is feasible, but semantic regions must be isolated carefully inside a composite body mesh."
        else:
            feasibility = "low"
            conclusion = "No clean, semantically matched donor component was detected for direct reshaping."
        targets[target] = {
            "feasibility": feasibility,
            "conclusion": conclusion,
            "topCandidates": ranked[:8],
        }
    targets["hoodie"]["recommendedAssembly"] = {
        "donor": "Yuuka",
        "components": [
            "Yuuka_Original_Body/c146",
            "Yuuka_Original_Body/c141",
            "Yuuka_Original_Body/c181",
        ],
        "note": "Use the central upper-body shell plus the mirrored skinned arm shells; verify seams against c157 before editing.",
    }
    targets["shirt"]["recommendedAssembly"] = {
        "donor": "Yuuka",
        "components": ["Yuuka_Original_Body/c157"],
        "note": "Best continuous torso-region candidate by bounds and Spine/Clavicle influence; isolate from skin by material/UV inspection before deformation.",
    }
    targets["pants"]["recommendedAssembly"] = {
        "donor": "Yuuka",
        "components": [
            "Yuuka_Original_Body/c063",
            "Yuuka_Original_Body/c099",
            "Yuuka_Original_Body/c218",
        ],
        "note": "Mirrored thigh/calf shells plus pelvis bridge preserve the existing Player rig; Mika c403/c414 are denser cross-donor alternatives.",
    }
    targets["shoes"]["recommendedAssembly"] = {
        "donor": "Yuuka",
        "components": [
            "Yuuka_Original_Body/c000",
            "Yuuka_Original_Body/c001",
            "Yuuka_Original_Body/c214",
            "Yuuka_Original_Body/c221",
        ],
        "note": "Use mirrored foot shells and sole/toe shells as a set; additional small trim islands remain listed in JSON.",
    }
    targets["cap"]["recommendedAssembly"] = {
        "donor": None,
        "components": [],
        "note": "No native cap component. Retained hair/scalp may be sampled as a non-destructive fit envelope only.",
    }
    return targets


def markdown_report(report: dict) -> str:
    lines = [
        "# Player Human V5 Proof9 donor-surface audit",
        "",
        "This is a read-only topology/skinning audit. Only the allow-listed Yuuka and Mika FBX files were imported. "
        "The excluded test3/Sakurako material was not enumerated, opened, or loaded. No FBX, `.blend`, Proof8, or Unity asset was written.",
        "",
        f"- Generated UTC: `{report['generatedUtc']}`",
        f"- Blender: `{report['blenderVersion']}`",
        f"- JSON schema: `{report['schema']}`",
        "",
        "## Donor totals",
        "",
        "| Donor | FBX SHA256 | Meshes | Bones | Vertices | Polygons | Components | Bounds (X×Y×Z) |",
        "|---|---|---:|---:|---:|---:|---:|---|",
    ]
    for donor in report["donors"]:
        dims = donor["characterWorldBounds"]["dimensions"]
        lines.append(
            f"| {donor['donor']} | `{donor['source']['sha256']}` | {donor['counts']['meshObjects']} | "
            f"{donor['counts']['armatureBones']} | {donor['counts']['vertices']} | {donor['counts']['polygons']} | "
            f"{donor['counts']['disconnectedComponents']} | {dims[0]:.4f}×{dims[1]:.4f}×{dims[2]:.4f} |"
        )

    lines += ["", "## Per-mesh topology", ""]
    for donor in report["donors"]:
        lines += [
            f"### {donor['donor']}",
            "",
            "| Mesh | V | E | P | Tri | Components | Material polygon counts | Weighted / bone-weighted V | Mean / max bone influences |",
            "|---|---:|---:|---:|---:|---:|---|---|---|",
        ]
        for mesh in donor["meshes"]:
            materials = ", ".join(
                f"{item['name']}:{item['polygons']}" for item in mesh["materialsByPolygon"]
            ) or "<none>"
            influence = mesh["boneInfluenceSummary"]
            count = mesh["counts"]
            lines.append(
                f"| `{mesh['name']}` | {count['vertices']} | {count['edges']} | {count['polygons']} | "
                f"{count['triangles']} | {count['disconnectedComponents']} | {materials} | "
                f"{influence['weightedVertices']} / {influence['armatureBoneWeightedVertices']} | "
                f"{influence['meanArmatureInfluencesPerVertex']} / {influence['maxArmatureInfluencesPerVertex']} |"
            )
        lines.append("")

    lines += ["## Disconnected components", ""]
    for donor in report["donors"]:
        lines += [f"### {donor['donor']}", "", "| Component | Role | V | P | Bounds min → max | Top bone groups |", "|---|---|---:|---:|---|---|"]
        for mesh in donor["meshes"]:
            for component in mesh["components"]:
                bounds = component["worldBounds"]
                top_bones = [
                    item["name"]
                    for item in component["skinning"]["topVertexGroups"]
                    if item["matchesArmatureBone"]
                ][:5]
                lines.append(
                    f"| `{donor['donor']}/{component['id']}` | {component['semantic']['role']} | "
                    f"{component['counts']['vertices']} | {component['counts']['polygons']} | "
                    f"{bounds['min']} → {bounds['max']} | {', '.join(top_bones) or 'none'} |"
                )
        lines.append("")

    lines += [
        "## Direct surface-reshaping assessment",
        "",
        "| Player target | Feasibility | Best candidates | Recommended assembly | Judgment |",
        "|---|---|---|---|---|",
    ]
    for target, assessment in report["directSurfaceReshaping"].items():
        candidates = ", ".join(
            f"`{item['donor']}/{item['component']}` ({item['score']})" for item in assessment["topCandidates"][:3]
        ) or "none"
        assembly = assessment.get("recommendedAssembly", {})
        assembly_components = ", ".join(f"`{item}`" for item in assembly.get("components", [])) or "none"
        lines.append(
            f"| {target} | **{assessment['feasibility']}** | {candidates} | {assembly_components} | "
            f"{assessment['conclusion']} {assembly.get('note', '')} |"
        )

    lines += [
        "",
        "## Interpretation constraints",
        "",
        "- A connected component is defined by mesh edges; it is not automatically a semantic garment.",
        "- Bounds are raw Blender world units immediately after FBX import; no Proof8 scale or geometry edit was applied.",
        "- `Body` meshes can contain skin, uniform, socks, and shoes in one skinned object. Bone groups and bounds are evidence, not perfect labels.",
        "- A high score means the source surface can be reshaped directly without replacing topology. It does not mean the donor texture already matches the Player design.",
        "- The cap is intentionally rated low: neither audited donor has native baseball-cap topology. Head/hair surfaces should remain fit references unless a destructive hair conversion is explicitly authorized.",
        "- Full material, component, bounding-box, and per-group weight figures are in the sibling JSON report.",
        "",
    ]
    return "\n".join(lines)


def main() -> None:
    for source in SOURCES.values():
        safe_source(source)
    donors = [analyze_donor(label, path) for label, path in SOURCES.items()]
    report = {
        "schema": "family-company.player-human-v5-donor-surface-audit.v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "blenderVersion": bpy.app.version_string,
        "mode": "read-only-source-audit",
        "safeguards": {
            "allowListedDonors": list(SOURCES),
            "excludedMaterial": "test3/Sakurako",
            "excludedMaterialOpened": False,
            "sourceFilesWritten": False,
            "blendFilesSaved": False,
            "proof8Modified": False,
            "unityAssetsModified": False,
        },
        "donors": donors,
    }
    report["directSurfaceReshaping"] = build_reuse_assessment(donors)

    json_path = OUTPUT / "player-human-v5-donor-surface-audit.json"
    markdown_path = OUTPUT / "player-human-v5-donor-surface-audit.md"
    json_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    markdown_path.write_text(markdown_report(report), encoding="utf-8")
    print(json.dumps({"json": str(json_path), "markdown": str(markdown_path)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
