import json
D=json.load(open('D:/19356/UnityLearning/animation_inspect/controllers.json',encoding='utf8'))
def walk(c,p=''):
 print(p,c.get('class'),c.get('props'))
 for k in c.get('keys',[]):
  if k['time'] in ['3f','4f','5f','6f','7f','8f']:print(k)
 for s in c.get('subs',[]):
  if isinstance(s['data'],dict):walk(s['data'],p+'/'+s['name'])
for n in ['Bn_katana_hilt','Bip001 R Hand','Bip001 R UpperArm','Bip002 Prop1']:
 print(n);walk(D[n]['controller'])
