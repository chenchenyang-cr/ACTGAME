exec(open('D:/19356/UnityLearning/animation_inspect/summarize.py').read().split('for name,d in D.items():')[0])
C=json.load(open('D:/19356/UnityLearning/animation_inspect/controllers.json',encoding='utf8'))
for name in ['Bn_katana_hilt','Bip001 R Hand','Bip001 R UpperArm']:
 print(name)
 rot=C[name]['controller']['subs'][1]['data']
 for sub in rot['subs']:
  print(sub['name'].encode('ascii','replace').decode(),[(k['time'],round(float(k['props']['value']),2)) for k in sub['data'].get('keys',[]) if k['time'] in ['4f','5f','6f','7f']])
for a,b in [('Bn_katana_hilt','Bip002 Prop1'),('Bip001 R Hand','Bip002 R Hand')]:
 diff=[(ang(x['world'],y['world']),x['t']) for x,y in zip(D[a]['samples'],D[b]['samples']) if 5<=x['t']<=6]
 print('DIFF',a,b,'peak',max(diff),'integer',[x for x in diff if x[1] in [5,6]])
def rel(a,b):
 aa,bb=norm(a),norm(b)
 return [[sum(aa[i][k]*bb[j][k] for k in range(3)) for j in range(3)] for i in range(3)]
for a,b in [('Bn_katana_hilt','Bip001 R Hand'),('Bip002 Prop1','Bip002 R Hand')]:
 seq=[(x['t'],rel(x['world'],y['world'])) for x,y in zip(D[a]['samples'],D[b]['samples']) if 5<=x['t']<=6]
 ref=seq[0][1]
 print('GRIP',a,'peak rotation drift',max((ang(ref,m),t) for t,m in seq),'end',ang(ref,seq[-1][1]))
