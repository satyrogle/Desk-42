from PIL import Image
import math, os
OUT="/home/user/Desk-42/design-system/portfolio/assets/pixel"
BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]]
def bayer(x,y): return (BAYER[y%4][x%4]+0.5)/16.0
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
class Cv:
    def __init__(s,w,h,bg=(8,8,14)): s.w,s.h=w,h; s.px=[[bg for _ in range(w)] for _ in range(h)]
    def set(s,x,y,c):
        if 0<=x<s.w and 0<=y<s.h: s.px[int(y)][int(x)]=c
    def rect(s,x,y,w,h,c):
        for j in range(int(h)):
            for i in range(int(w)): s.set(x+i,y+j,c)
    def vgrad(s,x,y,w,h,c0,c1):
        for j in range(int(h)):
            t=j/max(1,h-1)
            for i in range(int(w)):
                tt=min(1,max(0,t+(bayer(x+i,y+j)-0.5)*0.18))
                s.set(x+i,y+j,lerp(c0,c1,tt))
    def glow(s,cx,cy,r,c,strength=1.0):
        for j in range(int(cy-r),int(cy+r)):
            for i in range(int(cx-r),int(cx+r)):
                if 0<=i<s.w and 0<=j<s.h:
                    d=math.hypot(i-cx,(j-cy)*1.15)/r
                    if d<1:
                        t=(1-d)**1.6*strength
                        if t>bayer(i,j)*0.95:
                            base=s.px[j][i]; s.px[j][i]=lerp(base,c,min(1,t))
    def save(s,name,scale=1):
        im=Image.new("RGBA",(s.w,s.h))
        for y in range(s.h):
            for x in range(s.w): r,g,b=s.px[y][x]; im.putpixel((x,y),(r,g,b,255))
        if scale>1: im=im.resize((s.w*scale,s.h*scale),Image.NEAREST)
        im.save(os.path.join(OUT,name)); print("wrote",name,im.size)

# ---------- SCENE: OPERATOR AT NIGHT DESK (hero) ----------
W,H=256,144
c=Cv(W,H,(7,7,13))
c.vgrad(0,0,W,96,(30,24,46),(15,13,24))           # back wall
c.rect(0,96,W,H-96,(20,15,11))                     # floor shadow
# window with moon
c.rect(150,20,70,52,(12,18,24)); c.vgrad(154,24,62,44,(40,58,72),(20,30,42))
c.glow(186,40,26,(120,150,170),0.7); c.rect(183,30,6,6,(180,200,215)); c.rect(184,31,4,4,(150,175,195))
c.rect(150,18,72,2,(50,40,30)); c.rect(184,20,2,52,(20,15,12)); c.rect(150,44,72,2,(20,15,12))
# amber lamp glow from top-left
c.glow(60,40,72,(255,180,84),0.5)
# desk
c.vgrad(0,104,W,40,(60,44,28),(30,20,12)); c.rect(0,104,W,2,(96,72,46))
# monitor + green screen
c.rect(28,72,56,34,(14,12,22)); c.rect(50,106,12,6,(20,16,28))
c.rect(33,77,46,24,(8,26,18)); 
for k in range(5):
    ln=18+(k*7)%30; c.rect(36,80+k*4,ln,2,(43,209,122) if k%2 else (90,200,140))
c.rect(37,80+5*4,3,3,(138,255,192))
c.glow(56,90,46,(43,209,122),0.45)                 # monitor spill
# operator silhouette (seated, hunched)
c.rect(150,70,40,40,(5,5,9))                        # torso behind desk
c.rect(158,54,24,18,(5,5,9))                        # head/neck
c.rect(156,50,28,12,(5,5,9))
c.glow(170,52,30,(255,200,120),0.18)               # rim from window
c.set(165,57,(255,208,130)); c.set(166,57,(255,208,130)); c.set(175,57,(255,208,130)); c.set(176,57,(255,208,130)) # eyes
# papers
c.rect(96,98,16,8,(205,191,163)); c.rect(94,96,16,8,(220,208,182)); c.rect(120,100,6,3,(180,70,60))
# coffee mug
c.rect(118,92,10,8,(180,168,134)); c.rect(120,90,6,3,(28,18,12)); c.rect(128,93,3,4,(150,140,110))
# vignette
for y in range(H):
    for x in range(W):
        d=math.hypot((x-W/2)/(W/2),(y-H/2)/(H/2))
        if d>0.78:
            t=min(1,(d-0.78)/0.5)*0.7
            if t>bayer(x,y)*0.9: c.px[y][x]=lerp(c.px[y][x],(4,4,9),t)
