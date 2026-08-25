"""Mother texture-first proof from the user-owned Mika FBX.

Hard constraints for this experiment:
* import only Mika/test owned input; never test3/Sakurako;
* create zero character mesh/curve/primitive/accessory objects;
* keep the original body/hair surfaces, UVs, weights and full rig;
* recolor existing connected components/material regions only;
* remove only whole unwanted source components (wings/cape/ornaments) and the
  known disconnected opaque mouth plate;
* do not morph face, hands or age proportions.
"""

from __future__ import annotations

import argparse
import colorsys
import hashlib
import json
import math
import os
from collections import Counter, defaultdict, deque

import bmesh
import bpy
from mathutils import Vector


def parse_args():
    argv = list(__import__("sys").argv)
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--texture-dir", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--polished2", action="store_true")
    parser.add_argument("--adult-clean3", action="store_true")
    return parser.parse_args(argv)


ARGS = parse_args()
INPUT = os.path.abspath(ARGS.input)
TEXTURE_DIR = os.path.abspath(ARGS.texture_dir)
OUTPUT = os.path.abspath(ARGS.output_dir)
os.makedirs(OUTPUT, exist_ok=True)
ADULT_CLEAN3 = bool(ARGS.adult_clean3)
POLISHED2 = bool(ARGS.polished2 or ADULT_CLEAN3)
if ADULT_CLEAN3:
    STEM = "mother-texture-first3-adult-clean-gate"
    STATUS = "AWAITING_ROOT_ADULT_CLEAN_GATE"
    SCHEMA = "family-company.mother-texture-first3-adult-clean-gate.v1"
elif POLISHED2:
    STEM = "mother-texture-first2-polished-gate"
    STATUS = "AWAITING_ROOT_POLISHED_SURFACE_GATE"
    SCHEMA = "family-company.mother-texture-first2-polished-gate.v1"
