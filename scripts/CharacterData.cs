using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class CharacterStats
{
    [JsonPropertyName("stars")] public int Stars {get;set;}
    [JsonPropertyName("hp")] public int Hp {get;set;}
    [JsonPropertyName("attack")] public int Attack {get;set;}
    [JsonPropertyName("damage_min")] public int DamageMin {get;set;}
    [JsonPropertyName("damage_max")] public int DamageMax {get;set;}
    [JsonPropertyName("speed")] public int Speed {get;set;}
    [JsonPropertyName("armor")] public int Armor {get;set;}
}
public sealed class AttackRule
{
    [JsonPropertyName("animation")] public string Animation {get;set;}="attack01";
    [JsonPropertyName("mode")] public string Mode {get;set;}="melee";
    [JsonPropertyName("hits")] public int Hits {get;set;}=1;
    [JsonPropertyName("power")] public int Power {get;set;}=100;
    [JsonPropertyName("frames")] public int Frames {get;set;}=8;
    [JsonPropertyName("hit_frames")] public int[] HitFrames {get;set;}=Array.Empty<int>();
    [JsonPropertyName("pierce")] public int Pierce {get;set;}
    [JsonPropertyName("splash")] public string Splash {get;set;}="";
    [JsonPropertyName("splash_power")] public int SplashPower {get;set;}
    [JsonPropertyName("min_stars")] public int MinStars {get;set;}
    [JsonPropertyName("status")] public string Status {get;set;}="";
    [JsonPropertyName("splash_status")] public string SplashStatus {get;set;}="";
    [JsonPropertyName("lifesteal")] public int Lifesteal {get;set;}
    [JsonPropertyName("team_heal")] public int TeamHeal {get;set;}
    [JsonPropertyName("heal")] public int Heal {get;set;}
    [JsonPropertyName("swap_back")] public bool SwapBack {get;set;}
    [JsonPropertyName("target_back")] public bool TargetBack {get;set;}
    [JsonPropertyName("anti_heal")] public bool AntiHeal {get;set;}
}
public sealed class CombatRules
{
    [JsonPropertyName("actions")] public AttackRule[] Actions {get;set;}=Array.Empty<AttackRule>();
    [JsonPropertyName("first")] public AttackRule? First {get;set;}
    [JsonPropertyName("finisher")] public string Finisher {get;set;}="";
    [JsonPropertyName("finisher_hits")] public int FinisherHits {get;set;}
    [JsonPropertyName("finisher_frames")] public int FinisherFrames {get;set;}
    [JsonPropertyName("finisher_hit_frames")] public int[] FinisherHitFrames {get;set;}=Array.Empty<int>();
    [JsonPropertyName("block_first")] public bool BlockFirst {get;set;}
    [JsonPropertyName("bodyguard")] public bool Bodyguard {get;set;}
    [JsonPropertyName("first_bonus")] public int FirstBonus {get;set;}
    [JsonPropertyName("first_heal")] public int FirstHeal {get;set;}
    [JsonPropertyName("low_armor")] public int LowArmor {get;set;}
    [JsonPropertyName("death_armor")] public int DeathArmor {get;set;}
    [JsonPropertyName("kill_status")] public string KillStatus {get;set;}="";
    [JsonPropertyName("kill_splash")] public string KillSplash {get;set;}="";
    [JsonPropertyName("suicide")] public bool Suicide {get;set;}
    [JsonPropertyName("kill_no_counter")] public bool KillNoCounter {get;set;}
    [JsonPropertyName("execute")] public int Execute {get;set;}
}
public sealed class CharacterDefinition
{
    [JsonPropertyName("kind")] public int Kind {get;set;}
    [JsonPropertyName("character_id")] public string Id {get;set;}="";
    [JsonPropertyName("name")] public string Name {get;set;}="";
    [JsonPropertyName("group")] public int Group {get;set;}
    [JsonPropertyName("cost")] public int Cost {get;set;}
    [JsonPropertyName("enabled")] public bool Enabled {get;set;}
    [JsonPropertyName("max_copies")] public int MaxCopies {get;set;}=4;
    [JsonPropertyName("role")] public string Role {get;set;}="";
    [JsonPropertyName("tags")] public string Tags {get;set;}="";
    [JsonPropertyName("description")] public string Description {get;set;}="";
    [JsonPropertyName("ability_name")] public string AbilityName {get;set;}="";
    [JsonPropertyName("ability_description")] public string AbilityDescription {get;set;}="";
    [JsonPropertyName("passive_name")] public string PassiveName {get;set;}="";
    [JsonPropertyName("passive_description")] public string PassiveDescription {get;set;}="";
    [JsonPropertyName("scene_path")] public string ScenePath {get;set;}="";
    [JsonPropertyName("enemy_scene_path")] public string EnemyScenePath {get;set;}="";
    [JsonPropertyName("card_art_path")] public string CardArtPath {get;set;}="";
    [JsonPropertyName("card_x")] public int CardX {get;set;}
    [JsonPropertyName("card_y")] public int CardY {get;set;}
    [JsonPropertyName("card_w")] public int CardW {get;set;}
    [JsonPropertyName("card_h")] public int CardH {get;set;}
    [JsonPropertyName("portrait_path")] public string PortraitPath {get;set;}="";
    [JsonPropertyName("portrait_x")] public int PortraitX {get;set;}
    [JsonPropertyName("portrait_y")] public int PortraitY {get;set;}
    [JsonPropertyName("portrait_w")] public int PortraitW {get;set;}
    [JsonPropertyName("portrait_h")] public int PortraitH {get;set;}
    [JsonPropertyName("stats")] public CharacterStats[] Stats {get;set;}=Array.Empty<CharacterStats>();
    [JsonPropertyName("combat")] public CombatRules? Combat {get;set;}
}
public sealed class CharacterCatalogData
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}
    [JsonPropertyName("group_names")] public string[] GroupNames {get;set;}={"Knight","Ranger","Mage","Guardian","Neutral"};
    [JsonPropertyName("characters")] public CharacterDefinition[] Characters {get;set;}=Array.Empty<CharacterDefinition>();
}
public static class CharacterData
{
    private static CharacterCatalogData _catalog=Parse(Godot.FileAccess.GetFileAsString("res://data/characters.json"));
    public static CharacterDefinition[] All=>_catalog.Characters;
    public static string[] Groups=>_catalog.GroupNames;
    public static CharacterDefinition Get(int kind)=>All[kind];
    public static CharacterStats Stats(int kind,int stars)=>Get(kind).Stats[Mathf.Clamp(stars,1,4)-1];
    public static int EffectValue(int value,int stars)=>(int)Math.Round(value*new[]{1d,1.5d,2d,2.5d}[Mathf.Clamp(stars,1,4)-1],MidpointRounding.AwayFromZero);
    private static string Area(string shape)=>shape switch {"back"=>"ด้านหลังเป้าหมายในแนวเดียวกัน","sides"=>"ข้างเป้าหมายในแถวเดียวกัน","around"=>"รอบเป้าหมาย 1 ช่อง","column"=>"อีกแถวในแนวเดียวกับเป้าหมาย",_=>"ใกล้เป้าหมาย"};
    private static string StatusEffect(string status,int stars)=>status switch {
        "fire"=>$"ติดไฟ: รับดาเมจเพิ่ม {EffectValue(3,stars)} ในการโจมตีถัดไป",
        "stun"=>"สตั้น: ข้ามคิวถัดไป",
        "poison"=>$"ติดพิษ: เสียเลือด {EffectValue(2,stars)} หลังถึงคิวตนเอง 2 ครั้ง โดยไม่คิดเกราะ",_=>""
    };
    private static string DescribeAttack(AttackRule a,int stars)
    {
        if(a.Mode=="heal")return $"ฟื้นเลือด {EffectValue(a.Heal,stars)} ให้ฝ่ายเดียวกันที่เสียเลือดมากที่สุด";
        if(a.Mode=="curse")return "ทำให้ศัตรูอ่อนแรง: การโจมตีถัดไปมีดาเมจเหลือ 50%";
        if(a.Mode=="summon")return $"อัญเชิญ Skeleton เลือด {EffectValue(5,stars)} เกราะ 0 สูงสุด 2 ตัวต่อการต่อสู้ ต้องมีช่องว่าง; หากอัญเชิญไม่ได้จะโจมตีประชิด";
        if(a.Mode=="revive")return "คืนชีพเพื่อนที่มีเลือดเต็มมากที่สุด ด้วยเลือด 40% เกราะ 0 (1 ครั้งต่อการต่อสู้) ต้องมีช่องว่าง; ไม่รวมตัวอัญเชิญและตัวที่เคยคืนชีพ หากคืนชีพไม่ได้จะโจมตีประชิด";
        var parts=new System.Collections.Generic.List<string>{a.Mode=="ranged"?"โจมตีระยะไกล ไม่ถูกสวนกลับ":"โจมตีประชิด"};
        if(a.Hits>1)parts.Add($"โจมตี {a.Hits} ครั้ง ดาเมจรวม {a.Power}% ของพลังโจมตี");
        else if(a.Power!=100)parts.Add($"ดาเมจ {a.Power}% ของพลังโจมตี");
        if(a.Pierce>0)parts.Add($"ดาเมจ {a.Pierce}% ไม่คิดเกราะ");
        if(a.Splash.Length>0){
            string area=$"ศัตรู{Area(a.Splash)}รับดาเมจ {a.SplashPower}% ของพลังโจมตี";
            if(a.MinStars>stars)area=$"{a.MinStars} ดาวขึ้นไป: "+area;
            if(a.SplashStatus.Length>0)area+=" และ"+StatusEffect(a.SplashStatus,stars);
            parts.Add(area);
        }
        if(a.Status.Length>0)parts.Add(StatusEffect(a.Status,stars));
        if(a.Lifesteal>0)parts.Add($"ดูดเลือดเท่าที่ศัตรูเสียจริง สูงสุด {EffectValue(a.Lifesteal,stars)}"+(a.Mode=="ranged"?"":" หากรอดจากการสวนกลับ"));
        if(a.TeamHeal>0)parts.Add($"หากยังรอด ฟื้นเลือดให้ฝ่ายเดียวกันทุกตัว ตัวละ {EffectValue(a.TeamHeal,stars)}");
        if(a.SwapBack)parts.Add("ดึงศัตรูแถวหลังมาแถวหน้า สลับกับตัวที่อยู่ด้านหน้า");
        if(a.TargetBack)parts.Add("สุ่มโจมตีศัตรูแถวหลัง หากไม่มีจะสุ่มจากศัตรูที่เหลือ");
        if(a.AntiHeal)parts.Add("ห้ามเป้าหมายรับการฟื้นฟูเลือดจนจบคิวถัดไป");
        return string.Join(" • ",parts);
    }
    public static string AttackDescription(int kind,int stars)
    {
        var c=Get(kind);var r=c.Combat;if(r==null)return c.AbilityDescription;
        var actions=r.Actions.Select(a=>DescribeAttack(a,stars)).ToArray();
        var lines=new System.Collections.Generic.List<string>();
        if(actions.Distinct().Count()==1)lines.Add(actions[0]);
        else {
            lines.Add("ลำดับการโจมตี (วนซ้ำ)");
            for(int i=0;i<actions.Length;i++){
                int last=i;while(last+1<actions.Length&&actions[last+1]==actions[i])last++;
                lines.Add($"{(last==i?$"{i+1}":$"{i+1}–{last+1}")}. {actions[i]}");i=last;
            }
        }
        if(r.First!=null)lines.Add("ครั้งแรก: "+DescribeAttack(r.First,stars));
        if(r.FinisherHits>1)lines.Add($"ท่าปิดฉาก: โจมตี {r.FinisherHits} ครั้ง ดาเมจรวม 120% ของพลังโจมตี");
        return string.Join("\n",lines);
    }
    public static string PassiveDescriptionFor(int kind,int stars)
    {
        var r=Get(kind).Combat;if(r==null)return Get(kind).PassiveDescription;
        var lines=new System.Collections.Generic.List<string>();
        if(r.BlockFirst)lines.Add("การโจมตีแรกที่ได้รับมีดาเมจลดลง 50%");
        if(r.Bodyguard)lines.Add("รับการโจมตีแทนฝ่ายเดียวกันที่อยู่ด้านหลังในแนวเดียวกัน 1 ครั้งต่อการต่อสู้");
        if(r.FirstBonus>0)lines.Add($"โจมตีครั้งแรก ดาเมจเพิ่ม {EffectValue(r.FirstBonus,stars)}");
        if(r.FirstHeal>0)lines.Add($"เมื่อเสียเลือดครั้งแรกและยังรอด ฟื้นเลือดทันที {EffectValue(r.FirstHeal,stars)} (1 ครั้งต่อการต่อสู้)");
        if(r.LowArmor>0)lines.Add($"เมื่อเลือดต่ำกว่าครึ่งและยังรอด ได้เกราะ {EffectValue(r.LowArmor,stars)} (1 ครั้งต่อการต่อสู้)");
        if(r.DeathArmor>0)lines.Add($"เมื่อตาย สุ่มเพิ่มเกราะ {EffectValue(r.DeathArmor,stars)} ให้ฝ่ายเดียวกันที่ยังมีชีวิต 1 ตัว");
        if(r.KillStatus.Length>0)lines.Add($"เมื่อฆ่าศัตรูได้ครั้งแรก ทำให้ศัตรู{Area(r.KillSplash)}{StatusEffect(r.KillStatus,stars)}");
        if(r.Execute>0)lines.Add($"เมื่อเป้าหมายมีเลือดไม่เกิน {r.Execute}% ของเลือดสูงสุด ประหารโดยไม่คิดเกราะ (1 ครั้งต่อการต่อสู้)");
        if(r.Suicide)lines.Add("เมื่อโจมตีจนศัตรูตาย ตัวเองจะตายด้วย");
        if(r.KillNoCounter)lines.Add("เมื่อฆ่าศัตรูได้ครั้งแรก ไม่ถูกสวนกลับ");
        return string.Join("\n",lines);
    }
    public static string CombatDescription(int kind,int stars)
    {
        string passive=PassiveDescriptionFor(kind,stars);
        return AttackDescription(kind,stars)+(passive.Length>0?"\n"+passive:"");
    }
    private static CharacterCatalogData Parse(string json)
    {
        var data=JsonSerializer.Deserialize(json,GameJsonContext.Default.CharacterCatalogData)??throw new Exception("Character catalog is empty.");
        if(data.SchemaVersion!=1 || data.Characters.Length<1 || data.GroupNames.Length<5 || data.GroupNames[4]!="Neutral")throw new Exception("Unsupported character catalog.");
        for(int i=0;i<data.Characters.Length;i++){
            var c=data.Characters[i];if(c.Kind!=i || c.Stats.Length!=4 || c.Group<0 || c.Group>=data.GroupNames.Length)throw new Exception("Character catalog IDs, groups or stats are invalid.");
            foreach(string path in new[]{c.ScenePath,c.EnemyScenePath,c.CardArtPath,c.PortraitPath}.Distinct())
                if(!path.StartsWith("res://") || !ResourceLoader.Exists(path))throw new Exception($"Missing character asset: {path}. Update the game assets first.");
        }
        return data;
    }
    public static async Task Refresh(NakamaConnection connection)
    {
        // Catalog grows with asset packs; use HTTP rather than a realtime socket frame.
        var response=await connection.Client.RpcAsync(connection.Session,"character_catalog","{}");_catalog=Parse(response.Payload);
    }
}
