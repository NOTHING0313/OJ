import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getMyProfile, getUserProfile, type ProfileSummary } from "../api/profileApi";
import { formatDate, languageLabel, statusLabel } from "../utils/labels";

const roleNames: Record<number, string> = {
  1: "答题人",
  2: "出题人",
  3: "Root"
};

export function MyProfilePage() {
  const { userId } = useParams();
  const [profile, setProfile] = useState<ProfileSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    async function loadProfile() {
      try {
        setIsLoading(true);
        const data = userId ? await getUserProfile(userId) : await getMyProfile();
        if (!ignore) {
          setProfile(data);
          setError(null);
        }
      } catch (err) {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "个人中心加载失败");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    void loadProfile();

    return () => {
      ignore = true;
    };
  }, [userId]);

  if (isLoading) {
    return <div className="state-line">正在加载个人中心...</div>;
  }

  if (error || !profile) {
    return <div className="alert error">{error ?? "个人中心不可用"}</div>;
  }

  const { user, submissionSummary, problemSummary, languageSummary, challengeSummary } = profile;

  return (
    <section className="challenge-page profile-page">
      <div className="profile-hero admin-panel">
        <div className="leaderboard-user profile-user">
          {user.avatarUrl ? (
            <img src={user.avatarUrl} alt={user.userName} />
          ) : (
            <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
          )}
          <div>
            <p className="eyebrow">PROFILE</p>
            <h1>{user.userName}</h1>
            <p>{user.email}</p>
          </div>
        </div>
        <div className="profile-badges">
          <span className="challenge-status">{roleNames[user.role] ?? "未知角色"}</span>
          <span className={user.isBlacklisted ? "challenge-status danger" : "challenge-status"}>
            {user.isBlacklisted ? "已拉黑" : "正常"}
          </span>
          <span className="muted">注册：{formatDate(user.createdAt)}</span>
          {!userId && <Link className="button" to="/account/settings">账号设置</Link>}
        </div>
      </div>

      <div className="admin-metrics profile-stats-grid">
        <Metric label="总提交" value={submissionSummary.totalSubmissionCount} />
        <Metric label="AC 提交" value={submissionSummary.acceptedSubmissionCount} />
        <Metric label="AC 率" value={formatPercent(submissionSummary.acceptedRate)} />
        <Metric label="通过题目" value={problemSummary.acceptedProblemCount} />
        <Metric label="参与 Challenge" value={challengeSummary.participatedChallengeCount} />
        <Metric label="Challenge 总分" value={challengeSummary.totalScore} />
      </div>

      <div className="profile-two-column-grid">
        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>提交状态分布</h2>
          </div>
          <div className="profile-list">
            <Fact label="答案正确" value={submissionSummary.acceptedSubmissionCount} />
            <Fact label="答案错误" value={submissionSummary.wrongAnswerCount} />
            <Fact label="编译错误" value={submissionSummary.compileErrorCount} />
            <Fact label="运行错误" value={submissionSummary.runtimeErrorCount} />
            <Fact label="系统错误" value={submissionSummary.systemErrorCount} />
            <Fact label="最近提交" value={formatDate(submissionSummary.lastSubmittedAt)} />
          </div>
        </section>

        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>语言统计</h2>
          </div>
          {languageSummary.length === 0 ? (
            <div className="empty-state">暂无语言统计</div>
          ) : (
            <div className="table-wrap profile-compact-table">
              <table>
                <thead>
                  <tr>
                    <th>语言</th>
                    <th>提交</th>
                    <th>AC</th>
                  </tr>
                </thead>
                <tbody>
                  {languageSummary.map((item) => (
                    <tr key={item.language}>
                      <td>{languageLabel(item.language)}</td>
                      <td>{item.submissionCount}</td>
                      <td>{item.acceptedCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      <section className="admin-panel profile-full-width">
        <div className="admin-panel-header">
          <h2>最近提交</h2>
        </div>
        {profile.recentSubmissions.length === 0 ? (
          <div className="empty-state">暂无提交记录</div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>题目</th>
                  <th>语言</th>
                  <th>状态</th>
                  <th>时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {profile.recentSubmissions.map((submission) => (
                  <tr key={submission.id}>
                    <td>
                      <Link to={`/problems/${submission.problemId}`}>{submission.problemTitle}</Link>
                    </td>
                    <td>{languageLabel(submission.language)}</td>
                    <td>{statusLabel(submission.status)}</td>
                    <td>{formatDate(submission.createdAt)}</td>
                    <td>
                      <Link className="button" to={`/submissions/${submission.id}`}>
                        查看
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className="profile-two-column-grid">
        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>最近通过题目</h2>
          </div>
          {problemSummary.recentAcceptedProblems.length === 0 ? (
            <div className="empty-state">暂无通过记录</div>
          ) : (
            <div className="profile-list">
              {problemSummary.recentAcceptedProblems.map((problem) => (
                <Link className="profile-list-item" key={problem.problemId} to={`/problems/${problem.problemId}`}>
                  <strong>{problem.title}</strong>
                  <span>{formatDate(problem.acceptedAt)}</span>
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>Challenge 概览</h2>
          </div>
          <div className="profile-list">
            <Fact label="参与 Challenge" value={challengeSummary.participatedChallengeCount} />
            <Fact label="完成任务" value={challengeSummary.completedTaskCount} />
            <Fact label="总得分" value={challengeSummary.totalScore} />
            <Fact label="最近完成" value={formatDate(challengeSummary.lastCompletedAt)} />
          </div>
        </section>
      </div>

      <div className="profile-two-column-grid">
        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>最近 Challenge 完成</h2>
          </div>
          {profile.recentChallengeCompletions.length === 0 ? (
            <div className="empty-state">暂无 Challenge 完成记录</div>
          ) : (
            <div className="profile-list">
              {profile.recentChallengeCompletions.map((completion) => (
                <Link className="profile-list-item" key={`${completion.challengeId}-${completion.taskId}-${completion.completedAt}`} to={`/challenges/${completion.challengeId}`}>
                  <strong>{completion.challengeTitle} · {completion.taskTitle}</strong>
                  <span>{completion.score} 分 · {formatDate(completion.completedAt)}</span>
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="admin-panel profile-equal-card">
          <div className="admin-panel-header">
            <h2>文件题评分</h2>
          </div>
          {profile.recentFileReviews.length === 0 ? (
            <div className="empty-state">暂无文件题提交或评分记录</div>
          ) : (
            <div className="profile-list">
              {profile.recentFileReviews.map((review) => (
                <Link className="profile-list-item" key={`${review.challengeId}-${review.taskId}-${review.submittedAt}`} to={`/challenges/${review.challengeId}/tasks/${review.taskId}/answer`}>
                  <strong>{review.challengeTitle} · {review.taskTitle}</strong>
                  <span>
                    {review.reviewScore === null ? "等待评分" : `${review.reviewScore} 分`}
                    {review.reviewedAt ? ` · ${formatDate(review.reviewedAt)}` : ` · 提交于 ${formatDate(review.submittedAt)}`}
                  </span>
                  {review.reviewComment && <span className="muted">{review.reviewComment}</span>}
                </Link>
              ))}
            </div>
          )}
        </section>
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="admin-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="profile-fact">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function formatPercent(value: number) {
  return `${Math.round(value * 1000) / 10}%`;
}
