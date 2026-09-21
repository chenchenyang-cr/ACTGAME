import pymxs,json,math,traceback,hashlib
r=pymxs.runtime
qmatrix=r.execute('(fn toMatrixForRepair q = q as matrix3)')
base='D:/19356/UnityLearning/animation_inspect/'
src='D:/BaiduNetdiskDownload/039-雅/max/雅/Unagi_Attack_01.max'
dst=base+'Unagi_Attack_01_fixed.max'
def mat(m):return [[float(v) for v in (m[i].x,m[i].y,m[i].z)] for i in range(4)]
def angle(a,b):
 aa=[[x/math.sqrt(sum(y*y for y in row)) for x in row] for row in a[:3]]
 bb=[[x/math.sqrt(sum(y*y for y in row)) for x in row] for row in b[:3]]
 return math.degrees(math.acos(max(-1,min(1,(sum(aa[i][j]*bb[i][j] for i in range(3) for j in range(3))-1)/2))))
def props(k):
 return {str(p):r.getProperty(k,p) for p in r.getPropNames(k) if str(p) not in ['time','selected']}
try:
 original_hash=hashlib.sha256(open(src,'rb').read()).hexdigest()
 n=r.getNodeByName('Bn_katana_hilt'); hand=r.getNodeByName('Bip001 R Hand')
 controllers=[r.getPropertyController(r.getPropertyController(n.controller,r.Name('position')),r.Name(a)) for a in ['x_position','y_position','z_position']]+[r.getPropertyController(r.getPropertyController(n.controller,r.Name('rotation')),r.Name(a)) for a in ['x_rotation','y_rotation','z_rotation']]
 bounds=[]
 for c in controllers:
  bounds.append({int(k.time.ticks/160):props(k) for k in c.keys if float(k.time.ticks)/160 in [5,6]})
 outside=[i/4 for i in range(79) if not 20<i<24]
 before={str(t):mat(None) for t in []}
 for t in outside:
  with pymxs.attime(t):before[str(t)]=mat(n.transform)
 with pymxs.attime(5):off5=n.transform*r.inverse(hand.transform)
 with pymxs.attime(6):off6=n.transform*r.inverse(hand.transform)
 desired=[]
 baseline=[]
 for tick in range(800,961):
  t=tick/160;u=t-5
  with pymxs.attime(t):
   off=qmatrix(r.slerp(off5.rotation,off6.rotation,u))
   off.translation=off5.translation*(1-u)+off6.translation*u
   target=off*hand.transform
   desired.append((t,target))
   baseline.append(angle(mat(n.transform*r.inverse(hand.transform)),mat(off5)))
 for t,target in desired[1:-1]:
  with pymxs.attime(t):
   with pymxs.animate(True):n.transform=target
 for idx,c in enumerate(controllers):
  for k in c.keys:
   f=float(k.time.ticks)/160
   if 5<f<6:
    k.inTangentType=r.Name('linear');k.outTangentType=r.Name('linear')
  for k in c.keys:
   f=float(k.time.ticks)/160
   if f in [5,6]:
    p=bounds[idx][int(f)]
    for key in ['x_locked','y_locked','z_locked','freeHandle']:
     if key in p:
      try:r.setProperty(k,r.Name(key),False)
      except:pass
    for key,val in p.items():
     if key not in ['x_locked','y_locked','z_locked','freeHandle']:
      try:r.setProperty(k,r.Name(key),val)
      except:pass
    if f==5:k.outTangentType=r.Name('linear')
    else:k.inTangentType=r.Name('linear')
 errors=[];outside_errors=[];grip=[]
 for t,target in desired:
  with pymxs.attime(t):
   errors.append({'t':t,'angle':angle(mat(n.transform),mat(target)),'position':float(r.distance(n.transform.translation,target.translation))})
   grip.append(angle(mat(n.transform*r.inverse(hand.transform)),mat(off5)))
 for t in outside:
  with pymxs.attime(t):
   a,b=mat(n.transform),before[str(t)]
   outside_errors.append(max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(3)))
 result={'source':src,'output':dst,'source_sha256':original_hash,'max_target_angle_error':max(x['angle'] for x in errors),'max_target_position_error':max(x['position'] for x in errors),'max_outside_matrix_error':max(outside_errors),'grip_before_max':max(baseline),'grip_after_max':max(grip),'errors':errors}
 with open(base+'fix_validation.json','w') as f:json.dump(result,f,indent=2)
 assert result['max_target_angle_error']<0.1,result
 assert result['max_target_position_error']<0.0001,result
 assert result['max_outside_matrix_error']<0.0001,result
 r.execute('cbRepairLog = stringStream ""; callbacks.show to:cbRepairLog')
 with open(base+'save_callbacks.txt','w',encoding='utf8') as f:f.write(str(r.cbRepairLog))
 saved=r.saveMaxFile(dst.replace('/',chr(92)),quiet=True)
 with open(base+'save_status.json','w') as f:json.dump({'saved':saved,'nodes_after_save':int(r.objects.count),'filename':str(r.maxFileName),'path':str(r.maxFilePath)},f)
 assert saved, '3ds Max saveMaxFile returned false'
 assert original_hash==hashlib.sha256(open(src,'rb').read()).hexdigest()
except:
 with open(base+'fix.error','w') as f:f.write(traceback.format_exc())




