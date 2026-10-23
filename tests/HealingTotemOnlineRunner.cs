using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
public partial class HealingTotemOnlineRunner:Node
{
    private async Task Wait(Func<bool> test){ulong end=Time.GetTicksMsec()+20000;while(!test()){if(Time.GetTicksMsec()>end)throw new TimeoutException("Totem live action");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
    public override async void _Ready(){NakamaConnection? a=null,b=null;try{
        a=new();b=new();OnlineState? sa=null,sb=null;a.StateReceived+=s=>sa=s;b.StateReceived+=s=>sb=s;
        a.ErrorReceived+=e=>GD.PushError(e.Message);b.ErrorReceived+=e=>GD.PushError(e.Message);
        await a.Connect();await b.Connect();await CharacterData.Refresh(a);
        var room=await a.EnterRoom(true);await b.EnterRoom(false,room.Code);await Wait(()=>sa?.Phase=="preparation"&&sb?.Phase=="preparation");
        foreach(var c in new[]{a,b}){
            OnlineState State()=>ReferenceEquals(c,a)?sa!:sb!;
            OnlinePlayer Own()=>State().Players.First(p=>p?.UserId==c.Session.UserId)!;
            async Task Act(string type,int token=0,int slot=0){long seq=c.NextActionSequence();await c.Send(new OnlineAction {Type=type,Token=token,Slot=slot,Round=State().Round,Sequence=seq});await Wait(()=>State().AckSequence>=seq);}
            GD.Print($"LIVE TOTEM: kind={CharacterData.SupportKind} count={CardCatalog.Count} offers={string.Join(",",Own().Offers.Select(o=>o.Kind))}");
            var card=Own().Offers.Single(o=>o.Kind==CharacterData.SupportKind);int coins=Own().Coins;
            await Act("buy",card.Token);if(Own().Coins!=coins-2||!Own().Hand.Any(h=>h.Token==card.Token))throw new Exception("Live support purchase failed");
            await Act("deploy",card.Token,8);if(!Own().Units.Any(u=>u.Token==card.Token&&u.Slot==8))throw new Exception("Live support deployment failed");
            var hero=Own().Offers.First(o=>!CharacterData.IsSupport(o.Kind));await Act("buy",hero.Token);await Act("deploy",hero.Token,3);
        }
        long sqa=a.NextActionSequence(),sqb=b.NextActionSequence();await a.Send(new OnlineAction {Type="ready",Round=sa!.Round,Sequence=sqa});await b.Send(new OnlineAction {Type="ready",Round=sb!.Round,Sequence=sqb});
        await Wait(()=>sa?.Battle!=null);
        var plan=sa!.Battle!;
        if(!plan.Units.Any(u=>CharacterData.IsSupport(u.Kind))||plan.Events.Any(e=>plan.Units.Any(u=>CharacterData.IsSupport(u.Kind)&&(u.Id==e.Attacker||u.Id==e.Target))))throw new Exception("Live support combat plan failed");
        GD.Print("HEALING TOTEM ONLINE PASS: mandatory on both decks, purchase enters hand, coins charged, support row placement, heroes remain separate, authoritative plan excludes support targets");GetTree().Quit();
    }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}finally{if(a!=null)await a.Close();if(b!=null)await b.Close();}}
}
