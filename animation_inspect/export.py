import pymxs,json,traceback,os
r=pymxs.runtime
base='D:/19356/UnityLearning/animation_inspect/'
try:
 data=json.load(open(base+'original_fbx_info.json',encoding='utf-8-sig'))
 names=[x[1].split('\x00')[0] for x in data['models']]
 nodes=[r.getNodeByName(n) for n in names]
 assert all(n is not None for n in nodes)
 r.select(r.Array(*nodes))
 r.execute('animationRange = interval 0f 20f')
 r.execute('addNewKey (getNodeByName "Avatar_Female_Size02_Unagi_Model").position.controller 0f; addNewKey (getNodeByName "Avatar_Female_Size02_Unagi_Model").position.controller 20f')
 r.pluginManager.loadClass(r.FBXEXP)
 settings={'Animation':True,'ASCII':False,'BakeAnimation':False,'BakeResampleAnimation':False,'FilterKeyReducer':False,'Removesinglekeys':False,'Skin':True,'Shape':True,'Cameras':False,'Lights':False,'EmbedTextures':False,'UpAxis':'Y','AxisConversionMethod':'Animation','FileVersion':'FBX202000','ScaleFactor':1.0,'ConvertUnit':'cm','SmoothingGroups':True,'ShowWarnings':False}
 result={k:str(r.FBXExporterSetParam(k,v)) for k,v in settings.items()}
 output=(base+'Unagi_Attack_01_fixed_export.fbx').replace('/',chr(92))
 result['export']=r.exportFile(output,r.Name('noPrompt'),selectedOnly=True,using=r.FBXEXP)
 result['exists']=os.path.exists(output)
 result['units']={'system':str(r.units.SystemType),'scale':float(r.units.SystemScale)}
 with open(base+'export_status.json','w') as f:json.dump(result,f,indent=2)
except:
 with open(base+'export.error','w') as f:f.write(traceback.format_exc())
