using Godot;
using System;
public partial class AccountInstanceRunner : Node
{
    public override async void _Ready()
    {
        try
        {
            var connection=GameAccount.Connection();await connection.Connect();
            GD.Print($"INSTANCE_ACCOUNT slot={GameAccount.InstanceSlot} uid={connection.Session.UserId}");
            await ToSignal(GetTree().CreateTimer(5),SceneTreeTimer.SignalName.Timeout);
            await connection.Close();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
