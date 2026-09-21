import json,math
D=json.load(open(r'D:\19356\UnityLearning\animation_inspect\samples.json',encoding='utf8'))
def delta(a,b):
 p=math.dist(a['p'],b['p']);q=abs(sum(x*y for x,y in zip(a['q'],b['q'])))/math.sqrt(sum(x*x for x in a['q'])*sum(x*x for x in b['q']));return round(p,4),round(math.degrees(2*math.acos(min(1,q))),3)
for n in D['nodes']:
 if any(s in n['name'] for s in ['katana_hilt','Bn_Weapon','Prop','R Hand','L Hand']):
  print(n['name']);s={a['t']:a for a in n['samples']}
  for a,b in [(3,4),(4,5),(5,6),(6,7),(7,8),(4.99,5),(5,5.01),(5,5.25),(5.25,5.5),(5.5,5.75),(5.75,5.99),(5.99,6),(6,6.01)]:print(a,b,delta(s[a],s[b]))
