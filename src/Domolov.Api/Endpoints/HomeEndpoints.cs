using Domolov.Application.Common;
using Domolov.Application.Homes;
using Domolov.Application.Listings;
using Domolov.Domain.Homes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Domolov.Api.Endpoints;

/// <summary>Homes (the feed), Bookmarks, matches and Listings.</summary>
public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this RouteGroupBuilder api)
    {
        var homes = api.MapGroup("/homes").WithTags("Homes");

        homes
            .MapGet(
                "",
                async Task<Ok<PagedResponse<HomeSummaryResponse>>> (
                    [AsParameters] HomeSearchQuery query,
                    SearchHomesHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(query, ct))
            )
            .WithName("SearchHomes")
            .WithSummary("The Homes feed: one card per Home with filters, sorting and paging");

        homes
            .MapGet(
                "/{id:guid}",
                async Task<Ok<HomeDetailResponse>> (
                    Guid id,
                    GetHomeHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, ct))
            )
            .WithName("GetHome")
            .WithSummary("A Home with every ad in its history, price history and possible matches")
            .ProducesProblem(StatusCodes.Status404NotFound);

        homes
            .MapPut(
                "/{id:guid}/bookmark",
                async Task<Ok<BookmarkResponse>> (
                    Guid id,
                    SetBookmarkRequest request,
                    SetBookmarkHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, request, ct))
            )
            .WithName("SetBookmark")
            .WithSummary("Bookmark a Home or change its stage and note")
            .ProducesValidationProblem();

        homes
            .MapDelete(
                "/{id:guid}/bookmark",
                async Task<NoContent> (Guid id, RemoveBookmarkHandler h, CancellationToken ct) =>
                {
                    await h.HandleAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("RemoveBookmark")
            .WithSummary("Remove a Home's Bookmark");

        homes
            .MapPut(
                "/{id:guid}/dismissal",
                async Task<NoContent> (Guid id, DismissHomeHandler h, CancellationToken ct) =>
                {
                    await h.HandleAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("DismissHome")
            .WithSummary(
                "Dismiss a Home: hidden from feeds and never notified (removes its Bookmark)"
            );

        homes
            .MapDelete(
                "/{id:guid}/dismissal",
                async Task<NoContent> (Guid id, RestoreHomeHandler h, CancellationToken ct) =>
                {
                    await h.HandleAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("RestoreHome")
            .WithSummary("Undo a dismissal");

        homes
            .MapPut(
                "/{id:guid}/seen",
                async Task<NoContent> (Guid id, MarkHomeSeenHandler h, CancellationToken ct) =>
                {
                    await h.HandleAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("MarkHomeSeen")
            .WithSummary("Mark a Home as seen");

        homes
            .MapPost(
                "/seen",
                async Task<Ok<CountResponse>> (
                    MarkAllSeenRequest request,
                    MarkAllHomesSeenHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(request, ct))
            )
            .WithName("MarkAllHomesSeen")
            .WithSummary("Mark every Unseen Home as seen");

        homes
            .MapPost(
                "/{id:guid}/listings",
                async Task<Ok<HomeDetailResponse>> (
                    Guid id,
                    LinkListingRequest request,
                    LinkListingHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, request, ct))
            )
            .WithName("LinkListing")
            .WithSummary("Link another ad to this Home (same home)");

        homes
            .MapDelete(
                "/{id:guid}/listings/{listingId:guid}",
                async Task<Ok<HomeSummaryResponse>> (
                    Guid id,
                    Guid listingId,
                    UnlinkListingHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, listingId, ct))
            )
            .WithName("UnlinkListing")
            .WithSummary("Split an ad off this Home (not the same home); returns the new Home")
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet(
                "/bookmarks",
                async Task<Ok<BookmarkBoardResponse>> (
                    GetBookmarkBoardHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(ct))
            )
            .WithTags("Homes")
            .WithName("GetBookmarkBoard")
            .WithSummary("Bookmarked Homes grouped by stage");

        var matches = api.MapGroup("/home-matches").WithTags("Matches");

        matches
            .MapGet(
                "",
                async Task<Ok<IReadOnlyList<HomeMatchResponse>>> (
                    HomeMatchState? state,
                    ListMatchesHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(state, ct))
            )
            .WithName("ListHomeMatches")
            .WithSummary("Matches to review (possible by default)");

        matches
            .MapPatch(
                "/{id:guid}",
                async Task<Results<Ok<HomeMatchResponse>, NoContent>> (
                    Guid id,
                    ReviewMatchRequest request,
                    ReviewMatchHandler h,
                    CancellationToken ct
                ) =>
                    await h.HandleAsync(id, request, ct) is { } match
                        ? TypedResults.Ok(match)
                        : TypedResults.NoContent()
            )
            .WithName("ReviewHomeMatch")
            .WithSummary("Confirm (same home) or reject (different homes) a possible match")
            .ProducesValidationProblem();
    }

    public static void MapListingEndpoints(this RouteGroupBuilder api)
    {
        var listings = api.MapGroup("/listings").WithTags("Listings");

        listings
            .MapGet(
                "/{id:guid}",
                async Task<Ok<ListingDetailResponse>> (
                    Guid id,
                    GetListingHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(id, ct))
            )
            .WithName("GetListing")
            .WithSummary("One ad with its price history")
            .ProducesProblem(StatusCodes.Status404NotFound);

        listings
            .MapDelete(
                "",
                async Task<Ok<DeleteAllListingsResponse>> (
                    DeleteAllListingsHandler h,
                    CancellationToken ct
                ) => TypedResults.Ok(await h.HandleAsync(ct))
            )
            .WithName("DeleteAllListings")
            .WithSummary("Delete every Listing and Home (danger zone)");
    }
}
