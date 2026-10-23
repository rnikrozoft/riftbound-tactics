using Godot;
using System;

public partial class Welcome : Control
{
    private NakamaConnection? _connection;
    private bool _busy,_exiting;
    private PixelLoadingIndicator _loader=null!;
    public override void _Process(double delta){if(_loader!=null)_loader.Visible=_busy;}
    private TextureButton _guest=null!,_continue=null!;
    private Label _status=null!,_error=null!;
    private LineEdit _name=null!,_server=null!;
    private GameModal _modal=null!;
    public override void _Ready()
    {
        if(OS.IsDebugBuild()&&OS.GetEnvironment("RIFT_IOS_NETWORK_TEST")=="1"){AddChild(new IosNetworkRunner());return;}
        var page=DeckMenuUi.Page(this);
        var brand=MenuUi.Eyebrow("RIFTBOUND TACTICS   /   TACTICAL AUTO BATTLER");page.AddChild(brand);
        var center=new CenterContainer {SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(center);
        var body=new HBoxContainer();body.AddThemeConstantOverride("separation",64);center.AddChild(body);
        var hero=new VBoxContainer {CustomMinimumSize=new(480,0),SizeFlagsVertical=SizeFlags.ShrinkCenter};body.AddChild(hero);hero.Visible=!MobileLayout.Enabled;
        hero.AddChild(MenuUi.Eyebrow("BUILD YOUR STRATEGY"));
        hero.AddChild(DeckMenuUi.Text("A small army.\nA bigger plan.",50));
        var intro=DeckMenuUi.Text("Choose your heroes, shape your deck,\nand find your next rival in the arena.",17);hero.AddChild(intro);
        var arena=new PanelContainer {CustomMinimumSize=new(480,220)};arena.AddThemeStyleboxOverride("panel",MenuUi.Box(false,4));hero.AddChild(arena);
        arena.AddChild(new ArenaPreview {CustomMinimumSize=new(460,208)});
        var form=DeckMenuUi.Panel(body,MobileLayout.Enabled?420:400);form.GetParent<Control>().SizeFlagsVertical=SizeFlags.ShrinkCenter;
        form.AddChild(MenuUi.Eyebrow("WELCOME, COMMANDER"));form.AddChild(DeckMenuUi.Text("Enter the arena",30));
        var subtitle=DeckMenuUi.Text("Connect to play, or explore combat offline.",14);subtitle.AutowrapMode=TextServer.AutowrapMode.WordSmart;form.AddChild(subtitle);
        form.AddChild(new Control {CustomMinimumSize=new(0,12)});
        form.AddChild(DeckMenuUi.Text("Server address",14));
        _server=DeckMenuUi.Input(GameAccount.ServerHost,"127.0.0.1",360);_server.Name="ServerIpInput";form.AddChild(_server);
        _server.TextSubmitted+=_=>LoginGuest();
        _guest=DeckMenuUi.Button("Continue as guest",LoginGuest,360,48);GameUi.Primary(_guest);form.AddChild(_guest);
        form.AddChild(new HSeparator());
        form.AddChild(DeckMenuUi.Text("JUST HERE TO EXPERIMENT?",12));
        var simulator=DeckMenuUi.Button("Open Combat Simulator",()=>{
            if(_busy)return;
            CombatSimulatorLab.ReturnScene="res://scenes/welcome.tscn";
            GetTree().ChangeSceneToFile("res://scenes/combat_simulator_lab.tscn");
        },360,44);simulator.Name="OfflineCombatSimulatorButton";form.AddChild(simulator);
        _loader=new PixelLoadingIndicator {Name="DataLoadingSpinner",Visible=false};form.AddChild(_loader);
        _status=DeckMenuUi.Text("",14);_status.AutowrapMode=TextServer.AutowrapMode.WordSmart;form.AddChild(_status);
        page.AddChild(DeckMenuUi.Text("Six commanders. One arena. Your strategy makes the difference.",13));
        var layer=new CanvasLayer {Layer=30};AddChild(layer);_modal=new GameModal {MenuStyle=true};layer.AddChild(_modal);
        _modal.Body.AddChild(DeckMenuUi.Text("CHOOSE YOUR NAME",24));
        _name=DeckMenuUi.Input("","Your commander name",332);_name.MaxLength=24;_modal.Body.AddChild(_name);
        _error=DeckMenuUi.Text("",14);_modal.Body.AddChild(_error);
        _continue=DeckMenuUi.Button("ENTER LOBBY",ConfirmName,332,44);GameUi.Primary(_continue);_modal.Body.AddChild(_continue);
        _name.TextSubmitted+=_=>ConfirmName();_modal.Hide();
    }
    public async void LoginGuest()
    {
        if(_busy||_exiting)return;
        if(!GameAccount.SetServerHost(_server.Text)){_status.Text="Enter a valid server IP or hostname (without http:// or port).";return;}
        _busy=true;_server.Editable=false;_guest.Disabled=true;_status.Text="Connecting...";
        try {
            PlayerInventory.Reset();GameAccount.RequiresLogin=false;GameAccount.Session=null;BattleRecovery.Notice="";_connection=GameAccount.Connection();await _connection.Connect();
            if(_exiting){await _connection.Close();return;}
            await PlayerInventory.Load(_connection,importLegacy:true);
            var account=await _connection.Client.GetAccountAsync(_connection.Session);
            if(_exiting)return;
            AvatarProfiles.Update(account.User);
            _name.Text=account.User.DisplayName??"";_status.Text="";_modal.Show();_name.GrabFocus();
        }catch(Exception e){if(!_exiting){_status.Text="Unable to login: "+e.Message;GD.PushError(e.ToString());}if(_connection!=null)await _connection.Close();_connection=null;}
        finally{_busy=false;if(!_exiting){_guest.Disabled=false;_server.Editable=true;}}
    }
    public async void ConfirmName()
    {
        if(_busy||_exiting||_connection==null)return;
        string name=_name.Text.Trim();if(name.Length<2||name.Length>24){_error.Text="Please enter a name with 2–24 characters.";return;}
        _busy=true;_continue.Disabled=true;_error.Text="Saving...";
        try {
            await _connection.Client.UpdateAccountAsync(_connection.Session,username:null,displayName:name);
            GameAccount.DisplayName=name;await _connection.Close();if(_exiting)return;
            GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
        }catch(Exception){if(!_exiting)_error.Text="Unable to save your name. Please try again.";}
        finally{_busy=false;if(!_exiting)_continue.Disabled=false;}
    }
    public override void _ExitTree(){_exiting=true;if(_connection!=null)_=_connection.Close();}
}
