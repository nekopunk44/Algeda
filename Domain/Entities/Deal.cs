using Domain.Common;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class Deal : BaseEntity
    {
        public Guid PropertyId { get; private set; }
        public Guid ClientId { get; private set; }
        public Guid RealtorId { get; private set; }
        public Guid? ClientRequirementId { get; private set; }
        public DealSource Source { get; private set; }
        public DealStatus Status { get; private set; }
        public string? RequestMessage { get; private set; }
        public DateTime? AcceptedAtUtc { get; private set; }
        public DateTime? RejectedAtUtc { get; private set; }
        public Guid? PriorityRealtorId { get; private set; }
        public DateTime? PriorityUntilUtc { get; private set; }

        // Данные для аналитики
        public Money Commission { get; private set; }
        public decimal CommissionAmount => Commission.Amount;
        public string CommissionCurrency => Commission.Currency;
        public decimal RealtorCommissionPercent { get; private set; }
        public Money RealtorPayout { get; private set; }
        public decimal RealtorPayoutAmount => RealtorPayout.Amount;
        public string RealtorPayoutCurrency => RealtorPayout.Currency;
        public Money AgencyNetCommission { get; private set; }
        public decimal AgencyNetCommissionAmount => AgencyNetCommission.Amount;
        public string AgencyNetCommissionCurrency => AgencyNetCommission.Currency;
        public DateTime? CompletedAt { get; private set; }
        public List<DealNote> Notes { get; private set; } = [];

        public bool IsIncoming => Status == DealStatus.Created && RealtorId == Guid.Empty;

        public Deal(
            Guid propertyId,
            Guid clientId,
            Guid realtorId,
            DealSource source = DealSource.Manual,
            Guid? clientRequirementId = null,
            string? requestMessage = null)
        {
            PropertyId = propertyId;
            ClientId = clientId;
            RealtorId = realtorId;
            ClientRequirementId = clientRequirementId;
            Source = source;
            Status = DealStatus.Created;
            RequestMessage = NormalizeRequestMessage(requestMessage);
            AcceptedAtUtc = realtorId == Guid.Empty ? null : DateTime.UtcNow;
            PriorityRealtorId = null;
            PriorityUntilUtc = null;
            Commission = Money.Zero();
            RealtorPayout = Money.Zero();
            AgencyNetCommission = Money.Zero();
            RealtorCommissionPercent = 0m;

            Validate();
        }

        private Deal()
        {
            Commission = Money.Zero();
            RealtorPayout = Money.Zero();
            AgencyNetCommission = Money.Zero();
        }

        public static Deal CreateIncoming(
            Guid clientId,
            DealSource source,
            Guid? propertyId = null,
            Guid? clientRequirementId = null,
            string? requestMessage = null)
        {
            return new Deal(
                propertyId ?? Guid.Empty,
                clientId,
                Guid.Empty,
                source,
                clientRequirementId,
                requestMessage);
        }

        public void AcceptIncoming(Guid realtorId)
        {
            if (!IsIncoming)
                throw new DomainException("Принять можно только входящую заявку.");

            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            RealtorId = realtorId;
            Status = DealStatus.InProgress;
            AcceptedAtUtc = DateTime.UtcNow;
            RejectedAtUtc = null;
            ClearPriority();
        }

        public void RejectIncoming(Guid realtorId)
        {
            if (!IsIncoming)
                throw new DomainException("Отклонить можно только входящую заявку.");

            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            RealtorId = realtorId;
            Status = DealStatus.Cancelled;
            RejectedAtUtc = DateTime.UtcNow;
            ClearPriority();
        }

        public void ReassignRealtor(Guid realtorId)
        {
            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            if (Status is DealStatus.Completed or DealStatus.Cancelled)
                throw new DomainException(ValidationMessages.CannotChangeState("Сделка"));

            RealtorId = realtorId;
            ClearPriority();

            if (Status == DealStatus.Created)
            {
                Status = DealStatus.InProgress;
            }

            AcceptedAtUtc ??= DateTime.UtcNow;
        }

        public void ReleaseRealtor(Guid realtorId)
        {
            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            if (Status != DealStatus.InProgress)
                throw new DomainException("Отказаться можно только от заявки в работе.");

            if (RealtorId != realtorId)
                throw new DomainException("Отказаться можно только от своей заявки.");

            RealtorId = Guid.Empty;
            Status = DealStatus.Created;
            AcceptedAtUtc = null;
            RejectedAtUtc = null;
        }

        public bool HasActivePriority(DateTime utcNow)
        {
            return PriorityRealtorId.HasValue
                && PriorityUntilUtc.HasValue
                && PriorityUntilUtc.Value > utcNow;
        }

        public void SetPriority(Guid realtorId, DateTime untilUtc)
        {
            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            if (untilUtc <= DateTime.UtcNow)
                throw new DomainException("Срок приоритета должен быть в будущем.");

            PriorityRealtorId = realtorId;
            PriorityUntilUtc = untilUtc;
        }

        public void ClearPriority()
        {
            PriorityRealtorId = null;
            PriorityUntilUtc = null;
        }

        public void UpdateRequestMessage(string? requestMessage)
        {
            RequestMessage = NormalizeRequestMessage(requestMessage);
        }

        public DealNote AddNote(string text, Guid? authorRealtorId = null)
        {
            var note = new DealNote(Id, text, authorRealtorId);
            Notes.Add(note);
            return note;
        }

        public DealNote UpdateNote(Guid noteId, string text)
        {
            var note = FindNote(noteId);
            note.UpdateText(text);
            return note;
        }

        public void RemoveNote(Guid noteId)
        {
            var note = FindNote(noteId);
            Notes.Remove(note);
        }

        private DealNote FindNote(Guid noteId)
        {
            if (noteId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(noteId)));

            var note = Notes.FirstOrDefault(x => x.Id == noteId);
            if (note is null)
                throw new DomainException("Заметка сделки не найдена.");

            return note;
        }

        public void Complete(
            decimal commissionAmount,
            string commissionCurrency = "USD",
            decimal realtorCommissionPercent = 0m)
        {
            if (Status == DealStatus.Completed)
                throw new DomainException(ValidationMessages.AlreadyInState("Сделка", "Завершена"));

            if (Status == DealStatus.Cancelled)
                throw new DomainException(ValidationMessages.CannotChangeState("Сделка"));

            if (RealtorId == Guid.Empty)
                throw new DomainException("Нельзя завершить сделку без назначенного риелтора.");

            if (commissionAmount <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(commissionAmount)));

            if (realtorCommissionPercent < 0 || realtorCommissionPercent > 100)
                throw new DomainException("Процент выплаты риелтору должен быть в диапазоне от 0 до 100.");

            var commission = new Money(commissionAmount, commissionCurrency);
            var payoutAmount = decimal.Round(
                commission.Amount * realtorCommissionPercent / 100m,
                2,
                MidpointRounding.AwayFromZero);
            var realtorPayout = new Money(payoutAmount, commission.Currency);
            var agencyNetCommission = commission.Subtract(realtorPayout);

            Status = DealStatus.Completed;
            Commission = commission;
            RealtorCommissionPercent = decimal.Round(realtorCommissionPercent, 2, MidpointRounding.AwayFromZero);
            RealtorPayout = realtorPayout;
            AgencyNetCommission = agencyNetCommission;
            CompletedAt = DateTime.UtcNow;
        }

        public void CompleteSaleWorkflow()
        {
            if (Status == DealStatus.Completed)
                return;

            if (Status == DealStatus.Cancelled)
                throw new DomainException(ValidationMessages.CannotChangeState("Заявка"));

            Status = DealStatus.Completed;
            Commission = Money.Zero();
            RealtorCommissionPercent = 0m;
            RealtorPayout = Money.Zero();
            AgencyNetCommission = Money.Zero();
            CompletedAt = DateTime.UtcNow;
        }

        public void Cancel()
        {
            if (Status == DealStatus.Completed)
                throw new DomainException(ValidationMessages.CannotChangeState("Сделка"));

            Status = DealStatus.Cancelled;
        }

        private static string? NormalizeRequestMessage(string? requestMessage)
        {
            if (string.IsNullOrWhiteSpace(requestMessage))
                return null;

            var normalized = requestMessage.Trim();
            if (normalized.Length > 4000)
                throw new DomainException("Комментарий к сделке не должен превышать 4000 символов.");

            return normalized;
        }

        private void Validate()
        {
            if (PropertyId == Guid.Empty && ClientRequirementId is null)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(PropertyId)));

            if (ClientId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ClientId)));

            if (Source == DealSource.Undefined)
                throw new DomainException("Источник сделки не указан.");

            if (Source == DealSource.Matching && ClientRequirementId is null)
                throw new DomainException("Для сделок из подбора нужно указать требование клиента.");

            if (RealtorId == Guid.Empty && Status != DealStatus.Created)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(RealtorId)));

            if (Commission is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(Commission)));

            if (RealtorPayout is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(RealtorPayout)));

            if (AgencyNetCommission is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(AgencyNetCommission)));
        }
    }
}
