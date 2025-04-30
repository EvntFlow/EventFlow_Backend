namespace EventFlow.Utils;

public static class ErrorStrings
{
    public static string ErrorTryAgain => "An error occurred. Please try again.";

    public static string EventStartAfterEnd => "Start date must be before end date.";

    public static string EventPriceNegative => "Price cannot be negative.";

    public static string InvalidCategory => "The specified category is invalid.";

    public static string InvalidEvent => "The specified event is invalid.";

    public static string InvalidPaymentMethod => "The specified payment method is invalid.";

    public static string InvalidTicket => "The specified ticket is invalid.";

    public static string InvalidTicketOption => "The specified ticket option is invalid.";

    public static string ListLengthMismatch => "Length mismatch in attribute lists.";

    public static string NotAnAttendee => "This functionality is only available to attendees.";

    public static string NotAnOrganizer => "This functionality is only available to organizers.";

    public static string SessionExpired => "Session expired.";

    public static string TicketNoAccess => "You do not have access to this ticket.";

    public static string TicketGone => "This ticket is no longer available.";
}
