"""Rasterize unchanged library vectors for a user preview. Not shipping UI."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
data = json.loads((root / 'artifacts/orb-preview-geometry.json').read_text())
S = 3
font_path = str(root / 'src/Murmur/web/fonts/Figtree.ttf')
def font(n): return ImageFont.truetype(font_path, int(n * S))
def text(im, xy, value, n=16, fill='#272829', anchor=None):
    ImageDraw.Draw(im).text((xy[0]*S,xy[1]*S),value,font=font(n),fill=fill,anchor=anchor)
def orb(im, geometry, x, y, scale=1, dark=False):
    d = ImageDraw.Draw(im, 'RGBA')
    def ink(item):
        w = min(1,max(0,item['white']))
        g = round((1-w if dark else w)*255)
        return (g,g,g,round(item.get('a',1)*255))
    for l in geometry['lines']:
        d.line([(int((x+l['x1']*scale)*S),int((y+l['y1']*scale)*S)),(int((x+l['x2']*scale)*S),int((y+l['y2']*scale)*S))],fill=ink(l),width=max(1,round(l['w']*scale*S)))
    for p in geometry['dots']:
        a,b,r=(x+p['x']*scale)*S,(y+p['y']*scale)*S,p['r']*scale*S
        d.ellipse((a-r,b-r,a+r,b+r),fill=ink(p))

im=Image.new('RGB',(960*S,740*S),'#f5f4f0')
text(im,(46,30),'Thinking Orbs',30)
text(im,(46,74),'The library used in Elsepin — frames from its original geometry',16,fill='#636761')
for i,state in enumerate(data['states']):
    x,y=46+(i%3)*294,122+(i//3)*163
    ImageDraw.Draw(im).rounded_rectangle((x*S,y*S,(x+280)*S,(y+150)*S),radius=16*S,fill='white')
    orb(im,data['still'][i],x+92,y+12,1.5)
    text(im,(x+140,y+119),state.capitalize(),16,anchor='mm')
text(im,(46,638),'Elsepin’s usual inline style:',16)
orb(im,data['inline'],46,674)
text(im,(76,673),'Thinking…',17)
text(im,(550,675),'20 px preset · enlarged examples above',14,fill='#636761')
im.resize((960,740),Image.Resampling.LANCZOS).save(root/'artifacts/thinking-orbs-preview.png')

frames=[]
for geom in data['motion']:
    im=Image.new('RGB',(800*S,250*S),'#191d20')
    for i,state in enumerate(data['states'][:4]):
        orb(im,geom[i],i*200+52,46,1.5,True)
        text(im,(i*200+100,184),state.capitalize(),16,fill='#f5f4f0',anchor='mm')
    frames.append(im.resize((800,250),Image.Resampling.LANCZOS))
frames[0].save(root/'artifacts/thinking-orbs-motion.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,optimize=False)
print('Created thinking-orbs-preview.png and thinking-orbs-motion.gif')
