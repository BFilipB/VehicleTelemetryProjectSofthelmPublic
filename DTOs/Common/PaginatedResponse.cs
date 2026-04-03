namespace VehicleTelemetryAPI.DTOs.Common;

/// <summary>
/// Generic paginated response for API endpoints returning lists.
/// </summary>
/// <typeparam name="T">The type of items in the paginated response.</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// The data items for the current page.
    /// </summary>
    public List<T> Data { get; set; } = new();

    /// <summary>
    /// Current page number.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total count of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Calculated total pages based on TotalCount and PageSize.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Indicates if there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Indicates if there is a next page available.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Base pagination request DTO.
/// </summary>
public class PaginationRequest
{
    private int _pageNumber = 1;
    private int _pageSize = 10;

    /// <summary>
    /// Gets or sets the page number (1-based index). Defaults to 1.
    /// </summary>
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value > 0 ? value : 1;
    }

    /// <summary>
    /// Gets or sets the page size. Must be between 1 and 100. Defaults to 10.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 0 && value <= 100 ? value : 10;
    }
}

/// <summary>
/// Filter and sorting request DTO for telemetry queries.
/// Extends PaginationRequest to include filtering and sorting capabilities.
/// </summary>
public class FilterRequest : PaginationRequest
{
    /// <summary>
    /// Field to sort by. Valid values: "timestamp", "fuelLevel", "engineRPM".
    /// </summary>
    public string SortBy { get; set; } = "timestamp";

    /// <summary>
    /// Sort order. Valid values: "asc" (ascending) or "desc" (descending).
    /// </summary>
    public string SortOrder { get; set; } = "asc";

    /// <summary>
    /// Minimum fuel level percentage to filter by (inclusive).
    /// </summary>
    public decimal? MinFuelLevel { get; set; }

    /// <summary>
    /// Maximum fuel level percentage to filter by (inclusive).
    /// </summary>
    public decimal? MaxFuelLevel { get; set; }

    /// <summary>
    /// Minimum engine RPM to filter by (inclusive).
    /// </summary>
    public int? MinEngineRPM { get; set; }

    /// <summary>
    /// Maximum engine RPM to filter by (inclusive).
    /// </summary>
    public int? MaxEngineRPM { get; set; }
}
