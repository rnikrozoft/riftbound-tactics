using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DeckMatchupChart : VBoxContainer
{
    public sealed record Result(string Name,int Wins,int Draws,int Losses)
    {
        public int Total=>Wins+Draws+Losses;
        public double WinRate=>Total==0?0:100d*Wins/Total;
    }
    private static readonly Dictionary<string,Result[]> Cache=new();
    private readonly DeckDefinition _deck;
    public bool Finished {get;private set;}
    public Result[] Results {get;private set;}=Array.Empty<Result>();
    private Label _progress=null!;
    private VBoxContainer _rows=null!;
    public DeckMatchupChart():this(DeckDefinition.Starter()){}
    public DeckMatchupChart(DeckDefinition deck){_deck=deck.Clone();SizeFlagsHorizontal=SizeFlags.ExpandFill;AddThemeConstantOverride("separation",7);}
    public override async void _Ready()
    {
        _rows=new VBoxContainer();_rows.AddThemeConstantOverride("separation",8);AddChild(_rows);
        _progress=DeckMenuUi.Text("Simulating…",12);AddChild(_progress);
        try{await Calculate();}catch(Exception e){if(IsInsideTree())_progress.Text="Benchmark unavailable";GD.PushWarning(e.Message);}
    }
    public static OnlineCombatUnit[] Sample(DeckDefinition deck,string team,int stars,Random rng,int[] costs)
    {
        var pool=deck.Cards.SelectMany(e=>Enumerable.Repeat(e.Kind,Math.Max(0,e.Copies))).ToList();
        var units=new List<OnlineCombatUnit>();var slots=Enumerable.Range(0,6).OrderBy(_=>rng.Next()).ToArray();
        for(int i=0;i<costs.Length;i++){
            var options=pool.Where(k=>CardCatalog.Cost(k)==costs[i]).ToArray();
            if(options.Length==0)return Array.Empty<OnlineCombatUnit>();
            int kind=options[rng.Next(options.Length)];pool.Remove(kind);
            units.Add(new OnlineCombatUnit {Id=team+":"+i,Team=team,Token=i,Kind=kind,Stars=stars,Slot=slots[i]});
        }
        return units.ToArray();
    }
    private async Task Calculate()
    {
        string key=string.Join(";",_deck.Cards.OrderBy(e=>e.Kind).Select(e=>$"{e.Kind}:{e.Copies}"));
        if(Cache.TryGetValue(key,out var cached)){Results=cached;Render();return;}
        var results=new List<Result>();int[] costs={2,3,4,4,5,6};
        var groups=CharacterData.All.Where(c=>c.Enabled).GroupBy(c=>c.Group).Where(g=>costs.All(cost=>g.Any(c=>c.Cost==cost))).OrderBy(g=>g.Key).ToArray();
        foreach(var group in groups){
            var opponent=new DeckDefinition();opponent.Cards=group.Select(c=>new DeckEntry {Kind=c.Kind,Copies=c.MaxCopies}).ToList();
            int wins=0,draws=0,losses=0;
            for(int trial=0;trial<40;trial++){
                if(!IsInsideTree())return;
                int stars=trial%2+1;var rng=new Random(7409+trial*103+group.Key*997);
                string ourTeam=trial%4<2?"A":"B",theirTeam=ourTeam=="A"?"B":"A";
                var ours=Sample(_deck,ourTeam,stars,rng,costs);var theirs=Sample(opponent,theirTeam,stars,rng,costs);
                if(ours.Length!=6||theirs.Length!=6){_progress.Text="Not enough cards for equal-cost samples";return;}
                var plan=new LocalCombat(1907+trial*59).Simulate(ours.Concat(theirs));
                if(plan.Winner==ourTeam)wins++;else if(plan.Winner=="DRAW")draws++;else losses++;
                if(trial%5==4){_progress.Text=$"Simulating {results.Count*40+trial+1}/{groups.Length*40}";await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            }
            results.Add(new Result(CharacterData.Groups[group.Key],wins,draws,losses));
        }
        if(!IsInsideTree())return;Results=results.ToArray();Cache[key]=Results;Render();
    }
    private void Render()
    {
        DeckMenuUi.Clear(_rows);int total=Results.Sum(r=>r.Total),wins=Results.Sum(r=>r.Wins);
        _rows.AddChild(new MatchupDonut(wins,Results.Sum(r=>r.Draws),Results.Sum(r=>r.Losses)) {Name="MatchupDonut"});
        foreach(var result in Results){
            var row=new VBoxContainer();row.AddThemeConstantOverride("separation",3);_rows.AddChild(row);
            var label=DeckMenuUi.Text($"{result.Name}   {result.WinRate:0}%   ({result.Wins}/{result.Draws}/{result.Losses})",12);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;row.AddChild(label);

        }
        _progress.Text="";_progress.Hide();Finished=true;
    }
}
