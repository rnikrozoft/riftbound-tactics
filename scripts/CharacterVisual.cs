using Godot;
using System.Collections.Generic;

public static class CharacterVisual
{
    private static readonly Dictionary<Texture2D, Rect2I> Bounds = new();
    private static readonly Dictionary<Texture2D, Vector2> Heads = new();
    public static Vector2 HeadAnchor(AnimatedSprite2D sprite)
    {
        var frame=sprite.SpriteFrames.GetFrameTexture(BattleAnimations.Idle,0);
        if(!Heads.TryGetValue(frame,out var head)) {
            var bounds=VisibleBounds(frame);
            // Use the top of the figure, excluding weapons and wide lower bodies.
            int height=Mathf.Max(1,Mathf.RoundToInt(bounds.Size.Y*.2f));
            using var image=frame.GetImage();
            using var top=image.GetRegion(new Rect2I(0,bounds.Position.Y,frame.GetWidth(),height));
            var headBounds=top.GetUsedRect();
            head=new Vector2(headBounds.Position.X+headBounds.Size.X/2f,bounds.Position.Y);
            Heads[frame]=head;
        }
        var relative=head-new Vector2(frame.GetWidth()/2f,frame.GetHeight()/2f);
        if(sprite.FlipH)relative.X=-relative.X;
        if(sprite.FlipV)relative.Y=-relative.Y;
        return sprite.Position+(sprite.Offset+relative)*sprite.Scale;
    }
    public static Rect2I VisibleBounds(Texture2D texture)
    {
        if (!Bounds.TryGetValue(texture, out var bounds)) {
            using var image = texture.GetImage();
            bounds = image.GetUsedRect();
            Bounds[texture] = bounds;
        }
        return bounds;
    }

    public static AtlasTexture Portrait(Texture2D atlas, Rect2 region)
    {
        var crop = new AtlasTexture { Atlas = atlas, Region = region };
        using var image = crop.GetImage();
        var bounds = image.GetUsedRect();
        if (bounds.Size.Y == 0) return crop;
        crop.Region = new Rect2(region.Position + bounds.Position, bounds.Size);
        // Fit tall and wide figures in a common card canvas without clipping.
        float height = Mathf.Max(bounds.Size.Y, Mathf.Ceil(bounds.Size.X / 1.5f));
        float width = Mathf.Ceil(height * 1.5f);
        crop.Margin = new Rect2((width - bounds.Size.X) / 2, (height - bounds.Size.Y) / 2, width - bounds.Size.X, height - bounds.Size.Y);
        return crop;
    }

    public static void Normalize(AnimatedSprite2D sprite)
    {
        var frame = sprite.SpriteFrames.GetFrameTexture(BattleAnimations.Idle, 0);
        var bounds = VisibleBounds(frame);
        if (bounds.Size.Y == 0) return;
        float scale = 31.5f / bounds.Size.Y;
        sprite.Scale = new Vector2(scale, scale);
        sprite.Offset = new Vector2(frame.GetWidth() / 2f - bounds.Position.X - bounds.Size.X / 2f,
            frame.GetHeight() / 2f - bounds.End.Y);
    }
}
