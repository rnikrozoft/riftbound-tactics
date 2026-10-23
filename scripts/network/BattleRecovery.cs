using System.Collections.Concurrent;

// Holds the disconnected transport across scenes without leaving the reserved match.
public static class BattleRecovery
{
    public static NakamaConnection? Connection {get;private set;}
    public static DeckDefinition? Deck {get;private set;}
    public static bool Ready {get;set;}
    public static bool Pending=>Connection!=null&&!Ready;
    public static string Notice {get;set;}="";
    public static readonly ConcurrentQueue<OnlineState> Snapshots=new();
    private static void Receive(OnlineState state)=>Snapshots.Enqueue(state);
    public static void Begin(NakamaConnection connection,DeckDefinition? deck)
    {
        Connection=connection;Deck=deck?.Clone();Ready=false;
        Snapshots.Clear();connection.StateReceived+=Receive;
    }
    public static NakamaConnection? Take()
    {
        var connection=Connection;if(connection!=null)connection.StateReceived-=Receive;
        Connection=null;Ready=false;Deck=null;return connection;
    }
    public static async System.Threading.Tasks.Task Cancel()
    {
        var connection=Take();Snapshots.Clear();if(connection!=null)await connection.Close();
    }
}
