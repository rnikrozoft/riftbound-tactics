using Godot;
using System.Linq;

// Shared visual language for menus, combat HUD and the combat simulator.
public static class GameUi
{
    public static readonly Color Ink=new("101820"), Panel=new("18232f"), Border=new("3b4c5c"),
        Gold=new("f5d37a"), Text=new("f1e9d2"), Muted=new("91a2b3"), Teal=new("3ec7c3");
    public static StyleBoxFlat Box(bool accent=false,int padding=14)=>MenuUi.Box(accent,padding);
    public static Panel Backdrop(Control parent,bool accent=false)
    {
        var panel=new Panel {Name="Surface",MouseFilter=Control.MouseFilterEnum.Ignore,ShowBehindParent=true};
        panel.AddThemeStyleboxOverride("panel",Box(accent,0)); parent.AddChild(panel);parent.MoveChild(panel,0);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);return panel;
    }
    public static void Label(Label label,int size=16,bool muted=false)
    {
        label.Modulate=Colors.White;label.AddThemeColorOverride("font_color",muted?Muted:Text);
        label.AddThemeFontSizeOverride("font_size",MobileLayout.Enabled?Mathf.Max(size,muted?20:22):size);
    }
    public static void Primary(TextureButton button){button.SetMeta("menu_primary",true);MenuUi.Button(button,true);}
    public static void Selected(TextureButton button,bool selected)=>MenuUi.Button(button,button.HasMeta("menu_primary"),selected);
    private static Texture2D? _fighter;
    public static void DecorateCard(TextureRect card,string cardName)
    {
        for(int kind=0;kind<CardCatalog.Count;kind++)if(CardCatalog.Name(kind)==cardName){DecorateCard(card,kind);return;}
        DecorateCard(card,0);
    }
    public static void DecorateCard(TextureRect card,int kind)
    {
        card.SelfModulate=Colors.White;
        var figure=card.GetNodeOrNull<CardIdlePortrait>("Fighter");
        if(figure==null) {
            _fighter ??= new AtlasTexture {Atlas=GD.Load<Texture2D>("res://assets/characters/knight/knight_idle.png"),Region=new Rect2(40,36,20,28)};
            figure=new CardIdlePortrait {Name="Fighter",Texture=_fighter,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};
            card.AddChild(figure);
            var cardFigure=figure;
            card.Resized+=()=>LayoutCardFigure(card,cardFigure);
            var emblem=Icon("IconEnergy01a",12);emblem.Name="ClassEmblem";card.AddChild(emblem);emblem.Position=new(5,5);emblem.Size=new(12,12);
        }
        figure.SetKind(kind);
        CardSheen.Attach(card);
        LayoutCardFigure(card,figure);
        // A newly added/reused hand card may not have its container size yet.
        var portrait=figure;
        Callable.From(()=> {
            if(GodotObject.IsInstanceValid(card) && GodotObject.IsInstanceValid(portrait) && !card.IsQueuedForDeletion())
                LayoutCardFigure(card,portrait);
        }).CallDeferred();
        var emblems=new[]{"IconStar01a","IconArrow01a","IconEnergy01a","IconHeart01a","IconHome01a","IconStar01a","IconEnergy01a","IconHeart01a"};
        card.GetNode<TextureRect>("ClassEmblem").Visible=!CharacterData.IsSupport(kind);
        int group=CardCatalog.Group(kind);
        card.GetNode<TextureRect>("ClassEmblem").Texture=TravelBookUi.Texture(group<emblems.Length?emblems[group]:"IconStar01a");
    }
    public static AtlasTexture CardCircle(int color=0)=>new() {Atlas=GD.Load<Texture2D>("res://assets/cards/card_frames.png"),Region=new Rect2(376+color*24,183,20,20)};
    private static void LayoutCardFigure(TextureRect card,TextureRect figure)
    {
        if(card.Texture==null || card.Size.X<=0 || card.Size.Y<=0)return;
        var textureSize=card.Texture.GetSize();
        float fit=Mathf.Min(card.Size.X/textureSize.X,card.Size.Y/textureSize.Y);
        var drawn=card.StretchMode==TextureRect.StretchModeEnum.Scale?card.Size:textureSize*fit;
        figure.Position=(card.Size-drawn)/2+drawn*new Vector2(.04f,.14f);
        figure.Size=drawn*new Vector2(.92f,.66f);
    }
    public static TextureRect Icon(string name,int size=20) => new() {
        Texture=SlateBrassUi.Icon(name),CustomMinimumSize=new(size,size),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore
    };
}
