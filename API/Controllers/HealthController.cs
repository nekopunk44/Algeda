using API.Background;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly AutoMatchingWorkerState _workerState;
        private readonly IOptionsMonitor<AutoMatchingWorkerOptions> _workerOptionsMonitor;

        public HealthController(
            AppDbContext dbContext,
            AutoMatchingWorkerState workerState,
            IOptionsMonitor<AutoMatchingWorkerOptions> workerOptionsMonitor)
        {
            _dbContext = dbContext;
            _workerState = workerState;
            _workerOptionsMonitor = workerOptionsMonitor;
        }

        [HttpGet("db")]
        public async Task<IActionResult> CheckDatabase(CancellationToken cancellationToken)
        {
            try
            {
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

                if (!canConnect)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        status = "unavailable",
                        database = "postgresql",
                        timestampUtc = DateTime.UtcNow
                    });
                }

                var pendingMigrations = await _dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken);

                return Ok(new
                {
                    status = "ok",
                    database = "postgresql",
                    pendingMigrationsCount = pendingMigrations.Count(),
                    timestampUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "unavailable",
                    database = "postgresql",
                    error = ex.Message,
                    timestampUtc = DateTime.UtcNow
                });
            }
        }

        [HttpGet("auto-matching")]
        public IActionResult CheckAutoMatchingWorker()
        {
            var snapshot = _workerState.GetSnapshot();
            var options = _workerOptionsMonitor.CurrentValue;

            var pollInterval = Math.Max(1, options.PollIntervalSeconds);
            var staleAfterSeconds = Math.Max(30, pollInterval * 3);

            var nowUtc = DateTime.UtcNow;
            var lastHeartbeatUtc = snapshot.LastIterationCompletedUtc
                                   ?? snapshot.LastIterationStartedUtc
                                   ?? snapshot.WorkerStartedUtc;

            var alive = !options.Enabled
                        || nowUtc - lastHeartbeatUtc <= TimeSpan.FromSeconds(staleAfterSeconds);

            var status = !options.Enabled
                ? "disabled"
                : alive
                    ? "ok"
                    : "degraded";

            var payload = new
            {
                status,
                worker = "auto-matching",
                enabled = options.Enabled,
                alive,
                staleAfterSeconds,
                snapshot.WorkerStartedUtc,
                snapshot.Iteration,
                snapshot.IsRunningIteration,
                snapshot.LastIterationStartedUtc,
                snapshot.LastIterationCompletedUtc,
                snapshot.LastErrorUtc,
                snapshot.LastError,
                snapshot.LastScannedPropertiesCount,
                snapshot.LastChangedPropertiesCount,
                snapshot.LastEmailsSentCount,
                snapshot.TrackedPropertiesCount,
                snapshot.PollIntervalSeconds,
                snapshot.BatchSize,
                timestampUtc = nowUtc
            };

            return status == "degraded"
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, payload)
                : Ok(payload);
        }
    }
}
