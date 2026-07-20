namespace IssueBridge.Api.Dtos;

public class DashboardSummaryDto
{
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Done { get; set; }
    public int HighPriority { get; set; }
}
