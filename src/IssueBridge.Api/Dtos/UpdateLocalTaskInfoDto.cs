using IssueBridge.Api.Models;

namespace IssueBridge.Api.Dtos;

public class UpdateLocalTaskInfoDto
{
    public string? AssignedTo { get; set; }
    public LocalStatus LocalStatus { get; set; }
    public string? Notes { get; set; }
    public Priority Priority { get; set; }
}
