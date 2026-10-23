using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public sealed class TranslationDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}=1;
    [JsonPropertyName("messages")] public List<string[]> Messages {get;set;}=new();
    [JsonPropertyName("fragments")] public List<string[]> Fragments {get;set;}=new();
    public void Validate()
    {
        if(SchemaVersion!=1||Messages==null||Fragments==null||Messages.Count==0||Fragments.Count==0||Messages.Count+Fragments.Count>20000)throw new Exception("Invalid translation catalog");
        foreach(var row in Messages.Concat(Fragments)){
            if(row==null||row.Length is not (2 or 4)||row.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>16000))throw new Exception("Invalid translation row");
            string Args(string s)=>string.Join(",",Regex.Matches(s,@"\{\d+\}").Select(m=>m.Value).Distinct().OrderBy(x=>x));
            if(row.Any(s=>Args(s)!=Args(row[0])))throw new Exception("Invalid translation placeholders");
        }
    }
}
public sealed class LocalizationRequest {[JsonPropertyName("cached_version")] public string CachedVersion {get;set;}="";}
public sealed class LocalizationResponse
{
    [JsonPropertyName("version")] public string Version {get;set;}="";
    [JsonPropertyName("document")] public TranslationDocument? Document {get;set;}
}
public sealed class CachedLocalization
{
    public string Server {get;set;}="";
    public string Version {get;set;}="";
    public string Checksum {get;set;}="";
    public TranslationDocument Document {get;set;}=new();
}
public static class LocalizationCache
{
    internal static string? TestDirectory;
    public static string CurrentVersion {get;private set;}="";
    public static string LastError {get;private set;}="";
    private static string Hash(string text)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string Path(string host)=>TestDirectory!=null?System.IO.Path.Combine(TestDirectory,Hash(host)+".json"):ProjectSettings.GlobalizePath("user://localization-"+Hash(host)+".json");
    private static string Checksum(TranslationDocument doc)=>Hash(JsonSerializer.Serialize(doc,GameJsonContext.Default.TranslationDocument));
    public static CachedLocalization? Read(string host)
    {
        try{
            string path=Path(host);if(!System.IO.File.Exists(path))return null;
            if(new System.IO.FileInfo(path).Length>8*1024*1024)throw new Exception("Translation cache too large");
            var cache=JsonSerializer.Deserialize(System.IO.File.ReadAllText(path),GameJsonContext.Default.CachedLocalization);
            if(cache==null||cache.Server!=host||!Regex.IsMatch(cache.Version,"^[a-f0-9]{64}$")||cache.Checksum!=Checksum(cache.Document))return null;
            cache.Document.Validate();return cache;
        }catch(Exception e){LastError=e.Message;return null;}
    }
    public static void LoadCached(string host)
    {
        var cache=Read(host);CurrentVersion=cache?.Version??"";
        Localization.ApplyDocument(cache?.Document??Localization.BundledDocument());
    }
    public static bool Store(string host,string version,TranslationDocument doc)
    {
        doc.Validate();if(!Regex.IsMatch(version,"^[a-f0-9]{64}$"))throw new Exception("Invalid translation version");
        try{
            var cache=new CachedLocalization {Server=host,Version=version,Document=doc,Checksum=Checksum(doc)};
            string path=Path(host);System.IO.File.WriteAllText(path+".tmp",JsonSerializer.Serialize(cache,GameJsonContext.Default.CachedLocalization));System.IO.File.Move(path+".tmp",path,true);return true;
        }catch(Exception e){LastError=e.Message;GD.Print("Localization cache: "+e.Message);return false;}
    }
    public static async Task Sync(NakamaConnection connection)
    {
        string host=connection.Client.Scheme+"://"+connection.Client.Host+":"+connection.Client.Port;
        LoadCached(host);LastError="";
        try{
            var request=new LocalizationRequest {CachedVersion=CurrentVersion};
            var rpc=await connection.Client.RpcAsync(connection.Session,"localization_bootstrap",JsonSerializer.Serialize(request,GameJsonContext.Default.LocalizationRequest));
            var response=JsonSerializer.Deserialize(rpc.Payload,GameJsonContext.Default.LocalizationResponse)??throw new Exception("Missing translation response");
            if(response.Document==null){if(CurrentVersion==""||CurrentVersion!=response.Version)throw new Exception("Translation cache version mismatch");return;}
            response.Document.Validate();if(!Regex.IsMatch(response.Version,"^[a-f0-9]{64}$"))throw new Exception("Invalid translation version");
            Localization.ApplyDocument(response.Document);CurrentVersion=response.Version;Store(host,response.Version,response.Document);
        }catch(Exception e){LastError=e.Message;GD.Print("Localization: using cached or bundled text. "+e.Message);}
    }
}
