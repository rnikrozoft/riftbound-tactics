using Nakama;
using System;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

// Transport is independent of Godot. Socket events must be marshalled to the scene thread.
public sealed class NakamaConnection
{
    public const string MmrLeaderboardId = "riftbound_mmr";
    public const string WinsLeaderboardId = "riftbound_match_wins";
    public const string DebugServerKey = "riftbound-debug-key";
    public Client Client { get; }
    public ISocket Socket { get; private set; } = null!;
    public ISession Session { get; private set; } = null!;
    public string MatchId { get; private set; } = "";
    public string Code { get; private set; } = "";
    public string DeviceId { get; }
    public long ClockOffsetMs { get; private set; }
    public long ServerNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ClockOffsetMs;
    public event Action<OnlineState>? StateReceived;
    public event Action<OnlineError>? ErrorReceived;
    public event Action? Disconnected;
    public NakamaConnection(string host = "127.0.0.1", int port = 7350, string scheme = "http", string key = DebugServerKey, string? deviceId = null, ISession? session = null)
    {
        Session=session!;
        DeviceId=deviceId??("rift-debug-"+Guid.NewGuid().ToString("N"));
        Client = new Client(scheme,host,port,key) { Timeout = 8 };
    }
    public async Task Connect(bool realtime = true)
    {
        // Lobby supplies a persisted device ID; independent debug/test clients stay isolated.
        if(Session==null)Session = await Client.AuthenticateDeviceAsync(DeviceId,create:true);
        else await Client.GetAccountAsync(Session); // SDK refreshes tokens before expiry; invalidated sessions must fail.
        if(DeviceId.StartsWith("rift-player-",StringComparison.Ordinal))GameAccount.Session=Session;
        if(realtime)await ConnectSocket();
    }
    private async Task ConnectSocket()
    {
        if(_closed)throw new ObjectDisposedException(nameof(NakamaConnection));
        Socket = Nakama.Socket.From(Client);
        Socket.ReceivedMatchState += OnMatchState;
        var socket=Socket;
        Socket.Closed += () => {if(ReferenceEquals(socket,Socket)&&!_closed)Disconnected?.Invoke();};
        try {
        await socket.ConnectAsync(Session,connectTimeout:8);
        if(_closed)throw new ObjectDisposedException(nameof(NakamaConnection));
        long bestRtt = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await socket.RpcAsync("server_clock","{}");
            long end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if(_closed)throw new ObjectDisposedException(nameof(NakamaConnection));
            var clock = JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.ServerClock)!;
            if (end - start < bestRtt) { bestRtt = end - start; ClockOffsetMs = clock.ServerMs - (start + end) / 2; }
        }
        } catch {
            socket.ReceivedMatchState-=OnMatchState;
            if(ReferenceEquals(socket,Socket))Socket=null!;
            try {await socket.CloseAsync();}catch{}
            throw;
        }
    }
    private long _actionSequence;
    public long NextActionSequence()=>Interlocked.Increment(ref _actionSequence);
    private void ObserveSequence(long value)
    {
        long current;
        do { current=Interlocked.Read(ref _actionSequence);if(value<=current)return; }
        while(Interlocked.CompareExchange(ref _actionSequence,value,current)!=current);
    }
    // Never authenticate again here: an older client must not steal the newest login.
    public async Task Reconnect()
    {
        await Client.GetAccountAsync(Session);
        if(Socket!=null){Socket.ReceivedMatchState-=OnMatchState;var old=Socket;Socket=null!;try{await old.CloseAsync();}catch{}}
        await ConnectSocket();
    }
    public Task Rejoin(DeckDefinition? deck=null)=>Socket.JoinMatchAsync(MatchId,metadata:deck==null?null:new Dictionary<string,string>{["deck"]=JsonSerializer.Serialize(deck,GameJsonContext.Default.DeckDefinition)});
    private void OnMatchState(IMatchState state)
    {
        if(state.MatchId!=MatchId)return;
        try
        {
            if (state.OpCode == 2)
            {
                var snapshot = JsonSerializer.Deserialize(state.State,GameJsonContext.Default.OnlineState);
                if (snapshot != null)
                {
                    ObserveSequence(snapshot.AckSequence);
                    if (snapshot.Roster.Length>0 && (snapshot.Phase=="eliminated" || snapshot.Phase=="game_over"))
                        foreach(var player in snapshot.Roster)
                            if(player.UserId==Session.UserId && player.Hp==0) {MatchId="";break;}
                    StateReceived?.Invoke(snapshot);
                }
            }
            else if (state.OpCode == 3)
            {
                var error = JsonSerializer.Deserialize(state.State,GameJsonContext.Default.OnlineError);
                if (error != null) ErrorReceived?.Invoke(error);
            }
        }
        catch (JsonException exception) { ErrorReceived?.Invoke(new() { Message = "Invalid server message: " + exception.Message }); }
    }
    public async Task<RoomResponse> EnterRoom(bool create, string code = "", DeckDefinition? deck = null)
    {
        deck ??= DeckDefinition.Starter();
        string payload = JsonSerializer.Serialize(new RoomRequest { Code = code, Deck = deck },GameJsonContext.Default.RoomRequest);
        var response = await Socket.RpcAsync(create ? "room_create" : "room_join",payload);
        var room = JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.RoomResponse)!;
        // Set before joining because the first snapshot may arrive before JoinMatchAsync returns.
        MatchId = room.MatchId; Code = room.Code;
        await Socket.JoinMatchAsync(MatchId, metadata: new Dictionary<string,string> { ["deck"] = JsonSerializer.Serialize(deck,GameJsonContext.Default.DeckDefinition) });
        return room;
    }
    public Task Send(OnlineAction action) => Socket.SendMatchStateAsync(MatchId,1,
        JsonSerializer.Serialize(action,GameJsonContext.Default.OnlineAction));
    private readonly CancellationTokenSource _searchCancellation = new();
    private string _ticket = "";
    private bool _searchActive;
    public async Task<RoomResponse> FindMatch(DeckDefinition deck, Action<int>? progress = null)
    {
        if (_searchActive) throw new InvalidOperationException("Already searching");
        _searchActive = true;
        try
        {
            await Socket.RpcAsync("queue_begin", JsonSerializer.Serialize(new RoomRequest { Deck = deck },GameJsonContext.Default.RoomRequest));
            var ticket = await Socket.AddMatchmakerAsync("*",2,6);
            _ticket = ticket.Ticket;
            long started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); bool widened = false;
            while (true)
            {
                _searchCancellation.Token.ThrowIfCancellationRequested();
                var response = await Socket.RpcAsync("queue_status","{}");
                var room = JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.RoomResponse)!;
                if (!string.IsNullOrEmpty(room.MatchId))
                {
                    await RemoveTicket();
                    _searchCancellation.Token.ThrowIfCancellationRequested();
                    MatchId=room.MatchId;Code=room.Code;
                    await Socket.JoinMatchAsync(MatchId);
                    return room;
                }
                int elapsed=(int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-started)/1000);
                progress?.Invoke(elapsed);
                if (!widened && elapsed>=10)
                {
                    await RemoveTicket();
                    widened=true;
                    try { ticket=await Socket.AddMatchmakerAsync("*",2,6);_ticket=ticket.Ticket; }
                    catch
                    {
                        // A match can be reserved between the status poll and ticket renewal.
                        var assigned=await Socket.RpcAsync("queue_status","{}");
                        var reserved=JsonSerializer.Deserialize(assigned.Payload,GameJsonContext.Default.RoomResponse)!;
                        if (string.IsNullOrEmpty(reserved.MatchId)) throw;
                    }
                }
                await Task.Delay(750,_searchCancellation.Token);
            }
        }
        finally
        {
            await RemoveTicket();
            try { await Socket.RpcAsync("queue_cancel","{}"); } catch { }
            _searchActive=false;
        }
    }
    private async Task RemoveTicket()
    {
        string ticket=_ticket;_ticket="";
        if (ticket!="") try { await Socket.RemoveMatchmakerAsync(ticket); } catch { /* Already matched/removed. */ }
    }
    private bool _closed;
    public async Task Close()
    {
        if(_closed)return;
        _searchCancellation.Cancel();
        _closed=true;
        if (Socket == null) {MatchId="";return;}
        await RemoveTicket();
        if (_searchActive) try { await Socket.RpcAsync("queue_cancel","{}"); } catch { }
        string match=MatchId;MatchId="";
        if(match!="")try{await Socket.LeaveMatchAsync(match);}catch{}
        Socket.ReceivedMatchState -= OnMatchState;
        try { await Socket.CloseAsync(); } catch { /* Shutdown must not interrupt scene teardown. */ }
    }
}


