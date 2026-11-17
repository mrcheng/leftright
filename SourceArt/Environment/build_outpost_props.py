"""Editable low-poly outpost props. Run with Blender --background --python this_file."""
from pathlib import Path
import math
import random
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT.parent / 'Assets/Rebuild/Art/Outpost/Models'
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(1943)

COLORS = {
    'Wood': (0.28, 0.20, 0.115, 1), 'WoodLight': (0.43, 0.33, 0.20, 1),
    'Olive': (0.23, 0.28, 0.17, 1), 'OliveLight': (0.32, 0.36, 0.23, 1),
    'Steel': (0.13, 0.17, 0.17, 1), 'Sand': (0.52, 0.46, 0.30, 1),
    'SandLight': (0.61, 0.54, 0.37, 1), 'Canvas': (0.39, 0.40, 0.27, 1),
    'Red': (0.46, 0.085, 0.075, 1), 'Skin': (0.70, 0.53, 0.37, 1),
    'Uniform': (0.10, 0.12, 0.11, 1), 'Leather': (0.095, 0.085, 0.065, 1),
    'Shirt': (0.72, 0.70, 0.58, 1),
    'Brass': (0.71, 0.55, 0.24, 1), 'Lamp': (1.0, 0.74, 0.32, 1),
    'FloodLamp': (0.95, 0.94, 0.80, 1),
    'Earth': (0.25, 0.25, 0.17, 1), 'EarthLight': (0.27, 0.27, 0.185, 1),
    'Grass': (0.245, 0.275, 0.17, 1), 'GrassLight': (0.27, 0.30, 0.19, 1),
    'Trench': (0.17, 0.16, 0.12, 1),
}
MATS = {}
for name, color in COLORS.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    MATS[name] = mat

def material(obj, name):
    obj.data.materials.append(MATS[name])
    return obj

