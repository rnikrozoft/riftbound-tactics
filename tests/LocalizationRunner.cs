using Godot;
using System;
using System.Linq;
using System.Text.RegularExpressions;

public partial class LocalizationRunner : Node
{
    public override async void _Ready()
    {
        string original=GamePreferences.Language;
        try{
            int checks=0;
            for(int frame=0;frame<16;frame++)if(GD.Load<Texture2D>($"res://assets/effects/four-fire_v1.1.0/frames/wide-fire_red_32/{frame:000}.png")==null)throw new Exception("Missing fire animation frame");
            void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
            foreach(var character in CharacterData.All.Where(c=>c.Enabled))
                for(int tier=1;tier<=4;tier++) {
                    string description=CharacterData.AttackDescription(character.Kind,tier);
                    Check(!description.Contains("ลำดับการโจมตี")&&!description.Contains(" • โจมตีประชิด"),"Basic attack boilerplate remains");
                    Check(!description.Contains("คิวที่")&&!description.Contains("คริติคอล"),"Description retains turn cycle or invents critical mechanics");
                }
            Check(Localization.Translate("คิวที่ 2 ของทุก 3 คิว: โจมตี 2 ครั้ง","en").StartsWith("Action 2 of every 3 turns:"),"Ability cadence translation");
            Check(Localization.Translate("WIN","th")=="ชนะ"&&Localization.Translate("LOSS","th")=="แพ้","Round result must be fully localized");
            Check(Localization.Translate("Play","th")=="เล่น","Main menu translation missing");
            Check(Localization.Translate("BUY / 120 COINS","th")=="ซื้อ / 120 เหรียญ","Dynamic purchase text");
            Check(Localization.Translate("OWNED / 42","th")=="มีแล้ว / 42","Dynamic count");
            var label=new Label {Text="Play"};AddChild(label);
            var player=new Label {Text="Play",AutoTranslateMode=AutoTranslateModeEnum.Disabled};AddChild(player);
            Localization.Refresh(this);GamePreferences.SetLanguage("th");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(label.Tr(label.Text)=="เล่น"&&label.Text=="Play","Native translation must preserve source text");
            var config=new ConfigFile();Check(config.Load("user://settings.cfg")==Error.Ok&&config.GetValue("general","language").AsString()=="th","Selected language must persist");
            label.Text="FIELD 3/6";Localization.Refresh(this);Check(label.Tr(label.Text)=="สนาม 3/6","Dynamic HUD updates must translate");
            label.Text="Play";
            Check(player.AutoTranslateMode==AutoTranslateModeEnum.Disabled,"Player name protection");
            GamePreferences.SetLanguage("en");Check(label.Tr(label.Text)=="Play","Switch back to English");
            var remote=new TranslationDocument {
                Messages=Localization.BundledDocument().Messages.Select(p=>new[]{p[0],p[1],p[0],p[1]}).ToList(),
                Fragments=Localization.BundledDocument().Fragments.Select(p=>new[]{p[0],p[1],p[0],p[1]}).ToList()
            };
            var play=remote.Messages.First(p=>p[0]=="Play");play[2]="Start match";play[3]="เริ่มเล่น";
            var cost=remote.Messages.First(p=>p[0]=="Cost {0}");cost[2]="Price {0}";cost[3]="ใช้ {0} เหรียญ";
            remote.Fragments.First(p=>p[0]=="HP")[2]="Health";
            Localization.ApplyDocument(remote);
            Check(Localization.Translate("HP 30","en")=="Health 30","Admin fragment edits must apply to English source text");
            Check(Localization.Translate("Play","en")=="Start match"&&Localization.Translate("Play","th")=="เริ่มเล่น","Edited English/Thai must preserve the original source keys");
            Check(Localization.Translate("Cost 5","en")=="Price 5"&&Localization.Translate("Cost 5","th")=="ใช้ 5 เหรียญ","Edited dynamic templates must preserve source keys");
            string cacheDir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rift-localization-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(cacheDir);LocalizationCache.TestDirectory=cacheDir;
            try{
                Check(LocalizationCache.Store("server-a",new string('a',64),remote),"Cache write failed");
                Localization.ApplyDocument(Localization.BundledDocument());LocalizationCache.LoadCached("server-a");
                Check(Localization.Translate("Play","th")=="เริ่มเล่น"&&LocalizationCache.CurrentVersion==new string('a',64),"Offline cache did not restore edited translations");
                Check(LocalizationCache.Read("server-b")==null,"Servers must have separate caches");
                System.IO.File.WriteAllText(System.IO.Directory.GetFiles(cacheDir).Single(),"broken cache");
                LocalizationCache.LoadCached("server-a");Check(Localization.Translate("Play","th")=="เล่น"&&LocalizationCache.CurrentVersion=="","Corrupt cache must use bundled translations");
            }finally{LocalizationCache.TestDirectory=null;System.IO.Directory.Delete(cacheDir,true);Localization.ApplyDocument(Localization.BundledDocument());}
            int missing=0;
            foreach(var character in CharacterData.All.Where(c=>c.Enabled))for(int tier=1;tier<=4;tier++){
                string raw=CharacterData.CombatDescription(character.Kind,tier);
                string english=Localization.Translate(raw,"en");
                if(Regex.IsMatch(english,"[ก-๙]")){missing++;GD.Print("UNTRANSLATED: "+english);}
            }
            Check(missing==0,$"Combat descriptions retain Thai in English: {missing}");
            GD.Print($"LOCALIZATION PASS: {checks} checks and all enabled character tiers");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
        finally{GamePreferences.SetLanguage(original);}
    }
}
