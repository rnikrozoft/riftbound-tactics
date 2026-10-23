using Godot;

// Scenery is a local presentation preference; match rules and server slots stay unchanged.
public static class BattleAreas
{
    public const string UndeadScenery="res://scenes/areas/undead_hollow.tscn";
    public static string ScenePath {get {GamePreferences.Load();return GamePreferences.BattleArea=="undead_hollow"?"res://scenes/undead_battle.tscn":"res://scenes/main.tscn";}}
    public static string Name=>GamePreferences.BattleArea=="undead_hollow"?GamePreferences.Text("Undead Hollow","หุบเขาอันเดด"):GamePreferences.Text("Living Garden","สวนมีชีวิต");
}
