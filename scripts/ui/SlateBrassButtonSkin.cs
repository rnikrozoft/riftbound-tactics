using Godot;

// Keep generated corners at native UI scale instead of stretching a whole button image.
public partial class SlateBrassButtonSkin : NinePatchRect
{
    public TextureButton Button {get;set;}=null!;
    private Texture2D? _last;
    public override void _Process(double delta)
    {
        var source=Button.Disabled?Button.TextureDisabled:Button.IsPressed()?Button.TexturePressed:
            Button.IsHovered()?Button.TextureHover:Button.HasFocus()?Button.TextureFocused:Button.TextureNormal;
        if(source==null||source==_last)return;
        _last=source;Texture=SlateBrassUi.UiTexture(source);
    }
}
