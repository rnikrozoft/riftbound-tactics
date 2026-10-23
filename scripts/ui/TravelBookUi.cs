using Godot;

public static class TravelBookUi
{
    private const string Root = "res://assets/ui/travel_book/sprites/";
    public static Texture2D Texture(string name) => GD.Load<Texture2D>(Root + "UI_TravelBook_" + name + ".png");
    public static void StyleButton(TextureButton button)=>MenuUi.Button(button);
}
