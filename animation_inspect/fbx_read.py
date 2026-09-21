import struct,zlib,json,sys
class Reader:
 def __init__(self,p):
  self.b=open(p,'rb').read();self.i=23;self.version=self.take('I');self.wide=self.version>=7500;self.hs=25 if self.wide else 13;self.nodes=[]
  while True:
   n=self.node()
   if n is None:break
   self.nodes.append(n)
 def take(self,f):
  n=struct.calcsize('<'+f);v=struct.unpack_from('<'+f,self.b,self.i);self.i+=n;return v[0] if len(v)==1 else v
 def prop(self):
  c=chr(self.take('B'))
  if c in 'YCLFDI':return self.take({'Y':'h','C':'?','L':'q','F':'f','D':'d','I':'i'}[c])
  if c in 'SR':
   n=self.take('I');v=self.b[self.i:self.i+n];self.i+=n;return v.decode('utf8',errors='replace') if c=='S' else '<raw>'
  if c in 'fdlibc':
   n,enc,size=self.take('III');v=self.b[self.i:self.i+size];self.i+=size
   if enc:v=zlib.decompress(v)
   return list(struct.unpack('<'+str(n)+{'f':'f','d':'d','l':'q','i':'i','b':'b','c':'b'}[c],v))
  raise ValueError(c)
 def node(self):
  end,count,plen,nlen=self.take('QQQB' if self.wide else 'IIIB')
  if end==0:return None
  name=self.b[self.i:self.i+nlen].decode();self.i+=nlen
  props=[self.prop() for _ in range(count)];children=[]
  while self.i<end:
   n=self.node()
   if n is None:break
   children.append(n)
  assert self.i==end,(name,self.i,end)
  return {'name':name,'p':props,'c':children}
def child(n,name):return next(x for x in n['c'] if x['name']==name)
def top(r,name):return next(x for x in r.nodes if x['name']==name)
if __name__=='__main__':
 r=Reader(sys.argv[1]);models=[n for n in top(r,'Objects')['c'] if n['name']=='Model']
 data={'version':r.version,'global':top(r,'GlobalSettings'),'models':[n['p'] for n in models],'animationStacks':[n for n in top(r,'Objects')['c'] if n['name']=='AnimationStack'],'hilt':[n for n in models if 'Bn_katana_hilt\x00' in n['p'][1]]}
 print(json.dumps(data,ensure_ascii=True,indent=2))
