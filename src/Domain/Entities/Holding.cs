using OsuStocks.Domain.Common.Interfaces;

namespace OsuStocks.Domain.Entities;

public sealed class Holding : IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid StockId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public Portfolio Portfolio { get; set; } = null!;
    public PlayerStock Stock { get; set; } = null!;
}
