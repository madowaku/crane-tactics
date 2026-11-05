"""Original procedural study toys, not a reproduction of the unavailable Library sheets.
Run with Blender --background --python ArtSource/create_toys.py.
Meters, Z-up authoring, -Y toy front; FBX +Z forward / Y up for this Unity import contract.
"""
import bpy, math, pathlib, json
from mathutils import Vector, Matrix
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/_CraneTactics/Art'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
colors={'ivory':(.87,.81,.64,1),'teal':(.08,.32,.33,1),'brass':(.83,.51,.15,1),'clay':(.65,.23,.12,1),'mint':(.2,.74,.48,1),'ink':(.025,.045,.05,1),'face':(.92,.69,.4,1),'indigo':(.19,.2,.37,1)}
mats={}
for name,color in colors.items():
    m=bpy.data.materials.new(name); m.diffuse_color=color; m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=color; bs.inputs['Roughness'].default_value=.3
    if name=='brass': bs.inputs['Metallic'].default_value=.65
    mats[name]=m
objects=[]
def shape(name,pos,scale,mat,kind='sphere'):
    if kind=='cube': bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    elif kind=='torus': bpy.ops.mesh.primitive_torus_add(major_radius=.21,minor_radius=.045,major_segments=24,minor_segments=8,location=pos,rotation=(math.pi/2,0,0))
    else: bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=.5,location=pos)
    o=bpy.context.object; o.name=name; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mats[mat])
    if kind=='cube':
        bevel=o.modifiers.new('Toy rounded edges','BEVEL'); bevel.width=.05; bevel.segments=3
        o.modifiers.new('Weighted toy normals','WEIGHTED_NORMAL')
    else:
        for p in o.data.polygons: p.use_smooth=True
    objects.append(o); return o
def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    # Unity's FBX importer restores its front-axis convention independently of the
    # exporter metadata. Bake the half-turn into the export objects, not gameplay.
    originals={o:o.matrix_world.copy() for o in objects}
    conversion=Matrix.Rotation(math.pi,4,'Z')
    for o in objects: o.matrix_world=conversion@o.matrix_world
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,add_leaf_bones=False)
    for o,matrix in originals.items(): o.matrix_world=matrix
    collection=bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    for o in objects:
        for old in list(o.users_collection): old.objects.unlink(o)
        collection.objects.link(o)
        o.hide_set(True); o.hide_render=True
    objects.clear()

# A one-metre calibration cube validates actual Unity import before cast admission.
shape('OneMeterCube',(0,0,.5),(1,1,1),'ivory','cube'); export('ScaleProbe')
shape('PositiveX',(1,0,0),(.1,.1,.1),'brass','cube')
shape('FrontMinusY',(0,-1,0),(.1,.1,.1),'teal','cube'); export('AxisProbe')
shape('Knight body',(0,0,.35),(.48,.4,.56),'ivory')
shape('Helmet',(0,0,.67),(.55,.48,.43),'ivory')
shape('Face opening',(0,-.23,.64),(.4,.09,.19),'face','cube')
for side in [-1,1]: shape('Knight eye',(side*.1,-.285,.65),(.052,.03,.09),'ink')
shape('Teal crest',(0,-.21,.81),(.08,.06,.15),'teal','cube')
shape('Cloak',(0,.18,.4),(.52,.16,.5),'teal','cube')
shape('Ring',(0,0,1.04),(1,1,1),'brass','torus')
for side in [-1,1]:
    shape('Boot',(side*.14,-.04,.09),(.22,.3,.18),'teal','cube')
    shape('Arm',(side*.29,0,.4),(.18,.23,.3),'ivory')
shape('Shield',(-.34,-.12,.4),(.24,.11,.36),'teal','cube')
export('Knight')
shape('Heavy torso',(0,0,.43),(.75,.56,.68),'clay','cube')
shape('Heavy brow',(0,-.05,.77),(.65,.5,.18),'clay','cube')
shape('Heavy face',(0,-.29,.6),(.4,.06,.21),'face','cube')
for side in [-1,1]:
    shape('Heavy feet',(side*.23,-.03,.09),(.3,.4,.18),'teal','cube')
    shape('Heavy fist',(side*.44,-.04,.37),(.26,.32,.32),'clay','cube')
    shape('Heavy eye',(side*.1,-.333,.62),(.045,.025,.085),'ink')
