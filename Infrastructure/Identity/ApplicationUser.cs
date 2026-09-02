using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? DisplayName { get; set; }

        public bool IsFrozen { get; set; }

        public DateTime? FrozenAtUtc { get; set; }

        public Guid? FrozenByUserId { get; set; }

        public string? EmailConfirmationCodeHash { get; set; }

        public DateTime? EmailConfirmationCodeExpiresAtUtc { get; set; }

        public string? PasswordResetCodeHash { get; set; }

        public DateTime? PasswordResetCodeExpiresAtUtc { get; set; }
    }
}
