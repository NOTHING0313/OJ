namespace OnlineJudge.Application.Account.Dtos;

public class SmsSendResultDto
{
    public string Message { get; set; } = string.Empty;

    public string? DebugCode { get; set; }
}
