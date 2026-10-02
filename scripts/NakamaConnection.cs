using Nakama;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Transport is independent of Godot. Socket events must be marshalled to the scene thread.
public sealed class NakamaConnection
{
    public const string DebugServerKey = "riftbound-debug-key";
    public Client Client { get; }
    public ISocket Socket { get; private set; } = null!;
    public ISession Session { get; private set; } = null!;
    public string MatchId { get; private set; } = "";
    public string Code { get; private set; } = "";
    public string DeviceId { get; } = "rift-debug-" + Guid.NewGuid().ToString("N");
    public long ClockOffsetMs { get; private set; }
    public long ServerNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ClockOffsetMs;
    public event Action<OnlineState>? StateReceived;
    public event Action<OnlineError>? ErrorReceived;
    public event Action? Disconnected;
    public NakamaConnection(string host = "127.0.0.1", int port = 7350, string scheme = "http", string key = DebugServerKey)
    {
        Client = new Client(scheme,host,port,key) { Timeout = 8 };
    }
    public async Task Connect()
    {
        // No persisted device/session: every game process authenticates as a new Nakama user.
        Session = await Client.AuthenticateDeviceAsync(DeviceId,create:true);
        Socket = Nakama.Socket.From(Client);
        Socket.ReceivedMatchState += OnMatchState;
        Socket.Closed += () => Disconnected?.Invoke();
        await Socket.ConnectAsync(Session,connectTimeout:8);
        long bestRtt = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await Socket.RpcAsync("server_clock","{}");
            long end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clock = JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.ServerClock)!;
            if (end - start < bestRtt) { bestRtt = end - start; ClockOffsetMs = clock.ServerMs - (start + end) / 2; }
        }
    }
    private void OnMatchState(IMatchState state)
    {
        try
        {
            if (state.OpCode == 2)
            {
                var snapshot = JsonSerializer.Deserialize(state.State,GameJsonContext.Default.OnlineState);
                if (snapshot != null) StateReceived?.Invoke(snapshot);
            }
            else if (state.OpCode == 3)
            {
                var error = JsonSerializer.Deserialize(state.State,GameJsonContext.Default.OnlineError);
                if (error != null) ErrorReceived?.Invoke(error);
            }
        }
        catch (JsonException exception) { ErrorReceived?.Invoke(new() { Message = "Invalid server message: " + exception.Message }); }
    }
    public async Task<RoomResponse> EnterRoom(bool create, string code = "")
    {
        string payload = create ? "{}" : JsonSerializer.Serialize(new RoomRequest { Code = code },GameJsonContext.Default.RoomRequest);
        var response = await Socket.RpcAsync(create ? "room_create" : "room_join",payload);
        var room = JsonSerializer.Deserialize(response.Payload,GameJsonContext.Default.RoomResponse)!;
        // Set before joining because the first snapshot may arrive before JoinMatchAsync returns.
        MatchId = room.MatchId; Code = room.Code;
        await Socket.JoinMatchAsync(MatchId);
        return room;
    }
    public Task Send(OnlineAction action) => Socket.SendMatchStateAsync(MatchId,1,
        JsonSerializer.Serialize(action,GameJsonContext.Default.OnlineAction));
    public async Task Close()
    {
        if (Socket == null) return;
        Socket.ReceivedMatchState -= OnMatchState;
        try { await Socket.CloseAsync(); } catch { /* Shutdown must not interrupt scene teardown. */ }
    }
}


