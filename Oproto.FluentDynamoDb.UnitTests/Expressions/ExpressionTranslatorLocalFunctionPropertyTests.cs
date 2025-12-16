using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for method/function evaluation in ExpressionTranslator.
/// **Feature: v1-rough-edges, Property 8: Local Function Evaluation**
/// **Validates: Requirements 6.1, 6.4**
/// </summary>
public class ExpressionTranslatorLocalFunctionPropertyTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private class ValueHolder
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public bool BoolValue { get; set; }
        public int GetIntValue() => IntValue;
        public string GetStringValue() => StringValue;
        public bool GetBoolValue() => BoolValue;
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(attributeValues, attributeNames, null, ExpressionValidationMode.None);
    }

    [Property(MaxTest = 100)]
    public Property MethodOnCapturedObject_ReturningInt_ShouldBeEvaluatedAtTranslationTime()
    {
        var valueGen = Arb.Default.Int32().Generator.Where(i => i >= 0 && i <= 1000).ToArbitrary();
        return Prop.ForAll(valueGen, expectedValue =>
        {
            var translator = CreateTranslator();
            var context = CreateContext();
            var holder = new ValueHolder { IntValue = expectedValue };
            Expression<Func<TestEntity, bool>> expression = x => x.Age > holder.GetIntValue();
            var result = translator.Translate(expression, context);
            var ok = result.Contains("#attr0 > :p0") &&
                     context.AttributeNames.AttributeNames.Count == 1 &&
                     context.AttributeValues.AttributeValues.Count == 1 &&
                     context.AttributeNames.AttributeNames.Values.Contains("Age") &&
                     context.AttributeValues.AttributeValues.Values.Any(v => v.N == expectedValue.ToString());
            return ok.ToProperty().Label("ExpectedValue: " + expectedValue + ", Result: " + result);
        });
    }

    [Property(MaxTest = 100)]
    public Property StaticMethodCall_ShouldBeEvaluatedAtTranslationTime()
    {
        var value1Gen = Arb.Default.Int32().Generator.Where(i => i >= 0 && i <= 500).ToArbitrary();
        var value2Gen = Arb.Default.Int32().Generator.Where(i => i >= 0 && i <= 500).ToArbitrary();
        return Prop.ForAll(value1Gen, value2Gen, (value1, value2) =>
        {
            var translator = CreateTranslator();
            var context = CreateContext();
            var expectedValue = Math.Max(value1, value2);
            Expression<Func<TestEntity, bool>> expression = x => x.Age > Math.Max(value1, value2);
            var result = translator.Translate(expression, context);
            var ok = result.Contains("#attr0 > :p0") &&
                     context.AttributeNames.AttributeNames.Count == 1 &&
                     context.AttributeValues.AttributeValues.Count == 1 &&
                     context.AttributeNames.AttributeNames.Values.Contains("Age") &&
                     context.AttributeValues.AttributeValues.Values.Any(v => v.N == expectedValue.ToString());
            return ok.ToProperty().Label("Value1: " + value1 + ", Value2: " + value2 + ", ExpectedValue: " + expectedValue);
        });
    }

    [Property(MaxTest = 100)]
    public Property MethodOnCapturedObject_InConditionalTest_ShouldBeEvaluatedAtTranslationTime()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator.Where(s => !string.IsNullOrWhiteSpace(s.Get)).Select(s => s.Get).ToArbitrary();
        return Prop.ForAll(flagGen, valueGen, (flagValue, testValue) =>
        {
            var translator = CreateTranslator();
            var context = CreateContext();
            var holder = new ValueHolder { BoolValue = flagValue };
            Expression<Func<TestEntity, bool>> expression = x => holder.GetBoolValue() ? x.Name == testValue : true;
            var result = translator.Translate(expression, context);
            if (flagValue)
            {
                var ok = result.Contains("#attr0 = :p0") &&
                         context.AttributeNames.AttributeNames.Count == 1 &&
                         context.AttributeValues.AttributeValues.Count == 1 &&
                         context.AttributeNames.AttributeNames.Values.Contains("Name") &&
                         context.AttributeValues.AttributeValues.Values.Any(v => v.S == testValue);
                return ok.ToProperty().Label("Flag true: TestValue: " + testValue + ", Result: " + result);
            }
            else
            {
                var ok = string.IsNullOrEmpty(result) &&
                         context.AttributeNames.AttributeNames.Count == 0 &&
                         context.AttributeValues.AttributeValues.Count == 0;
                return ok.ToProperty().Label("Flag false: TestValue: " + testValue + ", Result: " + result);
            }
        });
    }
}
