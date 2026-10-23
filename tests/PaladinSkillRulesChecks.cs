using System;
using System.Linq;
using System.Reflection;
using System.Collections;
public static class PaladinSkillRulesChecks
{
 public static void Run(){var ft=typeof(LocalCombat).GetNestedType("Fighter",BindingFlags.NonPublic)!;
 object Fighter(int side,int kind,string id){var f=Activator.CreateInstance(ft)!;void Set(string n,object v)=>ft.GetField(n)!.SetValue(f,v);Set("Unit",new OnlineCombatUnit{Id=id,Team=side==0?"A":"B",Kind=kind,Slot=kind>=514?6:3,MaxHp=100,Stars=1});Set("Hp",100);Set("Side",side);Set("Rules",new CombatRules{Actions=new[]{new AttackRule{Mode="ranged",Power=100,Hits=1,HitFrames=new[]{2},Frames=8}}});return f;}
 foreach(var card in CharacterData.All.Where(c=>SkillCardVisual.IsSkill(c.Kind))){var sim=new LocalCombat(19);var list=(IList)typeof(LocalCombat).GetField("_units",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(sim)!;var a=Fighter(0,0,"A:1");var b=Fighter(1,0,"B:1");var u=Fighter(1,card.Kind,"B:skill");list.Add(a);list.Add(b);list.Add(u);int Get(object f,string name)=>(int)ft.GetField(name)!.GetValue(f)!;void Set(object f,string name,object value)=>ft.GetField(name)!.SetValue(f,value);void Call(string name,params object[] args)=>typeof(LocalCombat).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(sim,args);int v=card.SupportValue;void Check(bool ok){if(!ok)throw new Exception("Offline skill mismatch: "+card.Name);}
 switch(card.SupportEffect){
 case "armor_once":Call("StartSupports");Check(Get(b,"Armor")==v);break;
 case "heal_once":case "heal_all_once":Call("EndSupports",0);Check(Get(u,"Hp")==100);Set(b,"Hp",50);Call("EndSupports",0);Check(Get(b,"Hp")==50+v);break;
 case "cleanse_once":Set(b,"Hp",50);Set(b,"Fire",2);Set(b,"Poison",2);Set(b,"PoisonTicks",2);Set(b,"Stun",true);Set(b,"Curse",true);Set(b,"AntiHeal",true);Call("EndSupports",0);Check(Get(b,"Hp")==50+v&&Get(b,"Fire")==0&&Get(b,"Poison")==0&&!(bool)ft.GetField("Stun")!.GetValue(b)!);break;
 case "ward":Call("Damage",a,b,200,100,0,"damage");Check(Get(b,"Hp")==100&&Get(b,"Armor")==v);break;
 case "seal":var rules=(CombatRules)ft.GetField("Rules")!.GetValue(a)!;rules.Actions[0].Hits=4;rules.Actions[0].Power=40;Call("Action",a,b,0L);Check(Get(b,"Hp")==100-(10*v+50)/100);break;
 case "thorns":Call("AfterAttack",a,b,"melee",20,0);Check(Get(a,"Hp")==100-v);break;
 case "mirror":Call("AfterAttack",a,b,"ranged",20,0);Check(Get(a,"Hp")==100-(20*v+50)/100);break;
 case "frost":Call("AfterAttack",a,b,"ranged",20,0);Check(Get(a,"Hp")==100-v&&(bool)ft.GetField("Stun")!.GetValue(a)!);break;
 case "mine":Call("AfterAttack",a,b,"melee",20,0);Check(Get(a,"Hp")==100-v&&Get(a,"Fire")==2);break;
 case "retaliate":Set(a,"Armor",2);Call("AfterAttack",a,b,"ranged",20,0);Check(Get(a,"Hp")==100-v+2);break;
 case "weaken":Call("AfterAttack",a,b,"ranged",20,0);Check(Get(a,"Hp")==100-v&&(bool)ft.GetField("Curse")!.GetValue(a)!);break;
 default:throw new Exception("Unknown skill");}
 Check(Get(u,"Hp")==0);
 }
 }
}
