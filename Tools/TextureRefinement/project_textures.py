"""Bake imagegen-painted orthographic views into the original mesh UVs.
No mesh, skin, animation, or source image is modified. Numerical operations
here implement 3D texture projection, visibility, resampling and UV padding.
"""
import pathlib, json, numpy as np, cv2
from PIL import Image
from scipy.ndimage import distance_transform_edt
from scipy.spatial import cKDTree
from numba import njit

ROOT=pathlib.Path(r'D:\HumanThings'); QA=ROOT/'QA/ToxicBunnyTexture'
SIZE=4096
surf=np.load(QA/'surface.npz'); pos=surf['pos']; uv=surf['uv']; norms=surf['norm']

@njit(cache=True)
def raster(xy, attributes, width, height, depth_test=False):
    out=np.zeros((height,width,attributes.shape[2]),np.float32)
    mask=np.zeros((height,width),np.uint8)
    depth=np.full((height,width),1e10,np.float32)
    for t in range(len(xy)):
        a,b,c=xy[t,0],xy[t,1],xy[t,2]
        det=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(det)<1e-9:continue
        xmin=max(0,int(np.ceil(min(a[0],b[0],c[0]))));xmax=min(width-1,int(np.floor(max(a[0],b[0],c[0]))))
        ymin=max(0,int(np.ceil(min(a[1],b[1],c[1]))));ymax=min(height-1,int(np.floor(max(a[1],b[1],c[1]))))
        for y in range(ymin,ymax+1):
            for x in range(xmin,xmax+1):
                w0=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/det
                w1=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/det
                w2=1-w0-w1
                if min(w0,w1,w2)<-1e-5:continue
                dep=w0*attributes[t,0,0]+w1*attributes[t,1,0]+w2*attributes[t,2,0]
                if depth_test and dep>=depth[y,x]:continue
                depth[y,x]=dep;mask[y,x]=1
                for k in range(attributes.shape[2]):
                    out[y,x,k]=w0*attributes[t,0,k]+w1*attributes[t,1,k]+w2*attributes[t,2,k]
    return out,mask

def camera(coords, name, width, height):
    definitions={'front':((0,-4,.85),(0,0,.82),1.8),'back':((0,4,.85),(0,0,.82),1.8),'right':((4,0,.85),(0,0,.82),1.8),'left':((-4,0,.85),(0,0,.82),1.8),'face':((0,-4,1.43),(0,0,1.43),.56)}
    origin,target,scale=definitions[name];origin=np.array(origin);target=np.array(target)
    f=target-origin;f/=np.linalg.norm(f);r=np.cross(f,[0,0,1]);r/=np.linalg.norm(r);u=np.cross(r,f)
    d=coords-target
    px=(d@r/(scale*width/height)+.5)*width-.5
    py=(.5-d@u/scale)*height-.5
    depth=(coords-origin)@f
    return np.stack((px,py),axis=-1).astype(np.float32),depth.astype(np.float32),-f

cache=QA/'uv_surface_4096.npz'
if cache.exists():
    c=np.load(cache);field=c['field'];mask=c['mask']
else:
    xy=uv.copy();xy[:,:,0]=xy[:,:,0]*SIZE-.5;xy[:,:,1]=(1-xy[:,:,1])*SIZE-.5
    field,mask=raster(xy,np.concatenate((pos,norms),axis=2),SIZE,SIZE)
    np.savez_compressed(cache,field=field,mask=mask)
print('UV texels',mask.sum(),flush=True)
ys,xs=np.where(mask>0)
points=field[ys,xs,:3];normal=field[ys,xs,3:];normal/=np.maximum(np.linalg.norm(normal,axis=1,keepdims=True),1e-6)
del field

