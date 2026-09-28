using Domolov.Application.Abstractions;
using Domolov.Application.Common;
using Domolov.Domain.Scans;
using Microsoft.EntityFrameworkCore;

namespace Domolov.Application.Scans;

/// <summary>Filters for listing ScanRuns.</summary>
public sealed record ScanRunQuery(
    Guid? WatchId,
    ScanRunStatus? Status,
    bool? Active,
    int? Page,
    int? PageSize
);

/// <summary>Lists ScanRuns, newest first.</summary>
public sealed class ListScanRunsHandler(IAppDbContext db)
{
    public async Task<PagedResponse<ScanRunResponse>> HandleAsync(
        ScanRunQuery query,
        CancellationToken cancellationToken
    )
    {
        var (page, size, skip) = Paging.Normalize(query.Page, query.PageSize);
        var runs = db.ScanRuns.AsNoTracking();
        if (query.WatchId is Guid watchId)
        {
            runs = runs.Where(r => r.WatchId == watchId);
        }

        if (query.Status is ScanRunStatus status)
        {
            runs = runs.Where(r => r.Status == status);
        }

        if (query.Active is true)
        {
            runs = runs.Where(r =>
                r.Status == ScanRunStatus.Queued || r.Status == ScanRunStatus.Running
            );
        }

        var total = await runs.CountAsync(cancellationToken);
        var items = await ScanRunProjection
            .Project(db, runs.OrderByDescending(r => r.QueuedAt).Skip(skip).Take(size))
            .ToListAsync(cancellationToken);
        return new PagedResponse<ScanRunResponse>(items, total, page, size);
    }
}

/// <summary>Gets one ScanRun.</summary>
public sealed class GetScanRunHandler(IAppDbContext db)
{
    public async Task<ScanRunResponse> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        await ScanRunProjection
            .Project(db, db.ScanRuns.AsNoTracking().Where(r => r.Id == id))
            .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("ScanRun", id);
}

/// <summary>Lists diagnostic artifacts of a ScanRun.</summary>
public sealed class ListScanArtifactsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ScanArtifactResponse>> HandleAsync(
        Guid scanRunId,
        CancellationToken cancellationToken
    )
    {
        if (!await db.ScanRuns.AnyAsync(r => r.Id == scanRunId, cancellationToken))
        {
            throw new NotFoundException("ScanRun", scanRunId);
        }

        return await db
            .ScanRunArtifacts.AsNoTracking()
            .Where(a => a.ScanRunId == scanRunId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ScanArtifactResponse(
                a.Id,
                a.ScanRunId,
                a.Kind,
                a.Label,
                a.ContentType,
                a.Content.Length,
                a.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Returns an artifact's bytes.</summary>
public sealed class GetScanArtifactHandler(IAppDbContext db)
{
    public async Task<ScanArtifactContent> HandleAsync(
        Guid scanRunId,
        Guid artifactId,
        CancellationToken cancellationToken
    )
    {
        var artifact =
            await db
                .ScanRunArtifacts.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.Id == artifactId && a.ScanRunId == scanRunId,
                    cancellationToken
                ) ?? throw new NotFoundException("ScanRunArtifact", artifactId);
        var extension = artifact.Kind switch
        {
            ScanArtifactKind.Screenshot => "png",
            ScanArtifactKind.Html => "html",
            _ => "txt",
        };
        return new ScanArtifactContent(
            artifact.ContentType,
            artifact.Content,
            $"scan-{scanRunId:N}-{artifact.Label}.{extension}"
        );
    }
}

/// <summary>Shared SQL projection of ScanRuns with Watch name and artifact count.</summary>
public static class ScanRunProjection
{
    public static IQueryable<ScanRunResponse> Project(IAppDbContext db, IQueryable<ScanRun> runs) =>
        runs.Select(r => new ScanRunResponse(
            r.Id,
            r.WatchId,
            r.Watch == null ? "" : r.Watch.Name,
            r.Status,
            r.IsManual,
            r.QueuedAt,
            r.StartedAt,
            r.FinishedAt,
            r.PagesScanned,
            r.NewCount,
            r.PriceChangeCount,
            r.RepostCount,
            r.ErrorSummary,
            r.NotifyErrorSummary,
            r.CloudflareBlocked,
            r.CrawlReachedEnd,
            db.ScanRunArtifacts.Count(a => a.ScanRunId == r.Id)
        ));
}
