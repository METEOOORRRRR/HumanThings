"""Replace material references only; retain every original binary byte."""
import hashlib, json, pathlib, struct, sys
ROOT=pathlib.Path(r'D:\HumanThings'); QA=ROOT/'QA/ToxicBunnyTexture'
source=ROOT/'Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb'
texture=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else QA/'ToxicBunny_Clean_BaseColor_v1.png'
output=pathlib.Path(sys.argv[2]) if len(sys.argv)>2 else QA/'ToxicBunny_Clean_v1.glb'
if len(sys.argv)>4: source=pathlib.Path(sys.argv[4])
material_name=sys.argv[5] if len(sys.argv)>5 else 'ToxicBunny'
raw=source.read_bytes();json_len=struct.unpack_from('<I',raw,12)[0]
doc=json.loads(raw[20:20+json_len]); original_doc=json.loads(raw[20:20+json_len])
start=20+json_len;bin_len=struct.unpack_from('<I',raw,start)[0];binary=raw[start+8:start+8+bin_len]
original_binary=binary
while len(binary)%4:binary+=b'\0'
offset=len(binary);png=texture.read_bytes();binary+=png
doc['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(png)})
doc['images'][1]['bufferView']=len(doc['bufferViews'])-1
doc['images'][1]['name']=material_name+'_Clean_BaseColor'
normal_path=pathlib.Path(sys.argv[3]) if len(sys.argv)>3 and sys.argv[3]!='-' else None
if normal_path:
    while len(binary)%4:binary+=b'\0'
    normal_offset=len(binary);normal_png=normal_path.read_bytes();binary+=normal_png
    doc['bufferViews'].append({'buffer':0,'byteOffset':normal_offset,'byteLength':len(normal_png)})
    doc['images'][0]['bufferView']=len(doc['bufferViews'])-1
    doc['images'][0]['name']=material_name+'_Clean_Normal'
pbr=doc['materials'][0]['pbrMetallicRoughness']
pbr.pop('metallicRoughnessTexture',None)
pbr['metallicFactor']=0.0;pbr['roughnessFactor']=0.82
doc['materials'][0]['name']=material_name+'_Clean_Matte'
doc['materials'][0]['normalTexture']['scale']=1.0 if normal_path else 0.0
doc['buffers'][0]['byteLength']=len(binary)
while len(binary)%4:binary+=b'\0'
new_json=json.dumps(doc,separators=(',',':'),ensure_ascii=False).encode('utf-8')
new_json+=b' '*((-len(new_json))%4)
glb=struct.pack('<III',0x46546c67,2,28+len(new_json)+len(binary))+struct.pack('<II',len(new_json),0x4e4f534a)+new_json+struct.pack('<II',len(binary),0x004e4942)+binary
output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(glb)
keys=['meshes','nodes','skins','animations','accessors','scenes','scene','samplers']
for key in keys:assert doc.get(key)==original_doc.get(key),key
assert binary[:len(original_binary)]==original_binary
baseline=hashlib.sha256(raw).hexdigest() if len(sys.argv)>4 else json.loads((QA/'source_inspection.json').read_text())['sha256']
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline
report={'source_sha256':baseline,'source_unchanged':True,'geometry_uv_rig_animations_identical':True,'preserved_sections':keys,'original_binary_preserved_byte_for_byte':True,'animations':[a.get('name') for a in doc['animations']],'new_glb':str(output),'new_glb_sha256':hashlib.sha256(glb).hexdigest(),'texture':str(texture),'material':doc['materials'][0]}
output.with_suffix('.validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
