namespace SubscriptionsService.API.Messages;

public class UnsubscribeMessage : BaseMessage
{
    public SubscriptionParams Params { get; set; } = default!;
}
