using Godot;

public partial class BattleUnit : CharacterBody2D
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
    [Signal] public delegate void DiedEventHandler();
    [Export(PropertyHint.Range, "1,1000,1")] public int MaxHealth { get; set; } = 100;
    public int Health { get; private set; }
    public bool IsDead => Health == 0;
    public bool HasBattled { get; set; }
    public int CardToken { get; set; } = -1;
    public string ServerId { get; set; } = "";
    public int CardKind { get; set; }
    public int GridSlot { get; set; } = -1;
    public AnimatedSprite2D Sprite { get; private set; } = null!;
    public bool IsAlly { get; private set; }
    private TextureProgressBar _bar = null!;
    private const string Ui = "res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/";

    public override void _Ready()
    {
        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        IsAlly = GetMeta("team", "Ally").AsString() == "Ally";
        Health = MaxHealth;
        _bar = new TextureProgressBar {
            Name = "HealthBar", Position = new(-16, -43), MouseFilter = Control.MouseFilterEnum.Ignore,
            MaxValue = MaxHealth, Value = Health, TextureUnder = GD.Load<Texture2D>(Ui + "UI_Flat_Bar05a.png"),
            TextureProgress = new AtlasTexture {
                Atlas = GD.Load<Texture2D>(Ui + (IsAlly ? "UI_Flat_BarFill01f.png" : "UI_Flat_BarFill01c.png")),
                Region = new Rect2(0, 0, 28, 3)
            }, TextureProgressOffset = new(2, 3)
        };
        AddChild(_bar);
        _bar.Size = new(32, 10);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        Health = Mathf.Clamp(Health - Mathf.Max(0, amount), 0, MaxHealth);
        _bar.Value = Health;
        if (IsDead) _bar.Hide();
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        if (IsDead) EmitSignal(SignalName.Died);
    }

    public void ApplyServerHealth(int health)
    {
        Health = Mathf.Clamp(health,0,MaxHealth);
        _bar.Value = Health; _bar.Visible = !IsDead;
        EmitSignal(SignalName.HealthChanged,Health,MaxHealth);
        if (IsDead) EmitSignal(SignalName.Died);
    }

    public void ResetHealth()
    {
        Health = MaxHealth;
        _bar.MaxValue = MaxHealth;
        _bar.Value = Health;
        _bar.Show();
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }
}
