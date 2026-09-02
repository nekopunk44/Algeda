namespace API.Background
{
    public sealed class AutoMatchingWorkerState
    {
        private readonly object _sync = new();
        private DateTime _workerStartedUtc = DateTime.UtcNow;
        private long _iteration;
        private bool _isEnabled;
        private bool _isRunningIteration;
        private DateTime? _lastIterationStartedUtc;
        private DateTime? _lastIterationCompletedUtc;
        private DateTime? _lastErrorUtc;
        private string? _lastError;
        private int _lastScannedPropertiesCount;
        private int _lastChangedPropertiesCount;
        private int _lastEmailsSentCount;
        private int _trackedPropertiesCount;
        private int _pollIntervalSeconds;
        private int _batchSize;

        public void MarkWorkerStarted()
        {
            lock (_sync)
            {
                _workerStartedUtc = DateTime.UtcNow;
            }
        }

        public void UpdateConfiguration(bool enabled, int pollIntervalSeconds, int batchSize)
        {
            lock (_sync)
            {
                _isEnabled = enabled;
                _pollIntervalSeconds = pollIntervalSeconds;
                _batchSize = batchSize;
            }
        }

        public long MarkIterationStarted(DateTime startedUtc)
        {
            lock (_sync)
            {
                _iteration++;
                _isRunningIteration = true;
                _lastIterationStartedUtc = startedUtc;
                return _iteration;
            }
        }

        public void MarkIterationCompleted(
            DateTime completedUtc,
            int scannedPropertiesCount,
            int changedPropertiesCount,
            int emailsSentCount,
            int trackedPropertiesCount)
        {
            lock (_sync)
            {
                _isRunningIteration = false;
                _lastIterationCompletedUtc = completedUtc;
                _lastScannedPropertiesCount = scannedPropertiesCount;
                _lastChangedPropertiesCount = changedPropertiesCount;
                _lastEmailsSentCount = emailsSentCount;
                _trackedPropertiesCount = trackedPropertiesCount;
            }
        }

        public void MarkIterationFailed(DateTime failedUtc, string error)
        {
            lock (_sync)
            {
                _isRunningIteration = false;
                _lastErrorUtc = failedUtc;
                _lastError = error;
            }
        }

        public AutoMatchingWorkerSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return new AutoMatchingWorkerSnapshot(
                    _workerStartedUtc,
                    _iteration,
                    _isEnabled,
                    _isRunningIteration,
                    _lastIterationStartedUtc,
                    _lastIterationCompletedUtc,
                    _lastErrorUtc,
                    _lastError,
                    _lastScannedPropertiesCount,
                    _lastChangedPropertiesCount,
                    _lastEmailsSentCount,
                    _trackedPropertiesCount,
                    _pollIntervalSeconds,
                    _batchSize);
            }
        }
    }

    public sealed record AutoMatchingWorkerSnapshot(
        DateTime WorkerStartedUtc,
        long Iteration,
        bool Enabled,
        bool IsRunningIteration,
        DateTime? LastIterationStartedUtc,
        DateTime? LastIterationCompletedUtc,
        DateTime? LastErrorUtc,
        string? LastError,
        int LastScannedPropertiesCount,
        int LastChangedPropertiesCount,
        int LastEmailsSentCount,
        int TrackedPropertiesCount,
        int PollIntervalSeconds,
        int BatchSize);
}
