using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static class CharacterImpactEffects
{
    // Ordered by attack01, attack02, attack03. Codes keep the per-character choices readable.
    private static readonly Dictionary<string,string[]> Attacks=new()
    {
        ["archer"]=new[]{"D1s","D1l"},
        ["armored_axeman"]=new[]{"D2s","S1s","S1l"},
        ["armored_orc"]=new[]{"S1s","S1l","D2l"},
        ["armored_skeleton"]=new[]{"S1s","S1l"},
        ["knight"]=new[]{"S1s","S1l","S6l"},
        ["knight_templar"]=new[]{"S1s","S2l","S6l"},
        ["knight_vanguard"]=new[]{"S1s","S1s","S6l"},
        ["black_knight_a"]=new[]{"S1s","S3l","S6l"},
        ["black_knight_b"]=new[]{"S1s","S1l","S6l"},
        ["black_knight_c"]=new[]{"D1s","D1l","S6l"},
        ["soldier"]=new[]{"S1s","S1l","S6l"},
        ["swordsman"]=new[]{"S1s","S1s","S1s"},
        ["lancer"]=new[]{"D1s","D1l","S6l"},
        ["greatsword_skeleton"]=new[]{"S1s","S1l","D2l"},
        ["skeleton"]=new[]{"S1s","S1l"},
        ["skeleton_archer"]=new[]{"D1s"},
        ["orc"]=new[]{"S1s","S1l"},
        ["elite_orc"]=new[]{"S1s","S6l","D2l"},
        ["orc_rider"]=new[]{"D1s","S1l","D2l"},
        ["minotaur"]=new[]{"D2s","S6l","D2l"},
        ["werebear"]=new[]{"S1s","S6l","D2l"},
        ["werewolf"]=new[]{"S1s","S1s"},
        ["hellhound"]=new[]{"S1s","S3l"},
        ["hellbat"]=new[]{"D3s","D3l"},
        ["eyeball_monster"]=new[]{"D3s","S2l","S4l"},
        ["slime"]=new[]{"S2s","S2l"},
        ["lava_slime"]=new[]{"D4s","D4l"},
        ["flame_golem"]=new[]{"D4s","S3l","D4l"},
        ["ghostfire"]=new[]{"D4s","S3l"},
        ["blood_monster"]=new[]{"S1s","S4l"},
        ["blood_monster_b"]=new[]{"D3s","D3l"},
        ["demon"]=new[]{"S1s","S3l"},
        ["demon_b"]=new[]{"S1s","D3l"},
        ["demon_c"]=new[]{"D4l","D3s"},
        ["demon_d"]=new[]{"S6l","S1l","D2s"},
        ["demon_e"]=new[]{"S1s","S1s","S6l"},
        ["demoness_a"]=new[]{"D3s","D3l","S4l"},
        ["demoness_b"]=new[]{"D3s","D3l"},
        ["priest"]=new[]{"S2s"},
        ["warlock"]=new[]{"D3s","D3l"},
        ["wizard"]=new[]{"S2s","S2l"}
    };
    private sealed record Visual(SpriteFrames Frames,Vector2 Center,Vector2 Foot,float Scale);
    private static readonly Dictionary<string,Visual> Cache=new();
    private static Dictionary<string,EffectEntry>? _catalog;
    public static string Resolve(int kind,string animation)
    {
        var choice=CombatVisualSettings.Find(kind,animation);
        return choice!=null?choice.Effect:DefaultEffect(kind,animation);
    }
    public static string DefaultEffect(int kind,string animation)
    {
        string slug=CharacterData.Get(kind).ScenePath.GetFile().GetBaseName();
        int number=int.TryParse(animation.Replace("attack",""),out int parsed)?parsed:1;
        string code=Attacks.TryGetValue(slug,out var choices)&&number>0&&number<=choices.Length?choices[number-1]:"S1s";
        bool directional=code[0]=='D';int variant=code[1]-'0';
        string color=directional?variant switch {1=>"blue",2=>"white",3=>"violet",_=>"yellow"}:variant==2?"blue":"yellow";
        return $"{(directional?"directional":"symmetrical")}_impact_{variant:000}_{(code[2]=='s'?"small":"large")}_{color}";
    }
    private static Visual Load(string name)
    {
        if(Cache.TryGetValue(name,out var visual))return visual;
        _catalog??=(JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray)??Array.Empty<EffectEntry>()).ToDictionary(e=>e.Name);
        var entry=_catalog[name];var texture=GD.Load<Texture2D>(entry.Texture);
        var frames=new SpriteFrames();frames.RemoveAnimation("default");frames.AddAnimation(BattleAnimations.Effect);
        frames.SetAnimationLoopMode(BattleAnimations.Effect,SpriteFrames.LoopMode.None);frames.SetAnimationSpeed(BattleAnimations.Effect,15);
        Rect2 bounds=default;bool hasBounds=false;
        foreach(var rect in entry.Frames){
            var frame=new AtlasTexture {Atlas=texture,Region=new Rect2(rect[0],rect[1],rect[2],rect[3])};frames.AddFrame(BattleAnimations.Effect,frame);
            var used=CharacterVisual.VisibleBounds(frame);
            if(used.Size.X>0&&used.Size.Y>0){var relative=new Rect2(used.Position-new Vector2(rect[2]/2f,rect[3]/2f),used.Size);bounds=hasBounds?bounds.Merge(relative):relative;hasBounds=true;}
        }
        // Match battlefield character scale; large hits remain bigger without covering adjacent slots.
        float extent=name.Contains("_large_")?48:28;
        float scale=extent/Mathf.Max(1,Mathf.Max(bounds.Size.X,bounds.Size.Y));
        visual=new Visual(frames,bounds.GetCenter(),new Vector2(bounds.GetCenter().X,bounds.End.Y),scale);Cache.Add(name,visual);return visual;
    }
    public static AnimatedSprite2D? Play(BattleUnit source,BattleUnit target,string animation,string kind,int damage)
    {
        string lookupAnimation=kind=="counter"?"attack01":animation;
        var choice=CombatVisualSettings.Find(source.CardKind,lookupAnimation);
        bool eligible=(damage>0&&kind is "damage" or "splash" or "counter" or "execute")||kind is "heal" or "revive" or "curse" or "summon";
        if(choice!=null&&eligible)return PlayChoice(source,target,choice,kind);
        string? name=kind switch {
            "heal"=>"spell_heal_001_small_red",
            "revive"=>"spell_heal_001_large_red",
            "curse"=>"spell_absorb_001_small_violet",
            "summon"=>"spell_death_001_large_red",
            "armor"=>"spell_defense_up_001_small_blue",
            "poison"=>damage>0?"spell_poison_001_small_green":null,
            _=>damage>0?DefaultEffect(source.CardKind,lookupAnimation):null
        };
        if(name==null)return null;
        return Create(source,target,name,kind,null);
    }
    public static AnimatedSprite2D? PlayChoice(BattleUnit source,BattleUnit target,CombatVisualChoice choice,string kind="damage")
    {
        if(choice.Effect.Length==0)return null;
        return Create(source,target,choice.Effect,kind,choice);
    }
    private static AnimatedSprite2D Create(BattleUnit source,BattleUnit target,string name,string kind,CombatVisualChoice? choice)
    {
        var visual=Load(name);
        bool ground=name.StartsWith("directional_impact_002")||name.StartsWith("directional_impact_003")||name.StartsWith("directional_impact_004")||kind=="summon";
        if(choice?.Anchor=="body")ground=false;
        if(choice?.Anchor=="feet")ground=true;
        float scale=visual.Scale*(choice?.Scale??1);
        float rotation=name.StartsWith("directional_impact_001")?(target.GlobalPosition-source.GlobalPosition).Angle():0;
        var feet=CharacterVisual.GroundAnchor(target.Sprite);
        var anchor=target.Position+(ground?feet:(feet+CharacterVisual.HeadAnchor(target.Sprite))*.5f);
        var offset=(ground?visual.Foot:visual.Center)*scale;
        var effect=new AnimatedSprite2D {Name="CombatImpact",SpriteFrames=visual.Frames,Scale=Vector2.One*scale,
            Rotation=rotation,Position=anchor-offset.Rotated(rotation)+new Vector2(choice?.OffsetX??0,choice?.OffsetY??0),ZIndex=10,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};
        effect.SetMeta("effect_name",name);effect.SetMeta("hit_target",target.ServerId);effect.SetMeta("hit_source",source.ServerId);
        // A sibling survives the victim's fade and inherits arena movement and replay pause.
        target.GetParent().AddChild(effect);
        effect.AnimationFinished+=effect.QueueFree;effect.Play(BattleAnimations.Effect);return effect;
    }
}
