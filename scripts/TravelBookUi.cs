using Godot;

public static class TravelBookUi
{
    private const string Root = "res://assets/01_TravelBookLite/Sprites/";
    public static Texture2D Texture(string name) => GD.Load<Texture2D>(Root + "UI_TravelBook_" + name + ".png");
    public static void StyleButton(TextureButton button)
    {
        button.TextureNormal = Texture("FrameSelect01a");
        button.TextureHover = Texture("FrameSelect01b");
        button.TexturePressed = Texture("Frame01a");
        button.TextureDisabled = Texture("Frame01a");
    }
}