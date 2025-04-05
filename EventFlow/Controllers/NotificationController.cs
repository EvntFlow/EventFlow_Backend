using EventFlow.Data.Model;
using EventFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventFlow.Controllers;

[ApiController]
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
    public ActionResult<IAsyncEnumerable<Notification>> GetNotifications()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            return Ok(_notificationService.GetNotificationsAsync(userId));
        }
        else
        {
            return BadRequest();
        }
    }
}
