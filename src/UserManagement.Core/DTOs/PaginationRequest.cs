using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class PaginationRequest
{
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
