using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Domain.Common
{
    public abstract class BaseValueObject
    {
        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            var json = JsonSerializer.Serialize(this);
            var otherJson = JsonSerializer.Serialize(obj);
            var compare = String.Compare(json, otherJson, StringComparison.InvariantCultureIgnoreCase);
            return compare == 0;
        }

        public override int GetHashCode()
        {
            var json = JsonSerializer.Serialize(this);
            return json.GetHashCode();
        }
    }
}
