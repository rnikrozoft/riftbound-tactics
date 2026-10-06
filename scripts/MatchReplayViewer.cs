using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class MatchReplayViewer : Node
{
    private Node? _arena;
    private BattleDemo _battle=null!;
    private CardShop _shop=null!;
    private Label _label=null!;
    private ReplayTimeline _timeline=null!;
    private PanelContainer _drawer=null!;
    private TextureButton _drawerToggle=null!;
    private Tween? _drawerTween;
    private TextureButton _playPause=null!;
    private bool _playing,_paused;
    private double _clock;
    public bool IsPaused=>_paused;
    public bool IsPlaying=>_playing;
    public long PlaybackClockMs=>(long)(_clock*1000);
    public bool DrawerCollapsed {get;private set;}
    public Control ReplayDrawer=>_drawer;
    private int _selectedRound,_selectedStep;
    private bool _preparation;
    private int[] _rounds=Array.Empty<int>();
    private bool _closed;
    private int _generation;
    public string PlaybackStatus=>_label?.Text??"";
    public int SelectedRound=>_selectedRound;
    public bool ShowingPreparation=>_preparation;
    public int SelectedStep=>_selectedStep;
    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        _rounds=MatchReplayStore.Rounds.Concat(MatchReplayStore.Preparations).Select(s=>s.Round).Distinct().OrderBy(r=>r).ToArray();
        var layer=new CanvasLayer {Layer=32};AddChild(layer);
        var root=new PanelContainer {Name="ReplayDrawer",AnchorRight=1,AnchorTop=1,AnchorBottom=1,OffsetLeft=16,OffsetRight=-16,OffsetTop=-140,OffsetBottom=-12};_drawer=root;root.AddThemeStyleboxOverride("panel",GameUi.Box(false,8));layer.AddChild(root);
        _drawerToggle=DeckMenuUi.Button("↓",ToggleDrawer,48,30);_drawerToggle.TooltipText="Hide timeline";_drawerToggle.Name="TimelineToggle";layer.AddChild(_drawerToggle);
        _drawerToggle.AnchorLeft=_drawerToggle.AnchorRight=.5f;_drawerToggle.AnchorTop=_drawerToggle.AnchorBottom=1;
        _drawerToggle.OffsetLeft=-24;_drawerToggle.OffsetRight=24;_drawerToggle.OffsetTop=-174;_drawerToggle.OffsetBottom=-144;
        var box=new VBoxContainer();root.AddChild(box);
        var controls=new HBoxContainer();controls.AddThemeConstantOverride("separation",8);box.AddChild(controls);
        _playPause=DeckMenuUi.Button("PLAY",TogglePlayback,90);_playPause.Name="PlayPause";_playPause.TooltipText="Play / Pause • Esc: back to replays";controls.AddChild(_playPause);
        _label=DeckMenuUi.Text("REPLAY",12);_label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;controls.AddChild(_label);
        var scroll=new ScrollContainer {CustomMinimumSize=new(0,70),HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled};box.AddChild(scroll);
        _timeline=new ReplayTimeline {Name="ReplayTimeline"};scroll.AddChild(_timeline);_timeline.Build(_rounds);
        _timeline.PointSelected+=(round,preparation,step)=>{SelectSection(round,preparation,step);Start();};
        _selectedRound=_rounds.FirstOrDefault();_preparation=!MatchReplayStore.Rounds.Any(s=>s.Round==_selectedRound);
        ResetArena();SelectionChanged();Callable.From(Start).CallDeferred();
    }
    public override void _Process(double delta){if(!_paused)_clock+=delta;}
    private OnlineState[] PreparationSteps()=>MatchReplayStore.Preparations.Where(s=>s.Round==SelectedRound).ToArray();
    private void ResetArena()
    {
        if(_arena!=null){RemoveChild(_arena);_arena.QueueFree();}
        _arena=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate();
        _arena.ProcessMode=ProcessModeEnum.Pausable;
        var online=_arena.GetNode<OnlineBattle>("OnlineBattle");_arena.RemoveChild(online);online.Free();
        _battle=_arena.GetNode<BattleDemo>("BattleDemo");_battle.NetworkEnabled=true;
        _battle.SpeedEnabled=_playing;
        AddChild(_arena);_shop=_arena.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        var back=DeckMenuUi.Button("BACK TO LOBBY",ReturnToLobby,150,32);
        back.Name="ReplayLobbyButton";back.Position=new(16,16);back.ProcessMode=ProcessModeEnum.Always;
        _arena.GetNode<Control>("UI/SafeArea/Content").AddChild(back);
    }
    public void ToggleDrawer()
    {
        DrawerCollapsed=!DrawerCollapsed;_drawerTween?.Kill();_drawer.Show();
        _drawerToggle.GetNode<Label>("Text").Text=DrawerCollapsed?"↑":"↓";
        _drawerToggle.TooltipText=DrawerCollapsed?"Show timeline":"Hide timeline";
        _drawerTween=CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _drawerTween.TweenProperty(_drawer,"offset_top",DrawerCollapsed?12f:-140f,.25);
        _drawerTween.TweenProperty(_drawer,"offset_bottom",DrawerCollapsed?140f:-12f,.25);
        _drawerTween.TweenProperty(_drawerToggle,"offset_top",DrawerCollapsed?-42f:-174f,.25);
        _drawerTween.TweenProperty(_drawerToggle,"offset_bottom",DrawerCollapsed?-12f:-144f,.25);
        _drawerTween.Finished+=()=>{if(DrawerCollapsed)_drawer.Hide();};
    }
    public void SelectSection(int round,bool preparation,int step=0)
    {
        int index=Array.IndexOf(_rounds,round);if(index<0)return;
        if(preparation&&!MatchReplayStore.Preparations.Any(s=>s.Round==round))preparation=false;
        _selectedRound=round;_preparation=preparation;_selectedStep=Math.Max(0,step);SelectionChanged();
    }
    private void SelectionChanged()
    {
        _generation++;_playing=false;_paused=false;GetTree().Paused=false;UpdatePlayButton();_timeline.Select(SelectedRound,ShowingPreparation);
        ResetArena();
        if(ShowingPreparation)ShowPreparation(SelectedStep);
        else {
            var saved=MatchReplayStore.Rounds.FirstOrDefault(s=>s.Round==SelectedRound);
            if(saved==null){_label.Text="Battle not recorded for this round";return;}
            SetBattle(saved);_label.Text=$"ROUND {SelectedRound} / BATTLE";
        }
    }
    private void ShowPreparation(int index)
    {
        var steps=PreparationSteps();
        if(steps.Length==0)return;
        index=Math.Clamp(index,0,steps.Length-1);_selectedStep=index;_timeline.Select(SelectedRound,true);
        var state=MatchReplayStore.Clone(steps[index]);state.Phase="preparation";
        _battle.HideResult();_shop.NetworkPending=true;_shop.ApplyNetworkState(state,MatchReplayStore.UserId);
        UpdateProfiles(state);
        long seconds=Math.Max(0,(steps[index].ServerMs-steps[0].ServerMs)/1000);
        _label.Text=$"ROUND {SelectedRound} / PREPARATION / STEP {index+1} OF {steps.Length} / {seconds/60}:{seconds%60:00}";
    }
    private void SetBattle(OnlineState saved)
    {
        var state=MatchReplayStore.Clone(saved);state.Phase="preparation";_shop.ApplyNetworkState(state,MatchReplayStore.UserId);
        state.Phase="battle";_shop.ApplyNetworkState(state,MatchReplayStore.UserId);_shop.LockNetworkInteraction();_shop.SetReplayPlan(state.Battle!);_battle.HideResult();
        UpdateProfiles(state);
    }
    private void UpdateProfiles(OnlineState state)
    {
        foreach(var player in state.Players)if(player!=null){
            bool own=player.UserId==MatchReplayStore.UserId;
            var profile=_arena!.GetNode<PlayerProfile>("UI/SafeArea/Content/"+(own?"ProfileA":"ProfileB"));profile.SetCoins(player.Coins);profile.SetHealth(player.Hp);
        }
    }
    private void Check(int token){if(_closed||token!=_generation)throw new OperationCanceledException();}
    private async Task Wait(long until,int token)
    {
        while(_paused||PlaybackClockMs<until){Check(token);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}Check(token);
    }
    private async Task PlayPreparation(int token,int first=0)
    {
        var steps=PreparationSteps();
        for(int i=first;i<steps.Length;i++){
            Check(token);ShowPreparation(i);
            if(i+1<steps.Length)await Wait(PlaybackClockMs+Math.Clamp(steps[i+1].ServerMs-steps[i].ServerMs,250,5000),token);
        }
    }
    private async Task PlayBattle(OnlineState saved,int token)
    {
        ResetArena();SetBattle(saved);var battle=_battle;var shop=_shop;var plan=saved.Battle!;
        await battle.GetParent<BattleDisplay>().WaitForPhaseTransition();Check(token);
        battle.BeginCombatPresentation(shop.ReplayPlan!);
        _label.Text=$"ROUND {saved.Round} / BATTLE";long start=PlaybackClockMs;
        foreach(var combat in plan.Events){
            await Wait(start+Math.Max(0,combat.AtMs-plan.StartMs),token);
            if(shop.NetworkUnits.TryGetValue(combat.Attacker,out var attacker)){
                var target=shop.NetworkUnits.TryGetValue(combat.Target,out var existing)?existing:attacker;
                await battle.PlayServerEvent(attacker,target,combat);
            }
            Check(token);
        }
        foreach(var combat in plan.Events)shop.ApplyCombatEvent(combat);
        battle.EndCombatPresentation();battle.ShowServerResult(plan.Winner);await Wait(PlaybackClockMs+1200,token);
    }
    private void UpdatePlayButton()=>_playPause.GetNode<Label>("Text").Text=_playing&&!_paused?"PAUSE":"PLAY";
    public void TogglePlayback()
    {
        if(!_playing){Start();return;}
        _paused=!_paused;GetTree().Paused=_paused;UpdatePlayButton();
    }
    private async void Start()
    {
        if(_closed||_rounds.Length==0)return;int token=++_generation;_playing=true;_paused=false;GetTree().Paused=false;UpdatePlayButton();
        _battle.SpeedEnabled=true;
        int firstRound=SelectedRound;bool firstPreparation=ShowingPreparation;int firstStep=SelectedStep;
        try {
                foreach(int round in _rounds.Where(r=>r>=firstRound)){
                    Check(token);_selectedRound=round;_preparation=true;_selectedStep=0;
                    if(round!=firstRound||firstPreparation){ResetArena();await PlayPreparation(token,round==firstRound?firstStep:0);}
                    var saved=MatchReplayStore.Rounds.FirstOrDefault(s=>s.Round==round);
                    if(saved!=null){_preparation=false;_selectedStep=0;_timeline.Select(round,false);await PlayBattle(saved,token);}
                }
            Check(token);_label.Text="REPLAY COMPLETE";
        }catch(OperationCanceledException){}
        catch(Exception e){if(!_closed&&token==_generation){_label.Text="REPLAY UNAVAILABLE";GD.PushError(e.ToString());}}
        finally{if(token==_generation){_playing=false;_paused=false;_battle.SpeedEnabled=false;GetTree().Paused=false;UpdatePlayButton();}}
    }
    public void PlaySelected()=>Start();
    private void ReturnToLobby()
    {
        _generation++;_playing=false;_paused=false;GetTree().Paused=false;
        Lobby.InitialMenu="Play";GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(input is InputEventKey {Pressed:true,Keycode:Key.Escape}){GetTree().Paused=false;Lobby.InitialMenu=MatchReplayStore.FromHistory?"Replays":"Play";GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree(){_closed=true;_generation++;GetTree().Paused=false;}
}
