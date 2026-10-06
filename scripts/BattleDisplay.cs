using Godot;
using System;
using System.Threading.Tasks;

public partial class BattleDisplay : Node2D
{
    // Reserved for future trap/effect cards; combat deployment remains six slots.
    public static readonly Vector2I[] SupportCells = {new(37,14),new(37,16),new(37,18),new(37,20),new(37,22)};
    public static readonly Vector2I[] EnemySupportCells = {new(65,14),new(65,16),new(65,18),new(65,20),new(65,22)};
    private Vector2 _arenaOffset;
    public Vector2 ArenaOffset
    {
        get => _arenaOffset;
        set { Position += value - _arenaOffset; _arenaOffset = value; }
    }

    private CanvasLayer? _phaseLayer;
    private ColorRect? _phaseShade;
    private Label? _phaseText;
    private Tween? _phaseTween;
    public bool PhaseTransitionActive=>_phaseShade!=null&&GodotObject.IsInstanceValid(_phaseShade)&&_phaseShade.Visible;
    public async Task WaitForPhaseTransition()
    {
        while(GodotObject.IsInstanceValid(this)&&IsInsideTree()&&PhaseTransitionActive)
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(!GodotObject.IsInstanceValid(this)||!IsInsideTree())throw new OperationCanceledException();
    }
    public void ShowPhaseTransition(string phase,int round)
    {
        if(_phaseLayer==null){
            _phaseLayer=new CanvasLayer {Name="PhaseTransition",Layer=25};AddChild(_phaseLayer);
            _phaseShade=new ColorRect {Color=new Color(.025f,.04f,.06f,.8f),MouseFilter=Control.MouseFilterEnum.Ignore};_phaseLayer.AddChild(_phaseShade);_phaseShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _phaseText=new Label {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(_phaseText,32);_phaseShade.AddChild(_phaseText);_phaseText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        _phaseTween?.Kill();_phaseText!.Text=phase=="battle"?"BATTLE":$"ROUND {round}  /  PREPARATION";
        _phaseShade!.Show();_phaseShade.Modulate=new Color(1,1,1,0);
        _phaseTween=CreateTween().SetIgnoreTimeScale(true).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _phaseTween.TweenProperty(_phaseShade,"modulate:a",1f,.12);
        _phaseTween.TweenInterval(1);
        _phaseTween.TweenProperty(_phaseShade,"modulate:a",0f,.32);
        _phaseTween.Finished+=()=>_phaseShade?.Hide();
    }
    public override void _ExitTree(){_phaseTween?.Kill();}
    public override void _Ready()
    {
        var tiles=GetNode<TileMapLayer>("TileMapLayer");
        tiles.Material=new ShaderMaterial {Shader=new Shader {Code="""
            shader_type canvas_item;
            void fragment() {
                vec4 pixel = COLOR;
                if (pixel.r > pixel.b * 1.15 && pixel.g > pixel.b * 1.05) {
                    float light = dot(pixel.rgb, vec3(0.299, 0.587, 0.114));
                    pixel.rgb = vec3(0.43, 0.57, 0.70) * light;
                }
                COLOR = pixel;
            }
            """}};
        AddHeroSlots(tiles,"HeroSlots",CardShop.DeploymentCells,new Color("79d9eb"),new Color("304c59"));
        AddHeroSlots(tiles,"EnemyHeroSlots",CardShop.EnemyCells,new Color("d99d83"),new Color("59413c"));
        var support=new Node2D {Name="SupportSlots",ZIndex=1};AddChild(support);
        AddSupportSlots(support,tiles,SupportCells);
        var enemySupport=new Node2D {Name="EnemySupportSlots",ZIndex=1};AddChild(enemySupport);
        AddSupportSlots(enemySupport,tiles,EnemySupportCells);
        var content=GetNode<Control>("UI/SafeArea/Content");
        content.AddChild(new CombatStatusPanel());
        AddChild(new BattleSpeedControl {Name="BattleSpeedControl"});
        var result=content.GetNode<Control>("BattleResult");
        result.AnchorTop=result.AnchorBottom=.5f;result.OffsetTop=-54;result.OffsetBottom=54;result.OffsetLeft=-230;result.OffsetRight=230;
        result.GetNode<TextureRect>("Banner").Hide();GameUi.Backdrop(result,true);GameUi.Label(result.GetNode<Label>("Text"),26);result.GetNode<Label>("Text").OffsetTop=20;
        var heading=new Label {Name="Heading",Text="BATTLE RESULT",AnchorRight=1,OffsetTop=12,OffsetBottom=32,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(heading,12);heading.AddThemeColorOverride("font_color",GameUi.Gold);result.AddChild(heading);
        if (OS.GetName() is "Windows" or "Linux" or "macOS")
        {
            var window = GetWindow();
            window.Unresizable = false;
            window.MaximizeDisabled = false;
            window.MinSize = new Vector2I(640, 360);
            window.MaxSize = Vector2I.Zero;
        }
    }
    private void AddHeroSlots(TileMapLayer tiles,string name,Vector2I[] cells,Color border,Color stone)
    {
        var layer=new Node2D {Name=name,ZIndex=1};AddChild(layer);
        foreach(var cell in cells){
            var slot=new Node2D {Position=ToLocal(tiles.ToGlobal(tiles.MapToLocal(cell)))};layer.AddChild(slot);
            var edge=new[]{new Vector2(-19,0),new Vector2(0,-9),new Vector2(19,0),new Vector2(0,9)};
            var inset=new[]{new Vector2(-15,0),new Vector2(0,-7),new Vector2(15,0),new Vector2(0,7)};
            slot.AddChild(new Polygon2D {Polygon=edge,Color=new Color("182b34")});
            slot.AddChild(new Polygon2D {Polygon=inset,Color=stone});
            slot.AddChild(new Line2D {Points=edge,Closed=true,Width=1,DefaultColor=border,Antialiased=false});
            slot.AddChild(new Line2D {Points=inset,Closed=true,Width=1,DefaultColor=new Color(border.R,border.G,border.B,.35f),Antialiased=false});
            var rune=new[]{new Vector2(-4,0),new Vector2(0,-2),new Vector2(4,0),new Vector2(0,2)};
            slot.AddChild(new Line2D {Points=rune,Closed=true,Width=1,DefaultColor=border,Antialiased=false});
        }
    }
    private static void AddSupportSlots(Node2D parent,TileMapLayer tiles,Vector2I[] cells)
    {
        foreach(var cell in cells) {
            var slot=new Node2D {Position=tiles.Position+tiles.MapToLocal(cell)};parent.AddChild(slot);
            var points=new[]{new Vector2(-19,0),new Vector2(0,-9),new Vector2(19,0),new Vector2(0,9)};
            slot.AddChild(new Polygon2D {Polygon=points,Color=new Color(.13f,.2f,.23f,.65f)});
            slot.AddChild(new Line2D {Points=points,Closed=true,Width=1,DefaultColor=new Color(.72f,.61f,.4f,.8f)});
        }
    }
}
