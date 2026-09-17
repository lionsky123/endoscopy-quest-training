"""Blender background import, structure report and preview; never modifies the source FBX."""
import bpy, json, sys, math
import inspect
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/model'
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
# Blender 5.2's bundled FBX importer still writes a removed Cycles field.
# Patch that single importer function in memory for this process; do not alter the installation.
from io_scene_fbx import import_fbx
light_source=inspect.getsource(import_fbx.blen_read_light).replace('lamp.cycles.cast_shadow = lamp.use_shadow','pass')
exec(light_source,import_fbx.__dict__)
bpy.ops.import_scene.fbx(filepath=str(root / '01_原始资料/清洗室模型.fbx'))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector(tuple(min(v[i] for v in corners) for i in range(3)))
hi = Vector(tuple(max(v[i] for v in corners) for i in range(3)))
center = (lo + hi) / 2
extent = max(hi-lo)
report = {'mesh_count':len(meshes), 'bounds_min':list(lo), 'bounds_max':list(hi),
          'vertices':sum(len(o.data.vertices) for o in meshes),
          'polygons':sum(len(o.data.polygons) for o in meshes),
          'materials':len(bpy.data.materials),
          'images':[{'name':i.name,'packed':bool(i.packed_file),'path':i.filepath} for i in bpy.data.images]}
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=center+Vector((extent*.9,-extent*1.1,extent*.85)))
cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.6;cam.data.clip_end=extent*20
scene.camera=cam
scene.render.filepath=str(out/'overview.png')
bpy.ops.render.render(write_still=True)
for obj in meshes:
    box=[obj.matrix_world @ Vector(c) for c in obj.bound_box]
    zmin=min(v.z for v in box);zmax=max(v.z for v in box)
    if zmin>2.5: obj.hide_render=True
scene.render.filepath=str(out/'cutaway.png')
bpy.ops.render.render(write_still=True)
(out/'objects.json').write_text(json.dumps([{'name':o.name,'location':list(o.location),'dimensions':list(o.dimensions)} for o in meshes],ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
