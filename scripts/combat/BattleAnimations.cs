using Godot;

// Intern animation identifiers once instead of converting strings in every attack.
public static class BattleAnimations
{
    public static readonly StringName Idle = new("idle");
    public static readonly StringName Walk = new("walk");
    public static readonly StringName Attack = new("attack");
    public static readonly StringName Hit = new("hit");
    public static readonly StringName Die = new("die");
    public static readonly StringName Effect = new("effect");
}
