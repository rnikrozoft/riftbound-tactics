using Godot;
using System;

public static class DeckMenuUi
{
    public static Label Text(string text,int size=18)
    {
        var label=new Label {Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};
        GameUi.Label(label,size,size<16); return label;
    }
    public static TextureButton Button(string text,Action action,int width=180,int height=38)
    {
        var button=new TextureButton {CustomMinimumSize=new(width,height),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};
        TravelBookUi.StyleButton(button);
        var label=Text(text,15); label.Name="Text"; GameUi.Label(label,15); label.HorizontalAlignment=HorizontalAlignment.Center; label.VerticalAlignment=VerticalAlignment.Center;
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);button.Pressed+=action;return button;
    }
    public static LineEdit Input(string text,string placeholder,int width=220)
    {
        var edit=new LineEdit {Text=text,PlaceholderText=placeholder,MaxLength=40,CustomMinimumSize=new(width,38)};
        foreach(string state in new[]{"normal","focus","read_only"}) edit.AddThemeStyleboxOverride(state,GameUi.Box(state=="focus",10));
        edit.AddThemeColorOverride("font_color",GameUi.Text);edit.AddThemeColorOverride("font_placeholder_color",GameUi.Muted); return edit;
    }
    public static void Clear(Node node)
    {
        foreach(Node child in node.GetChildren()){node.RemoveChild(child);child.QueueFree();}
    }
    public static TextureRect Art(int kind,int width,int height)
    {
        var art=new TextureRect {Texture=CardCatalog.Art(kind),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new(width,height),MouseFilter=Control.MouseFilterEnum.Ignore};
        GameUi.DecorateCard(art,kind);return art;
    }
    public static VBoxContainer Panel(Node parent,int width=0)
    {
        var frame=new PanelContainer {CustomMinimumSize=new(width,0),SizeFlagsVertical=Control.SizeFlags.ExpandFill};
        frame.AddThemeStyleboxOverride("panel",GameUi.Box());parent.AddChild(frame);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",10);frame.AddChild(box);return box;
    }
    public static VBoxContainer Page(Control root)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        var background=new ColorRect {Color=GameUi.Ink,MouseFilter=Control.MouseFilterEnum.Ignore};
        root.AddChild(background);background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var edge=new ColorRect {Color=GameUi.Gold,MouseFilter=Control.MouseFilterEnum.Ignore,AnchorRight=1,OffsetBottom=3};root.AddChild(edge);
        var margin=new MarginContainer();root.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(string side in new[]{"left","right","top","bottom"}) margin.AddThemeConstantOverride("margin_"+side,28);
        var layout=new VBoxContainer();layout.AddThemeConstantOverride("separation",12);margin.AddChild(layout);return layout;
    }
}
