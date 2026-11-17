"""Low-poly medical supply crate, origin at bottom center. Run with Blender CLI."""
from pathlib import Path
import bpy
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mats={}
for n,c in {'Wood':(.28,.20,.115,1),'WoodLight':(.43,.33,.20,1),'Steel':(.13,.17,.17,1),'Shirt':(.72,.70,.58,1),'Red':(.46,.085,.075,1)}.items():
 m=bpy.data.materials.new(n);m.diffuse_color=c;mats[n]=m
def box(n,p,s,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=n;o.dimensions=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
 b=o.modifiers.new('Soft crate edges','BEVEL');b.width=.007;b.segments=1;bpy.ops.object.modifier_apply(modifier=b.name)
box('Wooden supply crate',(0,0,.27),(.68,.54,.54),'Wood')
for x in [-.26,-.13,0,.13,.26]:
 for y in [-.278,.278]:box('Face plank',(x,y,.27),(.12,.025,.49),'WoodLight')
 box('Lid plank',(x,0,.55),(.12,.53,.035),'WoodLight')
for x in [-.29,.29]:
 for y in [-.29,.29]:box('Corner reinforcement',(x,y,.28),(.055,.045,.54),'Steel')
for y in [-.299,.299]:
 box('Painted medic panel',(0,y,.30),(.35,.025,.34),'Shirt')
 box('Medic cross horizontal',(0,y*1.05,.30),(.26,.012,.075),'Red')
 box('Medic cross vertical',(0,y*1.05,.30),(.075,.012,.26),'Red')
for x in [-.35,.35]:
 box('Side medic panel',(x,0,.30),(.025,.33,.34),'Shirt')
 box('Side medic cross horizontal',(x*1.04,0,.30),(.012,.25,.075),'Red')
 box('Side medic cross vertical',(x*1.04,0,.30),(.012,.075,.26),'Red')
box('Lid medic panel',(0,0,.58),(.35,.34,.025),'Shirt')
box('Lid cross horizontal',(0,0,.60),(.26,.075,.012),'Red')
box('Lid cross vertical',(0,0,.60),(.075,.26,.012),'Red')
bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=bpy.context.selected_objects[0];bpy.ops.object.join()
o=bpy.context.object;o.name='HealthCrate';bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/Rebuild/Art/Outpost/Models/HealthCrate.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/Environment/HealthCrate.blend'))
