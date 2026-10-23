using Godot;
using System.Linq;

// Plays the original card-pack sheen frames; the shader clips light to the card art.
public partial class CardSheen : TextureRect
{
    private static readonly Texture2D[] Frames=Enumerable.Range(0,10).Select(i=>GD.Load<Texture2D>($"res://assets/effects/cards/sheen/sheen__{i:0000}_sheen0{i+1}.png")).ToArray();
    private TextureRect _card=null!;
    private Texture2D? _mask;
    private int _frame=-1;
    private double _elapsed;
    public static void Attach(TextureRect card)
    {
        if(card.GetNodeOrNull<CardSheen>("CardSheen")!=null)return;
        var sheen=new CardSheen {Name="CardSheen",MouseFilter=MouseFilterEnum.Ignore,ExpandMode=ExpandModeEnum.IgnoreSize,StretchMode=StretchModeEnum.Scale,TextureFilter=TextureFilterEnum.Nearest};
        sheen._card=card;
        sheen.Material=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/card_sheen.gdshader")};
        card.AddChild(sheen);card.Resized+=sheen.Layout;sheen.Layout();
    }
    private void Layout()
    {
        if(_card.Texture==null)return;
        var textureSize=_card.Texture.GetSize();
        float fit=Mathf.Min(_card.Size.X/textureSize.X,_card.Size.Y/textureSize.Y);
        Size=_card.StretchMode==StretchModeEnum.Scale?_card.Size:textureSize*fit;Position=(_card.Size-Size)/2;
    }
    public override void _Process(double delta)
    {
        if(!IsVisibleInTree())return;
        if(_mask!=_card.Texture){
            _mask=_card.Texture;
            var material=(ShaderMaterial)Material;
            if(_mask is AtlasTexture atlas){
                var size=atlas.Atlas.GetSize();
                material.SetShaderParameter("card_mask",atlas.Atlas);
                material.SetShaderParameter("mask_rect",new Vector4(atlas.Region.Position.X/size.X,atlas.Region.Position.Y/size.Y,atlas.Region.Size.X/size.X,atlas.Region.Size.Y/size.Y));
            }else {material.SetShaderParameter("card_mask",_mask);material.SetShaderParameter("mask_rect",new Vector4(0,0,1,1));}
            Layout();
        }
        _elapsed=(_elapsed+delta)%3.8;
        int frame=(int)(_elapsed/.08);
        if(frame==_frame)return;
        _frame=frame;
        Texture=frame<Frames.Length?Frames[frame]:null;
    }
}
