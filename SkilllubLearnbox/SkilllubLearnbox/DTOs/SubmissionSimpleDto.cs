public class SubmissionSimpleDto
{
    public string Id { get; set; }
    public string Status { get; set; }
    public int? Score { get; set; }
    public int? TestsPassed { get; set; }
    public int? TestsTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}