"""Adapt CC0 Quaternius soldiers into four course recruits. Run with Blender CLI."""
from pathlib import Path
import bpy
import math
import json
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Assets/Rebuild/Art/Recruits/Models'
OUT.mkdir(parents=True, exist_ok=True)
PALETTE = {}
ROLES = [
    ('Rifle', 'Soldier_Female', (.23,.31,.17,1), (.66,.43,.26,1), (.20,.07,.025,1)),
    ('Sniper', 'BlueSoldier_Male', (.16,.24,.29,1), (.43,.29,.18,1), (.055,.035,.025,1)),
    ('Mortar', 'Soldier_Male', (.43,.36,.16,1), (.72,.53,.34,1), (.10,.065,.025,1)),
    ('Medic', 'BlueSoldier_Female', (.37,.15,.135,1), (.78,.59,.43,1), (.065,.045,.03,1)),
]

def mat(name, color):
    name = 'Recruit_' + name
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = color
    PALETTE[name] = color
    return m

def box(name, pos, size, material, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object
    o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if bevel:
        b = o.modifiers.new('Faceted edge', 'BEVEL'); b.width=bevel; b.segments=1
        bpy.ops.object.modifier_apply(modifier=b.name)
    return o

def tube(name, a, b, radius, material):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=(b-a).length, location=(a+b)/2)
    o=bpy.context.object; o.name=name
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    o.data.materials.append(material)
    return o

for role, source, uniform, skin, hair in ROLES:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'SourceArt/Characters/Quaternius'/f'{source}.blend'))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    body=next(o for o in bpy.data.objects if o.type=='MESH')
    rig.animation_data_clear()
    rig.data.pose_position='POSE'
    for bone in rig.pose.bones:
        for constraint in list(bone.constraints): bone.constraints.remove(constraint)
        bone.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    # Lower the source T-pose arms in armature space, then bake a neutral rest pose.
    for suffix, angle in [('L',83),('R',-83)]:
        bone=rig.pose.bones['UpperArm.'+suffix]
        pivot=bone.head.copy()
        bone.matrix=Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(angle),4,'Y') @ Matrix.Translation(-pivot) @ bone.matrix
    bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get()
    baked=bpy.data.meshes.new_from_object(body.evaluated_get(deps), depsgraph=deps)
    body.modifiers.clear()
    body.data=baked
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    mod=body.modifiers.new('Recruit skeleton','ARMATURE');mod.object=rig
    rig.name='RecruitSkeleton'
    colors = {
        'Skin':mat(role+'_Skin',skin), 'Face':mat('Eyes',(.88,.88,.76,1)),
        'Hair':mat(role+'_Hair',hair), 'Main':mat(role+'_Uniform',uniform),
        'DarkGreen':mat(role+'_Trousers',tuple(v*.70 for v in uniform[:3])+(1,)),
        'Black':mat('Leather',(.065,.057,.043,1)),
    }
    for slot in body.material_slots:
        name=slot.material.name.split('.')[0]
        slot.material=colors.get(name,colors['Main'])
    for p in body.data.polygons:p.use_smooth=False
    steel=mat('Gunmetal',(.085,.11,.105,1))
    wood=mat('Stock',(.27,.14,.065,1))
    canvas=mat('Webbing',(.34,.32,.21,1))
    pale=mat('Dressing',(.78,.76,.60,1))
    badge=mat('MedicMark',(.43,.075,.055,1))
    gear=[]
    def pack():
        gear.append(box('Field pack',(0,.37,1.72),(.66,.30,.65),canvas))
        for x in [-.23,.23]:gear.append(box('Pack webbing',(x,.535,1.72),(.065,.025,.59),wood,.005))
    pack()
    # Equipment is attached to the torso, leaving the marching arms free.
    if role in ['Rifle','Sniper']:
        x=-.43 if role=='Rifle' else .43
        gear.append(box('Wooden rifle stock',(x,.61,1.47),(.14,.13,.60),wood))
        gear.append(tube('Rifle barrel',(x,.61,1.72),(x,.61,2.65 if role=='Sniper' else 2.42),.035,steel))
        gear.append(box('Rifle receiver',(x,.61,1.90),(.10,.14,.32),steel))
        if role=='Sniper':
            gear.append(tube('Optical scope',(x+.11,.61,1.87),(x+.11,.61,2.25),.065,steel))
            gear.append(box('Rolled camouflage cape',(0,.57,2.05),(.74,.23,.19),colors['Main']))
        else:
            for x in [-.25,.25]:gear.append(box('Ammunition pouch',(x,-.27,1.32),(.20,.15,.22),canvas))
    elif role=='Mortar':
        gear.append(tube('Mortar tube',(.34,.62,1.13),(.34,.62,2.59),.14,steel))
        gear.append(tube('Mortar muzzle rim',(.34,.62,2.51),(.34,.62,2.63),.17,steel))
        gear.append(box('Mortar base plate',(-.24,.62,1.40),(.65,.09,.67),steel))
        for x in [-.14,.09]:gear.append(tube('Mortar support leg',(x,.66,1.7),(x,.66,2.33),.04,canvas))
    else:
        gear.append(box('Medical satchel',(-.50,-.02,1.12),(.42,.28,.44),pale))
        gear.append(box('Medical insignia vertical',(-.50,-.17,1.12),(.07,.025,.24),badge,.003))
        gear.append(box('Medical insignia horizontal',(-.50,-.18,1.12),(.24,.025,.07),badge,.003))
        gear.append(box('Medical pack panel',(0,.535,1.72),(.34,.035,.34),pale))
        gear.append(box('Pack insignia vertical',(0,.56,1.72),(.065,.025,.23),badge,.003))
        gear.append(box('Pack insignia horizontal',(0,.565,1.72),(.23,.025,.065),badge,.003))
    # Different headgear also keeps roles readable when seen from the front.
    if role in ['Rifle','Mortar']:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=4, radius=1,location=(0,0,2.96))
        helmet=bpy.context.object;helmet.name='Steel helmet';helmet.scale=(.54,.48,.29)
        helmet.data.materials.append(colors['Main'])
        gear_head=[helmet,box('Helmet brim',(0,-.08,2.91),(.99,.84,.065),colors['Main'])]
    else:
        gear_head=[box('Field cap',(0,.0,3.0),(.79,.69,.21),colors['Main'],.07),box('Cap peak',(0,-.39,2.93),(.63,.28,.065),colors['Main'])]
    for o in gear+gear_head:
        world=o.matrix_world.copy()
        o.parent=rig;o.parent_type='BONE';o.parent_bone='Head' if o in gear_head else 'Torso'
        o.matrix_world=world
    # Preserve skinning in FBX; animation is driven from the shared Unity music clock.
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True);body.select_set(True)
    for o in gear+gear_head:o.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'Recruit{role}.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
        add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'SourceArt/Characters'/f'Recruit{role}.blend'))
(ROOT/'SourceArt/Characters/recruit-palette.json').write_text(json.dumps(PALETTE,indent=2)+'\n')
print('Exported four adapted Quaternius recruits')
