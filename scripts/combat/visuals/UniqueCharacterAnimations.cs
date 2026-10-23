using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

// Compare rendered pixel sequences, not names or source paths (aliases can use different atlases).
public static class UniqueCharacterAnimations
{
    private static readonly Dictionary<(SpriteFrames,string),string> Signatures=new();
    private static readonly Dictionary<Texture2D,string> FrameSignatures=new();
    public static string Signature(SpriteFrames frames,string animation)
    {
        if(Signatures.TryGetValue((frames,animation),out var cached))return cached;
        var sequence=new List<string>();
        for(int i=0;i<frames.GetFrameCount(animation);i++){
            var texture=frames.GetFrameTexture(animation,i);
            if(!FrameSignatures.TryGetValue(texture,out var signature)){
                using var image=texture.GetImage();if(image.IsCompressed())image.Decompress();image.Convert(Image.Format.Rgba8);
                var pixels=image.GetData();
                // Invisible RGB differences are not distinct poses.
                for(int p=0;p<pixels.Length;p+=4)if(pixels[p+3]==0)pixels[p]=pixels[p+1]=pixels[p+2]=0;
                signature=$"{image.GetWidth()}x{image.GetHeight()}:"+Convert.ToHexString(SHA256.HashData(pixels));FrameSignatures[texture]=signature;
            }
            sequence.Add(signature);
        }
        cached=string.Join("/",sequence);Signatures[(frames,animation)]=cached;return cached;
    }
    public static string[] Names(SpriteFrames frames,IEnumerable<string> candidates)
    {
        var seen=new HashSet<string>();return candidates.Where(n=>frames.HasAnimation(n)&&frames.GetFrameCount(n)>0&&seen.Add(Signature(frames,n))).ToArray();
    }
    public static string Canonical(SpriteFrames frames,string name,IReadOnlyList<string> unique)
    {
        if(frames.HasAnimation(name)){
            string signature=Signature(frames,name);
            foreach(string candidate in unique)if(Signature(frames,candidate)==signature)return candidate;
        }
        return unique[0];
    }
}
