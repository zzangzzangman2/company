import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-fbx", required=True)
    parser.add_argument("--output-texture", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def receipt_path(path, external=False):
    path = Path(path).resolve()
    if external:
        return path.name
    try:
        return path.relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        return path.name


def world_bounds(objects):
    corners = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector((min(point.x for point in corners), min(point.y for point in corners), min(point.z for point in corners)))
    maximum = Vector((max(point.x for point in corners), max(point.y for point in corners), max(point.z for point in corners)))
    return minimum, maximum


def main():
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_fbx = Path(args.output_fbx).resolve()
    output_texture = Path(args.output_texture).resolve()
    receipt_path = Path(args.receipt).resolve()
    output_fbx.parent.mkdir(parents=True, exist_ok=True)
    output_texture.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(input_path))

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected exactly one mesh, found {len(meshes)}")
    mesh = meshes[0]

    before_min, before_max = world_bounds(meshes)
    centre_xy = Vector(((before_min.x + before_max.x) * 0.5, (before_min.y + before_max.y) * 0.5, 0.0))
    translation = Vector((-centre_xy.x, -centre_xy.y, -before_min.z))
    for obj in meshes:
        obj.location += translation
    bpy.context.view_layer.update()

    after_min, after_max = world_bounds(meshes)
    if abs(after_min.z) > 1e-5:
        raise RuntimeError(f"Ground normalization failed: min Z={after_min.z}")

    images = [image for image in bpy.data.images if image.type == "IMAGE" and image.size[0] > 0]
    if len(images) != 1:
        raise RuntimeError(f"Expected exactly one embedded image, found {len(images)}")
    image = images[0]
    image.filepath_raw = str(output_texture)
    image.file_format = "PNG"
    image.save()

    materials = list(dict.fromkeys(
        slot.material for slot in mesh.material_slots if slot.material is not None
    ))
    if len(materials) != 1:
        names = ", ".join(material.name for material in materials)
        raise RuntimeError(
            f"Expected exactly one mesh-slot material, found {len(materials)}: {names}"
        )
    material = materials[0]
    material.name = "FatherV18HiggsfieldStatic_Material"
    image.name = "father-v18-higgsfield-static-albedo"
    mesh.name = "FatherV18HiggsfieldStatic"
    mesh.data.name = "FatherV18HiggsfieldStatic_Mesh"

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.export_scene.fbx(
        filepath=str(output_fbx),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        path_mode="RELATIVE",
        embed_textures=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        bake_anim=False,
    )

    receipt = {
        "contract": "FC-FATHER-V18-HIGGSFIELD-STATIC-UNITY-PREP-V1",
        "sourceGlb": receipt_path(input_path, external=True),
        "sourceGlbSha256": sha256(input_path),
        "outputFbx": receipt_path(output_fbx),
        "outputFbxSha256": sha256(output_fbx),
        "outputTexture": receipt_path(output_texture),
        "outputTextureSha256": sha256(output_texture),
        "meshCount": len(meshes),
        "vertexCount": len(mesh.data.vertices),
        "polygonCount": len(mesh.data.polygons),
        "materialCount": len(materials),
        "imageCount": len(images),
        "imageWidth": int(image.size[0]),
        "imageHeight": int(image.size[1]),
        "sourceBoundsMin": list(before_min),
        "sourceBoundsMax": list(before_max),
        "unityPrepBoundsMin": list(after_min),
        "unityPrepBoundsMax": list(after_max),
        "normalization": "Blender XY centered; Blender Z grounded; FBX -Z forward/Y up; no rig or animation added",
        "productionEligible": False,
    }
    receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
