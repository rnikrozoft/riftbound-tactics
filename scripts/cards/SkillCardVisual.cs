using Godot;
using System.Linq;
public partial class SkillCardVisual:Node
{
 private BattleUnit _unit=null!;private ShaderMaterial _material=null!;private double _elapsed,_ripTime;private bool _ripping;private int _ripFrame=-1;
 private static Texture2D[]? _rip;
 public static bool IsSkill(int kind)=>CharacterData.Get(kind).Tags.Split(',').Contains("skill_card");
 public static void Attach(BattleUnit unit){if(!IsSkill(unit.CardKind))return;var visual=new SkillCardVisual();unit.AddChild(visual);visual.Configure(unit);}
 private void Configure(BattleUnit unit){_unit=unit;var frames=new SpriteFrames();foreach(string name in new[]{"idle","walk","attack","attack01","attack02","attack03","hit","die"}){if(!frames.HasAnimation(name))frames.AddAnimation(name);frames.AddFrame(name,CardCatalog.Art(unit.CardKind));}unit.Sprite.SpriteFrames=frames;unit.Sprite.Offset=new(0,-17.5f);unit.Sprite.Scale=Vector2.One;unit.Sprite.FlipH=false;unit.Sprite.Play("idle");_material=new ShaderMaterial{Shader=GD.Load<Shader>("res://shaders/skill_card.gdshader")};_material.SetShaderParameter("skill_icon",GD.Load<Texture2D>(CharacterData.Get(unit.CardKind).PortraitPath));unit.Sprite.Material=_material;
 _rip??=DirAccess.GetFilesAt("res://assets/effects/cards/rip").Where(p=>p.StartsWith("cardRIP_")&&p.EndsWith(".png")).OrderByDescending(p=>p).Select(p=>GD.Load<Texture2D>("res://assets/effects/cards/rip/"+p)).ToArray();}
 public void Rip(){if(_ripping)return;_ripping=true;_ripTime=0;_material.SetShaderParameter("ripping",true);}
 public static void Destroy(BattleUnit unit){foreach(Node child in unit.GetChildren())if(child is SkillCardVisual visual){visual.Rip();return;}unit.Hide();}
 public override void _Process(double delta){_elapsed+=delta;_unit.Sprite.Position=new(0,(float)System.Math.Sin(_elapsed*2)*1.5f);if(!_ripping)return;_ripTime+=delta;int frame=(int)(_ripTime*18);if(frame>=_rip!.Length){_unit.Hide();SetProcess(false);return;}if(frame!=_ripFrame){_ripFrame=frame;_material.SetShaderParameter("rip_mask",_rip[frame]);}}
}
