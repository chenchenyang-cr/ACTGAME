import json,math
D=json.load(open('D:/19356/UnityLearning/animation_inspect/detail.json',encoding='utf8'))
def norm(m):return [[x/math.sqrt(sum(y*y for y in row)) for x in row] for row in m[:3]]
def ang(a,b):
 a,b=norm(a),norm(b)
 return math.degrees(math.acos(max(-1,min(1,(sum(a[i][j]*b[i][j] for i in range(3) for j in range(3))-1)/2))))
def walk(c,path=''):
 print(path,c.get('class'),c.get('props'))
 for k in c.get('keys',[]):print(' KEY',k)
 for s in c.get('subs',[]):
  if isinstance(s['data'],dict):walk(s['data'],path+'/'+s['name'])
for name,d in D.items():
 if name=='meshes': print(d);continue
 print('\nNODE',name)
 walk(d['controller'])
 for typ in ['world','local']:
  for a,b in [(3,4),(4,5),(5,6),(6,7)]:
   ss=[x for x in d['samples'] if a<=x['t']<=b]
   steps=[(ang(x[typ],y[typ]),y['t']) for x,y in zip(ss,ss[1:])]
   print(typ,a,b,'net',ang(ss[0][typ],ss[-1][typ]),'path',sum(x[0] for x in steps),'peak',max(steps))
