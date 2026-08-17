namespace OnlineJudge.Application.Email.Dtos;

public class EmailSendResultDto
{
    public string Message { get; set; } = string.Empty;

    public string? DebugCode { get; set; }
}
