using Domolov.Domain.Common;

namespace Domolov.Domain.Scans;

/// <summary>Kind of diagnostic captured during a ScanRun.</summary>
public enum ScanArtifactKind
{
    Screenshot = 0,
    Html = 1,
    Headers = 2,
}

/// <summary>A diagnostic capture (screenshot, HTML, response headers) from a ScanRun.</summary>
public sealed class ScanRunArtifact
{
    public const int MaxContentBytes = 5 * 1024 * 1024;

    private ScanRunArtifact()
    {
        Label = "";
        ContentType = "";
        Content = [];
    }

    public ScanRunArtifact(
        Guid scanRunId,
        ScanArtifactKind kind,
        string label,
        string contentType,
        byte[] content,
        DateTimeOffset now
    )
    {
        Id = Ids.New();
        ScanRunId = scanRunId;
        Kind = kind;
        Label = label.Length <= 100 ? label : label[..100];
        ContentType = contentType;
        Content = content.Length <= MaxContentBytes ? content : content[..MaxContentBytes];
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ScanRunId { get; private set; }
    public ScanArtifactKind Kind { get; private set; }
    public string Label { get; private set; }
    public string ContentType { get; private set; }
    public byte[] Content { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
