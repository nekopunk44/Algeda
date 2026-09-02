using Application.DTOs.DealDocuments;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Primitives;
using System.Security.Cryptography;

namespace Application.Services
{
    public sealed class DealDocumentService
    {
        public const long MaxFileBytes = 5 * 1024 * 1024;

        private static readonly Dictionary<string, string[]> AllowedContentTypesByExtension =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = ["application/pdf"],
                [".doc"] = ["application/msword", "application/octet-stream"],
                [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
                [".jpg"] = ["image/jpeg"],
                [".jpeg"] = ["image/jpeg"],
                [".png"] = ["image/png"],
                [".webp"] = ["image/webp"]
            };

        private readonly IDealRepository _dealRepository;
        private readonly IDealDocumentRepository _documentRepository;
        private readonly IDealDocumentAccessLogRepository _accessLogRepository;
        private readonly DealService _dealService;
        private readonly IIdentityAccountManager _identityAccountManager;

        public DealDocumentService(
            IDealRepository dealRepository,
            IDealDocumentRepository documentRepository,
            IDealDocumentAccessLogRepository accessLogRepository,
            DealService dealService,
            IIdentityAccountManager identityAccountManager)
        {
            _dealRepository = dealRepository;
            _documentRepository = documentRepository;
            _accessLogRepository = accessLogRepository;
            _dealService = dealService;
            _identityAccountManager = identityAccountManager;
        }

        public async Task<IReadOnlyList<DealDocumentResponse>> GetForDeal(
            Guid dealId,
            DealDocumentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var deal = await GetDealAndEnsureAccess(dealId, actor);
            var documents = await _documentRepository.GetActiveByDeal(deal.Id, cancellationToken);
            return documents
                .OrderByDescending(x => x.CreatedDate)
                .Select(MapDocument)
                .ToList();
        }

        public async Task<DealDocumentResponse> Add(
            Guid dealId,
            CreateDealDocumentRequest request,
            DealDocumentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var deal = await GetDealAndEnsureAccess(dealId, actor);
            ValidateFile(request);

            var actorName = await ResolveActorName(actor, cancellationToken);
            var contentType = NormalizeContentType(request.ContentType, request.FileName);
            var document = new DealDocument(
                deal.Id,
                request.Title,
                Path.GetFileName(request.FileName),
                contentType,
                request.Content,
                BuildSha256(request.Content),
                actor.UserId,
                actorName,
                actor.Email);

            await _documentRepository.Add(document);
            await AddLog(document, DealDocumentAction.Added, actor, actorName, cancellationToken);

            return MapDocument(document);
        }

        public async Task<DealDocumentFileResponse> Open(
            Guid dealId,
            Guid documentId,
            DealDocumentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            await GetDealAndEnsureAccess(dealId, actor);
            var document = await _documentRepository.GetActiveById(dealId, documentId, cancellationToken);
            if (document is null)
                throw new NotFoundException("Документ не найден.");

            var actorName = await ResolveActorName(actor, cancellationToken);
            await AddLog(document, DealDocumentAction.Opened, actor, actorName, cancellationToken);

            return new DealDocumentFileResponse(
                document.Id,
                document.Title,
                document.OriginalFileName,
                document.ContentType,
                document.Content);
        }

        public async Task Delete(
            Guid dealId,
            Guid documentId,
            DealDocumentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            await GetDealAndEnsureAccess(dealId, actor);
            var document = await _documentRepository.GetActiveById(dealId, documentId, cancellationToken);
            if (document is null)
                throw new NotFoundException("Документ не найден.");

            var actorName = await ResolveActorName(actor, cancellationToken);
            document.MarkDeleted(actor.UserId, actorName);
            _documentRepository.Update(document);
            await AddLog(document, DealDocumentAction.Deleted, actor, actorName, cancellationToken);
        }

        public async Task<IReadOnlyList<DealDocumentAccessLogResponse>> GetAccessLog(
            DealDocumentAccessLogFilter filter,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            if (!isAdmin)
                throw new NotFoundException("Журнал доступа к документам не найден.");

            var safeFilter = filter with
            {
                Limit = Math.Clamp(filter.Limit, 1, 500),
                Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim()
            };

            var logs = await _accessLogRepository.Search(safeFilter, cancellationToken);
            return logs.Select(MapLog).ToList();
        }

