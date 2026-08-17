namespace OnlineJudge.Application.Account.Requests;

public class VerifyPhoneRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
