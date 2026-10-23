using Godot;
using System.Collections.Generic;
using System.Text.Json;

// Use the source Tiled frame coordinates and durations without repainting textures.
public partial class UndeadTileAnimation : Node2D
{
    private sealed class Clip
    {
        public int Source, Duration, Current = -1;
        public readonly List<(Vector2I Tile,int End)> Frames = new();
        public readonly List<(TileMapLayer Layer,Vector2I Cell,int Alternative)> Cells = new();
    }
    private readonly List<Clip> _clips = new();
    private ulong _start;
    public int AnimatedCellCount { get; private set; }
    public override void _Ready()
    {
        using var json=JsonDocument.Parse(FileAccess.GetFileAsString("res://tilesets/undead_animations.json"));
        var lookup=new Dictionary<(int,Vector2I),Clip>();
        foreach(var item in json.RootElement.EnumerateArray()) {
            var clip=new Clip {Source=item.GetProperty("source").GetInt32()};
            foreach(var frame in item.GetProperty("frames").EnumerateArray()) {
                clip.Duration+=frame[2].GetInt32();
                clip.Frames.Add((new Vector2I(frame[0].GetInt32(),frame[1].GetInt32()),clip.Duration));
            }
            lookup[(clip.Source,new Vector2I(item.GetProperty("x").GetInt32(),item.GetProperty("y").GetInt32()))]=clip;
        }
        void Collect(Node node) {
            if(node is TileMapLayer layer)
                foreach(var cell in layer.GetUsedCells())
                    if(lookup.TryGetValue((layer.GetCellSourceId(cell),layer.GetCellAtlasCoords(cell)),out var clip)) {
                        clip.Cells.Add((layer,cell,layer.GetCellAlternativeTile(cell)));AnimatedCellCount++;
                    }
            foreach(var child in node.GetChildren())Collect(child);
        }
        Collect(this);
        foreach(var clip in lookup.Values)if(clip.Cells.Count>0)_clips.Add(clip);
        _start=Time.GetTicksMsec();
        SetProcess(_clips.Count>0);
    }
    public override void _Process(double delta)
    {
        ulong elapsed=Time.GetTicksMsec()-_start;
        foreach(var clip in _clips) {
            int time=(int)(elapsed%(ulong)clip.Duration),index=0;
            while(time>=clip.Frames[index].End)index++;
            if(index==clip.Current)continue;
            clip.Current=index;
            foreach(var cell in clip.Cells)cell.Layer.SetCell(cell.Cell,clip.Source,clip.Frames[index].Tile,cell.Alternative);
        }
    }
}
