"""Apply the reviewed combat design while retaining stable kinds and deck price tiers."""
import argparse, json, math, subprocess, zipfile, xml.etree.ElementTree as E
from pathlib import Path
from PIL import Image
from combat_animation_variety import apply_animation_variety
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--backend',required=True,type=Path)
parser.add_argument('--workbook',required=True,type=Path)
parser.add_argument('--godot',required=True,type=Path,help='Godot .NET executable; build the project before importing.')
args=parser.parse_args();backend=args.backend;book=args.workbook
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(book) as z:
    shared=[''.join(t.itertext()) for t in E.fromstring(z.read('xl/sharedStrings.xml')).findall('m:si',ns)]
    sheet=E.fromstring(z.read('xl/worksheets/sheet1.xml'))
    design={}
    for row in sheet.findall('m:sheetData/m:row',ns):
        cells={}
        for c in row.findall('m:c',ns):
            v=c.find('m:v',ns)
            if v is not None: cells[''.join(x for x in c.attrib['r'] if x.isalpha())]=shared[int(v.text)] if c.attrib.get('t')=='s' else v.text
        if int(row.attrib['r'])>=7 and cells.get('A'):
            design[cells['A'].lower().replace(' ','_')]=cells
def atk(anim='attack01',mode='melee',hits=1,power=100,**kw):return dict(animation=anim,mode=mode,hits=hits,power=power,**kw)
profiles={}
def profile(slug,actions=None,**kw):profiles[slug]=dict(actions=actions or [atk()],**kw)
for slug in design:profile(slug)
for slug in ['archer','skeleton_archer','ghostfire','hellbat','lava_slime','demoness_b','blood_monster_b','demon_c']:profile(slug,[atk(mode='ranged')])
profile('armored_axeman',[atk(),atk('attack02',splash='sides',splash_power=25,min_stars=2)],finisher='attack03')
profile('armored_orc',[atk(),atk('attack02',splash='back',splash_power=40,min_stars=2)],finisher='attack03')
# At >=2 stars every normal action uses the corresponding area attack.
profiles['armored_axeman']['actions']=[atk('attack02',splash='sides',splash_power=25,min_stars=2)]
profiles['armored_orc']['actions']=[atk('attack02',splash='back',splash_power=40,min_stars=2)]
profile('armored_skeleton',[atk(),atk('attack02',splash='around',splash_power=25)])
profile('black_knight_a',[atk(hits=2,power=120),atk('attack02',splash='back',splash_power=40,status='fire'),atk('attack03',splash='around',splash_power=25,status='stun')])
profile('black_knight_b',[atk(),atk('attack02'),atk('attack03',status='stun')])
profile('black_knight_c',[atk(pierce=50),atk(pierce=50),atk('attack02',pierce=50,splash='back',splash_power=40)],execute=25)
profile('blood_monster',[atk(),atk('attack02',lifesteal=3)])
profile('blood_monster_b',[atk(mode='ranged'),atk('attack02',mode='ranged',lifesteal=3)])
profile('demon_b',[atk(),atk('attack02',mode='ranged')])
profile('demon_c',[atk('attack02',mode='ranged'),atk(mode='ranged',status='fire')])
profile('demon_d',[atk('attack03'),atk('attack02'),atk(status='stun')],kill_status='fire',kill_splash='sides',finisher='attack03')
profile('demon_e',[atk(),atk('attack02',hits=2,power=120)],finisher='attack03')
profile('demoness',[atk(),atk('attack02',mode='curse',hits=0,power=0)])
profile('demoness_a',[atk(team_heal=2),atk('attack02',mode='curse',hits=0,power=0)],finisher='attack03')
profile('demoness_b',[atk(mode='ranged',lifesteal=2)],finisher='attack02')
profile('elite_orc',[atk(),atk('attack02',splash='around',splash_power=30)],finisher='attack03')
profile('eyeball_monster',kill_status='poison',kill_splash='back',finisher='attack03')
profile('flame_golem',[atk(),atk('attack02',power=110)],kill_status='stun',kill_splash='back',finisher='attack03')
profile('ghostfire',[atk(mode='ranged',status='fire')],finisher='attack02')
profile('greatsword_skeleton',[atk(),atk('attack02'),atk('attack03')])
profile('hellbat',[atk(mode='ranged')],suicide=True,finisher='attack02')
profile('hellhound',finisher='attack02')
profile('knight',bodyguard=True)
profile('soldier',[atk(),atk('attack02',pierce=50)])
profile('knight_templar',[atk(splash='back',splash_power=40),atk('attack02')],block_first=True,finisher='attack03',finisher_hits=2)
profile('knight_vanguard',[atk(),atk('attack02',hits=2,power=120)],kill_status='fire',kill_splash='column',finisher='attack03')
profile('lancer',[atk(),atk('attack02',swap_back=True,anti_heal=True)],finisher='attack03')
profile('lava_slime',[atk(mode='ranged'),atk(mode='ranged'),atk('attack02',mode='ranged',status='stun')])
profile('minotaur',first=atk(splash='back',splash_power=40))
profile('orc',first_bonus=2)
profile('orc_rider',first=atk(mode='ranged',target_back=True))
profile('slime',first_heal=2)
profile('werebear',low_armor=5)
profile('priest',[atk(),atk('attack02',mode='heal',hits=0,power=0,heal=4)])
profile('skeleton',block_first=True)
profile('skeleton_archer',[atk(mode='ranged')],death_armor=2)
profile('swordsman',[atk(),atk('attack02',hits=2,power=120),atk('attack03',hits=4,power=140)])
profile('warlock',[atk(),atk('attack02',mode='summon',hits=0,power=0)])
profile('werewolf',kill_no_counter=True,finisher='attack02')
profile('wizard',[atk('attack01',mode='revive',hits=0,power=0)])
# Status skills start immediately so short battles can show them.
for slug in ['black_knight_b', 'demon_d', 'lava_slime']:
    actions=profiles[slug]['actions']
    profiles[slug]['actions']=[actions[-1], *actions[:-1]]
