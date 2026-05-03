using HostSimTester.App.Models;
using Microsoft.Extensions.Options;
using NLog;
using Secs4Net;
using System.Linq;

namespace HostSimTester.App.Secs;

public sealed class SecsConnection : IAsyncDisposable
{
    private readonly Logger _uiLogger = LogManager.GetLogger(Logging.LoggerNames.Ui);
    private readonly SecsGemNLogAdapter _secsLogger;
    private readonly SecsTemplateRegistry _templateRegistry;
    private readonly List<PrimaryWaiter> _waiters = new();
    private readonly object _waitersLock = new();

    private HsmsConnection? _connection;
    private SecsGem? _secsGem;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;
    private ConnectionSettings? _activeSettings;
    private readonly SemaphoreSlim _commSemaphore = new(1, 1);

    public event Action<string>? ConnectionStateChanged;
    public event Action<PrimaryMessageWrapper>? PrimaryMessageReceived;

    public bool IsConnected => _connection?.State == ConnectionState.Connected || _connection?.State == ConnectionState.Selected;
    public DateTime? LastS1F13ReceivedAt { get; private set; }

    public SecsConnection()
    {
        _secsLogger = new SecsGemNLogAdapter(_uiLogger);
        _templateRegistry = SecsTemplateRegistry.LoadFromToolIdXmlOrEmpty(_uiLogger);
    }

    public async Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            var sameEndpoint =
                _activeSettings is not null &&
                string.Equals(_activeSettings.IpAddress, settings.IpAddress, StringComparison.OrdinalIgnoreCase) &&
                _activeSettings.Port == settings.Port &&
                _activeSettings.DeviceId == settings.DeviceId;

            if (sameEndpoint && IsConnected)
            {
                _uiLogger.Info($"Connect ignored: already connected to ip={settings.IpAddress} port={settings.Port} deviceId={settings.DeviceId}");
                return;
            }

