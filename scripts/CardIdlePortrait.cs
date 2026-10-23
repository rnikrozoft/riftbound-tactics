using Godot;
using System;
using System.Collections.Generic;

// A fixed crop shared by every idle frame keeps the figure steady inside its card.
public partial class CardIdlePortrait : TextureRect
{
    private sealed record IdleClip(Texture2D[] Textures,double[] Durations,double Length);
    private static readonly Dictionary<string,IdleClip> Clips=new();
    private IdleClip? _clip;
    private string _scene="";
    private double _elapsed;
    public int CurrentFrame {get;private set;}
    public int IdleFrameCount=>_clip?.Textures.Length??0;
    public void SetKind(int kind)
    {
        string scene=CharacterData.Get(kind).ScenePath;
        if(_scene==scene)return;
        _scene=scene;
        if(!Clips.TryGetValue(scene,out _clip)){
            var actor=GD.Load<PackedScene>(scene).Instantiate();
            var frames=actor.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames;
            var animation=BattleAnimations.Idle;
            int count=frames.GetFrameCount(animation);
            if(count<1){actor.Free();throw new InvalidOperationException("Missing idle animation: "+scene);}
            Rect2I crop=CharacterVisual.VisibleBounds(frames.GetFrameTexture(animation,0));
            for(int i=1;i<count;i++)crop=crop.Merge(CharacterVisual.VisibleBounds(frames.GetFrameTexture(animation,i)));
            var textures=new Texture2D[count];var durations=new double[count];double length=0;
            for(int i=0;i<count;i++){
                var source=frames.GetFrameTexture(animation,i);
                var atlas=source as AtlasTexture;
                textures[i]=new AtlasTexture {
                    // Keep padded frame coordinates when an atlas uses margins to align its feet.
                    Atlas=atlas!=null&&atlas.Margin!=default?source:atlas?.Atlas??source,
                    Region=new Rect2((atlas!=null&&atlas.Margin!=default?Vector2.Zero:atlas?.Region.Position??Vector2.Zero)+(Vector2)crop.Position,crop.Size)
                };
                durations[i]=frames.GetFrameDuration(animation,i)/Math.Max(.01,frames.GetAnimationSpeed(animation));
                length+=durations[i];
            }
            actor.Free();_clip=new(textures,durations,length);Clips[scene]=_clip;
        }
        _elapsed=0;CurrentFrame=0;Texture=_clip.Textures[0];
    }
    public override void _Process(double delta)
    {
        if(_clip==null || !IsVisibleInTree())return;
        _elapsed=(_elapsed+delta)%_clip.Length;
        double remaining=_elapsed;int frame=0;
        while(frame<_clip.Textures.Length-1 && remaining>=_clip.Durations[frame])remaining-=_clip.Durations[frame++];
        if(frame==CurrentFrame)return;
        CurrentFrame=frame;Texture=_clip.Textures[frame];
    }
}
