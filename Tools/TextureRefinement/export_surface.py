import bpy, numpy as np, pathlib, sys
from mathutils import Vector
ROOT=pathlib.Path(r'D:\HumanThings')
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
source=pathlib.Path(args[0]) if args else ROOT/'Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb'
output=pathlib.Path(args[1]) if len(args)>1 else ROOT/'QA/ToxicBunnyTexture/surface.npz'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
me=mesh.data;me.calc_loop_triangles();me.calc_normals_split();me.calc_tangents()
positions=np.array([tuple(mesh.matrix_world @ v.co) for v in me.vertices],dtype=np.float32)
tri=np.array([t.vertices[:] for t in me.loop_triangles])
loops=np.array([t.loops[:] for t in me.loop_triangles])
uv=np.array([l.uv[:] for l in me.uv_layers.active.data],dtype=np.float32)[loops]
norm=np.array([tuple(mesh.matrix_world.to_3x3() @ l.normal) for l in me.loops],dtype=np.float32)[loops]
tangent=np.array([tuple(mesh.matrix_world.to_3x3() @ l.tangent) for l in me.loops],dtype=np.float32)[loops]
sign=np.array([l.bitangent_sign for l in me.loops],dtype=np.float32)[loops]
np.savez_compressed(str(output),pos=positions[tri],uv=uv,norm=norm,tangent=tangent,sign=sign)
print('EXPORTED_TRIANGLES',len(tri))
