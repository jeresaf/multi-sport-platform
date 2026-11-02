using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>A parameter's type, as the files write it (<c>int</c>, <c>bool</c>, <c>enum</c>, <c>list</c>).</summary>
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

    /// <summary>
    /// An ordered list of distinct codes, each one of the parameter's <c>values</c> (a ruleset template's tiebreakers;
    /// formats have none).
    /// </summary>
    [JsonStringEnumMemberName("list")]
    List,
}
