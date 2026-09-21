import pymxs,json,traceback
r=pymxs.runtime
base='D:/19356/UnityLearning/animation_inspect/'
names=['Bn_katana_hilt','Bn_Weapon','Bip001 R Hand','Bip001 R Forearm','Bip001 R UpperArm','Bip002 R Hand','Bip002 R Forearm','Bip002 R UpperArm','Bip002 Prop1','Bip002 Prop2']
def props(x):
 d={}
 try:
  for p in r.getPropNames(x):
   try:d[str(p)]=str(r.getProperty(x,p))
   except:pass
 except:pass
 return d
def tree(c,depth=0):
 d={'class':str(r.classOf(c)),'props':props(c)}
 try:
  d['keys']=[{'time':str(k.time),'props':props(k)} for k in c.keys]
 except:pass
 if depth<5:
  d['subs']=[]
  try:
   for i in range(1,int(r.numSubs(c))+1):
    s=r.getSubAnim(c,i)
    try:cc=s.controller
    except:cc=None
    d['subs'].append({'name':str(s.name),'data':tree(cc,depth+1) if cc else str(s.value)})
  except:pass
 return d
def mat(m):return [[float(v) for v in (m[i].x,m[i].y,m[i].z)] for i in range(4)]
try:
 data={}
 for name in names:
  n=r.getNodeByName(name)
  d={'controller':tree(n.controller),'samples':[]}
  for i in range(480,1121):
   t=i/160
   with pymxs.attime(t):
    m=n.transform
    d['samples'].append({'t':t,'world':mat(m),'local':mat(m*r.inverse(n.parent.transform))})
  data[name]=d
 data['meshes']={}
 for n in r.geometry:
  if 'Weapon' in n.name:
   data['meshes'][str(n.name)]={'modifiers':[str(m) for m in n.modifiers]}
   for mod in n.modifiers:
    if str(r.classOf(mod))=='Skin':
     data['meshes'][str(n.name)]['bones']=[str(r.skinOps.GetBoneName(mod,i,0)) for i in range(1,r.skinOps.GetNumberBones(mod)+1)]
 with open(base+'detail.json','w',encoding='utf8') as f:json.dump(data,f,ensure_ascii=False)
except:
 with open(base+'detail.error','w') as f:f.write(traceback.format_exc())

