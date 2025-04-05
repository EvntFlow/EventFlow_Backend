using EventFlow.Data;
using EventFlow.Services;
using Microsoft.AspNetCore.Identity;

namespace EventFlow.Identity;

internal sealed class IdentityEmailSender : IEmailSender<Account>
{
    private readonly IEmailService _emailService;

    public IdentityEmailSender(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public Task SendConfirmationLinkAsync(Account user, string email, string confirmationLink) =>
        _emailService.SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by clicking here: {confirmationLink}",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>."
        );

    public Task SendPasswordResetLinkAsync(Account user, string email, string resetLink) =>
        _emailService.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password by clicking here: {resetLink}",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>."
        );

    public Task SendPasswordResetCodeAsync(Account user, string email, string resetCode) =>
        _emailService.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password using the following code: {resetCode}",
            $"Please reset your password using the following code: {resetCode}"
        );
}
