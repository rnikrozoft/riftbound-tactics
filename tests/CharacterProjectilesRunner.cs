using Godot;
using System;
using System.Linq;

public partial class CharacterProjectilesRunner : Node2D
{
    private int _checks;
    private void Check(bool condition,string message){_checks++;if(!condition)throw new Exception(message);}
    private BattleUnit Unit(string slug,Vector2 position)
    {
        var card=CharacterData.All.Single(c=>c.Enabled&&CombatVisualSettings.Slug(c.Kind)==slug);
        var unit=GD.Load<PackedScene>(card.ScenePath).Instantiate<BattleUnit>();unit.CardKind=card.Kind;unit.Position=position;AddChild(unit);return unit;
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var victim=Unit("knight",new(160,80));
            foreach(string slug in new[]{"archer","skeleton_archer","demon_b"}) {
                var actor=Unit(slug,new(30,80));
                foreach(string animation in slug=="archer"?new[]{"attack01","attack02"}:new[]{"attack01"}) {
                    foreach(bool reverse in new[]{false,true}) {
                        actor.Position=new(reverse?260:30,80);actor.Sprite.FlipH=reverse;
                        actor.Sprite.Play(animation);actor.Sprite.SetFrameAndProgress(1,0);
                        var shot=CharacterProjectiles.Begin(actor,victim,animation,animation,4)!;
                        Check(shot!=null,"missing arrow");
                        var start=shot!.GlobalPosition;
                        actor.Sprite.SetFrameAndProgress(3,0);shot._Process(0);
                        Check(reverse?shot.GlobalPosition.X<start.X:shot.GlobalPosition.X>start.X,"arrow travels toward target");
                        Check(reverse?Mathf.Cos(shot.Rotation)<-.9f:Mathf.Cos(shot.Rotation)>.9f,"arrow direction");
                        actor.Sprite.SetFrameAndProgress(4,0);shot._Process(0);
                        Check(shot.IsQueuedForDeletion(),"arrow removed at impact");
                    }
                }
                Check(CharacterProjectiles.ArrowFor(actor.CardKind,"attack03")==null,"no arrow for other attacks");actor.QueueFree();
            }
            foreach(var pair in new[]{("priest","attack01","damage"),("priest","attack02","heal"),("wizard","attack01","revive"),("wizard","attack02","damage"),("necromancer","attack02","damage"),("necromancer","summon","summon")}) {
                var actor=Unit(pair.Item1,new(30,80));
                Check(actor.Sprite.SpriteFrames.HasAnimation(pair.Item2),"missing matching animation");
                var effect=CharacterProjectiles.Impact(actor,victim,pair.Item2,pair.Item3);
                Check(effect!=null&&effect.SpriteFrames.GetFrameCount("effect")>1,"magic sheet animated");
                Check(effect!.GetMeta("projectile_asset").AsString().Contains(pair.Item1.Capitalize()),"magic belongs to character");
                await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);
                Check(!GodotObject.IsInstanceValid(effect),"magic cleans up");actor.QueueFree();
            }
            var rider=Unit("orc_rider",Vector2.Zero);Check(CharacterProjectiles.ArrowFor(rider.CardKind,"attack01")==null,"thrown weapon is not a bow");
            GD.Print($"CHARACTER PROJECTILES PASS: {_checks} checks");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