shape('Brass belt',(0,-.29,.29),(.55,.035,.09),'brass','cube')
export('Heavy')
shape('Slimy body',(0,0,.28),(.73,.62,.56),'mint')
shape('Slimy crown',(0,.02,.56),(.38,.34,.36),'mint')
for side in [-1,1]:
    shape('Slimy eye',(side*.13,-.285,.35),(.075,.045,.1),'ink')
    shape('Slimy foot',(side*.23,-.02,.08),(.3,.4,.16),'mint')
export('Slimy')
for name,guard in [('Scout',False),('Guard',True)]:
    shape(name+' body',(0,0,.3),(.54,.46,.5),'indigo')
    shape(name+' face',(0,-.22,.55),(.38,.08,.22),'face','cube')
    shape(name+' helmet',(0,0,.68),(.59,.49,.32),'indigo')
    for side in [-1,1]:
        shape(name+' eyes',(side*.1,-.27,.56),(.048,.025,.085),'ink')
        shape(name+' boots',(side*.15,-.03,.07),(.23,.3,.14),'indigo','cube')
        if guard: shape(name+' horn',(side*.32,0,.76),(.13,.14,.3),'ivory')
    if not guard:
        bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=.32,radius2=.04,depth=.42,location=(0,0,.92))
        bpy.context.object.data.materials.append(mats['indigo']); objects.append(bpy.context.object)
        shape('Wizard brim',(0,0,.72),(.85,.65,.1),'indigo')
    export(name)

# Decorative cabinet only; the runtime owns collision rails and the visible grid/chute.
shape('Cabinet body',(2.5,2.5,-.65),(7,7,.85),'teal','cube')
for x in [-.8,5.8]:
    shape('Back column',(x,5.65,1.5),(.26,.32,3.5),'teal','cube')
    shape('Column inlay',(x,5.44,1.5),(.09,.06,3.35),'brass','cube')
shape('Canopy',(2.5,5.65,3.25),(6.9,.55,.35),'teal','cube')
shape('Canopy trim',(2.5,5.33,3.2),(6.7,.08,.13),'brass','cube')
shape('Curtain backdrop',(2.5,5.85,1.9),(6.6,.12,2.6),'teal','cube')
shape('Moon medallion',(2.5,5.72,2.25),(.72,.08,.72),'brass')
shape('Moon shadow',(2.68,5.65,2.39),(.6,.08,.6),'teal')
for x in [0,2.5,5]:
    if x!=2.5: shape('Curtain gold accent',(x,5.75,2.15),(.035,.04,1.25),'brass','cube')
for y in [-.65,5.65]: shape('Cabinet gold rim',(2.5,y,-.13),(6.8,.13,.18),'brass','cube')
for x in [-.8,5.8]: shape('Cabinet gold side rim',(x,2.5,-.13),(.13,6.4,.18),'brass','cube')
export('Cabinet')
shape('Claw dome',(0,0,.04),(.58,.58,.3),'brass')
shape('Claw cuff',(0,0,.23),(.35,.35,.25),'teal','cube')
for i in range(3):
    angle=i*math.tau/3
    points=[Vector((math.cos(angle)*r,math.sin(angle)*r,z)) for r,z in [(.2,-.04),(.4,-.3),(.34,-.57),(.17,-.7)]]
    for a,b in zip(points,points[1:]):
        link=shape('Curved claw finger',(a+b)/2,(.11,.12,(b-a).length),'brass','cube'); link.rotation_mode='QUATERNION'; link.rotation_quaternion=(b-a).to_track_quat('Z','Y')
export('Claw')
# Source collection retains independent authored objects; models are exported independently.
# Keep only Knight visible when the source opens; all models have named collections.
for o in bpy.data.collections['Knight'].objects: o.hide_set(False); o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/toys.blend'))
print(json.dumps({'blender':bpy.app.version_string,'exports':[p.name for p in OUT.glob('*.fbx')],'units':'meters','source':'procedural original study'}))
