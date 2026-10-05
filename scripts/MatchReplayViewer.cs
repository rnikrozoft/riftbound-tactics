using Godot;
using System;
using System.Threading.Tasks;

public partial class MatchReplayViewer : Node
{
    private BattleDemo _battle=null!;
    private CardShop _shop=null!;
    private Label _label=null!;
    private bool _closed,_playing;
    public override void _Ready()
    {
        var arena=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate();
        var online=arena.GetNode<OnlineBattle>("OnlineBattle");arena.RemoveChild(online);online.Free();
        _battle=arena.GetNode<BattleDemo>("BattleDemo");_battle.NetworkEnabled=true;
        AddChild(arena);_shop=arena.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        arena.GetNode<Control>("UI/SafeArea/Content/EffectsLabButton").Hide();
        var layer=new CanvasLayer {Layer=32};AddChild(layer);
        var controls=new HBoxContainer {Position=new(16,16)};layer.AddChild(controls);
        controls.AddChild(DeckMenuUi.Button("BACK TO LOBBY",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),180));
        controls.AddChild(DeckMenuUi.Button("PLAY AGAIN",()=>Play(),150));
        _label=DeckMenuUi.Text("REPLAY",18);controls.AddChild(_label);
        Callable.From(Play).CallDeferred();
    }
    private async Task Wait(long until)
    {
        while(!_closed && (long)Time.GetTicksMsec()<until)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(_closed)throw new OperationCanceledException();
    }
    private async void Play()
    {
        if(_playing || _closed)return;_playing=true;
        try {
            foreach(var state in MatchReplayStore.Rounds) {
                _label.Text=$"REPLAY / ROUND {state.Round}";_battle.HideResult();
                state.Phase="preparation";_shop.ApplyNetworkState(state,MatchReplayStore.UserId);
                state.Phase="battle";_shop.ApplyNetworkState(state,MatchReplayStore.UserId);_shop.LockNetworkInteraction();
                var plan=state.Battle!;long start=(long)Time.GetTicksMsec();
                foreach(var combat in plan.Events) {
                    await Wait(start+Math.Max(0,combat.AtMs-plan.StartMs));
                    if(_shop.NetworkUnits.TryGetValue(combat.Attacker,out var attacker) && _shop.NetworkUnits.TryGetValue(combat.Target,out var target))
                        await _battle.PlayServerEvent(attacker,target,combat);
                    if(_closed)throw new OperationCanceledException();
                }
                _battle.ShowServerResult(plan.Winner);await Wait((long)Time.GetTicksMsec()+1200);
            }
            _label.Text="REPLAY COMPLETE";
        } catch(OperationCanceledException) { }
        catch(Exception e){if(!_closed){_label.Text="REPLAY UNAVAILABLE";GD.PushError(e.ToString());}}
        finally{_playing=false;}
    }
    public override void _ExitTree()=>_closed=true;
}
