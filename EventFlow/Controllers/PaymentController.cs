using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;

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
            return Ok(_paymentService.GetPaymentMethodsAsync(userId));
        }
        else
        {
            return BadRequest();
        }
    }

    [HttpPost("Card")]
    [Authorize]
    public async Task<ActionResult<CardPaymentMethod>> AddCard(
        [FromForm]
        string? displayName,
        [FromForm]
        [CreditCard]
        string number,
        [FromForm]
        string expiry,
        [FromForm]
        string cvv,
        [FromForm]
        string name,
        [FromQuery]
        string returnUrl
    )
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            try
            {
                await _paymentService.AddCardAsync(userId, displayName, number, expiry, cvv, name);
            }
            catch
            {
                return BadRequest();
            }

            return Redirect(returnUrl);
        }
        else
        {
            return BadRequest();
        }
    }
}