else:
    STEM = "mother-texture-first1"
    STATUS = "DIAGNOSTIC_ONLY_NOT_A_CANDIDATE"
    SCHEMA = "family-company.mother-texture-first-proof.v1"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    normalized = sorted(tuple(round(float(value), 7) for value in point) for point in points)
    return hashlib.sha256(json.dumps(normalized, separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def weight_hash(obj, indices):
    group_names = {group.index: group.name for group in obj.vertex_groups}
    records = []
    for index in indices:
        vertex = obj.data.vertices[index]
        records.append((
            tuple(round(float(value), 7) for value in (obj.matrix_world @ vertex.co)),
            sorted((group_names[item.group], round(float(item.weight), 7)) for item in vertex.groups),
        ))
    return hashlib.sha256(json.dumps(sorted(records), separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def rig_hash(armature):
    records = []
    for bone in armature.data.bones:
        records.append((
            bone.name,
            tuple(round(float(value), 8) for value in bone.head_local),
            tuple(round(float(value), 8) for value in bone.tail_local),
            bone.parent.name if bone.parent else None,
            bool(bone.use_connect),
        ))
    return hashlib.sha256(json.dumps(sorted(records), separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def surface_uv_hash(obj, polygon_indices=None):
    allowed = None if polygon_indices is None else set(polygon_indices)
    uv_layer = obj.data.uv_layers.active.data
    records = []
    for polygon in obj.data.polygons:
        if allowed is not None and polygon.index not in allowed:
            continue
        corners = []
        for loop_index in polygon.loop_indices:
            loop = obj.data.loops[loop_index]
            coordinate = tuple(round(float(value), 7) for value in obj.data.vertices[loop.vertex_index].co)
            uv = tuple(round(float(value), 7) for value in uv_layer[loop_index].uv)
            corners.append((coordinate, uv))
        records.append(sorted(corners))
    return hashlib.sha256(json.dumps(sorted(records), separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def load_image(name, colorspace="sRGB"):
    image = bpy.data.images.load(os.path.join(TEXTURE_DIR, name), check_existing=False)
    image.colorspace_settings.name = colorspace
    return image


def material_components(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    polygon_map = {polygon.index: polygon for polygon in polygons}
    by_vertex = defaultdict(list)
    for polygon in polygons:
        for vertex_index in polygon.vertices:
            by_vertex[vertex_index].append(polygon.index)
    remaining = set(polygon_map)
    components = []
    while remaining:
        seed = remaining.pop()
        queue = deque([seed])
        component = {seed}
        while queue:
            polygon = polygon_map[queue.popleft()]
            for vertex_index in polygon.vertices:
                for neighbor in by_vertex[vertex_index]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        components.append(sorted(component))
    return sorted(components, key=len, reverse=True)


def component_vertices(mesh, component):
    return sorted({index for polygon_index in component for index in mesh.polygons[polygon_index].vertices})


def material_vertices(mesh, material_index):
    return sorted({
        vertex_index
        for polygon in mesh.polygons
        if polygon.material_index == material_index
        for vertex_index in polygon.vertices
    })


def set_principled_defaults(node, roughness=0.66, specular=0.10):
    node.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in node.inputs:
        node.inputs["Specular IOR Level"].default_value = specular


def faithful_texture_material(name, color_image, mask_image=None, roughness=0.66):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, roughness)
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "OriginalUVColor"
    texture.image = color_image
    texture.interpolation = "Linear"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    if mask_image is not None:
        mask = nodes.new("ShaderNodeTexImage")
        mask.name = "OriginalUVMask"
        mask.image = mask_image
        mask.interpolation = "Linear"
        separate = nodes.new("ShaderNodeSeparateColor")
        invert = nodes.new("ShaderNodeMath")
        invert.operation = "MULTIPLY_ADD"
        invert.inputs[1].default_value = -0.24
        invert.inputs[2].default_value = 0.78
        links.new(mask.outputs["Color"], separate.inputs["Color"])
        links.new(separate.outputs["Green"], invert.inputs[0])
        links.new(invert.outputs[0], principled.inputs["Roughness"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def polished_face_material(name, color_image):
    """Keep Mika face UV art while gently reducing the adolescent pink cast."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, 0.76, 0.09)
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "OriginalUVFace"
    texture.image = color_image
    texture.interpolation = "Linear"
    tone = nodes.new("ShaderNodeHueSaturation")
    # AdultClean3 is still an exact donor face: only reduce the painted blush
    # saturation by 14.6% relative to the already-polished TF2 node pass.
    tone.inputs["Saturation"].default_value = 0.70 if ADULT_CLEAN3 else 0.82
    tone.inputs["Value"].default_value = 0.98
    links.new(texture.outputs["Color"], tone.inputs["Color"])
    warm = nodes.new("ShaderNodeMixRGB")
    warm.blend_type = "MULTIPLY"
    warm.inputs[0].default_value = 0.08
    warm.inputs[2].default_value = (1.0, 0.90, 0.84, 1.0)
    links.new(tone.outputs["Color"], warm.inputs[1])
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    abs_x = nodes.new("ShaderNodeMath")
    abs_x.operation = "ABSOLUTE"
    links.new(separate.outputs["X"], abs_x.inputs[0])
    x_soft = nodes.new("ShaderNodeMapRange")
    x_soft.interpolation_type = "SMOOTHERSTEP"
    x_soft.clamp = True
    x_soft.inputs["From Min"].default_value = 0.034
    x_soft.inputs["From Max"].default_value = 0.043
    x_soft.inputs["To Min"].default_value = 1.0
    x_soft.inputs["To Max"].default_value = 0.0
    links.new(abs_x.outputs[0], x_soft.inputs["Value"])
    x_squared = nodes.new("ShaderNodeMath")
    x_squared.operation = "MULTIPLY"
    links.new(separate.outputs["X"], x_squared.inputs[0])
    links.new(separate.outputs["X"], x_squared.inputs[1])
    smile_height = nodes.new("ShaderNodeMath")
    smile_height.operation = "MULTIPLY_ADD"
    smile_height.inputs[1].default_value = 6.0
    smile_height.inputs[2].default_value = 1.235
    links.new(x_squared.outputs[0], smile_height.inputs[0])
    z_delta = nodes.new("ShaderNodeMath")
    z_delta.operation = "SUBTRACT"
    links.new(separate.outputs["Z"], z_delta.inputs[0])
    links.new(smile_height.outputs[0], z_delta.inputs[1])
    abs_delta = nodes.new("ShaderNodeMath")
    abs_delta.operation = "ABSOLUTE"
    links.new(z_delta.outputs[0], abs_delta.inputs[0])
    line_soft = nodes.new("ShaderNodeMapRange")
    line_soft.interpolation_type = "SMOOTHERSTEP"
    line_soft.clamp = True
    line_soft.inputs["From Min"].default_value = 0.0012
    line_soft.inputs["From Max"].default_value = 0.0030
    line_soft.inputs["To Min"].default_value = 1.0
    line_soft.inputs["To Max"].default_value = 0.0
    links.new(abs_delta.outputs[0], line_soft.inputs["Value"])
    front_gate = nodes.new("ShaderNodeMath")
    front_gate.operation = "LESS_THAN"
    front_gate.inputs[1].default_value = -0.145
    links.new(separate.outputs["Y"], front_gate.inputs[0])
    mouth_xz = nodes.new("ShaderNodeMath")
    mouth_xz.operation = "MULTIPLY"
    links.new(x_soft.outputs["Result"], mouth_xz.inputs[0])
    links.new(line_soft.outputs["Result"], mouth_xz.inputs[1])
    mouth_mask = nodes.new("ShaderNodeMath")
    mouth_mask.operation = "MULTIPLY"
    links.new(mouth_xz.outputs[0], mouth_mask.inputs[0])
    links.new(front_gate.outputs[0], mouth_mask.inputs[1])
    mouth_mix = nodes.new("ShaderNodeMixRGB")
    mouth_mix.blend_type = "MIX"
    mouth_mix.inputs[2].default_value = (0.17, 0.018, 0.024, 1.0)
    links.new(mouth_mask.outputs[0], mouth_mix.inputs[0])
    links.new(warm.outputs["Color"], mouth_mix.inputs[1])
    links.new(mouth_mix.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def polished_brow_material(name, color_image):
    """Source brow UV, lower saturation and stronger warm-brown contrast only."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, 0.72, 0.08)
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "OriginalUVBrow"
    texture.image = color_image
    texture.interpolation = "Linear"
    tone = nodes.new("ShaderNodeHueSaturation")
    tone.inputs["Saturation"].default_value = 0.52
    tone.inputs["Value"].default_value = 0.76
    links.new(texture.outputs["Color"], tone.inputs["Color"])
    contrast = nodes.new("ShaderNodeMixRGB")
    contrast.blend_type = "MULTIPLY"
    contrast.inputs[0].default_value = 0.28
    contrast.inputs[2].default_value = (0.24, 0.075, 0.045, 1.0)
    links.new(tone.outputs["Color"], contrast.inputs[1])
    links.new(contrast.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def palette_material(name, color_image, mask_image, dark, mid, light, temple_gray=False, flatten_detail=0.0):
    """Preserve the source UV/luminance/mask while changing only palette."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, 0.70, 0.07)
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "OriginalUVColor"
    texture.image = color_image
    texture.interpolation = "Linear"
    bw = nodes.new("ShaderNodeRGBToBW")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    first = ramp.color_ramp.elements[0]
    first.position = 0.08
    first.color = (*dark, 1.0)
    middle = ramp.color_ramp.elements.new(0.56)
    middle.color = (*mid, 1.0)
    last = ramp.color_ramp.elements.new(0.93)
    last.color = (*light, 1.0)
    links.new(texture.outputs["Color"], bw.inputs["Color"])
    links.new(bw.outputs["Val"], ramp.inputs["Fac"])
    palette_color = ramp.outputs["Color"]
    if flatten_detail > 0.0:
        # Material-only debranding.  This leaves the source UV/mask untouched,
        # but attenuates printed crests/ornaments that read as fantasy costume.
        flatten = nodes.new("ShaderNodeMixRGB")
        flatten.blend_type = "MIX"
        flatten.inputs[0].default_value = flatten_detail
        flatten.inputs[2].default_value = (*mid, 1.0)
        links.new(ramp.outputs["Color"], flatten.inputs[1])
        palette_color = flatten.outputs["Color"]
    if temple_gray:
        geometry = nodes.new("ShaderNodeNewGeometry")
        separate = nodes.new("ShaderNodeSeparateXYZ")
        links.new(geometry.outputs["Position"], separate.inputs["Vector"])
        abs_x = nodes.new("ShaderNodeMath")
        abs_x.operation = "ABSOLUTE"
        links.new(separate.outputs["X"], abs_x.inputs[0])
        side = nodes.new("ShaderNodeMapRange")
        side.interpolation_type = "SMOOTHERSTEP"
        side.clamp = True
        side.inputs["From Min"].default_value = 0.13
        side.inputs["From Max"].default_value = 0.27
        side.inputs["To Min"].default_value = 0.0
        side.inputs["To Max"].default_value = 1.0
        links.new(abs_x.outputs[0], side.inputs["Value"])
        low = nodes.new("ShaderNodeMapRange")
        low.interpolation_type = "SMOOTHERSTEP"
        low.clamp = True
        low.inputs["From Min"].default_value = 1.33
        low.inputs["From Max"].default_value = 1.43
        low.inputs["To Min"].default_value = 0.0
        low.inputs["To Max"].default_value = 1.0
        links.new(separate.outputs["Z"], low.inputs["Value"])
        high = nodes.new("ShaderNodeMapRange")
        high.interpolation_type = "SMOOTHERSTEP"
        high.clamp = True
        high.inputs["From Min"].default_value = 1.48
        high.inputs["From Max"].default_value = 1.59
        high.inputs["To Min"].default_value = 1.0
        high.inputs["To Max"].default_value = 0.0
        links.new(separate.outputs["Z"], high.inputs["Value"])
        vertical = nodes.new("ShaderNodeMath")
        vertical.operation = "MULTIPLY"
        links.new(low.outputs["Result"], vertical.inputs[0])
        links.new(high.outputs["Result"], vertical.inputs[1])
        region = nodes.new("ShaderNodeMath")
        region.operation = "MULTIPLY"
        links.new(side.outputs["Result"], region.inputs[0])
        links.new(vertical.outputs[0], region.inputs[1])
        subtle = nodes.new("ShaderNodeMath")
        subtle.operation = "MULTIPLY"
        subtle.inputs[1].default_value = 0.055
        links.new(region.outputs[0], subtle.inputs[0])
        gray_mix = nodes.new("ShaderNodeMixRGB")
        gray_mix.blend_type = "MIX"
        gray_mix.inputs[2].default_value = (0.082, 0.058, 0.045, 1.0)
        links.new(subtle.outputs[0], gray_mix.inputs[0])
        links.new(palette_color, gray_mix.inputs[1])
        palette_color = gray_mix.outputs["Color"]
    links.new(palette_color, principled.inputs["Base Color"])
    mask = nodes.new("ShaderNodeTexImage")
    mask.name = "OriginalUVMask"
    mask.image = mask_image
    mask.interpolation = "Linear"
    separate = nodes.new("ShaderNodeSeparateColor")
    rough = nodes.new("ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = 0.0
    rough.inputs["From Max"].default_value = 1.0
    rough.inputs["To Min"].default_value = 0.78
    rough.inputs["To Max"].default_value = 0.48
    links.new(mask.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], rough.inputs["Value"])
    links.new(rough.outputs["Result"], principled.inputs["Roughness"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (*mid, 1.0)
    return material


def transparent_material(name):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    links.new(transparent.outputs["BSDF"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    material.diffuse_color = (0.0, 0.0, 0.0, 0.0)
    return material


def build_eyemouth_alpha(source_image, output_path):
    width, height = source_image.size
    pixels = list(source_image.pixels[:])
    result = [0.0] * len(pixels)
    # The supplied atlas has no useful alpha channel.  Its most frequent RGB
    # value is the flat purple key colour surrounding the iris/highlight/lash
    # painting.  Measure that key from the decoded pixels instead of relying on
    # a brittle luminance threshold (which previously wrote an all-opaque PNG).
    keyed = defaultdict(int)
    for offset in range(0, len(pixels), 4):
        keyed[tuple(round(float(value), 6) for value in pixels[offset : offset + 3])] += 1
    key = max(keyed, key=keyed.get)
    for offset in range(0, len(pixels), 4):
        red, green, blue = pixels[offset : offset + 3]
        distance = math.sqrt((red - key[0]) ** 2 + (green - key[1]) ** 2 + (blue - key[2]) ** 2)
        factor = max(0.0, min(1.0, (distance - 0.0025) / 0.0325))
        # Smoothstep keeps the painted anti-aliased lash edge while making the
        # flat atlas key truly transparent.
        alpha = factor * factor * (3.0 - 2.0 * factor)
        result[offset + 0] = red
        result[offset + 1] = green
        result[offset + 2] = blue
        result[offset + 3] = alpha
    image = bpy.data.images.new("MotherTF_EyeMouthAlpha", width=width, height=height, alpha=True)
    image.pixels.foreach_set(result)
    image.alpha_mode = "STRAIGHT"
    image.file_format = "PNG"
    image.filepath_raw = output_path
    prior_color_mode = bpy.context.scene.render.image_settings.color_mode
    bpy.context.scene.render.image_settings.color_mode = "RGBA"
    image.save()
    bpy.context.scene.render.image_settings.color_mode = prior_color_mode
    return image


def alpha_texture_material(name, image, polished=False, procedural_mouth=False):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, 0.52, 0.14)
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    eye_color = texture.outputs["Color"]
    if polished:
        hue_sat = nodes.new("ShaderNodeHueSaturation")
        # AdultClean3 lowers iris/lash saturation by 13.3% versus TF2.  Alpha,
        # highlight topology and source UV artwork are unchanged.
        hue_sat.inputs["Saturation"].default_value = 0.52 if ADULT_CLEAN3 else 0.60
        hue_sat.inputs["Value"].default_value = 0.86
        links.new(texture.outputs["Color"], hue_sat.inputs["Color"])
        eye_color = hue_sat.outputs["Color"]

    alpha_socket = texture.outputs["Alpha"]
    color_socket = eye_color
    if procedural_mouth:
        # Reuse the source 32-polygon mouth plate as a fully transparent carrier
        # and draw one short, face-conforming smile in its shader.  This creates
        # no geometry and eliminates the opaque gray/purple muzzle.
        geometry = nodes.new("ShaderNodeNewGeometry")
        separate_position = nodes.new("ShaderNodeSeparateXYZ")
        links.new(geometry.outputs["Position"], separate_position.inputs["Vector"])

        abs_x = nodes.new("ShaderNodeMath")
        abs_x.operation = "ABSOLUTE"
        links.new(separate_position.outputs["X"], abs_x.inputs[0])
        x_gate = nodes.new("ShaderNodeMath")
        x_gate.operation = "LESS_THAN"
        x_gate.inputs[1].default_value = 0.052
        links.new(abs_x.outputs[0], x_gate.inputs[0])

        x_squared = nodes.new("ShaderNodeMath")
        x_squared.operation = "MULTIPLY"
        links.new(separate_position.outputs["X"], x_squared.inputs[0])
        links.new(separate_position.outputs["X"], x_squared.inputs[1])
        smile_height = nodes.new("ShaderNodeMath")
        smile_height.operation = "MULTIPLY_ADD"
        smile_height.inputs[1].default_value = 2.35
        smile_height.inputs[2].default_value = 1.238
        links.new(x_squared.outputs[0], smile_height.inputs[0])
        z_delta = nodes.new("ShaderNodeMath")
        z_delta.operation = "SUBTRACT"
        links.new(separate_position.outputs["Z"], z_delta.inputs[0])
        links.new(smile_height.outputs[0], z_delta.inputs[1])
        abs_z_delta = nodes.new("ShaderNodeMath")
        abs_z_delta.operation = "ABSOLUTE"
        links.new(z_delta.outputs[0], abs_z_delta.inputs[0])
        line_gate = nodes.new("ShaderNodeMath")
        line_gate.operation = "LESS_THAN"
        line_gate.inputs[1].default_value = 0.0048
        links.new(abs_z_delta.outputs[0], line_gate.inputs[0])

        front_gate = nodes.new("ShaderNodeMath")
        front_gate.operation = "LESS_THAN"
        front_gate.inputs[1].default_value = -0.155
        links.new(separate_position.outputs["Y"], front_gate.inputs[0])
        mouth_mask_xz = nodes.new("ShaderNodeMath")
        mouth_mask_xz.operation = "MULTIPLY"
        links.new(x_gate.outputs[0], mouth_mask_xz.inputs[0])
        links.new(line_gate.outputs[0], mouth_mask_xz.inputs[1])
        mouth_mask = nodes.new("ShaderNodeMath")
        mouth_mask.operation = "MULTIPLY"
        links.new(mouth_mask_xz.outputs[0], mouth_mask.inputs[0])
        links.new(front_gate.outputs[0], mouth_mask.inputs[1])

        combined_alpha = nodes.new("ShaderNodeMath")
        combined_alpha.operation = "MAXIMUM"
        links.new(texture.outputs["Alpha"], combined_alpha.inputs[0])
        links.new(mouth_mask.outputs[0], combined_alpha.inputs[1])
        alpha_socket = combined_alpha.outputs[0]
        mouth_color = nodes.new("ShaderNodeMixRGB")
        mouth_color.blend_type = "MIX"
        mouth_color.inputs[2].default_value = (0.42, 0.055, 0.065, 1.0)
        links.new(mouth_mask.outputs[0], mouth_color.inputs[0])
        links.new(eye_color, mouth_color.inputs[1])
        color_socket = mouth_color.outputs["Color"]

    links.new(color_socket, principled.inputs["Base Color"])
    mix = nodes.new("ShaderNodeMixShader")
    links.new(alpha_socket, mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(principled.outputs["BSDF"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    return material


def position_camera(camera, location, target):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


clear_scene()
bpy.ops.import_scene.fbx(filepath=INPUT)
armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("CH0069_Body")
weapon = bpy.data.objects.get("CH0069_Weapon")
if armature is None or body is None:
    raise RuntimeError("Expected owned Mika Armature and CH0069_Body")

source_mesh_objects = sorted(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH")
source_body_vertices = len(body.data.vertices)
source_body_polygons = len(body.data.polygons)
source_bones = len(armature.data.bones)
source_rig_hash = rig_hash(armature)
source_groups = [group.name for group in body.vertex_groups]
source_uv_layers = [layer.name for layer in body.data.uv_layers]
source_vertex_positions = [tuple(vertex.co) for vertex in body.data.vertices]
source_character_mesh_data = {obj.name: obj.data.name for obj in bpy.context.scene.objects if obj.type == "MESH"}

# Visual normalization only; source mesh coordinates, UVs and weights remain.
armature.scale = (1.8, 1.8, 1.8)
bpy.context.view_layer.update()

if weapon is not None:
    bpy.data.objects.remove(weapon, do_unlink=True)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200 if POLISHED2 else 1024
scene.render.resolution_y = 1200 if POLISHED2 else 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.012, 0.018, 0.028)
if POLISHED2:
    scene.view_settings.look = "AgX - Medium High Contrast"

camera_data = bpy.data.cameras.new("MotherTF_ReviewCameraData")
camera = bpy.data.objects.new("MotherTF_ReviewCamera", camera_data)
scene.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.02
scene.camera = camera

for name, location, energy, size in (
    ("MotherTF_Key", (-3.5, -4.0, 4.2), 920.0, 3.2),
    ("MotherTF_Fill", (3.8, -2.0, 2.6), 620.0, 3.0),
    ("MotherTF_Rim", (0.0, 3.8, 3.0), 780.0, 2.8),
):
    light_data = bpy.data.lights.new(name + "Data", "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector((0.0, 0.0, 0.90)) - light.location).to_track_quat("-Z", "Y").to_euler()

views = {
    "front": ((0.0, -4.1, 0.94), (0.0, 0.0, 0.87)),
    "three-quarter": ((2.8, -3.35, 1.00), (0.0, 0.0, 0.88)),
    "side": ((4.1, 0.0, 0.96), (0.0, 0.0, 0.88)),
    "back": ((0.0, 4.1, 0.94), (0.0, 0.0, 0.87)),
}


def render_material_set(prefix):
    paths = []
    scene.render.engine = "BLENDER_EEVEE"
    body.display_type = "TEXTURED"
    for label, (location, target) in views.items():
        position_camera(camera, location, target)
        path = os.path.join(OUTPUT, f"{prefix}-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths


# TF3 is intentionally limited to authority/color/gray/wire four-view evidence.
# The component audit already records the unmodified donor before cleanup.
original_views = [] if ADULT_CLEAN3 else render_material_set("mika-original-owned-surface")

body_image = load_image("CH0069_Body.png")
body_mask = load_image("CH0069_Body_Mask.png", "Non-Color")
hair_image = load_image("CH0069_Hair.png")
hair_mask = load_image("CH0069_Hair_Mask.png", "Non-Color")
face_image = load_image("CH0069_Face.png")
eye_source = load_image("CH0069_EyeMouth.png")
eye_alpha_path = os.path.join(OUTPUT, f"{STEM}-eyemouth-alpha.png")
eye_alpha_image = build_eyemouth_alpha(eye_source, eye_alpha_path)
eye_alpha_pixels = list(eye_alpha_image.pixels[:])
eye_alpha_values = eye_alpha_pixels[3::4]
eye_alpha_audit = {
    "minimum": round(float(min(eye_alpha_values)), 6),
    "maximum": round(float(max(eye_alpha_values)), 6),
    "transparentPixels": sum(1 for value in eye_alpha_values if value <= 0.01),
    "partialPixels": sum(1 for value in eye_alpha_values if 0.01 < value < 0.99),
    "opaquePixels": sum(1 for value in eye_alpha_values if value >= 0.99),
    "totalPixels": len(eye_alpha_values),
}

MAT_BODY_SOURCE = faithful_texture_material("MotherTF_SourceBodyFaithful", body_image, body_mask, 0.68)
MAT_FACE_SOURCE = (
    polished_face_material("MotherTF2_SourceFacePolished", face_image)
    if POLISHED2
    else faithful_texture_material("MotherTF_SourceFaceFaithful", face_image, None, 0.74)
)
MAT_BROW_SOURCE = (
    polished_brow_material("MotherTF2_SourceBrowMatureContrast", face_image)
    if POLISHED2
    else faithful_texture_material("MotherTF_SourceBrowFaithful", face_image, None, 0.70)
)
MAT_EYE_ALPHA = alpha_texture_material("MotherTF_EyeMouthAlpha", eye_alpha_image, polished=POLISHED2)
MAT_HAIR_CHESTNUT = palette_material(
    "MotherTF_ChestnutHair_SourceUV", hair_image, hair_mask,
    *((
        (0.004, 0.0015, 0.0010), (0.026, 0.0085, 0.0045), (0.105, 0.034, 0.018)
    ) if POLISHED2 else (
        (0.010, 0.002, 0.001), (0.065, 0.012, 0.005), (0.24, 0.070, 0.025)
    )),
    temple_gray=POLISHED2,
)
MAT_PEACH = palette_material(
    "MotherTF_PeachCardigan_SourceUV", body_image, body_mask,
    *((
        (0.070, 0.012, 0.010), (0.265, 0.067, 0.050), (0.53, 0.20, 0.14)
    ) if POLISHED2 else (
        (0.14, 0.020, 0.016), (0.48, 0.12, 0.090), (0.76, 0.32, 0.24)
    )),
    flatten_detail=0.82 if ADULT_CLEAN3 else 0.0,
)
MAT_CREAM = palette_material(
    "MotherTF_CreamBlouse_SourceUV", body_image, body_mask,
    *((
        (0.30, 0.23, 0.16), (0.72, 0.64, 0.52), (0.94, 0.88, 0.76)
    ) if POLISHED2 else (
        (0.38, 0.29, 0.20), (0.86, 0.76, 0.61), (0.99, 0.94, 0.82)
    )),
    flatten_detail=0.90 if ADULT_CLEAN3 else 0.0,
)
MAT_TEAL = palette_material(
    "MotherTF_TealSkirt_SourceUV", body_image, body_mask,
    *((
        (0.001, 0.008, 0.011), (0.003, 0.036, 0.043), (0.012, 0.115, 0.125)
    ) if POLISHED2 else (
        (0.004, 0.040, 0.045), (0.012, 0.18, 0.19), (0.080, 0.43, 0.41)
    )),
    flatten_detail=0.96 if ADULT_CLEAN3 else 0.0,
)
MAT_BROWN = palette_material(
    "MotherTF2_BrownShoe_SourceUV", body_image, body_mask,
    (0.006, 0.003, 0.002), (0.040, 0.018, 0.010), (0.15, 0.060, 0.030),
    flatten_detail=0.74 if ADULT_CLEAN3 else 0.0,
)
MAT_HIDDEN = transparent_material("MotherTF_WholeComponentHidden")


def slot(prefix):
    return next(index for index, material in enumerate(body.data.materials) if material and material.name.startswith(prefix))


hair_slot = slot("CH0069_Hair")
face_slot = slot("CH0069_Face")
brow_slot = slot("CH0069_Eyebrow")
eye_slot = slot("CH0069_EyeMouth")
body_slot = slot("CH0069_Body")
source_hair_vertices = material_vertices(body.data, hair_slot)
source_hair_polygons = sum(1 for polygon in body.data.polygons if polygon.material_index == hair_slot)
source_face_eye_vertices = sorted(set(
    material_vertices(body.data, face_slot)
    + material_vertices(body.data, brow_slot)
    + material_vertices(body.data, eye_slot)
))
hair_coordinate_hash_before = coordinate_hash(body, source_hair_vertices)
hair_weight_hash_before = weight_hash(body, source_hair_vertices)
face_eye_coordinate_hash_before = coordinate_hash(body, source_face_eye_vertices)
face_eye_weight_hash_before = weight_hash(body, source_face_eye_vertices)
body.data.materials[hair_slot] = MAT_HAIR_CHESTNUT
body.data.materials[face_slot] = MAT_FACE_SOURCE
body.data.materials[brow_slot] = MAT_BROW_SOURCE
body.data.materials[eye_slot] = MAT_EYE_ALPHA
body.data.materials[body_slot] = MAT_BODY_SOURCE

role_slots = {}
for role, material in (("peach", MAT_PEACH), ("cream", MAT_CREAM), ("teal", MAT_TEAL), ("brown", MAT_BROWN), ("hidden", MAT_HIDDEN)):
    role_slots[role] = len(body.data.materials)
    body.data.materials.append(material)

# Hair was audited as 24 disconnected source components before this pass.
# Component 0 owns both the crown and a connected waist-length rear sheet: it
# cannot be hidden without exposing a large open rear/crown hole.  Keep it
# fail-closed, while hiding only the audited detachable bun/outer-lock pieces.
hair_components = material_components(body.data, hair_slot)
adult_hair_keep = {0, 1, 3, 4, 16, 17, 21, 23}
hidden_hair_polygon_indices = set()
hair_component_receipt = []
for component_index, component in enumerate(hair_components):
    vertices = component_vertices(body.data, component)
    points = [(body.matrix_world @ body.data.vertices[index].co) for index in vertices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    keep = not ADULT_CLEAN3 or component_index in adult_hair_keep
    if not keep:
        hidden_hair_polygon_indices.update(component)
        for polygon_index in component:
            body.data.polygons[polygon_index].material_index = role_slots["hidden"]
    hair_component_receipt.append({
        "component": component_index,
        "polygons": len(component),
        "vertices": len(vertices),
        "boundsWorld": {
            "min": [round(float(value), 6) for value in lo],
            "max": [round(float(value), 6) for value in hi],
            "dimensions": [round(float(value), 6) for value in (hi - lo)],
        },
        "adultClean3Action": "keep" if keep else "hide-whole-component",
    })


def group_scores(vertex_indices):
    scores = defaultdict(float)
    for vertex_index in vertex_indices:
        for membership in body.data.vertices[vertex_index].groups:
            name = body.vertex_groups[membership.group].name
            weight = float(membership.weight)
            low = name.lower()
            if "bone_wing" in low:
                scores["wing"] += weight
            if "bone_cape" in low:
                scores["cape"] += weight
            if "dango_lace" in low:
                scores["dangoLace"] += weight
            if "bone_ribbon" in low:
                scores["ribbon"] += weight
            if "bone_skirt" in low:
                scores["skirt"] += weight
            if " hand" in low or " finger" in low:
                scores["hand"] += weight
            if any(token in low for token in ("thigh", "calf", " foot", " toe")):
                scores["leg"] += weight
            if any(token in low for token in ("upperarm", "forearm", "wrist")):
                scores["arm"] += weight
            if any(token in low for token in ("spine", "clavicle", " neck")):
                scores["torso"] += weight
            scores["total"] += weight
    return scores


body_components = material_components(body.data, body_slot)
component_receipt = []
delete_polygon_indices = set()
hidden_polygon_indices = set()
role_counts = defaultdict(int)
retained_hand_vertices = set()
for component_index, component in enumerate(body_components):
    vertices = component_vertices(body.data, component)
    points = [(body.matrix_world @ body.data.vertices[index].co) for index in vertices]
    lo = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    hi = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    center = (lo + hi) * 0.5
    scores = group_scores(vertices)
    total = max(scores["total"], 1e-9)
    fractions = {key: scores[key] / total for key in scores if key != "total"}

    reason = "source body/skin/leg retained"
    role = "source"
    if fractions.get("wing", 0.0) > 0.08:
        role, reason = "hidden", "whole wing component"
    elif fractions.get("cape", 0.0) > 0.30:
        role, reason = "hidden", "whole fantasy cape component"
    elif fractions.get("dangoLace", 0.0) > 0.14 or (center.z > 1.48 and center.x < -0.16):
        role, reason = "hidden", "whole hair-lace ornament component"
    elif fractions.get("ribbon", 0.0) > 0.24:
        role, reason = "hidden", "whole chest-ribbon ornament component"
    elif ADULT_CLEAN3 and component_index in {121, 142}:
        role, reason = "hidden", "whole disconnected Tea Party chest-badge ornament component"
    elif ADULT_CLEAN3 and component_index in {165, 167, 188, 190}:
        role, reason = "hidden", "whole disconnected fantasy waist-button ornament component"
    elif ADULT_CLEAN3 and fractions.get("skirt", 0.0) > 0.10:
        if component_index in {12, 13, 14, 15}:
            role, reason = "teal", "audited simplest connected A-line donor lower-shell component"
        else:
            role, reason = "hidden", "whole disconnected fantasy skirt/ruffle/apron component"
    elif (
        0.395 < abs(center.x) < 0.495
        and 0.72 < center.z < 0.91
        and (hi - lo).x < 0.135
        and (fractions.get("hand", 0.0) + fractions.get("arm", 0.0)) > 0.35
    ):
        role, reason = "hidden", "whole wrist-bracelet/cuff ornament component"
    elif (
        POLISHED2
        and fractions.get("skirt", 0.0) > 0.85
        and len(component) <= 25
        and center.y < -0.24
        and 0.56 < center.z < 0.70
        and (hi - lo).x < 0.16
        and (hi - lo).z < 0.12
    ):
        role, reason = "hidden", "whole disconnected skirt bow/ribbon ornament component"
    elif (
        POLISHED2
        and fractions.get("skirt", 0.0) > 0.85
        and len(component) <= 12
        and center.y < -0.19
        and 0.68 <= center.z < 0.87
        and 0.035 < abs(center.x) < 0.25
    ):
        role, reason = "hidden", "whole disconnected upper-skirt bow/button ornament component"
    elif (
        POLISHED2
        and center.z < 0.18
        and center.y > 0.04
        and 0.08 < abs(center.x) < 0.25
        and len(component) <= 30
    ):
        role, reason = "hidden", "whole disconnected shoe bow/heel ornament component"
    elif fractions.get("skirt", 0.0) > 0.10:
        role, reason = "teal", "existing skirt-weighted surface"
    elif fractions.get("hand", 0.0) > 0.30:
        role, reason = "source", "original hand surface unchanged"
    elif POLISHED2 and fractions.get("leg", 0.0) > 0.30 and hi.z < 0.23:
        role, reason = "brown", "existing low shoe/foot shell recolored brown"
    elif POLISHED2 and hi.z < 0.19 and 0.08 < abs(center.x) < 0.25:
        role, reason = "brown", "existing disconnected shoe surface recolored brown"
    elif fractions.get("leg", 0.0) > 0.35:
        role, reason = "source", "original leg/foot surface unchanged"
    elif fractions.get("arm", 0.0) > 0.18:
        role, reason = "peach", "existing sleeve/outer-arm surface"
    elif fractions.get("torso", 0.0) > 0.10:
        if abs(center.x) < 0.095 and center.y < 0.025:
            role, reason = "cream", "existing central blouse surface"
        else:
            role, reason = "peach", "existing outer torso/cardigan surface"

    if reason == "original hand surface unchanged":
        retained_hand_vertices.update(vertices)

    if role == "hidden":
        hidden_polygon_indices.update(component)
        if POLISHED2:
            for polygon_index in component:
                body.data.polygons[polygon_index].material_index = role_slots["hidden"]
        else:
            delete_polygon_indices.update(component)
    elif role in role_slots:
        for polygon_index in component:
            body.data.polygons[polygon_index].material_index = role_slots[role]
    role_counts[role] += len(component)
    component_receipt.append({
        "component": component_index,
        "polygons": len(component),
        "vertices": len(vertices),
        "centerWorld": [round(float(value), 6) for value in center],
        "dimensionsWorld": [round(float(value), 6) for value in (hi - lo)],
        "fractions": {key: round(float(value), 5) for key, value in fractions.items()},
        "role": role,
        "reason": reason,
    })

# The connected donor leg/pelvis component includes the dark fantasy
# underskirt as a polygon region.  Reassign only that already-existing region
# to teal; coordinates, topology, UVs and weights remain untouched.
lower_palette_polygon_count = 0
if POLISHED2:
    for polygon in body.data.polygons:
        if polygon.material_index != body_slot:
            continue
        center_world = body.matrix_world @ polygon.center
        if (
            0.50 < center_world.z < (0.75 if ADULT_CLEAN3 else 0.69)
            and center_world.y < 0.03
            and abs(center_world.x) < 0.28
        ):
            polygon.material_index = role_slots["teal"]
            lower_palette_polygon_count += 1

# The 32-polygon component maps entirely to the atlas background, not a usable
# mouth.  Remove that disconnected plate; all real eye components keep their
# source topology and the restored-alpha shader.
eye_components = material_components(body.data, eye_slot)
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError(f"Expected one 32-polygon mouth background plate, got {len(mouth_components)}")
if POLISHED2:
    removed_mouth_polygons = 0
    retained_shader_mouth_polygons = len(mouth_components[0])
    for polygon_index in mouth_components[0]:
        body.data.polygons[polygon_index].material_index = role_slots["hidden"]
else:
    delete_polygon_indices.update(mouth_components[0])
    removed_mouth_polygons = len(mouth_components[0])
    retained_shader_mouth_polygons = 0

retained_polygon_indices_before = [
    polygon.index for polygon in body.data.polygons if polygon.index not in delete_polygon_indices
]
retained_surface_uv_hash_before = surface_uv_hash(body, retained_polygon_indices_before)
body_coordinate_hash_before = coordinate_hash(body, range(len(body.data.vertices)))
body_weight_hash_before = weight_hash(body, range(len(body.data.vertices)))
hand_indices = sorted(retained_hand_vertices)
hand_coordinate_hash_before = coordinate_hash(body, hand_indices)
hand_weight_hash_before = weight_hash(body, hand_indices)

# Delete only complete disconnected source components.  No generated geometry
# is introduced, and every retained coordinate/UV/weight comes from the FBX.
if delete_polygon_indices:
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    targets = [bm.faces[index] for index in sorted(delete_polygon_indices) if index < len(bm.faces)]
    bmesh.ops.delete(bm, geom=targets, context="FACES")
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()

if len(body.data.vertices) != source_body_vertices:
    raise RuntimeError("Whole-component face deletion unexpectedly changed source vertex count")
retained_surface_uv_hash_after = surface_uv_hash(body)
body_coordinate_hash_after = coordinate_hash(body, range(len(body.data.vertices)))
body_weight_hash_after = weight_hash(body, range(len(body.data.vertices)))
hand_coordinate_hash_after = coordinate_hash(body, hand_indices)
hand_weight_hash_after = weight_hash(body, hand_indices)
hair_coordinate_hash_after = coordinate_hash(body, source_hair_vertices)
hair_weight_hash_after = weight_hash(body, source_hair_vertices)
face_eye_coordinate_hash_after = coordinate_hash(body, source_face_eye_vertices)
face_eye_weight_hash_after = weight_hash(body, source_face_eye_vertices)

for polygon in body.data.polygons:
    polygon.use_smooth = True

conversion_views = render_material_set(STEM)


def render_face_closeups():
    if not POLISHED2 or ADULT_CLEAN3:
        return []
    paths = []
    original_scale = camera.data.ortho_scale
    camera.data.ortho_scale = 0.72
    for label, location in (
        ("front", (0.0, -4.1, 1.44)),
        ("three-quarter", (2.8, -3.35, 1.46)),
    ):
        position_camera(camera, location, (0.0, -0.02, 1.43))
        path = os.path.join(OUTPUT, f"{STEM}-face-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    camera.data.ortho_scale = original_scale
    return paths


face_closeups = render_face_closeups()


def wire_diagnostic_material():
    material = bpy.data.materials.new("MotherTF_WireContinuityDiagnostic")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    wire = nodes.new("ShaderNodeWireframe")
    wire.use_pixel_size = True
    wire.inputs["Size"].default_value = 0.72
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.82, 0.93, 1.0, 1.0)
    emission.inputs["Strength"].default_value = 1.0
    mix = nodes.new("ShaderNodeMixShader")
    links.new(wire.outputs["Fac"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    return material


def gray_diagnostic_material():
    material = bpy.data.materials.new("MotherTF2_GrayContinuityDiagnostic")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (0.46, 0.49, 0.53, 1.0)
    principled.inputs["Roughness"].default_value = 0.78
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (0.58, 0.60, 0.63, 1.0)
    return material


MAT_WIRE_DIAGNOSTIC = wire_diagnostic_material()
MAT_GRAY_DIAGNOSTIC = gray_diagnostic_material()


def render_workbench(prefix, wire=False):
    paths = []
    original_materials = list(body.data.materials)
    if wire:
        scene.render.engine = "BLENDER_EEVEE"
        body.display_type = "TEXTURED"
        for index in range(len(body.data.materials)):
            if not POLISHED2 or index != role_slots["hidden"]:
                body.data.materials[index] = MAT_WIRE_DIAGNOSTIC
    elif POLISHED2:
        scene.render.engine = "BLENDER_EEVEE"
        body.display_type = "TEXTURED"
        for index in range(len(body.data.materials)):
            if index != role_slots["hidden"]:
                body.data.materials[index] = MAT_GRAY_DIAGNOSTIC
    else:
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.studio_light = "paint.sl"
        scene.display.shading.color_type = "SINGLE" if POLISHED2 else "MATERIAL"
        if POLISHED2:
            scene.display.shading.single_color = (0.58, 0.60, 0.63)
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
        scene.display.shading.cavity_type = "BOTH"
        body.display_type = "SOLID"
    body.show_wire = False
    for label, (location, target) in views.items():
        position_camera(camera, location, target)
        path = os.path.join(OUTPUT, f"{prefix}-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    if wire or POLISHED2:
        for index, material in enumerate(original_materials):
            body.data.materials[index] = material
    body.display_type = "TEXTURED"
    return paths


solid_views = render_workbench(f"{STEM}-gray" if POLISHED2 else "mother-texture-first1-solid", wire=False)
wire_views = render_workbench(f"{STEM}-wire", wire=True)

blend_path = os.path.join(OUTPUT, f"{STEM}.blend")
scene.render.engine = "BLENDER_EEVEE"
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

after_mesh_objects = sorted(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH")
created_character_mesh_objects = sorted(set(after_mesh_objects) - {"CH0069_Body"})
rig_hash_after = rig_hash(armature)
preservation_checks = {
    "bodyCoordinatesExact": body_coordinate_hash_before == body_coordinate_hash_after,
    "bodyWeightsExact": body_weight_hash_before == body_weight_hash_after,
    "retainedSurfaceUvExact": retained_surface_uv_hash_before == retained_surface_uv_hash_after,
    "hairCoordinatesWeightsExact": (
        hair_coordinate_hash_before == hair_coordinate_hash_after
        and hair_weight_hash_before == hair_weight_hash_after
    ),
    "faceEyeCoordinatesWeightsExact": (
        face_eye_coordinate_hash_before == face_eye_coordinate_hash_after
        and face_eye_weight_hash_before == face_eye_weight_hash_after
    ),
    "nativeHandsCoordinatesWeightsExact": (
        hand_coordinate_hash_before == hand_coordinate_hash_after
        and hand_weight_hash_before == hand_weight_hash_after
    ),
    "rigExact151": source_bones == 151 and len(armature.data.bones) == 151 and source_rig_hash == rig_hash_after,
}
if POLISHED2 and not all(preservation_checks.values()):
    raise RuntimeError("Polished source-surface preservation gate failed: " + json.dumps(preservation_checks))

hidden_reason_counts = Counter(
    item["reason"] for item in component_receipt if item["role"] == "hidden"
)
receipt = {
    "schema": SCHEMA,
    "status": STATUS,
    "candidateClaim": False,
    "passClaim": False,
    "source": {
        "fbx": INPUT,
        "sha256": sha256(INPUT),
        "ownership": "user-attested test/Mika owned input",
        "test3OrSakurakoUsed": False,
    },
    "hardConstraints": {
        "createdCharacterMeshObjects": created_character_mesh_objects,
        "createdCharacterMeshObjectCount": len(created_character_mesh_objects),
        "createdCharacterCurves": [],
        "createdCharacterPrimitives": [],
        "createdAccessories": [],
        "newCharacterGeometryCount": 0,
        "deletionPolicy": "only complete disconnected source components selected by audited component rules",
        "sourceUVOnly": [layer.name for layer in body.data.uv_layers] == source_uv_layers,
        "sourceRigBoneCount": len(armature.data.bones),
        "sourceVertexGroupNamesUnchanged": [group.name for group in body.vertex_groups] == source_groups,
        "faceHandAgeMorphApplied": False,
        "unityModified": False,
        "docsModified": False,
        "gifCreated": False,
    },
    "preservation": {
        "automaticChecks": preservation_checks,
        "sourceMeshObjects": source_mesh_objects,
        "sourceCharacterMeshData": source_character_mesh_data,
        "bodyVertexCountBefore": source_body_vertices,
        "bodyPolygonCountBefore": source_body_polygons,
        "bodyVertexCountAfterWholeComponentDeletion": len(body.data.vertices),
        "bodyPolygonCountAfterWholeComponentDeletion": len(body.data.polygons),
        "hiddenWholeComponentPolygonCount": (
            len(hidden_polygon_indices) + len(hidden_hair_polygon_indices) if POLISHED2 else 0
        ),
        "visiblePolygonCount": len(body.data.polygons) - (
            len(hidden_polygon_indices) + len(hidden_hair_polygon_indices) if POLISHED2 else 0
        ),
        "armatureBonesBefore": source_bones,
        "armatureBonesAfter": len(armature.data.bones),
        "rigHashBefore": source_rig_hash,
        "rigHashAfter": rig_hash_after,
        "vertexGroups": len(body.vertex_groups),
        "uvLayers": [layer.name for layer in body.data.uv_layers],
        "bodyCoordinateHashBefore": body_coordinate_hash_before,
        "bodyCoordinateHashAfter": body_coordinate_hash_after,
        "bodyWeightHashBefore": body_weight_hash_before,
        "bodyWeightHashAfter": body_weight_hash_after,
        "retainedSurfaceUvHashBefore": retained_surface_uv_hash_before,
        "retainedSurfaceUvHashAfter": retained_surface_uv_hash_after,
        "hairCoordinateHashBefore": hair_coordinate_hash_before,
        "hairCoordinateHashAfter": hair_coordinate_hash_after,
        "hairWeightHashBefore": hair_weight_hash_before,
        "hairWeightHashAfter": hair_weight_hash_after,
        "faceEyeCoordinateHashBefore": face_eye_coordinate_hash_before,
        "faceEyeCoordinateHashAfter": face_eye_coordinate_hash_after,
        "faceEyeWeightHashBefore": face_eye_weight_hash_before,
        "faceEyeWeightHashAfter": face_eye_weight_hash_after,
        "handCoordinateHashBefore": hand_coordinate_hash_before,
        "handCoordinateHashAfter": hand_coordinate_hash_after,
        "handWeightHashBefore": hand_weight_hash_before,
        "handWeightHashAfter": hand_weight_hash_after,
        "hairSurfacePolygonCountBefore": source_hair_polygons,
        "hairSurfacePolygonCountAfter": sum(1 for polygon in body.data.polygons if polygon.material_index == hair_slot),
        "hairGeometryShortened": False,
        "hairChange": (
            "audited detachable bun/outer-lock components hidden whole; crown/rear component 0 retained fail-closed because removing it opens the rear head; retained source coordinates/weights/UV exact"
            if ADULT_CLEAN3
            else "full original hair surface retained and recolored muted chestnut through source UV/luminance/mask; shader-only 11% temple gray region"
        ),
        "hands": "original 3-digit stylized hand retained",
        "face": "original source face vertices retained exactly; only source-texture/node polish, no age morph",
    },
    "componentConversion": {
        "rules": component_receipt,
        "polygonCountsBeforeDeletionByRole": dict(role_counts),
        "hiddenWholeComponentReasonCounts": dict(hidden_reason_counts),
        "deletedWholeComponentPolygons": len(delete_polygon_indices) - removed_mouth_polygons,
        "hiddenWholeComponentPolygons": (
            len(hidden_polygon_indices) + len(hidden_hair_polygon_indices) if POLISHED2 else 0
        ),
        "hairComponentRules": hair_component_receipt,
        "adultClean3HairKeep": sorted(adult_hair_keep) if ADULT_CLEAN3 else [],
        "adultClean3HairHidden": (
            sorted(set(range(len(hair_components))) - adult_hair_keep) if ADULT_CLEAN3 else []
        ),
        "adultClean3SkirtKeep": [12, 13, 14, 15] if ADULT_CLEAN3 else [],
        "componentAudit": (
            os.path.join(OUTPUT, "ComponentAudit", "mother-adult-clean-component-audit.json")
            if ADULT_CLEAN3
            else None
        ),
        "existingLowerPalettePolygonsReassignedTeal": lower_palette_polygon_count,
        "removedOpaqueMouthBackgroundPlatePolygons": removed_mouth_polygons,
        "retainedShaderMouthCarrierPolygons": retained_shader_mouth_polygons,
        "retainedTransparentMouthCarrierPolygons": retained_shader_mouth_polygons,
        "mouthPlateUVFinding": "32-polygon component samples only uniform atlas background; no usable mouth art exists in the supplied FBX atlas region",
    },
    "shaderFidelity": {
        "body": "original CH0069_Body.png UV luminance + CH0069_Body_Mask roughness retained; palette-only transform",
        "hair": "original CH0069_Hair.png UV luminance + CH0069_Hair_Mask retained; muted chestnut palette plus very slight shader-only temple gray",
        "face": (
            "original CH0069_Face.png retained; blush/face saturation reduced 14.6% from TF2 in nodes only, no gray muzzle material"
            if ADULT_CLEAN3
            else "original CH0069_Face.png retained; saturation/value adjustment only, no gray muzzle material"
        ),
        "brows": "original source brow UV retained; saturation lowered and warm-brown contrast increased in nodes only",
        "eyes": (
            "original CH0069_EyeMouth.png iris/highlights/lashes retained; alpha reconstructed by measured flat-purple color key; saturation reduced 13.3% from TF2"
            if ADULT_CLEAN3
            else "original CH0069_EyeMouth.png iris/highlights/lashes retained; alpha reconstructed by measured flat-purple color key, with gentle saturation reduction"
        ),
        "derivedEyeTexture": eye_alpha_path,
        "derivedEyeAlphaAudit": eye_alpha_audit,
        "mouth": (
            "source 32-polygon carrier retained but made fully transparent; one short warm-rose smile is generated directly on the original face material nodes"
            if POLISHED2
            else "source uniform-background mouth plate removed"
        ),
    },
    "identityRead": [
        "peach existing outer-torso/sleeve surfaces",
        "cream existing central blouse surfaces",
        "teal existing skirt-weighted surfaces",
        "brown existing shoe surfaces",
        (
            "audited whole-component hair cleanup; component 0 retained fail-closed"
            if ADULT_CLEAN3
            else "full original hair surface recolored muted chestnut with slight temple gray"
        ),
    ],
    "identityGate": {
        "result": (
            "NO_PASS_CLAIM_ADULT_CLEAN_BASELINE_ONLY"
            if ADULT_CLEAN3
            else ("NOT_PASS_YOUNG_DONOR_LIMIT" if POLISHED2 else "FAIL")
        ),
        "reasons": (
            [
                "whole-component cleanup removes detachable side-bun/outer-lock and fantasy skirt islands, but cannot remove hair component 0 because crown and waist-length rear hair are topologically connected",
                "the remaining donor face/body proportions still read younger than the 44-year-old authority; no age morph was permitted",
                "the retained donor lower shell remains short and flared rather than a literal below-knee midi skirt",
            ]
            if ADULT_CLEAN3
            else [
                "surface polish and facial readability are materially better, but exact Mika head/body proportions still read younger than the 44-year-old authority",
                "source-connected dress silhouette and integral ruffles remain; palette regions cannot become a literal cardigan/blouse/midi-skirt cut without geometry edits",
                "full preserved hair remains waist-length with a side bun rather than shoulder-length half-up",
            ]
            if POLISHED2
            else [
                "source silhouette remains Mika fantasy dress, not open peach cardigan + cream blouse + teal midi skirt",
                "source hair remains waist-length with side bun, not shoulder-length half-up",
                "ornate ruffles/bows/heels and youthful face remain",
            ]
        ),
        "candidateClaim": False,
        "passClaim": False,
        "gifCreated": False,
    },
    "renderUtilityObjectsOnly": [camera.name, "MotherTF_Key", "MotherTF_Fill", "MotherTF_Rim"],
    "views": {
        "original": original_views,
        "conversion": conversion_views,
        "faceCloseups": face_closeups,
        "grayContinuity": solid_views,
        "wireContinuity": wire_views,
    },
    "limitations": (
        [
            "Whole-component cleanup is partial: crown/scalp and waist-length rear hair share source component 0; hiding it caused an exposed rear-head hole in the audit, so it remains visible.",
            "The simplest safely retained donor lower shell is still short/flared, not an authority-matched midi skirt.",
            "The supplied FBX has no usable mouth artwork in the mouth-carrier UV region; that carrier remains transparent and the visible smile is shader-only on the original face surface.",
            "Exact donor face/body proportions remain youthful; no 44-year-old Mother or PASS claim is made.",
        ]
        if ADULT_CLEAN3
        else [
            "Texture/node polish gate only; the source skirt silhouette remains its original flared dress length and is not a literal adult midi cut.",
            "The supplied FBX has no usable mouth artwork in the mouth-carrier UV region; that carrier is retained transparent and the visible smile is shader-only on the original face surface.",
            "Exact donor face/body proportions remain youthful; this result must not be presented as a convincing 44-year-old Mother.",
            "Integral donor ruffles and the long side-bun hair cannot be removed without violating the no-new-geometry/full-surface constraint.",
        ]
    ),
    "blend": blend_path,
}
with open(os.path.join(OUTPUT, f"{STEM}-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
print("MOTHER_TEXTURE_FIRST_BLEND=" + blend_path)
