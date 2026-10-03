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
    public int Stars { get; private set; } = 1;
    public int Attack => 30 * Stars;
    public int Speed => 10 + 2 * (Stars - 1);
    private readonly Sprite2D[] _stars = new Sprite2D[4];
    public void ConfigureStars(int stars, bool animate = false)
    {
        int previous = Stars; Stars = Mathf.Clamp(stars,1,4); MaxHealth = 100 * Stars;
        if (_bar != null) { if (Stars != previous) ResetHealth(); for(int i=0;i<4;i++) _stars[i].Visible = i < Stars; }
        if (animate && Stars > previous) UpgradeEffect.Play(this, new(0,-20));
    }
    public int GridSlot { get; set; } = -1;
    public AnimatedSprite2D Sprite { get; private set; } = null!;
    public bool IsAlly { get; private set; }
    private TextureProgressBar _bar = null!;

    public override void _Ready()
    {
        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        IsAlly = GetMeta("team", "Ally").AsString() == "Ally";
        Health = MaxHealth;
        _bar = new TextureProgressBar {
            Name = "HealthBar", Position = new(-16, -43), MouseFilter = Control.MouseFilterEnum.Ignore,
            MaxValue = MaxHealth, Value = Health, TextureUnder = TravelBookUi.Texture("Bar01a"),
            TextureProgress = TravelBookUi.Texture(IsAlly ? "Fill01a" : "Fill01b"),
            NinePatchStretch = true, StretchMarginLeft = 1, StretchMarginRight = 1,
            TextureProgressOffset = new(1, 1)
        };
        AddChild(_bar);
        _bar.Size = new(64, 6);
        _bar.Scale = new(.5f, 1);
        for(int i=0;i<4;i++) { _stars[i] = new Sprite2D { Texture = TravelBookUi.Texture("IconStar01a"), Position = new(-10.5f+i*7,-51), Scale = new(.4f,.4f), Visible = i < Stars }; AddChild(_stars[i]); }
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
