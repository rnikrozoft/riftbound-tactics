using Godot;
using System;
using System.Collections.Generic;

// Pack sprites are visual only: authoritative hits retain their original frame and damage.
public partial class CharacterProjectiles : Node2D
{
    public const string Pack="res://assets/characters/packs/tiny_rpg_01/";
    private static readonly Dictionary<string,SpriteFrames> Cache=new();
    private BattleUnit _source=null!,_target=null!;
    private string _animation="";
    private int _impact;
    private AnimatedSprite2D _visual=null!;
    public static string? ArrowFor(int kind,string animation)
    {
        string slug=CombatVisualSettings.Slug(kind);
        return (slug,animation) switch {
            ("archer","attack01")=>"Arrow01(32x32).png",
            ("archer","attack02")=>"Arrow02(32x32).png",
            ("skeleton_archer","attack01")=>"Arrow03(32x32).png",
            ("demon_b","attack01")=>"Arrow03(32x32).png",
            _=>null
        };
    }
    public static string? MagicFor(int kind,string animation,string action="damage")
    {
        string slug=CombatVisualSettings.Slug(kind);
        return (slug,animation,action) switch {
            ("priest",_,"heal")=>"Priest_Heal_effect.png",
            ("priest","attack02",_)=>"Priest_Heal_effect.png",
            ("priest","attack01",_)=>"Priest_Attack_effect.png",
            ("wizard","attack01",_)=>"Wizard_Attack01_Effect.png",
            ("wizard","attack02",_)=>"Wizard_Attack02_Effect.png",
            ("necromancer",_,"summon")=>"Necromancer_Sumon_Effect.png",
            ("necromancer","summon",_)=>"Necromancer_Sumon_Effect.png",
            ("necromancer","attack02",_)=>"Necromancer_Attack02_Effect.png",
            _=>null
        };
    }
    private static SpriteFrames Frames(string path,int cell)
    {
        if(Cache.TryGetValue(path,out var cached))return cached;
        var texture=GD.Load<Texture2D>(path);
        var frames=new SpriteFrames();frames.RemoveAnimation("default");frames.AddAnimation("effect");
        frames.SetAnimationLoopMode("effect",SpriteFrames.LoopMode.None);frames.SetAnimationSpeed("effect",12);
        for(int x=0;x<texture.GetWidth();x+=cell)frames.AddFrame("effect",new AtlasTexture {Atlas=texture,Region=new Rect2(x,0,cell,cell)});
        Cache[path]=frames;return frames;
    }
    private static Vector2 Body(BattleUnit unit)=>unit.GlobalPosition+(CharacterVisual.GroundAnchor(unit.Sprite)+CharacterVisual.HeadAnchor(unit.Sprite))*.5f;
    public static CharacterProjectiles? Begin(BattleUnit source,BattleUnit target,string original,string display,int impact)
    {
        string? arrow=ArrowFor(source.CardKind,original);
        bool fireball=CombatVisualSettings.Slug(source.CardKind)=="wizard"&&original=="attack02";
        if(arrow==null&&!fireball)return null;
        var node=new CharacterProjectiles {_source=source,_target=target,_animation=display,_impact=Math.Max(1,impact),ZIndex=9};
        source.GetParent().AddChild(node);
        string path=Pack+(arrow!=null?"Arrow(Projectile)/"+arrow:"Magic(Projectile)/Wizard_Attack02_Effect.png");
        node._visual=new AnimatedSprite2D {SpriteFrames=Frames(path,arrow!=null?32:100),TextureFilter=TextureFilterEnum.Nearest,Scale=Vector2.One*(arrow!=null?1.3f:source.Sprite.Scale.X)};
        node.AddChild(node._visual);node._visual.Animation="effect";
        node.SetMeta("projectile_asset",path);node.UpdatePosition();return node;
    }
    private void UpdatePosition()
    {
        float frame=_source.Sprite.Frame+_source.Sprite.FrameProgress;
        float progress=Mathf.Clamp((frame-Math.Max(0,_impact-2))/Math.Min(2,_impact),0,1);
        _visual.Visible=frame>=Math.Max(0,_impact-2);
        GlobalPosition=Body(_source).Lerp(Body(_target),progress);
        Rotation=(Body(_target)-Body(_source)).Angle();
        // The supplied fireball has its own impact frames; flight uses its first three.
        _visual.Frame=Math.Min(_visual.SpriteFrames.GetFrameCount("effect")-1,(int)(progress*2));
    }
    public override void _Process(double delta)
    {
        if(!GodotObject.IsInstanceValid(_source)||!GodotObject.IsInstanceValid(_target)||_source.IsQueuedForDeletion()||_target.IsQueuedForDeletion()||_source.IsDead||_source.Sprite.Animation.ToString()!=_animation||!_source.Sprite.IsPlaying()||_source.Sprite.Frame>=_impact){QueueFree();return;}
        UpdatePosition();
    }
    public static AnimatedSprite2D? Impact(BattleUnit source,BattleUnit target,string animation,string action)
    {
        if(action is "splash" or "armor" or "poison")return null;
        string? file=MagicFor(source.CardKind,animation,action);if(file==null)return null;
        var visual=new AnimatedSprite2D {SpriteFrames=Frames(Pack+"Magic(Projectile)/"+file,100),Scale=Vector2.One*source.Sprite.Scale.X,ZIndex=10,TextureFilter=TextureFilterEnum.Nearest};
        target.GetParent().AddChild(visual);
        visual.GlobalPosition=file.Contains("Heal")||file.Contains("Sumon")?target.GlobalPosition+CharacterVisual.GroundAnchor(target.Sprite):Body(target);
        visual.SetMeta("projectile_asset",Pack+"Magic(Projectile)/"+file);
        visual.AnimationFinished+=visual.QueueFree;visual.Play("effect");return visual;
    }
}
