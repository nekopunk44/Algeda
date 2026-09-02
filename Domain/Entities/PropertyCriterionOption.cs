using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class PropertyCriterionOption : BaseEntity
    {
        public Guid PropertyCriterionDefinitionId { get; private set; }
        public string Value { get; private set; }
        public string Label { get; private set; }
        public int SortOrder { get; private set; }

        public PropertyCriterionOption(
            Guid propertyCriterionDefinitionId,
            string value,
            string label,
            int sortOrder = 0)
        {
            PropertyCriterionDefinitionId = propertyCriterionDefinitionId;
            Value = value.Trim();
            Label = label.Trim();
            SortOrder = sortOrder;

            Validate();
        }

        private PropertyCriterionOption()
        {
            Value = string.Empty;
            Label = string.Empty;
        }

        private void Validate()
        {
            if (PropertyCriterionDefinitionId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(PropertyCriterionDefinitionId)));

            if (string.IsNullOrWhiteSpace(Value))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Value)));

            if (string.IsNullOrWhiteSpace(Label))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Label)));
        }
    }
}
