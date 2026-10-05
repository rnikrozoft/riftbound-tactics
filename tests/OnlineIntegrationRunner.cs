using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class OnlineIntegrationRunner : Node
{
    private SceneTree _tree = null!;
    private Node2D _a = null!, _b = null!;
    private OnlineBattle _netA = null!, _netB = null!;
    private CardShop _shopA = null!, _shopB = null!;
    private int _checks;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok,string message) { _checks++;if (!ok) throw new InvalidOperationException(message); }
    private async Task Wait(Func<bool> condition,string message,int timeout = 20000)
    {
        ulong end = Time.GetTicksMsec() + (ulong)timeout;
        while (!condition()) {
            if (Time.GetTicksMsec() > end) throw new TimeoutException(message);
            await ToSignal(_tree,SceneTree.SignalName.ProcessFrame);
        }
    }
    private Node2D AddGame(string name)
    {
        var viewport = new SubViewport { Name = name,Size = new(1280,720),RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var field = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();viewport.AddChild(field);return field;
    }
    private async Task Action(OnlineBattle net,CardShop shop,string type,int token=0,int slot=0)
    {
        net.SendAction(type,token,slot);
        await Wait(() => !shop.NetworkPending,type + " was not acknowledged");
    }
    private void CompareBoard()
    {
        Check(_shopA.NetworkUnits.Count == _shopB.NetworkUnits.Count,"Board counts differ");
        foreach (var pair in _shopA.NetworkUnits) {
            var other = _shopB.NetworkUnits[pair.Key];
            var ownerShop = pair.Key.StartsWith("A:") ? _shopA : _shopB;
            var enemyShop = ownerShop == _shopA ? _shopB : _shopA;
            Check(ownerShop.NetworkUnits[pair.Key].GlobalPosition.IsEqualApprox(ownerShop.Tiles.ToGlobal(ownerShop.Tiles.MapToLocal(CardShop.DeploymentCells[pair.Value.GridSlot]))),"Own hero must be on left");
            Check(enemyShop.NetworkUnits[pair.Key].GlobalPosition.IsEqualApprox(enemyShop.Tiles.ToGlobal(enemyShop.Tiles.MapToLocal(new Vector2I(102,36) - CardShop.DeploymentCells[pair.Value.GridSlot]))),"Opponent must be on right");
            Check(pair.Value.IsAlly != other.IsAlly,"Team orientation differs");
        }
    }
    private async void Run()
    {
        _tree = GetTree();
        NakamaConnection? third = null;
        try
        {
            _a = AddGame("A"); _b = AddGame("B");
            // Exercise the actual scene reconciliation for all six slots on both perspectives.
            foreach (string localTeam in new[] { "A", "B" }) {
                var game = localTeam == "A" ? _a : _b;
                var shop = game.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
                var players = new[] { new OnlinePlayer { UserId = "testA", Team = "A" }, new OnlinePlayer { UserId = "testB", Team = "B" } };
                foreach (var player in players) player.Units = Enumerable.Range(0,6).Select(slot=>new OnlineUnit { Token = slot+1, Slot = slot }).ToArray();
                shop.ApplyNetworkState(new OnlineState { Phase = "preparation", Round = 99, Players = players },"test" + localTeam);
                for (int slot = 0; slot < 6; slot++) {
                    var own = shop.NetworkUnits[localTeam + ":" + (slot+1)];
                    var other = shop.NetworkUnits[(localTeam == "A" ? "B" : "A") + ":" + (slot+1)];
                    var ownCell = CardShop.DeploymentCells[slot];
                    Check(own.GlobalPosition.IsEqualApprox(shop.Tiles.ToGlobal(shop.Tiles.MapToLocal(ownCell))),"Own slot changed");
                    Check(other.GlobalPosition.IsEqualApprox(shop.Tiles.ToGlobal(shop.Tiles.MapToLocal(new Vector2I(102,36) - ownCell))),"Opponent slot must rotate 180 degrees");
                    Check(other.GridSlot == slot,"Visual rotation changed authoritative slot identity");
                }
            }
            _netA = _a.GetNode<OnlineBattle>("OnlineBattle");_netB = _b.GetNode<OnlineBattle>("OnlineBattle");
            _shopA = _a.GetNode<CardShop>("UI/SafeArea/Content/CardShop");_shopB = _b.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            await Wait(() => _netA.Connected && _netB.Connected,"Nakama connection");
            Check(_netA.UserId != _netB.UserId,"Debug identities reused");
            Check(_netA.Connection!.DeviceId != _netB.Connection!.DeviceId,"Debug device ids reused");
            _netA.CreateRoom();await Wait(() => _netA.State?.Phase == "waiting","Create room");
            string code = _netA.State!.Code;
            Check(code.Length == 6 && code.All(char.IsDigit),"Room code");
            _netB.JoinRoom(code);
            await Wait(() => _shopA.Drafting && _shopB.Drafting,"Shared preparation");
            Check(_netA.LocalTeam == "A" && _netB.LocalTeam == "B","Stable roles");
            Check(_netA.State!.DeadlineMs - _netA.State.ServerMs <= 60000 && _netA.State.DeadlineMs - _netA.State.ServerMs > 58000,"60 second preparation");
            Check(_shopA.Offers.Count == 3 && _shopB.Offers.Count == 3,"Both get shops");
            Check(!_shopA.OpponentHandZone.Visible && !_shopB.OpponentHandZone.Visible,"Opponent backs visible before battle");
            Check(_netA.State.Players[1]!.Hand.Length == 0 && _netA.State.Players[1]!.Offers.Length == 0,"Opponent private cards leaked");
            third = new();await third.Connect();
            bool denied = false;
            try { await third.EnterRoom(false,code); } catch { denied = true; }
            Check(denied,"Third player admitted");
            await third.Close();third = null;

            Check(_shopA.Coins == 4 && _shopB.Coins == 4 && _shopA.Offers.Count == 3, "Network starting economy");
            _shopB.UpgradeShop(); await Wait(() => !_shopB.NetworkPending && _shopB.ShopLevel == 3,"Paid network upgrade");
            Check(_shopB.Coins==2 && _shopB.UpgradeCost==8 && _shopB.Offers.Count==3,"Upgrade deduction/reset without refill");
            _shopA.ToggleShopLock(); await Wait(() => !_shopA.NetworkPending && _shopA.ShopLocked,"Network lock");
            // Reroll now unlocks the shop; retain this fixture's starting coins for two purchases.
            Check(_shopA.Coins == 4 && !_shopA.NetworkPending,"Lock does not spend coins");
            int aToken = _shopA.Offers[0].Token,bToken = _shopB.Offers[0].Token;
            Check(_shopA.TakeCard(aToken),"A purchase queued");
            Check(_shopA.Hand.Count == 0,"Client applied purchase before server");
            await Wait(() => !_shopA.NetworkPending && _shopA.Hand.Count == 1,"A purchase confirmation");
            Check(_shopB.TakeCard(bToken),"B purchase queued");
            await Wait(() => !_shopB.NetworkPending && _shopB.Hand.Count == 1,"B purchase confirmation");
            var dragA = new CardDrag { Shop = _shopA,Token = aToken };
            var dragB = new CardDrag { Shop = _shopB,Token = bToken };
            _shopA.FinishLooseDrop(dragA,new Vector2(80,260));
            _shopB.FinishLooseDrop(dragB,new Vector2(1200,350));
            await Wait(() => !_shopA.NetworkPending && !_shopB.NetworkPending && _shopA.Deployed.Count == 1 && _shopB.Deployed.Count == 1,"Deploy own teams");
            Check(_shopA.NetworkUnits.Count == 1 && _shopB.NetworkUnits.Count == 1,"Opponent visible during preparation");
            Check(_netA.State!.Players[1]!.Units.Length == 0 && _netB.State!.Players[0]!.Units.Length == 0,"Opponent formation leaked in protocol");
            if (_netA.State?.Phase == "battle") CompareBoard();
            Check(_shopB.Deployed[bToken].Unit.IsAlly,"B units placed on wrong side");
            var ownB = _shopB.Deployed[bToken].Unit;
            Check(_shopB.PickUnit(ownB.GlobalPosition) == ownB,"B cannot drag its unit");
            Check(!_shopB.NetworkUnits.ContainsKey("A:"+aToken),"Opponent revealed before battle");
            await Action(_netB,_shopB,"move",bToken,2);
            Check(_shopB.Deployed[bToken].Unit.GridSlot == 2,"Own move not applied");
            Check(!_shopA.NetworkUnits.ContainsKey("B:"+bToken),"Opponent move leaked");
            if (_netA.State?.Phase == "battle") CompareBoard();

            // Buying a spare card proves hands remain usable for inspection during battle.
            await Action(_netA,_shopA,"buy",_shopA.Offers[0].Token);
            Check(_shopA.Coins == 0 && _shopB.Coins == 0,"Authoritative purchase deductions");
            int[] lockedTokens = _shopA.Offers.Select(c=>c.Token).ToArray();
            _shopA.ConfirmTurn();await Wait(() => _shopA.ReadyForBattle,"A readiness confirmation");
            Check(_shopA.Drafting,"Preparation editing remains enabled after ready");
            Check(_netA.State!.Phase == "preparation","One ready started combat");
            _shopB.ConfirmTurn();
            await Wait(() => _netA.State?.Phase == "battle" && _netB.State?.Phase == "battle","Both ready start");
            CompareBoard();
            string planA = JsonSerializer.Serialize(_netA.State!.Battle);
            string planB = JsonSerializer.Serialize(_netB.State!.Battle);
            Check(planA == planB,"Different combat plans sent to players");
            Check(_shopA.ZoneB.Visible && _shopB.ZoneB.Visible,"Hands hidden during battle");
            Check(!_shopA.ZoneA.Visible && !_shopB.ZoneA.Visible,"Shop visible during battle");
            Check(_shopA.OpponentHandZone.Visible && _shopB.OpponentHandZone.Visible,"Opponent backs hidden during battle");
            Check(_shopA.OpponentHandRow.GetChildren().OfType<TextureRect>().Count(c=>c.Visible) == 0,"Empty opponent hand has placeholders");
            Check(_shopB.OpponentHandRow.GetChildren().OfType<TextureRect>().Count(c=>c.Visible) == _shopA.Hand.Count,"Opponent back count differs from held cards");
            Check(_netB.State!.Players[0]!.HandCount == _shopA.Hand.Count && _netB.State.Players[0]!.Hand.Length == 0,"Opponent identities leaked or count missing");
            Check(!_shopA.AcceptsHandToken(_shopA.Hand[0].Token),"Battle edits allowed");
            _shopA.ShowCardDetails(_shopA.Hand[0].Token);
            Check(_shopA.Details.Visible,"Battle hand inspection");
            _shopA.CloseDetails();
            _shopB.ShowUnitDetails(ownB);Check(_shopB.Details.Visible,"Battle unit inspection");_shopB.CloseDetails();
            GD.Print($"ONLINE: room {code}; two unique users, mirrored preparation, identical authoritative replay. Waiting for playback...");
            await Wait(() => _netA.State?.Phase == "finished" && _netB.State?.Phase == "finished","Server battle end",40000);
            await Wait(() => _a.GetNode<BattleDemo>("BattleDemo").TurnCount == _netA.State!.Battle!.Events.Length &&
                _b.GetNode<BattleDemo>("BattleDemo").TurnCount == _netB.State!.Battle!.Events.Length,"All replay events displayed");
            foreach (var pair in _shopA.NetworkUnits)
                Check(pair.Value.Health == _shopB.NetworkUnits[pair.Key].Health,"Client health differs");
            Check(_netA.State!.Battle!.Winner == _netB.State!.Battle!.Winner,"Winner differs");
            var dead = _shopA.NetworkUnits.Values.First(u=>u.IsDead);
            _shopA.ShowUnitDetails(dead);
            Check(_shopA.DetailsText.Text.Contains("DEAD"),"Dead network-unit inspection");_shopA.CloseDetails();
            int endA = _shopA.Coins, endB = _shopB.Coins;
            Check(endA == 0 && endB == 0,"Battle does not award coins");
            _shopA.ConfirmTurn();_shopB.ConfirmTurn();
            await Wait(() => _shopA.Drafting && _shopB.Drafting && _netA.State?.Round == 2,"Next round readiness");
            Check(_shopA.Coins == endA + 6 && _shopB.Coins == endB + 6,"Network round income");
            Check(_shopA.ShopLocked && lockedTokens.All(t => _shopA.Offers.Any(c=>c.Token==t)) && _shopA.Offers.Count==3,"Locked offers persist on server");
            _shopA.UpgradeShop(); await Wait(() => !_shopA.NetworkPending && _shopA.ShopLevel==3,"Free round-two upgrade");
            Check(_shopA.UpgradeCost==8 && _shopA.Coins==endA+6,"Free upgrade reset");
            _shopA.ToggleShopLock(); await Wait(() => !_shopA.NetworkPending && !_shopA.ShopLocked,"Unlock");
            _shopA.RerollShop(); await Wait(() => !_shopA.NetworkPending && _shopA.Coins==endA+4,"Paid network reroll");
            Check(_shopA.Offers.Count==4 && _shopA.Offers.All(c=>c.Price<=3),"Network level-filtered reroll");
            Check(!_shopA.CanReturnUnit(_shopA.Deployed[aToken].Unit),"Veteran returned");
            Check(!_shopA.OpponentHandZone.Visible && !_shopB.OpponentHandZone.Visible,"Opponent backs remained visible in preparation");
            Check(_shopA.NetworkUnits.Count == 1 && _shopB.NetworkUnits.Count == 1,"Opponent from previous round remained visible");
            Check(_netA.State!.Players[1]!.Units.Length == 0 && _netB.State!.Players[0]!.Units.Length == 0,"Next round formation leaked");
            if (_netA.State?.Phase == "battle") CompareBoard();
            GD.Print("ONLINE: Round 2 preparation. Testing actual 60-second deadline without ready clicks...");
            await Wait(() => _netA.State?.Phase == "battle" && _netB.State?.Phase == "battle","60-second automatic start",65000);
            Check(_netA.State!.Battle!.Round == 2,"Timeout did not start next round");
            Check(JsonSerializer.Serialize(_netA.State.Battle) == JsonSerializer.Serialize(_netB.State!.Battle),"Timeout plans differ");
            _a.GetParent().QueueFree();_b.GetParent().QueueFree();
            await ToSignal(_tree,SceneTree.SignalName.ProcessFrame);
            GD.Print($"PASS ONLINE: {_checks} checks; real Nakama/Postgres, two identities, room capacity, server-only edits, owner-left mirrored orientation and hidden preparation, identical playback, next round and actual 60-second timeout");
            _tree.Quit();
        }
        catch (Exception exception) { GD.PushError("ONLINE TEST FAIL: "+exception);_tree.Quit(1); }
        finally { if (third != null) await third.Close(); }
    }
}
