using Godot;
using System.Linq;

public partial class CombatStatusPanel : PanelContainer
{
    private Node _field=null!;
    private VBoxContainer _rows=null!;
    private int _mask=-1,_count;
    public int StatusCount=>_count;
    public override void _Ready()
    {
        _field=GetParent().GetParent().GetParent().GetParent();
        Name="CombatStatusPanel";TextureFilter=TextureFilterEnum.Nearest;
        AnchorLeft=AnchorRight=1;OffsetLeft=-244;OffsetRight=-16;OffsetTop=64;
        AddThemeStyleboxOverride("panel",GameUi.Box(false,10));
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",8);AddChild(box);
        var scroll=new ScrollContainer {Name="StatusScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill};box.AddChild(scroll);
        _rows=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};_rows.AddThemeConstantOverride("separation",6);scroll.AddChild(_rows);
        Resized+=LayoutPanel;Refresh();
    }
    public override void _Process(double delta){Refresh();LayoutPanel();}
    private void Refresh()
    {
        int mask=0;var units=_field.GetChildren().OfType<BattleUnit>().Where(u=>!u.IsQueuedForDeletion()).ToArray();
        for(int i=0;i<CombatStatuses.All.Length;i++)if(units.Any(u=>CombatStatuses.Active(u,CombatStatuses.All[i].Key)))mask|=1<<i;
        if(mask==_mask)return;_mask=mask;_count=0;DeckMenuUi.Clear(_rows);
        for(int i=0;i<CombatStatuses.All.Length;i++)if((mask&(1<<i))!=0){
            _count++;var status=CombatStatuses.All[i];
            var row=new HBoxContainer {Name=status.Key,CustomMinimumSize=new(0,54)};row.AddThemeConstantOverride("separation",8);_rows.AddChild(row);
            row.AddChild(new TextureRect {Texture=CombatStatuses.Texture(status),CustomMinimumSize=new(32,32),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore});
            var copy=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};copy.AddThemeConstantOverride("separation",2);row.AddChild(copy);
            copy.AddChild(DeckMenuUi.Text(status.Title,12));var description=DeckMenuUi.Text(status.Description,11);description.AutowrapMode=TextServer.AutowrapMode.WordSmart;copy.AddChild(description);
        }
        Visible=_count>0;LayoutPanel();
    }
    private void LayoutPanel()
    {
        if(_rows==null)return;
        float available=Mathf.Max(90,GetParent<Control>().Size.Y-64-160);
        OffsetBottom=64+Mathf.Min(available,20+_count*60);
    }
}
