using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class UndeadHollowRunner : Node
{
    private const string Pack="res://assets/Free-Undead-Tileset-Top-Down-Pixel-Art/";
    private async Task Frames(int count=4){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://output/"+name+".png");
    }
    private void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private void Inspect(Node node)
    {
        if(node is Sprite2D sprite && sprite.Texture!=null) {
            var texture=sprite.Texture is AtlasTexture atlas?atlas.Atlas:sprite.Texture;
            Check(texture.ResourcePath.StartsWith(Pack),"Environment uses another asset pack: "+texture.ResourcePath);
            Check(sprite.Modulate==Colors.White&&sprite.SelfModulate==Colors.White&&sprite.Material==null,"Environment applies a custom style");
            Check(sprite.GetParent().Name!="RelicsAndDeadGrove" || sprite.Position.X<220 || sprite.Position.X>590 || sprite.Position.Y<288 || sprite.Position.Y>535,"Large prop obstructs central combat area");
        }
        if(node is TileMapLayer layer)
            for(int i=0;i<layer.TileSet.GetSourceCount();i++)
                if(layer.TileSet.GetSource(layer.TileSet.GetSourceId(i)) is TileSetAtlasSource atlas)
                    Check(atlas.Texture.ResourcePath.StartsWith(Pack),"Terrain uses another pack");
        foreach(var child in node.GetChildren())Inspect(child);
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        GamePreferences.Load();string oldArea=GamePreferences.BattleArea;
        try {
            var scenery=GD.Load<PackedScene>(BattleAreas.UndeadScenery).Instantiate<Node2D>();Inspect(scenery);
            AddChild(scenery);var camera=new Camera2D {Position=new(408,366),Zoom=Vector2.One*1.1f};scenery.AddChild(camera);
            await Frames();
            var animated=(UndeadTileAnimation)scenery;Check(animated.AnimatedCellCount>0,"Native animated tiles were not registered");
            var animatedLayers=scenery.FindChildren("*","TileMapLayer",true,false).OfType<TileMapLayer>().ToArray();
            var before=animatedLayers.SelectMany(l=>l.GetUsedCells().Where(c=>l.GetCellSourceId(c) is 1 or 2 or 3 or 5 or 6 or 7).Select(c=>(Layer:l,Cell:c,Atlas:l.GetCellAtlasCoords(c)))).ToArray();
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            Check(before.Any(c=>c.Layer.GetCellAtlasCoords(c.Cell)!=c.Atlas),"Native animation frames do not advance");
            Check(scenery.GetNode<Node2D>("ReferenceMap/EasternRidge").Position.Y==48,"Skull ridge was not lowered");
            Check(scenery.GetNode("SceneryProps").GetChildCount()>=10,"Peripheral decorations missing");
            await Capture("undead-hollow-scenery");
            foreach(var size in new[]{new Vector2I(1600,720),new Vector2I(1280,900)}) {
                GetWindow().Size=size;await Frames(6);var ground=scenery.GetNode<TileMapLayer>("Ground");
                var extent=GetViewport().GetVisibleRect().Size;var inverse=ground.GetGlobalTransformWithCanvas().AffineInverse();
                foreach(var corner in new[]{Vector2.Zero,new Vector2(extent.X,0),extent,new Vector2(0,extent.Y)})
                    Check(ground.GetCellSourceId(ground.LocalToMap(inverse*corner))>=0,"Viewport edge exposes empty ground");
            }
            GetWindow().Size=new Vector2I(1280,720);scenery.Free();await Frames();
            GamePreferences.SetBattleArea("undead_hollow");Check(BattleAreas.ScenePath=="res://scenes/undead_battle.tscn","Selection does not route to new scene");
            var original=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            var field=GD.Load<PackedScene>(BattleAreas.ScenePath).Instantiate<BattleDisplay>();
            foreach(var cell in CardShop.DeploymentCells.Concat(CardShop.EnemyCells).Concat(BattleDisplay.SupportCells).Concat(BattleDisplay.EnemySupportCells))
                Check(field.GetNode<TileMapLayer>("TileMapLayer").MapToLocal(cell)==original.GetNode<TileMapLayer>("TileMapLayer").MapToLocal(cell),"Combat slot projection changed");
            foreach(var layer in field.GetNode("UndeadHollow/ReferenceMap").GetChildren().OfType<TileMapLayer>().Where(l=>l.Name.ToString().StartsWith("Objects")))
                foreach(var cell in CardShop.DeploymentCells.Concat(CardShop.EnemyCells).Concat(BattleDisplay.SupportCells).Concat(BattleDisplay.EnemySupportCells)) {
                    var local=field.GetNode<TileMapLayer>("TileMapLayer").MapToLocal(cell);
                    var reference=(local-new Vector2(-168,-18))/2;
                    Check(layer.GetCellSourceId(layer.LocalToMap(reference))<0,"Source-map object overlaps a combat slot: "+layer.Name+" "+cell+" -> "+layer.LocalToMap(reference)+" local "+local);
                }
            original.Free();foreach(var unit in field.GetChildren().OfType<BattleUnit>().ToArray())unit.Free();field.GetNode("OnlineBattle").Free();field.GetNode<BattleDemo>("BattleDemo").AutoStart=false;AddChild(field);await Frames();
            foreach(var pair in new[]{("HeroSlots",CardShop.DeploymentCells),("EnemyHeroSlots",CardShop.EnemyCells),("SupportSlots",BattleDisplay.SupportCells),("EnemySupportSlots",BattleDisplay.EnemySupportCells)}) {
                var markers=field.GetNode<Node2D>(pair.Item1);Check(markers.GetChildCount()==pair.Item2.Length,"Missing deployment slots");
                for(int i=0;i<pair.Item2.Length;i++)Check(markers.GetChild<Node2D>(i).Position.IsEqualApprox(field.GetNode<TileMapLayer>("TileMapLayer").MapToLocal(pair.Item2[i])),"Slot marker misaligned with unit grid");
            }
            if(!MobileLayout.Enabled) {
                Check(field.Scale.IsEqualApprox(new Vector2(1.4f,1.4f)),"Undead character/camera scale differs from original map");
                Check(field.Position.IsEqualApprox(new Vector2(GetViewport().GetVisibleRect().Size.X*.5f-408*1.4f,-206.2f)),"Undead battlefield is shifted from original map");
            } else Check(field.Scale.IsEqualApprox(Vector2.One*1.5f),"Mobile battlefield scale differs from original map");
            Check(!field.DecorateArena&&field.GetNodeOrNull("CloudShadows")==null&&field.GetNodeOrNull("HeroSlots")!=null,"Old arena decoration present");
            Check(!field.GetNode<TileMapLayer>("TileMapLayer").Visible&&field.GetNodeOrNull("Wildlife")==null,"Original terrain or wildlife present");
            var state=System.Text.Json.JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ApplyNetworkState(state,state.Players[0]!.UserId);
            await Frames(10);await field.WaitForPhaseTransition();await Frames();await Capture("undead-hollow-gameplay");
            for(int i=0;i<state.Players.Length;i++)if(state.Players[i]!=null)state.Players[i]!.Units=i==0?state.Players[i]!.Units.Take(1).ToArray():Array.Empty<OnlineUnit>();
            state.Revision++;field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ApplyNetworkState(state,state.Players[0]!.UserId);await Frames();await Capture("undead-hollow-layout");
            field.Free();await Frames();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames();
            var picker=lobby.FindChild("BattleAreaSelector",true,false) as OptionButton;
            Check(picker!=null&&picker.ItemCount==2&&picker.Selected==1,"Area menu missing or selection lost");
            picker!.EmitSignal(OptionButton.SignalName.ItemSelected,0);Check(GamePreferences.BattleArea=="living_garden"&&BattleAreas.ScenePath=="res://scenes/main.tscn","Original area cannot be selected");
            picker.EmitSignal(OptionButton.SignalName.ItemSelected,1);Check(GamePreferences.BattleArea=="undead_hollow","New area cannot be selected");
            await Frames();await Capture("undead-hollow-area-menu");
            lobby.GetNodeOrNull<BattleAreaPreviewModal>("BattleAreaPreviewModal")?.Free();
            lobby.AddChild(new BattleAreaPreviewModal {Name="BattleAreaPreviewModal"});await Frames(12);await Capture("undead-hollow-menu-preview");lobby.Free();
            GD.Print("UNDEAD HOLLOW PASS: exclusive source pack, open centre, unchanged combat coordinates, original/new selection, preview and gameplay scene");
            GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
        finally{GamePreferences.SetBattleArea(oldArea);}
    }
}



