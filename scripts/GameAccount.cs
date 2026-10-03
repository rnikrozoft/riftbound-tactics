using Godot;
using System;
using System.IO;

public static class GameAccount
{
    private static string? _deviceId;
    // Keep an exclusive slot lease until this process exits. Other running
    // instances use another persisted slot instead of sharing the same account.
    private static FileStream? _instanceLease;
    public static int InstanceSlot {get;private set;}
    private static string AccountPath()
    {
        for(int slot=1;slot<=64;slot++)
        {
            string lease=ProjectSettings.GlobalizePath($"user://account-instance-{slot}.lock");
            try {_instanceLease=new FileStream(lease,FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){continue;}
            InstanceSlot=slot;
            return slot==1?"user://account-device.txt":$"user://account-device-{slot}.txt";
        }
        throw new Exception("Cannot reserve an account slot for this game instance.");
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
    public static NakamaConnection Connection()
    {
        string host=OS.GetEnvironment("RIFTBOUND_HOST");if(string.IsNullOrWhiteSpace(host))host="127.0.0.1";
        return new NakamaConnection(host,deviceId:DeviceId);
    }
}
