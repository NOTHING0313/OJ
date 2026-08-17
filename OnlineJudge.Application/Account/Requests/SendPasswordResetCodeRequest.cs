namespace OnlineJudge.Application.Account.Requests;

public class SendPasswordResetCodeRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
