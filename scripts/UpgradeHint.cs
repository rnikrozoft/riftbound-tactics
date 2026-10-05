using Godot;

public static class UpgradeHint
{
    private static SpriteFrames? _frames;
    public static void Set(Node parent, bool enabled)
    {
        var effect=parent.GetNodeOrNull<AnimatedSprite2D>("UpgradeHint");
        if(!enabled) { if(effect!=null) {effect.Hide();effect.Stop();} return; }
        if(effect==null) {
            if(_frames==null) {
                _frames=new SpriteFrames();_frames.SetAnimationLoopMode("default",SpriteFrames.LoopMode.Linear);_frames.SetAnimationSpeed("default",24);
                for(int i=0;;i++) {
                    string path=$"res://assets/effects/pixel/frames/fantasy_spells/status_sparkling_001/status_sparkling_001_small_yellow/frame{i:0000}.png";
                    if(!ResourceLoader.Exists(path))break;
                    _frames.AddFrame("default",GD.Load<Texture2D>(path));
                }
            }
            effect=new AnimatedSprite2D {Name="UpgradeHint",SpriteFrames=_frames,ZIndex=25,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};
            parent.AddChild(effect);
            if(parent is Control card) { var particle=effect;card.Resized+=()=>particle.Position=card.Size*.5f; }
        }
        effect.Position=parent is Control control ? control.Size*.5f : new Vector2(0,-16);
        effect.Show();if(!effect.IsPlaying())effect.Play();
    }
}
