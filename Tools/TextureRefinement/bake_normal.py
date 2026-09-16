"""Bake a smoother shading normal into a map; mesh data stays unchanged."""
import numpy as np, pathlib
from scipy.spatial import cKDTree
from scipy.ndimage import distance_transform_edt
from numba import njit
from PIL import Image
QA=pathlib.Path(r'D:\HumanThings\QA\ToxicBunnyTexture');SIZE=4096
s=np.load(QA/'surface.npz');p=s['pos'];n=s['norm'];t=s['tangent'];sign=s['sign']
area=np.linalg.norm(np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]),axis=1)
unique,inverse=np.unique(np.round(p.reshape(-1,3),6),axis=0,return_inverse=True)
weighted=np.zeros_like(unique);weights=np.zeros(len(unique))
np.add.at(weighted,inverse,(n*area[:,None,None]).reshape(-1,3));np.add.at(weights,inverse,np.repeat(area,3))
weighted/=np.maximum(np.linalg.norm(weighted,axis=1,keepdims=True),1e-9)
tree=cKDTree(unique);neighbors=tree.query_ball_point(unique,.04,workers=-1)
smoothed=np.zeros_like(unique)
for i,ids in enumerate(neighbors):
    ids=np.array(ids);d=np.linalg.norm(unique[ids]-unique[i],axis=1)
    angular=np.maximum(weighted[ids]@weighted[i],0.0)**2
    w=np.exp(-(d/.022)**2)*np.sqrt(weights[ids])*angular
    smoothed[i]=(weighted[ids]*w[:,None]).sum(axis=0)/max(w.sum(),1e-9)
smoothed/=np.maximum(np.linalg.norm(smoothed,axis=1,keepdims=True),1e-9)
desired=smoothed[inverse].reshape(n.shape)
desired=.85*desired+.15*n;desired/=np.maximum(np.linalg.norm(desired,axis=2,keepdims=True),1e-9)
b=np.cross(n,t)*sign[:,:,None]
local=np.stack(((desired*t).sum(axis=2),(desired*b).sum(axis=2),(desired*n).sum(axis=2)),axis=2)
local/=np.maximum(np.linalg.norm(local,axis=2,keepdims=True),1e-9)
uv=s['uv'].copy();uv[:,:,0]=uv[:,:,0]*SIZE-.5;uv[:,:,1]=(1-uv[:,:,1])*SIZE-.5

@njit(cache=True)
def raster_norm(xy,values):
    out=np.zeros((SIZE,SIZE,3),np.float32);mask=np.zeros((SIZE,SIZE),np.uint8)
    for k in range(len(xy)):
        a,b,c=xy[k,0],xy[k,1],xy[k,2]
        det=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(det)<1e-9:continue
        xmin=max(0,int(np.ceil(min(a[0],b[0],c[0]))));xmax=min(SIZE-1,int(np.floor(max(a[0],b[0],c[0]))))
        ymin=max(0,int(np.ceil(min(a[1],b[1],c[1]))));ymax=min(SIZE-1,int(np.floor(max(a[1],b[1],c[1]))))
        for y in range(ymin,ymax+1):
            for x in range(xmin,xmax+1):
                w0=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/det
                w1=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/det;w2=1-w0-w1
                if min(w0,w1,w2)<-1e-5:continue
                for ch in range(3):out[y,x,ch]=w0*values[k,0,ch]+w1*values[k,1,ch]+w2*values[k,2,ch]
                mask[y,x]=1
    return out,mask
normal,mask=raster_norm(uv,local)
normal/=np.maximum(np.linalg.norm(normal,axis=2,keepdims=True),1e-9)
normal=np.clip((normal*.5+.5)*255+.5,0,255).astype(np.uint8)
normal[mask==0]=[128,128,255]
dist,near=distance_transform_edt(mask==0,return_indices=True);pad=(mask==0)&(dist<=12)
normal[pad]=normal[near[0][pad],near[1][pad]]
Image.fromarray(normal).save(QA/'ToxicBunny_Clean_Normal_v4.png')
print('Saved tangent-space normal correction; source mesh untouched.')
