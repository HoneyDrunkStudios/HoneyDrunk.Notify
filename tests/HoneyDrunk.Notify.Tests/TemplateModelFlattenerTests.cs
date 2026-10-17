using AwesomeAssertions;
using HoneyDrunk.Notify.Templates;

namespace HoneyDrunk.Notify.Tests;

/// <summary>
/// Tests for <see cref="TemplateModelFlattener"/>.
/// </summary>
public sealed class TemplateModelFlattenerTests
{
    /// <summary>
    /// Verifies that a null model produces an empty dictionary.
    /// </summary>
    [Fact]
    public void Null_model_returns_empty_dictionary()
    {
        TemplateModelFlattener.Flatten(null).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a flat model produces string values.
    /// </summary>
    [Fact]
    public void Flat_model_produces_string_values()
    {
        var model = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Age"] = 30,
        };

        var result = TemplateModelFlattener.Flatten(model);

        result.Should().ContainKey("Name").WhoseValue.Should().Be("Alice");
        result.Should().ContainKey("Age").WhoseValue.Should().Be("30");
    }

    /// <summary>
    /// Verifies that a null value becomes an empty string.
    /// </summary>
    [Fact]
    public void Null_value_becomes_empty_string()
    {
        var model = new Dictionary<string, object?> { ["Key"] = null };
        TemplateModelFlattener.Flatten(model).Should().ContainKey("Key").WhoseValue.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that nested dictionaries are flattened with dot-separated keys.
    /// </summary>
    [Fact]
    public void Nested_dictionary_is_dot_separated()
    {
        var model = new Dictionary<string, object?>
        {
            ["Address"] = new Dictionary<string, object?>
            {
                ["City"] = "Portland",
                ["State"] = "OR",
            },
        };

        var result = TemplateModelFlattener.Flatten(model);

        result.Should().ContainKey("Address.City").WhoseValue.Should().Be("Portland");
        result.Should().ContainKey("Address.State").WhoseValue.Should().Be("OR");
    }

    /// <summary>
    /// Verifies that key lookups are case insensitive.
    /// </summary>
    [Fact]
    public void Keys_are_case_insensitive()
    {
        var model = new Dictionary<string, object?> { ["name"] = "Bob" };
        var result = TemplateModelFlattener.Flatten(model);

        result.Should().ContainKey("NAME");
        result.Should().ContainKey("name");
    }
}
