using Application.DTOs.Profile;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public sealed class UserSessionService
{
    private static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromMinutes(5);

    private readonly IUserSessionRepository _sessions;

    public UserSessionService(IUserSessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<Guid> ResolveLoginSessionId(
        Guid userId,
        string deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var fingerprint = BuildFingerprint(deviceName, ipAddress, userAgent);
        var sessions = await _sessions.GetActiveByUser(userId, nowUtc, cancellationToken);
        var existing = sessions
            .Where(x => BuildFingerprint(x.DeviceName, x.IpAddress, x.UserAgent) == fingerprint)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ThenByDescending(x => x.CreatedDate)
            .FirstOrDefault();

        return existing?.Id ?? Guid.NewGuid();
    }

    public async Task RecordLogin(
        Guid sessionId,
        Guid userId,
        string deviceName,
        string? ipAddress,
        string? userAgent,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var fingerprint = BuildFingerprint(deviceName, ipAddress, userAgent);
        var activeSessions = await _sessions.GetActiveTrackedByUser(userId, nowUtc, cancellationToken);
        var session = activeSessions.FirstOrDefault(x => x.Id == sessionId);

        if (session is null)
        {
            session = new UserSession(
                sessionId,
                userId,
                deviceName,
                ipAddress,
                userAgent,
                expiresAtUtc);

            await _sessions.Add(session, cancellationToken);
        }
        else
        {
            session.Refresh(deviceName, ipAddress, userAgent, expiresAtUtc, nowUtc);
        }

        foreach (var duplicate in activeSessions
            .Where(x => x.Id != session.Id
                && BuildFingerprint(x.DeviceName, x.IpAddress, x.UserAgent) == fingerprint))
        {
            duplicate.Revoke(nowUtc);
        }

        await _sessions.SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSessionResponse>> GetActiveSessions(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        await CompactDuplicateSessions(userId, currentSessionId, nowUtc, cancellationToken);

        var sessions = await _sessions.GetActiveByUser(userId, nowUtc, cancellationToken);

        return sessions
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ThenByDescending(x => x.CreatedDate)
            .Select(x => new UserSessionResponse(
                x.Id,
                x.DeviceName,
                x.IpAddress,
                x.CreatedDate,
                x.LastSeenAtUtc,
                x.ExpiresAtUtc,
                currentSessionId.HasValue && x.Id == currentSessionId.Value))
            .ToArray();
    }

    public async Task<bool> ValidateAndTouch(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var session = await _sessions.GetActiveById(sessionId, userId, nowUtc, cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (nowUtc - session.LastSeenAtUtc >= LastSeenWriteInterval)
        {
            session.Touch(nowUtc);
            await _sessions.SaveChanges(cancellationToken);
        }

        return true;
    }

    public async Task Revoke(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetById(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            throw new NotFoundException("Подключение не найдено.");
        }

        session.Revoke(DateTime.UtcNow);
        await _sessions.SaveChanges(cancellationToken);
    }

    private async Task CompactDuplicateSessions(
        Guid userId,
        Guid? currentSessionId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var sessions = await _sessions.GetActiveTrackedByUser(userId, nowUtc, cancellationToken);
        var hasChanges = false;

        foreach (var group in sessions.GroupBy(x => BuildFingerprint(x.DeviceName, x.IpAddress, x.UserAgent)))
        {
            var ordered = group
                .OrderByDescending(x => currentSessionId.HasValue && x.Id == currentSessionId.Value)
                .ThenByDescending(x => x.LastSeenAtUtc)
                .ThenByDescending(x => x.CreatedDate)
                .ToArray();

            foreach (var duplicate in ordered.Skip(1))
            {
                duplicate.Revoke(nowUtc);
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _sessions.SaveChanges(cancellationToken);
        }
    }

    private static string BuildFingerprint(string deviceName, string? ipAddress, string? userAgent)
    {
        var os = DetectOs(deviceName, userAgent);
        var client = DetectClient(deviceName, userAgent);
        var ip = string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown-ip"
            : ipAddress.Trim().ToLowerInvariant();

        return $"{ip}|{os}|{client}";
    }

    private static string DetectOs(string deviceName, string? userAgent)
    {
        var source = $"{deviceName} {userAgent}";

        if (source.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return "android";

        if (source.Contains("iOS", StringComparison.OrdinalIgnoreCase)
            || source.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || source.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            return "ios";

        if (source.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            return "windows";

        if (source.Contains("macOS", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
            return "macos";

        if (source.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            return "linux";

        return "unknown-os";
    }

    private static string DetectClient(string deviceName, string? userAgent)
    {
        var source = $"{deviceName} {userAgent}";

        if (source.Contains("мобильное приложение", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Dart/", StringComparison.OrdinalIgnoreCase))
            return "mobile-app";

        if (source.Contains("Edg/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            return "edge";

        if (source.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return "chrome";

        if (source.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return "firefox";

        if (source.Contains("Safari/", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Safari", StringComparison.OrdinalIgnoreCase))
            return "safari";

        if (source.Trim().Equals("Web", StringComparison.OrdinalIgnoreCase))
            return "browser";

        return "unknown-client";
    }
}
