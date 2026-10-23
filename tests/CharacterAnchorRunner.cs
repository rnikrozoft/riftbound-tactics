using Godot;
using System;
using System.Threading.Tasks;

public partial class CharacterAnchorRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            foreach(bool flipped in new[]{false,true}) {
                var unit=GD.Load<PackedScene>("res://scenes/characters/black_knight_a.tscn").Instantiate<BattleUnit>();
                unit.Position=new(flipped?760:520,400);unit.ConfigureStars(2);unit.Scale=Vector2.One*3;AddChild(unit);
                unit.Sprite.FlipH=flipped;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var head=CharacterVisual.HeadAnchor(unit.Sprite);
                var native=(head-unit.Sprite.Position)/unit.Sprite.Scale-unit.Sprite.Offset;
                if(flipped)native.X=-native.X;
                native+=new Vector2(50,50);
                if(native.X<48||native.X>53||native.Y<36||native.Y>42)throw new Exception("HUD is anchored to polearm instead of helmet: "+native);
                var feet=CharacterVisual.GroundAnchor(unit.Sprite);
                float starCenter=(unit.GetNode<Sprite2D>("Star1").Position.X+unit.GetNode<Sprite2D>("Star2").Position.X)/2;
                float statsCenter=(unit.GetNode<Label>("ArmorValue").Position.X+unit.GetNode<Label>("PowerValue").Position.X)/2+12;
                if(Mathf.Abs(starCenter-head.X)>.1f||Mathf.Abs(statsCenter-head.X)>1||Mathf.Abs(feet.X-head.X)>.1f)throw new Exception("Stats or stars are not centered on body");
            }
            if(DisplayServer.GetName()!="headless") {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://output/black-knight-hud.png");
            }
            GD.Print("CHARACTER ANCHOR PASS: Black Knight A helmet, stats and stars aligned in both facing directions");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}

