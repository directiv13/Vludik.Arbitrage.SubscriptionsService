namespace SubscriptionsService.API.Messages;

public class SubscribeMessage : BaseMessage
{
    public SubscriptionParams Params { get; set; } = default!;
}
