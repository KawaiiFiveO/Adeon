using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MinimalChessEngine
{
    // This attribute tells the source generator to create serialization logic for the specified types.
    [JsonSerializable(typeof(Dictionary<string, PlayStyle>))]
    [JsonSerializable(typeof(Dictionary<string, List<BookMove>>))]
    // Add any other top-level types you need to serialize/deserialize here.
    internal partial class JsonContext : JsonSerializerContext
    {
        // The C# compiler and source generator will automatically generate the implementation
        // for the abstract members of JsonSerializerContext based on the types listed in the
        // [JsonSerializable] attributes above.
        //
        // This single line is all that's needed in modern .NET.
        // The parameterless constructor is gone, and we don't need to manually override anything.
    }
}