sheet=np.asarray(Image.open(QA/'turnaround_clean.png').convert('RGB'))
face=np.asarray(Image.open(QA/'face_clean.png').convert('RGB'))
views={v:sheet[:,i*(sheet.shape[1]//4):(i+1)*(sheet.shape[1]//4)] for i,v in enumerate(['front','right','back','left'])}
views['front']=np.asarray(Image.open(QA/'front_clean_v5.png').convert('RGB'))
views['face']=face
color_sum=np.zeros((len(points),3),np.float32);weight_sum=np.zeros(len(points),np.float32)

for name,im in views.items():
    h,w=im.shape[:2]
    # Extrapolate painted foreground a few pixels to keep the gray studio
    # backdrop out of edge texels when the generated silhouette drifts.
    rgb=im.astype(np.int16)
    bg=(rgb[:,:,2]-rgb[:,:,0]>3)&(rgb[:,:,2]-rgb[:,:,1]>0)&(rgb[:,:,0]>65)&(rgb[:,:,0]<165)&(abs(rgb[:,:,1]-rgb[:,:,0])<20)
    count,labels,stats,_=cv2.connectedComponentsWithStats(bg.astype(np.uint8),8)
    bg=np.isin(labels,np.where(stats[:,cv2.CC_STAT_AREA]>1000)[0]);bg[labels==0]=False
    dist,inds=distance_transform_edt(bg,return_indices=True)
    extended=im[inds[0],inds[1]]
    tri_xy,tri_depth,view_dir=camera(pos,name,w,h)
    depth_map,depth_mask=raster(tri_xy,tri_depth[:,:,None],w,h,True)
    depth_map=depth_map[:,:,0];depth_map[depth_mask==0]=1e10
    sample_xy,dep,_=camera(points,name,w,h)
    sx=sample_xy[:,0];sy=sample_xy[:,1]
    ix=np.clip(np.round(sx).astype(int),0,w-1);iy=np.clip(np.round(sy).astype(int),0,h-1)
    visible=(sx>=0)&(sx<w-1)&(sy>=0)&(sy<h-1)&(dep-depth_map[iy,ix]<.0045)
    # Meshy contains inconsistent vertex normals on thin folds/hair. Visibility
    # is determined by depth, while the angular preference is two-sided.
    priority=6.0 if name in ['front','back'] else 1.0
    weight=priority*np.maximum(np.abs(normal@view_dir)**4,.025)*visible
    if name=='face':
        # Dedicated face projection takes priority over the low-resolution
        # turnaround without spilling into shoulders or the side hood logos.
        fade_z=np.clip((points[:,2]-1.32)/.055,0,1)
        fade_x=np.clip((.205-np.abs(points[:,0]))/.055,0,1)
        weight*=100*fade_z*fade_x
    # Samples are flattened into <=32766-wide remap blocks (OpenCV limit).
    sampled=np.zeros((len(points),3),np.float32)
    for start in range(0,len(points),25000):
        end=min(len(points),start+25000)
        sampled[start:end]=cv2.remap(extended,sx[start:end,None],sy[start:end,None],cv2.INTER_LINEAR,borderMode=cv2.BORDER_REPLICATE)[:,0]
    color_sum+=sampled*weight[:,None];weight_sum+=weight
    print(name,'covered',int((weight>.01).sum()),flush=True)

original=np.asarray(Image.open(QA/'original_image_01.png').convert('RGB').resize((SIZE,SIZE),Image.Resampling.LANCZOS)).copy()
painted=color_sum/np.maximum(weight_sum[:,None],1e-10)
valid=weight_sum>1e-6
original[ys[valid],xs[valid]]=np.clip(painted[valid],0,255).astype(np.uint8)
# Extend the nearest painted 3D surface into occluded creases. This operates
# in world space rather than across unrelated neighboring UV islands.
known=np.where(valid)[0]
voxel=np.round(points[known]/.001).astype(np.int32)
_,unique=np.unique(voxel,axis=0,return_index=True)
support=known[unique]
tree=cKDTree(points[support])
missing=np.where(~valid)[0]
dist3,near3=tree.query(points[missing],k=1,workers=-1)
extend=dist3<.035
original[ys[missing[extend]],xs[missing[extend]]]=np.clip(painted[support[near3[extend]]],0,255).astype(np.uint8)
print('Occluded texels extended in 3D',int(extend.sum()),'of',len(missing),flush=True)
# Pad UV islands from their own nearest painted surface texels.
distance,near=distance_transform_edt(mask==0,return_indices=True)
pad=(mask==0)&(distance<=12)
original[pad]=original[near[0][pad],near[1][pad]]
output=QA/'ToxicBunny_Clean_BaseColor_v5.png'
Image.fromarray(original).save(output)
(QA/'projection_report.json').write_text(json.dumps({'texture_size':[SIZE,SIZE],'surface_texels':int(len(points)),'direct_projection_texels':int(valid.sum()),'direct_projection_percent':float(valid.mean()*100),'occluded_texels_extended_in_3d':int(extend.sum()),'total_retextured_percent':float((valid.sum()+extend.sum())/len(points)*100),'remaining_deeply_hidden_surface':'original albedo retained','uv_padding_pixels':12},indent=2))
print('SAVED',output,'coverage',valid.mean(),flush=True)
diagnostic=np.zeros((SIZE,SIZE,3),np.uint8);diagnostic[ys,xs]=[200,20,20];diagnostic[ys[valid],xs[valid]]=[20,200,20]
diagnostic[pad]=diagnostic[near[0][pad],near[1][pad]]
Image.fromarray(diagnostic).save(QA/'projection_coverage.png')
