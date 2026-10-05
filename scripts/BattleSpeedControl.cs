using Godot;

public partial class BattleSpeedControl : Node
{
    private static BattleSpeedControl? _active;
    public static int SelectedSpeed {get;private set;}=1;
    private BattleDemo _battle=null!;
    private TextureButton _button=null!;
    public override void _Ready()
    {
        _active=this;ProcessMode=ProcessModeEnum.Always;
        _battle=GetParent().GetNode<BattleDemo>("BattleDemo");
        _button=DeckMenuUi.Button($"{SelectedSpeed}×",CycleSpeed,80,32);_button.Name="BattleSpeedButton";
        _button.TooltipText="ความเร็วการต่อสู้ 1× / 2× / 4×";_button.ProcessMode=ProcessModeEnum.Always;
        GetParent().GetNode<Control>("UI/SafeArea/Content").AddChild(_button);
        _button.AnchorTop=_button.AnchorBottom=1;_button.OffsetLeft=16;_button.OffsetRight=96;_button.OffsetTop=-186;_button.OffsetBottom=-154;
        Apply();
    }
    public void CycleSpeed()
    {
        SelectedSpeed=SelectedSpeed switch {1=>2,2=>4,_=>1};
        _button.GetNode<Label>("Text").Text=$"{SelectedSpeed}×";Apply();
    }
    private void Apply(){if(_active==this)Engine.TimeScale=_battle.SpeedEnabled?SelectedSpeed:1;}
    public override void _Process(double delta)=>Apply();
    public override void _ExitTree(){if(_active==this){_active=null;Engine.TimeScale=1;}}
}
