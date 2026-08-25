"""Create an isolated Mother conversion feasibility proof from the user-owned Mika FBX.

The imported face/body topology, vertex positions, skin weights and armature are
kept intact except for the explicitly rejected disconnected 32-polygon mouth
plate.  The proof restores the extracted EyeMouth texture's missing alpha, then
demonstrates a Mother-specific material and garment/hair blockout on top of that
proven human/anime base.  It does not import or inspect test3/Sakurako.

This is review evidence, not a final character or a Unity candidate.
"""

import argparse
import colorsys
import hashlib
import json
import math
import os
import sys
from collections import defaultdict, deque

import bpy
import bmesh
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--textures", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--quality", choices=("draft", "final"), default="draft")
    return parser.parse_args(argv)


ARGS = parse_args()
FBX_PATH = os.path.abspath(ARGS.fbx)
TEXTURE_DIR = os.path.abspath(ARGS.textures)
OUTPUT = os.path.abspath(ARGS.output)
os.makedirs(OUTPUT, exist_ok=True)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


clear_scene()
bpy.ops.import_scene.fbx(filepath=FBX_PATH)

armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("CH0069_Body")
if armature is None or body is None:
    raise RuntimeError("Mika source must contain Armature and CH0069_Body")

for removable_name in ("CH0069_Weapon",):
    removable = bpy.data.objects.get(removable_name)
    if removable is not None:
        bpy.data.objects.remove(removable, do_unlink=True)

# The FBX stores centimetre-scale transforms.  Preserve hierarchy and normalize
# only the armature root so the complete source becomes 1.77 m tall.
armature.scale = (1.8, 1.8, 1.8)
bpy.context.view_layer.update()

source_vertex_count = len(body.data.vertices)
source_polygon_count = len(body.data.polygons)
source_bone_count = len(armature.data.bones)
source_vertex_positions = [tuple(vertex.co) for vertex in body.data.vertices]
source_vertex_group_count = len(body.vertex_groups)


