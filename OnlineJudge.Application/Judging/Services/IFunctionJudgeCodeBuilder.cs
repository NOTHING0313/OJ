using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Application.Judging.Services;

public interface IFunctionJudgeCodeBuilder
{
    Result<JudgeRequest> Build(JudgeRequest request);
}
