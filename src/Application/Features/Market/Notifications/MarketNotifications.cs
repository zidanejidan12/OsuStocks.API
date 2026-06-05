using MediatR;
using OsuStocks.Domain.Market.Events;
using OsuStocks.Domain.OsuIntegration.Events;

namespace OsuStocks.Application.Features.Market.Notifications;

public sealed record BuyOrderExecutedNotification(BuyOrderExecuted Event) : INotification;
public sealed record SellOrderExecutedNotification(SellOrderExecuted Event) : INotification;
public sealed record TopPlayDetectedNotification(TopPlayDetected Event) : INotification;
public sealed record PpIncreasedNotification(PpIncreased Event) : INotification;
public sealed record PlayerInactiveNotification(PlayerInactive Event) : INotification;
public sealed record PriceChangedNotification(PriceChanged Event) : INotification;
