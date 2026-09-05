import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getMyProfile, getUserProfile, type ProfileSummary } from "../api/profileApi";
import type { JudgeStatus } from "../api/submissionsApi";
import { formatDate, languageLabel, statusLabel } from "../utils/labels";

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
    <section className="challenge-page profile-page profile-v2-page">
      <div className="profile-hero admin-panel profile-v2-hero">
        <div className="leaderboard-user profile-user profile-v2-user">
          {user.avatarUrl ? (
            <img src={user.avatarUrl} alt={user.userName} />
          ) : (
            <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
          )}
          <div className="profile-v2-identity-copy">
            <p className="eyebrow">PROFILE</p>
            <h1>{user.userName}</h1>
            <p>{user.email}</p>
          </div>
        </div>
        <div className="profile-v2-hero-actions">
          <div className="profile-badges profile-v2-badges">
            <RoleBadge role={user.role} />
            <UserStatusBadge isBlacklisted={user.isBlacklisted} />
            <span className="profile-registered-at">注册于 {formatDate(user.createdAt)}</span>
          </div>
          {!userId && <Link className="button profile-settings-link" to="/account/settings">账号设置</Link>}
        </div>
      </div>

      <div className="admin-metrics profile-stats-grid profile-v2-stats-grid">
        <Metric label="总提交" value={submissionSummary.totalSubmissionCount} />
        <Metric label="AC 提交" value={submissionSummary.acceptedSubmissionCount} />
        <Metric label="AC 率" value={formatPercent(submissionSummary.acceptedRate)} />
        <Metric label="通过题目" value={problemSummary.acceptedProblemCount} />
        <Metric label="参与 Challenge" value={challengeSummary.participatedChallengeCount} />
        <Metric label="Challenge 总分" value={challengeSummary.totalScore} />
      </div>

      <div className="profile-two-column-grid profile-v2-grid">
        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">SUBMISSIONS</span>
              <h2>提交状态分布</h2>
            </div>
          </div>
          <div className="profile-list profile-v2-fact-list">
            <SubmissionFact status={3} label="答案正确" value={submissionSummary.acceptedSubmissionCount} />
            <SubmissionFact status={4} label="答案错误" value={submissionSummary.wrongAnswerCount} />
            <SubmissionFact status={8} label="编译错误" value={submissionSummary.compileErrorCount} />
            <SubmissionFact status={7} label="运行错误" value={submissionSummary.runtimeErrorCount} />
            <SubmissionFact status={9} label="系统错误" value={submissionSummary.systemErrorCount} />
            <Fact label="最近提交" value={formatDate(submissionSummary.lastSubmittedAt)} />
          </div>
        </section>

        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">LANGUAGES</span>
              <h2>语言统计</h2>
            </div>
          </div>
          {languageSummary.length === 0 ? (
            <div className="empty-state profile-v2-empty-state">暂无语言统计</div>
          ) : (
            <div className="table-wrap profile-compact-table profile-v2-table-wrap">
              <table className="profile-v2-table profile-language-table">
                <thead>
                  <tr>
                    <th>语言</th>
                    <th>提交</th>
                    <th>AC</th>
                    <th>通过率</th>
                  </tr>
                </thead>
                <tbody>
                  {languageSummary.map((item) => (
                    <tr key={item.language}>
                      <td><span className="submission-language-badge">{languageLabel(item.language)}</span></td>
                      <td>{item.submissionCount}</td>
                      <td>{item.acceptedCount}</td>
                      <td>{formatLanguageRate(item.acceptedCount, item.submissionCount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      <section className="admin-panel profile-full-width profile-v2-card profile-v2-recent-submissions">
        <div className="admin-panel-header profile-v2-card-header profile-v2-card-header-row">
          <div>
            <span className="profile-section-kicker">RECENT ACTIVITY</span>
            <h2>最近提交</h2>
          </div>
          {!userId && <Link className="profile-section-link" to="/submissions/my">查看全部提交</Link>}
        </div>
        {profile.recentSubmissions.length === 0 ? (
          <div className="empty-state profile-v2-empty-state">暂无提交记录</div>
        ) : (
          <div className="table-wrap profile-v2-table-wrap profile-v2-recent-table-wrap">
            <table className="profile-v2-table profile-recent-submission-table">
              <thead>
                <tr>
                  <th>题目</th>
                  <th>语言</th>
                  <th>状态</th>
                  <th>提交时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {profile.recentSubmissions.map((submission) => (
                  <tr key={submission.id}>
                    <td>
                      <Link className="profile-problem-link" title={submission.problemTitle} to={`/problems/${submission.problemId}`}>
                        {submission.problemTitle}
                      </Link>
                    </td>
                    <td><span className="submission-language-badge">{submission.language ? languageLabel(submission.language) : "选择题"}</span></td>
                    <td><SubmissionStatusBadge status={submission.status} /></td>
                    <td><SubmissionDateTime value={submission.createdAt} /></td>
                    <td><Link className="button submission-view-link" to={`/submissions/${submission.id}`}>查看</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className="profile-two-column-grid profile-v2-grid">
        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">PROBLEMS</span>
              <h2>最近通过题目</h2>
            </div>
          </div>
          {problemSummary.recentAcceptedProblems.length === 0 ? (
            <div className="empty-state profile-v2-empty-state">暂无通过记录</div>
          ) : (
            <div className="profile-list profile-v2-link-list">
              {problemSummary.recentAcceptedProblems.map((problem) => (
                <Link className="profile-list-item profile-v2-list-item" key={problem.problemId} to={`/problems/${problem.problemId}`}>
                  <div className="profile-v2-list-copy">
                    <strong>{problem.title}</strong>
                    <span>已通过</span>
                  </div>
                  <time>{formatDate(problem.acceptedAt)}</time>
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">CHALLENGE</span>
              <h2>Challenge 概览</h2>
            </div>
          </div>
          <div className="profile-list profile-v2-fact-list">
            <Fact label="参与 Challenge" value={challengeSummary.participatedChallengeCount} />
            <Fact label="完成任务" value={challengeSummary.completedTaskCount} />
            <Fact label="总得分" value={challengeSummary.totalScore} />
            <Fact label="最近完成" value={formatDate(challengeSummary.lastCompletedAt)} />
          </div>
        </section>
      </div>

      <div className="profile-two-column-grid profile-v2-grid">
        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">CHALLENGE ACTIVITY</span>
              <h2>最近 Challenge 完成</h2>
            </div>
          </div>
          {profile.recentChallengeCompletions.length === 0 ? (
            <div className="empty-state profile-v2-empty-state">暂无 Challenge 完成记录</div>
          ) : (
            <div className="profile-list profile-v2-link-list">
              {profile.recentChallengeCompletions.map((completion) => (
                <Link className="profile-list-item profile-v2-list-item" key={`${completion.challengeId}-${completion.taskId}-${completion.completedAt}`} to={`/challenges/${completion.challengeId}`}>
                  <div className="profile-v2-list-copy">
                    <strong>{completion.challengeTitle} · {completion.taskTitle}</strong>
                    <span>{completion.score} 分</span>
                  </div>
                  <time>{formatDate(completion.completedAt)}</time>
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className="admin-panel profile-equal-card profile-v2-card">
          <div className="admin-panel-header profile-v2-card-header">
            <div>
              <span className="profile-section-kicker">FILE REVIEW</span>
              <h2>文件题评分</h2>
            </div>
          </div>
          {profile.recentFileReviews.length === 0 ? (
            <div className="empty-state profile-v2-empty-state">暂无文件题提交或评分记录</div>
          ) : (
            <div className="profile-list profile-v2-link-list">
              {profile.recentFileReviews.map((review) => (
                <Link className="profile-list-item profile-v2-list-item profile-v2-review-item" key={`${review.challengeId}-${review.taskId}-${review.submittedAt}`} to={`/challenges/${review.challengeId}/tasks/${review.taskId}/answer`}>
                  <div className="profile-v2-list-copy">
                    <strong>{review.challengeTitle} · {review.taskTitle}</strong>
                    <span>
                      {review.reviewScore === null ? "等待评分" : `${review.reviewScore} 分`}
                      {review.reviewedAt ? ` · ${formatDate(review.reviewedAt)}` : ` · 提交于 ${formatDate(review.submittedAt)}`}
                    </span>
                    {review.reviewComment && <span className="profile-v2-review-comment">{review.reviewComment}</span>}
                  </div>
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
    <div className="admin-metric profile-metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="profile-fact profile-v2-fact">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function SubmissionFact({ status, label, value }: { status: JudgeStatus; label: string; value: number }) {
  return (
    <div className="profile-fact profile-v2-fact profile-v2-status-fact">
      <span className={`submission-status-badge submission-status-${statusTone(status)}`}>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function RoleBadge({ role }: { role: number }) {
  const className = role === 3 ? "admin-user-role-root" : role === 2 ? "admin-user-role-problem-setter" : "admin-user-role-answerer";
  const label = role === 3 ? "Root" : role === 2 ? "出题人" : "答题人";
  return <span className={`admin-user-badge ${className}`}>{label}</span>;
}

function UserStatusBadge({ isBlacklisted }: { isBlacklisted: boolean }) {
  return (
    <span className={`admin-user-badge ${isBlacklisted ? "admin-user-status-blacklisted" : "admin-user-status-active"}`}>
      {isBlacklisted ? "已拉黑" : "正常"}
    </span>
  );
}

function SubmissionStatusBadge({ status }: { status: JudgeStatus }) {
  return <span className={`submission-status-badge submission-status-${statusTone(status)}`}>{statusLabel(status)}</span>;
}

function SubmissionDateTime({ value }: { value: string | null }) {
  const dateTime = formatProfileDateTime(value);
  if (!dateTime) {
    return <span className="submission-empty-value">—</span>;
  }

  return (
    <time className="submission-date-time" dateTime={value ?? undefined}>
      <strong>{dateTime.date}</strong>
      <span>{dateTime.time}</span>
    </time>
  );
}

function statusTone(status: JudgeStatus) {
  switch (status) {
    case 1:
      return "pending";
    case 2:
      return "judging";
    case 3:
      return "accepted";
    case 4:
      return "wrong-answer";
    case 5:
    case 6:
      return "limit";
    case 7:
      return "runtime-error";
    case 8:
      return "compile-error";
    case 9:
      return "system-error";
  }
}

function formatPercent(value: number) {
  return `${Math.round(value * 1000) / 10}%`;
}

function formatLanguageRate(acceptedCount: number, submissionCount: number) {
  return submissionCount <= 0 ? "—" : `${Math.round((acceptedCount / submissionCount) * 1000) / 10}%`;
}

function formatProfileDateTime(value: string | null) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  const parts = new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23"
  }).formatToParts(date);
  const readPart = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";

  return {
    date: `${readPart("year")}-${readPart("month")}-${readPart("day")}`,
    time: `${readPart("hour")}:${readPart("minute")}`
  };
}
