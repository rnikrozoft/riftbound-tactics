using Godot;
using System;
using System.Linq;
public partial class CombatInitiativeRunner : Node
{
 public override void _Ready() {
  try {
   int checks=0; var kind=CharacterData.All.First(c=>c.Enabled).Kind;
   foreach(bool strongerA in new[]{true,false}) foreach(int seed in Enumerable.Range(0,30)) {
    var units=new[]{new OnlineCombatUnit{Id="A:front",Team="A",Kind=kind,Stars=strongerA?4:1,Slot=5},new OnlineCombatUnit{Id="A:back",Team="A",Kind=kind,Stars=1,Slot=0},new OnlineCombatUnit{Id="B:front",Team="B",Kind=kind,Stars=strongerA?1:4,Slot=5},new OnlineCombatUnit{Id="B:back",Team="B",Kind=kind,Stars=1,Slot=0}};
    var p=new LocalCombat(seed).Simulate(units);
    if(p.Events.Length==0||p.Events[0].Attacker!=(strongerA?"A:front":"B:front"))throw new Exception("Attack total / mirrored front order");checks++;
   }
   var starters=Enumerable.Range(0,30).Select(seed=>new LocalCombat(seed).Simulate(new[]{new OnlineCombatUnit{Id="A",Team="A",Kind=kind,Stars=1,Slot=5},new OnlineCombatUnit{Id="B",Team="B",Kind=kind,Stars=1,Slot=0}}).Events[0].Attacker).Distinct().Count();
   if(starters!=2)throw new Exception("Tie must allow either side");
   GD.Print($"PASS combat initiative: {checks+1} checks");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
