using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.Shared.Api.DynamoDb.TimeSeries;

public abstract class TimeSeriesTableCollection<TTableType> where TTableType : DynamoDbTableBase
{
    protected abstract int MonthsInTablePeriod { get; }
    protected abstract bool AllowFutureTables { get; }
    protected abstract Task<string> GetTableNameForDateAsync(DateTime date);
    protected abstract TTableType GetTableInstanceForTableName(string tableName);

    public async Task<TTableType> ForDate(DateTime date)
    {
        var tableName = await GetTableNameForDateAsync(date);
        return GetTableInstanceForTableName(tableName);
    }

    public async Task<IList<TTableType>> TablesForDateRange(DateTime startDate, DateTime endDate)
    {
        List<TTableType> tables = new();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endDate, startDate);
        while (true)
        {
            var tableName = await GetTableNameForDateAsync(startDate);
            var table = GetTableInstanceForTableName(tableName);
            tables.Add(table);

            startDate = NormalizeToNextPeriod(startDate, MonthsInTablePeriod, true);
            if (startDate >= endDate) break;
        }

        return tables;
    }


    public async Task<CrossTableQueryResponse> CrossTableQueryAsync(DateTime startDate, int pageSize,
        string paginationToken, bool isAscending,
        DateRange limits,
        Action<QueryRequestBuilder> queryBuilderFunc,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Implement safety features to prevent running amok
        // Ideas:
        //  - Limit number of time series tables to query

        CrossTableQueryResponse response = new CrossTableQueryResponse();
        response.Items = new List<Dictionary<string, AttributeValue>>();

        DateTime nextTableDate = startDate;

        if (!String.IsNullOrEmpty(paginationToken))
        {
            var paginationTokenParts = paginationToken.Split('_');
            nextTableDate = new DateTime(long.Parse(paginationTokenParts[0]), DateTimeKind.Utc);
            paginationToken = paginationTokenParts[1];
        }

        while (response.Items.Count < pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            response.PaginationToken = String.Empty;

            if (!limits.Contains(nextTableDate)) break;

            TTableType table = await ForDate(nextTableDate);
            if (table == null) break;

            var queryBuilder = table.Query;
            queryBuilderFunc(queryBuilder);
            var queryResponse = await queryBuilder
                .ScanIndexForward(isAscending)
                .Paginate(new PaginationRequest(pageSize - response.Items.Count, paginationToken))
                .ExecuteAsync(cancellationToken);
            paginationToken = String.Empty;

            // Aggregate query metrics up to the combined query
            if (queryResponse.ConsumedCapacity != null) response.AddCapacityUsed(queryResponse.ConsumedCapacity);
            response.ScannedCount += queryResponse.ScannedCount ?? 0;
            response.Count += queryResponse.Count ?? 0;
            response.QueryOperations++;

            if (queryResponse.Count > 0)
            {
                response.Items.AddRange(queryResponse.Items);
            }

            // If we have a last evaluated key, then we reached our pagination limit
            if (queryResponse.LastEvaluatedKey != null && queryResponse.LastEvaluatedKey.Count != 0)
            {
                response.PaginationToken = $"{nextTableDate.Ticks}_{queryResponse.GetEncodedPaginationToken()}";
                break;
            }

            nextTableDate = NormalizeToNextPeriod(nextTableDate, MonthsInTablePeriod, isAscending);
            response.PaginationToken = $"{nextTableDate.Ticks}_";
        }

        return response;
    }

    protected DateTime NormalizeToNextPeriod(DateTime currentDate, int monthsInPeriod, bool isAscending)
    {
        // Get the base year and month for the period
        var baseYear = currentDate.Year;
        var baseMonth = currentDate.Month;

        // Calculate which period this date falls into
        var periodIndex = (baseMonth - 1) / monthsInPeriod;

        if (isAscending)
        {
            // Move to start of next period
            var nextPeriodStartMonth = (periodIndex + 1) * monthsInPeriod + 1;

            // If we're already past the last period in the year, move to next year
            if (nextPeriodStartMonth > 12)
            {
                baseYear++;
                nextPeriodStartMonth = 1;
            }

            return new DateTime(baseYear, nextPeriodStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            // Move to start of current period
            var previousPeriodStartMonth = periodIndex * monthsInPeriod + 1;

            // If we're in the first period of the year, move to previous year
            if (previousPeriodStartMonth < 1)
            {
                baseYear--;
                previousPeriodStartMonth = 12 - monthsInPeriod + 1;
            }

            return new DateTime(baseYear, previousPeriodStartMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        }
    }

    public class CrossTableQueryResponse
    {
        public List<Dictionary<string, AttributeValue>> Items { get; set; }
        public string PaginationToken { get; set; }
        public int ScannedCount { get; set; }
        public int Count { get; set; }
        public int QueryOperations { get; set; }

        public ConsumedCapacity AggregatedConsumedCapacity { get; init; } = new();

        public Dictionary<string, ConsumedCapacity> TableConsumedCapacity { get; init; } = new();

        private void CopyCapacity(ConsumedCapacity src, ConsumedCapacity dest, bool append)
        {
            dest.CapacityUnits = (append ? dest.CapacityUnits : 0) + src.CapacityUnits;
            dest.ReadCapacityUnits = (append ? dest.ReadCapacityUnits : 0) + src.ReadCapacityUnits;
            dest.WriteCapacityUnits = (append ? dest.WriteCapacityUnits : 0) + src.WriteCapacityUnits;

            CopyCapacity(src.Table, dest.Table, append);

            if (src.GlobalSecondaryIndexes != null)
                foreach (var kvp in src.GlobalSecondaryIndexes)
                {
                    if (!dest.GlobalSecondaryIndexes.ContainsKey(kvp.Key))
                        dest.GlobalSecondaryIndexes.Add(kvp.Key, new Capacity());
                    var destIndexCapacity = dest.GlobalSecondaryIndexes[kvp.Key];
                    CopyCapacity(kvp.Value, destIndexCapacity, append);
                }

            if (src.LocalSecondaryIndexes != null)
                foreach (var kvp in src.LocalSecondaryIndexes)
                {
                    if (!dest.LocalSecondaryIndexes.ContainsKey(kvp.Key))
                        dest.LocalSecondaryIndexes.Add(kvp.Key, new Capacity());
                    var destIndexCapacity = dest.LocalSecondaryIndexes[kvp.Key];
                    CopyCapacity(kvp.Value, destIndexCapacity, append);
                }
        }

        private void CopyCapacity(Capacity src, Capacity dest, bool append)
        {
            dest.CapacityUnits = (append ? dest.CapacityUnits : 0) + src.CapacityUnits;
            dest.ReadCapacityUnits = (append ? dest.ReadCapacityUnits : 0) + src.ReadCapacityUnits;
            dest.WriteCapacityUnits = (append ? dest.WriteCapacityUnits : 0) + src.WriteCapacityUnits;
        }

        internal void AddCapacityUsed(ConsumedCapacity consumedCapacity)
        {
            var tblName = consumedCapacity.TableName;
            if (!TableConsumedCapacity.ContainsKey(tblName))
                TableConsumedCapacity.Add(tblName, new ConsumedCapacity());

            CopyCapacity(consumedCapacity, TableConsumedCapacity[tblName],
                true); // For multiple reads of the same table
            CopyCapacity(consumedCapacity, AggregatedConsumedCapacity, true); // For aggregation across multiple tables
        }
    }
}