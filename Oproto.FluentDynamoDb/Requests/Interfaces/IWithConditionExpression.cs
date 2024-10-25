namespace Oproto.FluentDynamoDb.Requests.Interfaces;

public interface IWithConditionExpression<out TBuilder>
{
    public TBuilder Where(string conditionExpression);
}