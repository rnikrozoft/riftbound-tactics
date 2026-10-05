using Godot;
using System;
using System.Linq;

public partial class CharacterAnimationLabRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var lab=GD.Load<PackedScene>("res://scenes/character_animation_lab.tscn").Instantiate<CharacterAnimationLab>();AddChild(lab);
            if(lab.Characters.Length!=CharacterData.All.Select(c=>c.ScenePath).Distinct().Count())throw new Exception("Character scene missing from lab");
            int animations=0,frames=0;
            for(int i=0;i<lab.Characters.Length;i++){
                lab.SelectCharacter(i);if(lab.Animations.Length<5)throw new Exception("Missing source animations: "+lab.Characters[i].Name);
                for(int a=0;a<lab.Animations.Length;a++){
                    lab.SelectAnimation(a);int count=lab.Preview.SpriteFrames.GetFrameCount(lab.Preview.Animation);animations++;frames+=count;
                    for(int f=0;f<count;f++){
                        var atlas=(AtlasTexture)lab.Preview.SpriteFrames.GetFrameTexture(lab.Preview.Animation,f);
                        if(atlas.Region.Size!=new Vector2(100,100) || atlas.Region.End.X>atlas.Atlas.GetWidth() || atlas.Region.End.Y>atlas.Atlas.GetHeight())throw new Exception("Invalid source frame");
                    }
                    lab.Scrub(count-1);if(lab.Preview.IsPlaying() || lab.Preview.Frame!=count-1)throw new Exception("Frame scrub did not pause on selected frame");
                    lab.Step(-1);if(lab.Preview.Frame!=Math.Max(0,count-2))throw new Exception("Previous frame failed");
                    lab.SetLoop(false);if(lab.Preview.SpriteFrames.GetAnimationLoopMode(lab.Preview.Animation)!=SpriteFrames.LoopMode.None)throw new Exception("Loop toggle failed");
                    lab.Play();if(!lab.Preview.IsPlaying() || lab.Preview.Frame!=0)throw new Exception("Restart failed");lab.Pause();
                }
            }
            int archer=Array.FindIndex(lab.Characters,c=>c.Name=="Archer");lab.SelectCharacter(archer);
            if(!lab.Animations.Contains("attack02"))throw new Exception("Alternative attack missing");
            lab.SelectAnimation(Array.IndexOf(lab.Animations,"attack02"));lab.SetLoop(true);lab.Scrub(6);lab.Preview.FlipH=true;if(!lab.Preview.FlipH)throw new Exception("Facing toggle failed");lab.Preview.FlipH=false;
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(lab.Preview.GetParent<Control>().Size.X<300)throw new Exception("Preview stage must expand to available width");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-character-animation-lab.png");
            GD.Print($"CHARACTER ANIMATION LAB PASS: {lab.Characters.Length} characters, {animations} animations, {frames} frames, alternate attacks, playback, stepping and loop");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
