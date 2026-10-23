using Godot;
using System;
using System.Linq;

// Launch on a device with: tests/ios_network.tscn -- --server=192.168.0.103
public partial class IosNetworkRunner : Node
{
    public override async void _Ready()
    {
        string stage="initialization",report;
        var host=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--server="))?[9..]??OS.GetEnvironment("RIFT_IOS_NETWORK_HOST");
        if(string.IsNullOrEmpty(host))host="127.0.0.1";
        var connection=new NakamaConnection(host,deviceId:"rift-ios-network-smoke");
        try {
            stage="authentication and socket";await connection.Connect();
            stage="account";var account=await connection.Client.GetAccountAsync(connection.Session);
            if(account.User==null)throw new Exception("Account user missing");
            GameAccount.Session=connection.Session;
            stage="bootstrap RPC";await PlayerInventory.Load(connection,importLegacy:false);
            if(!PlayerInventory.Ready)throw new Exception("Bootstrap not ready");
            report="PASS: authentication, realtime socket, account and player bootstrap over LAN.";
        }catch(Exception e){report="FAIL at "+stage+": "+e;}
        finally{await connection.Close();}
        using(var file=FileAccess.Open("user://ios-network-test.txt",FileAccess.ModeFlags.Write))file.StoreString(report);
        GD.Print(report);GetTree().Quit(report.StartsWith("PASS")?0:1);
    }
}
