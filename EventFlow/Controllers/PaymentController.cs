using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EventFlow.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet]
    [Authorize]
    public ActionResult<IAsyncEnumerable<PaymentMethod>> GetPaymentMethods()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            return Ok(_paymentService.GetPaymentMethods(userId));
        }
        else
        {
            return BadRequest();
        }
    }

    [HttpPost("Card")]
    [Authorize]
    public async Task<ActionResult<CardPaymentMethod>> AddCard(
        [FromForm] string? displayName,
        [FromForm, Required, CreditCard] string number,
        [FromForm, Required]
        [RegularExpression(@"(?:0\d|1(?:0|1|2))/\d\d", ErrorMessage = "Invalid expiry.")]
        string expiry,
        [FromForm, Required, RegularExpression(@"\d\d\d", ErrorMessage = "Invalid CVV.")]
        string cvv,
        [FromForm, Required] string name,
        [FromQuery] string? returnUrl
    )
    {
        if (!ModelState.IsValid)
        {
            return this.RedirectWithError(includeForm: false);
        }

        var accountId = this.TryGetAccountId();
        if (accountId == Guid.Empty)
        {
            return this.RedirectWithError(error: ErrorStrings.SessionExpired, includeForm: false);
        }

        try
        {
            await _paymentService.AddCard(accountId, displayName, number, expiry, cvv, name);
            return this.RedirectToReferrer(returnUrl ?? "/");
        }
        catch
        {
            return this.RedirectWithError(error: ErrorStrings.ErrorTryAgain, includeForm: false);
        }
    }
}
