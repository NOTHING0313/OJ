namespace OnlineJudge.Application.Account.Requests;

public class ConfirmAccountDeleteRequest
{
    public string Code { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
