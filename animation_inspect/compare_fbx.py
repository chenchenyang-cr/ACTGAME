from fbx_read import *
b='D:/19356/UnityLearning/animation_inspect/'
a=Reader('D:/19356/UnityLearning/Assets/ThirdParty/UnagiAnimationPack/FBX/MAX_Variant/Unagi/01_Attack/Unagi_Attack_01.fbx');e=Reader(b+'Unagi_Attack_01_fixed_export.fbx')
def summary(r):
 objs=top(r,'Objects')['c'];models={n['p'][1].split('\x00')[0]:n for n in objs if n['name']=='Model'};cs=[n['p'] for n in top(r,'Connections')['c']];byid={n['p'][0]:n for n in objs}
 mid=models['Bn_katana_hilt']['p'][0];anims={c[1]:c[3] for c in cs if c[0]=='OP' and c[2]==mid}
 curves=[]
 for c in cs:
  if c[0]=='OP' and c[2] in anims:
   n=byid[c[1]];times=child(n,'KeyTime')['p'][0];curves.append({'track':anims[c[2]],'axis':c[3],'keys':len(times),'between5and6':sum(5<t/46186158000*30<6 for t in times),'values_first':child(n,'KeyValueFloat')['p'][0][:4]})
 return models,{'version':r.version,'models':len(models),'global':top(r,'GlobalSettings'),'stacks':[n for n in objs if n['name']=='AnimationStack'],'curves':curves}
am,aa=summary(a);em,ee=summary(e)
print('NAMES',set(am)-set(em),set(em)-set(am));print('CURVES',json.dumps([aa['curves'],ee['curves']],indent=2));print('GLOBALS SAME',aa['global']==ee['global']);print('STACKS',ee['stacks']);print('HILT SAME',am['Bn_katana_hilt']['c']==em['Bn_katana_hilt']['c'])
with open(b+'fbx_comparison.json','w') as f:json.dump({'original':aa,'export':ee},f,indent=2)
