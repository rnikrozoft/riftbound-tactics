using Godot;

// Shared visual language for menus, combat HUD and the combat simulator.
public static class GameUi
{
    public static readonly Color Ink=new("101820"), Panel=new("18232f"), Border=new("3b4c5c"),
        Gold=new("e9bf77"), Text=new("efe9dc"), Muted=new("91a2b3"), Teal=new("82c9bd");
    public static StyleBoxFlat Box(bool accent=false,int padding=14)
    {
        return new StyleBoxFlat {
            BgColor=Panel, BorderColor=accent?Gold:Border,
            BorderWidthLeft=1, BorderWidthRight=1, BorderWidthTop=accent?2:1, BorderWidthBottom=1,
            CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5,
            ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding,
            ShadowColor=new Color(0,0,0,.3f),ShadowSize=6,ShadowOffset=new(0,4)
        };
    }
    public static Panel Backdrop(Control parent,bool accent=false)
    {
        var panel=new Panel {Name="Surface",MouseFilter=Control.MouseFilterEnum.Ignore,ShowBehindParent=true};
        panel.AddThemeStyleboxOverride("panel",Box(accent,0)); parent.AddChild(panel);parent.MoveChild(panel,0);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);return panel;
    }
    public static void Label(Label label,int size=16,bool muted=false)
    {
        label.Modulate=Colors.White;label.AddThemeColorOverride("font_color",muted?Muted:Text);
        label.AddThemeFontSizeOverride("font_size",size);
    }
    public static void Primary(TextureButton button)
    {
        if(button.Material is ShaderMaterial material) {material.SetShaderParameter("fill_color",Gold);material.SetShaderParameter("edge_color",new Color("ffe0a3"));}
        foreach(var node in button.GetChildren())if(node is Label label)label.AddThemeColorOverride("font_color",Ink);
    }
    public static void Selected(TextureButton button,bool selected)
    {
        if(button.Material is ShaderMaterial material) {
            material.SetShaderParameter("fill_color",selected?new Color("293c4d"):new Color("213140"));
            material.SetShaderParameter("edge_color",selected?Gold:Border);
        }
        foreach(var node in button.GetChildren())if(node is Label label)label.AddThemeColorOverride("font_color",selected?Gold:Text);
    }
    private static Texture2D? _fighter;
    public static void DecorateCard(TextureRect card,string cardName)
    {
        for(int kind=0;kind<CardCatalog.Count;kind++)if(CardCatalog.Name(kind)==cardName){DecorateCard(card,kind);return;}
        DecorateCard(card,0);
    }
    public static void DecorateCard(TextureRect card,int kind)
    {
        card.SelfModulate=new Color(.32f,.4f,.48f);
        var figure=card.GetNodeOrNull<TextureRect>("Fighter");
        if(figure==null) {
            _fighter ??= new AtlasTexture {Atlas=GD.Load<Texture2D>("res://assets/characters/knight/knight_idle.png"),Region=new Rect2(40,36,20,28)};
            figure=new TextureRect {Name="Fighter",Texture=_fighter,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};
            card.AddChild(figure);
            var cardFigure=figure;
            card.Resized+=()=>LayoutCardFigure(card,cardFigure);
            var emblem=Icon("IconEnergy01a",12);emblem.Name="ClassEmblem";card.AddChild(emblem);emblem.Position=new(5,5);emblem.Size=new(12,12);
        }
        figure.Texture=CardCatalog.Portrait(kind);
        LayoutCardFigure(card,figure);
        // A newly added/reused hand card may not have its container size yet.
        var portrait=figure;
        Callable.From(()=> {
            if(GodotObject.IsInstanceValid(card) && GodotObject.IsInstanceValid(portrait) && !card.IsQueuedForDeletion())
                LayoutCardFigure(card,portrait);
        }).CallDeferred();
        var emblems=new[]{"IconStar01a","IconArrow01a","IconEnergy01a","IconHeart01a","IconHome01a","IconStar01a","IconEnergy01a","IconHeart01a"};
        int group=CardCatalog.Group(kind);
        card.GetNode<TextureRect>("ClassEmblem").Texture=TravelBookUi.Texture(group<emblems.Length?emblems[group]:"IconStar01a");
    }
    private static void LayoutCardFigure(TextureRect card,TextureRect figure)
    {
        if(card.Texture==null || card.Size.X<=0 || card.Size.Y<=0)return;
        var textureSize=card.Texture.GetSize();
        float fit=Mathf.Min(card.Size.X/textureSize.X,card.Size.Y/textureSize.Y);
        var drawn=textureSize*fit;
        figure.Position=(card.Size-drawn)/2+drawn*new Vector2(.18f,.2f);
        figure.Size=drawn*new Vector2(.64f,.62f);
    }
    public static TextureRect Icon(string name,int size=20) => new() {
        Texture=TravelBookUi.Texture(name),CustomMinimumSize=new(size,size),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore
    };
}
