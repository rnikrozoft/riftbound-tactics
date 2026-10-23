using Godot;

public static class UpgradeEffect
{
    private static SpriteFrames? _frames;
    public static void Play(Node parent, Vector2 position)
    {
        if (_frames == null) {
            _frames = new SpriteFrames(); _frames.RemoveAnimation("default");
            Load("level", "symbols/symbol_level_up_text_001/symbol_level_up_text_001_small_blue", 43, 30);
            Load("burst", "fantasy_spells/spell_heal_001/spell_heal_001_small_red", 16, 24);
        }
        var effect = new Node2D { Name="UpgradeEffect", Position=position, ZIndex=20, TextureFilter=CanvasItem.TextureFilterEnum.Nearest };
        parent.AddChild(effect);
        var burst = new AnimatedSprite2D { SpriteFrames=_frames }; effect.AddChild(burst); burst.Play("burst");
        var text = new AnimatedSprite2D { SpriteFrames=_frames, Position=parent is BattleUnit ? new(0,30) : new(0,-25) }; effect.AddChild(text); text.Play("level");
        text.AnimationFinished += () => { if(GodotObject.IsInstanceValid(effect)) effect.QueueFree(); };
    }
    private static void Load(string animation, string folder, int count, float fps)
    {
        _frames!.AddAnimation(animation); _frames.SetAnimationLoopMode(animation,SpriteFrames.LoopMode.None); _frames.SetAnimationSpeed(animation,fps);
        for(int i=0;i<count;i++) _frames.AddFrame(animation, GD.Load<Texture2D>($"res://assets/effects/pixel/frames/{folder}/frame{i:0000}.png"));
    }
}
