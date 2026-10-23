using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;

public partial class CardBacksRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(){for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("/tmp/"+name+".png");
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try{
            Check(CardBackCatalog.All.Length==20,"card back catalog count");
            var signatures=new System.Collections.Generic.HashSet<string>();
            foreach(var back in CardBackCatalog.All){
                var texture=CardBackCatalog.Texture(back.Id);Check(texture.Region.End.X<=texture.Atlas.GetWidth()&&texture.Region.End.Y<=texture.Atlas.GetHeight(),"back crop outside atlas");
                using var image=texture.GetImage();signatures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData())));
            }
            Check(signatures.Count==20,"duplicate card back crops");
            var starter=DeckDefinition.Starter();Check(starter.Cards.Count==30&&starter.Validate()=="","unique 30-slot starter");
            Check(!CardCatalog.HeroGroups.Any(g=>g>=1&&g<=4),"retired roles selectable");
            var groups=CharacterData.All.Where(c=>c.Enabled).Select(c=>c.ScenePath).ToArray();Check(groups.Distinct().Count()==groups.Length,"duplicate role scenes");
            var state=new PlayerBootstrapResponse {Profile=new(){OwnedCardBacks=new[]{CardBackCatalog.DefaultId,"card_back_01"}}};
            typeof(PlayerInventory).GetProperty(nameof(PlayerInventory.State))!.SetValue(null,state);
            DeckStore.ApplyRemote("",new(){starter},starter.Id);DeckStore.EditId=starter.Id;
            var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);await Frames();
            var cards=editor.Draft.Cards.Select(c=>(c.Kind,c.Copies)).ToArray();
            Check(!editor.SelectCardBack("card_back_02"),"locked back selectable");Check(!editor.SelectCardBack("forged"),"unknown back selectable");
            Check(editor.SelectCardBack("card_back_01")&&editor.Draft.CardBackId=="card_back_01","owned back selection");
            Check(cards.SequenceEqual(editor.Draft.Cards.Select(c=>(c.Kind,c.Copies))),"cosmetic selection changed cards");
            var clone=editor.Draft.Clone();Check(clone.CardBackId=="card_back_01","clone lost selection");
            var stored=StoredDeck.From(clone);var roundTrip=JsonSerializer.Deserialize(JsonSerializer.Serialize(stored,GameJsonContext.Default.StoredDeck),GameJsonContext.Default.StoredDeck)!.ToDeck();
            Check(roundTrip.CardBackId==clone.CardBackId&&roundTrip.Validate()=="","cloud model lost card back");
            var old=JsonSerializer.Deserialize("{\"name\":\"Old deck\"}",GameJsonContext.Default.DeckDefinition)!;Check(old.CardBackId==CardBackCatalog.DefaultId,"old deck default back");
            editor.FindChild("ChooseCardBackButton",true,false).EmitSignal(BaseButton.SignalName.Pressed);await Frames();
            var choices=editor.FindChildren("CardBackChoice_*","",true,false).OfType<TextureButton>().ToArray();
            Check(choices.Length==20&&choices.Count(b=>!b.Disabled)==2,"ownership not reflected in selector");await Capture("riftbound-card-back-selector");
            editor.FindChild("CardBackChoice_card_back_01",true,false).EmitSignal(BaseButton.SignalName.Pressed);await Frames();await Capture("riftbound-card-back-deck");editor.Free();
            var page=new CollectionShopPage();AddChild(page);page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);page.Open(true);page.ShowCardBacks();await Frames();
            Check(page.FindChildren("BuyCardBack_*","",true,false).Count==18,"shop must offer only unowned backs");await Capture("riftbound-card-back-shop");page.Free();
            var game=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();game.GetNode("OnlineBattle").Free();game.GetNode<BattleDemo>("BattleDemo").NetworkEnabled=true;AddChild(game);await Frames();
            var shop=game.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            foreach(string local in new[]{"a","b"}){
                shop.ApplyNetworkState(new OnlineState {Phase="battle",Players=new OnlinePlayer?[]{new(){UserId="a",Team="A",CardBackId="card_back_01",HandCount=3},new(){UserId="b",Team="B",CardBackId="card_back_19",HandCount=2}}},local);
                var expected=CardBackCatalog.Texture(local=="a"?"card_back_19":"card_back_01").Region;
                Check(shop.OpponentHandRow.GetChildren().OfType<TextureRect>().All(c=>((AtlasTexture)c.Texture).Region==expected),"not all opponent cards use chosen back");
                Check(shop.OpponentHandRow.GetChildren().OfType<TextureRect>().Count(c=>c.Visible)==(local=="a"?2:3),"opponent hand count changed");
            }
            var profile=game.GetNode<Control>("UI/SafeArea/Content/ProfileB");
            foreach(int count in new[]{0,3,10}) {
                shop.ApplyNetworkState(new OnlineState {Phase="battle",Players=new OnlinePlayer?[]{new(){UserId="a",Team="A"},new(){UserId="b",Team="B",HandCount=count}}},"a");
                await Frames();
                Check(shop.OpponentHandZone.GetParent()==profile,"opponent hand follows profile");
                Check(Math.Abs(shop.OpponentHandZone.GetGlobalRect().End.Y-(profile.GlobalPosition.Y-8))<1,"opponent hand sits above profile");
                foreach(var card in shop.OpponentHandRow.GetChildren().OfType<TextureRect>().Where(c=>c.Visible)) {
                    Check(card.GetGlobalRect().Position.X>=profile.GlobalPosition.X-1 && card.GetGlobalRect().End.X<=profile.GetGlobalRect().End.X+1,"opponent hand fits profile width");
                }
                if(count==3)await Capture("riftbound-opponent-hand-above-profile");
            }
            game.Free();PlayerInventory.Reset();GD.Print($"CARD BACKS PASS: {_checks} checks / 20 unique crops, ownership, deck selection, clone, cloud format, old defaults, shop and both opponent views");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
