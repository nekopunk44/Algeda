using System.Text.Json;
using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class ClientRequirementCriterion : BaseEntity
    {
        private const int MaxSingleValueLength = 4000;

        public Guid ClientRequirementId { get; private set; }
        public Guid CriterionDefinitionId { get; private set; }
        public RequirementCriterionPriority Priority { get; private set; }
        public string? Value { get; private set; }
        public string? ValuesJson { get; private set; }

        public ClientRequirement? ClientRequirement { get; private set; }

        public ClientRequirementCriterion(
            Guid criterionDefinitionId,
            RequirementCriterionPriority priority,
            string? value = null,
            IEnumerable<string>? values = null)
        {
            CriterionDefinitionId = criterionDefinitionId;
            Priority = priority;

            SetValues(value, values);
            Validate();
        }



        private ClientRequirementCriterion()
        {
        }

        public IReadOnlyList<string> GetValues()
        {
            if (!string.IsNullOrWhiteSpace(Value))
                return [Value];

            if (string.IsNullOrWhiteSpace(ValuesJson))
                return [];

            var values = JsonSerializer.Deserialize<List<string>>(ValuesJson);
            return values ?? [];
        }

        internal void AttachToRequirement(Guid clientRequirementId)
        {
            if (clientRequirementId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(clientRequirementId)));

            ClientRequirementId = clientRequirementId;
        }

        private void SetValues(string? value, IEnumerable<string>? values)
        {
            var normalizedSingle = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();

            var normalizedMultiple = values?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasSingle = !string.IsNullOrWhiteSpace(normalizedSingle);
            var hasMultiple = normalizedMultiple is { Count: > 0 };

            if (hasSingle == hasMultiple)
                throw new DomainException("Для критерия требования нужно указать ровно одно: Value или Values.");

            if (hasSingle)
            {
                if (normalizedSingle!.Length > MaxSingleValueLength)
                    throw new DomainException($"Длина значения критерия должна быть не больше {MaxSingleValueLength}.");

                Value = normalizedSingle;
                ValuesJson = null;
                return;
            }

            Value = null;
            ValuesJson = JsonSerializer.Serialize(normalizedMultiple);
        }

        private void Validate()
        {
            if (CriterionDefinitionId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(CriterionDefinitionId)));

            if (Priority == RequirementCriterionPriority.Undefined)
                throw new DomainException("Приоритет критерия требования должен быть указан.");
        }
    }
}
