import pymxs,json,math,traceback,os
r=pymxs.runtime
base='D:/19356/UnityLearning/animation_inspect/'
def mat(m):return [[float(v) for v in (m[i].x,m[i].y,m[i].z)] for i in range(4)]
def sample():
 data={}
 for t in [i/4 for i in range(79) if not 20<i<24]:
  with pymxs.attime(t):data[str(t)]={str(n.name):mat(n.transform) for n in r.objects}
 return data
def meshes():
 data={}
 for t in [5,6]:
  with pymxs.attime(t):
   for n in r.geometry:
    if str(n.name).startswith('Unagi_Weapon'):
     mesh=r.snapshotAsMesh(n)
     data[str(t)+'/'+str(n.name)]=[[float(v.x),float(v.y),float(v.z)] for v in [r.getVert(mesh,i) for i in range(1,int(r.getNumVerts(mesh))+1)]]
 return data
try:
 before=sample();mbefore=meshes();count=int(r.objects.count)
 assert r.loadMaxFile((base+'Unagi_Attack_01_fixed.max').replace('/',chr(92)),quiet=True), 'Reopen failed'
 after=sample();mafter=meshes()
 maxdiff=0.;worst=None
 for t,ns in before.items():
  for name,a in ns.items():
   b=after[t][name];d=max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(3))
   if d>maxdiff:maxdiff=d;worst=[t,name]
 mesh_error=max(abs(a-b) for k,vs in mbefore.items() for va,vb in zip(vs,mafter[k]) for a,b in zip(va,vb))
 h=r.getNodeByName('Bip001 R Hand');n=r.getNodeByName('Bn_katana_hilt')
 def ang(a,b):
  aa=[[x/math.sqrt(sum(y*y for y in row)) for x in row] for row in a[:3]];bb=[[x/math.sqrt(sum(y*y for y in row)) for x in row] for row in b[:3]]
  return math.degrees(math.acos(max(-1,min(1,(sum(aa[i][j]*bb[i][j] for i in range(3) for j in range(3))-1)/2))))
 with pymxs.attime(5):ref=mat(n.transform*r.inverse(h.transform))
 grip=[]
 for i in range(800,961):
  with pymxs.attime(i/160):grip.append(ang(mat(n.transform*r.inverse(h.transform)),ref))
 textures=[str(x.filename) for x in r.getClassInstances(r.Bitmaptexture)]
 result={'reopened':True,'object_count':int(r.objects.count),'original_object_count':count,'outside_all_objects_max_matrix_error':maxdiff,'worst':worst,'endpoint_weapon_mesh_max_coordinate_error':mesh_error,'grip_max_degrees':max(grip),'textures':textures}
 with open(base+'reopen_validation.json','w',encoding='utf8') as f:json.dump(result,f,indent=2,ensure_ascii=False)
 assert count==int(r.objects.count)
 assert maxdiff<0.0001
 assert mesh_error<0.0001
 assert max(grip)<0.1
 with open(base+'reopen_validation.json','w',encoding='utf8') as f:json.dump(result,f,indent=2,ensure_ascii=False)
except:
 with open(base+'verify.error','w') as f:f.write(traceback.format_exc())
