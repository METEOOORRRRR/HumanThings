import hashlib, json, pathlib, struct

ROOT = pathlib.Path(r'D:\HumanThings')
SOURCE = ROOT / 'Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb'
OUT = ROOT / 'QA/ToxicBunnyTexture'
OUT.mkdir(parents=True, exist_ok=True)
raw = SOURCE.read_bytes()
chunks = []
offset = 12
while offset < len(raw):
    length, kind = struct.unpack_from('<II', raw, offset)
    chunks.append((kind, raw[offset+8:offset+8+length]))
    offset += 8 + length
doc = json.loads(chunks[0][1])
binary = chunks[1][1]
summary = {k:doc.get(k) for k in ['asset','materials','textures','images','extensionsUsed','extensionsRequired']}
summary['meshes'] = [{ 'name':m.get('name'), 'primitives':[{k:v for k,v in p.items() if k in ['attributes','indices','material','extensions']} for p in m['primitives']]} for m in doc['meshes']]
summary['animations'] = [{'name':a.get('name'), 'channels':len(a['channels'])} for a in doc.get('animations',[])]
summary['sha256'] = hashlib.sha256(raw).hexdigest()
for i, img in enumerate(doc.get('images',[])):
    if 'bufferView' in img:
        bv = doc['bufferViews'][img['bufferView']]
        start = bv.get('byteOffset',0)
        suffix = '.png' if img['mimeType']=='image/png' else '.jpg'
        path = OUT / ('original_image_%02d' % i + suffix)
        path.write_bytes(binary[start:start+bv['byteLength']])
        img['extracted'] = str(path)
(OUT / 'source_inspection.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
