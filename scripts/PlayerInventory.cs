using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class EconomySettings
{
    [JsonPropertyName("all_unlocked")] public bool AllUnlocked {get;set;}
    [JsonPropertyName("currency")] public string Currency {get;set;}="coins";
    [JsonPropertyName("prices")] public Dictionary<string,long> Prices {get;set;}=new();
}
public sealed class GameConfiguration
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}
    [JsonPropertyName("catalog")] public CharacterCatalogData Catalog {get;set;}=new();
    [JsonPropertyName("economy")] public EconomySettings Economy {get;set;}=new();
}
public sealed class StoredCard
{
    [JsonPropertyName("character_id")] public string CharacterId {get;set;}="";
    [JsonPropertyName("copies")] public int Copies {get;set;}
}
public sealed class StoredDeck
{
    [JsonPropertyName("id")] public string Id {get;set;}="";
    [JsonPropertyName("name")] public string Name {get;set;}="";
    [JsonPropertyName("heroes")] public int[] Heroes {get;set;}=Array.Empty<int>();
    [JsonPropertyName("cards")] public StoredCard[] Cards {get;set;}=Array.Empty<StoredCard>();
    public static StoredDeck From(DeckDefinition deck)=>new(){Id=deck.Id,Name=deck.Name,Heroes=deck.Heroes.ToArray(),Cards=deck.Cards.Select(c=>new StoredCard {CharacterId=CharacterData.Get(c.Kind).Id,Copies=c.Copies}).ToArray()};
    public DeckDefinition ToDeck()=>new(){Id=Id,Name=Name,Heroes=Heroes.ToArray(),Cards=Cards.Select(c=>new DeckEntry {Kind=CharacterData.All.FirstOrDefault(d=>d.Id==c.CharacterId)?.Kind??throw new Exception("Unknown owned character: "+c.CharacterId),Copies=c.Copies}).ToList()};
}
public sealed class PlayerProfileData
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}
    [JsonPropertyName("owned_characters")] public string[] OwnedCharacters {get;set;}=Array.Empty<string>();
    [JsonPropertyName("decks")] public StoredDeck[] Decks {get;set;}=Array.Empty<StoredDeck>();
    [JsonPropertyName("selected_deck_id")] public string SelectedDeckId {get;set;}="";
}
public sealed class PlayerBootstrapResponse
{
    [JsonPropertyName("user_id")] public string UserId {get;set;}="";
    [JsonPropertyName("config_version")] public string ConfigVersion {get;set;}="";
    [JsonPropertyName("config")] public GameConfiguration? Config {get;set;}
    [JsonPropertyName("profile")] public PlayerProfileData Profile {get;set;}=new();
    [JsonPropertyName("profile_version")] public string ProfileVersion {get;set;}="";
    [JsonPropertyName("wallet")] public Dictionary<string,long> Wallet {get;set;}=new();
}
public sealed class PlayerBootstrapRequest
{
    [JsonPropertyName("cached_config_version")] public string CachedConfigVersion {get;set;}="";
    [JsonPropertyName("legacy_decks")] public StoredDeck[] LegacyDecks {get;set;}=Array.Empty<StoredDeck>();
    [JsonPropertyName("legacy_selected_id")] public string LegacySelectedId {get;set;}="";
}
public sealed class PlayerDeckSaveRequest
{
    [JsonPropertyName("decks")] public StoredDeck[] Decks {get;set;}=Array.Empty<StoredDeck>();
    [JsonPropertyName("selected_deck_id")] public string SelectedDeckId {get;set;}="";
    [JsonPropertyName("profile_version")] public string ProfileVersion {get;set;}="";
    [JsonPropertyName("config_version")] public string ConfigVersion {get;set;}="";
}
public sealed class CharacterPurchaseRequest
{
    [JsonPropertyName("character_id")] public string CharacterId {get;set;}="";
    [JsonPropertyName("request_id")] public string RequestId {get;set;}="";
    [JsonPropertyName("config_version")] public string ConfigVersion {get;set;}="";
}
public sealed class CatalogCache
{
    [JsonPropertyName("version")] public string Version {get;set;}="";
    [JsonPropertyName("config")] public GameConfiguration Config {get;set;}=new();
}
public static class PlayerInventory
{
    public static PlayerBootstrapResponse? State {get;private set;}
    public static GameConfiguration? Configuration {get;private set;}
    public static bool Ready=>State!=null&&State.UserId==GameAccount.Session?.UserId;
    public static bool AllUnlocked=>!Ready||Configuration!.Economy.AllUnlocked;
    public static long Coins=>State?.Wallet.GetValueOrDefault("coins")??0;
    public static bool Owns(int kind)=>State?.Profile.OwnedCharacters.Contains(CharacterData.Get(kind).Id)??false;
    public static bool CanUse(int kind)=>AllUnlocked||Owns(kind);
    public static long Price(int kind)=>Configuration?.Economy.Prices.GetValueOrDefault(CharacterData.Get(kind).Id)??0;
    public static void Reset(){State=null;Configuration=null;DeckStore.ResetAccount();}
    private static string CachePath(NakamaConnection c)
    {
        string key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c.Client.Scheme+"://"+c.Client.Host+":"+c.Client.Port)));
        return "user://catalog-"+key+".json";
    }
    public static async Task Load(NakamaConnection c,bool importLegacy=false)
    {
        CatalogCache? cache=null;
        string path=CachePath(c);
        try {if(Godot.FileAccess.FileExists(path)){cache=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString(path),GameJsonContext.Default.CatalogCache);if(cache!=null)CharacterData.Apply(cache.Config.Catalog);}}
        catch {cache=null;}
        var request=new PlayerBootstrapRequest {CachedConfigVersion=cache?.Version??""};
        // The legacy file was shared by every process: migrate it only for the original account slot.
        if(importLegacy&&GameAccount.InstanceSlot==1){DeckStore.Load();request.LegacyDecks=DeckStore.Decks.Where(d=>d.Validate()=="").Select(StoredDeck.From).Take(32).ToArray();request.LegacySelectedId=DeckStore.SelectedId;}
        var response=await c.Client.RpcAsync(c.Session,"player_bootstrap",JsonSerializer.Serialize(request,GameJsonContext.Default.PlayerBootstrapRequest));
        var state=JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.PlayerBootstrapResponse)??throw new Exception("Player data missing");
        var config=state.Config??(cache?.Version==state.ConfigVersion?cache.Config:null)??throw new Exception("Configuration cache is stale");
        if(config.SchemaVersion!=1||config.Economy.Currency!="coins")throw new Exception("Unsupported game configuration");
        CharacterData.Apply(config.Catalog);
        if(state.UserId!=c.Session.UserId||state.Profile.SchemaVersion!=1)throw new Exception("Player storage belongs to another account");
        Configuration=config;Apply(state);
        // Cache is an optimization; a disk failure must not discard a successful server load.
        try {using var file=Godot.FileAccess.Open(path+".tmp",Godot.FileAccess.ModeFlags.Write);if(file!=null){file.StoreString(JsonSerializer.Serialize(new CatalogCache {Version=state.ConfigVersion,Config=config},GameJsonContext.Default.CatalogCache));file.Close();DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(path+".tmp"),ProjectSettings.GlobalizePath(path));}}
        catch(Exception e){GD.Print("Catalog cache: "+e.Message);}
    }
    private static void Apply(PlayerBootstrapResponse state)
    {
        if(GameAccount.Session!=null&&state.UserId!=GameAccount.Session.UserId)throw new Exception("The account changed while loading player data");
        State=state;DeckStore.ApplyRemote(state.UserId,state.Profile.Decks.Select(d=>d.ToDeck()).ToList(),state.Profile.SelectedDeckId);
    }
    public static async Task SaveDecks(List<DeckDefinition> decks,string selected)
    {
        if(!Ready)throw new Exception("Login to save decks to your account");
        var c=GameAccount.Connection();try {
            await c.Connect(realtime:false);
            var request=new PlayerDeckSaveRequest {Decks=decks.Select(StoredDeck.From).ToArray(),SelectedDeckId=selected,ProfileVersion=State!.ProfileVersion,ConfigVersion=State.ConfigVersion};
            var response=await c.Client.RpcAsync(c.Session,"player_decks_save",JsonSerializer.Serialize(request,GameJsonContext.Default.PlayerDeckSaveRequest));
            Apply(JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.PlayerBootstrapResponse)!);
        } catch(Nakama.ApiResponseException e) when(e.StatusCode==409){await Load(c);throw new Exception("Collection was updated elsewhere. Review your deck and save again.");}
        finally {await c.Close();}
    }
    public static async Task Purchase(int kind,string requestId)
    {
        if(!Ready)throw new Exception("Login to purchase characters");
        var c=GameAccount.Connection();try {
            await c.Connect(realtime:false);
            var request=new CharacterPurchaseRequest {CharacterId=CharacterData.Get(kind).Id,RequestId=requestId,ConfigVersion=State!.ConfigVersion};
            var response=await c.Client.RpcAsync(c.Session,"character_purchase",JsonSerializer.Serialize(request,GameJsonContext.Default.CharacterPurchaseRequest));
            Apply(JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.PlayerBootstrapResponse)!);
        } finally {await c.Close();}
    }
}
