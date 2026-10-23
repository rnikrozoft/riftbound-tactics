using Godot;

public static class UpgradeHint
{
    private static SpriteFrames? _frames;
    public static void Set(Node parent, bool enabled)
    {
        UpgradeCardFrame.Set(parent,enabled);
        var effect=parent.GetNodeOrNull<AnimatedSprite2D>("UpgradeHint");
        if(effect==null && enabled){
            if(_frames==null){
                _frames=new SpriteFrames();_frames.SetAnimationSpeed("default",18);
                var sheet=GD.Load<Texture2D>("res://assets/effects/pixel/spritesheets/fantasy_spells/status_sparkling_001/status_sparkling_001_small_yellow/spritesheet.png");
                for(int i=0;i<24;i++)_frames.AddFrame("default",new AtlasTexture {Atlas=sheet,Region=new Rect2(i%5*48,i/5*48,48,48)});
            }
            effect=new AnimatedSprite2D {Name="UpgradeHint",SpriteFrames=_frames,ZIndex=25,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};parent.AddChild(effect);
        }
        if(effect==null)return;
        effect.Visible=enabled;
        if(!enabled){effect.Stop();return;}
        if(parent is Control card){effect.Position=card.Size/2;effect.Scale=card.Size/48;}
        else if(parent is BattleUnit unit){effect.Position=CharacterVisual.GroundAnchor(unit.Sprite)+new Vector2(0,-16);effect.Scale=Vector2.One;}
        if(!effect.IsPlaying())effect.Play();
    }
}
