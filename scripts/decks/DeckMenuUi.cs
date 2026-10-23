using Godot;
using System;

public static class DeckMenuUi
{
    public static Label Text(string text,int size=18)
    {
        var label=new Label {Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};
        GameUi.Label(label,size,size<16);label.AddThemeFontSizeOverride("font_size",MobileLayout.Enabled?Math.Max(14,size):size);return label;
    }
    public static Label GameplayText(string text,int size=18)
    {
        var label=new Label {Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(label,size,size<16);return label;
    }
    public static TextureButton Button(string text,Action action,int width=180,int height=38)
    {
        var button=new TextureButton {CustomMinimumSize=new(MobileLayout.Enabled?Math.Max(width,60):width,MobileLayout.Enabled?Math.Max(height,60):height),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};
        MenuUi.Button(button);
        var label=Text(text,15); label.Name="Text";label.AutowrapMode=TextServer.AutowrapMode.Off;label.ClipText=true;label.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;label.AddThemeColorOverride("font_color",MenuUi.White); label.HorizontalAlignment=HorizontalAlignment.Center; label.VerticalAlignment=VerticalAlignment.Center;
        button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);button.Pressed+=action;return button;
    }
    public static TextureButton GameplayButton(string text,Action action,int width=180,int height=38)
    {
        var button=new TextureButton {CustomMinimumSize=new(MobileLayout.Enabled?Math.Max(width,60):width,MobileLayout.Enabled?Math.Max(height,60):height),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};
        TravelBookUi.StyleButton(button);
        var label=GameplayText(text,15);GameUi.Label(label,15);label.Name="Text";label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
        button.AddChild(label);label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);button.Pressed+=action;return button;
    }
    public static LineEdit Input(string text,string placeholder,int width=220)
    {
        var edit=new LineEdit {Text=text,PlaceholderText=placeholder,MaxLength=40,CustomMinimumSize=new(width,MobileLayout.Enabled?60:38)};
        if(MobileLayout.Enabled)edit.AddThemeFontSizeOverride("font_size",22);
        foreach(string state in new[]{"normal","focus","read_only"}) edit.AddThemeStyleboxOverride(state,MenuUi.Box(state=="focus",12));
        edit.AddThemeColorOverride("font_color",GameUi.Text);edit.AddThemeColorOverride("font_placeholder_color",GameUi.Muted); return edit;
    }
    public static void Clear(Node node)
    {
        foreach(Node child in node.GetChildren()){node.RemoveChild(child);child.QueueFree();}
    }
    public static TextureRect Art(int kind,int width,int height)
    {
        var art=new TextureRect {TextureFilter=CanvasItem.TextureFilterEnum.Nearest,Texture=CardCatalog.Art(kind),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new(width,height),MouseFilter=Control.MouseFilterEnum.Ignore};
        GameUi.DecorateCard(art,kind);return art;
    }
    public static VBoxContainer Panel(Node parent,int width=0)
    {
        var frame=new PanelContainer {CustomMinimumSize=new(width,0),SizeFlagsVertical=Control.SizeFlags.ExpandFill};
        frame.AddThemeStyleboxOverride("panel",MenuUi.Box(false,MobileLayout.Enabled?12:18));parent.AddChild(frame);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",10);frame.AddChild(box);return box;
    }
    public static VBoxContainer Page(Control root)
    {
        root.Theme=MenuUi.Theme();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        var background=new ColorRect {Color=MenuUi.Background,MouseFilter=Control.MouseFilterEnum.Ignore};
        root.AddChild(background);background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var edge=new ColorRect {Color=MenuUi.Line,MouseFilter=Control.MouseFilterEnum.Ignore,AnchorRight=1,OffsetBottom=1};root.AddChild(edge);
        MarginContainer margin=MobileLayout.Enabled?new SafeArea {EdgePadding=12}:new MarginContainer();root.AddChild(margin);margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(string side in new[]{"left","right","top","bottom"}) margin.AddThemeConstantOverride("margin_"+side,MobileLayout.Enabled?16:32);
        var layout=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};layout.AddThemeConstantOverride("separation",MobileLayout.Enabled?12:20);
        if(MobileLayout.Enabled){var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};margin.AddChild(scroll);scroll.AddChild(layout);layout.SizeFlagsVertical=Control.SizeFlags.ExpandFill;}
        else margin.AddChild(layout);return layout;
    }
}
