using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>A parameter's type, as the files write it (<c>int</c>, <c>bool</c>, <c>enum</c>).</summary>
public enum ParameterType
{
    /// <summary>A whole number within the parameter's <c>min</c> and <c>max</c>.</summary>
    [JsonStringEnumMemberName("int")]
    Number,

    [JsonStringEnumMemberName("bool")]
    Flag,

    /// <summary>One of the parameter's <c>values</c>.</summary>
    [JsonStringEnumMemberName("enum")]
    Choice,
}
