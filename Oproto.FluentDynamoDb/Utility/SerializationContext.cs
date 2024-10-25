using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Utility;

[JsonSerializable(typeof(AttributeValue))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<AttributeValue>))]
[JsonSerializable(typeof(Dictionary<string,AttributeValue>))]
internal partial class SerializationContext : JsonSerializerContext
{
    
}