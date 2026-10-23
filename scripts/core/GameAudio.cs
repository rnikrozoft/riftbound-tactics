using Godot;

// Keep ambience playing across scene changes and respect the existing audio settings.
public partial class GameAudio : Node
{
    private AudioStreamPlayer _reroll = null!;
    private AudioStreamPlayer _cardDraw = null!;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GamePreferences.Load();
        var forest = (AudioStreamWav)GD.Load<AudioStreamWav>("res://assets/sound/Forest Day.wav").Duplicate();
        forest.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        forest.LoopBegin = 0;
        forest.LoopEnd = (int)System.Math.Round(forest.GetLength() * forest.MixRate);
        var background = new AudioStreamPlayer { Name = "Background", Bus = "Music", Stream = forest };
        AddChild(background);
        _reroll = new AudioStreamPlayer {
            Name = "ShopReroll", Bus = "SFX", MaxPolyphony = 4,
            Stream = GD.Load<AudioStreamWav>("res://assets/sound/dice_grab.wav")
        };
        AddChild(_reroll);
        _cardDraw = new AudioStreamPlayer {
            Name = "CardDraw", Bus = "SFX", MaxPolyphony = 4,
            Stream = GD.Load<AudioStreamWav>("res://assets/sound/card_draw_3.wav")
        };
        AddChild(_cardDraw);
        background.Play();
    }

    public static void PlayShopReroll(Node context) =>
        context.GetNodeOrNull<GameAudio>("/root/GameAudio")?._reroll.Play();

    public static void PlayCardDraw(Node context) =>
        context.GetNodeOrNull<GameAudio>("/root/GameAudio")?._cardDraw.Play();
}
