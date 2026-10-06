using Godot;
using System;
using System.IO;

public static class GameAccount
{
    public static string DisplayName {get;set;}="";
    private static string? _deviceId;
    // Reserve a persisted account slot for this process. Parallel game instances
    // get different identities; restarting reuses the first available saved slot.
    private static FileStream? _instanceLease;
    public static int InstanceSlot {get;private set;}
    internal static Nakama.ISession? Session;
    public static bool RequiresLogin {get;set;}
    private static string AccountPath()
    {
        for(int slot=1;slot<=64;slot++){
            string lease=ProjectSettings.GlobalizePath($"user://account-instance-{slot}.lock");
            try{_instanceLease=new FileStream(lease,FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){continue;}
            InstanceSlot=slot;
            return slot==1?"user://account-device.txt":$"user://account-device-{slot}.txt";
        }
        throw new InvalidOperationException("Cannot reserve an account slot for this game instance.");
    }
    public static string DeviceId
    {
        get
        {
            if(_deviceId!=null)return _deviceId;
            string path=AccountPath();
            if(Godot.FileAccess.FileExists(path))
            {
                string existing=Godot.FileAccess.GetFileAsString(path).Trim();
                if(!string.IsNullOrWhiteSpace(existing))return _deviceId=existing;
            }
            string id="rift-player-"+Guid.NewGuid().ToString("N");
            using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Write);
            if(file==null)throw new Exception("Cannot save player identity: "+Godot.FileAccess.GetOpenError());
            file.StoreString(id);return _deviceId=id;
        }
    }
    private static string? _serverHost;
    public static string ServerHost
    {
        get {
            if(_serverHost!=null)return _serverHost;
            string configured=OS.GetEnvironment("RIFTBOUND_HOST").Trim();
            if(configured==""&&Godot.FileAccess.FileExists("user://server-host.txt"))configured=Godot.FileAccess.GetFileAsString("user://server-host.txt").Trim();
            return _serverHost=configured==""?"127.0.0.1":configured;
        }
    }
    public static bool SetServerHost(string host)
    {
        host=host.Trim();
        if(Uri.CheckHostName(host)==UriHostNameType.Unknown)return false;
        using var file=Godot.FileAccess.Open("user://server-host.txt",Godot.FileAccess.ModeFlags.Write);
        if(file==null)return false;
        file.StoreString(host);_serverHost=host;Session=null;return true;
    }
    public static NakamaConnection Connection()
    {
        if(RequiresLogin)throw new InvalidOperationException("Login again to continue.");
        return new NakamaConnection(ServerHost,deviceId:DeviceId,session:Session);
    }
}
