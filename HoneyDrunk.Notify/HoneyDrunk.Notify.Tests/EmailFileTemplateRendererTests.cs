using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Templates;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Notify.Tests;

/// <summary>
/// Tests for <see cref="EmailFileTemplateRenderer"/> covering subject/body rendering,
/// HTML detection, token replacement, caching, and path traversal.
/// </summary>
public sealed class EmailFileTemplateRendererTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly EmailFileTemplateRenderer _renderer;
    private readonly FakeTimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailFileTemplateRendererTests"/> class.
    /// </summary>
    public EmailFileTemplateRendererTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "hd-notify-email-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var options = Microsoft.Extensions.Options.Options.Create(new TemplateOptions
        {
            RootPath = _tempRoot,
            CacheEnabled = true,
            CacheTtl = TimeSpan.FromMinutes(5),
        });

        _renderer = new EmailFileTemplateRenderer(
            options,
            _timeProvider,
            NullLogger<EmailFileTemplateRenderer>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    /// <summary>
    /// Verifies that plain text body rendering returns subject and body with IsHtml false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_PlainTextBody_ReturnsSubjectAndBodyWithIsHtmlFalse()
    {
        WriteFile("welcome.subject.txt", "Welcome {{Name}}!");
        WriteFile("welcome.body.txt", "Hello {{Name}}, you joined {{Service}}.");

        var model = new Dictionary<string, object?>
        {
            ["Name"] = "Alice",
            ["Service"] = "HoneyDrunk",
        };

        var result = await _renderer.RenderEmailAsync(new TemplateKey("welcome"), model);

        result.Subject.Should().Be("Welcome Alice!");
        result.Body.Should().Be("Hello Alice, you joined HoneyDrunk.");
        result.IsHtml.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that HTML body is preferred over TXT when both exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_HtmlBodyExists_PrefersHtmlOverTxt()
    {
        WriteFile("newsletter.subject.txt", "{{Title}} Newsletter");
        WriteFile("newsletter.body.html", "<h1>{{Title}}</h1><p>{{Content}}</p>");
        WriteFile("newsletter.body.txt", "Fallback plain text");

        var model = new Dictionary<string, object?>
        {
            ["Title"] = "Weekly",
            ["Content"] = "Updates here",
        };

        var result = await _renderer.RenderEmailAsync(new TemplateKey("newsletter"), model);

        result.Subject.Should().Be("Weekly Newsletter");
        result.Body.Should().Be("<h1>Weekly</h1><p>Updates here</p>");
        result.IsHtml.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that unmatched tokens are preserved in output.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_MissingTokens_ArePreservedInOutput()
    {
        WriteFile("partial.subject.txt", "Hello {{Name}}");
        WriteFile("partial.body.txt", "Code: {{MissingToken}}");

        var model = new Dictionary<string, object?> { ["Name"] = "Bob" };

        var result = await _renderer.RenderEmailAsync(new TemplateKey("partial"), model);

        result.Subject.Should().Be("Hello Bob");
        result.Body.Should().Be("Code: {{MissingToken}}");
    }

    /// <summary>
    /// Verifies that a missing subject file throws FileNotFoundException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_MissingSubjectFile_ThrowsFileNotFoundException()
    {
        WriteFile("no-subject.body.txt", "Body content");

        var model = new Dictionary<string, object?>();

        var act = () => _renderer.RenderEmailAsync(new TemplateKey("no-subject"), model);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*no-subject*subject*");
    }

    /// <summary>
    /// Verifies that a missing body file throws FileNotFoundException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_MissingBodyFile_ThrowsFileNotFoundException()
    {
        WriteFile("no-body.subject.txt", "Subject line");

        var model = new Dictionary<string, object?>();

        var act = () => _renderer.RenderEmailAsync(new TemplateKey("no-body"), model);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*no-body*body*");
    }

    /// <summary>
    /// Verifies that path traversal is blocked.
    /// </summary>
    [Fact]
    public void RenderEmailAsync_PathTraversal_IsBlocked()
    {
        var model = new Dictionary<string, object?>();

        var act = () => _renderer.RenderEmailAsync(new TemplateKey("../secrets"), model);

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*traversal*");
    }

    /// <summary>
    /// Verifies that cached templates are reused within the TTL.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_CachedTemplate_ReusedWithinTtl()
    {
        WriteFile("cached.subject.txt", "Subject V1");
        WriteFile("cached.body.txt", "Body V1");

        var model = new Dictionary<string, object?>();

        var result1 = await _renderer.RenderEmailAsync(new TemplateKey("cached"), model);
        result1.Subject.Should().Be("Subject V1");
        result1.Body.Should().Be("Body V1");

        WriteFile("cached.subject.txt", "Subject V2");
        WriteFile("cached.body.txt", "Body V2");
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        var result2 = await _renderer.RenderEmailAsync(new TemplateKey("cached"), model);
        result2.Subject.Should().Be("Subject V1");
        result2.Body.Should().Be("Body V1");
    }

    /// <summary>
    /// Verifies that expired cache entries are reloaded from disk.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_CacheExpired_ReloadsFromDisk()
    {
        WriteFile("expiring.subject.txt", "S1");
        WriteFile("expiring.body.txt", "B1");

        var model = new Dictionary<string, object?>();

        var result1 = await _renderer.RenderEmailAsync(new TemplateKey("expiring"), model);
        result1.Subject.Should().Be("S1");

        WriteFile("expiring.subject.txt", "S2");
        WriteFile("expiring.body.txt", "B2");
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result2 = await _renderer.RenderEmailAsync(new TemplateKey("expiring"), model);
        result2.Subject.Should().Be("S2");
        result2.Body.Should().Be("B2");
    }

    /// <summary>
    /// Verifies that subdirectory templates are resolved correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task RenderEmailAsync_SubdirectoryTemplate_IsResolved()
    {
        var subDir = Path.Combine(_tempRoot, "account");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "verify.subject.txt"), "Verify {{Email}}");
        File.WriteAllText(Path.Combine(subDir, "verify.body.txt"), "Click here to verify {{Email}}");

        var model = new Dictionary<string, object?> { ["Email"] = "user@test.com" };

        var result = await _renderer.RenderEmailAsync(new TemplateKey("account/verify"), model);

        result.Subject.Should().Be("Verify user@test.com");
        result.Body.Should().Contain("Click here to verify user@test.com");
    }

    private void WriteFile(string name, string content)
    {
        File.WriteAllText(Path.Combine(_tempRoot, name), content);
    }

    private sealed class FakeTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _utcNow = initial;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
