using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Templates;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Notify.Tests;

/// <summary>
/// Tests for <see cref="FileTemplateRenderer"/> covering rendering, path traversal, and caching.
/// </summary>
public sealed class FileTemplateRendererTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileTemplateRenderer _renderer;
    private readonly FakeTimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTemplateRendererTests"/> class.
    /// </summary>
    public FileTemplateRendererTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "hd-notify-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var options = Microsoft.Extensions.Options.Options.Create(new TemplateOptions
        {
            RootPath = _tempRoot,
            Extension = ".txt",
            CacheEnabled = true,
            CacheTtl = TimeSpan.FromMinutes(5),
        });

        _renderer = new FileTemplateRenderer(
            options,
            _timeProvider,
            NullLogger<FileTemplateRenderer>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    /// <summary>
    /// Verifies that templates render with token replacement.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Renders_template_with_token_replacement()
    {
        WriteTemplate("greeting", "Hello {{Name}}, welcome to {{Service}}!");

        var model = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Service"] = "HoneyDrunk",
        };

        var result = await _renderer.RenderAsync(new TemplateKey("greeting"), model);

        result.Should().Be("Hello Alice, welcome to HoneyDrunk!");
    }

    /// <summary>
    /// Verifies that missing tokens are preserved in output.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Missing_tokens_are_preserved()
    {
        WriteTemplate("partial", "Hi {{Name}}, code is {{Code}}.");

        var model = new Dictionary<string, object?> { ["Name"] = "Bob" };

        var result = await _renderer.RenderAsync(new TemplateKey("partial"), model);

        result.Should().Be("Hi Bob, code is {{Code}}.");
    }

    /// <summary>
    /// Verifies that a missing template throws FileNotFoundException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Throws_when_template_file_not_found()
    {
        var model = new Dictionary<string, object?>();

        var act = () => _renderer.RenderAsync(new TemplateKey("nonexistent"), model);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*nonexistent*");
    }

    /// <summary>
    /// Verifies that dot-dot path traversal is blocked.
    /// </summary>
    [Fact]
    public void Path_traversal_with_dot_dot_is_blocked()
    {
        var model = new Dictionary<string, object?>();

        var act = () => _renderer.RenderAsync(new TemplateKey("../secrets"), model);

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*traversal*");
    }

    /// <summary>
    /// Verifies that absolute path traversal is blocked.
    /// </summary>
    [Fact]
    public void Path_traversal_with_absolute_path_is_blocked()
    {
        var model = new Dictionary<string, object?>();
        var absoluteKey = RuntimeInformation_IsWindows() ? "C:\\Windows\\system32\\config" : "/etc/passwd";

        var act = () => _renderer.RenderAsync(new TemplateKey(absoluteKey), model);

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*traversal*");
    }

    /// <summary>
    /// Verifies that cached templates are reused within TTL.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Cached_template_is_reused_within_ttl()
    {
        WriteTemplate("cached", "Version 1: {{Name}}");

        var model = new Dictionary<string, object?> { ["Name"] = "Test" };

        var result1 = await _renderer.RenderAsync(new TemplateKey("cached"), model);
        result1.Should().Be("Version 1: Test");

        WriteTemplate("cached", "Version 2: {{Name}}");

        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        var result2 = await _renderer.RenderAsync(new TemplateKey("cached"), model);
        result2.Should().Be("Version 1: Test", "cached version should still be served");
    }

    /// <summary>
    /// Verifies that cache expires after TTL.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Cache_expires_after_ttl()
    {
        WriteTemplate("expiring", "V1: {{Name}}");

        var model = new Dictionary<string, object?> { ["Name"] = "Test" };

        var result1 = await _renderer.RenderAsync(new TemplateKey("expiring"), model);
        result1.Should().Be("V1: Test");

        WriteTemplate("expiring", "V2: {{Name}}");

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result2 = await _renderer.RenderAsync(new TemplateKey("expiring"), model);
        result2.Should().Be("V2: Test", "cache should have expired");
    }

    /// <summary>
    /// Verifies that file extension is appended automatically.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Extension_is_appended_automatically()
    {
        WriteTemplate("auto-ext", "Content: {{Value}}");

        var model = new Dictionary<string, object?> { ["Value"] = "42" };

        var result = await _renderer.RenderAsync(new TemplateKey("auto-ext"), model);
        result.Should().Be("Content: 42");
    }

    /// <summary>
    /// Verifies that subdirectory templates are resolved.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task Subdirectory_templates_are_resolved()
    {
        var subDir = Path.Combine(_tempRoot, "emails");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "welcome.txt"), "Welcome {{Name}}");

        var model = new Dictionary<string, object?> { ["Name"] = "Eve" };

        var result = await _renderer.RenderAsync(new TemplateKey("emails/welcome"), model);
        result.Should().Be("Welcome Eve");
    }

    private static bool RuntimeInformation_IsWindows() =>
        OperatingSystem.IsWindows();

    private void WriteTemplate(string name, string content)
    {
        File.WriteAllText(Path.Combine(_tempRoot, name + ".txt"), content);
    }

    /// <summary>
    /// Minimal fake TimeProvider for cache TTL testing.
    /// </summary>
    private sealed class FakeTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _utcNow = initial;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
