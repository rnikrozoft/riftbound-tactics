using Godot;
using System;

public static class DeckMenuUi
{
    public static Label Text(string text,int size=18)
    {
        var label=new Label {Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeColorOverride("font_color",Colors.Black); label.AddThemeFontSizeOverride("font_size",size); return label;
    }
    public static TextureButton Button(string text,Action action,int width=180,int height=38)
    {
        var button=new TextureButton {CustomMinimumSize=new(width,height),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};
        TravelBookUi.StyleButton(button);
        var label=Text(text,15); label.HorizontalAlignment=HorizontalAlignment.Center; label.VerticalAlignment=VerticalAlignment.Center;
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);button.Pressed+=action;return button;
    }
    public static LineEdit Input(string text,string placeholder,int width=220)
    {
        var edit=new LineEdit {Text=text,PlaceholderText=placeholder,MaxLength=40,CustomMinimumSize=new(width,38)};
        foreach(string state in new[]{"normal","focus","read_only"}) edit.AddThemeStyleboxOverride(state,new StyleBoxTexture {Texture=TravelBookUi.Texture("Frame01a"),TextureMarginLeft=4,TextureMarginRight=4,TextureMarginTop=4,TextureMarginBottom=4,ContentMarginLeft=10,ContentMarginRight=10});
        edit.AddThemeColorOverride("font_color",Colors.Black);edit.AddThemeColorOverride("font_placeholder_color",Colors.DimGray); return edit;
    }
    public static void Clear(Node node)
    {
        foreach(Node child in node.GetChildren()){node.RemoveChild(child);child.QueueFree();}
    }
    public static TextureRect Art(int kind,int width,int height)
    {
        var art=new TextureRect {Texture=CardCatalog.Art(kind),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new(width,height),MouseFilter=Control.MouseFilterEnum.Ignore};
        return art;
    }
    public static VBoxContainer Panel(Node parent,int width=0)
    {
        var frame=new NinePatchRect {Texture=TravelBookUi.Texture("Popup01a"),PatchMarginLeft=6,PatchMarginRight=6,PatchMarginTop=6,PatchMarginBottom=6,CustomMinimumSize=new(width,0),SizeFlagsVertical=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore};parent.AddChild(frame);
        var margin=new MarginContainer();frame.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(string side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,12);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",8);margin.AddChild(box);return box;
    }
    public static VBoxContainer Page(Control root)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        var background=new NinePatchRect {Texture=TravelBookUi.Texture("BookPageRight01a"),PatchMarginLeft=12,PatchMarginRight=12,PatchMarginTop=12,PatchMarginBottom=12,MouseFilter=Control.MouseFilterEnum.Ignore};
        root.AddChild(background);background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var margin=new MarginContainer();root.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(string side in new[]{"left","right","top","bottom"}) margin.AddThemeConstantOverride("margin_"+side,24);
        var layout=new VBoxContainer();layout.AddThemeConstantOverride("separation",12);margin.AddChild(layout);return layout;
    }
}
