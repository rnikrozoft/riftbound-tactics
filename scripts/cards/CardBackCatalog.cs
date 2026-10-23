using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class CardBackDefinition
{
    [JsonPropertyName("id")] public string Id {get;set;}="";
    [JsonPropertyName("name")] public string Name {get;set;}="";
    [JsonPropertyName("price")] public long Price {get;set;}
    [JsonPropertyName("texture_path")] public string TexturePath {get;set;}="";
    [JsonPropertyName("x")] public int X {get;set;}
    [JsonPropertyName("y")] public int Y {get;set;}
    [JsonPropertyName("width")] public int Width {get;set;}
    [JsonPropertyName("height")] public int Height {get;set;}
}
public static class CardBackCatalog
{
    public const string DefaultId="card_back_00";
    private static readonly CardBackDefinition[] Local=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://data/card_backs.json"),GameJsonContext.Default.CardBackDefinitionArray)!;
    public static CardBackDefinition[] All=>PlayerInventory.Configuration?.CardBacks is {Length:>0} backs?backs:Local;
    public static string Normalize(string? id)=>string.IsNullOrEmpty(id)?DefaultId:id;
    public static CardBackDefinition? Find(string? id)=>All.FirstOrDefault(b=>b.Id==Normalize(id));
    public static AtlasTexture Texture(string? id)
    {
        var back=Find(id)??Find(DefaultId)!;
        return new(){Atlas=GD.Load<Texture2D>(back.TexturePath),Region=new(back.X,back.Y,back.Width,back.Height)};
    }
    public static TextureRect Preview(string? id,int width=72,int height=96)
    {
        var card=new TextureRect {Texture=Texture(id),CustomMinimumSize=new(width,height),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};
        CardSheen.Attach(card);return card;
    }

}
public sealed class CardBackPurchaseRequest
{
    [JsonPropertyName("card_back_id")] public string CardBackId {get;set;}="";
    [JsonPropertyName("request_id")] public string RequestId {get;set;}="";
    [JsonPropertyName("config_version")] public string ConfigVersion {get;set;}="";
}