            _uiLogger.Info($"Reconnect with latest settings ip={settings.IpAddress} port={settings.Port} deviceId={settings.DeviceId}");
            await DisconnectAsync().ConfigureAwait(false);
        }

        var options = Options.Create(new SecsGemOptions
        {
            DeviceId = settings.DeviceId,
            IsActive = true,
            IpAddress = settings.IpAddress,
            Port = settings.Port,
            T3 = 45000,
            T5 = 10000,
            T6 = 5000,
            T7 = 10000,
            T8 = 5000,
            LinkTestInterval = 30000
        });

        _connection = new HsmsConnection(options, _secsLogger);
        _connection.ConnectionChanged += OnConnectionChanged;

        _secsGem = new SecsGem(options, _connection, _secsLogger);

        _listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connection.Start(_listenCts.Token);
        _listenTask = Task.Run(() => ReceiveLoopAsync(_listenCts.Token), _listenCts.Token);
        _activeSettings = new ConnectionSettings
        {
            DeviceId = settings.DeviceId,
            IpAddress = settings.IpAddress,
            Port = settings.Port
        };

        _uiLogger.Info($"Start connect ip={settings.IpAddress} port={settings.Port} deviceId={settings.DeviceId}");
    }

    public async Task<SecsMessage?> SendAsync(string operationName, byte stream, byte function, Item? payload = null, bool expectReply = true, CancellationToken cancellationToken = default)
    {
        if (_secsGem is null)
        {
            throw new InvalidOperationException("SECS is not connected.");
        }

        var primary = new SecsMessage(stream, function, expectReply)
        {
            Name = operationName
        };
        primary.SecsItem = payload;

        _uiLogger.Info($"Start send primary S{stream}F{function} Operation=[{operationName}]");
        _uiLogger.Info("Send primary body S{stream}F{function} Operation=[{operation}]:{newline}{body}",
            stream,
            function,
            operationName,
            Environment.NewLine,
            SecsItemFormatter.Format(primary.SecsItem));

        if (!expectReply)
        {
            await _secsGem.SendAsync(primary, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var reply = await _secsGem.SendAsync(primary, cancellationToken).ConfigureAwait(false);
        if (reply is not null)
        {
            _uiLogger.Info("Received secondary body S{stream}F{function} ReplyTo=[{operation}]:{newline}{body}",
                reply.S,
                reply.F,
                operationName,
                Environment.NewLine,
                SecsItemFormatter.Format(reply.SecsItem));
        }
        return reply;
    }

    public async Task<bool> EstablishCommunicationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SECS is not connected (Selected/Connected required).");
        }

        await _commSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var reply = await SendAsync(
                    "HostInitiated_S1F13_EstablishCommunication",
                    1,
                    13,
                    SecsMessageFactory.S1F13EstablishCommunicationRequest(),
                    expectReply: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (reply is null)
            {
                _uiLogger.Warn("Host initiated S1F13 returned no secondary reply.");
                return false;
            }

            if (reply.S != 1 || reply.F != 14)
            {
                _uiLogger.Warn($"Host initiated S1F13 got unexpected reply S{reply.S}F{reply.F}.");
                return false;
            }

            var lines = SecsReplyInterpreter.Describe(reply);
            var success = !lines.Any(l => l.Contains("NAK/ERROR", StringComparison.OrdinalIgnoreCase));
            _uiLogger.Info(success
                ? "Host initiated S1F13/S1F14 communication established."
                : "Host initiated S1F13 got S1F14 NAK/ERROR.");

            return success;
        }
        finally
        {
            _commSemaphore.Release();
        }
    }

    public async Task<PrimaryMessageWrapper> WaitForPrimaryAsync(byte stream, byte function, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<PrimaryMessageWrapper>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiter = new PrimaryWaiter(stream, function, tcs);

        lock (_waitersLock)
        {
            _waiters.Add(waiter);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_waitersLock)
            {
                _waiters.Remove(waiter);
            }
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
        {
            return;
        }

        _listenCts?.Cancel();

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during shutdown.
            }
        }

        _connection.ConnectionChanged -= OnConnectionChanged;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _secsGem?.Dispose();

        _listenTask = null;
        _listenCts?.Dispose();
        _listenCts = null;
        _secsGem = null;
        _connection = null;
        _activeSettings = null;

        _uiLogger.Info("SECS disconnect complete");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_secsGem is null)
        {
            return;
        }

        await foreach (var primary in _secsGem.GetPrimaryMessageAsync(cancellationToken).ConfigureAwait(false))
        {
            _uiLogger.Info("Received primary detail S{stream}F{function} Name=[{name}]:{newline}{body}",
                primary.PrimaryMessage.S,
                primary.PrimaryMessage.F,
                primary.PrimaryMessage.Name,
                Environment.NewLine,
                SecsItemFormatter.Format(primary.PrimaryMessage.SecsItem));
            if (primary.PrimaryMessage.S == 6 && primary.PrimaryMessage.F == 11)
            {
                LogS6F11Summary(primary.PrimaryMessage);
            }

            HandleWaiters(primary);
            await TryAutoReplyAsync(primary, cancellationToken).ConfigureAwait(false);
            PrimaryMessageReceived?.Invoke(primary);
        }
    }

    private void LogS6F11Summary(SecsMessage message)
    {
        var hasDataId = SecsPayload.TryGetS6F11DataId(message, out var dataId);
        var hasCeid = SecsPayload.TryGetS6F11Ceid(message, out var ceid);
        var rptids = SecsPayload.GetS6F11Rptids(message);

        if (!hasDataId || !hasCeid)
        {
            _uiLogger.Warn("S6F11 parsed summary: unable to parse DATAID/CEID.");
            return;
        }

        _uiLogger.Info("S6F11 parsed summary: DATAID={dataId}, CEID={ceid}, EventName={eventName}, RPTIDs=[{rptids}]",
            dataId,
            ceid,
            SecsPayload.GetEventName(ceid),
            string.Join(",", rptids));
    }

    private void HandleWaiters(PrimaryMessageWrapper primary)
    {
        var hit = default(PrimaryWaiter);

        lock (_waitersLock)
        {
            hit = _waiters.FirstOrDefault(w =>
                w.Stream == primary.PrimaryMessage.S &&
                w.Function == primary.PrimaryMessage.F);
        }

        hit?.Completion.TrySetResult(primary);
    }

    private async Task TryAutoReplyAsync(PrimaryMessageWrapper primary, CancellationToken cancellationToken)
    {
        if (!primary.PrimaryMessage.ReplyExpected)
        {
            return;
        }

        if (primary.PrimaryMessage.S == 1 && primary.PrimaryMessage.F == 13)
        {
            LastS1F13ReceivedAt = DateTime.Now;
            var s1f14 = new SecsMessage(1, 14, false)
            {
                Name = "EstablishCommunicationsRequestAck_Host_Ack",
                SecsItem = SecsMessageFactory.S1F14EstablishCommunicationAck()
            };

            var replied = await primary.TryReplyAsync(s1f14, cancellationToken).ConfigureAwait(false);
            if (replied)
            {
                _uiLogger.Info("Auto reply S1F14 ACK for S1F13 with standard payload");
            }

            return;
        }

        if (primary.PrimaryMessage.S == 6 && primary.PrimaryMessage.F == 11)
        {
            var ack = new SecsMessage(6, 12, false)
            {
                Name = "EventReportAck_Ack",
                SecsItem = Item.B([0x00])
            };

            var replied = await primary.TryReplyAsync(ack, cancellationToken).ConfigureAwait(false);
            if (replied)
            {
                _uiLogger.Info("Auto reply S6F12 ACK for S6F11");
            }

            return;
        }

        if (_templateRegistry.TryGetBySxFy(primary.PrimaryMessage.S, primary.PrimaryMessage.F, out var candidates))
        {
            var source = candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.AutoReply));
            if (source is null)
            {
                return;
            }

            if (!_templateRegistry.TryGetByName(source.AutoReply, out var autoTemplate))
            {
                return;
            }

            var autoReply = new SecsMessage(autoTemplate.Stream, autoTemplate.Function, false)
            {
                Name = autoTemplate.Name,
                SecsItem = Item.L()
            };

            var replied = await primary.TryReplyAsync(autoReply, cancellationToken).ConfigureAwait(false);
            if (replied)
            {
                _uiLogger.Info($"Auto reply from TOOLID template: {autoTemplate.Name}");
            }
        }
    }

    private void OnConnectionChanged(object? sender, ConnectionState state)
    {
        ConnectionStateChanged?.Invoke(state.ToString());
        _uiLogger.Info($"Connection state changed: {state}");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private sealed class PrimaryWaiter(byte stream, byte function, TaskCompletionSource<PrimaryMessageWrapper> completion)
    {
        public byte Stream { get; } = stream;
        public byte Function { get; } = function;
        public TaskCompletionSource<PrimaryMessageWrapper> Completion { get; } = completion;
    }
}
