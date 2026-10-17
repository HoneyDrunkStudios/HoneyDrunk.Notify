// <copyright file="CommandLineParserTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Test methods are self-documenting via [Fact] + method name.
#pragma warning disable SA1515 // Inline test comments allowed.

using HoneyDrunk.Notify.Tools;

namespace HoneyDrunk.Notify.Tests.Tools;

/// <summary>
/// Tests for <see cref="CommandLineParser"/> — covers the while-loop / ApplyFlag dispatch
/// that replaced the previous for-loop in 0.4.0 (Sonar S127).
/// </summary>
public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_WithFewerThanTwoArgs_ReturnsNull()
    {
        Assert.Null(CommandLineParser.Parse([]));
        Assert.Null(CommandLineParser.Parse(["dlq"]));
    }

    [Fact]
    public void Parse_PreservesVerbAndSubVerb_LowerCased()
    {
        var parsed = CommandLineParser.Parse(["DLQ", "LIST"]);

        Assert.NotNull(parsed);
        Assert.Equal("dlq", parsed!.Verb);
        Assert.Equal("list", parsed.SubVerb);
        Assert.Null(parsed.TargetId);
    }

    [Fact]
    public void Parse_FullListInvocation_PopulatesAllFlags()
    {
        var parsed = CommandLineParser.Parse(
        [
            "dlq", "list",
            "--adapter", "AzureStorage",
            "--queue", "notify-main",
            "--dlq", "notify-dlq",
            "--connection", "UseDevelopmentStorage=true",
            "--take", "42",
        ]);

        Assert.NotNull(parsed);
        var opts = parsed!.Options;
        Assert.Equal("AzureStorage", opts.Adapter);
        Assert.Equal("notify-main", opts.QueueName);
        Assert.Equal("notify-dlq", opts.DeadLetterQueueName);
        Assert.Equal("UseDevelopmentStorage=true", opts.ConnectionString);
        Assert.Equal(42, opts.ListTake);
        Assert.False(opts.DryRun);
    }

    [Fact]
    public void Parse_PeekWithId_ExposesTargetId()
    {
        var parsed = CommandLineParser.Parse(
        [
            "dlq", "peek",
            "--id", "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            "--queue", "notify-main",
        ]);

        Assert.NotNull(parsed);
        Assert.Equal("peek", parsed!.SubVerb);
        Assert.Equal("01ARZ3NDEKTSV4RRFFQ69G5FAV", parsed.TargetId);
    }

    [Fact]
    public void Parse_DryRunFlag_IsBooleanAndConsumesOneSlot()
    {
        var parsed = CommandLineParser.Parse(
        [
            "dlq", "replay",
            "--id", "abc",
            "--queue", "main",
            "--dry-run",
        ]);

        Assert.NotNull(parsed);
        Assert.True(parsed!.Options.DryRun);
        Assert.Equal("abc", parsed.TargetId);
    }

    [Fact]
    public void Parse_TakeWithNonInteger_IgnoresValue_DoesNotConsumeExtraSlot()
    {
        // --take "not-a-number" must not assign ListTake and must not consume the
        // following arg as the value; subsequent flags should still parse.
        var parsed = CommandLineParser.Parse(
        [
            "dlq", "list",
            "--take", "not-a-number",
            "--queue", "main",
        ]);

        Assert.NotNull(parsed);
        Assert.NotEqual(0, parsed!.Options.ListTake); // default preserved (positive default)
        Assert.Equal("main", parsed.Options.QueueName);
    }

    [Fact]
    public void Parse_UnknownFlag_IsSkippedAndDoesNotBreakParser()
    {
        var parsed = CommandLineParser.Parse(
        [
            "dlq", "list",
            "--unknown-flag",
            "--queue", "main",
        ]);

        Assert.NotNull(parsed);
        Assert.Equal("main", parsed!.Options.QueueName);
    }

    [Fact]
    public void Parse_FlagWithoutValue_AtEndOfArgs_DoesNotThrow()
    {
        // --adapter requires a value; when it's the last token the parser must not crash.
        var parsed = CommandLineParser.Parse(["dlq", "list", "--adapter"]);

        Assert.NotNull(parsed);
        // Adapter stays at its default since no value followed.
        Assert.NotNull(parsed!.Options.Adapter);
    }
}
