import pymxs, json, traceback
rt=pymxs.runtime
out=r'D:\19356\UnityLearning\animation_inspect\samples.json'
def vec(v): return [float(v.x),float(v.y),float(v.z)]
try:
    data={'fps':int(rt.frameRate),'range':str(rt.animationRange),'nodes':[]}
    times=[0,1,2,3,4,4.5,4.9,4.99,5,5.01,5.1,5.25,5.5,5.75,5.9,5.99,6,6.01,6.1,6.5,7,8,9,10]
    for n in rt.objects:
        d={'name':str(n.name),'class':str(rt.classOf(n)),'parent':str(n.parent.name) if n.parent else None,'controller':str(rt.classOf(n.controller)),'samples':[]}
        for t in times:
            with pymxs.attime(t):
                m=n.transform
                q=m.rotation
                d['samples'].append({'t':t,'p':vec(m.translation),'q':[float(q.x),float(q.y),float(q.z),float(q.w)],'s':vec(m.scalepart)})
        data['nodes'].append(d)
    with open(out,'w',encoding='utf-8') as f: json.dump(data,f,ensure_ascii=False)
except: 
    with open(out+'.error','w') as f:f.write(traceback.format_exc())
