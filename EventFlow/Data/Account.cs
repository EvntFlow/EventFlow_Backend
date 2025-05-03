using Microsoft.AspNetCore.Identity;

namespace EventFlow.Data;

public class Account : IdentityUser
{
    [ProtectedPersonalData]
    public string? FirstName { get; set; }

    [ProtectedPersonalData]
    public string? LastName { get; set; }

    [ProtectedPersonalData]
    public string? Website { get; set; }

    [ProtectedPersonalData]
    public string? Company { get; set; }

    [ProtectedPersonalData]
    public string? Address1 { get; set; }

    [ProtectedPersonalData]
    public string? Address2 { get; set; }

    [ProtectedPersonalData]
    public string? City { get; set; }

    [ProtectedPersonalData]
    public string? Country { get; set; }

    [ProtectedPersonalData]
    public string? Postcode { get; set; }
}
