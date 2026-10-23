using Godot;
using System;
using System.Linq;

public partial class ArenaWildlifeRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            var source=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            if(source.Position.DistanceTo(new Vector2(46.8f,-222.2f))>.001f || source.Scale!=new Vector2(1.4f,1.4f)) throw new Exception("Gameplay framing changed");
            var board=source.GetNode<TileMapLayer>("TileMapLayer");
            if(board.Position!=Vector2.Zero || board.GetUsedCells().Count!=351) throw new Exception("Battlefield shape or height changed");
            var cells=board.GetUsedCells();
            if(cells.Any(c=>c.X<38||c.X>64||c.Y<12||c.Y>24)) throw new Exception("Board margins not symmetric");
            foreach(var slot in BattleDisplay.SupportCells.Concat(BattleDisplay.EnemySupportCells)) if(board.GetCellSourceId(slot)<0) throw new Exception("Special slot outside compact board");
            if(BattleDisplay.SupportCells.Min(c=>c.X)-cells.Min(c=>c.X)!=3 || cells.Max(c=>c.X)-BattleDisplay.EnemySupportCells.Max(c=>c.X)!=3) throw new Exception("Compact board side margins differ");
            foreach(var cell in cells) if(board.GetCellSourceId(new Vector2I(102-cell.X,36-cell.Y))<0) throw new Exception("Board lacks rotational symmetry");
            var expectedAllies=new[]{new Vector2I(44,14),new Vector2I(44,18),new Vector2I(44,22),new Vector2I(48,14),new Vector2I(48,18),new Vector2I(48,22)};
            var expectedEnemies=new[]{new Vector2I(54,14),new Vector2I(54,18),new Vector2I(54,22),new Vector2I(58,14),new Vector2I(58,18),new Vector2I(58,22)};
            if(!CardShop.DeploymentCells.SequenceEqual(expectedAllies) || !CardShop.EnemyCells.SequenceEqual(expectedEnemies)) throw new Exception("Six deployment points changed");
            var originalProjection=new TileMapLayer {TileSet=GD.Load<TileSet>("res://tilesets/arena_tileset.tres")};
            foreach(var cell in expectedAllies.Concat(expectedEnemies)) {
                var expected=originalProjection.MapToLocal(cell);
                if(board.MapToLocal(cell)!=expected) throw new Exception("Deployment projection changed");
            }
            originalProjection.Free();
            var arena=new Node2D {YSortEnabled=true}; AddChild(arena);
            foreach(var child in source.GetChildren().ToArray())
                if(child is TileMapLayer || child.Name=="Wildlife") { child.Owner=null; source.RemoveChild(child); arena.AddChild(child); }
            source.Free();
            var wildlife=arena.GetNode<ArenaWildlife>("Wildlife");
            if(wildlife.VegetationCount<35) throw new Exception("Forest bushes missing");
            if(arena.GetChildren().OfType<TileMapLayer>().Any(t=>t.TileSet!=GD.Load<TileSet>(t.Name=="GardenTrees"?"res://tilesets/bush_tileset.tres":"res://tilesets/woodland_tileset.tres"))) throw new Exception("Terrain uses another asset pack");
            var usedStone=arena.GetChildren().OfType<TileMapLayer>().SelectMany(t=>t.GetUsedCells().Where(c=>t.GetCellSourceId(c)==1).Select(c=>t.GetCellAtlasCoords(c))).Distinct().Count();
            if(usedStone!=971) throw new Exception("Stone atlas coverage incomplete");
            if(wildlife.AnimalCount<40) throw new Exception("Not enough wildlife habitats");
            if(wildlife.IsWalkable(new Vector2I(50,18))) throw new Exception("Battlefield must be protected");
            var animals=wildlife.GetChildren().OfType<AnimatedSprite2D>().ToArray();
            var initial=animals.Select(a=>a.Position).ToArray();
            int checks=0;
            for(int i=0;i<6000;i++)
            {
                wildlife._Process(.05);
                if(!wildlife.AllAnimalsOnLand) throw new Exception("Wildlife crossed water, scenery, or battlefield");
                if(animals.Select(a=>a.Position).Distinct().Count()!=animals.Length) throw new Exception("Animals overlap");
                checks++;
            }
            if(animals.Where((a,i)=>a.Position.DistanceTo(initial[i])>2).Count()<35) throw new Exception("Wildlife failed to roam independently");
            foreach(var a in animals) foreach(var animation in a.SpriteFrames.GetAnimationNames())
                for(int frame=0;frame<a.SpriteFrames.GetFrameCount(animation);frame++)
                {
                    var texture=(AtlasTexture)a.SpriteFrames.GetFrameTexture(animation,frame);
                    if(texture.Region.End.X>texture.Atlas.GetWidth() || texture.Region.End.Y>texture.Atlas.GetHeight()) throw new Exception("Invalid critter atlas frame");
                }
            var camera=new Camera2D {Position=new(470,420),Zoom=new(.9f,.9f)}; arena.AddChild(camera);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("res://tests/living-garden.png");
            arena.Free();
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=false;
            AddChild(field);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(field.GetNode("HeroSlots").GetChildCount()!=6 || field.GetNode("EnemyHeroSlots").GetChildCount()!=6) throw new Exception("Six battle slot markers missing");
            if(BattleDisplay.SupportCells.Any(c=>c.X!=41) || BattleDisplay.EnemySupportCells.Any(c=>c.X!=61)) throw new Exception("Special slots are too far from heroes");
            var pa=field.GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileA");var pb=field.GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileB");
            var selected=CardBackCatalog.All[1].Id;
            var state=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="waiting";state.Players[0]!.CardBackId=selected;state.Players[1]!.CardBackId=CardBackCatalog.DefaultId;
            field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ApplyNetworkState(state,state.Players[0]!.UserId);
            if(pa.CardBackId!=selected || pb.CardBackId!=CardBackCatalog.DefaultId) throw new Exception("Equipped card backs do not match network players");
            pa.SetNakamaName("123","account-username");pa.SetMmr(984);pa.SetCoins(11);pa.SetHealth(24);
            if(pa.GetNode<Label>("PlayerName").Text!="123" || pa.GetNode<Label>("Coins").Text!="24 HP" || pa.GetNode<Label>("Score").Visible) throw new Exception("Profile still shows removed text");
            pa.SetNakamaName("","fallback-username");
            if(pa.GetNode<Label>("PlayerName").Text!="fallback-username") throw new Exception("Nakama username fallback missing");
            pa.SetNakamaName("123","account-username");pb.SetPlayer("bot:preview",null,"");
            var back=pa.GetNode<TextureRect>("EquippedCardBack");
            if(back.Texture==null || back.MouseFilter!=Control.MouseFilterEnum.Ignore || back.Position.X+back.Size.X>pa.Size.X) throw new Exception("Profile card back is missing or overflows");
            pb.SetCardBack(selected);pb.SetCardBack("unknown-back");
            if(pb.CardBackId!=CardBackCatalog.DefaultId) throw new Exception("Unknown back must fall back");
            var mobile=GD.Load<PackedScene>("res://scenes/player_profile.tscn").Instantiate<PlayerProfile>();AddChild(mobile);mobile.CompactMobile();
            back=mobile.GetNode<TextureRect>("EquippedCardBack");
            if(back.Position.X+back.Size.X>mobile.Size.X || mobile.GetNode<Label>("PlayerName").Position.X+mobile.GetNode<Label>("PlayerName").Size.X>back.Position.X) throw new Exception("Mobile profile card back overlaps text");
            mobile.Free();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("res://tests/sanctuary-in-game.png");
            foreach(var size in new[]{new Vector2I(1600,720),new Vector2I(1280,900)}) {
                GetWindow().Size=size;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var ground=field.GetNode<TerrainCoverage>("GardenGround");ground.CoverViewport();
                var visible=GetViewport().GetVisibleRect();var inverse=ground.GetGlobalTransformWithCanvas().AffineInverse();
                foreach(var corner in new[]{Vector2.Zero,new Vector2(visible.Size.X,0),visible.Size,new Vector2(0,visible.Size.Y)})
                    if(ground.GetCellSourceId(ground.LocalToMap(inverse*corner))<0) throw new Exception("Window edge exposes empty terrain");
            }
            GetWindow().Size=new Vector2I(1280,720);
            field.Free();
            var preview=new ArenaPreview {Size=new(640,220)}; AddChild(preview);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var menuWildlife=preview.GetChildren().OfType<SubViewport>().Single().GetChildren().OfType<Node2D>().Single().GetNode<ArenaWildlife>("Wildlife");
            if(menuWildlife.AnimalCount<40) throw new Exception("Menu backdrop lost wildlife");
            preview.Free();
            GD.Print($"LIVING GARDEN PASS: {animals.Length} critters, four species, {checks} roaming checks, valid frames, symmetric battlefield, twelve slots preserved, viewport edges covered at three aspect ratios, all 971 stone tiles, closer support slots, equipped profile backs, menu and cleanup");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
