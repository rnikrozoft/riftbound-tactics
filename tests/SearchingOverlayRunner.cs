using Godot;
using System;
using System.Reflection;
public partial class SearchingOverlayRunner:Node
{
 public override async void _Ready(){try{
  BattleLaunch.Matchmaking=true;var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var demo=field.GetNode<BattleDemo>("BattleDemo");demo.NetworkEnabled=true;demo.AutoStart=true;AddChild(field);var online=field.GetNode<OnlineBattle>("OnlineBattle");typeof(OnlineBattle).GetField("_exiting",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(online,true);
  var overlay=(Control)typeof(OnlineBattle).GetField("_searchOverlay",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
  for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  if(overlay.Size.DistanceTo(GetViewport().GetVisibleRect().Size)>1)throw new Exception("Overlay not fullscreen");
  var box=overlay.GetChild<CenterContainer>(1).GetChild<VBoxContainer>(0);if(box.GetGlobalRect().GetCenter().DistanceTo(GetViewport().GetVisibleRect().Size/2)>2)throw new Exception("Search UI not centered");
  foreach(var path in new[]{"UI/SafeArea/Content/ProfileA","UI/SafeArea/Content/ProfileB"}){var profile=field.GetNode<PlayerProfile>(path);foreach(Node node in profile.GetChildren())if(node is PixelHealthBar bar&&(bar.Position.Y+bar.Size.Y>profile.Size.Y-8||bar.Size.Y>8))throw new Exception("Health bar outside profile");}
  if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/searching-overlay.png");}
  GD.Print("SEARCH OVERLAY PASS: fullscreen dimmer, centered search and cancel, health bar within profile panel");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