# Lava Slime alternates stun and basic attacks rather than waiting three turns.
profiles['lava_slime']['actions']=profiles['lava_slime']['actions'][:2]
for slug in ['demon_c', 'demoness_a', 'lancer']:
    profiles[slug]['actions'].reverse()
# Black Knight A applies fire or stun on each action, including adjacent targets.
a=profiles['black_knight_a']['actions']
a[0]['status']='fire'
a[1]['splash_status']='fire'
a[2]['splash_status']='stun'
profiles['black_knight_a']['actions']=[a[1], a[2], a[0]]
# Poison no longer depends on scoring a kill with a surviving neighbor.
profiles['eyeball_monster']['actions'][0]['status']='poison'
# These kill passives remain, with regular attacks also applying their status.
profiles['flame_golem']['actions'][1]['status']='stun'
profiles['flame_golem']['actions'].reverse()
profiles['knight_vanguard']['actions'][0]['status']='fire'
profiles['knight_vanguard']['actions'][1]['status']='fire'
for slug, rules in profiles.items():
    apply_animation_variety(slug, rules, root)
def timing(slug,a):
    path=root/'assets/characters'/slug/(slug+'_'+a['animation']+'.png')
    if slug=='swordsman' and a['animation']=='attack03':
        path=root/'assets/characters/swordsman/swordsman_attack3.png'
    if slug=='knight':
        a['frames']=9 if a['animation']=='attack03' else 6
        a['hit_frames']=[max(1,round((i+1)*a['frames']/(a['hits']+1))) for i in range(a['hits'])]
        return
    if not path.exists():
        # Original Knight's alternate attacks are in the atlas; basic attack is already in its scene.
        fallback=root/'assets/characters'/slug/(slug+'_attack01.png')
        if not fallback.exists():fallback=root/'assets/characters'/slug/(slug+'_attack.png')
        path=fallback
    if path.exists():
        w,h=Image.open(path).size;a['frames']=(w//100)*(h//100)
    else:a['frames']=9
    count=a['hits'];a['hit_frames']=[max(1,round((i+1)*a['frames']/(count+1))) for i in range(count)]
    a['hit_frames']=[min(a['frames']-1,f) for f in a['hit_frames']]
catalog=json.loads((root/'data/characters.json').read_text(encoding='utf-8'))
updated=0
for c in catalog['characters']:
    slug=Path(c['scene_path']).stem
    if slug not in design:continue
    d=design[slug];refcost=int(d['C']);basehp=int(d['D']);armor=int(d['E'])
    rules=json.loads(json.dumps(profiles[slug]))
    for a in rules['actions']:timing(slug,a)
    if 'first' in rules:timing(slug,rules['first'])
    if rules.get('finisher'):
        fin=atk(rules['finisher'],hits=rules.get('finisher_hits',1));timing(slug,fin)
        rules['finisher_frames']=fin['frames'];rules['finisher_hit_frames']=fin['hit_frames']
    c['combat']=rules;c['ability_key']='character_combat'
    c['role']=d['B']
    for s,m in zip(c['stats'],[1,1.7,2.4,3.1]):
        hp=max(1,math.floor(basehp*c['cost']/refcost*m+.5));ar=math.floor(armor*c['cost']/refcost*m+.5)
        s.update(hp=hp,attack=hp,damage_min=hp,damage_max=hp,speed=10,armor=ar)
    updated+=1
payload=json.dumps(catalog,ensure_ascii=False,indent=2)+'\n'
destinations=[root/'data/characters.json',backend/'data/characters.json',backend/'internal/game/data/characters.json']
for dest in destinations:dest.write_text(payload,encoding='utf-8')
# Generate player-facing descriptions from actual rules, never workbook development notes.
subprocess.run([str(args.godot),'--headless','--path',str(root),'res://tools/character_copy_export.tscn','--',*[str(p) for p in destinations]],check=True)
print('Updated',updated,'card definitions; profiles:',len(profiles),'Demoness remains without a catalog asset')
