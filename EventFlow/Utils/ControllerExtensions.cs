using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EventFlow.Utils;

public static class ControllerExtensions
{
    public static string GetRedirectUrl(this ControllerBase controller,
        string? path = null, IEnumerable<KeyValuePair<string, object?>>? args = null)
    {
        var referrer = controller.Request.GetTypedHeaders().Referer;
        var host = referrer is not null ?
            $"{referrer.Scheme}://{referrer.Authority}" :
            $"{controller.Request.Scheme}://{controller.Request.Host.ToUriComponent()}";
        path ??= "/";

        var redirectUrl = $"{host}{path}";
        if (args is not null)
        {
            redirectUrl = QueryHelpers.AddQueryString(redirectUrl, ToQueryStringSet(args));
        }

        return redirectUrl;
    }

    public static RedirectResult RedirectWithQuery(this ControllerBase controller,
        string returnUrl, IEnumerable<KeyValuePair<string, object?>> args)
    {
        return controller.RedirectImpl(returnUrl, null, args);
    }

    public static RedirectResult RedirectToReferrer(this ControllerBase controller,
        string returnUrl)
    {
        return controller.RedirectImpl(
            returnUrl,
            controller.Request.GetTypedHeaders().Referer,
            null
        );
    }

    public static RedirectResult RedirectToReferrerWithQuery(this ControllerBase controller,
        string returnUrl, IEnumerable<KeyValuePair<string, object?>> args)
    {
        return controller.RedirectImpl(
            returnUrl,
            controller.Request.GetTypedHeaders().Referer,
            args
        );
    }

    public static RedirectResult RedirectWithError(
        this ControllerBase controller,
        string? error = null,
        string? returnUrl = null,
        bool includeForm = true
    )
    {
        returnUrl ??= controller.Request.GetTypedHeaders().Referer?.ToString();
        returnUrl ??= "/";

        if (!controller.ModelState.IsValid)
        {
            error ??= controller.ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()
                ?.ErrorMessage;
        }

        var args = controller.Request.Query
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, object?>(q.Key, v)));

        if (includeForm && controller.Request.HasFormContentType)
        {
            var form = controller.Request.Form
                .SelectMany(f => f.Value.Select(v => new KeyValuePair<string, object?>(f.Key, v)));

            args = args.Concat(form);
        }

        if (error is not null)
        {
            args = args.Concat([ new(nameof(error), error) ]);
        }

        return controller.RedirectImpl(returnUrl, null, args);
    }

    private static RedirectResult RedirectImpl(this ControllerBase controller,
        string returnUrl, Uri? referrer, IEnumerable<KeyValuePair<string, object?>>? args)
    {
        if (args is not null)
        {
            returnUrl = QueryHelpers.AddQueryString(returnUrl, ToQueryStringSet(args));
        }

        // No referrer.
        if (referrer is null)
        {
            return controller.Redirect(returnUrl);
        }

        // Requested return is already absolute.
        var returnUri = new Uri(referrer, returnUrl);
        return controller.Redirect(returnUri.ToString());
    }

    [return: NotNullIfNotNull(nameof(args))]
    private static IEnumerable<KeyValuePair<string, string?>>? ToQueryStringSet(
        IEnumerable<KeyValuePair<string, object?>>? args)
    {
        return args?.Select((kvp) =>
        {
            return new KeyValuePair<string, string?>(kvp.Key, kvp.Value switch
            {
                bool b => b ? "true" : "false",
                _ => kvp.Value?.ToString()
            });
        });
    }

    public static Guid TryGetAccountId(this ControllerBase controller)
    {
        var value = controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(value))
        {
            return Guid.Empty;
        }

        if (!Guid.TryParse(value, out Guid userId))
        {
            return Guid.Empty;
        }

        return userId;
    }
}