def box(name, xyz, size, mat, bevel=0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    material(obj, mat)
    if bevel:
        modifier = obj.modifiers.new('Faceted edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

def beam(name, a, b, radius, mat, vertices=6):
    a, b = Vector(a), Vector(b)
    delta = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                      depth=delta.length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    return material(obj, mat)

def mesh(name, vertices, faces, mats):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for mat in mats:
        data.materials.append(MATS[mat])
    return obj

def roof(name, width, depth, base, peak, mat):
    w, d = width/2, depth/2
    return mesh(name, [(-w,-d,base),(w,-d,base),(0,-d,peak),
                       (-w,d,base),(w,d,base),(0,d,peak)],
                [(0,1,2),(3,5,4),(0,3,4,1),(0,2,5,3),(1,4,5,2)], [mat])

def build(name, function):
    before = set(bpy.data.objects)
    function()
    objects = sorted(set(bpy.data.objects)-before, key=lambda obj: obj.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / f'{name}.fbx'),
                             use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                             bake_anim=False, apply_unit_scale=True)
    obj.hide_set(True)
    obj.hide_render = True
    return obj

def sandbags():
    for row in range(3):
        for col in range(4):
            box('Sandbag', ((col-1.5)*.75 + (.17 if row == 1 else 0),
                            random.uniform(-.035,.035), .16+row*.27),
                (.80,.55,.31), 'Sand' if (row+col)%2 else 'SandLight', .095,
                (0,0,random.uniform(-.035,.035)))
build('SandbagWall', sandbags)

def wire():
    for x in (-2, 2):
        beam('Fence post', (x,0,0),(x,0,1.65),.045,'Steel')
        beam('Angled top', (x,0,1.6),(x,-.30,1.92),.04,'Steel')
    for height in (.45,.9,1.4):
        beam('Wire',(-2,0,height),(2,0,height),.012,'Steel',4)
    for i in range(33):
        angle=i/32*math.pi*12
        nextangle=(i+1)/32*math.pi*12
        p=(-2+i*.125, math.cos(angle)*.19,1.15+math.sin(angle)*.29)
        q=(-2+(i+1)*.125, math.cos(nextangle)*.19,1.15+math.sin(nextangle)*.29)
        beam('Coiled wire',p,q,.013,'Steel',4)
    for i in range(13):
        x=-1.8+i*.3
        beam('Barb',(x-.06,-.05,1.34),(x+.06,.05,1.47),.012,'Steel',4)
build('WireSection', wire)

def hedgehog():
    for rotation in ((0,.80,0),(0,-.80,0),(math.pi/2,0,0)):
        box('Steel obstacle',(0,0,.75),(.13,.13,2.1),'Steel',.015,rotation)
    box('Rivet plate',(0,-.09,.75),(.32,.05,.30),'Steel',.02)
build('SteelObstacle', hedgehog)

def barracks():
    box('Foundation',(0,0,.10),(6.1,3.3,.2),'Steel',.025)
    box('Timber walls',(0,0,1.4),(6,3.2,2.6),'Olive',.03)
    for side in (-1,1):
        for i in range(25):
            box('Wall plank',(-2.88+i*.24,side*1.61,1.4),(.025,.035,2.5),'OliveLight')
        for x in (-2, 1.8):
            box('Window rim',(x,side*1.64,1.7),(1.05,.07,.85),'WoodLight')
            box('Dark window',(x,side*1.69,1.7),(.87,.03,.66),'Steel')
            box('Window mullion',(x,side*1.72,1.7),(.05,.035,.68),'WoodLight')
    box('Door frame',(0,-1.68,1.05),(1.05,.12,2.05),'WoodLight')
    box('Door',(0,-1.76,1.05),(.88,.08,1.92),'Wood')
    box('Door handle',(.29,-1.83,1.02),(.07,.07,.12),'Brass')
    roof('Pitched metal roof',6.6,3.7,2.77,3.65,'Steel')
    for y in (-1.85,1.85):
        beam('Roof edge',(-3.3,y,2.77),(0,y,3.65),.065,'WoodLight')
        beam('Roof edge',(0,y,3.65),(3.3,y,2.77),.065,'WoodLight')
build('Barracks', barracks)

def tower():
    for x in (-1.1,1.1):
        for y in (-1.1,1.1):
            box('Tower post',(x,y,2.35),(.22,.22,4.7),'Wood')
    for side in (-1.1,1.1):
        beam('Cross brace',(-1.1,side,.35),(1.1,side,3.8),.07,'WoodLight',4)
        beam('Cross brace',(1.1,side,.35),(-1.1,side,3.8),.07,'WoodLight',4)
    box('Lookout floor',(0,0,4.12),(3.0,3.0,.23),'WoodLight')
    for x in (-1.3,1.3):
        box('Lookout side',(x,0,4.65),(.14,2.7,.9),'Olive')
    box('Lookout back',(0,1.3,4.65),(2.7,.14,.9),'Olive')
    box('Lookout front',(0,-1.3,4.48),(2.7,.14,.55),'Olive')
    for x in (-1.3,1.3):
        for y in (-1.3,1.3):
            box('Roof pole',(x,y,5.0),(.1,.1,1.7),'WoodLight')
    roof('Lookout roof',3.5,3.5,5.75,6.4,'Steel')
    for x in (-.30,.30):
        beam('Ladder', (x,-1.45,.05),(x,-1.45,4.05),.04,'WoodLight')
    for i in range(13):
        beam('Ladder rung',(-.30,-1.45,.22+i*.29),(.30,-1.45,.22+i*.29),.035,'WoodLight')
build('Watchtower', tower)

def stand():
    box('Command foundation',(0,0,.30),(4.6,3.3,.6),'Steel',.06)
    for i in range(15):
        box('Command planks',(-2.12+i*.303,0,.64),(.29,3.25,.12),'WoodLight',.01)
    for i in range(3):
        box('Steps',(0,-2.16+i*.24,.1+i*.18),(1.8,1.0-i*.2,.2+i*.18),'Wood')
build('CommandStand', stand)

def walkway():
    for z in (.05,.21):
        box('Support beam',(0,0,z),(20,.95,.12),'Steel',.01)
    for i in range(80):
        box('Walkway board',(-9.875+i*.25,0,.33),(.24,1.12,.11),
            'WoodLight' if i%4 else 'Wood',.008)
    for side in (-.57,.57):
        box('Metal edge',(0,side,.31),(20,.045,.15),'Steel')
    for x in (-9.72,9.72):
        for j in range(5):
            box('End marker',(x,-.44+j*.22,.397),(.30,.14,.018),'Brass')
build('Walkway', walkway)

def flag():
    beam('Flag pole',(0,0,0),(0,0,5.2),.045,'Steel',8)
    # Fabric is a separate subdivided Unity mesh so the pole stays rigid in wind.
    box('Flag hem',(0.05,-.025,4.30),(.055,.04,1.52),'Leather')
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=.085, location=(0,0,5.2))
    material(bpy.context.object,'Brass')
build('Flagpole', flag)

def lamp():
    beam('Lamp post',(0,0,0),(0,0,3.6),.045,'Steel')
    beam('Lamp arm',(0,0,3.55),(.5,0,3.55),.035,'Steel')
    box('Lamp cover',(.5,0,3.45),(.48,.38,.16),'Steel',.03)
    box('Lamp light',(.5,0,3.34),(.33,.26,.10),'Lamp',.015)
build('CampLamp', lamp)

