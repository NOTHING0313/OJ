namespace OnlineJudge.Application.Account.Requests;

public class ConfirmPasswordResetRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
