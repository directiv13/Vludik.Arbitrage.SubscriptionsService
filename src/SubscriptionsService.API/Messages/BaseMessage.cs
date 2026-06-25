namespace SubscriptionsService.API.Messages;

/// <summary>Minimal shape used to discriminate inbound messages by their action.</summary>
public class BaseMessage
{
    public string? Action { get; set; }
}
