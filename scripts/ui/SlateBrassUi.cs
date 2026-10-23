using Godot;
using System.Collections.Generic;

public static class SlateBrassUi
{
    private const string Root="res://assets/ui/slate_brass/";
    private static readonly Dictionary<string,Texture2D> Textures=new();
    public static Texture2D Texture(string category,string name)
    {
        string path=Root+category+"/"+name+".tres";
        if(!Textures.TryGetValue(path,out var texture))Textures[path]=texture=GD.Load<Texture2D>(path);
        return texture;
    }
    private static readonly Dictionary<Texture2D,Texture2D> UiTextures=new();
    // Normalize generated atlas resolution for logical UI pixels; source PNGs stay intact.
    public static Texture2D UiTexture(Texture2D source)
    {
        if(UiTextures.TryGetValue(source,out var cached))return cached;
        using var image=source.GetImage();
        image.Resize(Mathf.Max(1,source.GetWidth()/3),Mathf.Max(1,source.GetHeight()/3),Image.Interpolation.Nearest);
        return UiTextures[source]=ImageTexture.CreateFromImage(image);
    }
    public static Texture2D UiTexture(string category,string name)=>UiTexture(Texture(category,name));
    private static StyleBoxTexture Normalize(StyleBoxTexture original)
    {
        var box=(StyleBoxTexture)original.Duplicate();box.Texture=UiTexture(original.Texture);
        foreach(var side in new[]{Side.Left,Side.Right,Side.Top,Side.Bottom})box.SetTextureMargin(side,Mathf.Min(14,Mathf.Min(box.Texture.GetWidth(),box.Texture.GetHeight())/2f));
        return box;
    }
    private static Theme? _theme;
    public static Theme Theme()
    {
        if(_theme!=null)return _theme;
        _theme=(Theme)GD.Load<Theme>(Root+"slate_brass_theme.tres").Duplicate();
        foreach(var type in _theme.GetTypeList())foreach(var name in _theme.GetStyleboxList(type))
            if(_theme.GetStylebox(name,type) is StyleBoxTexture style)_theme.SetStylebox(name,type,Normalize(style));
        return _theme;
    }
    public static StyleBoxTexture Box(string name,int padding)
    {
        var box=Normalize(GD.Load<StyleBoxTexture>(Root+"styles/"+name+".tres"));
        box.ContentMarginLeft=box.ContentMarginRight=box.ContentMarginTop=box.ContentMarginBottom=padding;
        if(name.StartsWith("input"))box.ContentMarginLeft=box.ContentMarginRight=Mathf.Max(20,padding);
        return box;
    }
    public static void Button(TextureButton button,bool primary=false,bool selected=false)
    {
        button.Material=null;button.SelfModulate=new Color(1,1,1,0);button.IgnoreTextureSize=true;
        button.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        string family=primary?"primary":"secondary";
        button.TextureNormal=Texture("controls",selected?"tab_selected":family+"_normal");
        button.TextureHover=Texture("controls",selected?"tab_selected":family+"_hover");
        button.TexturePressed=Texture("controls",selected?"tab_selected":family+"_pressed");
        button.TextureDisabled=Texture("controls",family+"_disabled");
        button.TextureFocused=Texture("controls",primary?"primary_hover":"tab_selected");
        if(button.GetNodeOrNull<SlateBrassButtonSkin>("SlateBrassSkin")==null) {
            var skin=new SlateBrassButtonSkin {Name="SlateBrassSkin",Button=button,MouseFilter=Control.MouseFilterEnum.Ignore,TextureFilter=CanvasItem.TextureFilterEnum.Nearest,PatchMarginLeft=7,PatchMarginTop=7,PatchMarginRight=7,PatchMarginBottom=7};
            button.AddChild(skin);button.MoveChild(skin,0);skin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }
    public static Texture2D Icon(string oldName)
    {
        string? name=oldName switch {
            "IconCoin01a"=>"coin","IconRestart01a"=>"reroll","IconArrow01a"=>"upgrade",
            "IconHeart01a"=>"heart","IconStar01a"=>"star","IconLock01a"=>"lock",
            "IconClose01a"=>"close","IconHome01a"=>"chest",_=>null
        };
        return name==null?TravelBookUi.Texture(oldName):Texture("icons",name);
    }
}
