using Godot;

public static class GamePreferences
{
    public static string Language {get;private set;}="en";
    public static double Effects {get;private set;}=80;
    public static double Music {get;private set;}=60;
    private static bool _loaded;
    public static bool Thai=>Language=="th";
    public static string Text(string english,string thai)=>Thai?thai:english;
    public static void Load()
    {
        if(_loaded)return;_loaded=true;
        var file=new ConfigFile();if(file.Load("user://settings.cfg")==Error.Ok){Language=file.GetValue("general","language","en").AsString()=="th"?"th":"en";Effects=Mathf.Clamp(file.GetValue("audio","effects",80d).AsDouble(),0,100);Music=Mathf.Clamp(file.GetValue("audio","music",60d).AsDouble(),0,100);}
        Apply();
    }
    public static void SetLanguage(string language){Language=language=="th"?"th":"en";Apply();Save();}
    public static void SetVolume(bool music,double value){if(music)Music=Mathf.Clamp(value,0,100);else Effects=Mathf.Clamp(value,0,100);Apply();Save();}
    private static void Apply()
    {
        TranslationServer.SetLocale(Language);
        foreach(var item in new[]{("SFX",Effects),("Music",Music)}){
            int bus=AudioServer.GetBusIndex(item.Item1);if(bus<0){AudioServer.AddBus();bus=AudioServer.BusCount-1;AudioServer.SetBusName(bus,item.Item1);AudioServer.SetBusSend(bus,"Master");}
            AudioServer.SetBusMute(bus,item.Item2==0);AudioServer.SetBusVolumeDb(bus,item.Item2>0?Mathf.LinearToDb((float)item.Item2/100):-80);
        }
    }
    private static void Save()
    {
        var file=new ConfigFile();file.SetValue("general","language",Language);file.SetValue("audio","effects",Effects);file.SetValue("audio","music",Music);var error=file.Save("user://settings.cfg");if(error!=Error.Ok)GD.PushWarning("Settings: "+error);
    }
}
