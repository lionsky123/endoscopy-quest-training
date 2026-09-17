"""Blender: convert publisher meshes for Unity; no generated placeholder geometry."""
from pathlib import Path
import bpy,json,shutil
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'app/Assets/Endoscopy/Resources/WebAssets'
OUT.mkdir(parents=True,exist_ok=True)
report=[]
for asset,length in [('clipboard',.36),('plastic_bottle_gallon',.32)]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'05_网络资源'/asset/(asset+'.gltf')))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    verts=[o.matrix_world@Vector(corner) for o in meshes for corner in o.bound_box]
    low=Vector([min(p[i] for p in verts) for i in range(3)])
    high=Vector([max(p[i] for p in verts) for i in range(3)])
    scale=length/max(high-low)
    origin=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
    for obj in meshes:
        world=obj.matrix_world.copy()
        for vert in obj.data.vertices:vert.co=(world@vert.co-origin)*scale
        obj.parent=None;obj.matrix_world.identity()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:obj.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(asset+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    folder=ROOT/'05_网络资源'/asset/'textures'
    for texture in folder.glob('*.jpg'):shutil.copy2(texture,OUT/texture.name)
    report.append({'asset':asset,'source_bounds':[list(low),list(high)],'uniform_scale':scale,'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'meshes':[o.name for o in meshes]})
for asset in ['Metal032','Plastic010']:
    for suffix in ['Color','NormalGL','Roughness']:
        for texture in (ROOT/'05_网络资源'/asset).glob('*_'+suffix+'.jpg'):shutil.copy2(texture,OUT/texture.name)
(ROOT/'artifacts/web-assets-preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('WEB_ASSETS_PREPARED',json.dumps(report),flush=True)
