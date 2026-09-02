using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Client
{
    public record ClientResponse(
        Guid Id,
        string FirstName,
        string LastName,
        string? MiddleName,
        string PhoneNumber,
        string? Email,
        DateTime CreatedDate);
}
