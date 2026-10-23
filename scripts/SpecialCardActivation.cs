using Godot;
// A short world-space flash makes the single-use trigger visible before removal.
public partial class SpecialCardActivation:Node2D
{
 private float _age;
 public static void Play(BattleUnit unit){var effect=new SpecialCardActivation{Position=unit.Position,ZIndex=6};unit.GetParent().AddChild(effect);}
 public override void _Process(double delta){_age+=(float)delta;if(_age>.45f){QueueFree();return;}QueueRedraw();}
 public override void _Draw(){float t=_age/.45f;DrawArc(new Vector2(0,-18),10+36*t,0,Mathf.Tau,32,new Color(.65f,.9f,1,1-t),3);}
}