def principled_material(name, color, roughness=0.72, specular=0.08, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = specular
    material.diffuse_color = (*color, 1.0)
    return material


MAT_PEACH = principled_material("Mother_DustyPeach_Cardigan", (0.72, 0.27, 0.22), 0.78, 0.05)
MAT_PEACH_LIGHT = principled_material("Mother_Peach_Edge", (0.91, 0.48, 0.39), 0.76, 0.05)
MAT_CREAM = principled_material("Mother_Cream_Blouse", (0.94, 0.86, 0.70), 0.80, 0.04)
MAT_TEAL = principled_material("Mother_DeepTeal_Skirt", (0.035, 0.24, 0.24), 0.80, 0.04)
MAT_TEAL_LIGHT = principled_material("Mother_Teal_Seam", (0.08, 0.40, 0.38), 0.77, 0.05)
MAT_BROWN = principled_material("Mother_Chestnut_Hair", (0.075, 0.028, 0.016), 0.72, 0.06)
MAT_BROWN_LIGHT = principled_material("Mother_Chestnut_Highlight", (0.16, 0.060, 0.030), 0.70, 0.06)
MAT_LOAFER = principled_material("Mother_DarkBrown_Loafers", (0.10, 0.026, 0.014), 0.72, 0.07)
MAT_PEARL = principled_material("Mother_Pearl", (0.95, 0.90, 0.82), 0.35, 0.18)
MAT_METAL = principled_material("Mother_WatchMetal", (0.55, 0.48, 0.36), 0.34, 0.18, 0.25)
MAT_OUTLINE = principled_material("Mother_Seam", (0.18, 0.055, 0.035), 0.82, 0.03)
MAT_MOUTH = principled_material("Mother_WarmRose_Mouth", (0.58, 0.075, 0.085), 0.64, 0.04)
MAT_SKIN = principled_material("Mother_Skin_Blockout", (0.96, 0.69, 0.57), 0.76, 0.06)


def build_eye_texture(source_path, output_path, brown=False):
    """Restore the missing alpha and optionally recolor the purple iris brown.

    The extracted PNG is fully opaque even where its intended background is
    black.  Alpha is reconstructed from distance to black; saturated purple
    iris pixels remain opaque and the square/polygon background disappears.
    """

    source = bpy.data.images.load(source_path, check_existing=False)
    source.colorspace_settings.name = "sRGB"
    width, height = source.size
    pixels = list(source.pixels[:])
    result = [0.0] * len(pixels)
    for offset in range(0, len(pixels), 4):
        red, green, blue = pixels[offset : offset + 3]
        maximum = max(red, green, blue)
        minimum = min(red, green, blue)
        chroma = maximum - minimum
        # Pure/near black is the lost-alpha background.  A short smooth ramp
        # retains the antialiased edge without leaving a rectangular patch.
        alpha = max(0.0, min(1.0, (maximum - 0.018) / 0.080))
        if brown and maximum > 0.025 and chroma > 0.010:
            hue, saturation, value = colorsys.rgb_to_hsv(red, green, blue)
            # Preserve white sclera/glints and only move colored iris pixels.
            if saturation > 0.10:
                hue = 0.070
                saturation = min(1.0, 0.60 + saturation * 0.38)
                value = min(1.0, value * 1.08)
                red, green, blue = colorsys.hsv_to_rgb(hue, saturation, value)
        result[offset + 0] = red
        result[offset + 1] = green
        result[offset + 2] = blue
        result[offset + 3] = alpha

    image = bpy.data.images.new(
        "MotherEyeMouthBrownAlpha" if brown else "MikaEyeMouthAlphaRestored",
        width=width,
        height=height,
        alpha=True,
    )
    image.pixels.foreach_set(result)
    image.file_format = "PNG"
    image.filepath_raw = output_path
    image.save()
    return image


def alpha_texture_material(name, image, hue_shift=None):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Roughness"].default_value = 0.46
    if "Specular IOR Level" in principled.inputs:
        principled.inputs["Specular IOR Level"].default_value = 0.15
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    color_output = texture.outputs["Color"]
    if hue_shift is not None:
        hue = nodes.new("ShaderNodeHueSaturation")
        hue.inputs["Hue"].default_value = hue_shift
        hue.inputs["Saturation"].default_value = 1.15
        links.new(color_output, hue.inputs["Color"])
        color_output = hue.outputs["Color"]
    links.new(color_output, principled.inputs["Base Color"])
    mix = nodes.new("ShaderNodeMixShader")
    links.new(texture.outputs["Alpha"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(principled.outputs["BSDF"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
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
    return material


def unlit_texture_material(name, source_path, tint=(1.0, 1.0, 1.0)):
    """Use the source texture without the extracted tangent-normal artifacts.

    Mika's face texture already carries its intended soft tone.  Emission keeps
    that tone stable under front/three-quarter/side studio cameras and prevents
    the rejected black diagonal face band and black jaw/neck.
    """

    image = bpy.data.images.load(source_path, check_existing=False)
    image.colorspace_settings.name = "sRGB"
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    multiply.inputs[2].default_value = (*tint, 1.0)
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 0.92
    links.new(texture.outputs["Color"], multiply.inputs[1])
    links.new(multiply.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


eye_source_path = os.path.join(TEXTURE_DIR, "CH0069_EyeMouth.png")
restored_eye_path = os.path.join(OUTPUT, "mika-eyemouth-alpha-restored.png")
mother_eye_path = os.path.join(OUTPUT, "mother-eyemouth-brown-alpha.png")
restored_eye_image = build_eye_texture(eye_source_path, restored_eye_path, brown=False)
mother_eye_image = build_eye_texture(eye_source_path, mother_eye_path, brown=True)
restored_eye_material = alpha_texture_material("Mika_EyeMouth_AlphaRestored", restored_eye_image)
mother_eye_material = alpha_texture_material("Mother_EyeMouth_Brown_Alpha", mother_eye_image)

eye_slot_index = next(index for index, material in enumerate(body.data.materials) if material.name.startswith("CH0069_EyeMouth"))


def material_face_components(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    by_vertex = defaultdict(list)
    polygon_by_index = {polygon.index: polygon for polygon in polygons}
    for polygon in polygons:
        for vertex_index in polygon.vertices:
            by_vertex[vertex_index].append(polygon.index)
    remaining = set(polygon_by_index)
    components = []
    while remaining:
        first = remaining.pop()
        queue = deque([first])
        component = {first}
        while queue:
            polygon = polygon_by_index[queue.popleft()]
            for vertex_index in polygon.vertices:
                for adjacent in by_vertex[vertex_index]:
                    if adjacent in remaining:
                        remaining.remove(adjacent)
                        component.add(adjacent)
                        queue.append(adjacent)
        components.append(component)
    return components


# The extracted EyeMouth object contains one known loose 32-polygon mouth
# plate.  It is the exact source of the large white/grey hexagonal patch.  The
# user requested that component be removed while preserving face and eye
# components.  It is disconnected, so deleting its 25 vertices cannot touch
# the face shell, eyes, hands, skin weights or rig.
eye_components = material_face_components(body.data, eye_slot_index)
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError(f"Expected exactly one 32-polygon loose mouth component, got {len(mouth_components)}")
mouth_polygon_indices = mouth_components[0]
mouth_vertex_indices = {
    vertex_index
    for polygon_index in mouth_polygon_indices
    for vertex_index in body.data.polygons[polygon_index].vertices
}
mouth_bmesh = bmesh.new()
mouth_bmesh.from_mesh(body.data)
mouth_bmesh.verts.ensure_lookup_table()
bmesh.ops.delete(
    mouth_bmesh,
    geom=[mouth_bmesh.verts[index] for index in sorted(mouth_vertex_indices)],
    context="VERTS",
)
mouth_bmesh.to_mesh(body.data)
mouth_bmesh.free()
body.data.update()
removed_mouth_polygon_count = len(mouth_polygon_indices)
removed_mouth_vertex_count = len(mouth_vertex_indices)
body.data.materials[eye_slot_index] = restored_eye_material


def smooth_object(obj):
    if hasattr(obj.data, "polygons"):
        for polygon in obj.data.polygons:
            polygon.use_smooth = True


def ellipsoid(name, location, radii, material, segments=40, rings=28):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = radii
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    smooth_object(obj)
    return obj


def loft(name, rings, material, segments=44, exponent=2.1, front_gap_half_angle=0.0):
    vertices = []
    for z, radius_x, radius_y, centre_y in rings:
        for index in range(segments):
            angle = math.tau * index / segments
            c = math.cos(angle)
            s = math.sin(angle)
            x = radius_x * math.copysign(abs(c) ** (2.0 / exponent), c)
            y = centre_y + radius_y * math.copysign(abs(s) ** (2.0 / exponent), s)
            vertices.append((x, y, z))
    faces = []
    for ring_index in range(len(rings) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            angle_mid = math.tau * (index + 0.5) / segments
            front_delta = math.atan2(
                math.sin(angle_mid + math.pi * 0.5),
                math.cos(angle_mid + math.pi * 0.5),
            )
            if front_gap_half_angle > 0.0 and abs(front_delta) < front_gap_half_angle:
                continue
            faces.append((start + index, start + nxt, next_start + nxt, next_start + index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    smooth_object(obj)
    bevel = obj.modifiers.new("GarmentEdgeSoftness", "BEVEL")
    bevel.width = 0.008
    bevel.segments = 3
    return obj


def tube_curve(name, points, radius, material, cyclic=False):
    curve = bpy.data.curves.new(name + "Curve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 12
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def cone_between(name, start, end, radius_start, radius_end, material):
    start = Vector(start)
    end = Vector(end)
    vector = end - start
    bpy.ops.mesh.primitive_cone_add(
        vertices=36,
        radius1=radius_end,
        radius2=radius_start,
        depth=vector.length,
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = vector.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(material)
    smooth_object(obj)
    bevel = obj.modifiers.new("SleeveSoftness", "BEVEL")
    bevel.width = min(radius_start, radius_end) * 0.12
    bevel.segments = 3
    return obj


def rounded_box(name, location, scale, material, bevel_width=0.018):
    bpy.ops.mesh.primitive_cube_add(size=2.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("RoundedGarmentPanel", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = 4
    smooth_object(obj)
    return obj


def tapered_clump(name, points, radii, material, sides=12):
    points = [Vector(point) for point in points]
    vertices = []
    for index, (point, radius) in enumerate(zip(points, radii)):
        if index == 0:
            tangent = (points[1] - points[0]).normalized()
        elif index == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[index + 1] - points[index - 1]).normalized()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.93:
            reference = Vector((1.0, 0.0, 0.0))
        axis_a = tangent.cross(reference).normalized()
        axis_b = tangent.cross(axis_a).normalized()
        for side in range(sides):
            angle = math.tau * side / sides
            offset = axis_a * (math.cos(angle) * radius) + axis_b * (math.sin(angle) * radius * 0.60)
            vertices.append(tuple(point + offset))
    faces = []
    for ring in range(len(points) - 1):
        start = ring * sides
        next_start = (ring + 1) * sides
        for side in range(sides):
            nxt = (side + 1) % sides
            faces.append((start + side, start + nxt, next_start + nxt, next_start + side))
    faces.append(tuple(reversed(range(sides))))
    tip_start = (len(points) - 1) * sides
    faces.append(tuple(tip_start + side for side in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    smooth_object(obj)
    subdivision = obj.modifiers.new("HairClumpSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 1
    return obj


scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 768 if ARGS.quality == "draft" else 1024
scene.render.resolution_y = 768 if ARGS.quality == "draft" else 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.use_freestyle = True
scene.render.line_thickness = 0.55
scene.view_settings.look = "AgX - Medium High Contrast"

world = bpy.data.worlds.new("MotherProofWorld")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.065, 0.082, 1.0)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.46


def add_area(name, location, energy, color, size):
    data = bpy.data.lights.new(name, type="AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 0.95)) - obj.location).to_track_quat("-Z", "Y").to_euler()


add_area("Key", (-3.0, -4.0, 4.4), 980.0, (1.0, 0.86, 0.75), 3.4)
add_area("Fill", (3.5, -2.0, 3.1), 600.0, (0.70, 0.83, 1.0), 3.0)
add_area("Rim", (1.5, 3.4, 4.0), 800.0, (0.70, 0.86, 1.0), 2.8)

bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 0.0, -0.018))
floor = bpy.context.object
floor.name = "ReviewFloor"
floor.data.materials.append(principled_material("ReviewFloorMaterial", (0.11, 0.14, 0.18), 0.88, 0.03))

camera_data = bpy.data.cameras.new("ReviewCamera")
camera = bpy.data.objects.new("ReviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.18


def position_camera(location, target=(0.0, 0.0, 0.87)):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def bone_head(name):
    bone = armature.data.bones.get(name)
    if bone is None:
        raise RuntimeError("Missing bone: " + name)
    return armature.matrix_world @ bone.head_local


views = {
    "front": ((0.0, -4.2, 0.95), (0.0, 0.0, 0.87)),
    "three-quarter": ((2.9, -3.45, 1.02), (0.0, 0.0, 0.88)),
    "side": ((4.25, 0.0, 0.98), (0.0, 0.0, 0.88)),
}


def render_set(prefix):
    outputs = []
    for label, (location, target) in views.items():
        position_camera(location, target)
        path = os.path.join(OUTPUT, f"{prefix}-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        outputs.append(path)
    return outputs


# The deleted loose plate is replaced by a small curved line that hugs the
# face surface and remains readable in front, three-quarter and side views.
tube_curve(
    "RestoredMouthVolume",
    (
        (-0.030, -0.186, 1.237),
        (-0.015, -0.192, 1.233),
        (0.000, -0.195, 1.231),
        (0.015, -0.192, 1.233),
        (0.030, -0.186, 1.237),
    ),
    0.0048,
    MAT_MOUTH,
)

# Original source appearance, with the lost EyeMouth alpha/polygon background
# repaired.  No Mother identity changes have been applied yet.
original_outputs = render_set("mika-owned-base-original")


# ---------------------------------------------------------------------------
# Mother conversion blockout.  Source body topology/weights/rig stay intact.
# ---------------------------------------------------------------------------

# Preserve the Mika face/eyes/hands/rig while replacing hair and costume.
hair_slot = next(index for index, material in enumerate(body.data.materials) if material.name.startswith("CH0069_Hair"))
face_slot = next(index for index, material in enumerate(body.data.materials) if material.name.startswith("CH0069_Face"))
brow_slot = next(index for index, material in enumerate(body.data.materials) if material.name.startswith("CH0069_Eyebrow"))
body_slot = next(index for index, material in enumerate(body.data.materials) if material.name.startswith("CH0069_Body"))
mother_face_material = unlit_texture_material(
    "Mother_MikaFace_NoNormalBand",
    os.path.join(TEXTURE_DIR, "CH0069_Face.png"),
    tint=(1.0, 0.96, 0.93),
)
body.data.materials[face_slot] = mother_face_material
body.data.materials[brow_slot] = MAT_BROWN
body.data.materials[eye_slot_index] = mother_eye_material
source_body_hidden = transparent_material("Mika_OriginalCostume_HiddenPreservedTopology")
body.data.materials[body_slot] = source_body_hidden

# Keep the original rigged hands/fingers and only recolor their polygons to
# skin.  All remaining source costume polygons become transparent but stay in
# the mesh and retain their weights for a reversible final conversion.
source_hand_slot = len(body.data.materials)
body.data.materials.append(MAT_SKIN)
hand_group_indices = {
    group.index
    for group in body.vertex_groups
    if " Hand" in group.name or " Finger" in group.name
}
preserved_hand_polygon_count = 0
for polygon in body.data.polygons:
    if polygon.material_index != body_slot:
        continue
    relevant_weight = 0.0
    for vertex_index in polygon.vertices:
        for membership in body.data.vertices[vertex_index].groups:
            if membership.group in hand_group_indices:
                relevant_weight += membership.weight
    if relevant_weight / len(polygon.vertices) > 0.22:
        polygon.material_index = source_hand_slot
        preserved_hand_polygon_count += 1

# Preserve Mika's source hair topology/weights but hide the entire donor hair;
# Mother receives new original shoulder-length half-up geometry below.
hidden_hair = transparent_material("Mother_MikaHair_HiddenPreservedTopology")
hidden_polygon_count = sum(1 for polygon in body.data.polygons if polygon.material_index == hair_slot)
body.data.materials[hair_slot] = hidden_hair

# A-line skirt, fitted cream blouse and an actual open-front cardigan shell.
loft(
    "Mother_AlineSkirt_Blockout",
    (
        (0.18, 0.290, 0.188, -0.010),
        (0.30, 0.270, 0.180, -0.012),
        (0.54, 0.220, 0.158, -0.015),
        (0.68, 0.188, 0.145, -0.020),
    ),
    MAT_TEAL,
    exponent=2.2,
)
loft(
    "Mother_CreamBlouseTorso",
    (
        (0.64, 0.168, 0.129, -0.010),
        (0.78, 0.160, 0.126, -0.012),
        (0.92, 0.184, 0.136, -0.010),
        (1.045, 0.202, 0.132, -0.005),
        (1.095, 0.118, 0.103, -0.005),
    ),
    MAT_CREAM,
    exponent=2.25,
)
loft(
    "Mother_CardiganTorso_Blockout",
    (
        (0.64, 0.185, 0.142, -0.010),
        (0.78, 0.176, 0.137, -0.012),
        (0.92, 0.205, 0.148, -0.010),
        (1.045, 0.230, 0.145, -0.005),
        (1.095, 0.135, 0.116, -0.005),
    ),
    MAT_PEACH,
    exponent=2.25,
    front_gap_half_angle=0.52,
)

# Cardigan open-front edges, blouse collar and skirt hem are raised seams that
# follow the new garment volumes rather than face-camera planes.
tube_curve("CardiganEdge.L", ((-0.102, -0.145, 1.025), (-0.100, -0.151, 0.86), (-0.098, -0.143, 0.70)), 0.008, MAT_PEACH_LIGHT)
tube_curve("CardiganEdge.R", ((0.102, -0.145, 1.025), (0.100, -0.151, 0.86), (0.098, -0.143, 0.70)), 0.008, MAT_PEACH_LIGHT)
tube_curve("BlouseCollar.L", ((-0.090, -0.148, 1.020), (-0.030, -0.151, 0.982), (0.0, -0.152, 0.967)), 0.009, MAT_CREAM)
tube_curve("BlouseCollar.R", ((0.090, -0.148, 1.020), (0.030, -0.151, 0.982), (0.0, -0.152, 0.967)), 0.009, MAT_CREAM)
tube_curve("SkirtHem", ((-0.270, -0.145, 0.205), (0.0, -0.186, 0.185), (0.270, -0.145, 0.205)), 0.010, MAT_TEAL_LIGHT)


# Sleeves follow the retained rig's actual upper-arm/forearm axes.
for side, suffix in (("L", "L"), ("R", "R")):
    upper = bone_head(f"Bip001 {side} UpperArm")
    elbow = bone_head(f"Bip001 {side} Forearm")
    wrist = bone_head(f"Bip001 {side} Hand")
    cone_between("Mother_CardiganUpperSleeve." + suffix, upper, elbow, 0.082, 0.071, MAT_PEACH)
    cone_between("Mother_CardiganForeSleeve." + suffix, elbow, wrist - (wrist - elbow).normalized() * 0.075, 0.073, 0.056, MAT_PEACH)
    ellipsoid("Mother_SleeveCuff." + suffix, wrist - (wrist - elbow).normalized() * 0.075, (0.052, 0.048, 0.045), MAT_PEACH_LIGHT, 32, 22)

# The transparent-preserved source costume included its legs and neck.  Add
# clean body volumes on the same retained skeleton for the static proof.
cone_between(
    "Mother_Neck",
    (0.0, -0.006, 1.070),
    (0.0, -0.006, 1.180),
    0.050,
    0.046,
    MAT_SKIN,
)
for side, suffix in (("L", "L"), ("R", "R")):
    calf = bone_head(f"Bip001 {side} Calf")
    foot = bone_head(f"Bip001 {side} Foot")
    cone_between("Mother_BareCalf." + suffix, (calf.x, calf.y, 0.315), (foot.x, foot.y - 0.010, 0.115), 0.066, 0.052, MAT_SKIN)

# Dark brown loafers wrap both complete source feet.
for sign, suffix in ((-1.0, "R"), (1.0, "L")):
    foot = bone_head(f"Bip001 {suffix} Foot")
    loafer = ellipsoid("Mother_Loafer." + suffix, (foot.x, -0.058, 0.047), (0.105, 0.155, 0.065), MAT_LOAFER, 44, 28)
    tube_curve("Mother_LoaferVamp." + suffix, ((foot.x - 0.070, -0.170, 0.062), (foot.x, -0.195, 0.077), (foot.x + 0.070, -0.170, 0.062)), 0.008, MAT_BROWN_LIGHT)

# Mother-specific shoulder-length chestnut bob and low half-up twist.  The
# coherent rear/crown masses give skull depth; pointed overlapping clumps form
# bangs and shoulder tips without twin loops, mounts or a long donor tail.
ellipsoid("Mother_HairBackVolume", (0.0, 0.095, 1.455), (0.258, 0.205, 0.245), MAT_BROWN, 52, 34)
ellipsoid("Mother_HairCrownVolume", (0.0, 0.015, 1.610), (0.258, 0.208, 0.174), MAT_BROWN, 48, 32)

bang_specs = (
    ("BangOuter.L", ((-0.205, -0.045, 1.665), (-0.230, -0.155, 1.535), (-0.210, -0.190, 1.365)), (0.075, 0.068, 0.006), MAT_BROWN),
    ("BangMid.L", ((-0.115, -0.090, 1.695), (-0.130, -0.185, 1.560), (-0.105, -0.205, 1.420)), (0.066, 0.060, 0.006), MAT_BROWN_LIGHT),
    ("BangCentre.L", ((-0.040, -0.105, 1.705), (-0.050, -0.195, 1.565), (-0.032, -0.210, 1.445)), (0.056, 0.050, 0.005), MAT_BROWN),
    ("BangCentre.R", ((0.040, -0.105, 1.705), (0.052, -0.195, 1.570), (0.038, -0.210, 1.455)), (0.056, 0.050, 0.005), MAT_BROWN_LIGHT),
    ("BangMid.R", ((0.120, -0.085, 1.695), (0.140, -0.180, 1.560), (0.115, -0.202, 1.415)), (0.066, 0.060, 0.006), MAT_BROWN),
    ("BangOuter.R", ((0.205, -0.040, 1.665), (0.232, -0.150, 1.530), (0.214, -0.188, 1.360)), (0.075, 0.068, 0.006), MAT_BROWN_LIGHT),
    ("SideLock.L", ((-0.235, 0.010, 1.580), (-0.265, -0.075, 1.390), (-0.235, -0.115, 1.175)), (0.075, 0.062, 0.006), MAT_BROWN),
    ("SideLock.R", ((0.235, 0.010, 1.580), (0.265, -0.075, 1.390), (0.235, -0.115, 1.175)), (0.075, 0.062, 0.006), MAT_BROWN),
    ("BackTip.L", ((-0.150, 0.215, 1.560), (-0.205, 0.225, 1.350), (-0.190, 0.145, 1.155)), (0.070, 0.060, 0.006), MAT_BROWN_LIGHT),
    ("BackTip.R", ((0.150, 0.215, 1.560), (0.205, 0.225, 1.350), (0.190, 0.145, 1.155)), (0.070, 0.060, 0.006), MAT_BROWN_LIGHT),
)
for name, points, radii, material in bang_specs:
    tapered_clump("Mother_" + name, points, radii, material)

# Six staggered shoulder layers break the spherical bob silhouette and make
# the intended shoulder-length cut readable from front, 3/4 and side.  Their
# narrow tapered tips avoid the rejected twin-loop or helmet-ball impression.
shoulder_layer_specs = (
    ("ShoulderLayerOuter.L", ((-0.225, 0.080, 1.515), (-0.285, 0.040, 1.335), (-0.270, -0.005, 1.135)), (0.067, 0.052, 0.004), MAT_BROWN),
    ("ShoulderLayerMid.L", ((-0.165, 0.185, 1.515), (-0.225, 0.175, 1.315), (-0.225, 0.115, 1.105)), (0.064, 0.050, 0.004), MAT_BROWN_LIGHT),
    ("ShoulderLayerBack.L", ((-0.080, 0.245, 1.505), (-0.125, 0.255, 1.300), (-0.135, 0.185, 1.090)), (0.060, 0.047, 0.004), MAT_BROWN),
    ("ShoulderLayerBack.R", ((0.080, 0.245, 1.505), (0.125, 0.255, 1.300), (0.135, 0.185, 1.090)), (0.060, 0.047, 0.004), MAT_BROWN_LIGHT),
    ("ShoulderLayerMid.R", ((0.165, 0.185, 1.515), (0.225, 0.175, 1.315), (0.225, 0.115, 1.105)), (0.064, 0.050, 0.004), MAT_BROWN),
    ("ShoulderLayerOuter.R", ((0.225, 0.080, 1.515), (0.285, 0.040, 1.335), (0.270, -0.005, 1.135)), (0.067, 0.052, 0.004), MAT_BROWN_LIGHT),
)
for name, points, radii, material in shoulder_layer_specs:
    tapered_clump("Mother_" + name, points, radii, material)

# Rear-facing layered ribbons cover the smooth support volume and keep the
# back view from reading as a sphere/empty helmet shell.
rear_layer_specs = (
    ("RearRibbonOuter.L", ((-0.205, 0.205, 1.585), (-0.245, 0.275, 1.365), (-0.235, 0.205, 1.145)), (0.060, 0.050, 0.004), MAT_BROWN_LIGHT),
    ("RearRibbonMid.L", ((-0.112, 0.245, 1.610), (-0.155, 0.305, 1.380), (-0.150, 0.230, 1.120)), (0.063, 0.052, 0.004), MAT_BROWN),
    ("RearRibbonCentre", ((0.000, 0.265, 1.620), (0.000, 0.325, 1.380), (0.012, 0.245, 1.105)), (0.067, 0.054, 0.004), MAT_BROWN_LIGHT),
    ("RearRibbonMid.R", ((0.112, 0.245, 1.610), (0.155, 0.305, 1.380), (0.150, 0.230, 1.120)), (0.063, 0.052, 0.004), MAT_BROWN),
    ("RearRibbonOuter.R", ((0.205, 0.205, 1.585), (0.245, 0.275, 1.365), (0.235, 0.205, 1.145)), (0.060, 0.050, 0.004), MAT_BROWN_LIGHT),
)
for name, points, radii, material in rear_layer_specs:
    tapered_clump("Mother_" + name, points, radii, material)

# A compact folded twist replaces the rejected round bun/donut.  Two crossed
# locks create the knot and one short tapered tail makes the half-up structure
# readable without becoming twin-tails.
tapered_clump(
    "Mother_HalfUpFold.L",
    ((-0.115, 0.325, 1.555), (-0.040, 0.365, 1.520), (0.025, 0.355, 1.505)),
    (0.036, 0.030, 0.006),
    MAT_BROWN_LIGHT,
)
tapered_clump(
    "Mother_HalfUpFold.R",
    ((0.115, 0.325, 1.550), (0.040, 0.372, 1.535), (-0.020, 0.358, 1.505)),
    (0.036, 0.030, 0.006),
    MAT_BROWN,
)
ellipsoid("Mother_HalfUpKnot", (0.0, 0.355, 1.515), (0.050, 0.032, 0.032), MAT_BROWN, 32, 22)
tapered_clump(
    "Mother_HalfUpShortTail",
    ((0.0, 0.350, 1.505), (0.015, 0.365, 1.425), (0.030, 0.330, 1.330)),
    (0.045, 0.032, 0.004),
    MAT_BROWN_LIGHT,
)

# Pearl studs and a small analogue watch make the role change unambiguous.
ellipsoid("Mother_PearlStud.L", (-0.238, -0.018, 1.345), (0.027, 0.022, 0.027), MAT_PEARL, 32, 22)
ellipsoid("Mother_PearlStud.R", (0.238, -0.018, 1.345), (0.027, 0.022, 0.027), MAT_PEARL, 32, 22)
watch_wrist = bone_head("Bip001 L Hand")
ellipsoid("Mother_Watch", (watch_wrist.x, watch_wrist.y - 0.012, watch_wrist.z + 0.030), (0.045, 0.052, 0.026), MAT_METAL, 32, 22)

bpy.context.view_layer.update()
# Transparent-preserved source costume and tail components would otherwise
# still produce Freestyle silhouette lines.  The Mother render uses baked
# source linework plus explicit garment seams, so disable geometric Freestyle
# after the original-reference set; hidden tech/twin-tail forms then vanish.
scene.render.use_freestyle = False
mother_outputs = render_set("mother-owned-mika-conversion-proof5")

# Close-up evidence is mandatory for judging the face band, mouth adhesion,
# eyes, jaw, ears and half-up hair from all three decisive angles.
closeup_views = {
    "front": ((0.0, -4.2, 1.48), (0.0, 0.0, 1.44)),
    "three-quarter": ((2.9, -3.45, 1.50), (0.0, 0.0, 1.44)),
    "side": ((4.25, 0.0, 1.48), (0.0, 0.0, 1.44)),
}
full_body_scale = camera.data.ortho_scale
camera.data.ortho_scale = 0.84
mother_closeups = []
for label, (location, target) in closeup_views.items():
    position_camera(location, target)
    closeup_path = os.path.join(OUTPUT, f"mother-owned-mika-conversion-proof5-{label}-closeup.png")
    scene.render.filepath = closeup_path
    bpy.ops.render.render(write_still=True)
    mother_closeups.append(closeup_path)
camera.data.ortho_scale = full_body_scale

blend_path = os.path.join(OUTPUT, "mother-owned-mika-conversion-proof5.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

rig_unchanged = len(armature.data.bones) == source_bone_count and len(body.vertex_groups) == source_vertex_group_count
mouth_only_topology_repair = (
    len(body.data.vertices) == source_vertex_count - removed_mouth_vertex_count
    and len(body.data.polygons) == source_polygon_count - removed_mouth_polygon_count
)

receipt = {
    "status": "FEASIBILITY_PROOF_REVIEW_REQUIRED",
    "proofRevision": "MotherProof5",
    "productionEligible": False,
    "unityModified": False,
    "docsModified": False,
    "source": {
        "fbx": FBX_PATH,
        "sha256": sha256(FBX_PATH),
        "userOwnershipAttested": True,
        "test3OrSakurakoUsed": False,
    },
    "preservation": {
        "sourceBodyVertexCount": source_vertex_count,
        "sourceBodyPolygonCount": source_polygon_count,
        "sourceArmatureBoneCount": source_bone_count,
        "sourceVertexGroupCount": source_vertex_group_count,
        "bodyTopologyOtherwisePreservedAfterMouthRepair": mouth_only_topology_repair,
        "removedLooseMouthPatchPolygons": removed_mouth_polygon_count,
        "removedLooseMouthPatchVertices": removed_mouth_vertex_count,
        "armatureAndVertexGroupsUnchanged": rig_unchanged,
        "originalHandsRetainedAndRecoloredPolygons": preserved_hand_polygon_count,
        "hiddenLongHairPolygonsPreservedInTopology": hidden_polygon_count,
    },
    "eyeMouthAlphaRepair": {
        "required": True,
        "method": "near-black color-key to smooth alpha; no opaque eye/mouth polygon patch",
        "originalDerivedTexture": os.path.basename(restored_eye_path),
        "motherDerivedTexture": os.path.basename(mother_eye_path),
    },
    "mouthReplacement": {
        "type": "short five-point shallow-U 3D curve",
        "material": "warm rose",
        "bevelRadiusMeters": 0.0048,
        "surfaceFit": "embedded into measured Mika face contour; no detached side gap",
    },
    "hairReplacement": {
        "sourceHairVisible": False,
        "style": "chestnut shoulder-length layered cut with folded low half-up twist",
        "addedTaperedShoulderLayers": 6,
        "addedRearSurfaceLayers": 5,
    },
    "motherIdentityBlockout": [
        "chestnut shoulder-length hair with low half-up twist",
        "brown eyes",
        "dusty-peach cardigan",
        "cream blouse and collar",
        "deep-teal A-line skirt",
        "dark-brown loafers",
        "pearl studs and analogue watch",
    ],
    "limitations": [
        "added garment/hair/accessory blockout geometry is not yet skinned",
        "source youthful face proportions require a later controlled Mother-age sculpt",
        "proof is static review only and is not a final family model",
    ],
    "originalViews": [os.path.basename(path) for path in original_outputs],
    "motherViews": [os.path.basename(path) for path in mother_outputs],
    "motherCloseups": [os.path.basename(path) for path in mother_closeups],
    "blend": os.path.basename(blend_path),
}
with open(os.path.join(OUTPUT, "mother-owned-mika-conversion-proof5-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("MOTHER_OWNED_MIKA_CONVERSION_PROOF5: REVIEW_REQUIRED")
