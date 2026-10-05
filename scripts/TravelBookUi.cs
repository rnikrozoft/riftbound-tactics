using Godot;

public static class TravelBookUi
{
    private const string Root = "res://assets/ui/travel_book/sprites/";
    public static Texture2D Texture(string name) => GD.Load<Texture2D>(Root + "UI_TravelBook_" + name + ".png");
    public static void StyleButton(TextureButton button)
    {
        button.SelfModulate=Colors.White;
        var skin=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/ui_button.gdshader")};button.Material=skin;
        skin.SetShaderParameter("hover_amount",0f);
        button.TextureNormal = Texture("FrameSelect01a");
        button.TextureHover = Texture("FrameSelect01b");
        button.TexturePressed = Texture("Frame01a");
        button.TextureDisabled = Texture("Frame01a");
        Tween? transition=null;
        float hover=0f;
        void Hover(float amount) {
            transition?.Kill();transition=button.CreateTween();
            transition.TweenMethod(Callable.From<float>(value=>{hover=value;skin.SetShaderParameter("hover_amount",value);}),hover,amount,.14);
        }
        button.MouseEntered += () => {if(!button.Disabled)Hover(1f);};
        button.MouseExited += () => Hover(0f);
        button.FocusEntered += () => Hover(1f);
        button.FocusExited += () => Hover(0f);
    }
}
