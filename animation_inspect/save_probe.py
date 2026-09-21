import pymxs,json,os
r=pymxs.runtime
base='D:/19356/UnityLearning/animation_inspect/'
results={}
results['scene_objects']=int(r.objects.count)
results['scene_save']=r.saveMaxFile((base+'save_probe_scene.max').replace('/',chr(92)),quiet=True)
r.resetMaxFile(r.Name('noPrompt'))
r.box()
results['simple_save']=r.saveMaxFile((base+'save_probe_box.max').replace('/',chr(92)),quiet=True)
results['exists']={x:os.path.exists(base+x) for x in ['save_probe_scene.max','save_probe_box.max']}
with open(base+'save_probe.json','w') as f:json.dump(results,f)
