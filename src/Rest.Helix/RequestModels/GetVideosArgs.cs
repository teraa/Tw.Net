using System.Collections.Generic;

namespace Twitch.Rest.Helix
{
    public enum VideoPeriod
    {
        All = 0,
        Day,
        Week,
        Month
    }

    public enum VideoType
    {
        All = 0,
        Upload,
        Archive,
        Highlight
    }

    public enum VideoSorting
    {
        Time = 0,
        Trending,
        Views
    }

    public abstract class GetVideosArgs : IRequestArgs
    {
        /// <summary>
        ///     Language of the video being queried.
        /// </summary>
        [QueryParam("language")]
        public string? Language { get; init; }

        /// <summary>
        ///     Period during which the video was created. Valid values:
        ///     <c>"all"</c>, <c>"day"</c>, <c>"week"</c>, <c>"month"</c>.
        ///     Default: <c>"all"</c>.
        /// </summary>
        [QueryParam("period")]
        public VideoPeriod? Period { get; init; }

        /// <summary>
        ///     Type of video. Valid values:
        ///     <c>"all"</c>, <c>"upload"</c>, <c>"archive"</c>, <c>"highlight"</c>.
        ///     Default: <c>"all"</c>.
        /// </summary>
        [QueryParam("type")]
        public VideoType? Type { get; init; }

        /// <summary>
        ///     Sort order of the videos. Valid values:
        ///     <c>"time"</c>, <c>"trending"</c>, <c>"views"</c>.
        ///     Default: <c>"time"</c>.
        /// </summary>
        [QueryParam("sort")]
        public VideoSorting? Sort { get; init; }

        /// <summary>
        ///     Maximum number of objects to return.
        ///     Maximum: 100. Default: 20.
        /// </summary>
        [QueryParam("first")]
        public int? First { get; init; }

        /// <summary>
        ///     Cursor for forward pagination;
        ///     where to start fetching the next set of results in a multi-page response.
        /// </summary>
        [QueryParam("after")]
        public string? After { get; init; }

        /// <summary>
        ///     Cursor for backward pagination;
        ///     where to start fetching the next set of results in a multi-page response.
        /// </summary>
        [QueryParam("before")]
        public string? Before { get; init; }
    }

    public class GetVideosByUserIdArgs : GetVideosArgs
    {
        [QueryParam("user_id")]
        public string? UserId { get; init; }
    }

    public class GetVideosByGameIdArgs : GetVideosArgs
    {
        public string? GameId { get; init; }
    }

    public class GetVideosByIdArgs : GetVideosArgs
    {
        public IReadOnlyList<string>? VideoIds { get; init; }
    }
}
