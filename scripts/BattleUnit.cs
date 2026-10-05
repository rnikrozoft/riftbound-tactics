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
    public int Attack => CharacterData.Stats(CardKind,Stars).Attack;
    public int Speed => CharacterData.Stats(CardKind,Stars).Speed;
    private readonly Sprite2D[] _stars = new Sprite2D[4];
    public void ConfigureStars(int stars, bool animate = false)
    {
        int previous = Stars; int previousMax=MaxHealth; Stars = Mathf.Clamp(stars,1,4); MaxHealth = CharacterData.Stats(CardKind,Stars).Hp;
        if (_bar != null) { if (Stars != previous || previousMax!=MaxHealth) ResetHealth(); for(int i=0;i<4;i++) _stars[i].Visible = i < Stars; UpdateStatusPosition(); }
        if (animate && Stars > previous) UpgradeEffect.Play(this, new(0,-20));
    }
    public int GridSlot { get; set; } = -1;
    public AnimatedSprite2D Sprite { get; private set; } = null!;
    public bool IsAlly { get; private set; }
    private ProgressBar _bar = null!;

    public override void _Ready()
    {
        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        CharacterVisual.Normalize(Sprite);
        IsAlly = GetMeta("team", "Ally").AsString() == "Ally";
        Sprite.FlipH = !IsAlly;
        Health = MaxHealth;
        _bar = new ProgressBar {
            Name = "HealthBar", Position = new(-13, -41), Size = new(26,4), MouseFilter = Control.MouseFilterEnum.Ignore,
            MaxValue = MaxHealth, Value = Health, ShowPercentage = false, ClipContents = true
        };
        _bar.AddThemeStyleboxOverride("background",new StyleBoxFlat {
            BgColor=new Color("3a1720"),BorderColor=new Color("160e14"),
            BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,
            ContentMarginLeft=1,ContentMarginRight=1,ContentMarginTop=1,ContentMarginBottom=1
        });
        _bar.AddThemeStyleboxOverride("fill",new StyleBoxFlat {
            BgColor=new Color("df3445"),ExpandMarginLeft=-1,ExpandMarginRight=-1,ExpandMarginTop=-1,ExpandMarginBottom=-1,
            ContentMarginLeft=0,ContentMarginRight=0,ContentMarginTop=0,ContentMarginBottom=0
        });
        AddChild(_bar);
        _bar.Size = new(26,4);
        for(int i=0;i<4;i++) { _stars[i] = new Sprite2D { Texture = TravelBookUi.Texture("IconStar01a"), Position = new(-10.5f+i*7,-51), Scale = new(.4f,.4f), Visible = i < Stars }; AddChild(_stars[i]); }
        UpdateStatusPosition();
    }

    public override void _Process(double delta) => UpdateStatusPosition();
    private void UpdateStatusPosition()
    {
        if(_bar==null)return;
        var head=CharacterVisual.HeadAnchor(Sprite);
        _bar.Position=head+new Vector2(-_bar.Size.X/2,-9.5f);
        for(int i=0;i<4;i++)
            if(_stars[i]!=null)_stars[i].Position=head+new Vector2((i-(Stars-1)/2f)*7,-19.5f);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        int previous=Health;
        Health = Mathf.Clamp(Health - Mathf.Max(0, amount), 0, MaxHealth);
        if(Health<previous)DamageNumber.Play(this,amount);
        _bar.Value = Health;
        if (IsDead) _bar.Hide();
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        if (IsDead) EmitSignal(SignalName.Died);
    }

    public void ApplyServerHealth(int health, int damage = 0)
    {
        int previous=Health;
        Health = Mathf.Clamp(health,0,MaxHealth);
        if(Health<previous && damage>0)DamageNumber.Play(this,damage);
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
