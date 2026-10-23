using Godot;

public partial class MatchupDonut : Control
{
    private readonly int[] _values;
    private readonly Color[] _colors={new("6bddc5"),new("e8ba65"),new("e17e8f")};
    public MatchupDonut():this(0,0,0){}
    public MatchupDonut(int wins,int draws,int losses)
    {
        _values=new[]{wins,draws,losses};CustomMinimumSize=new(0,124);MouseFilter=MouseFilterEnum.Ignore;Resized+=QueueRedraw;
    }
    public override void _Notification(int what){if(what==NotificationTranslationChanged)QueueRedraw();}
    public override void _Draw()
    {
        var center=new Vector2(63,61);var font=ThemeDB.FallbackFont;
        int total=_values[0]+_values[1]+_values[2];float angle=-Mathf.Pi/2;
        DrawArc(center,46,0,Mathf.Tau,96,new Color("29394a"),15,true);
        for(int i=0;i<3;i++){
            float sweep=total>0?Mathf.Tau*_values[i]/total:0;
            if(sweep>0)DrawArc(center,46,angle+.012f,angle+sweep-.012f,64,_colors[i],15,true);
            angle+=sweep;
            float y=30+i*29;DrawCircle(new Vector2(137,y-4),4,_colors[i]);
            string label=Localization.Text(new[]{"Wins","Draws","Losses"}[i]);
            DrawString(font,new Vector2(149,y),label,HorizontalAlignment.Left,-1,13,new Color("dbe3ec"));
            DrawString(font,new Vector2(Size.X-66,y),$"{_values[i]}",HorizontalAlignment.Right,62,13,new Color("dbe3ec"));
        }
        string rate=total>0?$"{100d*_values[0]/total:0}%":"—";
        float width=font.GetStringSize(rate,fontSize:23).X;
        DrawString(font,center+new Vector2(-width/2,2),rate,HorizontalAlignment.Left,-1,23,new Color("f2ebd5"));
        string caption=Localization.Text("win rate");float captionWidth=font.GetStringSize(caption,fontSize:12).X;
        DrawString(font,center+new Vector2(-captionWidth/2,21),caption,HorizontalAlignment.Left,-1,12,new Color("93a4b4"));
    }
}
