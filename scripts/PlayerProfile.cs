using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class PlayerProfile : NinePatchRect
{
    [Export] public string Team { get; set; } = "A";
    private Label _name = null!, _uid = null!, _rank = null!, _score = null!;
    private Label _coins = null!;
    private int _hp = 30;
    public void SetHealth(int hp) { _hp = hp; UpdateBalance(); }
    private void UpdateBalance() { if (_coins != null) _coins.Text = $"Coin: {_balance}  |  HP: {_hp}/30"; }
    private int _balance = CardShop.StartingCoins;
    public void SetCoins(int coins) { _balance = coins; UpdateBalance(); }
    private TextureRect _portrait = null!;
    private Texture2D _fallbackPortrait = null!;
    private string _userId = "";
    private int _generation;
    private bool _exiting;

    public override void _Ready()
    {
        _name = GetNode<Label>("PlayerName"); _uid = GetNode<Label>("Uid");
        _rank = GetNode<Label>("Rank"); _score = GetNode<Label>("Score");
        _coins = GetNode<Label>("Coins"); SetCoins(_balance);
        _portrait = GetNode<TextureRect>("PortraitFrame/Portrait");
        var character = GD.Load<PackedScene>(Team == "A" ? "res://scenes/Character.tscn" : "res://scenes/Orc.tscn").Instantiate<Node2D>();
        var sprite = character.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _fallbackPortrait = new AtlasTexture { Atlas = sprite.SpriteFrames.GetFrameTexture("idle", 0), Region = new Rect2(30,24,40,52) };
        _portrait.Texture = _fallbackPortrait;
        character.Free();
        ShowMock();
    }
    private void ShowMock()
    {
        _name.Text = $"PLAYER {Team} / {(Team == "A" ? "Knight" : "Raider")}";
        _uid.Text = "UID: " + (string.IsNullOrEmpty(_userId) ? $"mock-player-{Team.ToLowerInvariant()}" : _userId);
        _uid.TooltipText = _uid.Text;
        _rank.Text = "Rank: Silver II (mock)";
        _score.Text = "Leaderboard: 1,250 (mock)";
    }
    public void SetPlayer(string userId, NakamaConnection? connection, string leaderboardId, bool refresh = false)
    {
        if (_exiting || (!refresh && userId == _userId)) return;
        _userId = userId; int generation = ++_generation;
        ShowMock();
        _portrait.Texture = _fallbackPortrait;
        if (!string.IsNullOrWhiteSpace(userId) && connection != null) LoadProfile(connection, userId, leaderboardId, generation);
    }
    private bool Current(int generation) => !_exiting && generation == _generation && IsInsideTree();
    private async void LoadProfile(NakamaConnection connection, string userId, string leaderboardId, int generation)
    {
        try
        {
            var users = await connection.Client.GetUsersAsync(connection.Session, new[] { userId });
            if (!Current(generation)) return;
            var user = users.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                string name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
                if (!string.IsNullOrWhiteSpace(name)) { _name.Text = $"PLAYER {Team} / {name}"; _name.TooltipText = name; }
                if (!string.IsNullOrWhiteSpace(user.Metadata))
                {
                    try
                    {
                        using var metadata = JsonDocument.Parse(user.Metadata);
                        if (metadata.RootElement.TryGetProperty("rank", out var rank) && rank.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(rank.GetString()))
                            _rank.Text = "Rank: " + rank.GetString();
                    }
                    catch (JsonException) { }
                }
                LoadAvatar(user.AvatarUrl, generation);
            }
        }
        catch (Exception exception) { if (Current(generation)) GD.PushWarning("Player profile lookup: " + exception.Message); }
        if (!Current(generation) || string.IsNullOrWhiteSpace(leaderboardId)) return;
        try
        {
            var records = await connection.Client.ListLeaderboardRecordsAsync(connection.Session, leaderboardId, new[] { userId }, limit: 1);
            if (!Current(generation)) return;
            var record = records.OwnerRecords.FirstOrDefault(r => r.OwnerId == userId);
            if (record == null) return;
            _score.Text = "Leaderboard: " + record.Score;
            if (_rank.Text.EndsWith("(mock)") && !string.IsNullOrEmpty(record.Rank)) _rank.Text = "Rank: #" + record.Rank;
        }
        catch (Exception exception) { if (Current(generation)) GD.PushWarning("Player leaderboard lookup: " + exception.Message); }
    }
    private void LoadAvatar(string url, int generation)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")) return;
        var request = new HttpRequest { Timeout = 8, BodySizeLimit = 4 * 1024 * 1024 };
        AddChild(request);
        request.RequestCompleted += (result, code, headers, body) => {
            if (Current(generation) && result == (long)HttpRequest.Result.Success && code == 200)
            {
                var image = new Image();
                var error = image.LoadPngFromBuffer(body);
                if (error != Error.Ok) error = image.LoadJpgFromBuffer(body);
                if (error != Error.Ok) error = image.LoadWebpFromBuffer(body);
                if (error == Error.Ok) _portrait.Texture = ImageTexture.CreateFromImage(image);
            }
            request.QueueFree();
        };
        if (request.Request(url) != Error.Ok) request.QueueFree();
    }
    public override void _ExitTree() { _exiting = true; _generation++; }
}