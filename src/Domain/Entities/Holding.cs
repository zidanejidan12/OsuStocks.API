namespace OsuStocks.Domain.Entities;

public sealed class Holding
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid StockId { get; set; }
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public Portfolio Portfolio { get; set; } = null!;
    public PlayerStock Stock { get; set; } = null!;
}
