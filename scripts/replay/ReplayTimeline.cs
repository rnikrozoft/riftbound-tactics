using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ReplayTimeline : Control
{
    private sealed record Point(int Round,bool Preparation,Vector2 Position);
    private readonly List<Point> _points=new();
    private readonly List<Point> _naturalPoints=new();
    private readonly List<(int Round,float X,float? BattleX,bool Preparation)> _labels=new();
    private readonly List<(int Round,float X,float? BattleX,bool Preparation)> _naturalLabels=new();
    private int _selected=-1;
    public int PointCount=>_points.Count;
    public event Action<int,bool,int>? PointSelected;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Stop;FocusMode=FocusModeEnum.All;MouseDefaultCursorShape=CursorShape.PointingHand;SizeFlagsHorizontal=SizeFlags.ExpandFill;Resized+=LayoutPoints;}
    public void Build(int[] rounds)
    {
        _naturalPoints.Clear();_naturalLabels.Clear();float x=20;
        foreach(int round in rounds){
            int steps=MatchReplayStore.Preparations.Count(s=>s.Round==round);
            bool hasBattle=MatchReplayStore.Rounds.Any(s=>s.Round==round);
            if(steps==0&&!hasBattle)continue;
            if(steps>0)_naturalPoints.Add(new Point(round,true,new Vector2(x+26,32)));
            float battle=steps>0?x+130:x+26;
            if(hasBattle)_naturalPoints.Add(new Point(round,false,new Vector2(battle,32)));
            _naturalLabels.Add((round,x,hasBattle?battle:null,steps>0));x=hasBattle?battle+68:x+130;
        }
        CustomMinimumSize=new Vector2(Math.Max(200,x),64);LayoutPoints();
    }
    private void LayoutPoints()
    {
        _points.Clear();_labels.Clear();
        if(_naturalPoints.Count==0){QueueRedraw();return;}
        float width=Math.Max(Size.X,CustomMinimumSize.X);
        float first=_naturalPoints[0].Position.X,last=_naturalPoints[^1].Position.X;
        // Keep labels inside the panel while spreading spare space across every recorded point.
        float Map(float x)=>last>first?46+(x-first)*(width-92)/(last-first):width/2+(x-first);
        foreach(var point in _naturalPoints)_points.Add(point with {Position=new Vector2(Map(point.Position.X),32)});
        foreach(var label in _naturalLabels)_labels.Add((label.Round,Map(label.X+26)-26,label.BattleX is float battle?Map(battle):null,label.Preparation));
        QueueRedraw();
    }
    public Vector2 PointPosition(int round,bool preparation)=>_points.First(p=>p.Round==round&&p.Preparation==preparation).Position;
    public void Select(int round,bool preparation)
    {
        _selected=_points.FindIndex(p=>p.Round==round&&p.Preparation==preparation);QueueRedraw();
        if(_selected>=0&&GetParent() is ScrollContainer scroll)scroll.ScrollHorizontal=Math.Max(0,(int)(_points[_selected].Position.X-scroll.Size.X/2));
    }
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true} mouse){
            int nearest=-1;float distance=13;
            for(int i=0;i<_points.Count;i++){float d=mouse.Position.DistanceTo(_points[i].Position);if(d<distance){nearest=i;distance=d;}}
            if(nearest>=0){Activate(nearest);AcceptEvent();}
        }else if(input is InputEventKey {Pressed:true} key&&(key.Keycode==Key.Left||key.Keycode==Key.Right)){
            Activate(Math.Clamp(_selected+(key.Keycode==Key.Left?-1:1),0,Math.Max(0,_points.Count-1)));AcceptEvent();
        }
    }
    private void Activate(int index){if(index<0||index>=_points.Count)return;var point=_points[index];PointSelected?.Invoke(point.Round,point.Preparation,0);}
    public override void _Draw()
    {
        var font=GetThemeDefaultFont();
        if(_points.Count>1)DrawLine(_points[0].Position,_points[^1].Position,GameUi.Border,2);
        foreach(var label in _labels){
            DrawString(font,new Vector2(label.X,13),$"ROUND {label.Round}",fontSize:11,modulate:GameUi.Text);
            if(label.Preparation)DrawString(font,new Vector2(label.X+2,58),"PREPARATION",fontSize:10,modulate:GameUi.Teal);
            if(label.BattleX is float battle)DrawString(font,new Vector2(battle-18,58),"BATTLE",fontSize:10,modulate:GameUi.Gold);
        }
        for(int i=0;i<_points.Count;i++){
            var point=_points[i];Color color=point.Preparation?GameUi.Teal:GameUi.Gold;
            if(i==_selected)DrawCircle(point.Position,10,new Color(color.R,color.G,color.B,.22f));
            if(point.Preparation)DrawCircle(point.Position,6,color);
            else DrawColoredPolygon(new[]{point.Position+new Vector2(0,-7),point.Position+new Vector2(7,0),point.Position+new Vector2(0,7),point.Position+new Vector2(-7,0)},color);
            if(i==_selected)DrawArc(point.Position,10,0,Mathf.Tau,24,Colors.White,1);
        }
    }
}
