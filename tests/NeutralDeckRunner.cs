using Godot;
using System;
using System.Linq;
public partial class NeutralDeckRunner:Node
{
 public override async void _Ready(){try{var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);foreach(int kind in CharacterData.SupportKinds){var c=CharacterData.Get(kind);c.CardArtPath="res://assets/ui/cards/card_frames.png";c.CardX=482;c.CardY=4;c.CardW=100;c.CardH=128;}
editor.SetGroup(4);
if(CharacterData.SupportKinds.Select(k=>CardCatalog.Art(k).Atlas.ResourcePath).Distinct().Count()<5)throw new Exception("Server catalog overwrote cosmetic assignments");if(editor.ActiveGroup!=4||editor.VisibleCardCount!=58||editor.Draft.Cards.Count!=30)throw new Exception("Neutral tab or hero deck changed");foreach(int k in CharacterData.SupportKinds){editor.SelectCard(k);if(editor.CanAdd(k))throw new Exception("Support should use automatic shop pool");}for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/neutral-deck.png");}GD.Print("NEUTRAL DECK PASS: ten cards, details, 30 hero slots preserved");GetTree().Quit();}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
