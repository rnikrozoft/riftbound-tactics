using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class CombatSimulatorLab : Control
{
    public static string ReturnScene {get;set;}="res://scenes/welcome.tscn";
    public CharacterDefinition[] Characters {get;private set;}=Array.Empty<CharacterDefinition>();
    public EffectEntry[] Effects {get;private set;}=Array.Empty<EffectEntry>();
    public BattleUnit Attacker {get;private set;}=null!;
    public BattleUnit Target {get;private set;}=null!;
    public CombatVisualChoice Choice {get;private set;}=new();
    public bool Busy {get;private set;}
    public bool Dirty {get;private set;}
    private readonly Dictionary<string,CombatVisualChoice> _drafts=new();
    private OptionButton _a=null!,_b=null!,_source=null!,_animation=null!,_effect=null!,_anchor=null!,_pose=null!;
    private SpinBox _x=null!,_y=null!,_size=null!,_zoom=null!;
    private Label _status=null!,_details=null!;
    private Control _stage=null!;
    private Node2D _arena=null!;
    private AnimatedSprite2D? _previewEffect;
    private double _repeatDelay;
    public string PreviewPose {get;private set;}="";
    private int _generation;
    private bool _updating,_exiting;
    private string _effectQuery="";
    private readonly List<string> _visibleEffects=new();
    public string[] DisplayAnimations {get;private set;}=Array.Empty<string>();
    private string[] _sourceAnimations=Array.Empty<string>();
    private string SourceAnimation=>_sourceAnimations[_source.Selected];
    public override void _Ready()
    {
        Characters=CharacterData.All.Where(c=>c.Enabled).GroupBy(c=>c.ScenePath).Select(g=>g.First()).OrderBy(c=>c.Name).ToArray();
        Effects=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray)??Array.Empty<EffectEntry>();
        BuildUi();SelectCharacters(0,Math.Min(1,Characters.Length-1));
    }
    private OptionButton Picker(VBoxContainer box,string label)
    {
        box.AddChild(DeckMenuUi.Text(label,14));var picker=new OptionButton {FitToLongestItem=false,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,CustomMinimumSize=new(300,32),SizeFlagsHorizontal=SizeFlags.ExpandFill};box.AddChild(picker);
        
        void SelectWithKeyboard(int index)
        {
            if(_updating||index<0||index>=picker.ItemCount||index==picker.Selected)return;
            picker.Select(index);picker.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);
        }
        picker.ItemFocused+=i=>SelectWithKeyboard((int)i);
        picker.GuiInput+=input=>{
            if(input is not InputEventKey {Pressed:true} key||picker.GetPopup().Visible)return;
            int direction=key.Keycode switch {Key.Down or Key.Right=>1,Key.Up or Key.Left=>-1,_=>0};
            if(direction==0)return;
            SelectWithKeyboard(Mathf.Clamp(picker.Selected+direction,0,picker.ItemCount-1));picker.AcceptEvent();
        };
        return picker;
    }
    private SpinBox Number(VBoxContainer box,string label,double min,double max,double step,double value)
    {
        var row=new HBoxContainer();box.AddChild(row);var title=DeckMenuUi.Text(label,13);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(title);
        var number=new SpinBox {MinValue=min,MaxValue=max,Step=step,Value=value,CustomMinimumSize=new(120,32)};row.AddChild(number);
        var edit=number.GetLineEdit();
        edit.GuiInput+=input=>{
            if(input is not InputEventKey {Pressed:true} key||key.Keycode is not (Key.Up or Key.Down))return;
            number.Value+=number.Step*(key.Keycode==Key.Up?1:-1);edit.AcceptEvent();
        };
        return number;
    }
    private void BuildUi()
    {
        var page=DeckMenuUi.Page(this);var header=new HBoxContainer();page.AddChild(header);
        header.AddChild(DeckMenuUi.Button(ReturnScene=="res://scenes/lobby.tscn"?"< LOBBY":"< BACK",()=>GetTree().ChangeSceneToFile(ReturnScene),110));
        var title=DeckMenuUi.Text("Combat Simulator",28);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        header.AddChild(DeckMenuUi.Button("Export settings",Export,140));
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",14);page.AddChild(body);
        var settings=DeckMenuUi.Panel(body,320);var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};settings.AddChild(scroll);
        var controls=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};controls.AddThemeConstantOverride("separation",10);scroll.AddChild(controls);
        _a=Picker(controls,"ตัวละคร A / ผู้โจมตี");_b=Picker(controls,"ตัวละคร B / เป้าหมายทดลอง");
        foreach(var c in Characters){string name=c.Name;_a.AddItem(name);_b.AddItem(name);}
        _a.ItemSelected+=i=>{if(!_updating)SelectCharacters((int)i,_b.Selected);};_b.ItemSelected+=i=>{if(!_updating)SelectCharacters(_a.Selected,(int)i);};
        _pose=Picker(controls,"ดูแอนิเมชันทีละท่า / Animation preview");_pose.Name="PosePreviewPicker";
        _pose.ItemSelected+=i=>SelectPreviewPose(i==0?"":_pose.GetItemText((int)i));
        _source=Picker(controls,"ท่าในเกมที่ต้องการปรับ");
        _source.ItemSelected+=i=>{if(!_updating)LoadChoice();};
        _animation=Picker(controls,"แอนิเมชันที่แสดง / ท่าที่ไม่ซ้ำ");
        _animation.ItemSelected+=i=>Changed();
        controls.AddChild(DeckMenuUi.Text("PARTICLE / EFFECT",14));
        var search=DeckMenuUi.Input("","ค้นหาเอฟเฟกต์ / fire, heal, impact...",300);controls.AddChild(search);search.TextChanged+=query=>{_effectQuery=query;RebuildEffects();};
        _effect=Picker(controls,"เอฟเฟกต์บน B");_effect.Name="TargetEffectPicker";
        
        _effect.ItemSelected+=i=>SelectEffect((int)i);
        _anchor=Picker(controls,"จุดยึดเอฟเฟกต์");foreach(string name in new[]{"อัตโนมัติ","กลางตัว B","เท้า B"})_anchor.AddItem(name);_anchor.ItemSelected+=i=>Changed();
        _x=Number(controls,"ตำแหน่ง X (ขวา +)",-200,200,1,0);_y=Number(controls,"ตำแหน่ง Y (ลง +)",-200,200,1,0);_size=Number(controls,"ขนาดเอฟเฟกต์ (เท่า)",.1,5,.05,1);
        _x.Name="EffectOffsetX";_y.Name="EffectOffsetY";_size.Name="EffectScale";
        foreach(var n in new[]{_x,_y,_size})n.ValueChanged+=v=>Changed();
        var center=DeckMenuUi.Panel(body);center.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _details=DeckMenuUi.Text("",15);_details.AutowrapMode=TextServer.AutowrapMode.WordSmart;center.AddChild(_details);
        _stage=new Control {SizeFlagsVertical=SizeFlags.ExpandFill,ClipContents=true,CustomMinimumSize=new(360,240),MouseFilter=MouseFilterEnum.Ignore};center.AddChild(_stage);
        var background=new ColorRect {Color=MenuUi.Background,MouseFilter=MouseFilterEnum.Ignore};_stage.AddChild(background);background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _arena=new Node2D {Name="PreviewArena"};_stage.AddChild(_arena);_stage.Resized+=LayoutStage;
        _zoom=Number(center,"ซูมภาพทดลอง",1,6,.25,4);_zoom.ValueChanged+=v=>LayoutStage();
        var buttons=new HBoxContainer();center.AddChild(buttons);
        var play=DeckMenuUi.Button("Play attack",()=>{_ = PlayAttack();},180,44);play.Name="PlayAttackButton";buttons.AddChild(play);

        buttons.AddChild(DeckMenuUi.Button("RESET",ResetChoice,100,44));
        _status=DeckMenuUi.Text("",13);_status.AutowrapMode=TextServer.AutowrapMode.WordSmart;center.AddChild(_status);
        RebuildEffects();
    }
    private string DraftKey()=>CombatVisualSettings.Slug(Attacker.CardKind)+":"+SourceAnimation+":"+CombatVisualSettings.Slug(Target.CardKind);
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
        PreviewPose="";_pose.Clear();_pose.AddItem("Combat / โจมตีเป้าหมาย");
        foreach(string pose in new[]{"idle","walk","attack01","attack02","attack03","hit","die"})
            if(Attacker.Sprite.SpriteFrames.HasAnimation(pose))_pose.AddItem(pose);
        _pose.Select(0);
        DisplayAnimations=UniqueCharacterAnimations.Names(Attacker.Sprite.SpriteFrames,new[]{"attack01","attack02","attack03","summon"});
        var rules=CharacterData.Get(Attacker.CardKind).Combat;
        _sourceAnimations=(rules?.Actions.Select(a=>a.Animation)??new[]{"attack01"})
            .Concat(Attacker.Sprite.SpriteFrames.HasAnimation("summon")?new[]{"summon"}:Array.Empty<string>())
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
    private void LoadChoice(bool autoSave=true)
    {
        string original=SourceAnimation;
        Choice=_drafts.TryGetValue(DraftKey(),out var draft)?draft.Copy():CombatVisualSettings.Find(Attacker.CardKind,original,Target.CardKind)??new() {
            Character=CombatVisualSettings.Slug(Attacker.CardKind),SourceAnimation=original,Animation=original,Effect=CharacterImpactEffects.DefaultEffect(Attacker.CardKind,original)};
        Choice.TargetCharacter=CombatVisualSettings.Slug(Target.CardKind);
        CancelAttackPreview();SyncControls();RefreshPreview();if(autoSave)SaveChoice();else UpdateStatus();
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
        _drafts[DraftKey()]=Choice.Copy();SaveChoice();
        CancelAttackPreview();RefreshPreview();
        _ = PlayAttack();
    }
    public void SetChoice(CombatVisualChoice choice)
    {
        if(!CombatVisualSettings.Valid(choice)||choice.Character!=CombatVisualSettings.Slug(Attacker.CardKind))throw new ArgumentException("Invalid preview choice");
        CancelAttackPreview();
        int sourceIndex=Array.IndexOf(_sourceAnimations,choice.SourceAnimation);
        if(sourceIndex<0)throw new ArgumentException("This character does not use the source attack");
        _source.Select(sourceIndex);Choice=choice.Copy();Choice.TargetCharacter=CombatVisualSettings.Slug(Target.CardKind);_drafts[DraftKey()]=Choice.Copy();CancelAttackPreview();SyncControls();RefreshPreview();SaveChoice();
    }
    private void UpdateStatus()
    {
        var saved=CombatVisualSettings.Find(Attacker.CardKind,Choice.SourceAnimation,Target.CardKind);
        var baseline=saved??new CombatVisualChoice {Character=Choice.Character,SourceAnimation=Choice.SourceAnimation,Animation=Choice.SourceAnimation,Effect=CharacterImpactEffects.DefaultEffect(Attacker.CardKind,Choice.SourceAnimation)};
        baseline.TargetCharacter=CombatVisualSettings.Slug(Target.CardKind);
        baseline.Animation=UniqueCharacterAnimations.Canonical(Attacker.Sprite.SpriteFrames,baseline.Animation,DisplayAnimations);
        Dirty=System.Text.Json.JsonSerializer.Serialize(baseline,GameJsonContext.Default.CombatVisualChoice)!=System.Text.Json.JsonSerializer.Serialize(Choice,GameJsonContext.Default.CombatVisualChoice);
        _status.Text=Dirty?"บันทึกอัตโนมัติไม่สำเร็จ":saved!=null?"บันทึกอัตโนมัติแล้ว • เกมใช้ค่านี้อยู่":"ค่าเริ่มต้น";
        var rules=CharacterData.Get(Attacker.CardKind).Combat;
        bool used=rules?.Actions.Any(a=>a.Animation==Choice.SourceAnimation)==true||rules?.First?.Animation==Choice.SourceAnimation||rules?.Finisher==Choice.SourceAnimation;
        _details.Text=$"A: {Characters[_a.Selected].Name}  →  B: {Characters[_b.Selected].Name}\n{Choice.SourceAnimation}  →  {Choice.Animation}"+(used?"":"\nท่าต้นทางนี้ยังไม่อยู่ในรอบโจมตีของตัวละคร เลือกท่าต้นทางที่ใช้อยู่เพื่อเห็นในเกม");
    }
    private void StopEffect(){if(GodotObject.IsInstanceValid(_previewEffect)){_previewEffect!.GetParent()?.RemoveChild(_previewEffect);_previewEffect.QueueFree();}_previewEffect=null;}
    private void RefreshPreview()
    {
        if(Busy||Attacker==null)return;StopEffect();Attacker.Sprite.Play(BattleAnimations.Idle);Target.Sprite.Play(BattleAnimations.Idle);
        _repeatDelay=0;
    }
    public void SelectPreviewPose(string animation)
    {
        if(animation!=""&&!Attacker.Sprite.SpriteFrames.HasAnimation(animation))return;
        _generation++;Busy=false;StopEffect();PreviewPose=animation;_repeatDelay=0;
        Attacker.Position=new(-48,0);Target.Position=new(48,0);
        Target.Sprite.Play(BattleAnimations.Idle);
        Attacker.Sprite.Play(animation==""?BattleAnimations.Idle:new StringName(animation));
        Attacker.Sprite.SetFrameAndProgress(0,0);
        for(int i=0;i<_pose.ItemCount;i++)if((i==0?"":_pose.GetItemText(i))==animation)_pose.Select(i);
    }
    public async Task PlayAttack()
    {
        if(Busy||_exiting)return;PreviewPose="";_pose.Select(0);Busy=true;int generation=++_generation;var choice=Choice.Copy();var actor=Attacker;var victim=Target;StopEffect();actor.ResetHealth();victim.ResetHealth();
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
            CharacterProjectiles.Begin(actor,victim,choice.SourceAnimation,choice.Animation,impacts[0]);
            foreach(int frame in impacts){
                while(actor.Sprite.IsPlaying()&&actor.Sprite.Frame<frame){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_exiting||generation!=_generation)return;}
                if(_exiting||generation!=_generation)return;
                victim.Sprite.Play(BattleAnimations.Hit);victim.Sprite.SetFrameAndProgress(0,0);CharacterProjectiles.Impact(actor,victim,choice.SourceAnimation,rule?.Mode??"damage");CharacterImpactEffects.PlayChoice(actor,victim,choice);
            }
            while(actor.Sprite.IsPlaying()){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(_exiting||generation!=_generation)return;}
            if(rule?.Mode=="melee"){
                actor.Sprite.Play(BattleAnimations.Walk);
                if(!await MovePreview(actor,home,generation))return;
            }
            await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);
            if(_exiting||generation!=_generation)return;actor.Sprite.Play(BattleAnimations.Idle);victim.Sprite.Play(BattleAnimations.Idle);
        }finally {if(!_exiting&&generation==_generation){Busy=false;RefreshPreview();_repeatDelay=.2;}}
    }
    public override void _Process(double delta)
    {
        if(_exiting||_updating||Busy||Attacker==null||!IsVisibleInTree())return;
        _repeatDelay-=delta;
        if(PreviewPose!=""){
            if(Attacker.Sprite.Animation!=PreviewPose){Attacker.Sprite.Play(PreviewPose);Attacker.Sprite.SetFrameAndProgress(0,0);}
            if(Attacker.Sprite.IsPlaying()){_repeatDelay=.6;return;}
            if(_repeatDelay<=0){Attacker.Sprite.Play(PreviewPose);Attacker.Sprite.SetFrameAndProgress(0,0);}
            return;
        }
        if(_repeatDelay<=0)_ = PlayAttack();
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
        if(CombatVisualSettings.Save(Choice,out string error)){_drafts.Remove(DraftKey());UpdateStatus();_status.Text="บันทึกอัตโนมัติแล้ว • ใช้ในเกมทันที และยังอยู่หลังเปิดเกมใหม่";return true;}
        _status.Text="เซฟไม่สำเร็จ: "+error;return false;
    }
    private void ResetChoice()
    {
        if(CombatVisualSettings.Reset(Attacker.CardKind,Choice.SourceAnimation,out string error,Target.CardKind)){_drafts.Remove(DraftKey());LoadChoice(false);_status.Text="กลับไปใช้ค่าเริ่มต้นแล้ว";}
        else _status.Text="รีเซ็ตไม่สำเร็จ: "+error;
    }
    private void Export()
    {
        var dialog=new FileDialog {FileMode=FileDialog.FileModeEnum.SaveFile,Access=FileDialog.AccessEnum.Filesystem,Title="Export saved combat visuals",CurrentFile="combat_visuals.json",UseNativeDialog=true};dialog.AddFilter("*.json","Combat visuals");AddChild(dialog);
        dialog.FileSelected+=path=>{try{CombatVisualSettings.AtomicWrite(path,CombatVisualSettings.Serialize(CombatVisualSettings.Combined()));_status.Text="ส่งออกค่าที่เซฟแล้ว: "+path;}catch(Exception e){_status.Text="ส่งออกไม่สำเร็จ: "+e.Message;}dialog.QueueFree();};dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered(new Vector2I(900,600));
    }
    public override void _ExitTree(){_exiting=true;_generation++;}
}
