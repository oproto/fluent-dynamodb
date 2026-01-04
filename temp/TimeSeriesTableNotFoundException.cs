namespace Oproto.Shared.Api.DynamoDb.TimeSeries;

public class TimeSeriesTableNotFoundException : Exception
{
    public TimeSeriesTableNotFoundException(string message) : base(message)
    {
    }

    public TimeSeriesTableNotFoundException(DateTime dateTime) :
        base($"Time series table not found for date {dateTime:s}")
    {
    }
}