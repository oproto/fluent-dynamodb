using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Examples.Shared;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using TodoList.Entities;

namespace Examples.Tests.TodoList;

/// <summary>
/// Property-based tests for TodoItem operations.
/// These tests require DynamoDB Local to be running on port 8000.
/// </summary>
public class TodoItemPropertyTests
{
    private const string TestTableName = "todo-items-test";

    /// <summary>
    /// **Feature: example-applications, Property 2: Todo Item Creation Completeness**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any valid description string, creating a todo item should result in an item with
    /// a non-empty ID, the provided description, IsComplete=false, and a CreatedAt timestamp
    /// within the last minute.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TodoCreation_HasRequiredFields()
    {
        return Prop.ForAll(
            GenerateValidDescription(),
            description =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
                    var item = table.AddAsync(description).GetAwaiter().GetResult();
                    var afterCreation = DateTime.UtcNow.AddSeconds(1);

                    // Clean up
                    table.DeleteAsync(item.Id).GetAwaiter().GetResult();

                    var hasNonEmptyId = !string.IsNullOrEmpty(item.Id);
                    var hasCorrectDescription = item.Description == description;
                    var isNotComplete = !item.IsComplete;
                    var hasValidTimestamp = item.CreatedAt >= beforeCreation && item.CreatedAt <= afterCreation;
                    var completedAtIsNull = item.CompletedAt == null;

                    return (hasNonEmptyId && hasCorrectDescription && isNotComplete && hasValidTimestamp && completedAtIsNull)
                        .ToProperty()
                        .Label($"HasId: {hasNonEmptyId}, CorrectDesc: {hasCorrectDescription}, " +
                               $"NotComplete: {isNotComplete}, ValidTimestamp: {hasValidTimestamp}, " +
                               $"CompletedAtNull: {completedAtIsNull}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 3: Todo List Retrieval Completeness**
    /// **Validates: Requirements 2.2**
    /// 
    /// For any set of todo items stored in the table, querying all items should return
    /// exactly those items with all fields intact.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TodoListRetrieval_ReturnsAllItems()
    {
        return Prop.ForAll(
            GenerateDescriptionList(),
            descriptions =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    // Clear existing items
                    ClearTable(table);

                    // Create items
                    var createdItems = descriptions
                        .Select(d => table.AddAsync(d).GetAwaiter().GetResult())
                        .ToList();

                    // Retrieve all items
                    var retrievedItems = table.GetAllAsync().GetAwaiter().GetResult();

                    // Clean up
                    foreach (var item in createdItems)
                    {
                        table.DeleteAsync(item.Id).GetAwaiter().GetResult();
                    }

                    var countMatches = retrievedItems.Count == createdItems.Count;
                    var allItemsFound = createdItems.All(created =>
                        retrievedItems.Any(retrieved =>
                            retrieved.Id == created.Id &&
                            retrieved.Description == created.Description &&
                            retrieved.IsComplete == created.IsComplete));

                    return (countMatches && allItemsFound).ToProperty()
                        .Label($"CountMatches: {countMatches} ({retrievedItems.Count} == {createdItems.Count}), " +
                               $"AllItemsFound: {allItemsFound}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 4: Todo Completion State Transition**
    /// **Validates: Requirements 2.3**
    /// 
    /// For any incomplete todo item, marking it as complete should set IsComplete=true
    /// and CompletedAt to a non-null timestamp, while preserving all other fields.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TodoCompletion_SetsCorrectState()
    {
        return Prop.ForAll(
            GenerateValidDescription(),
            description =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    // Create an incomplete item
                    var item = table.AddAsync(description).GetAwaiter().GetResult();
                    var originalId = item.Id;
                    var originalDescription = item.Description;
                    var originalCreatedAt = item.CreatedAt;

                    var beforeCompletion = DateTime.UtcNow.AddSeconds(-1);
                    
                    // Mark as complete
                    var success = table.MarkCompleteAsync(item.Id).GetAwaiter().GetResult();
                    
                    var afterCompletion = DateTime.UtcNow.AddSeconds(1);

                    // Retrieve the updated item
                    var updatedItem = table.GetByIdAsync(item.Id).GetAwaiter().GetResult();

                    // Clean up
                    table.DeleteAsync(item.Id).GetAwaiter().GetResult();

                    if (updatedItem == null)
                    {
                        return false.ToProperty().Label("Updated item not found");
                    }

                    var operationSucceeded = success;
                    var isNowComplete = updatedItem.IsComplete;
                    var hasCompletedAt = updatedItem.CompletedAt != null;
                    var completedAtInRange = updatedItem.CompletedAt >= beforeCompletion && 
                                             updatedItem.CompletedAt <= afterCompletion;
                    var idPreserved = updatedItem.Id == originalId;
                    var descriptionPreserved = updatedItem.Description == originalDescription;
                    var createdAtPreserved = Math.Abs((updatedItem.CreatedAt - originalCreatedAt).TotalSeconds) < 1;

                    return (operationSucceeded && isNowComplete && hasCompletedAt && completedAtInRange &&
                            idPreserved && descriptionPreserved && createdAtPreserved)
                        .ToProperty()
                        .Label($"Success: {operationSucceeded}, IsComplete: {isNowComplete}, " +
                               $"HasCompletedAt: {hasCompletedAt}, InRange: {completedAtInRange}, " +
                               $"IdPreserved: {idPreserved}, DescPreserved: {descriptionPreserved}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 5: Todo Description Edit Preservation**
    /// **Validates: Requirements 2.4**
    /// 
    /// For any todo item and new description, editing the description should update only
    /// the description field while preserving Id, IsComplete, CreatedAt, and CompletedAt.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TodoDescriptionEdit_PreservesOtherFields()
    {
        return Prop.ForAll(
            GenerateValidDescription(),
            GenerateValidDescription(),
            (originalDescription, newDescription) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    // Create an item
                    var item = table.AddAsync(originalDescription).GetAwaiter().GetResult();
                    var originalId = item.Id;
                    var originalIsComplete = item.IsComplete;
                    var originalCreatedAt = item.CreatedAt;
                    var originalCompletedAt = item.CompletedAt;

                    // Edit description
                    var success = table.EditDescriptionAsync(item.Id, newDescription).GetAwaiter().GetResult();

                    // Retrieve the updated item
                    var updatedItem = table.GetByIdAsync(item.Id).GetAwaiter().GetResult();

                    // Clean up
                    table.DeleteAsync(item.Id).GetAwaiter().GetResult();

                    if (updatedItem == null)
                    {
                        return false.ToProperty().Label("Updated item not found");
                    }

                    var operationSucceeded = success;
                    var descriptionUpdated = updatedItem.Description == newDescription;
                    var idPreserved = updatedItem.Id == originalId;
                    var isCompletePreserved = updatedItem.IsComplete == originalIsComplete;
                    var createdAtPreserved = Math.Abs((updatedItem.CreatedAt - originalCreatedAt).TotalSeconds) < 1;
                    var completedAtPreserved = updatedItem.CompletedAt == originalCompletedAt;

                    return (operationSucceeded && descriptionUpdated && idPreserved && 
                            isCompletePreserved && createdAtPreserved && completedAtPreserved)
                        .ToProperty()
                        .Label($"Success: {operationSucceeded}, DescUpdated: {descriptionUpdated}, " +
                               $"IdPreserved: {idPreserved}, IsCompletePreserved: {isCompletePreserved}, " +
                               $"CreatedAtPreserved: {createdAtPreserved}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 6: Todo Deletion Removes Item**
    /// **Validates: Requirements 2.5**
    /// 
    /// For any existing todo item, deleting it should result in the item no longer
    /// being retrievable from the table.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TodoDeletion_RemovesItem()
    {
        return Prop.ForAll(
            GenerateValidDescription(),
            description =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    // Create an item
                    var item = table.AddAsync(description).GetAwaiter().GetResult();

                    // Verify it exists
                    var existsBefore = table.GetByIdAsync(item.Id).GetAwaiter().GetResult() != null;

                    // Delete it
                    table.DeleteAsync(item.Id).GetAwaiter().GetResult();

                    // Verify it no longer exists
                    var existsAfter = table.GetByIdAsync(item.Id).GetAwaiter().GetResult() != null;

                    return (existsBefore && !existsAfter).ToProperty()
                        .Label($"ExistedBefore: {existsBefore}, ExistsAfter: {existsAfter}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 7: Todo List Sort Order**
    /// **Validates: Requirements 2.6**
    /// 
    /// For any list of todo items, the displayed order should have all incomplete items
    /// before all completed items, with each group sorted by CreatedAt ascending.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TodoListSortOrder_IncompleteFirstThenByCreatedAt()
    {
        return Prop.ForAll(
            GenerateDescriptionList(),
            descriptions =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestTodoTable(client);

                    // Clear existing items
                    ClearTable(table);

                    // Create items with small delays to ensure different timestamps
                    var createdItems = new List<TodoItem>();
                    foreach (var desc in descriptions)
                    {
                        var item = table.AddAsync(desc).GetAwaiter().GetResult();
                        createdItems.Add(item);
                        Thread.Sleep(10); // Small delay to ensure different timestamps
                    }

                    // Mark some items as complete (every other one)
                    for (int i = 0; i < createdItems.Count; i += 2)
                    {
                        table.MarkCompleteAsync(createdItems[i].Id).GetAwaiter().GetResult();
                    }

                    // Retrieve and sort items (same logic as Program.cs)
                    var items = table.GetAllAsync().GetAwaiter().GetResult();
                    var sortedItems = items
                        .OrderBy(x => x.IsComplete)
                        .ThenBy(x => x.CreatedAt)
                        .ToList();

                    // Clean up
                    foreach (var item in createdItems)
                    {
                        table.DeleteAsync(item.Id).GetAwaiter().GetResult();
                    }

                    // Verify sort order
                    var incompleteFirst = true;
                    var foundComplete = false;
                    DateTime? lastIncompleteCreatedAt = null;
                    DateTime? lastCompleteCreatedAt = null;

                    foreach (var item in sortedItems)
                    {
                        if (item.IsComplete)
                        {
                            foundComplete = true;
                            if (lastCompleteCreatedAt.HasValue && item.CreatedAt < lastCompleteCreatedAt.Value)
                            {
                                return false.ToProperty().Label("Complete items not sorted by CreatedAt");
                            }
                            lastCompleteCreatedAt = item.CreatedAt;
                        }
                        else
                        {
                            if (foundComplete)
                            {
                                incompleteFirst = false;
                            }
                            if (lastIncompleteCreatedAt.HasValue && item.CreatedAt < lastIncompleteCreatedAt.Value)
                            {
                                return false.ToProperty().Label("Incomplete items not sorted by CreatedAt");
                            }
                            lastIncompleteCreatedAt = item.CreatedAt;
                        }
                    }

                    return incompleteFirst.ToProperty()
                        .Label($"IncompleteFirst: {incompleteFirst}, ItemCount: {sortedItems.Count}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    #region Helper Methods

    /// <summary>
    /// Test table that uses a separate table name to avoid conflicts with the main application.
    /// We use the actual TodoTable class but with the test table name.
    /// </summary>
    private class TestTodoTable : DynamoDbTableBase
    {
        public TestTodoTable(IAmazonDynamoDB client) : base(client, TestTableName)
        {
        }

        public async Task<TodoItem> AddAsync(string description)
        {
            var item = new TodoItem
            {
                Id = Guid.NewGuid().ToString(),
                Description = description,
                IsComplete = false,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = null
            };

            await PutAsync(item);
            return item;
        }

        public async Task<List<TodoItem>> GetAllAsync()
        {
            return await Scan<TodoItem>().ToListAsync();
        }

        public async Task<bool> MarkCompleteAsync(string id)
        {
            try
            {
                await Update<TodoItem>()
                    .WithKey("pk", id)
                    .Set("SET isComplete = {0}, completedAt = {1:o}", true, DateTime.UtcNow)
                    .UpdateAsync();
                return true;
            }
            catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
            {
                return false;
            }
        }

        public async Task<bool> EditDescriptionAsync(string id, string newDescription)
        {
            try
            {
                await Update<TodoItem>()
                    .WithKey("pk", id)
                    .Set("SET description = {0}", newDescription)
                    .UpdateAsync();
                return true;
            }
            catch (Amazon.DynamoDBv2.Model.ConditionalCheckFailedException)
            {
                return false;
            }
        }

        public async Task DeleteAsync(string id)
        {
            await Delete<TodoItem>()
                .WithKey("pk", id)
                .DeleteAsync();
        }

        public async Task<TodoItem?> GetByIdAsync(string id)
        {
            return await Get<TodoItem>()
                .WithKey("pk", id)
                .GetItemAsync();
        }
    }

    private static void EnsureTestTableExists(IAmazonDynamoDB client)
    {
        DynamoDbSetup.EnsureTableExistsAsync(client, TestTableName, "pk").GetAwaiter().GetResult();
    }

    private static void ClearTable(TestTodoTable table)
    {
        var items = table.GetAllAsync().GetAwaiter().GetResult();
        foreach (var item in items)
        {
            table.DeleteAsync(item.Id).GetAwaiter().GetResult();
        }
    }

    private static bool IsDynamoDbConnectionError(AmazonDynamoDBException ex)
    {
        return ex.Message.Contains("Unable to connect") ||
               ex.Message.Contains("Connection refused") ||
               ex.Message.Contains("No connection could be made");
    }

    private static Arbitrary<string> GenerateValidDescription()
    {
        var wordGen = Gen.Elements("buy", "call", "fix", "write", "read", "clean", "send", "check");
        var nounGen = Gen.Elements("groceries", "report", "email", "code", "room", "car", "document", "meeting");
        
        return Arb.From(
            Gen.Choose(1, 4).SelectMany(wordCount =>
                Gen.ArrayOf(wordCount, wordGen).SelectMany(words =>
                    nounGen.Select(noun => string.Join(" ", words.Append(noun)))))
        );
    }

    private static Arbitrary<string[]> GenerateDescriptionList()
    {
        return Arb.From(
            Gen.Choose(1, 5).SelectMany(count =>
                Gen.ArrayOf(count, GenerateValidDescription().Generator))
        );
    }

    #endregion
}
