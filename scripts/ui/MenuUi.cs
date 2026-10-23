using Godot;

// Opt-in menu styling. The battle HUD continues to use GameUi/SlateBrassUi.
public static class MenuUi
{
    public static readonly Color Background=new("0b1019"),Surface=new("141d2b"),Raised=new("1c293b"),Line=new("2b3b51"),Accent=new("69dfc5"),White=new("e9eef5"),Muted=new("91a3b9");
    private static Theme? _theme;
    public static StyleBoxFlat Box(bool accent=false,int padding=18)
    {
        return new StyleBoxFlat {BgColor=Surface,BorderColor=accent?Accent:Line,BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,
            CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,
            ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    }
    public static Theme Theme()
    {
        if(_theme!=null)return _theme;
        var t=new Theme {DefaultFontSize=16};
        t.SetColor("font_color","Label",White);t.SetColor("font_color","LineEdit",White);t.SetColor("font_placeholder_color","LineEdit",Muted);
        foreach(string type in new[]{"LineEdit","SpinBox","OptionButton","Button"}){
            t.SetStylebox("normal",type,Box(false,12));var hover=Box(false,12);hover.BgColor=Raised;t.SetStylebox("hover",type,hover);
            t.SetStylebox("pressed",type,Box(true,12));t.SetStylebox("focus",type,Box(true,12));t.SetStylebox("disabled",type,Box(false,12));
            t.SetColor("font_color",type,White);t.SetColor("font_hover_color",type,Accent);t.SetColor("font_disabled_color",type,Muted);
        }
        t.SetStylebox("panel","TabContainer",Box(false,12));
        t.SetStylebox("tab_selected","TabContainer",Box(true,12));t.SetStylebox("tab_unselected","TabContainer",Box(false,12));
        t.SetStylebox("tab_selected","TabBar",Box(true,12));t.SetStylebox("tab_unselected","TabBar",Box(false,12));
        t.SetStylebox("panel","PanelContainer",Box());t.SetStylebox("panel","PopupMenu",Box());
        t.SetColor("font_color","PopupMenu",White);t.SetStylebox("hover","PopupMenu",Box(true,8));
        foreach(string type in new[]{"HScrollBar","VScrollBar"}){
            var track=Box(false,0);track.BgColor=Background;track.BorderWidthLeft=track.BorderWidthRight=track.BorderWidthTop=track.BorderWidthBottom=0;
            var grab=Box(false,0);grab.BgColor=Line;
            t.SetStylebox("scroll",type,track);t.SetStylebox("grabber",type,grab);t.SetStylebox("grabber_highlight",type,Box(true,0));t.SetStylebox("grabber_pressed",type,Box(true,0));
        }
        t.SetConstant("separation","VBoxContainer",12);t.SetConstant("separation","HBoxContainer",12);
        _theme=t;return t;
    }
    public static void Button(TextureButton button,bool primary=false,bool selected=false)
    {
        button.SetMeta("custom_menu",true);button.FocusMode=Control.FocusModeEnum.All;button.Material=null;button.SelfModulate=Colors.White;
        button.TextureNormal=button.TextureHover=button.TexturePressed=button.TextureDisabled=button.TextureFocused=null;
        var old=button.GetNodeOrNull("SlateBrassSkin");if(old!=null){button.RemoveChild(old);old.QueueFree();}
        var skin=button.GetNodeOrNull<MenuButtonSkin>("MenuSkin");
        if(skin==null){skin=new MenuButtonSkin {Name="MenuSkin",Button=button,MouseFilter=Control.MouseFilterEnum.Ignore};button.AddChild(skin);button.MoveChild(skin,0);skin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);}
        skin.Primary=primary;skin.Selected=selected;skin.QueueRedraw();
        foreach(var node in button.GetChildren())if(node is Label label)label.AddThemeColorOverride("font_color",primary?Background:selected?Accent:White);
    }
    public static void StyleSurface(Control control)
    {
        if(control is NinePatchRect patch)patch.Texture=null;
        if(control is TextureRect image)image.Texture=null;
        var panel=control.GetNodeOrNull<Panel>("CustomSurface");
        if(panel==null){panel=new Panel {Name="CustomSurface",MouseFilter=Control.MouseFilterEnum.Ignore,ShowBehindParent=true};control.AddChild(panel);control.MoveChild(panel,0);panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);}
        panel.AddThemeStyleboxOverride("panel",Box(false,0));
    }
    public static Label Eyebrow(string text)
    {
        var label=DeckMenuUi.Text(text,12);label.AddThemeColorOverride("font_color",Accent);return label;
    }
}
