using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace API.Background
{
    public sealed class AutoMatchingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<AutoMatchingWorkerOptions> _optionsMonitor;
        private readonly AutoMatchingWorkerState _state;
        private readonly ILogger<AutoMatchingBackgroundService> _logger;

        private readonly Dictionary<Guid, TrackedPropertyState> _trackedProperties = [];
        private bool _hasTrackedSnapshot;

        public AutoMatchingBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<AutoMatchingWorkerOptions> optionsMonitor,
            AutoMatchingWorkerState state,
            ILogger<AutoMatchingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _optionsMonitor = optionsMonitor;
            _state = state;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _state.MarkWorkerStarted();
            _logger.LogInformation("Воркер автоподбора запущен.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = Normalize(_optionsMonitor.CurrentValue);
                _state.UpdateConfiguration(options.Enabled, options.PollIntervalSeconds, options.BatchSize);

                if (!options.Enabled)
                {
                    await DelaySafely(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                var iterationStartedUtc = DateTime.UtcNow;
                var iteration = _state.MarkIterationStarted(iterationStartedUtc);

                _logger.LogInformation(
                    "Итерация автоподбора запущена. Iteration={Iteration}; BatchSize={BatchSize}; RequirementsLimit={RequirementsLimit}",
                    iteration,
                    options.BatchSize,
                    options.RequirementsLimit);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var propertyRepository = scope.ServiceProvider.GetRequiredService<IPropertyRepository>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<PropertyMatchingNotificationService>();

                    var availableProperties = await propertyRepository.GetAvailable(options.BatchSize, 0);

                    var changedProperties = _hasTrackedSnapshot
                        ? DetectChangedProperties(availableProperties, options.TrackedPropertiesTtlMinutes)
                        : InitializeTrackedSnapshot(availableProperties);

                    var emailsSent = 0;
                    foreach (var property in changedProperties)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        emailsSent += await notificationService.NotifySubscribedClientsForProperty(
                            property,
                            options.RequirementsLimit);
                    }

                    _state.MarkIterationCompleted(
                        DateTime.UtcNow,
                        availableProperties.Count,
                        changedProperties.Count,
                        emailsSent,
                        _trackedProperties.Count);

                    _logger.LogInformation(
                        "Итерация автоподбора завершена. Iteration={Iteration}; Scanned={Scanned}; Changed={Changed}; EmailsSent={EmailsSent}; Tracked={Tracked}",
                        iteration,
                        availableProperties.Count,
                        changedProperties.Count,
                        emailsSent,
                        _trackedProperties.Count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    _state.MarkIterationFailed(DateTime.UtcNow, ex.Message);
                    _logger.LogInformation(
                        ex,
                        "Скоуп воркера автоподбора был освобожден. Фоновый цикл остановлен.");
                    break;
                }
                catch (Exception ex)
                {
                    _state.MarkIterationFailed(DateTime.UtcNow, ex.Message);
                    _logger.LogError(
                        ex,
                        "Ошибка в итерации автоподбора. Iteration={Iteration}",
                        iteration);
                }

                await DelaySafely(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Воркер автоподбора остановлен.");
        }

        private List<Property> InitializeTrackedSnapshot(IReadOnlyCollection<Property> properties)
        {
            var now = DateTime.UtcNow;

            foreach (var property in properties)
            {
                _trackedProperties[property.Id] = new TrackedPropertyState(BuildFingerprint(property), now);
            }

            _hasTrackedSnapshot = true;
            return [];
        }

        private List<Property> DetectChangedProperties(IReadOnlyCollection<Property> properties, int ttlMinutes)
        {
            var changed = new List<Property>();
            var now = DateTime.UtcNow;

            foreach (var property in properties)
            {
                var fingerprint = BuildFingerprint(property);

                if (!_trackedProperties.TryGetValue(property.Id, out var tracked))
                {
                    _trackedProperties[property.Id] = new TrackedPropertyState(fingerprint, now);
                    changed.Add(property);
                    continue;
                }

                if (!string.Equals(tracked.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    _trackedProperties[property.Id] = new TrackedPropertyState(fingerprint, now);
                    changed.Add(property);
                    continue;
                }

                tracked.LastSeenUtc = now;
            }

            var staleThreshold = now.AddMinutes(-Math.Max(1, ttlMinutes));
            var staleIds = _trackedProperties
                .Where(x => x.Value.LastSeenUtc < staleThreshold)
                .Select(x => x.Key)
                .ToList();

            foreach (var staleId in staleIds)
            {
                _trackedProperties.Remove(staleId);
            }

            return changed;
        }

        private static string BuildFingerprint(Property property)
        {
            var criteriaFingerprint = string.Join(
                ";",
                property.CriterionValues
                    .OrderBy(x => x.CriterionDefinitionId)
                    .Select(x => $"{x.CriterionDefinitionId:N}:{x.Value}"));

            return string.Join(
                "|",
                property.Status,
                property.Price,
                property.Area,
                property.RoomsCount,
                property.Address,
                criteriaFingerprint);
        }

        private static AutoMatchingWorkerOptions Normalize(AutoMatchingWorkerOptions options)
        {
            return new AutoMatchingWorkerOptions
            {
                Enabled = options.Enabled,
                PollIntervalSeconds = Math.Max(1, options.PollIntervalSeconds),
                BatchSize = Math.Max(1, options.BatchSize),
                RequirementsLimit = Math.Max(1, options.RequirementsLimit),
                TrackedPropertiesTtlMinutes = Math.Max(1, options.TrackedPropertiesTtlMinutes)
            };
        }

        private static async Task DelaySafely(TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Штатное завершение фонового цикла.
            }
        }

        private sealed class TrackedPropertyState
        {
            public TrackedPropertyState(string fingerprint, DateTime lastSeenUtc)
            {
                Fingerprint = fingerprint;
                LastSeenUtc = lastSeenUtc;
            }

            public string Fingerprint { get; set; }
            public DateTime LastSeenUtc { get; set; }
        }
    }
}
