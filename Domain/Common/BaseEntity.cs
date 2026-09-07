namespace Domain.Common
{
    /// <summary>
    /// Базовый класс сущностей
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Уникальный идентификатор сущности
        /// </summary>
        public Guid Id { get; protected set; }

        /// <summary>
        /// Дата создания сущности
        /// </summary>
        public DateTime CreatedDate { get; protected set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Метод для сравнения двух сущностей по ID
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is not BaseEntity other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (GetType() != other.GetType())
                return false;

            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
    }
}
