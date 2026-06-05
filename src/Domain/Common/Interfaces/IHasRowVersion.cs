namespace OsuStocks.Domain.Common.Interfaces;

public interface IHasRowVersion
{
    long RowVersion { get; set; }
}
