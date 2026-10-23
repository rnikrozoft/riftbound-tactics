using Godot;
using System.Collections.Generic;

public static class CombatStatuses
{
    public sealed record Status(string Key,string Title,string Description,int Icon);
    public static readonly Status[] All={
        new("fire","ติดไฟ","เสียพลังชีวิต 2 แต้มทุกเทิร์นของทั้งสองฝ่าย โดยไม่คิดเกราะ",18),
        new("stun","สตั้น","ข้ามการกระทำครั้งถัดไป",20),
        new("poison","ติดพิษ","เสียเลือดหลังออกแอ็กชัน 2 ครั้งถัดไป",17),
        new("curse","อ่อนแรง","โจมตีครั้งถัดไปด้วยดาเมจ 50%",23),
        new("anti_heal","ห้ามฮีล","ไม่สามารถรับการฟื้นฟูเลือดได้",22)
    };
    private static readonly Dictionary<int,Texture2D> Icons=new();
    public static Texture2D Texture(Status status)
    {
        int frame=status.Key=="fire"?(int)(Time.GetTicksMsec()/100%16):-1;
        int key=frame>=0?1000+frame:status.Icon;
        if(!Icons.TryGetValue(key,out var texture)){
            texture=frame>=0?GD.Load<Texture2D>($"res://assets/effects/packs/four_fire_v1_1_0/frames/wide-fire_red_32/{frame:000}.png"):GD.Load<Texture2D>($"res://assets/ui/status_icons/png_32/icon{status.Icon:00}.png");Icons.Add(key,texture);
        }
        return texture;
    }
    public static bool Active(BattleUnit unit,string key)=>!unit.IsDead&&key switch {
        "fire"=>unit.Fire>0,"stun"=>unit.Stunned,"poison"=>unit.Poison>0,"curse"=>unit.Cursed,"anti_heal"=>unit.AntiHeal,_=>false
    };
}
