using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public partial class DamageSimulator : Node
{
    public sealed class EffectEntry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("texture")] public string Texture { get; set; } = "";
        [JsonPropertyName("frames")] public int[][] Frames { get; set; } = Array.Empty<int[]>();
    }
    public EffectEntry[] Catalog { get; private set; } = Array.Empty<EffectEntry>();
    public int SelectedIndex { get; private set; } = -1;
    public HashSet<string> Favorites { get; } = new();
    public bool Busy { get; private set; }
    private const string ChoicesPath = "user://effect_choices.json";
    private readonly List<string> _categories = new() { "Impacts" };
    private readonly Dictionary<int,SpriteFrames> _cache = new();
    private readonly RandomNumberGenerator _rng = new();
    private int _categoryIndex;
    private VBoxContainer _list = null!;
    private Label _title = null!, _status = null!;
    private TextureButton _categoryButton = null!, _keepButton = null!;
    private BattleUnit _target = null!;
    private AnimatedSprite2D _sprite = null!, _effect = null!;
    private BattleDisplay _field = null!;
    private SceneTree _tree = null!;
    private bool _loop, _hitstop = true, _shake = true, _exiting;
    private float _shakeLeft, _loopElapsed;

    public override void _Ready()
    {
        _tree = GetTree(); _rng.Randomize();
        Catalog = JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray) ?? Array.Empty<EffectEntry>();
        foreach (var entry in Catalog) if (!_categories.Contains(entry.Category)) _categories.Add(entry.Category);
        if (FileAccess.FileExists(ChoicesPath))
        {
            try {
                var saved = JsonSerializer.Deserialize(FileAccess.GetFileAsString(ChoicesPath),GameJsonContext.Default.StringArray);
                if (saved != null) foreach (string path in saved) Favorites.Add(path);
            } catch (JsonException) { GD.PushWarning("Ignoring invalid saved effect choices."); }
        }
        var field = GetNode<Node2D>("Battlefield");
        field.GetNode<BattleDemo>("BattleDemo").SetProcess(false);
        field.GetNode<Control>("UI/SafeArea/Content/ProfileA").Hide();
        field.GetNode<Control>("UI/SafeArea/Content/ProfileB").Hide();
        field.GetNode<CardShop>("UI/SafeArea/Content/CardShop").Hide();
        foreach (var child in field.GetChildren()) if (child is BattleUnit unit) unit.Visible = unit.Name == "Enemy_02";
        _target = field.GetNode<BattleUnit>("Enemy_02"); _target.Position = new(480,408);
        _sprite = _target.Sprite; _field = (BattleDisplay)field;
        _effect = new AnimatedSprite2D { Name = "DamageEffect", TextureFilter = CanvasItem.TextureFilterEnum.Nearest, ZIndex = 10, Position = new(0,-18), Visible = false };
        _target.AddChild(_effect);
        BuildUi(); RebuildList(); SetProcess(false);
    }
    public override void _Process(double delta)
    {
        _shakeLeft = Mathf.Max(0,_shakeLeft - (float)delta);
        _field.ArenaOffset = _shakeLeft > 0 ? new Vector2(_rng.RandfRange(-2.5f,2.5f),_rng.RandfRange(-2.5f,2.5f)) * -_field.Scale * _shakeLeft / .2f : Vector2.Zero;
        if (_loop)
        {
            _loopElapsed += (float)delta;
            if (_loopElapsed >= 1.4f && !Busy) { _loopElapsed = 0; PlayDamage(); }
        }
        if (!_loop && _shakeLeft <= 0) SetProcess(false);
    }
    private TextureButton Button(string text, Action action, float width = 310)
    {
        var button = new TextureButton {
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale, CustomMinimumSize = new(width,36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        TravelBookUi.StyleButton(button);
        var label = new Label { Name = "Text", Text = text, Modulate = Colors.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        GameUi.Label(label,14);button.AddChild(label); label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.Pressed += action; return button;
    }
    private void BuildUi()
    {
        var content = GetNode<Control>("LabUI/SafeArea/Content");
        var sidebar = new VBoxContainer();
        content.AddChild(sidebar); sidebar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide); sidebar.OffsetLeft=16;sidebar.OffsetTop=16;sidebar.OffsetBottom=-16;sidebar.OffsetRight = 344;
        var leftSurface=new Panel {AnchorBottom=1,OffsetLeft=8,OffsetTop=8,OffsetRight=352,OffsetBottom=-8,MouseFilter=Control.MouseFilterEnum.Ignore};leftSurface.AddThemeStyleboxOverride("panel",GameUi.Box());content.AddChild(leftSurface);content.MoveChild(leftSurface,0);
        sidebar.AddChild(DeckMenuUi.Text($"EFFECTS LIBRARY / {Catalog.Length}",16));
        _categoryButton = Button("CATEGORY: Impacts",NextCategory); sidebar.AddChild(_categoryButton);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sidebar.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_list);
        var controls = new VBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -344, OffsetRight=-16, OffsetTop=16, OffsetBottom = 380 };
        var rightSurface=new Panel {AnchorLeft=1,AnchorRight=1,OffsetLeft=-352,OffsetRight=-8,OffsetTop=8,OffsetBottom=460,MouseFilter=Control.MouseFilterEnum.Ignore};rightSurface.AddThemeStyleboxOverride("panel",GameUi.Box());content.AddChild(rightSurface);
        content.AddChild(controls);
        _title = new Label { CustomMinimumSize = new(310,50), AutowrapMode = TextServer.AutowrapMode.WordSmart }; GameUi.Label(_title,16);controls.AddChild(_title);
        controls.AddChild(Button("PLAY DAMAGE (-25 HP)",PlayDamage));
        controls.AddChild(Button("LOOP: OFF",() => { _loop = !_loop; SetToggle(controls,2,"LOOP",_loop); SetProcess(_loop || _shakeLeft > 0); }));
        controls.AddChild(Button("HITSTOP: ON",() => { _hitstop = !_hitstop; SetToggle(controls,3,"HITSTOP",_hitstop); }));
        controls.AddChild(Button("CAMERA SHAKE: ON",() => { _shake = !_shake; SetToggle(controls,4,"CAMERA SHAKE",_shake); }));
        controls.AddChild(Button("RESET HP",ResetTarget));
        _keepButton = Button("KEEP THIS EFFECT",ToggleFavorite); controls.AddChild(_keepButton);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; GameUi.Label(_status,13,true);controls.AddChild(_status);
        controls.AddChild(Button("CHARACTER ANIMATIONS",()=>_tree.ChangeSceneToFile("res://scenes/character_animation_lab.tscn")));
        controls.AddChild(Button("BACK TO LOBBY",() => _tree.ChangeSceneToFile("res://scenes/lobby.tscn")));
    }
    private static void SetToggle(VBoxContainer controls, int index, string title, bool enabled) =>
        controls.GetChild(index).GetNode<Label>("Text").Text = $"{title}: {(enabled ? "ON" : "OFF")}";
    private void NextCategory()
    {
        if (Busy) return;
        _categoryIndex = (_categoryIndex + 1) % _categories.Count;
        _categoryButton.GetNode<Label>("Text").Text = "CATEGORY: " + _categories[_categoryIndex]; RebuildList();
    }
    public void RebuildList()
    {
        foreach (var child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        int first = -1;
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Category != _categories[_categoryIndex]) continue;
            if (first < 0) first = i;
            int index = i;
            var button = Button(Catalog[i].Name.Replace('_',' '),() => { if (!Busy) { Select(index); PlayDamage(); } });
            button.GetNode<Label>("Text").AddThemeFontSizeOverride("font_size",12);
            button.TooltipText = Catalog[i].Name; _list.AddChild(button);
        }
        if (first >= 0) Select(first);
    }
    public void Select(int index)
    {
        if (index < 0 || index >= Catalog.Length || Busy) return;
        SelectedIndex = index;
        _title.Text = Catalog[index].Name + "\n15 FPS / " + Catalog[index].Category;
        _keepButton.GetNode<Label>("Text").Text = Favorites.Contains(Catalog[index].Texture) ? "REMOVE FROM PICKS" : "KEEP THIS EFFECT";
        _status.Text = $"{Favorites.Count} effects kept";
    }
    public SpriteFrames FramesFor(int index)
    {
        if (_cache.TryGetValue(index,out var cached)) return cached;
        var entry = Catalog[index];
        var frames = new SpriteFrames();
        frames.AddAnimation(BattleAnimations.Effect); frames.SetAnimationLoopMode(BattleAnimations.Effect,SpriteFrames.LoopMode.None); frames.SetAnimationSpeed(BattleAnimations.Effect,15);
        var texture = GD.Load<Texture2D>(entry.Texture);
        foreach (var rect in entry.Frames) frames.AddFrame(BattleAnimations.Effect,new AtlasTexture { Atlas = texture, Region = new(rect[0],rect[1],rect[2],rect[3]) });
        _cache.Add(index,frames); return frames;
    }
    private void CheckAlive() { if (_exiting) throw new OperationCanceledException(); }
    public async void PlayDamage()
    {
        if (Busy || SelectedIndex < 0) return;
        Busy = true;
        try
        {
            if (_target.IsDead) { _target.ResetHealth(); _sprite.Play(BattleAnimations.Idle); }
            _target.TakeDamage(25); _sprite.Play(_target.IsDead ? BattleAnimations.Die : BattleAnimations.Hit); _sprite.SetFrameAndProgress(0,0);
            _effect.SpriteFrames = FramesFor(SelectedIndex); _effect.Show(); _effect.Play(BattleAnimations.Effect); _effect.SetFrameAndProgress(0,0);
            if (_shake) { _shakeLeft = .2f; SetProcess(true); }
            if (_hitstop)
            {
                _sprite.SpeedScale = _effect.SpeedScale = 0;
                await ToSignal(_tree.CreateTimer(.075,true,false,true),SceneTreeTimer.SignalName.Timeout); CheckAlive();
                _sprite.SpeedScale = _effect.SpeedScale = 1;
            }
            while (_effect.IsPlaying() || _sprite.IsPlaying())
            { await ToSignal(_tree,SceneTree.SignalName.ProcessFrame); CheckAlive(); }
            _effect.Hide(); if (!_target.IsDead) _sprite.Play(BattleAnimations.Idle);
            _status.Text = $"HP {_target.Health} / {_target.MaxHealth} | {Favorites.Count} effects kept";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { GD.PushError(exception.ToString()); }
        finally { Busy = false; }
    }
    public void ResetTarget() { if (!Busy) { _target.ResetHealth(); _sprite.Play(BattleAnimations.Idle); } }
    private void ToggleFavorite()
    {
        if (SelectedIndex < 0) return;
        string path = Catalog[SelectedIndex].Texture;
        if (!Favorites.Remove(path)) Favorites.Add(path);
        using var file = FileAccess.Open(ChoicesPath,FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Favorites,GameJsonContext.Default.HashSetString));
        // Allow updating selection while an effect preview is playing.
        _keepButton.GetNode<Label>("Text").Text = Favorites.Contains(path) ? "REMOVE FROM PICKS" : "KEEP THIS EFFECT";
        _status.Text = $"{Favorites.Count} effects kept";
    }
    public override void _ExitTree()
    {
        _exiting = true;
        if (GodotObject.IsInstanceValid(_field)) _field.ArenaOffset = Vector2.Zero;
        if (GodotObject.IsInstanceValid(_sprite)) _sprite.SpeedScale = 1;
        if (GodotObject.IsInstanceValid(_effect)) _effect.SpeedScale = 1;
    }
}

