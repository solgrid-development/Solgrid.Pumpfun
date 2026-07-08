using System.Net.WebSockets;
using Solnet.Rpc;
using Solnet.Rpc.Core.Sockets;
using Solnet.Rpc.Types;

namespace Solgrid.Pumpfun.Stream;

// logsSubscribe mentioning the pump program: every bonding-curve trade
// anywhere on chain arrives here as a decoded event, not just your wallets
public class TradeStream : IAsyncDisposable
{
    private readonly IStreamingRpcClient _ws;
    private SubscriptionState? _sub;

    public event Action<Events.TradeEvent>? Trade;
    public event Action<Events.MigrationEvent>? Migration;

    public bool Running => _sub != null;

    public TradeStream(IStreamingRpcClient ws)
    {
        _ws = ws;
    }

    public async Task<bool> StartAsync()
    {
        if (_ws.State != WebSocketState.Open)
            await _ws.ConnectAsync();

        _sub = await _ws.SubscribeLogInfoAsync(Addresses.Pump.Key, (_, value) =>
        {
            var logs = value?.Value?.Logs;
            if (logs == null)
                return;

            foreach (var line in logs)
            {
                var ev = Events.EventDecoder.DecodeLogLine(line);
                if (ev is Events.TradeEvent trade)
                    Trade?.Invoke(trade);
                else if (ev is Events.MigrationEvent migration)
                    Migration?.Invoke(migration);
            }
        }, Commitment.Confirmed);

        return _sub != null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sub != null)
        {
            try { await _ws.UnsubscribeAsync(_sub); } catch { }
            _sub = null;
        }
        GC.SuppressFinalize(this);
    }
}
