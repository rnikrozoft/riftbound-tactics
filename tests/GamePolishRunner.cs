using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class GamePolishRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(int n=2){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var welcome=GD.Load<PackedScene>("res://scenes/welcome.tscn").Instantiate<Welcome>();AddChild(welcome);await Frames();
            var input=welcome.FindChild("ServerIpInput",true,false) as LineEdit;
            Check(input!=null&&input.Text==GameAccount.ServerHost,"Login must expose the active server address");
            string host=GameAccount.ServerHost;Check(!GameAccount.SetServerHost("http://127.0.0.1:7350")&&GameAccount.ServerHost==host,"Invalid host must not change the connection");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-login-ip.png");}
            welcome.QueueFree();await Frames();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames();
            Check(lobby.FindChild("DeleteDeckButton",true,false)!=null,"Deck deletion control missing");lobby.QueueFree();await Frames();
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=false;AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";shop.ApplyNetworkState(state,"effects-a");await Frames();
            Check(shop.NetworkUnits.Count>0&&shop.NetworkUnits.Values.All(u=>u.Sprite.IsPlaying()&&u.Sprite.Animation==BattleAnimations.Idle),"Preparation units must start idle immediately");
            var sprite=shop.NetworkUnits.Values.First().Sprite;int first=sprite.Frame;float progress=sprite.FrameProgress;await Frames(20);
            Check(sprite.Frame!=first||sprite.FrameProgress!=progress,"Idle must advance while preparation transition plays");
            var speed=field.FindChild("BattleSpeedButton",true,false) as Control;
            Check(speed!=null&&!speed.Visible,"Speed must be hidden before a match starts");battle.MatchEntered=true;await Frames();Check(speed!.Visible,"Speed must appear after entering the match");
            state.Phase="battle";shop.ApplyNetworkState(state,"effects-a");await Frames();
            var overlay=field.GetNode<CanvasLayer>("PhaseTransition").GetChild<ColorRect>(0);
            Check(overlay.Visible&&overlay.GetChild<Label>(0).Text=="BATTLE","Battle transition missing");
            shop.SetReplayPlan(state.Battle!);
            var firstEvent=state.Battle!.Events.First();
            var attacker=shop.NetworkUnits[firstEvent.Attacker];var target=shop.NetworkUnits[firstEvent.Target];
            int turns=battle.TurnCount;bool startedBehindOverlay=false;
            battle.TurnStarted+=(team,a,b)=>startedBehindOverlay|=overlay.Visible;
            var playback=battle.PlayServerEvent(attacker,target,firstEvent);
            ulong started=Time.GetTicksMsec();
            while(Time.GetTicksMsec()-started<800)await Frames();
            Check(overlay.Visible&&Mathf.IsEqualApprox(overlay.Modulate.A,1),"Transition must hold for one second before fading out");
            Check(battle.TurnCount==turns&&!playback.IsCompleted,"Combat must wait while the transition is visible");
            ulong until=started+2200;while(overlay.Visible&&Time.GetTicksMsec()<until)await Frames();Check(!overlay.Visible,"Transition must clear after its one-second hold");
            await playback;Check(!startedBehindOverlay&&battle.TurnCount==turns+1,"First attack must start only after the fade is fully gone");
            field.QueueFree();await Frames();
            var background=new ColorRect {Color=new Color(.08f,.12f,.16f)};AddChild(background);background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var modal=new GameModal();AddChild(modal);modal.Body.AddChild(DeckMenuUi.Text("DEFEAT",32));
            var button=DeckMenuUi.Button("BACK TO LOBBY",()=>{},220);modal.Body.AddChild(button);await Frames();
            var fade=modal.FadeIn(.5);fade.Pause();await Frames();
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var image=GetViewport().GetTexture().GetImage();var point=button.GetGlobalRect().GetCenter()+new Vector2(-70,0);
                var pixel=image.GetPixel((int)point.X,(int)point.Y);
                Check(Mathf.Abs(pixel.R-background.Color.R)<.025f&&Mathf.Abs(pixel.G-background.Color.G)<.025f,"Button shader must respect the popup's zero opacity");
            }
            fade.Play();await ToSignal(fade,Tween.SignalName.Finished);await Frames();
            Check(modal.GetChildren().OfType<Control>().All(c=>Mathf.IsEqualApprox(c.Modulate.A,1)),"Popup panel and buttons must complete the same fade");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-popup-fade.png");}
            GD.Print($"GAME POLISH PASS: {_checks} checks");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
