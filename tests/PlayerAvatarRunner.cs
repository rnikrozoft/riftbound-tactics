using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerAvatarRunner : Node
{
    public override async void _Ready()
    {
        try{
            int checks=0;void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
            Check(!AvatarProfiles.CanUseFacebookPhoto("user","","https://example.com/photo.png"),"Unlinked avatar URL must not be used");
            Check(AvatarProfiles.CanUseFacebookPhoto("user","facebook-id","https://example.com/photo.png"),"Linked Facebook photo must be eligible");
            Check(!AvatarProfiles.CanUseFacebookPhoto("bot:1","facebook-id","https://example.com/photo.png"),"Bots must use default avatar");
            Check(!AvatarProfiles.CanUseFacebookPhoto("user","facebook-id","bad-url"),"Invalid photo must fall back");
            var image=Image.CreateEmpty(32,32,false,Image.Format.Rgba8);image.Fill(new Color("6bddc5"));
            Check(AvatarProfiles.Decode(image.SavePngToBuffer()) is ImageTexture,"Avatar image decoder failed");
            var profile=GD.Load<PackedScene>("res://scenes/player_profile.tscn").Instantiate<PlayerProfile>();AddChild(profile);profile.SetPlayer("bot:avatar-test",null,"");
            Check(profile.GetNode<PlayerAvatar>("PortraitFrame/Portrait").Texture==AvatarProfiles.Default,"Profile must use Godot logo without Facebook");
            var host=new Control();AddChild(host);var roster=new ArenaRoster();host.AddChild(roster);
            roster.UpdateRoster(new OnlineState {Roster=new[]{new OnlineStanding {UserId="bot:avatar-test",Bot=true,Hp=30}}},"",new Dictionary<string,string>());
            var portrait=roster.FindChild("Portrait",true,false) as PlayerAvatar;
            Check(portrait!=null&&portrait.Texture==AvatarProfiles.Default,"Roster must use default avatar");
            Check(roster.FindChildren("*","CardIdlePortrait",true,false).Count==0,"Roster still contains character portraits");
            var avatar=new PlayerAvatar();AddChild(avatar);avatar.Bind("unlinked-avatar-test");AvatarProfiles.Set("unlinked-avatar-test","","");
            Check(avatar.Texture==AvatarProfiles.Default,"Unlinked account did not reset to default");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print($"PLAYER AVATAR PASS: {checks} checks");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
