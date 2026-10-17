using AwesomeAssertions;
using HoneyDrunk.Notify.Templates;

namespace HoneyDrunk.Notify.Tests;

/// <summary>
/// Tests for <see cref="SimpleTokenReplacer"/>.
/// </summary>
public sealed class SimpleTokenReplacerTests
{
    /// <summary>
    /// Verifies that a single token is replaced.
    /// </summary>
    [Fact]
    public void Replaces_single_token()
    {
        var result = SimpleTokenReplacer.Replace(
            "Hello {{Name}}!",
            new Dictionary<string, string> { ["Name"] = "Alice" });

        result.Should().Be("Hello Alice!");
    }

    /// <summary>
    /// Verifies that multiple tokens are replaced.
    /// </summary>
    [Fact]
    public void Replaces_multiple_tokens()
    {
        var template = "{{Greeting}}, {{Name}}. Welcome to {{Service}}.";
        var values = new Dictionary<string, string>
        {
            ["Greeting"] = "Hi",
            ["Name"] = "Bob",
            ["Service"] = "HoneyDrunk",
        };

        SimpleTokenReplacer.Replace(template, values).Should().Be("Hi, Bob. Welcome to HoneyDrunk.");
    }

    /// <summary>
    /// Verifies that missing tokens are left unchanged.
    /// </summary>
    [Fact]
    public void Missing_tokens_are_left_unchanged()
    {
        var result = SimpleTokenReplacer.Replace(
            "Hello {{Name}}, your code is {{Code}}.",
            new Dictionary<string, string> { ["Name"] = "Carol" });

        result.Should().Be("Hello Carol, your code is {{Code}}.");
    }

    /// <summary>
    /// Verifies that templates without tokens are returned unchanged.
    /// </summary>
    [Fact]
    public void No_tokens_returns_template_unchanged()
    {
        var template = "No tokens here.";
        SimpleTokenReplacer.Replace(template, new Dictionary<string, string>()).Should().Be(template);
    }

    /// <summary>
    /// Verifies that tokens with underscores and digits are replaced.
    /// </summary>
    [Fact]
    public void Tokens_with_underscores_and_digits_are_replaced()
    {
        var result = SimpleTokenReplacer.Replace(
            "{{item_1}} and {{item_2}}",
            new Dictionary<string, string> { ["item_1"] = "A", ["item_2"] = "B" });

        result.Should().Be("A and B");
    }

    /// <summary>
    /// Verifies that dot-separated tokens (matching the keys produced by <see cref="TemplateModelFlattener"/>
    /// for nested dictionaries) are replaced.
    /// </summary>
    [Fact]
    public void Dot_separated_tokens_for_nested_keys_are_replaced()
    {
        var result = SimpleTokenReplacer.Replace(
            "City: {{Address.City}}, ZIP: {{Address.Postal.Code}}.",
            new Dictionary<string, string>
            {
                ["Address.City"] = "Vilnius",
                ["Address.Postal.Code"] = "01108",
            });

        result.Should().Be("City: Vilnius, ZIP: 01108.");
    }

    /// <summary>
    /// Verifies that invalid token syntax is not replaced.
    /// </summary>
    [Fact]
    public void Invalid_token_syntax_is_not_replaced()
    {
        var template = "{{invalid-token}} and {{ spaced }} stay.";
        SimpleTokenReplacer.Replace(template, new Dictionary<string, string>()).Should().Be(template);
    }

    /// <summary>
    /// Verifies that empty values replace tokens with empty string.
    /// </summary>
    [Fact]
    public void Empty_value_replaces_token_with_empty_string()
    {
        var result = SimpleTokenReplacer.Replace(
            "Value: {{Key}}.",
            new Dictionary<string, string> { ["Key"] = string.Empty });

        result.Should().Be("Value: .");
    }
}
