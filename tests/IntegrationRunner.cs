using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class IntegrationRunner : Node
{
    private Node2D _field = null!;
    private CardShop _shop = null!;
    private BattleDemo _battle = null!;
    private SceneTree _tree = null!;
    private int _assertions;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private int FreshOffer()
    {
        for(int attempt=0;attempt<100;attempt++) {
            var card = _shop.Offers.FirstOrDefault(c=>!_shop.Hand.Any(h=>h.Name==c.Name)&&!_shop.Deployed.Values.Any(d=>d.Card.Name==c.Name));
            if(card!=null) return card.Token;
            _shop.RollShop();
        }
        throw new InvalidOperationException("No distinct card for capacity test");
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(_tree,SceneTree.SignalName.ProcessFrame);
    }
    private async Task Until(Func<bool> predicate, string message)
    {
        ulong deadline = Time.GetTicksMsec() + 20000;
        while (!predicate()) { Check(Time.GetTicksMsec() < deadline,message); await Frames(1); }
    }
    private Vector2 Screen(Vector2 world) => GetViewport().GetCanvasTransform() * world;
    private void Mouse(Vector2 position, bool pressed, bool motion)
    {
        if (DisplayServer.GetName() != "headless") DisplayServer.WarpMouse((Vector2I)position);
        InputEvent input = motion
            ? new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = new(40,40), ButtonMask = pressed ? MouseButtonMask.Left : 0 }
            : new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = pressed };
        GetViewport().PushInput(input,true);
    }
    private async Task Drag(Vector2 start, Vector2 end)
    {
        Mouse(start,false,true); await Frames(1);
        Mouse(start,true,false); await Frames(1);
        Mouse(start + new Vector2(25,25),true,true); await Frames(1);
        Mouse(end,true,true); await Frames(1);
        Check(GetViewport().GuiIsDragging(),"Native GUI drag did not start");
        Mouse(end,false,false); await Frames(4);
    }
    private async Task Click(Vector2 position)
    {
        Mouse(position,false,true); await Frames(1);
        Mouse(position,true,false); await Frames(1);
        Mouse(position,false,false); await Frames();
    }
    private CardDrag HandData(int token) => new() { Shop = _shop,Token = token };
    private async void Run()
    {
        _tree = GetTree();
        try
        {
            _field = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            _battle = _field.GetNode<BattleDemo>("BattleDemo");
            _battle.NetworkEnabled = false; _battle.ArenaFloatAmplitude = 0;
            _battle.StartDelay = .01f; _battle.TurnDelay = .001f; _battle.DashDuration = .01f;
            _battle.ReturnDuration = .01f; _battle.HitstopDuration = .01f;
            _battle.DamageMin = _battle.DamageMax = 100;
            AddChild(_field);
            _shop = _field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            await Until(() => _shop.Drafting,"Preparation did not start"); await Frames();
            Check(_shop.Offers.Count == 3 && _shop.Offers.Select(c => c.Token).Distinct().Count() == 3,"Shop sampling");
            Check(_shop.Hand.Count == 0 && _shop.HandRow.GetChildCount() == 0,"No placeholder controls");
            Check(_shop.Deployed.Count == 0,"Demo allies removed");
            Check(!_shop.IsProcessing(),"No per-frame details polling");
            Check(!_shop.FieldZone!.IsProcessing(),"No idle grid redraw");
            var enemies = _field.GetChildren().OfType<BattleUnit>().Where(u => !u.IsAlly).ToArray();
            Check(enemies.Length == 6,"Six enemies");
            foreach (var unit in enemies)
            {
                var cell = unit.GetMeta("grid_cell").AsVector2I();
                Check(unit.Position == _shop.Tiles.MapToLocal(cell),"Enemy centered");
                Check(cell.Y is 14 or 18 or 22,"Enemy row alignment");
                foreach (var anim in unit.Sprite.SpriteFrames.GetAnimationNames()) unit.Sprite.SpriteFrames.SetAnimationSpeed(anim,60);
            }
            _shop.ConfirmTurn(); Check(_shop.Drafting,"Battle requires an ally");

            await Drag(_shop.ShopRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter(),new(60,230));
            Check(_shop.Hand.Count == 1,"Loose shop drop buys");
            int token = _shop.Hand[0].Token;
            await Click(_shop.HandRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter());
            Check(_shop.Details.Visible && _shop.ZoneB.OffsetRight == 0,"Card popup preserves footer");
            await Click(new(50,80));
            Check(!_shop.Details.Visible && _shop.ZoneB.OffsetRight == 0,"Empty click closes");
            await Drag(_shop.HandRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter(),
                Screen(_shop.Tiles.ToGlobal(_shop.SlotCenters[1]) + new Vector2(-18,8)));
            Check(_shop.Hand.Count == 0 && _shop.Occupants[1]?.CardToken == token,"Loose hand drop auto places nearest");
            var first = _shop.Deployed[token].Unit;
            await Drag(Screen(_field.ToGlobal(first.Position + new Vector2(0,-18))),_shop.ZoneB.GetGlobalRect().GetCenter());
            Check(_shop.Hand.Count == 1 && _shop.Deployed.Count == 0,"New unit returns to hand");
            await Drag(_shop.HandRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter(),_shop.ShopRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter());
            Check(_shop.Hand.Count == 0,"Drag to shop sells");
            // This suite exercises capacity and native drag/drop; economy has its own runner.
            typeof(CardShop).GetProperty(nameof(CardShop.Coins))!.SetValue(_shop, 200);
            while(_shop.ShopLevel<CardShop.MaxShopLevel) _shop.UpgradeShop();
            _shop.RollShop(); _shop.RefreshNetworkControls();
            for (int slot = 0; slot < 6; slot++)
            {
                if (_shop.Offers.Count == 0) _shop.RollShop();
                int cardToken = FreshOffer();
                Check(_shop.TakeCard(cardToken),"Buy to deploy");
                Check(_shop.PlaceCardOrUnit(HandData(cardToken),_shop.Tiles.ToGlobal(_shop.SlotCenters[slot])),"Deploy slot");
            }
            await Frames();
            first = _shop.Occupants[0]!;
            var second = _shop.Occupants[1]!;
            await Drag(Screen(_field.ToGlobal(first.Position + new Vector2(0,-18))),Screen(second.GlobalPosition));
            Check(first.GridSlot == 1 && second.GridSlot == 0,"Native occupied slot swap");
            Check(first.GlobalPosition.IsEqualApprox(_shop.Tiles.ToGlobal(_shop.SlotCenters[1])),"Swap centered");
            if (_shop.Offers.Count == 0) _shop.RollShop();
            _shop.TakeCard(FreshOffer());
            Check(_shop.AutoPlaceSlot(HandData(_shop.Hand[0].Token),Vector2.Zero) == -1,"Full field retains hand");
            while (_shop.Hand.Count < 10)
            { Check(_shop.TakeCard(FreshOffer()),"Distinct purchase for full hand"); }
            Check(!_shop.TakeCard(FreshOffer()),"Full hand rejects purchase");
            int viewCount = _shop.HandRow.GetChildCount();
            Check(viewCount == 10,"Views capped at ten");
            Check(!_shop.CanReturnUnit(first),"Full hand rejects return");
            Check(_shop.SellCard(_shop.Hand[0].Token),"Sell one");
            Check(_shop.HandRow.GetChildCount() == viewCount,"UI controls reused");
            foreach (var entry in _shop.Deployed.Values)
                foreach (var anim in entry.Unit.Sprite.SpriteFrames.GetAnimationNames()) entry.Unit.Sprite.SpriteFrames.SetAnimationSpeed(anim,60);
            _shop.ShowUnitDetails(first);
            first.TakeDamage(1);
            Check(_shop.DetailsText.Text.Contains("HP  99"),"Health signal updates popup");
            first.ResetHealth(); _shop.CloseDetails();
            var probe = new CardDrag { Shop = _shop,Unit = first };
            var probePosition = _shop.Tiles.ToGlobal(_shop.SlotCenters[1]);
            for (int i = 0; i < 1000; i++) _shop.CanPlace(probe,probePosition);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) _shop.CanPlace(probe,probePosition);
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            GD.Print($"PROFILE: 10000 grid validations = {watch.Elapsed.TotalMilliseconds:F2} ms, {allocated} managed bytes (includes stopwatch)");
            await Frames();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("res://tests/preparation-csharp.png");
            _shop.ConfirmTurn(); await Frames();
            Check(!_shop.Drafting && !_shop.ZoneA.Visible && _shop.ZoneB.Visible,"Battle hand visible, shop hidden");
            Check(!_shop.AcceptsHandToken(_shop.Hand[0].Token),"Battle locks drag");
            await Click(_shop.HandRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter());
            Check(_shop.Details.Visible,"Hand inspect during combat");
            _shop.CloseDetails();
            _shop.ShowUnitDetails(first);
            Check(_shop.Details.Visible,"Unit inspect during combat");
            _shop.CloseDetails();
            await Until(() => _shop.Finished,"Combat did not finish");
            Check(_battle.TurnCount == 11,"Alternating combat reaches winner");
            var dead = _field.GetChildren().OfType<BattleUnit>().First(u => u.IsDead);
            Check(_shop.PickDetailUnit(dead.GlobalPosition) != null,"Dead units remain selectable");
            await Click(Screen(_field.ToGlobal(dead.Position + new Vector2(0,-8))));
            Check(_shop.Details.Visible && _shop.DetailsText.Text.Contains("DEAD"),"Native dead-unit inspect");
            _shop.CloseDetails();
            _shop.ConfirmTurn(); await Until(() => _shop.Drafting && _shop.TurnNumber == 2,"Next round");
            Check(_shop.Deployed.Count == 6 && _shop.Hand.Count == 9 && _shop.Offers.Count == _shop.OfferLimit,"Cards persist across rounds");
            foreach (var entry in _shop.Deployed.Values) Check(!entry.Unit.IsDead && entry.Unit.Health == 100,"Round resets HP");
            Check(!_shop.CanReturnUnit(first),"Veteran cannot return even with hand capacity");
            await Frames();
            await Drag(Screen(_field.ToGlobal(first.Position + new Vector2(0,-18))),_shop.ShopRow.GetChild<ShopCard>(0).GetGlobalRect().GetCenter());
            Check(_shop.Deployed.Count == 5 && _shop.Hand.Count == 9,"Veteran drag sells");
            _field.QueueFree(); await Frames();

            // Change scene during an attack/hitstop to verify async teardown.
            var exitField = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            var exitBattle = exitField.GetNode<BattleDemo>("BattleDemo");
            exitBattle.NetworkEnabled = false; exitBattle.CardShopEnabled = false; exitBattle.StartDelay = .001f;
            exitBattle.Impact += (a,b) => exitField.QueueFree();
            AddChild(exitField); await Until(() => !GodotObject.IsInstanceValid(exitField),"Exit during combat");
            await Frames(10);
            GD.Print($"PASS: {_assertions} checks; C# native drag/drop, six slots, swaps, cards, health/death, inspection, round persistence, scene teardown");
            _tree.Quit();
        }
        catch (Exception exception)
        { GD.PushError("INTEGRATION FAIL: " + exception); _tree.Quit(1); }
    }
}
