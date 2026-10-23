using Godot;

public partial class BattleUnit : CharacterBody2D
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int maximum);
    [Signal] public delegate void DiedEventHandler();
    [Export(PropertyHint.Range, "1,1000,1")] public int MaxHealth { get; set; } = 100;
    public int Health { get; private set; }
    public bool IsDead => Health == 0;
    public bool HasBattled { get; set; }
    public bool IsSummoned {get;set;}
    public int CardToken { get; set; } = -1;
    public string ServerId { get; set; } = "";
    public int CardKind { get; set; }
    public int Stars { get; private set; } = 1;
    public int Attack => Health;
    public int Armor {get;private set;}
    public int Fire {get;private set;}
    public bool Stunned {get;private set;}
    public int Poison {get;private set;}
    public bool Cursed {get;private set;}
    public bool AntiHeal {get;private set;}
    public string StatusSummary => $"{(Fire>0 ? "FIRE " : "")}{(Stunned ? "STUN " : "")}{(Poison>0 ? $"POISON {Poison} " : "")}{(Cursed ? "WEAKEN " : "")}{(AntiHeal ? "NO HEAL" : "")}".Trim();
    public int Speed => CharacterData.Stats(CardKind,Stars).Speed;
    private readonly Sprite2D[] _stars = new Sprite2D[8];
    public void ConfigureStars(int stars, bool animate = false)
    {
        int previous = Stars; int previousMax=MaxHealth; Stars = CharacterData.IsSupport(CardKind)?1:Mathf.Clamp(stars,1,4); MaxHealth = CharacterData.Stats(CardKind,Stars).Hp;
        if (_bar != null) { if (Stars != previous || previousMax!=MaxHealth) ResetHealth(); for(int i=0;i<8;i++) _stars[i].Visible = !CharacterData.IsSupport(CardKind) && i < CharacterData.DisplayStars(Stars); UpdateStatusPosition(); }
        if (animate && !CharacterData.IsSupport(CardKind) && Stars > previous) UpgradeEffect.Play(this, new(0,-20));
    }
    public int GridSlot { get; set; } = -1;
    public AnimatedSprite2D Sprite { get; private set; } = null!;
    public bool IsAlly { get; private set; }
    public bool FormationFlipH=>CharacterData.IsSupport(CardKind)?(CharacterData.Get(CardKind).Id=="healing_totem"&&IsAlly):!IsAlly;
    private ProgressBar _bar = null!;
    private Label _armorText=null!,_powerText=null!;
    private readonly Sprite2D[] _statusIcons=new Sprite2D[CombatStatuses.All.Length];
    private bool _deathPlaying;
    private float _deathFade;
    public const float DeathFadeDuration=.35f;
    private void BeginDeath()
    {
        if(SkillCardVisual.IsSkill(CardKind)){SkillCardVisual.Destroy(this);return;}
        _deathPlaying=true;_deathFade=0;
        _bar.Hide();foreach(var star in _stars)star.Hide();
        Sprite.Play(BattleAnimations.Die);Sprite.SetFrameAndProgress(0,0);
    }
    public void HideDeadImmediately(){if(!IsDead)return;_deathPlaying=false;Sprite.Stop();Hide();}
    private void RestorePresentation()
    {
        _deathPlaying=false;_deathFade=0;Modulate=new Color(Modulate.R,Modulate.G,Modulate.B,1);Show();
        Sprite.SpeedScale=1;Sprite.Play(BattleAnimations.Idle);
        for(int i=0;i<8;i++)_stars[i].Visible=i<CharacterData.DisplayStars(Stars);
    }

    public override void _Ready()
    {
        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        IsAlly = GetMeta("team", "Ally").AsString() == "Ally";
        if(CharacterData.IsSupport(CardKind)&&!SkillCardVisual.IsSkill(CardKind)&&!IsAlly)Sprite.SpriteFrames=GD.Load<SpriteFrames>(CharacterData.Get(CardKind).EnemyAnimationPath.Length>0?CharacterData.Get(CardKind).EnemyAnimationPath:"res://assets/cards/special/healing_totem/healing_totem_enemy_frames.tres");
        Sprite.SpriteFrames=CharacterCombatAnimations.Load(CardKind,Sprite.SpriteFrames);
        CharacterVisual.Normalize(Sprite);
        // Share a layer so arena Y sorting draws the nearest unit in front.
        // Stay above the arena pillars (layers 2–4).
        if(CharacterData.IsTrap(CardKind)&&CharacterData.SupportEffectFor(CardKind) is not ("ward" or "seal"))Sprite.Scale/=1.65f;
        ZIndex=5;
        IsAlly = GetMeta("team", "Ally").AsString() == "Ally";
        Sprite.FlipH = FormationFlipH;
        if(CharacterData.IsSupport(CardKind)){
            var outline=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/character_outline.gdshader")};
            outline.SetShaderParameter("outline_color",Colors.White);
            outline.SetShaderParameter("outline_width",Mathf.Clamp(1.25f/Sprite.Scale.X,1,24));
            Sprite.Material=outline;
        }
        SkillCardVisual.Attach(this);
        Health = MaxHealth;
        Armor=CharacterData.Stats(CardKind,Stars).Armor;
        _bar = new PixelHealthBar {
            Name = "HealthBar", Position = new(-13, -41), Size = new(26,4), MouseFilter = Control.MouseFilterEnum.Ignore,
            MaxValue = MaxHealth, Value = Health, ShowPercentage = false, ClipContents = true
        };
        AddChild(_bar);
        _armorText=StatLabel("ArmorValue",new Color("59c9ff"));
        _powerText=StatLabel("PowerValue",new Color("ff5262"));
        for(int i=0;i<_statusIcons.Length;i++){
            var status=CombatStatuses.All[i];
            _statusIcons[i]=new Sprite2D {Name="Status_"+status.Key,Texture=CombatStatuses.Texture(status),Scale=new(.625f,.625f),ZIndex=30,Visible=false,TextureFilter=TextureFilterEnum.Nearest};AddChild(_statusIcons[i]);
        }
        _bar.Size = new(34,5);
        for(int i=0;i<8;i++) { _stars[i] = new Sprite2D {Name="Star"+(i+1),Texture = TravelBookUi.Texture("IconStar01a"),Scale = new(.65f,.65f),Visible = i < CharacterData.DisplayStars(Stars),ZIndex=30}; AddChild(_stars[i]); }
        UpdateStatusPosition();
        UpdateNumbers();
        Sprite.Play(BattleAnimations.Idle);
    }
    private Label StatLabel(string name,Color color)
    {
        var label=new Label {Name=name,Size=new(24,14),MouseFilter=Control.MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center,ZIndex=30};
        label.AddThemeFontSizeOverride("font_size",11);label.AddThemeColorOverride("font_color",color);label.AddThemeColorOverride("font_outline_color",new Color("10131c"));label.AddThemeConstantOverride("outline_size",3);AddChild(label);return label;
    }
    private void UpdateNumbers(){if(_armorText==null)return;if(CharacterData.IsSupport(CardKind)){_bar.Hide();_armorText.Hide();_powerText.Hide();foreach(var star in _stars)star?.Hide();return;}_armorText.Text=Armor.ToString();_powerText.Text=Health.ToString();_armorText.Visible=_powerText.Visible=!IsDead;for(int i=0;i<_statusIcons.Length;i++)_statusIcons[i].Visible=CombatStatuses.Active(this,CombatStatuses.All[i].Key);UpdateStatusPosition();}

    public override void _Process(double delta)
    {
        if(_statusIcons[0]!=null&&_statusIcons[0].Visible)_statusIcons[0].Texture=CombatStatuses.Texture(CombatStatuses.All[0]);
        UpdateStatusPosition();
        if(!_deathPlaying||!IsDead)return;
        if(Sprite.Animation==BattleAnimations.Die&&Sprite.IsPlaying())return;
        _deathFade+=(float)delta;
        Modulate=new Color(Modulate.R,Modulate.G,Modulate.B,Mathf.Max(0,1-_deathFade/DeathFadeDuration));
        if(_deathFade>=DeathFadeDuration){_deathPlaying=false;Hide();}
    }
    private void UpdateStatusPosition()
    {
        if(_bar==null)return;
        var head=CharacterVisual.HeadAnchor(Sprite);
        var feet=CharacterVisual.GroundAnchor(Sprite);
        // Follow the visible figure's flipped offset; keep the numbers' left/right roles.
        _armorText.Position=feet+new Vector2(-29,-13);_powerText.Position=feet+new Vector2(6,-13);
        _bar.Position=head+new Vector2(-_bar.Size.X/2,-9.5f);
        for(int i=0;i<8;i++)
            if(_stars[i]!=null)_stars[i].Position=feet+new Vector2((i-(CharacterData.DisplayStars(Stars)-1)/2f)*7,7);
        bool burning=_statusIcons[0]!=null&&_statusIcons[0].Visible;
        if(burning)_statusIcons[0].Position=head+new Vector2(0,-22);
        int count=0;for(int i=1;i<_statusIcons.Length;i++)if(_statusIcons[i]!=null&&_statusIcons[i].Visible)count++;
        int index=0;for(int i=1;i<_statusIcons.Length;i++){
            var icon=_statusIcons[i];if(icon==null||!icon.Visible)continue;
            int row=index/3,columns=Mathf.Min(3,count-row*3);
            icon.Position=head+new Vector2((index%3-(columns-1)/2f)*20,-22-(burning?20:0)-row*20);index++;
        }
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        int previous=Health;
        int absorbed=Mathf.Min(Armor,Mathf.Max(0,amount));Armor-=absorbed;
        Health = Mathf.Clamp(Health - Mathf.Max(0, amount-absorbed), 0, MaxHealth);
        if(Health<previous)DamageNumber.Play(this,amount);
        _bar.Value = Health;
        UpdateNumbers();
        if (IsDead) _bar.Hide();
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        if (IsDead) {BeginDeath();EmitSignal(SignalName.Died);}
    }

    public void ApplyServerHealth(int health, int damage = 0)
    {
        int previous=Health;
        Health = Mathf.Clamp(health,0,MaxHealth);
        if(Health<previous && damage>0)DamageNumber.Play(this,damage);
        _bar.Value = Health; _bar.Visible = !IsDead;
        UpdateNumbers();
        EmitSignal(SignalName.HealthChanged,Health,MaxHealth);
        if(previous>0&&IsDead){BeginDeath();EmitSignal(SignalName.Died);}
        if(previous==0&&!IsDead)RestorePresentation();
    }
    public void ApplyCombatState(OnlineCombatChange state,int damage=0)
    {
        bool wasDead=IsDead;int oldHp=Health,oldArmor=Armor;
        Armor=Mathf.Max(0,state.Armor);Fire=state.Fire;Stunned=state.Stun;Poison=state.Poison;Cursed=state.Curse;AntiHeal=state.AntiHeal;
        Health=Mathf.Clamp(state.Hp,0,MaxHealth);GridSlot=state.Slot;
        if(damage>0&&(Health<oldHp||Armor<oldArmor))DamageNumber.Play(this,damage);
        _bar.MaxValue=MaxHealth;_bar.Value=Health;_bar.Visible=!IsDead;UpdateNumbers();EmitSignal(SignalName.HealthChanged,Health,MaxHealth);
        if(!wasDead&&IsDead){if(CharacterData.IsSupport(CardKind)){Sprite.Stop();if(SkillCardVisual.IsSkill(CardKind))SkillCardVisual.Destroy(this);else Hide();}else BeginDeath();EmitSignal(SignalName.Died);}
        if(wasDead&&!IsDead)RestorePresentation();
    }

    public void ResetHealth()
    {
        Health = MaxHealth;
        Armor=CharacterData.Stats(CardKind,Stars).Armor;Fire=Poison=0;Stunned=Cursed=AntiHeal=false;
        _bar.MaxValue = MaxHealth;
        _bar.Value = Health;
        _bar.Show();
        RestorePresentation();
        UpdateNumbers();
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }
}
