using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class CharacterCatalogRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? connection=null;
        try {
            connection=GameAccount.Connection();await connection.Connect();await CharacterData.Refresh(connection);
            if(CharacterData.All.Any(c=>SkillCardVisual.IsSkill(c.Kind)&&c.Name.StartsWith("Paladin Skill")))throw new Exception("Server returned provisional skill catalog");
            if(CardCatalog.Count<60 || DeckDefinition.Starter().Validate()!="")throw new Exception("Catalog or starter deck failed");
            foreach(var character in CharacterData.All){
                var portrait=CardCatalog.Portrait(character.Kind);var art=CardCatalog.Art(character.Kind);
                if(portrait.Region.End.X>portrait.Atlas.GetWidth() || portrait.Region.End.Y>portrait.Atlas.GetHeight() || art.Region.End.X>art.Atlas.GetWidth() || art.Region.End.Y>art.Atlas.GetHeight())throw new Exception("Invalid crop: "+character.Id);
                var actor=GD.Load<PackedScene>(character.ScenePath).Instantiate<BattleUnit>();
                var frames=actor.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames;
                var sprite=actor.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
                CharacterVisual.Normalize(sprite);
                var visible=CharacterVisual.VisibleBounds(frames.GetFrameTexture("idle",0));
                var animationSize=CharacterVisual.AnimationSize(frames,BattleAnimations.Idle)*sprite.Scale;
                if(animationSize.Y>CharacterVisual.FieldHeight*((character.ScenePath.Contains("/demon_e.")||character.ScenePath.Contains("/black_knight_c."))?1.5f:character.ScenePath.Contains("/lancer.")?1.8f:character.ScenePath.Contains("/black_knight_a.")?1.62f:character.ScenePath.Contains("/orc_rider.")?1.2f:CharacterData.IsSupport(character.Kind)?1.65f:1)+.01f||animationSize.X>CharacterVisual.FieldWidth*((character.ScenePath.Contains("/demon_e.")||character.ScenePath.Contains("/black_knight_c."))?1.5f:character.ScenePath.Contains("/lancer.")?1.8f:character.ScenePath.Contains("/black_knight_a.")?1.62f:character.ScenePath.Contains("/orc_rider.")?1.2f:CharacterData.IsSupport(character.Kind)?1.65f:1)+.01f)throw new Exception("Oversized character: "+character.Id);
                if(portrait.GetWidth()/(float)portrait.GetHeight()<1.49f || portrait.GetWidth()/(float)portrait.GetHeight()>1.55f)throw new Exception("Unequal portrait canvas: "+character.Id);
                foreach(string animation in new[]{"idle","walk","attack","hit","die"}){
                    if(!frames.HasAnimation(animation) || frames.GetFrameCount(animation)==0)throw new Exception("Missing animation: "+character.Id+" "+animation);
                    for(int f=0;f<frames.GetFrameCount(animation);f++){
                        var atlas=(AtlasTexture)frames.GetFrameTexture(animation,f);
                        if(atlas.Region.End.X>atlas.Atlas.GetWidth() || atlas.Region.End.Y>atlas.Atlas.GetHeight())throw new Exception("Invalid animation frame: "+character.Id);
                    }
                }
                actor.Free();
            }
            var definition=CharacterData.Get(0);var unit=GD.Load<PackedScene>(definition.ScenePath).Instantiate<BattleUnit>();unit.CardKind=0;unit.ConfigureStars(4);AddChild(unit);
            var stats=CharacterData.Stats(0,4);if(unit.Health!=stats.Hp || unit.Attack!=stats.Attack || unit.Speed!=stats.Speed)throw new Exception("Client stats do not match catalog");unit.QueueFree();
            if(string.IsNullOrWhiteSpace(definition.AbilityDescription) || definition.Description.Contains("Lorem"))throw new Exception("Missing catalog details");
            var orc=CharacterData.All.Single(c=>c.Id=="roster_orc");
            var demon=CharacterData.All.Single(c=>c.Id=="roster_demon");var monster=CharacterData.All.Single(c=>c.Id=="roster_blood_monster");
            if(orc.Group!=46 || demon.Group!=47 || monster.Group!=48 || CharacterData.All.Count(c=>c.Group==4)!=78)throw new Exception("Dedicated character groups failed");
            DeckStore.EditId="";var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);
            for(int index=0;index<3;index++)editor.SetHero(index,45+index);
            if(editor.Draft.Validate()!="" || !editor.Draft.Heroes.SequenceEqual(new[]{45,46,47}))throw new Exception("New characters cannot form a valid deck");
            foreach(int group in new[]{45,46,47}){
                editor.SetGroup(group);if(editor.VisibleCardCount!=CharacterData.All.Count(c=>c.Enabled&&c.Group==group))throw new Exception("Incomplete character card set");
                if(editor.Draft.Cards.Count(c=>CardCatalog.Group(c.Kind)==group)!=10)throw new Exception("Wrong character deck slots");
            }
            foreach(int group in CardCatalog.HeroGroups.Where(g=>!editor.Draft.Heroes.Skip(1).Contains(g))) {
                editor.SetHero(0,group);editor.SetGroup(group);
                if(editor.Draft.Validate()!="" || editor.VisibleCardCount!=CharacterData.All.Count(c=>c.Enabled&&c.Group==group))throw new Exception("New character cannot form a complete deck: "+CardCatalog.Groups[group]);
            }
            editor.SetHero(0,48);
            typeof(DeckBuilder).GetMethod("ShowHeroChoices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(editor,null);
            for(int frame=0;frame<8;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-new-character-selection.png");
            }
            editor.SetHero(0,45);
            editor.SetGroup(45);
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-dedicated-characters.png");
            }
            editor.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(CharacterData.All.Any(c=>c.Enabled&&c.Group>=1&&c.Group<=4&&!CharacterData.IsSupport(c.Kind)))throw new Exception("Duplicate roles still enabled");
            foreach(string localUser in new[]{"self","enemy"}){
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            field.GetNode("OnlineBattle").Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;
            AddChild(field);
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=new OnlineState {Phase="preparation",Round=1,Players=new OnlinePlayer?[]{
                new(){UserId="self",Team="A",Units=new[]{new OnlineUnit {Kind=orc.Kind,Token=1,Slot=0}}},
                new(){UserId="enemy",Team="B",Units=new[]{new OnlineUnit {Kind=0,Token=2,Slot=0},new OnlineUnit {Kind=orc.Kind,Token=3,Slot=1},new OnlineUnit {Kind=demon.Kind,Token=4,Slot=2},new OnlineUnit {Kind=monster.Kind,Token=5,Slot=3},new OnlineUnit {Kind=CardCatalog.Options(45,2).First(),Token=6,Slot=4},new OnlineUnit {Kind=CardCatalog.Options(46,3).First(),Token=7,Slot=5}}}
            }};
            shop.ApplyNetworkState(state,localUser);
            foreach(var pair in shop.NetworkUnits){
                var u=pair.Value;
                if(u.SceneFilePath!=CharacterData.Get(u.CardKind).ScenePath)throw new Exception("Wrong character asset: "+pair.Key);
                if(u.Sprite.FlipH==u.IsAlly)throw new Exception("Wrong facing: "+pair.Key);
            }
            if(localUser=="self" && DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-mixed-characters.png");
            }
            field.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            await connection.Close();GD.Print("CHARACTER CATALOG PASS: authenticated RPC, three dedicated characters, complete deck sets, unique character groups and 30-slot decks, exact scenes on both teams, animations and image crops");GetTree().Quit();
        }catch(Exception e){if(connection!=null)await connection.Close();GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
