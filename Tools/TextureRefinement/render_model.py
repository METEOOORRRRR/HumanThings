import bpy, math, pathlib, sys, json
from mathutils import Vector

ROOT = pathlib.Path(r'D:\HumanThings')
args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
source = args[0] if args else str(ROOT/'Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb')
tag = args[1] if len(args)>1 else 'original'
texture = args[2] if len(args)>2 else None
texture = texture if texture and texture != '-' else None
qa = pathlib.Path(args[3]) if len(args)>3 else ROOT/'QA/ToxicBunnyTexture'
face_height = float(args[4]) if len(args)>4 else 1.43
face_scale = float(args[5]) if len(args)>5 else .56
flat = tag=='projection_source' or tag.startswith('flat_')
OUT = qa / tag
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=source)
for ob in bpy.context.scene.objects:
    if ob.type=='ARMATURE':
        ob.animation_data_clear()
        ob.data.pose_position='REST'
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
print('BOUNDING_BOX', [tuple(mesh.matrix_world @ Vector(c)) for c in mesh.bound_box])
mat=mesh.data.materials[0]
if texture:
    bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node=mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image=bpy.data.images.load(texture)
    mat.node_tree.links.new(node.outputs['Color'], bsdf.inputs['Base Color'])
    for key,value in [('Metallic',0.0),('Roughness',.82),('Normal',None)]:
        for link in list(bsdf.inputs[key].links): mat.node_tree.links.remove(link)
        if value is not None: bsdf.inputs[key].default_value=value
if flat:
    bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    base=bsdf.inputs['Base Color'].links[0].from_socket
    emission=mat.node_tree.nodes.new('ShaderNodeEmission')
    mat.node_tree.links.new(base,emission.inputs['Color'])
    output=next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL')
    mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.eevee.use_gtao=True
scene.eevee.gtao_distance=0.025
scene.eevee.gtao_factor=0.6
scene.eevee.taa_render_samples=64
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.world.color=(0.45,0.45,0.45)
scene.view_settings.view_transform='Standard'
scene.view_settings.look='None'
if flat: scene.view_settings.look='None'
scene.view_settings.exposure=0
scene.view_settings.gamma=1
world=scene.world
world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(0.34,0.37,0.42,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=0.65

def area(name,pos,power,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=pos
    ob.rotation_euler=(Vector((0,0,0.85))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Key',(2,-3,4),200,4)
area('Fill',(-3,-1,2),140,3)
area('Rim',(1,3,3),160,3)
cam_data=bpy.data.cameras.new('ReviewCamera'); cam=bpy.data.objects.new('ReviewCamera',cam_data)
scene.collection.objects.link(cam);scene.camera=cam;cam_data.type='ORTHO';cam_data.lens=50
views=[('front',(0,-4,0.85),(0,0,0.82),1.8),('back',(0,4,0.85),(0,0,0.82),1.8),('left',(-4,0,0.85),(0,0,0.82),1.8),('right',(4,0,0.85),(0,0,0.82),1.8),('face',(0,-4,face_height),(0,0,face_height),face_scale),('threequarter',(2.8,-4,1.0),(0,0,0.86),1.8)]
for name,pos,target,scale in views:
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam_data.ortho_scale=scale
    scene.render.resolution_x=900 if name=='face' else 700
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    if flat:
        scene.render.resolution_x=1024 if name=='face' else 768
        scene.render.resolution_y=1024
        scene.render.film_transparent=True
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    print('RENDERED',name,flush=True)
if tag=='final':
    arm=next(o for o in scene.objects if o.type=='ARMATURE')
    arm.data.pose_position='POSE';arm.animation_data_create()
    for action in bpy.data.actions:
        action.use_fake_user=True
        if action.name.startswith(('Walking','Running')):
            arm.animation_data.action=action
            scene.frame_set(int((action.frame_range[0]+action.frame_range[1])*.5))
            scene.render.filepath=str(OUT/(action.name.split('_')[0].lower()+'_sample.png'))
            bpy.ops.render.render(write_still=True)
            print('ANIMATION_VERIFIED',action.name,tuple(action.frame_range),flush=True)
    arm.animation_data.action=None;arm.data.pose_position='REST';scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.overlay.show_overlays=False
                area.spaces.active.region_3d.view_distance=2.3
                area.spaces.active.region_3d.view_location=Vector((0,0,.86))
                area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
    bpy.ops.file.pack_all()
if texture or tag=='final':
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'review.blend'))
