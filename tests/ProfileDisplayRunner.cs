using Godot;
using System;
using System.Linq;

public partial class ProfileDisplayRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var originalPosition=field.Position;
            var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=false;AddChild(field);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var state=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";
            state.Players[0]!.Units[0].Kind=CharacterData.All.Single(c=>c.Enabled&&c.Name=="Lancer").Kind;
            state.Players[0]!.Units[1].Kind=CharacterData.All.Single(c=>c.Enabled&&c.Name=="Soldier").Kind;
            state.Players[0]!.Hand=new[]{new OnlineCard {Token=999,Kind=state.Players[0]!.Offers[0].Kind,Stars=2,Price=2,Paid=4}};
            state.Players[0]!.Offers[1].Kind=CharacterData.All.First(c=>c.Enabled && c.Combat?.Actions.Any(a=>a.Mode=="ranged")==true).Kind;
            state.Players[0]!.Units[2].Kind=CharacterData.All.Single(c=>c.Enabled&&c.Name=="Demon E").Kind;
            state.Players[0]!.Units[3].Kind=CharacterData.All.Single(c=>c.Enabled&&c.Name=="Black Knight C").Kind;
            field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ApplyNetworkState(state,state.Players[0]!.UserId);
            await field.WaitForPhaseTransition();
            if(CardCatalog.Art(0).Atlas.ResourcePath!="res://assets/cards/packs/deco/DecoCard.png")throw new Exception("Deco card skin missing");
            if(field.GetNode<TileMapLayer>("GardenTrees").TileSet.ResourcePath!="res://tilesets/bush_tileset.tres")throw new Exception("Bush tileset missing");
            var units=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").NetworkUnits.Values;
            var lancer=units.First(u=>CharacterData.Get(u.CardKind).Name=="Lancer");
            var soldier=units.First(u=>CharacterData.Get(u.CardKind).Name=="Soldier");
            // Compare the body below the raised lance to the soldier's whole body.
            using(var image=lancer.Sprite.SpriteFrames.GetFrameTexture("idle",0).GetImage())
            using(var body=image.GetRegion(new Rect2I(0,37,100,63))) {
                float bodyHeight=body.GetUsedRect().Size.Y*lancer.Sprite.Scale.Y;
                float soldierHeight=CharacterVisual.VisibleBounds(soldier.Sprite.SpriteFrames.GetFrameTexture("idle",0)).Size.Y*soldier.Sprite.Scale.Y;
                if(bodyHeight<soldierHeight*.85f||bodyHeight>soldierHeight*1.25f)throw new Exception("Lancer body differs too much from soldier");
                GD.Print($"LANCER BODY SCALE PASS: lancer {bodyHeight:F1}px, soldier {soldierHeight:F1}px");
            }
            foreach(var sample in new[]{("Demon E",36),("Black Knight C",39)}) {
                var mounted=units.First(u=>CharacterData.Get(u.CardKind).Name==sample.Item1);
                using var image=mounted.Sprite.SpriteFrames.GetFrameTexture("idle",0).GetImage();
                using var body=image.GetRegion(new Rect2I(0,sample.Item2,100,100-sample.Item2));
                float height=body.GetUsedRect().Size.Y*mounted.Sprite.Scale.Y;
                float reference=CharacterVisual.VisibleBounds(soldier.Sprite.SpriteFrames.GetFrameTexture("idle",0)).Size.Y*soldier.Sprite.Scale.Y;
                if(height<reference*.85f||height>reference*1.25f)throw new Exception("Mounted body differs too much: "+sample.Item1);
                var scale=mounted.Sprite.Scale;mounted.Sprite.Play("attack01");
                if(mounted.Sprite.Scale!=scale)throw new Exception("Attack changed mounted scale");mounted.Sprite.Play("idle");
                GD.Print($"MOUNTED BODY SCALE PASS: {sample.Item1} {height:F1}px, reference {reference:F1}px");
            }
            var row=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ShopRow;
            foreach(var child in row.GetChildren())if(child is ShopCard card) {
                if(card.Size.X<84 || card.StretchMode!=TextureRect.StretchModeEnum.Scale)throw new Exception("Card frame was not expanded");
                if(card.GetNode<CardIdlePortrait>("Fighter").Size.X<card.Size.X*.9f)throw new Exception("Character portrait is still too small");
                foreach(var name in new[]{"CardPrice","CardArmor","CardPower"}) {
                    var stat=card.GetNode<Label>(name);
                    var badge=stat.GetNode<TextureRect>("StatCircle");
                    if(((AtlasTexture)badge.Texture).Region.Size!=new Vector2(20,20) || badge.Size.X!=28 || stat.ClipText)throw new Exception("Pattern circle must be readable and text unclipped");
                    if(stat.Position.X< -4 || stat.Position.Y< -4 || stat.Position.X+stat.Size.X>card.Size.X+4 || stat.Position.Y+stat.Size.Y>card.Size.Y+4)throw new Exception("Stat exceeds its four-pixel corner overhang");
                }
            }
            foreach(var child in row.GetChildren())if(child is ShopCard card && card.GetNodeOrNull<TextureRect>("AttackTypeIcon")!=null)throw new Exception("Upper-right icon remains");
            var profile=field.GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileA");
            profile.SetNakamaName("123","username");profile.SetMmr(984);profile.SetCoins(11);profile.SetHealth(24);
            if(profile.GetNode<Label>("PlayerName").Text!="123" || profile.GetNode<Label>("Coins").Text!="24 HP" || profile.GetNode<Label>("Score").Visible || profile.GetNode<Label>("Rank").Text!="MMR: 984") throw new Exception("Unexpected profile content");
            profile.SetNakamaName(" ","fallback");
            if(profile.GetNode<Label>("PlayerName").Text!="fallback")throw new Exception("Username fallback failed");
            profile.SetNakamaName("123","username");
            var opponent=field.GetNode<PlayerProfile>("UI/SafeArea/Content/ProfileB");opponent.SetPlayer("bot:test",null,"");
            if(opponent.GetNode<Label>("PlayerName").Text!="BOT" || !opponent.GetNode<Label>("Rank").Visible || opponent.GetNode<Label>("Rank").Text!="MMR: 1000" || opponent.GetNode<Label>("Score").Visible)throw new Exception("Bot placeholder details remain");
            opponent.SetMmr(1234);opponent.SetPlayer("bot:test",null,"",true);
            if(opponent.GetNode<Label>("Rank").Text!="MMR: 1234")throw new Exception("Explicit bot MMR overwritten");
            opponent.SetPlayer("bot:fallback",null,"");
            if(field.Position!=originalPosition+new Vector2(0,48))throw new Exception("Arena did not move below shop");
            field.ArenaOffset=new Vector2(15,20);field.ArenaOffset=Vector2.Zero;
            if(field.Position!=originalPosition+new Vector2(0,48))throw new Exception("Combat cleanup undid framing");
            var tiles=field.GetNode<TileMapLayer>("TileMapLayer");
            var floorAtlas=(TileSetAtlasSource)tiles.TileSet.GetSource(2);
            var origin=floorAtlas.GetTileData(new Vector2I(17,0),0).TextureOrigin;
            // The original cube's top diamond center is y=10 in its 32px frame.
            // Godot subtracts texture_origin; its drawn surface must meet the slot plane.
            if(10-16-origin.Y!=0)throw new Exception("Rendered stone surface is offset from deployment plane");
            var cells=tiles.GetUsedCells();
            if(cells.Count!=351 || cells.Min(c=>c.Y)!=12 || cells.Max(c=>c.Y)!=24)throw new Exception("Board must have two-cell upper/lower margins");
            if(CardShop.DeploymentCells.Min(c=>c.Y)-cells.Min(c=>c.Y)!=2 || cells.Max(c=>c.Y)-CardShop.DeploymentCells.Max(c=>c.Y)!=2)throw new Exception("Upper and lower margins differ");
            var a1=tiles.ToGlobal(tiles.MapToLocal(BattleDisplay.SupportCells[0]));
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").ZoneA;
            if(a1.Y-80<shop.GetGlobalRect().End.Y)throw new Exception("A1 body overlaps shop");
            if(profile.GetNode<TextureRect>("EquippedCardBack").Texture==null)throw new Exception("Card back missing");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-original-cards-lancer.png");
            foreach(var child in field.GetChildren())if(child is BattleUnit unit)unit.Hide();
            field.GetNode<CanvasLayer>("UI").Visible=false;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-board-margins.png");
            profile.CompactMobile();profile.SetCoins(20);profile.SetHealth(17);
            if(profile.GetNode<Label>("Coins").Text!="17 HP" || profile.GetNode<Label>("Score").Visible)throw new Exception("Compact profile shows removed text");
            field.Free();GD.Print("PROFILE DISPLAY PASS: plain Nakama name and username fallback, MMR and HP, no leaderboard or gold, bot and mobile, card back retained");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
