from fbx_read import *
b='D:/19356/UnityLearning/animation_inspect/'
a=Reader('D:/19356/UnityLearning/Assets/ThirdParty/UnagiAnimationPack/FBX/MAX_Variant/Unagi/01_Attack/Unagi_Attack_01.fbx');e=Reader(b+'Unagi_Attack_01_fixed_export.fbx')
def objects(r,t):return {x['p'][1]:x for x in top(r,'Objects')['c'] if x['name']==t}
for typ in ['Model','Geometry','Material','Deformer']:
 aa,ee=objects(a,typ),objects(e,typ)
 shared=aa.keys()&ee.keys();changed=[k.split('\x00')[0] for k in shared if aa[k]['c']!=ee[k]['c']]
 print(typ,'counts',len(aa),len(ee),'same_content',len(shared)-len(changed),'changed',changed[:10])
