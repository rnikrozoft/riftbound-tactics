using Godot;
using System.Collections.Generic;

public static class CharacterCombatAnimations
{
    private static readonly Dictionary<string,SpriteFrames> Cache=new();
    public static SpriteFrames Load(int kind,SpriteFrames original)
    {
        var c=CharacterData.Get(kind);if(Cache.TryGetValue(c.ScenePath,out var cached))return cached;
        var frames=(SpriteFrames)original.Duplicate();string slug=c.ScenePath.GetFile().GetBaseName();string folder=$"res://assets/characters/{slug}";
        for(int i=1;i<=3;i++){
            string name=$"attack{i:00}";string path=$"{folder}/{slug}_{name}.png";
            if(slug=="swordsman"&&i==3)path=folder+"/swordsman_attack3.png";
            if(ResourceLoader.Exists(path)){
                var texture=GD.Load<Texture2D>(path);if(frames.HasAnimation(name))frames.RemoveAnimation(name);frames.AddAnimation(name);frames.SetAnimationSpeed(name,12);frames.SetAnimationLoopMode(name,SpriteFrames.LoopMode.None);
                for(int y=0;y<texture.GetHeight();y+=100)for(int x=0;x<texture.GetWidth();x+=100)frames.AddFrame(name,new AtlasTexture {Atlas=texture,Region=new Rect2(x,y,100,100)});
            }else if(slug=="knight"&&ResourceLoader.Exists(folder+"/knight_atlas.png")){
                var atlas=GD.Load<Texture2D>(folder+"/knight_atlas.png");frames.AddAnimation(name);frames.SetAnimationSpeed(name,12);frames.SetAnimationLoopMode(name,SpriteFrames.LoopMode.None);
                for(int f=0;f<(i==3?9:6);f++)frames.AddFrame(name,new AtlasTexture {Atlas=atlas,Region=new Rect2(f*100,(i+1)*100,100,100)});
            }else{
                StringName source=i==2&&frames.HasAnimation("heavy_attack")?new("heavy_attack"):BattleAnimations.Attack;
                frames.AddAnimation(name);frames.SetAnimationSpeed(name,12);frames.SetAnimationLoopMode(name,SpriteFrames.LoopMode.None);
                for(int f=0;f<frames.GetFrameCount(source);f++)frames.AddFrame(name,frames.GetFrameTexture(source,f));
            }
        }
        Cache[c.ScenePath]=frames;return frames;
    }
}
