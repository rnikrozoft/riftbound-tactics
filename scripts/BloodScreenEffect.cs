using Godot;
using System.Collections.Generic;
using System;
using System.Linq;

public partial class BloodScreenEffect : CanvasLayer
{
    public TextureRect Image {get;private set;}=null!;
    public float Opacity=>Image.Modulate.A;
    private readonly Dictionary<OnlineCombatHit,(float Before,float After)> _progress=new();
    private readonly HashSet<OnlineCombatHit> _threshold=new();
    private readonly Dictionary<string,int> _liveHp=new();
    private readonly HashSet<OnlineCombatHit> _thresholdStarts=new();
    private Tween? _intro;
    public bool Armed=>_progress.Count>0;
    public bool Eliminated {get;private set;}
    public event Action? LastUnitDied;
    private sealed class ImpactGroup
    {
        public List<(OnlineCombatHit Hit,int Loss)> Hits=new();
        public int Alive;
        public int Loss=>Hits.Sum(h=>h.Loss);
    }
    public override void _Ready()
    {
        Layer=30;
        Image=new TextureRect {Name="DefeatEffect",Texture=GD.Load<Texture2D>("res://assets/effects/Bloody Screen Effects/Effect_4.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=Control.MouseFilterEnum.Ignore};
        AddChild(Image);Image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);SetOpacity(0);
    }
    private void SetOpacity(float alpha){Image.Modulate=new Color(1,1,1,Mathf.Clamp(alpha,0,1));}
    public void Clear(){_intro?.Kill();_intro=null;_progress.Clear();_threshold.Clear();_thresholdStarts.Clear();_liveHp.Clear();Eliminated=false;SetOpacity(0);}
    private void StartFade(){_intro?.Kill();_intro=Image.CreateTween();_intro.TweenProperty(Image,"modulate:a",.08f,.4);}
    public void Prepare(OnlinePlan plan,string team,bool eliminated)
    {
        Clear();if(!eliminated||plan.Winner==team||plan.Winner=="DRAW")return;
        var hp=new Dictionary<string,int>();
        foreach(var unit in plan.Units)if(unit.Team==team)hp[unit.Id]=unit.Summoned?0:(unit.InitialHp>0?unit.InitialHp:unit.MaxHp);
        foreach(var entry in hp)_liveHp[entry.Key]=entry.Value;
        int initialAlive=hp.Values.Count(value=>value>0);
        if(initialAlive==0){Eliminated=true;SetOpacity(1);LastUnitDied?.Invoke();return;}
        var groups=new List<ImpactGroup>();
        foreach(var action in plan.Events){
        int previousFrame=-1;ImpactGroup? group=null;
        foreach(var hit in action.Hits){
            if(group==null||hit.Frame!=previousFrame){group=new();groups.Add(group);previousFrame=hit.Frame;}
            int loss=0;
            foreach(var change in hit.Changes)if(hp.TryGetValue(change.Id,out int before)){loss+=Mathf.Max(0,before-change.Hp);hp[change.Id]=change.Hp;}
            group.Hits.Add((hit,loss));group.Alive=hp.Values.Count(value=>value>0);
        }
        }
        int start=initialAlive<=2?0:groups.FindIndex(g=>g.Alive<=2);
        if(start<0||groups.Count==0)return;
        int damageStart=initialAlive<=2?start:start+1;
        int total=groups.Skip(damageStart).Sum(g=>g.Loss);
        if(total==0&&groups[start].Alive==0){damageStart=start;total=groups[start].Loss;}
        if(total==0)return;
        int spent=0;
        for(int i=start;i<groups.Count;i++){
            var group=groups[i];
            float before=.08f+.92f*spent/total;
            if(i>=damageStart)spent+=group.Loss;
            float after=.08f+.92f*spent/total;
            foreach(var entry in group.Hits){
                _progress[entry.Hit]=(before,after);
                if(i<damageStart)_threshold.Add(entry.Hit);
            }
            if(i<damageStart)_thresholdStarts.Add(group.Hits[^1].Hit);
        }
        if(initialAlive<=2)StartFade();
    }
    public void Preview(OnlineCombatHit hit,float fraction)
    {
        if(!_progress.TryGetValue(hit,out var value))return;
        if(_threshold.Contains(hit))return;
        _intro?.Kill();_intro=null;
        float eased=Mathf.SmoothStep(0,1,Mathf.Clamp(fraction,0,1));
        SetOpacity(Mathf.Max(Opacity,Mathf.Lerp(value.Before,value.After,eased)));
    }
    public void Apply(OnlineCombatHit hit)
    {
        foreach(var state in hit.Changes)if(_liveHp.ContainsKey(state.Id))_liveHp[state.Id]=state.Hp;
        if(_threshold.Contains(hit)){if(_thresholdStarts.Contains(hit)&&Opacity<.08f)StartFade();}
        else Preview(hit,1);
        if(!Eliminated&&Armed&&Opacity>=1&&_liveHp.Values.All(hp=>hp==0)){Eliminated=true;SetOpacity(1);LastUnitDied?.Invoke();}
    }
    public void Complete(bool immediate=false)
    {
        _intro?.Kill();_intro=null;
        if(immediate||_progress.Count>0){SetOpacity(1);return;}
        Image.CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut).TweenProperty(Image,"modulate:a",1f,1.5);
    }
}
