using Godot;
using System;
using System.Linq;

public partial class PlayerProfile : NinePatchRect
{
    [Export] public string Team { get; set; } = "A";
    private Label _name = null!, _uid = null!, _rank = null!, _score = null!;
    private Label _coins = null!;
    private TextureRect? _cardBack;
    public string CardBackId { get; private set; } = CardBackCatalog.DefaultId;
    public void SetCardBack(string? id)
    {
        var back=CardBackCatalog.Find(id)??CardBackCatalog.Find(CardBackCatalog.DefaultId)!;
        CardBackId=back.Id;
        if(_cardBack!=null) { _cardBack.Texture=CardBackCatalog.Texture(CardBackId); }
    }
    private int _hp = 30;
    private ProgressBar? _health;
    public void SetHealth(int hp) { _hp = hp; UpdateBalance(); }
    private void UpdateBalance() { if (_coins != null) _coins.Text = $"{_hp} HP"; if (_health!=null) _health.Value=_hp; }
    private int _balance = CardShop.StartingCoins;
    public void SetCoins(int coins) { _balance = coins; UpdateBalance(); }
    private PlayerAvatar _portrait = null!;
    private string _userId = "", _resolvedName = "";
    public void SetNakamaName(string? displayName,string? username)
    {
        _resolvedName=string.IsNullOrWhiteSpace(displayName)?(username??""):displayName;
        _name.AutoTranslateMode=AutoTranslateModeEnum.Disabled;_name.Text=_resolvedName;
    }
    private int _generation;
    private bool _exiting;
    private int? _mmr;
    public void SetMmr(int rating) { _mmr=rating;_rank.Text=$"MMR: {rating}";_rank.Visible=!MobileLayout.Enabled; }

    public override void _Ready()
    {
        _name = GetNode<Label>("PlayerName"); _uid = GetNode<Label>("Uid");
        _rank = GetNode<Label>("Rank"); _score = GetNode<Label>("Score");
        _coins = GetNode<Label>("Coins"); SetCoins(_balance);
        var previous=GetNode<TextureRect>("PortraitFrame/Portrait");
        _portrait=new PlayerAvatar {Name="Portrait",Position=previous.Position,Size=previous.Size};
        var parent=previous.GetParent();parent.RemoveChild(previous);previous.Free();parent.AddChild(_portrait);
        MenuUi.StyleSurface(this);
        PatchMarginLeft=PatchMarginRight=PatchMarginTop=PatchMarginBottom=10;
        TextureFilter=TextureFilterEnum.Nearest;SelfModulate=Colors.White;
        var portraitFrame=GetNode<NinePatchRect>("PortraitFrame");
        MenuUi.StyleSurface(portraitFrame);
        portraitFrame.PatchMarginLeft=portraitFrame.PatchMarginRight=portraitFrame.PatchMarginTop=portraitFrame.PatchMarginBottom=10;
        portraitFrame.SelfModulate=Colors.White;
        GameUi.Label(_name,15);GameUi.Label(_rank,12,true);GameUi.Label(_score,12,true);GameUi.Label(_coins,14);
        _coins.AddThemeColorOverride("font_color",GameUi.Gold);
        _uid.Hide();_score.Hide();_coins.Position=new(94,68);
        _health=new PixelHealthBar {Position=new(94,106),Size=new(154,8),BarHeight=8,MaxValue=30,Value=_hp,ShowPercentage=false,MouseFilter=MouseFilterEnum.Ignore};
        AddChild(_health);
        _cardBack=CardBackCatalog.Preview(CardBackId,44,66);_cardBack.Name="EquippedCardBack";
        _cardBack.Position=new(216,12);_cardBack.Size=new(44,66);AddChild(_cardBack);
        foreach(var label in new[]{_name,_rank,_score}) { label.Size=new(114,label.Size.Y);label.ClipText=true; }
        SetCardBack(Team=="A" ? DeckStore.Selected?.CardBackId : CardBackId);
        if(MobileLayout.Enabled)CompactMobile();
        ShowMock();
    }
    public void CompactMobile()
    {
        CustomMinimumSize=new(180,76);OffsetTop=-84;OffsetBottom=-8;
        GetNode<Control>("PortraitFrame").Hide();_rank.Hide();_score.Hide();
        _name.Position=new(8,6);_name.Size=new(124,24);_name.ClipText=true;
        _coins.Position=new(8,32);_coins.Size=new(124,24);
        if(_cardBack!=null){_cardBack.CustomMinimumSize=new(30,46);_cardBack.Position=new(142,6);_cardBack.Size=new(30,46);}
        if(_health!=null){_health.Position=new(8,62);_health.Size=new(164,6);}
    }
    private void ShowMock()
    {
        string cached=(_userId==GameAccount.Session?.UserId || (string.IsNullOrEmpty(_userId)&&Team=="A"))?GameAccount.DisplayName:"";
        _name.Text=!string.IsNullOrWhiteSpace(_resolvedName)?_resolvedName:!string.IsNullOrWhiteSpace(cached)?cached:"…";
        
        _uid.Text = "UID: " + (string.IsNullOrEmpty(_userId) ? "Local arena" : _userId);
        
        _rank.Text = _mmr.HasValue ? $"MMR: {_mmr.Value}" : "";
        _rank.Visible=_mmr.HasValue&&!MobileLayout.Enabled;
    }
    public void SetPlayer(string userId, NakamaConnection? connection, string leaderboardId, bool refresh = false)
    {
        if (_exiting || (!refresh && userId == _userId)) return;
        if (userId!=_userId) { _mmr=null;_resolvedName=""; }
        _userId = userId; int generation = ++_generation;
        ShowMock();
        _portrait.Bind(userId);
        if (userId.StartsWith("bot:") || userId.StartsWith("ghost:"))
        {
            SetNakamaName(userId.StartsWith("ghost:")?"ELIMINATED PLAYER":"BOT",null);
            if(!_mmr.HasValue) SetMmr(1000);return;
        }
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
                SetNakamaName(user.DisplayName,user.Username);
                AvatarProfiles.Update(user);
            }
        }
        catch (Exception exception) { if (Current(generation)) GD.PushWarning("Player profile lookup: " + exception.Message); }
    }
    public override void _ExitTree() { _exiting = true; _generation++; }
}