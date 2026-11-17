"""Articulated medical crate and red-filled syringe. Run with Blender CLI."""
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mats={}
for n,c in {'Wood':(.28,.20,.115,1),'WoodLight':(.43,.33,.20,1),'Steel':(.13,.17,.17,1),'Shirt':(.72,.70,.58,1),'Red':(.46,.085,.075,1)}.items():
 m=bpy.data.materials.new(n);m.diffuse_color=c;mats[n]=m
parts={};current=None

def box(n,p,s,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=n;o.dimensions=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
 b=o.modifiers.new('Soft edges','BEVEL');b.width=.007;b.segments=1;bpy.ops.object.modifier_apply(modifier=b.name)
 parts[current].append(o);return o

def cylinder(n,p,radius,depth,mat):
 bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=radius,depth=depth,location=p)
 o=bpy.context.object;o.name=n;o.data.materials.append(mats[mat]);parts[current].append(o);return o

def begin(n):
 global current
 current=n;parts[n]=[]

for y,n in [(-.278,'WallFront'),(.278,'WallBack')]:
 begin(n)
 for x in [-.26,-.13,0,.13,.26]:box('Face plank',(x,y,.29),(.12,.04,.49),'WoodLight')
 for x in [-.29,.29]:box('Corner reinforcement',(x,y,.28),(.055,.055,.54),'Steel')
 out=y*1.12
 box('Medic panel',(0,out,.30),(.35,.025,.34),'Shirt')
 box('Cross horizontal',(0,out*1.05,.30),(.26,.012,.075),'Red')
 box('Cross vertical',(0,out*1.05,.30),(.075,.012,.26),'Red')
for x,n in [(-.33,'WallLeft'),(.33,'WallRight')]:
 begin(n)
 for y in [-.19,-.065,.065,.19]:box('Side plank',(x,y,.29),(.04,.12,.49),'Wood')
 out=x*1.09
 box('Medic panel',(out,0,.30),(.025,.33,.34),'Shirt')
 box('Cross horizontal',(out*1.04,0,.30),(.012,.25,.075),'Red')
 box('Cross vertical',(out*1.04,0,.30),(.012,.075,.26),'Red')
begin('Lid')
for x in [-.26,-.13,0,.13,.26]:box('Lid plank',(x,0,.55),(.12,.53,.035),'WoodLight')
box('Lid panel',(0,0,.58),(.35,.34,.025),'Shirt')
box('Lid cross horizontal',(0,0,.60),(.26,.075,.012),'Red')
box('Lid cross vertical',(0,0,.60),(.075,.26,.012),'Red')
begin('CrateBase');box('Crate floor',(0,0,.025),(.65,.53,.05),'Wood')
begin('Syringe')
cylinder('Red lifeblood',(0,0,.43),.065,.32,'Red')
for z in [.25,.61]:cylinder('Barrel collar',(0,0,z),.085,.055,'Shirt')
for x in [-.073,.073]:box('Barrel glass rail',(x,0,.43),(.018,.025,.34),'Shirt')
box('Finger grip',(0,0,.22),(.25,.07,.045),'Shirt')
cylinder('Plunger rod',(0,0,.14),.022,.15,'Steel')
box('Thumb pad',(0,0,.055),(.15,.11,.04),'Shirt')
cylinder('Needle hub',(0,0,.67),.038,.085,'Shirt')
cylinder('Needle',(0,0,.79),.009,.18,'Steel')
for i in range(5):box('Dose marking',(.025,-.067,.31+i*.055),(.045 if i%2==0 else .025,.012,.008),'Steel')
origins={'WallFront':(0,-.278,.04),'WallBack':(0,.278,.04),'WallLeft':(-.33,0,.04),'WallRight':(.33,0,.04),'Lid':(0,0,.55),'CrateBase':(0,0,0),'Syringe':(0,0,0)}
for n,objects in parts.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=n
 bpy.context.scene.cursor.location=origins[n];bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/Rebuild/Art/Outpost/Models/HealthCrate.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/Environment/HealthCrate.blend'))