        private async Task<Deal> GetDealAndEnsureAccess(Guid dealId, DealDocumentActorContext actor)
        {
            var deal = await _dealRepository.GetById(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (actor.IsAdmin)
                return deal;

            if (!actor.IsRealtor)
                throw new NotFoundException("Сделка не найдена.");

            var realtorId = await _dealService.ResolveRealtorIdByEmail(actor.Email ?? string.Empty);
            if (deal.RealtorId == Guid.Empty || deal.RealtorId != realtorId)
                throw new NotFoundException("Сделка не найдена.");

            return deal;
        }

        private static void ValidateFile(CreateDealDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ValidationException("Укажите название документа.");

            if (string.IsNullOrWhiteSpace(request.FileName))
                throw new ValidationException("Не удалось определить имя файла.");

            if (request.Content.Length == 0)
                throw new ValidationException("Файл документа не должен быть пустым.");

            if (request.Content.Length > MaxFileBytes)
                throw new ValidationException("Размер документа не должен превышать 5 МБ.");

            var extension = Path.GetExtension(request.FileName);
            if (string.IsNullOrWhiteSpace(extension)
                || !AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes))
            {
                throw new ValidationException("Можно прикреплять только PDF, Word-документы и изображения JPG, PNG или WEBP.");
            }

            var contentType = NormalizeContentType(request.ContentType, request.FileName);
            if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                throw new ValidationException("Тип файла не соответствует разрешенным форматам.");
        }

        private async Task<string> ResolveActorName(
            DealDocumentActorContext actor,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(actor.DisplayName))
                return actor.DisplayName.Trim();

            if (actor.UserId.HasValue)
            {
                var user = await _identityAccountManager.GetUserById(actor.UserId.Value, cancellationToken);
                if (!string.IsNullOrWhiteSpace(user?.DisplayName))
                    return user.DisplayName.Trim();

                if (!string.IsNullOrWhiteSpace(user?.Email))
                    return user.Email.Trim();
            }

            return string.IsNullOrWhiteSpace(actor.Email)
                ? "Пользователь"
                : actor.Email.Trim();
        }

        private async Task AddLog(
            DealDocument document,
            DealDocumentAction action,
            DealDocumentActorContext actor,
            string actorName,
            CancellationToken cancellationToken)
        {
            var role = actor.IsAdmin ? "Администратор" : "Риелтор";
            var log = new DealDocumentAccessLog(
                document.DealId,
                document.Id,
                document.Title,
                document.OriginalFileName,
                action,
                actor.UserId,
                actorName,
                actor.Email,
                role,
                actor.IpAddress,
                actor.UserAgent);

            await _accessLogRepository.Add(log);
        }

        private static DealDocumentResponse MapDocument(DealDocument document)
        {
            return new DealDocumentResponse(
                document.Id,
                document.DealId,
                document.Title,
                document.OriginalFileName,
                document.ContentType,
                document.FileSizeBytes,
                document.UploadedByUserId,
                document.UploadedByDisplayName,
                document.UploadedByEmail,
                document.CreatedDate);
        }

        private static DealDocumentAccessLogResponse MapLog(DealDocumentAccessLogSearchRow row)
        {
            var log = row.Log;
            return new DealDocumentAccessLogResponse(
                log.Id,
                log.DealId,
                log.DealDocumentId,
                log.DocumentTitle,
                log.DocumentFileName,
                log.Action,
                log.ActorUserId,
                log.ActorDisplayName,
                log.ActorEmail,
                log.ActorRole,
                log.IpAddress,
                log.UserAgent,
                log.CreatedDate,
                row.DealSummary,
                row.ClientSummary,
                row.RealtorSummary);
        }

        private static string NormalizeContentType(string? contentType, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
                return contentType.Trim().ToLowerInvariant();

            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private static string BuildSha256(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }
    }
}
