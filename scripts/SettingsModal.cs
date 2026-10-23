using Godot;
using System;

public partial class SettingsModal : GameModal
{
    public bool FacebookConnected {get;set;}
    public Action? LanguageChanged {get;set;}
    public override void _Ready(){base._Ready();GamePreferences.Load();Cancel=()=>QueueFree();Render();}
    private static string T(string en,string th)=>GamePreferences.Text(en,th);
    private void Render()
    {
        DeckMenuUi.Clear(Body);Body.CustomMinimumSize=new(440,0);Body.AddThemeConstantOverride("separation",16);
        var header=new HBoxContainer();Body.AddChild(header);var title=DeckMenuUi.Text(T("Settings","ตั้งค่า"),26);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);header.AddChild(DeckMenuUi.Button(T("Close","ปิด"),()=>QueueFree(),76,34));
        Body.AddChild(MenuUi.Eyebrow(T("GENERAL","ทั่วไป")));
        var languageRow=new HBoxContainer();Body.AddChild(languageRow);var label=DeckMenuUi.Text(T("Language","ภาษา"),16);label.SizeFlagsHorizontal=SizeFlags.ExpandFill;languageRow.AddChild(label);
        var picker=new OptionButton {Name="SettingsLanguage",CustomMinimumSize=new(190,38)};picker.AddItem("English");picker.AddItem("ไทย");picker.Selected=GamePreferences.Thai?1:0;languageRow.AddChild(picker);
        picker.ItemSelected+=index=>{GamePreferences.SetLanguage(index==1?"th":"en");LanguageChanged?.Invoke();Render();};
        Body.AddChild(MenuUi.Eyebrow(T("AUDIO","เสียง")));Volume(false,T("Sound effects","เสียงเอฟเฟกต์"),GamePreferences.Effects);Volume(true,T("Music","เสียงดนตรี"),GamePreferences.Music);
        Body.AddChild(MenuUi.Eyebrow(T("ACCOUNT","บัญชี")));
        var facebook=DeckMenuUi.Button(FacebookConnected?T("Facebook connected","เชื่อมต่อ Facebook แล้ว"):T("Connect Facebook · Coming soon","เชื่อมต่อ Facebook · เร็ว ๆ นี้"),()=>{},440,38);facebook.Name="ConnectFacebookButton";facebook.Disabled=true;Body.AddChild(facebook);
        var delete=DeckMenuUi.Button(T("Delete account · Coming soon","ลบบัญชี · เร็ว ๆ นี้"),()=>{},440,38);delete.Name="DeleteAccountButton";delete.Disabled=true;delete.GetNode<Label>("Text").AddThemeColorOverride("font_color",new Color("e17e8f"));Body.AddChild(delete);
        string version=ProjectSettings.GetSetting("application/config/version","").AsString();
        var versionLabel=DeckMenuUi.Text(T("Game version","เวอร์ชันเกม")+"  "+(string.IsNullOrWhiteSpace(version)?T("Development build","รุ่นพัฒนา"):version),12);versionLabel.Name="GameBuildVersion";versionLabel.HorizontalAlignment=HorizontalAlignment.Center;Body.AddChild(versionLabel);
    }
    private void Volume(bool music,string title,double value)
    {
        var group=new VBoxContainer();group.AddThemeConstantOverride("separation",6);Body.AddChild(group);
        var row=new HBoxContainer();group.AddChild(row);var name=DeckMenuUi.Text(title,16);name.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(name);var amount=DeckMenuUi.Text($"{value:0}%",14);row.AddChild(amount);
        var slider=new HSlider {Name=music?"SettingsMusicVolume":"SettingsEffectsVolume",MinValue=0,MaxValue=100,Step=1,Value=value,CustomMinimumSize=new(0,24)};group.AddChild(slider);slider.ValueChanged+=v=>{amount.Text=$"{v:0}%";GamePreferences.SetVolume(music,v);};
    }
}
