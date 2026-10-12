"""
NeonSmash Night Shift 2026-08-21 (manueller Lauf, Zyklus 3)
Erstellt ein neues Low-Poly Collectible-Objekt "LeafGem_Boost" passend zum
Gruen/Wald-Fee-Konzept, als optisches Gegenstueck zum bereits vorhandenen
CrystalShard_Boost (Blau/Wasser). Bewusst andere Silhouette (flacher,
fuenfeckiger Querschnitt statt hoher sechseckiger Spitze), damit beide
Boost-Typen auf kleinen Mobile-Screens sofort unterscheidbar sind
(Form + Farbe, siehe Night-Shift-Routine Abschnitt 13).

Lauf via: blender --background --factory-startup --python make_leafgem_boost.py

Erzeugt EINE eigenstaendige neue .blend-Datei (ueberschreibt NICHTS Bestehendes,
insbesondere NICHT die aktuell live in der Blender-GUI geoeffnete
FairyForest_Chibi_Comic.blend) sowie FBX- und glTF-Exporte im selben Ordner.
Laeuft als separater Headless-Blender-Prozess, beruehrt die laufende
interaktive Blender-Session nicht.
"""
import bpy
import bmesh
import math
import os

OUT_DIR = "/private/tmp/claude-501/-Users-sherano-Sheronyx-unity-neonsmash/e31c27c0-3349-44a8-90f9-ccac78b57d7b/scratchpad/blender_out"
os.makedirs(OUT_DIR, exist_ok=True)

# --- clean scene ---
bpy.ops.wm.read_factory_settings(use_empty=True)

# --- build low-poly leaf gem (flattened pentagonal bipyramid) ---
mesh = bpy.data.meshes.new("LeafGem_Boost_Mesh")
obj = bpy.data.objects.new("LeafGem_Boost", mesh)
bpy.context.collection.objects.link(obj)

bm = bmesh.new()

SIDES = 5           # pentagonal cross-section -> andere Silhouette als Crystal Shard (6 Seiten)
RADIUS = 0.26        # breiter als Crystal Shard (0.18) -> flacherer, gedrungenerer Look
HEIGHT_MID = 0.0
HEIGHT_TOP = 0.28    # deutlich niedrigere Spitze als Crystal Shard (0.55) -> "Blatt/Gem"-Silhouette
HEIGHT_BOTTOM = -0.18

ring_verts = []
for i in range(SIDES):
    ang = (2 * math.pi / SIDES) * i + math.pi / 2  # eine Spitze zeigt nach oben (Y in Blender-Z-up)
    x = math.cos(ang) * RADIUS
    y = math.sin(ang) * RADIUS
    ring_verts.append(bm.verts.new((x, y, HEIGHT_MID)))

top_vert = bm.verts.new((0, 0, HEIGHT_TOP))
bottom_vert = bm.verts.new((0, 0, HEIGHT_BOTTOM))

bm.verts.ensure_lookup_table()

for i in range(SIDES):
    v1 = ring_verts[i]
    v2 = ring_verts[(i + 1) % SIDES]
    bm.faces.new((v1, v2, top_vert))

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
obj.location = (0, 0, 0)

# --- simple emissive-style material (placeholder, matches mobile/low-poly unlit look) ---
mat = bpy.data.materials.new(name="LeafGem_Boost_Mat")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value = (0.25, 0.85, 0.35, 1.0)  # NeonSmash "Gruen"
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (0.25, 0.85, 0.35, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.2
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.1
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.2
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
blend_path = os.path.join(OUT_DIR, "LeafGem_Boost.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

# --- export FBX ---
fbx_path = os.path.join(OUT_DIR, "LeafGem_Boost.fbx")
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
gltf_path = os.path.join(OUT_DIR, "LeafGem_Boost.glb")
bpy.ops.export_scene.gltf(
    filepath=gltf_path,
    use_selection=True,
    export_format='GLB',
)

print("QA_RESULT_FINAL", result_info)
print("SAVED_BLEND", blend_path)
print("SAVED_FBX", fbx_path)
print("SAVED_GLTF", gltf_path)
