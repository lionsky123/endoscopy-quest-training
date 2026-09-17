"""Convert the supplied FBX into deterministic Unity runtime meshes, preserving all objects.

Run with Blender --background --python. Source FBX is never modified.
"""
import bpy, inspect, json, struct, math
from pathlib import Path
from mathutils import Vector
from io_scene_fbx import import_fbx

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'app/Assets/Endoscopy/Resources/WashingRoom'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
exec(inspect.getsource(import_fbx.blen_read_light).replace('lamp.cycles.cast_shadow = lamp.use_shadow','pass'),import_fbx.__dict__)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'01_原始资料/清洗室模型.fbx'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
corners=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
cx=(min(v.x for v in corners)+max(v.x for v in corners))/2
cy=(min(v.y for v in corners)+max(v.y for v in corners))/2
materials=[];mat_index={};texture_index={}
for mat in bpy.data.materials:
    mat_index[mat.name]=len(materials)
    color=list(mat.diffuse_color);texture=''
    if mat.use_nodes:
        bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if bsdf:
            color=list(bsdf.inputs['Base Color'].default_value)
            links=bsdf.inputs['Base Color'].links
            node=links[0].from_node if links else None
            image=node.image if node and node.type=='TEX_IMAGE' else None
            if image:
                if image.name not in texture_index:
                    key='texture_'+str(len(texture_index));texture_index[image.name]=key
                    w,h=image.size
                    if max(w,h)>1024:image.scale(max(1,round(w*1024/max(w,h))),max(1,round(h*1024/max(w,h))))
                    image.filepath_raw=str(OUT/(key+'.png'));image.file_format='PNG';image.save()
                texture=texture_index[image.name]
    materials.append({'name':mat.name,'color':color,'texture':texture})

def v3(v):return(v.x-cx,v.z,-(v.y-cy))
def n3(v):return(v.x,v.z,-v.y)
def string(f,s):
    raw=s.encode('utf-8');f.write(struct.pack('<i',len(raw)));f.write(raw)
metadata=[];triangle_total=0
with (OUT/'geometry.bytes').open('wb') as f:
    f.write(b'ECR1');f.write(struct.pack('<i',len(objects)))
    for obj in objects:
        obj.hide_render=False
        if len(obj.data.polygons)>6000:
            mod=obj.modifiers.new('RuntimeLOD','DECIMATE');mod.ratio=.2
        evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
        matrix=obj.matrix_world;normal_matrix=matrix.to_3x3().inverted().transposed()
        uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        # Expanded vertices preserve split normals and UV seams exactly.
        vertex_records=[];groups={}
        for tri in mesh.loop_triangles:
            material=mat_index[obj.data.materials[tri.material_index].name] if len(obj.data.materials)>tri.material_index else 0
            indices=[]
            for loop_index in tri.loops:
                loop=mesh.loops[loop_index];vertex=mesh.vertices[loop.vertex_index]
                pos=v3(matrix@vertex.co);normal=n3((normal_matrix@vertex.normal).normalized());tex=uv[loop_index].uv if uv else (0,0)
                indices.append(len(vertex_records));vertex_records.append((*pos,*normal,*tex))
            groups.setdefault(material,[]).extend(reversed(indices))
        string(f,obj.name);f.write(struct.pack('<i',len(vertex_records)))
        for record in vertex_records:f.write(struct.pack('<8f',*record))
        f.write(struct.pack('<i',len(groups)))
        for material,indices in groups.items():
            f.write(struct.pack('<ii',material,len(indices)));f.write(struct.pack('<'+'i'*len(indices),*indices))
        count=len(vertex_records)//3;triangle_total+=count
        metadata.append({'name':obj.name,'triangles':count,'center':list(v3(matrix@Vector((0,0,0))))})
        evaluated.to_mesh_clear()
(OUT/'manifest.json').write_text(json.dumps({'materials':materials,'objects':metadata,'triangles':triangle_total,'source_center':[cx,cy]},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'objects':len(objects),'triangles':triangle_total,'materials':len(materials),'textures':len(texture_index),'bytes':(OUT/'geometry.bytes').stat().st_size}))