def floodlight_mount():
    box('Roof mounting plate',(0,0,.045),(.60,.60,.09),'Steel',.025)
    beam('Floodlight mast',(0,0,.08),(0,0,.70),.075,'Steel',8)
    for x in (-.21,.21):
        for y in (-.21,.21):
            box('Mounting bolt',(x,y,.10),(.065,.065,.055),'Brass',.01)
build('FloodlightMount',floodlight_mount)

def floodlight_head():
    box('Weatherproof housing',(0,0,0),(1.50,.65,.95),'Steel',.08)
    # Positive depth faces the beam's forward axis after Unity conversion.
    box('Glass lens',(0,.345,0),(1.28,.035,.73),'FloodLamp',.025)
    for x in (-.43,0,.43):
        box('Lens grille',(x,.375,0),(.026,.03,.75),'Steel')
    box('Lens grille',(0,.375,0),(1.30,.03,.028),'Steel')
    for x in (-.84,.84):
        box('Side bracket',(x,-.08,-.15),(.10,.48,.80),'Steel',.015)
        beam('Tilt hinge',(x-.07,0,0),(x+.07,0,0),.105,'Brass',8)
    box('Bracket crossbar',(0,-.08,-.58),(1.78,.15,.12),'Steel',.015)
build('FloodlightHead',floodlight_head)

def officer():
    box('Coat',(0,0,1.27),(.69,.40,.76),'Uniform',.045)
    box('Belt',(0,-.01,1.06),(.71,.43,.09),'Leather')
    box('Buckle',(0,-.235,1.06),(.13,.035,.08),'Brass')
    beam('Collar',(-.13,-.22,1.61),(0,-.25,1.47),.06,'Shirt',4)
    beam('Collar',(.13,-.22,1.61),(0,-.25,1.47),.06,'Shirt',4)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=.235,location=(0,0,1.88))
    obj=bpy.context.object; obj.scale=(.87,.84,1.16); material(obj,'Skin')
    box('Cap',(0,.015,2.10),(.46,.43,.17),'Leather',.04)
    box('Cap peak',(0,-.22,2.04),(.42,.28,.055),'Leather',.015)
build('OfficerPlaceholder',officer)

def officer_arm():
    # Origin at the shoulder; neutral arm hangs down. Unity rotates this pivot.
    beam('Sleeve',(0,0,0),(0,0,-.58),.12,'Uniform')
    beam('Hand',(0,0,-.58),(0,0,-.79),.078,'Skin')
    beam('Pointing finger',(0,-.025,-.75),(0,-.025,-1.02),.032,'Skin')
build('OfficerArm',officer_arm)

def officer_thigh():
    # Origin at the hip, length half a metre toward the knee.
    box('Upper trouser',(0,0,-.25),(.25,.30,.50),'Olive',.025)
build('OfficerThigh',officer_thigh)

def officer_shin():
    # Origin at the knee; the boot soles rest on the stage at zero knee bend.
    box('Lower trouser',(0,0,-.19),(.24,.28,.38),'Olive',.025)
    box('Boot',(0,-.10,-.43),(.25,.47,.26),'Leather',.035)
build('OfficerShin',officer_shin)

def terrain():
    verts=[]; faces=[]; n=33; step=2.5
    for row in range(n):
        depth=(row-(n-1)/2)*step
        for col in range(n):
            x=(col-(n-1)/2)*step
            distance=max(abs(x)/24,abs(depth-5)/24)
            height=-.42+random.uniform(-.055,.055)
            if distance>1:
                height+=(distance-1)*random.uniform(3,7)
            if abs(x)<10.8 and abs(depth)<4.5:
                height=-1.75+random.uniform(-.13,.13)
            verts.append((x,depth,height))
    for row in range(n-1):
        for col in range(n-1):
            a=row*n+col
            faces.extend([(a,a+1,a+n+1),(a,a+n+1,a+n)])
    obj=mesh('Faceted earth',verts,faces,['Earth','EarthLight','Grass','GrassLight','Trench'])
    for face in obj.data.polygons:
        center=sum((Vector(verts[v]) for v in face.vertices),Vector())/3
        face.material_index=4 if abs(center.x)<10.8 and abs(center.y)<4.5 else random.choice([0,0,1,2,2,3])
build('Terrain',terrain)

for i, obj in enumerate(bpy.data.objects):
    obj.hide_set(False)
    obj.hide_render=False
    obj.location = (i % 4 * 10, i // 4 * 10, 0) if obj.name != 'Terrain' else (0, 65, 0)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Environment/OutpostProps.blend'))
import json
(ROOT/'Environment/outpost-palette.json').write_text(json.dumps(COLORS,indent=2)+'\n')
print('Outpost models exported to',OUTPUT)
