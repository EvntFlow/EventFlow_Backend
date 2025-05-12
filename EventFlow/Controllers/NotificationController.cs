using EventFlow.Data.Model;
using EventFlow.Services;
using EventFlow.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventFlow.Controllers;

[Route("/api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [Authorize]
    public ActionResult<IAsyncEnumerable<Notification>> GetNotifications(
        [FromQuery] bool includeRead
    )
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest(ErrorStrings.SessionExpired);
        }

        try
        {
            return Ok(_notificationService.GetNotificationsAsync(userId, includeRead));
        }
        catch
        {
            return BadRequest(ErrorStrings.ErrorTryAgain);
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> ReadNotificationsAsync()
    {
        var userId = this.TryGetAccountId();
        if (userId == Guid.Empty)
        {
            return BadRequest(ErrorStrings.SessionExpired);
        }

        try
        {
            await _notificationService.ReadNotificationsAsync(userId);
            return Ok();
        }
        catch
        {
            return BadRequest(ErrorStrings.ErrorTryAgain);
        }
    }
}
