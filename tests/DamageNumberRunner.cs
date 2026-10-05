using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class DamageNumberRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async Task Frames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async void Run()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.AutoStart=false;battle.NetworkEnabled=false;battle.ArenaFloatAmplitude=0;AddChild(field);await Frames();
            var target=field.GetChildren().OfType<BattleUnit>().Where(unit=>unit.IsAlly).OrderByDescending(unit=>unit.GlobalPosition.Y).First();
            target.TakeDamage(30);var popup=field.GetChildren().OfType<Node2D>().First(node=>node.Name.ToString().StartsWith("DamageNumber"));
            if(popup.GetNode<Label>("Value").Text!="-30" || !popup.GetNode<CpuParticles2D>("Sparks").Emitting)throw new Exception("Damage text or particles missing");
            var start=popup.Position;await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            if(popup.Position.Y>=start.Y)throw new Exception("Damage did not rise");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-damage-number.png");
            target.TakeDamage(0);if(field.GetChildren().OfType<Node2D>().Count(n=>n.Name.ToString().StartsWith("DamageNumber"))!=1)throw new Exception("Zero damage spawned a number");
            await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);if(GodotObject.IsInstanceValid(popup))throw new Exception("Damage number was not cleaned up");
            target.ApplyServerHealth(40,30);popup=field.GetChildren().OfType<Node2D>().First(node=>node.Name.ToString().StartsWith("DamageNumber"));if(popup.GetNode<Label>("Value").Text!="-30")throw new Exception("Online damage number missing");
            target.ApplyServerHealth(40,30);target.ResetHealth();if(field.GetChildren().OfType<Node2D>().Count(n=>n.Name.ToString().StartsWith("DamageNumber"))!=1)throw new Exception("Sync or healing spawned duplicate numbers");
            target.TakeDamage(120);if(!target.IsDead || !field.GetChildren().OfType<Node2D>().Any(n=>n.Name.ToString().StartsWith("DamageNumber") && n.GetNode<Label>("Value").Text=="-120"))throw new Exception("Lethal damage number missing");
            target.QueueFree();await Frames();await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);if(field.GetChildren().Any(n=>n.Name.ToString().StartsWith("DamageNumber")))throw new Exception("Death left damage effects behind");
            GD.Print("DAMAGE NUMBER PASS: offline/online hits, zero damage, repeated state, healing, lethal hit, floating animation and cleanup");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
