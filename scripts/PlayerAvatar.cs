using Godot;
using System;
using System.Collections.Generic;

public static class AvatarProfiles
{
    private sealed record Profile(string FacebookId,string Url);
    private static readonly Dictionary<string,Profile> Profiles=new();
    private static readonly Dictionary<string,Texture2D> Pictures=new();
    private static readonly HashSet<string> Requested=new();
    public static event Action<string>? Changed;
    public static Texture2D Default=>GD.Load<Texture2D>("res://icon.svg");
    public static bool CanUseFacebookPhoto(string userId,string facebookId,string url)=>!string.IsNullOrWhiteSpace(userId)&&!userId.StartsWith("bot:")&&!userId.StartsWith("ghost:")&&!string.IsNullOrWhiteSpace(facebookId)&&Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https";
    public static void Update(Nakama.IApiUser user)=>Set(user.Id,user.FacebookId,user.AvatarUrl);
    public static void Set(string userId,string facebookId,string url)
    {
        var profile=new Profile(facebookId??"",url??"");
        if(Profiles.TryGetValue(userId,out var old)&&old==profile)return;
        Profiles[userId]=profile;Changed?.Invoke(userId);
    }
    public static bool Connected(string userId)=>Profiles.TryGetValue(userId,out var p)&&!string.IsNullOrWhiteSpace(p.FacebookId);
    public static Texture2D Picture(string userId)
    {
        if(!Profiles.TryGetValue(userId,out var p)||!CanUseFacebookPhoto(userId,p.FacebookId,p.Url))return Default;
        if(Pictures.TryGetValue(p.Url,out var image))return image;
        if(Requested.Add(p.Url)&&Engine.GetMainLoop() is SceneTree tree){
            var request=new HttpRequest {Timeout=8,BodySizeLimit=4*1024*1024};tree.Root.AddChild(request);
            request.RequestCompleted+=(result,code,headers,body)=>{
                if(result==(long)HttpRequest.Result.Success&&code==200){
                    var texture=Decode(body);if(texture!=null){Pictures[p.Url]=texture;foreach(var pair in Profiles)if(pair.Value.Url==p.Url)Changed?.Invoke(pair.Key);}
                }
                request.QueueFree();
            };
            if(request.Request(p.Url)!=Error.Ok)request.QueueFree();
        }
        return Default;
    }
    public static Texture2D? Decode(byte[] bytes)
    {
        var image=new Image();var error=image.LoadPngFromBuffer(bytes);if(error!=Error.Ok)error=image.LoadJpgFromBuffer(bytes);if(error!=Error.Ok)error=image.LoadWebpFromBuffer(bytes);
        if(error!=Error.Ok||image.GetWidth()>8192||image.GetHeight()>8192)return null;
        if(image.GetWidth()>512||image.GetHeight()>512){float scale=512f/Math.Max(image.GetWidth(),image.GetHeight());image.Resize(Math.Max(1,(int)(image.GetWidth()*scale)),Math.Max(1,(int)(image.GetHeight()*scale)));}
        return ImageTexture.CreateFromImage(image);
    }
}
public partial class PlayerAvatar : TextureRect
{
    public string UserId {get;private set;}="";
    public PlayerAvatar(){Texture=AvatarProfiles.Default;ExpandMode=ExpandModeEnum.IgnoreSize;StretchMode=StretchModeEnum.KeepAspectCentered;TextureFilter=TextureFilterEnum.Linear;MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Ready(){AvatarProfiles.Changed+=Changed;Refresh();}
    public void Bind(string userId){UserId=userId??"";Refresh();}
    private void Changed(string userId){if(userId==UserId)Refresh();}
    private void Refresh()=>Texture=AvatarProfiles.Picture(UserId);
    public override void _ExitTree()=>AvatarProfiles.Changed-=Changed;
}
