"""Five slim posts fit the existing lane markers. Blender -X imports as Unity +X."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Rebuild/Art/Outpost/Models/SpikePillar.fbx'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
mats={}
for name,color in {'Steel':(.13,.17,.17,1),'Olive':(.23,.28,.17,1),'Brass':(.71,.55,.24,1)}.items():
    m=bpy.data.materials.new(name);m.diffuse_color=color;mats[name]=m

def box(name,pos,size,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mats[material])
    bevel=o.modifiers.new('Chamfered armor','BEVEL');bevel.width=.025;bevel.segments=1
    bpy.ops.object.modifier_apply(modifier=bevel.name)

box('Armored pillar',(0,0,1.03),(.30,.14,2.06),'Olive')
box('Foot plate',(0,0,.025),(.30,.14,.05),'Steel')
box('Yellow marker cap',(0,0,2.07),(.30,.14,.06),'Brass')
for z in [.38,.85,1.32,1.80]:
    box('Spike mounting rail',(-.17,0,z),(.06,.14,.16),'Steel')
    for depth in [0]:
        a=Vector((-.20,depth,z));b=Vector((-.65,depth,z))
        bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=.07,radius2=0,depth=(b-a).length,location=(a+b)/2)
        o=bpy.context.object;o.name='Inward spike';o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
        o.data.materials.append(mats['Steel'])
for depth in [-.075,.075]:
    box('Hazard stripe',(0,depth,1.11),(.30,.01,.07),'Brass')
    box('Hazard stripe',(0,depth,.98),(.30,.01,.07),'Brass')
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active=bpy.context.selected_objects[0]
bpy.ops.object.join()
o=bpy.context.object;o.name='SpikePillar'
bpy.context.scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(OUT),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/Environment/SpikePillar.blend'))