c.save("operator-night.png")

# ---------- SCENE: ANOMALY CORRIDOR (filing archive under cosmic intrusion) ----------
c=Cv(W,H,(6,6,12))
c.vgrad(0,0,W,H,(20,18,32),(8,8,14))
# receding cabinet rows (perspective via shrinking)
for i,(x,w,col) in enumerate([(8,30,(34,26,20)),(46,26,(40,30,22)),(80,22,(30,22,16)),(166,22,(30,22,16)),(200,26,(40,30,22)),(238,30,(34,26,20))]):
    c.vgrad(x,52,w,80,col,lerp(col,(8,8,12),0.6))
    for dy in range(56,128,10): c.rect(x+2,dy,w-4,2,(12,10,8))      # drawer lines
    c.rect(x+w//2-2,60,4,3,(120,100,70))                            # handle
# central doorway with anomaly bloom
c.rect(112,40,32,92,(4,4,9))
c.glow(128,86,40,(173,129,223),0.75); c.glow(128,86,26,(211,95,152),0.5); c.glow(128,80,16,(138,255,192),0.3)
# impossible geometry: thin violet rays
for a in range(0,360,30):
    rad=math.radians(a); 
    for r in range(6,34):
        x=int(128+math.cos(rad)*r); y=int(86+math.sin(rad)*r*0.9)
        if bayer(x,y)<0.4: c.set(x,y,lerp((173,129,223),(6,6,12),r/34))
# floor + ceiling
c.vgrad(0,128,W,16,(26,20,14),(8,6,10)); c.rect(0,52,W,2,(40,32,24))
# a small operator silhouette facing the anomaly
c.rect(96,108,10,20,(3,3,7)); c.rect(98,100,6,8,(3,3,7))
c.glow(101,104,16,(173,129,223),0.25)
for y in range(H):
    for x in range(W):
        d=math.hypot((x-W/2)/(W/2),(y-H/2)/(H/2))
        if d>0.72:
            t=min(1,(d-0.72)/0.5)*0.8
            if t>bayer(x,y)*0.9: c.px[y][x]=lerp(c.px[y][x],(3,3,8),t)
c.save("anomaly-corridor.png")
print("done")

# ---------- SCENE: BUREAU VISTA (office floor, rows of desks under cosmic window) ----------
W,H=256,144
c=Cv(W,H,(7,7,13))
c.vgrad(0,0,W,84,(26,22,42),(12,11,20))
# huge impossible window/structure with violet moon behind
c.rect(96,8,64,70,(10,14,22)); c.vgrad(100,12,56,62,(46,40,70),(18,16,30))
c.glow(128,40,30,(173,129,223),0.6); c.glow(128,40,18,(211,150,200),0.35)
c.rect(124,22,8,8,(200,180,220)); 
c.rect(96,6,64,2,(40,34,26)); c.rect(127,8,2,70,(14,12,20)); c.rect(96,40,64,2,(14,12,20))
# amber ceiling lights
for lx in (40,128,216):
    c.glow(lx,6,40,(255,180,84),0.32); c.rect(lx-8,2,16,2,(255,200,120))
# rows of desks receding (3 rows, getting lower/larger)
rows=[(58,(34,28,40),18,(43,209,122)),(82,(40,32,46),26,(72,205,212)),(112,(52,40,30),40,(43,209,122))]
for ry,deskc,mh,scr in rows:
    c.vgrad(0,ry,W,mh,deskc,lerp(deskc,(8,8,12),0.5))
    for mx in range(16,W-16,52):
        c.rect(mx,ry-10,18,12,(10,10,18)); c.rect(mx+2,ry-8,14,8,lerp((8,24,18),scr,0.2))
        c.rect(mx+3,ry-7,8,2,scr); c.glow(mx+9,ry-4,16,scr,0.25)
# a lone lit operator at front desk
c.rect(120,118,12,20,(4,4,8)); c.rect(122,110,8,8,(4,4,8)); c.glow(126,113,18,(255,200,120),0.2)
for y in range(H):
    for x in range(W):
        d=math.hypot((x-W/2)/(W/2),(y-H/2)/(H/2))
        if d>0.74:
            t=min(1,(d-0.74)/0.5)*0.75
            if t>bayer(x,y)*0.9: c.px[y][x]=lerp(c.px[y][x],(4,4,9),t)
c.save("bureau-vista.png")
print("vista done")
