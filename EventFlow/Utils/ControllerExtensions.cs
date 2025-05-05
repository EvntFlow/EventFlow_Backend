using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

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

    public static ActionResult RedirectWithQuery(this ControllerBase controller,
        string returnUrl, IEnumerable<KeyValuePair<string, object?>> args)
    {
        return controller.RedirectImpl(returnUrl, null, args);
    }

    public static ActionResult RedirectToReferrer(this ControllerBase controller,
        string returnUrl)
    {
        return controller.RedirectImpl(
            returnUrl,
            controller.Request.GetTypedHeaders().Referer,
            null
        );
    }

    public static ActionResult RedirectToReferrerWithQuery(this ControllerBase controller,
        string returnUrl, IEnumerable<KeyValuePair<string, object?>> args)
    {
        return controller.RedirectImpl(
            returnUrl,
            controller.Request.GetTypedHeaders().Referer,
            args
        );
    }

    public static ActionResult RedirectWithError(
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

        var uri = new Uri(returnUrl);
        var existingQuery = QueryHelpers.ParseQuery(uri.Query);
        returnUrl = uri.GetLeftPart(UriPartial.Path).ToString();

        var args = ToQueryObjectSet([]);

        // Strip any potentially conflicting search params.
        foreach (var key in controller.Request.Query.Keys)
        {
            existingQuery.Remove(key);
        }
        args = args.Concat(ToQueryObjectSet(controller.Request.Query));

        if (includeForm && controller.Request.HasFormContentType)
        {
            // Strip any potentially conflicting form params.
            foreach (var key in controller.Request.Form.Keys)
            {
                existingQuery.Remove(key);
            }
            returnUrl = uri.GetLeftPart(UriPartial.Path).ToString();

            args = args.Concat(ToQueryObjectSet(controller.Request.Form));
        }

        // Prevent conflicting error messages.
        existingQuery.Remove(nameof(error));
        if (error is not null)
        {
            args = args.Concat([ new(nameof(error), error) ]);
        }

        // Add back the original query strings.
        args = args.Concat(ToQueryObjectSet(existingQuery));

        return controller.RedirectImpl(returnUrl, null, args);
    }

    private static ActionResult RedirectImpl(this ControllerBase controller,
        string returnUrl, Uri? referrer, IEnumerable<KeyValuePair<string, object?>>? args)
    {
        if (referrer is not null)
        {
            // Requested return is already absolute.
            returnUrl = new Uri(referrer, returnUrl).ToString();
        }

        if (args is not null)
        {
            returnUrl = QueryHelpers.AddQueryString(returnUrl, ToQueryStringSet(args));
        }

        controller.Response.GetTypedHeaders().Location = new Uri(returnUrl);
        return new StatusCodeResult(StatusCodes.Status303SeeOther);
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

    [return: NotNullIfNotNull(nameof(args))]
    private static IEnumerable<KeyValuePair<string, object?>>? ToQueryObjectSet(
        IEnumerable<KeyValuePair<string, StringValues>>? args)
    {
        return args?.SelectMany(a =>
            a.Value.Select(v => new KeyValuePair<string, object?>(a.Key, v))
        );
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
