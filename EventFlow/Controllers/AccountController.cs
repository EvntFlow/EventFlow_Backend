﻿using EventFlow.Data;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace EventFlow.Controllers;

[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private const string LoginCallbackAction = "LoginCallback";

    private readonly SignInManager<Account> _signInManager;
    private readonly UserManager<Account> _userManager;
    private readonly IUserStore<Account> _userStore;
    private readonly IEmailSender<Account> _emailSender;
    private readonly AccountService _accountService;

    public AccountController(
        SignInManager<Account> signInManager,
        UserManager<Account> userManager,
        IUserStore<Account> userStore,
        IEmailSender<Account> emailSender,
        AccountService accountService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userStore = userStore;
        _emailSender = emailSender;
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<Data.Model.Account>> GetAccount()
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync($"{userId}");
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new Data.Model.Account()
        {
            Id = Guid.Parse(user.Id),
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Website = user.Website,
            Company = user.Company,
            PhoneNumber = user.PhoneNumber,
            Address1 = user.Address1,
            Address2 = user.Address2,
            City = user.City,
            Country = user.Country,
            Postcode = user.Postcode
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> UpdateAccount(
        [FromForm] string? firstName,
        [FromForm] string? lastName,
        [FromForm] string? website,
        [FromForm] string? company,
        [FromForm, Phone] string? phoneNumber,
        [FromForm] string? address1,
        [FromForm] string? address2,
        [FromForm] string? city,
        [FromForm] string? country,
        [FromForm] string? postcode,
        [FromQuery] string? returnUrl
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        var user = await _userManager.FindByIdAsync($"{userId}");
        if (user is null)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Website = website;
        user.Company = company;
        user.Address1 = address1;
        user.Address2 = address2;
        user.City = city;
        user.Country = country;
        user.Postcode = postcode;

        if (_userStore is IUserPhoneNumberStore<Account> phoneNumberStore)
        {
            await phoneNumberStore.SetPhoneNumberAsync(user, phoneNumber, CancellationToken.None);
        }
        else
        {
            user.PhoneNumber = phoneNumber;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return this.RedirectWithError(result.Errors.First().Description);
        }

        return this.RedirectToReferrer(returnUrl ?? "/");
    }

    [HttpPost(nameof(Login))]
    public async Task<ActionResult> Login(
        [FromForm]
        string email,
        [FromForm]
        string password,
        [FromForm]
        bool isPersistent,
        [FromQuery]
        string returnUrl
    )
    {
        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent, false);

        if (result.Succeeded)
        {
            return this.RedirectToReferrer(returnUrl);
        }
        else if (result.RequiresTwoFactor)
        {
            return this.RedirectToReferrerWithQuery("/Account/LoginWith2fa",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    { nameof(isPersistent), isPersistent }
                });
        }
        else if (result.IsLockedOut)
        {
            return this.RedirectToReferrer("/Account/Lockout");
        }
        else
        {
            return this.RedirectToReferrerWithQuery("/Account/Login",
                new Dictionary<string, object?>() {
                    { nameof(returnUrl), returnUrl },
                    { "error", "Invaild email or password." }
                });
        }
    }

    [HttpPost(nameof(Logout))]
    [Authorize]
    public async Task<ActionResult> Logout(
        [FromQuery]
        string? returnUrl
    )
    {
        await _signInManager.SignOutAsync();
        return this.RedirectToReferrer(returnUrl ?? "/");
    }

    [HttpPost(nameof(Register))]
    public async Task<ActionResult> Register(
        [FromForm]
        [Required]
        [EmailAddress(ErrorMessage = "The email is invalid.")]
        string email,
        [FromForm]
        [Required]
        [StringLength(int.MaxValue,
            ErrorMessage = "The {0} must be at least {2} characters long.",
            MinimumLength = 8)]
        string password,
        [FromQuery]
        string returnUrl
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectToReferrerWithQuery("/Account/Register",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    {
                        "error",
                        ModelState.First(
                            ms => ms.Value?.ValidationState == ModelValidationState.Invalid
                        ).Value!.Errors.First().ErrorMessage
                    },
                }
            );
        }

        var user = new Account();

        await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await ((IUserEmailStore<Account>)_userStore)
            .SetEmailAsync(user, email, CancellationToken.None);
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return this.RedirectToReferrerWithQuery("/Account/Register",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    { "error", result.Errors.First().Description },
                }
            );
        }

        var userId = await _userManager.GetUserIdAsync(user);
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.GetRedirectUrl(
            path: "/Account/ConfirmEmail",
            args: new Dictionary<string, object?>() {
                { nameof(userId), userId },
                { nameof(code), code },
                { nameof(returnUrl), returnUrl }
            }
        );

        await _emailSender.SendConfirmationLinkAsync(user, email, callbackUrl);

        if (_userManager.Options.SignIn.RequireConfirmedAccount)
        {
            return this.RedirectToReferrerWithQuery("/Account/RegisterConfirmation",
                new Dictionary<string, object?>() {
                    { nameof(returnUrl), returnUrl },
                    { nameof(email), email }
                });
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return this.RedirectToReferrer(returnUrl);
    }

    [HttpGet(nameof(ExternalLogin))]
    public ActionResult ExternalLogin(
        [FromQuery]
        string provider,
        [FromQuery]
        bool isPersistent,
        [FromQuery]
        string referrer,
        [FromQuery]
        string returnUrl
    )
    {
        var query = new Dictionary<string, string?> {
            { nameof(returnUrl), returnUrl },
            { nameof(isPersistent), isPersistent.ToString().ToLowerInvariant() },
            { nameof(referrer), referrer },
            { "action", LoginCallbackAction }
        };

        var redirectUrl = $"{Request.Scheme}://" +
            $"{Request.Host.ToUriComponent()}" +
            $"/api/Account/{nameof(ExternalCallback)}" +
            QueryString.Create(query);

        var properties = _signInManager
            .ConfigureExternalAuthenticationProperties(provider, redirectUrl);

        return Challenge(properties, [provider]);
    }

    [HttpGet(nameof(ExternalCallback))]
    public async Task<ActionResult> ExternalCallback(
        [FromQuery]
        string action,
        [FromQuery]
        bool isPersistent,
        [FromQuery]
        string referrer,
        [FromQuery]
        string returnUrl
    )
    {
        if (referrer is not null)
        {
            // Set the referrer to the frontend instead of the external provider.
            Request.GetTypedHeaders().Referer = new Uri(referrer);
        }
        else
        {
            // Fall back to ourselves instead of confusing the external provider.
            Request.GetTypedHeaders().Referer = new Uri(Request.GetDisplayUrl());
        }

        var externalLoginInfo = await _signInManager.GetExternalLoginInfoAsync();
        if (externalLoginInfo is null)
        {
            return this.RedirectToReferrerWithQuery("/Account/Login",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    { "error", "Failed to sign in with the selected provider." }
                });
        }

        switch (action)
        {
            case LoginCallbackAction:
            {
                var result = await _signInManager.ExternalLoginSignInAsync(
                    externalLoginInfo.LoginProvider,
                    externalLoginInfo.ProviderKey,
                    isPersistent,
                    bypassTwoFactor: true
                );

                if (result.Succeeded)
                {
                    return this.RedirectToReferrer(returnUrl);
                }
                else if (result.IsLockedOut)
                {
                    return this.RedirectToReferrer("/Account/Lockout");
                }

                var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
                var providerDisplayName = externalLoginInfo.ProviderDisplayName;

                return this.RedirectToReferrerWithQuery("/Account/ExternalLogin",
                    new Dictionary<string, object?>() {
                        { nameof(email), email },
                        { nameof(providerDisplayName), providerDisplayName },
                        { nameof(returnUrl), returnUrl }
                    }
                );
            }
            default:
                return BadRequest("Invalid action.");
        }
    }

    [HttpPost(nameof(ExternalRegister))]
    public async Task<ActionResult> ExternalRegister(
        [FromForm]
        [EmailAddress(ErrorMessage = "The email is invalid.")]
        string email,
        [FromQuery]
        string returnUrl
    )
    {
        var externalLoginInfo = await _signInManager.GetExternalLoginInfoAsync();
        if (externalLoginInfo is null)
        {
            return this.RedirectToReferrerWithQuery("/Account/Login",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    { "error", "Failed to sign in with the selected provider." }
                }
            );
        }

        var emailStore = (IUserEmailStore<Account>)_userStore;
        var user = Activator.CreateInstance<Account>();

        await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);

        var result = await _userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            result = await _userManager.AddLoginAsync(user, externalLoginInfo);
            if (result.Succeeded)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = this.GetRedirectUrl(
                    path: "/Account/ConfirmEmail",
                    args: new Dictionary<string, object?>() {
                        { nameof(userId), userId },
                        { nameof(code), code },
                        { nameof(returnUrl), returnUrl }
                    }
                );

                await _emailSender.SendConfirmationLinkAsync(user, email, callbackUrl);

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    return this.RedirectToReferrerWithQuery("/Account/RegisterConfirmation",
                        new Dictionary<string, object?>() {
                            { nameof(returnUrl), returnUrl },
                            { nameof(email), email }
                        });
                }

                await _signInManager.SignInAsync(user, isPersistent: false);
                return this.RedirectToReferrer(returnUrl);
            }
        }

        return this.RedirectToReferrerWithQuery("/Account/ExternalLogin",
            new Dictionary<string, object?>()
            {
                { nameof(returnUrl), returnUrl },
                { nameof(email), email },
                {
                    "error",
                    string
                        .Join("; ", result.Errors.Select(error => error.Description))
                }
            }
        );
    }

    [HttpPost(nameof(ResendEmailConfirmation))]
    public async Task<ActionResult> ResendEmailConfirmation(
        [FromForm]
        string email,
        [FromQuery]
        string returnUrl
    )
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return this.RedirectToReferrerWithQuery("/Account/RegisterConfirmation",
                new Dictionary<string, object?>() {
                    { nameof(returnUrl), returnUrl },
                    { nameof(email), email }
                });
        }

        var userId = await _userManager.GetUserIdAsync(user);

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.GetRedirectUrl(
            path: "/Account/ConfirmEmail",
            args: new Dictionary<string, object?>() {
                { nameof(userId), userId },
                { nameof(code), code },
                { nameof(returnUrl), returnUrl }
            }
        );

        await _emailSender.SendConfirmationLinkAsync(user, email, callbackUrl);

        return this.RedirectToReferrerWithQuery("/Account/RegisterConfirmation",
            new Dictionary<string, object?>() {
                { nameof(returnUrl), returnUrl },
                { nameof(email), email }
            });
    }

    [HttpPost(nameof(ConfirmEmail))]
    public async Task<ActionResult> ConfirmEmail(
        [FromForm]
        string userId,
        [FromForm]
        string code,
        [FromQuery]
        string? returnUrl
    )
    {
        returnUrl ??= "/";

        try
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("Invalid user ID.");

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);

            if (result.Succeeded)
            {
                return this.RedirectToReferrerWithQuery("/Account/Login",
                    new Dictionary<string, object?>()
                    {
                        { nameof(returnUrl), returnUrl }
                    }
                );
            }
            else
            {
                return this.RedirectToReferrerWithQuery("/Account/ConfirmEmail",
                    new Dictionary<string, object?>()
                    {
                        { nameof(returnUrl), returnUrl },
                        { "error", result.Errors.First().Description },
                    }
                );
            }
        }
        catch
        {
            return this.RedirectToReferrerWithQuery("/Account/ConfirmEmail",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl },
                    { "error", "Invalid confirmation link." },
                }
            );
        }
    }

    [HttpPost(nameof(ForgotPassword))]
    public async Task<ActionResult> ForgotPassword(
        [FromForm]
        string email,
        [FromQuery]
        string returnUrl
    )
    {
        var user = await _userManager.FindByNameAsync(email);

        if (user is null
            || !await _userManager.IsEmailConfirmedAsync(user)
            || string.IsNullOrEmpty(user.Email))
        {
            return this.RedirectToReferrerWithQuery("/Account/ForgotPasswordConfirmation",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl }
                }
            );
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = this.GetRedirectUrl(
            path: "/Account/ResetPassword",
            args: new Dictionary<string, object?>() {
                { nameof(code), code },
                { nameof(email), email },
                { nameof(returnUrl), returnUrl }
            }
        );

        await _emailSender.SendPasswordResetLinkAsync(user, user.Email!, callbackUrl);

        return this.RedirectToReferrerWithQuery("/Account/ForgotPasswordConfirmation",
            new Dictionary<string, object?>()
            {
                { nameof(returnUrl), returnUrl }
            }
        );
    }

    [HttpPost(nameof(ResetPassword))]
    public async Task<ActionResult> ResetPassword(
        [FromForm]
        string email,
        [FromForm]
        string password,
        [FromForm]
        string code,
        [FromQuery]
        string returnUrl
    )
    {
        try
        {
            var user = await _userManager.FindByNameAsync(email)
                ?? throw new InvalidOperationException("Invalid user name");

            var result = await _userManager.ResetPasswordAsync(
                user,
                Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
                password
            );

            if (result.Succeeded)
            {
                return this.RedirectToReferrerWithQuery("/Account/ResetPasswordConfirmation",
                    new Dictionary<string, object?>()
                    {
                        { nameof(returnUrl), returnUrl }
                    }
                );
            }

            return this.RedirectToReferrerWithQuery("/Account/ResetPassword",
                new Dictionary<string, object?>()
                {
                    { nameof(email), email },
                    { nameof(code), code },
                    { nameof(returnUrl), returnUrl },
                    { "error", result.Errors.First().Description },
                }
            );
        }
        catch
        {
            // Don't reveal that the user does not exist or other server errors.
            return this.RedirectToReferrerWithQuery("/Account/ResetPasswordConfirmation",
                new Dictionary<string, object?>()
                {
                    { nameof(returnUrl), returnUrl }
                }
            );
        }
    }

    [HttpPost(nameof(ChangePassword))]
    [Authorize]
    public async Task<ActionResult> ChangePassword(
        [FromForm, Required]
        string oldPassword,
        [FromForm, Required]
        string newPassword,
        [FromQuery]
        string? returnUrl
    )
    {
        try
        {
            var userId = this.TryGetAccountId();
            if (userId == Guid.Empty)
            {
                return this.RedirectWithError(
                    error: ErrorStrings.SessionExpired,
                    includeForm: false
                );
            }

            var user = await _userManager.FindByIdAsync($"{userId}");
            if (user is null)
            {
                return this.RedirectWithError(
                    error: ErrorStrings.SessionExpired,
                    includeForm: false
                );
            }

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (!result.Succeeded)
            {
                return this.RedirectWithError(
                    result.Errors.First().Description,
                    includeForm: false
                );
            }

            return this.RedirectToReferrer(returnUrl ?? "/");
        }
        catch
        {
            return this.RedirectWithError(
                error: ErrorStrings.ErrorTryAgain,
                includeForm: false
            );
        }
    }

    [HttpPost(nameof(Data.Model.Attendee))]
    [Authorize]
    public async Task<ActionResult> CreateAttendee(
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        try
        {
            await _accountService.CreateAttendee(userId);
            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet(nameof(Data.Model.Attendee))]
    [Authorize]
    public async Task<ActionResult<Data.Model.Attendee>> GetAttendee(
        [FromQuery(Name = "user")] Guid? userId
    )
    {
        userId ??= this.TryGetAccountId();

        try
        {
            var attendee = await _accountService.GetAttendee(userId.Value);
            if (attendee is null)
            {
                return NotFound();
            }
            return Ok(attendee);
        }
        catch
        {
            return BadRequest();
        }
    }

    [HttpPost(nameof(Data.Model.Organizer))]
    [Authorize]
    public async Task<ActionResult> CreateOrganizer(
        [FromQuery(Name = "returnUrl")] Uri? returnUri
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError();
        }

        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired);
        }

        try
        {
            await _accountService.CreateOrganizer(userId);
            return this.RedirectToReferrer(returnUri?.ToString() ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpGet(nameof(Data.Model.Organizer))]
    [Authorize]
    public async Task<ActionResult<Data.Model.Organizer>> GetOrganizer(
        [FromQuery(Name = "user")] Guid? userId
    )
    {
        userId ??= this.TryGetAccountId();

        try
        {
            var organizer = await _accountService.GetOrganizer(userId.Value);
            if (organizer is null)
            {
                return NotFound();
            }
            return Ok(organizer);
        }
        catch
        {
            return BadRequest();
        }
    }
}
