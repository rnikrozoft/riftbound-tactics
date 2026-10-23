using Godot;

public partial class ScrollingDeckName : Control
{
    private readonly Label _label;
    public bool TranslateContent {set{_label.AutoTranslateMode=value?AutoTranslateModeEnum.Inherit:AutoTranslateModeEnum.Disabled;}}
    public bool CenterShortText {get;set;}
    private double _elapsed;
    private float _lastOverflow=-1;
    public ScrollingDeckName():this("",18){}
    public ScrollingDeckName(string text,int fontSize)
    {
        ClipContents=true;MouseFilter=MouseFilterEnum.Ignore;SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _label=DeckMenuUi.Text(text,fontSize);_label.MouseFilter=MouseFilterEnum.Ignore;
        _label.AutowrapMode=TextServer.AutowrapMode.Off;
        AddChild(_label);CustomMinimumSize=new(0,fontSize+10);
    }
    public override void _Process(double delta)
    {
        var minimum=_label.GetCombinedMinimumSize();
        _label.Size=new(minimum.X,Mathf.Max(Size.Y,minimum.Y));
        float overflow=Mathf.Max(0,minimum.X-Size.X);
        if(!Mathf.IsEqualApprox(overflow,_lastOverflow)){_elapsed=0;_lastOverflow=overflow;}
        if(overflow<=0){_label.Position=new(CenterShortText?Mathf.Max(0,(Size.X-minimum.X)/2):0,0);return;}
        _elapsed+=delta;
        const float pause=2,speed=24;
        float travel=overflow/speed;
        double time=_elapsed%(2*pause+2*travel);
        float x=time<pause?0:time<pause+travel?-(float)(time-pause)*speed:time<2*pause+travel?-overflow:-overflow+(float)(time-2*pause-travel)*speed;
        _label.Position=new(x,0);
    }
}
