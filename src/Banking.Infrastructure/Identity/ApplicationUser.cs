using Microsoft.AspNetCore.Identity;

namespace Banking.Infrastructure.Identity;

// Bilerek domain'den ayrı; hesaplar kullanıcıya sadece id string'i (Account.Owner) üzerinden referans verir
public sealed class ApplicationUser : IdentityUser;
