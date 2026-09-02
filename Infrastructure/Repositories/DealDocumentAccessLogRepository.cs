using Application.DTOs.DealDocuments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public sealed class DealDocumentAccessLogRepository
        : Repository<DealDocumentAccessLog>, IDealDocumentAccessLogRepository
    {
        private readonly AppDbContext _context;

        public DealDocumentAccessLogRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DealDocumentAccessLogSearchRow>> Search(
            DealDocumentAccessLogFilter filter,
            CancellationToken cancellationToken = default)
        {
            var query = _context.DealDocumentAccessLogs.AsNoTracking();

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(x => x.CreatedDate >= filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue));
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(x => x.CreatedDate < filter.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            }

            if (filter.Action.HasValue)
            {
                query = query.Where(x => x.Action == filter.Action.Value);
            }

            if (filter.DealId.HasValue)
            {
                query = query.Where(x => x.DealId == filter.DealId.Value);
            }

            var rawRows = await (
                from log in query
                join deal in _context.Deals.AsNoTracking() on log.DealId equals deal.Id
                join client in _context.Clients.AsNoTracking() on deal.ClientId equals client.Id
                join realtor in _context.Realtors.AsNoTracking() on deal.RealtorId equals realtor.Id into realtorJoin
                from realtor in realtorJoin.DefaultIfEmpty()
                select new
                {
                    Log = log,
                    Deal = deal,
                    Client = client,
                    Realtor = realtor
                })
                .OrderByDescending(x => x.Log.CreatedDate)
                .Take(2000)
                .ToListAsync(cancellationToken);

            var realtorPhones = rawRows
                .Select(x => x.Realtor?.PhoneNumber)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var realtorEmails = realtorPhones.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : await _context.RealtorRegistrationRequests
                    .AsNoTracking()
                    .Where(x => x.Status == RealtorRegistrationRequestStatus.Approved)
                    .Where(x => realtorPhones.Contains(x.PhoneNumber))
                    .GroupBy(x => x.PhoneNumber)
                    .Select(x => new
                    {
                        Phone = x.Key,
                        Email = x
                            .OrderByDescending(item => item.ReviewedAt ?? item.CreatedDate)
                            .Select(item => item.Email)
                            .FirstOrDefault()
                    })
                    .ToDictionaryAsync(x => x.Phone, x => x.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var rows = rawRows.Select(x =>
            {
                var realtorEmail = x.Realtor is null
                    ? null
                    : realtorEmails.GetValueOrDefault(x.Realtor.PhoneNumber);

                return new DealDocumentAccessLogSearchRow(
                    x.Log,
                    BuildDealSummary(x.Deal),
                    BuildClientSummary(x.Client),
                    BuildRealtorSummary(x.Realtor, realtorEmail));
            });

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                rows = rows.Where(x => MatchesSearch(x, search));
            }

            return rows
                .Take(Math.Clamp(filter.Limit, 1, 500))
                .ToList();
        }

        private static bool MatchesSearch(DealDocumentAccessLogSearchRow row, string search)
        {
            var log = row.Log;
            var values = new[]
            {
                log.DocumentTitle,
                log.DocumentFileName,
                log.ActorDisplayName,
                log.ActorEmail,
                log.ActorRole,
                log.DealId.ToString(),
                log.DealDocumentId.ToString(),
                log.CreatedDate.ToString("dd.MM.yyyy"),
                log.CreatedDate.ToString("yyyy-MM-dd"),
                row.DealSummary,
                row.ClientSummary,
                row.RealtorSummary
            };

            return values.Any(value =>
                !string.IsNullOrWhiteSpace(value)
                && value.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildDealSummary(Deal deal)
        {
            return $"{SourceLabel(deal.Source)}, {StatusLabel(deal.Status)}, {deal.Id}";
        }

        private static string BuildClientSummary(Client client)
        {
            var email = string.IsNullOrWhiteSpace(client.Email) ? null : client.Email.Trim();
            var contact = string.IsNullOrWhiteSpace(email)
                ? client.PhoneNumber
                : $"{client.PhoneNumber}, {email}";

            return $"{client.FullName} ({contact}), {client.Id}";
        }

        private static string BuildRealtorSummary(Realtor? realtor, string? email)
        {
            if (realtor is null)
                return "Риелтор не назначен";

            var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            var contact = string.IsNullOrWhiteSpace(normalizedEmail)
                ? realtor.PhoneNumber
                : $"{realtor.PhoneNumber}, {normalizedEmail}";

            return $"{realtor.FullName} ({contact}), {realtor.Id}";
        }

        private static string SourceLabel(DealSource source)
        {
            return source switch
            {
                DealSource.Home => "Каталог",
                DealSource.Matching => "Подбор",
                DealSource.Sale => "Продажа",
                _ => "Вручную"
            };
        }

        private static string StatusLabel(DealStatus status)
        {
            return status switch
            {
                DealStatus.Created => "Создана",
                DealStatus.InProgress => "В работе",
                DealStatus.Completed => "Завершена",
                DealStatus.Cancelled => "Отменена",
                _ => status.ToString()
            };
        }
    }
}
