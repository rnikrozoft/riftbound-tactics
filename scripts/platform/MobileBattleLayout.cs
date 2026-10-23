using Godot;
using System.Linq;

public partial class MobileBattleLayout : Node
{
    private BattleDisplay _field=null!;
    private Control _content=null!;
    private bool _standings;
    private TextureButton? _standingsButton;
    public override void _Ready()
    {
        _field=GetParent<BattleDisplay>();_content=_field.GetNode<Control>("UI/SafeArea/Content");
        _standingsButton=DeckMenuUi.GameplayButton("PLAYERS",()=>_standings=!_standings,140,60);
        _standingsButton.Name="MobileStandingsButton";_standingsButton.Position=new(8,76);_content.AddChild(_standingsButton);
        _content.Resized+=Layout;Layout();
    }
    public override void _Process(double delta)
    {
        var roster=_content.GetNodeOrNull<Control>("ArenaStatus");
        if(roster!=null){roster.Visible=_standings||roster.FindChild("ConnectionStatus",true,false) is Control {Visible:true};roster.Position=new(8,140);roster.ZIndex=20;}
        else _standingsButton!.Hide();
        var statuses=_content.GetNodeOrNull<Control>("CombatStatusPanel");
        if(statuses!=null){statuses.OffsetTop=144;statuses.OffsetLeft=-196;statuses.OffsetRight=-8;}
    }
    private void Layout()
    {
        var size=GetViewport().GetVisibleRect().Size;
        var tiles=_field.GetNode<TileMapLayer>("TileMapLayer");
        var points=CardShop.DeploymentCells.Concat(CardShop.EnemyCells).Select(c=>tiles.Position+tiles.MapToLocal(c)).ToArray();
        var min=new Vector2(points.Min(p=>p.X),points.Min(p=>p.Y));var max=new Vector2(points.Max(p=>p.X),points.Max(p=>p.Y));
        _field.Scale=Vector2.One*1.5f;
        _field.Position=new Vector2(size.X*.5f,size.Y*.54f)-(min+max)*.5f*1.5f;
        foreach(var name in new[]{"ProfileA","ProfileB"}){
            var profile=_content.GetNode<PlayerProfile>(name);profile.CompactMobile();
            bool left=profile.AnchorLeft<.5f;profile.OffsetLeft=left?8:-188;profile.OffsetRight=left?188:-8;
        }
        var shop=_content.GetNode<CardShop>("CardShop");
        shop.ZoneA.OffsetLeft=-306;shop.ZoneA.OffsetRight=306;
        shop.ZoneA.OffsetTop=8;shop.ZoneA.OffsetBottom=192;
        foreach(var (name,y) in new[]{("Upgrade",0f),("Reroll",60f),("Lock",120f)}){
            var action=shop.ZoneA.GetNodeOrNull<Control>(name);if(action==null)continue;
            action.Position=new(540,y);action.Size=new(60,60);
            foreach(var icon in action.GetChildren().OfType<TextureRect>()){icon.Position=new(20,6);icon.Size=new(20,20);}
            foreach(var label in action.GetChildren().OfType<Label>()){label.Position=new(0,32);label.Size=new(60,24);label.AddThemeFontSizeOverride("font_size",16);}
        }
        var lockLabel=shop.ZoneA.GetNodeOrNull<Label>("ShopLockLabel");if(lockLabel!=null){lockLabel.Position=new(540,152);lockLabel.Size=new(60,24);lockLabel.AddThemeFontSizeOverride("font_size",16);}
        var title=_content.GetNodeOrNull<Control>("RoundTitle");if(title!=null){title.AnchorLeft=title.AnchorRight=0;title.OffsetLeft=8;title.OffsetRight=208;title.OffsetTop=144;title.OffsetBottom=188;}
        shop.HandRow.OffsetLeft=208;shop.HandRow.OffsetRight=-208;
        var surface=shop.ZoneB.GetNodeOrNull<Panel>("Surface");if(surface!=null){surface.OffsetLeft=196;surface.OffsetRight=-196;}
        var speed=_content.GetNodeOrNull<Control>("BattleSpeedButton");
        if(speed!=null){speed.OffsetTop=-152;speed.OffsetBottom=-92;speed.OffsetLeft=8;speed.OffsetRight=108;}
        var back=_content.GetNodeOrNull<Control>("BackButton");
        if(back!=null){back.Position=new(8,8);back.Size=new(160,60);}
        var result=_content.GetNode<Control>("BattleResult");result.OffsetLeft=-230;result.OffsetRight=230;
    }
    public override void _ExitTree(){if(GodotObject.IsInstanceValid(_content))_content.Resized-=Layout;}
}
