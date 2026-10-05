using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Offline asset inspection: every animation sheet, including unused attack variants.
public partial class CharacterAnimationLab : Control
{
    public sealed record Entry(string Name, string Scene, string Folder);
    public Entry[] Characters {get;private set;}=Array.Empty<Entry>();
    public AnimatedSprite2D Preview {get;private set;}=null!;
    public string[] Animations {get;private set;}=Array.Empty<string>();
    public int SelectedCharacter {get;private set;}=-1;
    private readonly Dictionary<string,SpriteFrames> _cache=new();
    private VBoxContainer _list=null!;
    private Control _stage=null!;
    private OptionButton _animations=null!;
    private HSlider _frames=null!;
    private Label _title=null!,_status=null!;
    private bool _updating,_loop=true;
    private float _zoom=4;
    public override void _Ready()
    {
        Characters=CharacterData.All.Where(c=>c.Enabled).GroupBy(c=>c.ScenePath).Select(g=>new Entry(CardCatalog.Groups[g.First().Group],g.Key,g.First().PortraitPath.GetBaseDir())).OrderBy(c=>c.Name).ToArray();
        var page=DeckMenuUi.Page(this);
        var header=new HBoxContainer();page.AddChild(header);
        header.AddChild(DeckMenuUi.Button("< LOBBY",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),120));
        var heading=DeckMenuUi.Text("CHARACTER ANIMATION LAB",24);heading.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(heading);
        header.AddChild(DeckMenuUi.Button("EFFECTS LAB",()=>GetTree().ChangeSceneToFile("res://scenes/damage_simulator.tscn"),160));
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",12);page.AddChild(body);
        var left=DeckMenuUi.Panel(body,220);left.AddChild(DeckMenuUi.Text($"CHARACTERS / {Characters.Length}",16));
        var search=DeckMenuUi.Input("","Search characters...",200);left.AddChild(search);search.TextChanged+=RebuildList;
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};left.AddChild(scroll);
        _list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_list);
        var center=DeckMenuUi.Panel(body);center.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _title=DeckMenuUi.Text("",20);center.AddChild(_title);
        _stage=new Control {SizeFlagsVertical=SizeFlags.ExpandFill,ClipContents=true,MouseFilter=MouseFilterEnum.Ignore};center.AddChild(_stage);
        Preview=new AnimatedSprite2D {Name="CharacterPreview",TextureFilter=TextureFilterEnum.Nearest};_stage.AddChild(Preview);_stage.Resized+=LayoutPreview;
        Preview.FrameChanged+=UpdateStatus;Preview.AnimationFinished+=UpdateStatus;
        _status=DeckMenuUi.Text("",14);_status.AutowrapMode=TextServer.AutowrapMode.WordSmart;center.AddChild(_status);
        var right=DeckMenuUi.Panel(body,254);right.AddChild(DeckMenuUi.Text("PLAYBACK",18));
        _animations=new OptionButton {CustomMinimumSize=new(230,40)};right.AddChild(_animations);_animations.ItemSelected+=index=>SelectAnimation((int)index);
        right.AddChild(DeckMenuUi.Button("PLAY / RESTART",Play,230));
        right.AddChild(DeckMenuUi.Button("PAUSE",Pause,230));
        var loop=new CheckButton {Text="Loop animation",ButtonPressed=true};right.AddChild(loop);loop.Toggled+=SetLoop;
        var flip=new CheckButton {Text="Face left",ButtonPressed=false};right.AddChild(flip);flip.Toggled+=value=>Preview.FlipH=value;
        right.AddChild(DeckMenuUi.Text("FRAME",14));
        _frames=new HSlider {MinValue=1,Step=1,CustomMinimumSize=new(230,26)};right.AddChild(_frames);_frames.ValueChanged+=value=>{if(!_updating)Scrub((int)value-1);};
        var step=new HBoxContainer();right.AddChild(step);step.AddChild(DeckMenuUi.Button("< FRAME",()=>Step(-1),110));step.AddChild(DeckMenuUi.Button("FRAME >",()=>Step(1),110));
        var speedLabel=DeckMenuUi.Text("SPEED / 1.00x",14);right.AddChild(speedLabel);
        var speed=new HSlider {MinValue=.1,MaxValue=3,Step=.05,Value=1,CustomMinimumSize=new(230,26)};right.AddChild(speed);speed.ValueChanged+=value=>{Preview.SpeedScale=(float)value;speedLabel.Text=$"SPEED / {value:0.00}x";};
        var zoomLabel=DeckMenuUi.Text("ZOOM / 4x",14);right.AddChild(zoomLabel);
        var zoom=new HSlider {MinValue=1,MaxValue=8,Step=1,Value=4,CustomMinimumSize=new(230,26)};right.AddChild(zoom);zoom.ValueChanged+=value=>{_zoom=(float)value;LayoutPreview();zoomLabel.Text=$"ZOOM / {value:0}x";};
        right.AddChild(DeckMenuUi.Text("All original animation frames.\nAttack variants are listed separately.\nPreview does not change gameplay.",13));
        RebuildList("");SelectCharacter(0);
    }
    private void RebuildList(string query)
    {
        DeckMenuUi.Clear(_list);
        for(int i=0;i<Characters.Length;i++){
            int index=i;if(!Characters[i].Name.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            var button=DeckMenuUi.Button(Characters[i].Name,()=>SelectCharacter(index),200,38);_list.AddChild(button);
        }
    }
    public void SelectCharacter(int index)
    {
        if(index<0||index>=Characters.Length)return;
        SelectedCharacter=index;Preview.Stop();
        var entry=Characters[index];
        if(!_cache.TryGetValue(entry.Scene,out var frames)){
            var actor=GD.Load<PackedScene>(entry.Scene).Instantiate<Node2D>();
            frames=(SpriteFrames)actor.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames.Duplicate();actor.Free();
            if(frames.HasAnimation("default") && frames.GetFrameCount("default")==0)frames.RemoveAnimation("default");
            // The original Knight has alternate attacks only in its combined atlas.
            if(entry.Folder=="res://assets/characters/knight") {
                var atlas=GD.Load<Texture2D>(entry.Folder+"/knight_atlas.png");
                foreach(var attack in new[]{(Name:"attack01",Row:2,Count:6),(Name:"attack02",Row:3,Count:6),(Name:"attack03",Row:4,Count:9)}) {
                    frames.AddAnimation(attack.Name);frames.SetAnimationSpeed(attack.Name,12);
                    for(int f=0;f<attack.Count;f++)frames.AddFrame(attack.Name,new AtlasTexture {Atlas=atlas,Region=new Rect2(f*100,attack.Row*100,100,100)});
                }
            }
            using var dir=DirAccess.Open(entry.Folder);
            if(dir==null)throw new InvalidOperationException("Missing character folder: "+entry.Folder);
            foreach(string exported in dir.GetFiles().OrderBy(n=>n)){
                string file=exported.EndsWith(".png.remap")?exported[..^6]:exported;
                if(!file.EndsWith(".png") || file.Contains("atlas") || file.Contains("shadow"))continue;
                var texture=GD.Load<Texture2D>(entry.Folder+"/"+file);
                if(texture.GetWidth()%100!=0 || texture.GetHeight()%100!=0)continue;
                string name=file.GetBaseName();string prefix=entry.Folder.GetFile()+"_";if(name.StartsWith(prefix))name=name[prefix.Length..];
                if(frames.HasAnimation(name))continue;
                frames.AddAnimation(name);frames.SetAnimationSpeed(name,name is "idle" or "walk" or "flying"?8:12);
                frames.SetAnimationLoopMode(name,name is "idle" or "walk" or "flying"?SpriteFrames.LoopMode.Linear:SpriteFrames.LoopMode.None);
                for(int y=0;y<texture.GetHeight();y+=100)for(int x=0;x<texture.GetWidth();x+=100)
                    frames.AddFrame(name,new AtlasTexture {Atlas=texture,Region=new Rect2(x,y,100,100)});
            }
            _cache[entry.Scene]=frames;
        }
        Preview.SpriteFrames=frames;Preview.Offset=Vector2.Zero;
        Animations=frames.GetAnimationNames().OrderBy(n=>n is "idle" or "flying"?0:1).ThenBy(n=>n).ToArray();
        _animations.Clear();foreach(string name in Animations)_animations.AddItem(name.Replace('_',' ').ToUpperInvariant());
        _title.Text=entry.Name;SelectAnimation(0);LayoutPreview();
    }
    public void SelectAnimation(int index)
    {
        if(index<0||index>=Animations.Length)return;
        _animations.Select(index);Preview.Stop();Preview.Animation=Animations[index];
        _updating=true;_frames.MaxValue=Preview.SpriteFrames.GetFrameCount(Preview.Animation);_frames.Value=1;_updating=false;
        SetLoop(_loop);Play();
    }
    public void Play(){Preview.SetFrameAndProgress(0,0);Preview.Play();UpdateStatus();}
    public void Pause(){Preview.Pause();UpdateStatus();}
    public void SetLoop(bool value){_loop=value;if(Preview.SpriteFrames!=null)Preview.SpriteFrames.SetAnimationLoopMode(Preview.Animation,value?SpriteFrames.LoopMode.Linear:SpriteFrames.LoopMode.None);}
    public void Scrub(int frame){Preview.Pause();Preview.SetFrameAndProgress(Math.Clamp(frame,0,Preview.SpriteFrames.GetFrameCount(Preview.Animation)-1),0);UpdateStatus();}
    public void Step(int direction)=>Scrub(Preview.Frame+direction);
    private void LayoutPreview(){Preview.Position=_stage.Size/2;Preview.Scale=new(_zoom,_zoom);}
    private void UpdateStatus()
    {
        if(Preview.SpriteFrames==null || _frames==null || !Preview.SpriteFrames.HasAnimation(Preview.Animation))return;
        int count=Preview.SpriteFrames.GetFrameCount(Preview.Animation);
        _updating=true;_frames.Value=Preview.Frame+1;_updating=false;
        _status.Text=$"{Preview.Animation}  /  FRAME {Preview.Frame+1} OF {count}  /  {Preview.SpriteFrames.GetAnimationSpeed(Preview.Animation):0} FPS\n{(Preview.IsPlaying()?"PLAYING":"PAUSED")}  /  100 × 100 source frames";
    }
}
