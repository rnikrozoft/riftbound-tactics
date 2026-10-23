using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Wildlife follows the garden's cell graph, so it cannot cross water, slopes or vegetation.
public partial class ArenaWildlife : Node2D
{
	private readonly HashSet<Vector2I> _walkable = new();
	private readonly Dictionary<Vector2I, Critter> _reserved = new();
	private readonly List<Critter> _animals = new();
	private readonly Dictionary<string, SpriteFrames> _frames = new();
	private readonly RandomNumberGenerator _random = new();
	private readonly Vector2I[] _stepOptions = new Vector2I[4];
	private TileMapLayer _ground = null!;
	private static readonly Vector2I[] Steps = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
	public int AnimalCount => _animals.Count;
	public bool IsWalkable(Vector2I cell) => _walkable.Contains(cell);
	public bool AllAnimalsOnLand => _animals.All(a => IsWalkable(a.Cell) && IsWalkable(a.Target));
	public override void _Ready()
	{
		_random.Randomize();
		_ground = GetParent().GetNode<TileMapLayer>("GardenGround");
		var blocked = new HashSet<Vector2I>();
		foreach (var layer in GetParent().GetChildren().OfType<TileMapLayer>())
		{
			if (layer == _ground) continue;
			foreach (var cell in layer.GetUsedCells())
			{
				blocked.Add(cell);
				// Leave room around solid scenery and the river's edge.
				if (layer.Name != "GardenWater") foreach (var step in Steps) blocked.Add(cell + step);
			}
		}
		foreach (var cell in _ground.GetUsedCells())
			if (cell.X >= 18 && cell.X <= 86 && cell.Y >= -12 && cell.Y <= 46 &&
				!(cell.X >= 32 && cell.X <= 70 && cell.Y >= 8 && cell.Y <= 26) && !blocked.Contains(cell))
				_walkable.Add(cell);
		BuildForest();
		var candidates = _walkable.Where(c => Steps.Count(s => _walkable.Contains(c+s)) >= 2).OrderBy(c=>c.Y).ThenBy(c=>c.X).ToArray();
		string[] species = { "stag", "boar", "badger", "wolf" };
		// Distributed habitats, with individual timing and small wolf populations.
		for (int i = 0; i < 48 && candidates.Length > 0; i++)
		{
			var cell = candidates[(int)((i+.5f)*candidates.Length/48) % candidates.Length];
			if (_reserved.ContainsKey(cell)) continue;
			string kind = species[i%8 == 7 ? 3 : i%3];
			if (!_frames.ContainsKey(kind)) _frames[kind] = BuildFrames(kind);
			var animal = new Critter { Cell=cell, Target=cell, Home=cell, Kind=kind,
				Position=Point(cell), Wait=_random.RandfRange(.2f,5f), Speed=_random.RandfRange(9,16),
				SpriteFrames=_frames[kind], TextureFilter=TextureFilterEnum.Nearest,
				Scale=Vector2.One*(kind=="stag"?.72f:kind=="wolf"?.7f:.85f),
				Offset=new Vector2(0,kind=="stag"?-16:kind=="wolf"?-16:-11) };
			AddChild(animal); animal.Play("idle_SE"); animal.Frame=_random.RandiRange(0,animal.SpriteFrames.GetFrameCount("idle_SE")-1);
			_animals.Add(animal); _reserved[cell]=animal;
		}
	}
	public int VegetationCount { get; private set; }
	private void BuildForest()
	{
		var markers=GetParent().GetNode<TileMapLayer>("GardenTrees");
		var atlas=GD.Load<Texture2D>("res://assets/terrain/bushes/bush-atlas.png");
        // Batch bushes at identical Y positions so the arena's depth sorting
        // still places animals and heroes in front of/behind the same scenery.
        var rows = new Dictionary<float,ForestLayer>();
        foreach(var cell in markers.GetUsedCells())
        {
            var tile=markers.GetCellAtlasCoords(cell);
            var point=Point(cell);
            if(!rows.TryGetValue(point.Y,out var row))
            {
                row=new ForestLayer {Texture=atlas,Position=new Vector2(0,point.Y),TextureFilter=TextureFilterEnum.Nearest};
                rows.Add(point.Y,row);
                AddChild(row);
            }
            row.Bushes.Add((new Rect2(point-row.Position+new Vector2(-24,-46),new Vector2(48,48)),new Rect2(tile.X*48,tile.Y*48,48,48)));
            VegetationCount++;
        }
	}
	private Vector2 Point(Vector2I cell) => ToLocal(_ground.ToGlobal(_ground.MapToLocal(cell)));
	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) return;
		float dt=(float)Math.Min(delta,.1);
		foreach (var a in _animals)
		{
			if (a.Cell != a.Target)
			{
				var goal=Point(a.Target);
				a.Position=a.Position.MoveToward(goal,a.Speed*dt);
				if (a.Position.DistanceTo(goal)<.01f)
				{
					_reserved.Remove(a.Cell); a.Cell=a.Target;
					if (_random.Randf()<.32f) { a.Wait=_random.RandfRange(1.4f,6f); a.Play("idle_"+a.Facing); }
				}
				continue;
			}
			a.Wait-=dt; if(a.Wait>0) continue;
            int optionCount=0;
            foreach(var step in Steps)
            {
                var cell=a.Cell+step;
                if(_walkable.Contains(cell)&&!_reserved.ContainsKey(cell)&&cell.DistanceTo(a.Home)<14)
                    _stepOptions[optionCount++]=cell;
            }
            var options=_stepOptions.AsSpan(0,optionCount);
			if(options.Length==0) { a.Wait=_random.RandfRange(.5f,1.5f); a.Play("idle_"+a.Facing); continue; }
			// Prefer continuing forward, but vary turns and return toward the habitat.
			var target=options[_random.RandiRange(0,options.Length-1)];
			var forward=a.Cell+a.Heading;
			if(options.Contains(forward)&&_random.Randf()<.65f) target=forward;
			a.Heading=target-a.Cell; a.Target=target; _reserved[target]=a;
			var d=Point(target)-a.Position;
			a.Facing=d.X>=0?(d.Y>=0?"SE":"NE"):(d.Y>=0?"SW":"NW");
			a.Play("move_"+a.Facing); a.SpeedScale=_random.RandfRange(.85f,1.15f);
		}
	}
	private static SpriteFrames BuildFrames(string kind)
	{
		var frames=new SpriteFrames(); frames.RemoveAnimation("default");
		string[] directions={"NE","SE","SW","NW"};
		foreach(var state in new[]{"idle","move"}) foreach(var direction in directions)
		{
			string file; int width,count,row=0;
			if(kind=="wolf") { file=$"wolf/wolf-{(state=="idle"?"idle":"run")}.png";width=64;count=state=="idle"?4:8;row=Array.IndexOf(new[]{"SW","SE","NW","NE"},direction); }
			else if(kind=="boar") { file=$"boar/boar_{direction}_{(state=="idle"?"idle":"run")}_strip.png";width=direction=="NW"?40:41;count=state=="idle"?7:4; }
			else { file=$"{kind}/critter_{kind}_{direction}_{(state=="idle"?"idle":"walk")}.png";width=kind=="stag"?32:42;count=kind=="stag"?(state=="idle"?24:11):(state=="idle"?22:9); }
			var texture=GD.Load<Texture2D>("res://assets/characters/wildlife/"+file);
			string animation=state+"_"+direction; frames.AddAnimation(animation); frames.SetAnimationSpeed(animation,state=="idle"?7:10);
			int height=kind=="wolf"?64:texture.GetHeight();
			for(int i=0;i<count;i++) frames.AddFrame(animation,new AtlasTexture { Atlas=texture,Region=new Rect2(i*width,row*height,width,height) });
		}
		return frames;
	}
	public override void _ExitTree() { _animals.Clear(); _reserved.Clear(); _walkable.Clear(); _frames.Clear(); VegetationCount=0; }
    private partial class ForestLayer : Node2D
    {
        public Texture2D Texture = null!;
        public readonly List<(Rect2 Destination,Rect2 Source)> Bushes = new();
        public override void _Draw()
        {
            foreach(var bush in Bushes)DrawTextureRectRegion(Texture,bush.Destination,bush.Source);
        }
    }
	private partial class Critter : AnimatedSprite2D
	{
		public Vector2I Cell,Target,Home,Heading;
		public string Kind="",Facing="SE";
		public float Wait,Speed;
	}
}
