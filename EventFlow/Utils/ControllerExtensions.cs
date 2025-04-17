using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EventFlow.Utils;

public static class ControllerExtensions
{
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

    private static RedirectResult RedirectImpl(this ControllerBase controller,
        string returnUrl, Uri? referrer, IEnumerable<KeyValuePair<string, object?>>? args)
    {
        if (args is not null)
        {
            returnUrl = QueryHelpers.AddQueryString(returnUrl, args.Select((kvp) =>
            {
                return new KeyValuePair<string, string?>(kvp.Key, kvp.Value switch
                {
                    bool b => b ? "true" : "false",
                    _ => kvp.Value?.ToString()
                });
            }));
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
}
