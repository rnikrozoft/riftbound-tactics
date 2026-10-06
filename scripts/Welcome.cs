using Godot;
using System;

public partial class Welcome : Control
{
    private NakamaConnection? _connection;
    private bool _busy,_exiting;
    private TextureButton _guest=null!,_continue=null!;
    private Label _status=null!,_error=null!;
    private LineEdit _name=null!;
    private GameModal _modal=null!;
    public override void _Ready()
    {
        var page=DeckMenuUi.Page(this);
        var center=new CenterContainer {SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(center);
        var body=new VBoxContainer {CustomMinimumSize=new(420,0)};body.AddThemeConstantOverride("separation",20);center.AddChild(body);
        var title=DeckMenuUi.Text("RIFTBOUND\nTACTICS",48);title.HorizontalAlignment=HorizontalAlignment.Center;title.AddThemeColorOverride("font_color",GameUi.Gold);body.AddChild(title);
        var subtitle=DeckMenuUi.Text("BUILD YOUR ARMY. RULE THE ARENA.",15);subtitle.HorizontalAlignment=HorizontalAlignment.Center;body.AddChild(subtitle);
        body.AddChild(new ArenaPreview {CustomMinimumSize=new(420,200)});
        _guest=DeckMenuUi.Button("LOGIN WITH GUEST",LoginGuest,420,48);GameUi.Primary(_guest);body.AddChild(_guest);
        _status=DeckMenuUi.Text("",14);_status.HorizontalAlignment=HorizontalAlignment.Center;body.AddChild(_status);
        var layer=new CanvasLayer {Layer=30};AddChild(layer);_modal=new GameModal();layer.AddChild(_modal);
        _modal.Body.AddChild(DeckMenuUi.Text("CHOOSE YOUR NAME",24));
        _name=DeckMenuUi.Input("","Your commander name",332);_name.MaxLength=24;_modal.Body.AddChild(_name);
        _error=DeckMenuUi.Text("",14);_modal.Body.AddChild(_error);
        _continue=DeckMenuUi.Button("ENTER LOBBY",ConfirmName,332,44);GameUi.Primary(_continue);_modal.Body.AddChild(_continue);
        _name.TextSubmitted+=_=>ConfirmName();_modal.Hide();
    }
    public async void LoginGuest()
    {
        if(_busy||_exiting)return;_busy=true;_guest.Disabled=true;_status.Text="Connecting...";
        try {
            PlayerInventory.Reset();GameAccount.RequiresLogin=false;GameAccount.Session=null;BattleRecovery.Notice="";_connection=GameAccount.Connection();await _connection.Connect();
            if(_exiting){await _connection.Close();return;}
            await PlayerInventory.Load(_connection,importLegacy:true);
            var account=await _connection.Client.GetAccountAsync(_connection.Session);
            if(_exiting)return;
            _name.Text=account.User.DisplayName??"";_status.Text="";_modal.Show();_name.GrabFocus();
        }catch(Exception e){if(!_exiting){_status.Text="Unable to login: "+e.Message;GD.Print(e.Message);}if(_connection!=null)await _connection.Close();_connection=null;}
        finally{_busy=false;if(!_exiting)_guest.Disabled=false;}
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
