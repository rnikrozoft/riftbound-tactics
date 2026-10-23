using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
public partial class HealingTotemRunner:Node
{
    private async Task Frames(int count=5){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready(){try{
        int kind=CharacterData.SupportKind;if(kind<0||!CharacterData.IsSupport(kind)||DeckDefinition.Starter().MandatoryNeutralKind!=kind)throw new Exception("Mandatory neutral missing");
        var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();
        var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);
        var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
        var state=JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
        state.Phase="preparation";state.Players[0]!.UserId="self";
        state.Players[0]!.Offers=new[]{new OnlineCard {Kind=kind,Token=700,Stars=1,Price=2}};
        state.Players[0]!.Units=state.Players[0]!.Units.Concat(new[]{new OnlineUnit {Kind=kind,Token=701,Stars=1,Slot=6}}).ToArray();
        state.Players[1]!.Units=state.Players[1]!.Units.Concat(new[]{new OnlineUnit {Kind=kind,Token=702,Stars=1,Slot=6}}).ToArray();
        shop.ApplyNetworkState(state,"self");await Frames();
        var totem=shop.NetworkUnits[state.Players[0]!.Team+":701"];
        var cell=CardShop.CellFor(6);var tiles=field.GetNode<TileMapLayer>("TileMapLayer");
        if(totem.Position.DistanceTo(tiles.Position+tiles.MapToLocal(cell))>1)throw new Exception("Wrong support cell position");
        if(totem.Sprite.FlipH!=totem.IsAlly||totem.Sprite.Material is not ShaderMaterial outline||outline.GetShaderParameter("outline_color").AsColor()!=Colors.White||outline.GetShaderParameter("outline_width").AsSingle()<=1)throw new Exception("Totem orientation or white outline missing");
        var idleSize=CharacterVisual.AnimationSize(totem.Sprite.SpriteFrames,BattleAnimations.Idle);
        float baseScale=Mathf.Min(CharacterVisual.FieldHeight/idleSize.Y,CharacterVisual.FieldWidth/idleSize.X);
        if(Mathf.Abs(totem.Sprite.Scale.X/baseScale-1.65f)>.01f)throw new Exception("Totem battlefield scale incorrect");
        var enemy=shop.NetworkUnits[state.Players[1]!.Team+":702"];
        if(enemy.Sprite.FlipH||enemy.Sprite.SpriteFrames.GetFrameCount("idle")!=8||enemy.Sprite.SpriteFrames.GetFrameTexture("idle",0) is not AtlasTexture enemyFrame||!enemyFrame.Atlas.ResourcePath.Contains("enemy_heal_sheet"))throw new Exception("Enemy must use northwest facing sheet without a horizontal flip");
        int sceneryTop=new[]{"SanctuaryPillars","SanctuaryPillarCaps","SanctuaryBraziers"}.Max(name=>field.GetNode<TileMapLayer>(name).ZIndex);
        if(totem.ZIndex<=sceneryTop||enemy.ZIndex<=sceneryTop)throw new Exception("Totem is hidden behind arena pillars");
        var drag=new CardDrag {Shop=shop,Unit=totem,Token=701};
        if(!shop.CanPlace(drag,tiles.ToGlobal(tiles.MapToLocal(CardShop.CellFor(7))))||shop.CanPlace(drag,tiles.ToGlobal(tiles.MapToLocal(CardShop.CellFor(0)))))throw new Exception("Support drag placement validation failed");
        if(totem.Sprite.SpriteFrames.GetFrameCount("idle")!=8||!totem.Sprite.IsPlaying())throw new Exception("Healing loop not playing");
        int first=totem.Sprite.Frame;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);if(totem.Sprite.Frame==first)throw new Exception("Healing loop is static");
        var plan=new LocalCombat(19).Simulate(new[]{new OnlineCombatUnit {Id="A:hero",Team="A",Kind=460,Stars=1,Slot=3},new OnlineCombatUnit {Id="B:hero",Team="B",Kind=460,Stars=1,Slot=3},new OnlineCombatUnit {Id="A:totem",Team="A",Kind=kind,Stars=1,Slot=8}});
        if(plan.Events.Any(e=>e.Attacker=="A:totem"||e.Target=="A:totem")||!plan.Events.SelectMany(e=>e.Hits).Any(h=>h.Kind=="heal"&&h.Source=="A:totem"))throw new Exception("Offline support healing or target rules failed");
        shop.ShowCardDetails(700);await Frames();
        if(!shop.DetailsText.Text.Contains("1 แต้ม"))throw new Exception("Totem ability detail missing");shop.CloseDetails();
        if(DisplayServer.GetName()!="headless"){await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-healing-totem.png");}
        GD.Print("HEALING TOTEM PASS: mandatory neutral, support placement, 8-frame loop, offline healing, no targeting, ability details");GetTree().Quit();
    }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
