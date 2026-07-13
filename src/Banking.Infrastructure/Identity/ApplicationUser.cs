using Microsoft.AspNetCore.Identity;

namespace Banking.Infrastructure.Identity;

/// <summary>
/// The authentication identity. Deliberately separate from the domain:
/// accounts reference the user only through its id string (Account.Owner).
/// </summary>
public sealed class ApplicationUser : IdentityUser;
