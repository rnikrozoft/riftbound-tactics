using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ReadyFieldIntegrationRunner : Node
{
    private int _checks;
    private void Check(bool value,string message) { _checks++; if(!value) throw new InvalidOperationException(message); }
    private async Task Wait(Func<bool> test,string message)
    {
        ulong end=Time.GetTicksMsec()+20000;
        while(!test()) { if(Time.GetTicksMsec()>end) throw new TimeoutException(message); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    }
    private Node2D Game(string name)
    {
        var view=new SubViewport { Name=name, Size=new(1280,720) }; AddChild(view);
        var game=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>(); view.AddChild(game); return game;
    }
    private async Task Action(OnlineBattle net,CardShop shop,string type,int token=0,int slot=0)
    {
        net.SendAction(type,token,slot); await Wait(()=>!shop.NetworkPending,type);
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var ga=Game("A"); var gb=Game("B");
            var a=ga.GetNode<OnlineBattle>("OnlineBattle"); var b=gb.GetNode<OnlineBattle>("OnlineBattle");
            var sa=ga.GetNode<CardShop>("UI/SafeArea/Content/CardShop"); var sb=gb.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            await Wait(()=>a.Connected&&b.Connected,"connect"); a.CreateRoom(); await Wait(()=>a.State?.Phase=="waiting","create"); b.JoinRoom(a.State!.Code);
            await Wait(()=>sa.Drafting&&sb.Drafting,"prepare");
            for(int round=2;round<=7;round++) {
                await Action(a,sa,"ready"); await Action(b,sb,"ready"); await Wait(()=>a.State?.Phase=="finished"&&b.State?.Phase=="finished","draw");
                await Action(a,sa,"next"); await Action(b,sb,"next"); await Wait(()=>sa.Drafting&&sb.Drafting&&sa.TurnNumber==round,"income round");
            }
            Check(CardShop.DeploymentCells.Distinct().Count()==6,"Six distinct field positions");
            await Action(a,sa,"upgrade"); await Action(a,sa,"reroll");
            int spare=-1;
            for(int slot=0;slot<7;slot++) {
                int attempts=0;
                while(!sa.Offers.Any(c=>!sa.Deployed.Values.Any(d=>d.Card.Name==c.Name)&&!sa.Hand.Any(h=>h.Name==c.Name))) {
                    if(++attempts>35) throw new Exception("No distinct card"); await Action(a,sa,"reroll");
                }
                var card=sa.Offers.First(c=>!sa.Deployed.Values.Any(d=>d.Card.Name==c.Name)&&!sa.Hand.Any(h=>h.Name==c.Name));
                await Action(a,sa,"buy",card.Token);
                if(slot<6) await Action(a,sa,"deploy",card.Token,slot); else spare=card.Token;
            }
            Check(sa.Deployed.Count==6&&sa.Occupants.All(u=>u!=null),"All six slots accept cards");
            await Action(a,sa,"ready");
            Check(sa.ReadyForBattle&&sa.Drafting&&a.State!.Phase=="preparation","Ready still permits preparation edits");
            var sold=sa.Occupants[0]!; var moved=sa.Occupants[1]!;
            Check(!sa.CanReturnUnit(sold)&&!sa.ReturnUnit(sold),"Ready unit cannot return to hand");
            Check(sa.CanSell(new CardDrag {Shop=sa,Unit=sold}),"Ready unit can be sold");
            sa.SellDrop(new CardDrag {Shop=sa,Unit=sold}); await Wait(()=>!sa.NetworkPending&&sa.Deployed.Count==5,"ready sale");
            Check(sa.PlaceCardOrUnit(new CardDrag {Shop=sa,Unit=moved},sa.Tiles.ToGlobal(sa.SlotCenters[0])),"Ready move queued");
            await Wait(()=>!sa.NetworkPending&&sa.Occupants[0]?.CardToken==moved.CardToken,"ready move");
            Check(sa.PlaceCardOrUnit(new CardDrag {Shop=sa,Token=spare},sa.Tiles.ToGlobal(sa.SlotCenters[1])),"Ready deployment queued");
            await Wait(()=>!sa.NetworkPending&&sa.Deployed.Count==6,"ready deployment");
            Check(sa.Deployed[spare].Unit.GridSlot==1&&!sa.CanReturnUnit(sa.Deployed[spare].Unit),"Newly deployed after ready cannot return");
            Check(sa.ReadyForBattle&&sa.Hand.Count==0&&a.State!.Players[0]!.Ready,"Edits retain ready status");
            var last=sa.Occupants[5]!;
            Check(sa.PlaceCardOrUnit(new CardDrag {Shop=sa,Unit=last},sa.Tiles.ToGlobal(sa.SlotCenters[3])),"Ready occupied swap queued");
            await Wait(()=>!sa.NetworkPending&&sa.Deployed[last.CardToken].Unit.GridSlot==3,"ready swap");
            Check(sa.Deployed.Values.Select(d=>d.Unit.GridSlot).Distinct().Count()==6,"Six units remain in distinct slots after swap");
            await Action(b,sb,"ready"); await Wait(()=>a.State?.Phase=="battle"&&b.State?.Phase=="battle","battle");
            Check(!sa.Drafting&&!sa.CanSell(new CardDrag {Shop=sa,Unit=last})&&!sa.CanPlace(new CardDrag {Shop=sa,Unit=last},sa.Tiles.ToGlobal(sa.SlotCenters[0])),"Actual battle locks field editing");
            Check(a.State!.Battle!.Units.Count(u=>u.Team=="A")==6,"Server battle contains all six field units");
            ga.GetParent().QueueFree(); gb.GetParent().QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print($"READY FIELD PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
