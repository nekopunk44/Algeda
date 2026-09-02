using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Primitives
{
    public static class ValidationMessages
    {
        public static string NotNull(string fieldName)
            => $"{fieldName} не может быть null.";

        public static string NotEmpty(string fieldName)
            => $"{fieldName} не может быть пустым.";

        public static string InvalidProperty(string fieldName)
            => $"{fieldName} имеет недопустимое значение.";

        public static string MustBeGreaterThanZero(string fieldName)
            => $"{fieldName} должно быть больше нуля.";

        public static string InvalidGuid(string fieldName)
            => $"{fieldName} имеет некорректное значение.";

        public static string AlreadyInState(string entity, string state)
            => $"{entity} уже находится в состоянии \"{state}\".";

        public static string CannotChangeState(string entity)
            => $"Невозможно изменить состояние объекта \"{entity}\".";
    }
}
