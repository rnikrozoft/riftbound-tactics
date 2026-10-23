using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class MobileLayoutRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(){for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name){if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/rift-mobile-"+name+".png");}
    private bool Fits(Control c)=>new Rect2(Vector2.Zero,GetViewport().GetVisibleRect().Size).Encloses(c.GetGlobalRect());
    public override async void _Ready()
    {
        string originalLanguage=GamePreferences.Language;
        if(OS.GetEnvironment("RIFT_TEST_LANGUAGE")=="th")GamePreferences.SetLanguage("th");
        try {
            await Frames();Check(GetViewport().GlobalCanvasTransform==Transform2D.Identity,"No global zoom or pan");
            Check(GetWindow().ContentScaleSize==new Vector2I(960,540),"Mobile logical viewport");
            var welcome=GD.Load<PackedScene>("res://scenes/welcome.tscn").Instantiate<Welcome>();AddChild(welcome);await Frames();
            var input=welcome.FindChild("ServerIpInput",true,false) as LineEdit;Check(input!=null&&Fits(input),"Server input fits");
            var login=FindButton(welcome,"Continue as guest");Check(Fits(login)&&login.Size.Y>=60,"Guest login fits and has large touch area");await Shot("login");welcome.Free();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames();
            var find=FindButton(lobby,"Find match");GD.Print("FIND MATCH "+find.GetGlobalRect()+" VIEW "+GetViewport().GetVisibleRect());await Shot("lobby");Check(Fits(find),"Find match visible without canvas panning");lobby.Navigate("Shop");await Frames();Check(Fits(lobby.FindChild("BuyCharacterGroupButton",true,false) as Control??throw new Exception("Missing group purchase")),"Shop purchase fits");await Shot("shop");lobby.Free();
            var deck=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(deck);await Frames();Check(Fits(FindButton(deck,"Save deck")),"Deck save fits");await Shot("deck");deck.Free();
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();field.GetNode<BattleDemo>("BattleDemo").AutoStart=false;field.GetNode<BattleDemo>("BattleDemo").NetworkEnabled=true;AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=System.Text.Json.JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";shop.ApplyNetworkState(state,"effects-a");await Frames();
            Check(Fits(shop.ZoneA),"Arena shop fits");
            foreach(var actionName in new[]{"Upgrade","Reroll","Lock"}){var action=shop.ZoneA.GetNode<Control>(actionName);Check(Fits(action)&&action.Size.X>=60&&action.Size.Y>=60,"Shop action fits with large touch area: "+actionName);}Check(Fits(shop.HandRow),"Hand fits");
            var tiles=field.GetNode<TileMapLayer>("TileMapLayer");var screen=new Rect2(Vector2.Zero,GetViewport().GetVisibleRect().Size);
            foreach(var cell in CardShop.DeploymentCells.Concat(CardShop.EnemyCells))Check(screen.HasPoint(tiles.ToGlobal(tiles.MapToLocal(cell))),"Deployment slot visible");
            Check(Fits(field.GetNode<Control>("UI/SafeArea/Content/ProfileA"))&&Fits(field.GetNode<Control>("UI/SafeArea/Content/ProfileB")),"Compact profiles fit");await field.WaitForPhaseTransition();await Frames();await Shot("battle");
            GD.Print($"PASS mobile layout: {_checks} checks");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
        finally{GamePreferences.SetLanguage(originalLanguage);}
    }
    private static TextureButton FindButton(Node n,string text)
    {
        if(n is TextureButton b&&b.GetNodeOrNull<Label>("Text")?.Text==text)return b;
        foreach(var child in n.GetChildren()){var found=FindButtonOrNull(child,text);if(found!=null)return found;}
        throw new Exception("Button missing: "+text);
    }
    private static TextureButton? FindButtonOrNull(Node n,string text)
    {
        if(n is TextureButton b&&b.GetNodeOrNull<Label>("Text")?.Text==text)return b;
        foreach(var child in n.GetChildren()){var found=FindButtonOrNull(child,text);if(found!=null)return found;}return null;
    }
}
