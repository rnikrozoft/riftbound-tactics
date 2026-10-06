using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class CombatSimulatorLab : Control
{
    public CharacterDefinition[] Characters {get;private set;}=Array.Empty<CharacterDefinition>();
    public EffectEntry[] Effects {get;private set;}=Array.Empty<EffectEntry>();
    public BattleUnit Attacker {get;private set;}=null!;
    public BattleUnit Target {get;private set;}=null!;
    public CombatVisualChoice Choice {get;private set;}=new();
    public bool Busy {get;private set;}
    public bool Dirty {get;private set;}
    private readonly Dictionary<string,CombatVisualChoice> _drafts=new();
    private OptionButton _a=null!,_b=null!,_source=null!,_animation=null!,_effect=null!,_anchor=null!;
    private SpinBox _x=null!,_y=null!,_size=null!,_zoom=null!;
    private Label _status=null!,_details=null!;
    private Control _stage=null!;
    private Node2D _arena=null!;
    private AnimatedSprite2D? _previewEffect;
    private TextureButton _save=null!;
    private int _generation;
    private bool _updating,_exiting;
    private string _effectQuery="";
    private readonly List<string> _visibleEffects=new();
    public string[] DisplayAnimations {get;private set;}=Array.Empty<string>();
    private string[] _sourceAnimations=Array.Empty<string>();
    private string SourceAnimation=>_sourceAnimations[_source.Selected];
    public override void _Ready()
    {
        Characters=CharacterData.All.Where(c=>c.Enabled).GroupBy(c=>c.ScenePath).Select(g=>g.First()).OrderBy(c=>CharacterData.Groups[c.Group]).ToArray();
        Effects=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray)??Array.Empty<EffectEntry>();
        BuildUi();SelectCharacters(0,Math.Min(1,Characters.Length-1));
    }
    private OptionButton Picker(VBoxContainer box,string label)
    {
        box.AddChild(DeckMenuUi.Text(label,14));var picker=new OptionButton {FitToLongestItem=false,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,CustomMinimumSize=new(300,32),SizeFlagsHorizontal=SizeFlags.ExpandFill};box.AddChild(picker);return picker;
    }
    private SpinBox Number(VBoxContainer box,string label,double min,double max,double step,double value)
    {
        var row=new HBoxContainer();box.AddChild(row);var title=DeckMenuUi.Text(label,13);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(title);
        var number=new SpinBox {MinValue=min,MaxValue=max,Step=step,Value=value,CustomMinimumSize=new(120,32)};row.AddChild(number);return number;
    }
    private void BuildUi()
    {
        var page=DeckMenuUi.Page(this);var header=new HBoxContainer();page.AddChild(header);
        header.AddChild(DeckMenuUi.Button("< LOBBY",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),110));
        var title=DeckMenuUi.Text("COMBAT SIMULATOR",24);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        header.AddChild(DeckMenuUi.Button("EXPORT JSON",Export,140));
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",14);page.AddChild(body);
        var settings=DeckMenuUi.Panel(body,340);var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};settings.AddChild(scroll);
        var controls=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};controls.AddThemeConstantOverride("separation",4);scroll.AddChild(controls);
        _a=Picker(controls,"ตัวละคร A / ผู้โจมตี");_b=Picker(controls,"ตัวละคร B / เป้าหมายทดลอง");
        foreach(var c in Characters){string name=CharacterData.Groups[c.Group];_a.AddItem(name);_b.AddItem(name);}
        _a.ItemSelected+=i=>{if(!_updating)SelectCharacters((int)i,_b.Selected);};_b.ItemSelected+=i=>{if(!_updating)SelectCharacters(_a.Selected,(int)i);};
        _source=Picker(controls,"ท่าในเกมที่ต้องการปรับ");
        _source.ItemSelected+=i=>{if(!_updating)LoadChoice();};
        _animation=Picker(controls,"แอนิเมชันที่แสดง / ท่าที่ไม่ซ้ำ");
        _animation.ItemSelected+=i=>Changed();
        controls.AddChild(DeckMenuUi.Text("PARTICLE / EFFECT",14));
        var search=DeckMenuUi.Input("","ค้นหาเอฟเฟกต์ / fire, heal, impact...",300);controls.AddChild(search);search.TextChanged+=query=>{_effectQuery=query;RebuildEffects();};
        _effect=Picker(controls,"เอฟเฟกต์บน B");_effect.Name="TargetEffectPicker";
        _effect.TooltipText="ใช้ลูกศรขึ้น/ลงเพื่อเลือกและเล่นเอฟเฟกต์บน B ทันที";
        _effect.ItemSelected+=i=>SelectEffect((int)i);
        _effect.ItemFocused+=i=>{if(!_updating&&i!=_effect.Selected)SelectEffect((int)i);};
        _effect.GuiInput+=input=>{
            if(input is InputEventKey key&&key.Pressed&&!_effect.GetPopup().Visible&&(key.Keycode is Key.Up or Key.Down)){
                SelectEffect(Mathf.Clamp(_effect.Selected+(key.Keycode==Key.Down?1:-1),0,_visibleEffects.Count-1));_effect.AcceptEvent();
            }
        };
        _anchor=Picker(controls,"จุดยึดเอฟเฟกต์");foreach(string name in new[]{"อัตโนมัติ","กลางตัว B","เท้า B"})_anchor.AddItem(name);_anchor.ItemSelected+=i=>Changed();
        _x=Number(controls,"ตำแหน่ง X (ขวา +)",-200,200,1,0);_y=Number(controls,"ตำแหน่ง Y (ลง +)",-200,200,1,0);_size=Number(controls,"ขนาดเอฟเฟกต์ (เท่า)",.1,5,.05,1);
        foreach(var n in new[]{_x,_y,_size})n.ValueChanged+=v=>Changed();
        controls.AddChild(DeckMenuUi.Text("ตำแหน่งเป็นหน่วยสนามจริง\nค่าที่เซฟใช้กับ A ทุกระดับดาวและทุกเป้าหมาย",12));
        var center=DeckMenuUi.Panel(body);center.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _details=DeckMenuUi.Text("",15);_details.AutowrapMode=TextServer.AutowrapMode.WordSmart;center.AddChild(_details);
        _stage=new Control {SizeFlagsVertical=SizeFlags.ExpandFill,ClipContents=true,CustomMinimumSize=new(360,240),MouseFilter=MouseFilterEnum.Ignore};center.AddChild(_stage);
        var background=new ColorRect {Color=new Color("172030"),MouseFilter=MouseFilterEnum.Ignore};_stage.AddChild(background);background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _arena=new Node2D {Name="PreviewArena"};_stage.AddChild(_arena);_stage.Resized+=LayoutStage;
        _zoom=Number(center,"ซูมภาพทดลอง",1,6,.25,4);_zoom.ValueChanged+=v=>LayoutStage();
        var buttons=new HBoxContainer();center.AddChild(buttons);
        var play=DeckMenuUi.Button("PLAY ATTACK",()=>{_ = PlayAttack();},180,44);play.Name="PlayAttackButton";buttons.AddChild(play);
        _save=DeckMenuUi.Button("SAVE / ใช้ในเกม",()=>SaveChoice(),190,44);_save.Name="SaveCombatVisualButton";GameUi.Primary(_save);buttons.AddChild(_save);
        buttons.AddChild(DeckMenuUi.Button("RESET",ResetChoice,100,44));
        _status=DeckMenuUi.Text("",13);_status.AutowrapMode=TextServer.AutowrapMode.WordSmart;center.AddChild(_status);
        center.AddChild(DeckMenuUi.Text("ทดลองภาพโจมตีด้วยท่าที่เลือก • SAVE มีผลทันทีในเกมบนเครื่องนี้\nEXPORT JSON ใช้ส่งต่อค่าที่ปรับให้โปรเจกต์เกม",12));
        RebuildEffects();
    }
    private string DraftKey()=>CombatVisualSettings.Slug(Attacker.CardKind)+":"+SourceAnimation;
    public void SelectCharacters(int a,int b)
    {
        if(a<0||a>=Characters.Length||b<0||b>=Characters.Length)return;
        _generation++;Busy=false;StopEffect();
        if(Attacker!=null){_arena.RemoveChild(Attacker);Attacker.QueueFree();}
        if(Target!=null){_arena.RemoveChild(Target);Target.QueueFree();}
        _updating=true;_a.Select(a);_b.Select(b);_updating=false;
        Attacker=CreateUnit(Characters[a],true);Target=CreateUnit(Characters[b],false);
        _arena.AddChild(Attacker);_arena.AddChild(Target);RebuildAnimations();LayoutStage();LoadChoice();
    }
    private void RebuildAnimations()
    {
        string previous=_sourceAnimations.Length>0&&_source.Selected>=0?SourceAnimation:"attack01";
        DisplayAnimations=UniqueCharacterAnimations.Names(Attacker.Sprite.SpriteFrames,new[]{"attack01","attack02","attack03"});
        var rules=CharacterData.Get(Attacker.CardKind).Combat;
        _sourceAnimations=(rules?.Actions.Select(a=>a.Animation)??new[]{"attack01"})
            .Concat(rules?.First!=null?new[]{rules.First.Animation}:Array.Empty<string>())
            .Concat(rules?.Finisher.Length>0?new[]{rules.Finisher}:Array.Empty<string>())
            .Concat(rules?.Execute>0?new[]{"attack03"}:Array.Empty<string>()).Distinct().OrderBy(n=>n).ToArray();
        _updating=true;_source.Clear();foreach(string name in _sourceAnimations)_source.AddItem(name);
        _source.Select(Math.Max(0,Array.IndexOf(_sourceAnimations,previous)));
        _animation.Clear();foreach(string name in DisplayAnimations)_animation.AddItem(name);_updating=false;
    }
    private static BattleUnit CreateUnit(CharacterDefinition definition,bool ally)
    {
        var unit=GD.Load<PackedScene>(definition.ScenePath).Instantiate<BattleUnit>();unit.CardKind=definition.Kind;unit.ServerId=ally?"preview:A":"preview:B";unit.SetMeta("team",ally?"Ally":"Enemy");unit.ConfigureStars(1);return unit;
    }
    private void LayoutStage()
    {
        if(_arena==null||_zoom==null)return;
        _arena.Position=new Vector2(_stage.Size.X*.5f,_stage.Size.Y*.62f);_arena.Scale=Vector2.One*(float)_zoom.Value;
        if(Attacker!=null)Attacker.Position=new(-48,0);if(Target!=null)Target.Position=new(48,0);
    }
    public void SelectSourceAnimation(string animation)
    {
        int index=Array.IndexOf(_sourceAnimations,animation);if(index<0)return;_source.Select(index);LoadChoice();
    }
    private void LoadChoice()
    {
        string original=SourceAnimation;
        Choice=_drafts.TryGetValue(DraftKey(),out var draft)?draft.Copy():CombatVisualSettings.Find(Attacker.CardKind,original)??new() {
            Character=CombatVisualSettings.Slug(Attacker.CardKind),SourceAnimation=original,Animation=original,Effect=CharacterImpactEffects.DefaultEffect(Attacker.CardKind,original)};
        SyncControls();RefreshPreview();UpdateStatus();
    }
    private void SyncControls()
    {
        _updating=true;Choice.Animation=UniqueCharacterAnimations.Canonical(Attacker.Sprite.SpriteFrames,Choice.Animation,DisplayAnimations);
        _animation.Select(Array.IndexOf(DisplayAnimations,Choice.Animation));_anchor.Select(Array.IndexOf(new[]{"auto","body","feet"},Choice.Anchor));
        _x.Value=Choice.OffsetX;_y.Value=Choice.OffsetY;_size.Value=Choice.Scale;RebuildEffects();_updating=false;
    }
    private void RebuildEffects()
    {
        bool wasUpdating=_updating;_updating=true;_visibleEffects.Clear();_effect.Clear();_visibleEffects.Add("");_effect.AddItem("ไม่มีเอฟเฟกต์");
        foreach(var e in Effects.Where(e=>e.Name.Contains(_effectQuery,StringComparison.OrdinalIgnoreCase)||e.Category.Contains(_effectQuery,StringComparison.OrdinalIgnoreCase)||e.Name==Choice.Effect)){
            _visibleEffects.Add(e.Name);_effect.AddItem(e.Name.Replace('_',' '));
        }
        _effect.Select(Math.Max(0,_visibleEffects.IndexOf(Choice.Effect)));_updating=wasUpdating;
    }
    private void SelectEffect(int index)
    {
        if(_updating||index<0||index>=_visibleEffects.Count)return;
        CancelAttackPreview();
        _effect.Select(index);Changed(true);
    }
    private void CancelAttackPreview()
    {
        if(!Busy)return;
        _generation++;Busy=false;LayoutStage();
        foreach(var effect in _arena.GetChildren().OfType<AnimatedSprite2D>().Where(e=>e.HasMeta("effect_name"))){_arena.RemoveChild(effect);effect.QueueFree();}
        Attacker.Sprite.Play(BattleAnimations.Idle);Target.Sprite.Play(BattleAnimations.Idle);
    }
    private void Changed(bool playAttack=false)
    {
        if(_updating||Attacker==null)return;
        Choice.Animation=DisplayAnimations[_animation.Selected];Choice.Effect=_visibleEffects[_effect.Selected];Choice.Anchor=new[]{"auto","body","feet"}[_anchor.Selected];
        Choice.OffsetX=(float)_x.Value;Choice.OffsetY=(float)_y.Value;Choice.Scale=(float)_size.Value;
        _drafts[DraftKey()]=Choice.Copy();UpdateStatus();
        if(playAttack)_ = PlayAttack();else RefreshPreview();
    }
    public void SetChoice(CombatVisualChoice choice)
    {
        if(!CombatVisualSettings.Valid(choice)||choice.Character!=CombatVisualSettings.Slug(Attacker.CardKind))throw new ArgumentException("Invalid preview choice");
        CancelAttackPreview();
        int sourceIndex=Array.IndexOf(_sourceAnimations,choice.SourceAnimation);
        if(sourceIndex<0)throw new ArgumentException("This character does not use the source attack");
        _source.Select(sourceIndex);Choice=choice.Copy();_drafts[DraftKey()]=Choice.Copy();SyncControls();RefreshPreview();UpdateStatus();
    }
    private void UpdateStatus()
    {
        var saved=CombatVisualSettings.Find(Attacker.CardKind,Choice.SourceAnimation);
        var baseline=saved??new CombatVisualChoice {Character=Choice.Character,SourceAnimation=Choice.SourceAnimation,Animation=Choice.SourceAnimation,Effect=CharacterImpactEffects.DefaultEffect(Attacker.CardKind,Choice.SourceAnimation)};
        baseline.Animation=UniqueCharacterAnimations.Canonical(Attacker.Sprite.SpriteFrames,baseline.Animation,DisplayAnimations);
        Dirty=System.Text.Json.JsonSerializer.Serialize(baseline,GameJsonContext.Default.CombatVisualChoice)!=System.Text.Json.JsonSerializer.Serialize(Choice,GameJsonContext.Default.CombatVisualChoice);
        _status.Text=Dirty?"ยังไม่ได้เซฟ • กด PLAY เพื่อทดลอง แล้ว SAVE เพื่อใช้ในเกม":saved!=null?"โหลดค่าที่เซฟไว้แล้ว • เกมใช้ค่านี้อยู่":"ค่าเริ่มต้น • ปรับแล้วกด SAVE เพื่อใช้ในเกม";
        var rules=CharacterData.Get(Attacker.CardKind).Combat;
        bool used=rules?.Actions.Any(a=>a.Animation==Choice.SourceAnimation)==true||rules?.First?.Animation==Choice.SourceAnimation||rules?.Finisher==Choice.SourceAnimation;
        _details.Text=$"A: {CharacterData.Groups[Characters[_a.Selected].Group]}  →  B: {CharacterData.Groups[Characters[_b.Selected].Group]}\n{Choice.SourceAnimation}  →  {Choice.Animation}"+(used?"":"\nท่าต้นทางนี้ยังไม่อยู่ในรอบโจมตีของตัวละคร เลือกท่าต้นทางที่ใช้อยู่เพื่อเห็นในเกม");
    }
    private void StopEffect(){if(GodotObject.IsInstanceValid(_previewEffect)){_previewEffect!.GetParent()?.RemoveChild(_previewEffect);_previewEffect.QueueFree();}_previewEffect=null;}
    private void RefreshPreview()
    {
        if(Busy||Attacker==null)return;StopEffect();Attacker.Sprite.Play(BattleAnimations.Idle);Target.Sprite.Play(BattleAnimations.Idle);
        _previewEffect=CharacterImpactEffects.PlayChoice(Attacker,Target,Choice);
        if(_previewEffect!=null){_previewEffect.Pause();_previewEffect.Frame=Math.Max(0,_previewEffect.SpriteFrames.GetFrameCount(BattleAnimations.Effect)/3);}
    }
    public async Task PlayAttack()
    {
        if(Busy||_exiting)return;Busy=true;int generation=++_generation;var choice=Choice.Copy();var actor=Attacker;var victim=Target;StopEffect();actor.ResetHealth();victim.ResetHealth();
        try {
            var rule=CharacterData.Get(actor.CardKind).Combat?.Actions.FirstOrDefault(a=>a.Animation==choice.SourceAnimation);
            var home=actor.Position;
            if(rule?.Mode=="melee"){
                actor.Sprite.Play(BattleAnimations.Walk);
                if(!await MovePreview(actor,victim.Position-new Vector2(22,0),generation))return;
            }
            actor.Sprite.Play(choice.Animation);actor.Sprite.SetFrameAndProgress(0,0);
            int count=actor.Sprite.SpriteFrames.GetFrameCount(choice.Animation);
            int[] impacts=rule?.HitFrames.Length>0?rule.HitFrames.Select(f=>CombatVisualSettings.RemapFrame(f,rule.Frames,count)).ToArray():new[]{Math.Max(1,count/2)};
            foreach(int frame in impacts){
                while(actor.Sprite.IsPlaying()&&actor.Sprite.Frame<frame){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_exiting||generation!=_generation)return;}
                if(_exiting||generation!=_generation)return;
                victim.Sprite.Play(BattleAnimations.Hit);victim.Sprite.SetFrameAndProgress(0,0);CharacterImpactEffects.PlayChoice(actor,victim,choice);
            }
            while(actor.Sprite.IsPlaying()){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_exiting||generation!=_generation)return;}
            if(rule?.Mode=="melee"){
                actor.Sprite.Play(BattleAnimations.Walk);
                if(!await MovePreview(actor,home,generation))return;
            }
            await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);
            if(_exiting||generation!=_generation)return;actor.Sprite.Play(BattleAnimations.Idle);victim.Sprite.Play(BattleAnimations.Idle);
        }finally {if(!_exiting&&generation==_generation){Busy=false;RefreshPreview();UpdateStatus();}}
    }
    private async Task<bool> MovePreview(BattleUnit unit,Vector2 destination,int generation)
    {
        Vector2 start=unit.Position;float elapsed=0;
        while(elapsed<.25f){
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_exiting||generation!=_generation)return false;
            elapsed+=(float)GetProcessDeltaTime();unit.Position=start.Lerp(destination,Mathf.Clamp(elapsed/.25f,0,1));
        }
        return true;
    }
    public bool SaveChoice()
    {
        if(CombatVisualSettings.Save(Choice,out string error)){_drafts.Remove(DraftKey());UpdateStatus();_status.Text="เซฟแล้ว • ใช้ในเกมจริงทันที และยังอยู่หลังเปิดเกมใหม่";return true;}
        _status.Text="เซฟไม่สำเร็จ: "+error;return false;
    }
    private void ResetChoice()
    {
        if(CombatVisualSettings.Reset(Attacker.CardKind,Choice.SourceAnimation,out string error)){_drafts.Remove(DraftKey());LoadChoice();_status.Text="กลับไปใช้ค่าเริ่มต้นแล้ว";}
        else _status.Text="รีเซ็ตไม่สำเร็จ: "+error;
    }
    private void Export()
    {
        var dialog=new FileDialog {FileMode=FileDialog.FileModeEnum.SaveFile,Access=FileDialog.AccessEnum.Filesystem,Title="Export saved combat visuals",CurrentFile="combat_visuals.json",UseNativeDialog=true};dialog.AddFilter("*.json","Combat visuals");AddChild(dialog);
        dialog.FileSelected+=path=>{try{CombatVisualSettings.AtomicWrite(path,CombatVisualSettings.Serialize(CombatVisualSettings.Combined()));_status.Text="ส่งออกค่าที่เซฟแล้ว: "+path;}catch(Exception e){_status.Text="ส่งออกไม่สำเร็จ: "+e.Message;}dialog.QueueFree();};dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered(new Vector2I(900,600));
    }
    public override void _ExitTree(){_exiting=true;_generation++;}
}
