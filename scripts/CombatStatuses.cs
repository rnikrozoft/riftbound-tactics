using Godot;
using System.Collections.Generic;

public static class CombatStatuses
{
    public sealed record Status(string Key,string Title,string Description,int Icon);
    public static readonly Status[] All={
        new("fire","ติดไฟ","ได้รับดาเมจเพิ่มเมื่อถูกโจมตีครั้งถัดไป",18),
        new("stun","สตั้น","ข้ามการกระทำครั้งถัดไป",20),
        new("poison","ติดพิษ","เสียเลือดหลังออกแอ็กชัน 2 ครั้งถัดไป",17),
        new("curse","อ่อนแรง","โจมตีครั้งถัดไปด้วยดาเมจ 50%",23),
        new("anti_heal","ห้ามฮีล","ไม่สามารถรับการฟื้นฟูเลือดได้",22)
    };
    private static readonly Dictionary<int,Texture2D> Icons=new();
    public static Texture2D Texture(Status status)
    {
        if(!Icons.TryGetValue(status.Icon,out var texture)){texture=GD.Load<Texture2D>($"res://assets/48-buff-debuff-icons/png_32/icon{status.Icon:00}.png");Icons.Add(status.Icon,texture);}
        return texture;
    }
    public static bool Active(BattleUnit unit,string key)=>!unit.IsDead&&key switch {
        "fire"=>unit.Fire>0,"stun"=>unit.Stunned,"poison"=>unit.Poison>0,"curse"=>unit.Cursed,"anti_heal"=>unit.AntiHeal,_=>false
    };
}
