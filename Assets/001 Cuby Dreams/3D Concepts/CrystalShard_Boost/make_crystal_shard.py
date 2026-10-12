"""
NeonSmash Night Shift Zyklus 5 - 2026-08-20
Erstellt ein neues Low-Poly Collectible-Objekt "CrystalShard_Boost" passend zum
Blau/Kristall-Fee-Konzept (Wasser/Kristall-Natur), gedacht als neues Tap/Boost-
Collectible fuer Special Modes / Diamant-Bonus-Idee.

Lauf via: blender --background --factory-startup --python make_crystal_shard.py

Erzeugt EINE eigenstaendige neue .blend-Datei (ueberschreibt NICHTS Bestehendes)
sowie FBX- und glTF-Exporte im selben Ordner.
"""
import bpy
import bmesh
import math
import os

OUT_DIR = "/private/tmp/claude-501/-Users-sherano-Sheronyx-unity-neonsmash/c3125566-7163-4e7c-9dc0-ac2d32163ca2/scratchpad/blender_out"
os.makedirs(OUT_DIR, exist_ok=True)

# --- clean scene ---
bpy.ops.wm.read_factory_settings(use_empty=True)

# --- build low-poly crystal shard (elongated bipyramid, hand-modeled via bmesh) ---
mesh = bpy.data.meshes.new("CrystalShard_Boost_Mesh")
obj = bpy.data.objects.new("CrystalShard_Boost", mesh)
bpy.context.collection.objects.link(obj)

bm = bmesh.new()

SIDES = 6          # hexagonal cross-section -> classic low-poly crystal look
RADIUS = 0.18
HEIGHT_MID = 0.0
HEIGHT_TOP = 0.55   # sharp tip
HEIGHT_BOTTOM = -0.35

ring_verts = []
for i in range(SIDES):
    ang = (2 * math.pi / SIDES) * i
    x = math.cos(ang) * RADIUS
    y = math.sin(ang) * RADIUS
    ring_verts.append(bm.verts.new((x, y, HEIGHT_MID)))

top_vert = bm.verts.new((0, 0, HEIGHT_TOP))
bottom_vert = bm.verts.new((0, 0, HEIGHT_BOTTOM))

bm.verts.ensure_lookup_table()

# side faces -> top
for i in range(SIDES):
    v1 = ring_verts[i]
    v2 = ring_verts[(i + 1) % SIDES]
    bm.faces.new((v1, v2, top_vert))

# side faces -> bottom
for i in range(SIDES):
    v1 = ring_verts[i]
    v2 = ring_verts[(i + 1) % SIDES]
    bm.faces.new((v2, v1, bottom_vert))

bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.to_mesh(mesh)
bm.free()

# --- origin / pivot at object center (world origin), matches spawner pivot convention ---
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
# recenter object at world origin (0,0,0) explicitly for prefab-import convenience
obj.location = (0, 0, 0)

# --- simple emissive-style material (placeholder, matches mobile/low-poly unlit look) ---
mat = bpy.data.materials.new(name="CrystalShard_Boost_Mat")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value = (0.2, 0.55, 1.0, 1.0)  # NeonSmash "Blau"
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (0.2, 0.55, 1.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.2
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.1
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.15
obj.data.materials.append(mat)

# --- QA metrics ---
poly_count = len(mesh.polygons)
vert_count = len(mesh.vertices)
dims = tuple(round(d, 4) for d in obj.dimensions)

# shade flat -> intentional low-poly facets (no auto-smooth, mobile-friendly)
for p in mesh.polygons:
    p.use_smooth = False

result_info = {
    "object_name": obj.name,
    "poly_count": poly_count,
    "vert_count": vert_count,
    "dimensions_m": dims,
    "origin": tuple(obj.location),
}
print("QA_RESULT", result_info)

# --- save new standalone .blend (does not touch any existing file) ---
blend_path = os.path.join(OUT_DIR, "CrystalShard_Boost.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

# --- export FBX ---
fbx_path = os.path.join(OUT_DIR, "CrystalShard_Boost.fbx")
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    object_types={'MESH'},
    mesh_smooth_type='OFF',
)

# --- export glTF ---
gltf_path = os.path.join(OUT_DIR, "CrystalShard_Boost.glb")
bpy.ops.export_scene.gltf(
    filepath=gltf_path,
    use_selection=True,
    export_format='GLB',
)

print("QA_RESULT_FINAL", result_info)
print("SAVED_BLEND", blend_path)
print("SAVED_FBX", fbx_path)
print("SAVED_GLTF", gltf_path)
