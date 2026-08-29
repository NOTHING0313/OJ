namespace OnlineJudge.Application.Teams.Dtos;

public class TeamChatPageDto
{
    public IReadOnlyList<TeamChatMessageDto> Messages { get; set; } = [];
    public bool HasMore { get; set; }
}
