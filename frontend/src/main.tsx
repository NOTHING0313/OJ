import { lazy, StrictMode, Suspense } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "./AppLayout";
import { AuthProvider } from "./auth/AuthContext";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { ThemeProvider } from "./theme/ThemeContext";
import "./styles.css";

const AccountSettingsPage = lazy(() => import("./pages/AccountSettingsPage").then((module) => ({ default: module.AccountSettingsPage })));
const AdminChallengeEditorPage = lazy(() => import("./pages/AdminChallengeEditorPage").then((module) => ({ default: module.AdminChallengeEditorPage })));
const AdminChallengeListPage = lazy(() => import("./pages/AdminChallengeListPage").then((module) => ({ default: module.AdminChallengeListPage })));
const AdminChallengeTaskEditorPage = lazy(() => import("./pages/AdminChallengeTaskEditorPage").then((module) => ({ default: module.AdminChallengeTaskEditorPage })));
const AdminLeaderboardSeasonPage = lazy(() => import("./pages/AdminLeaderboardSeasonPage").then((module) => ({ default: module.AdminLeaderboardSeasonPage })));
const AdminProblemEditorPage = lazy(() => import("./pages/AdminProblemEditorPage").then((module) => ({ default: module.AdminProblemEditorPage })));
const AdminProblemListPage = lazy(() => import("./pages/AdminProblemListPage").then((module) => ({ default: module.AdminProblemListPage })));
const AdminSecurityAuditPage = lazy(() => import("./pages/AdminSecurityAuditPage").then((module) => ({ default: module.AdminSecurityAuditPage })));
const AdminSiteSettingsPage = lazy(() => import("./pages/AdminSiteSettingsPage"));
const AdminSubmissionsPage = lazy(() => import("./pages/AdminSubmissionsPage").then((module) => ({ default: module.AdminSubmissionsPage })));
const AdminTeamListPage = lazy(() => import("./pages/AdminTeamListPage").then((module) => ({ default: module.AdminTeamListPage })));
const AdminTestCaseEditorPage = lazy(() => import("./pages/AdminTestCaseEditorPage").then((module) => ({ default: module.AdminTestCaseEditorPage })));
const AdminUserListPage = lazy(() => import("./pages/AdminUserListPage").then((module) => ({ default: module.AdminUserListPage })));
const ChallengeAdminSummaryPage = lazy(() => import("./pages/ChallengeAdminSummaryPage").then((module) => ({ default: module.ChallengeAdminSummaryPage })));
const ChallengeAdminTaskDetailPage = lazy(() => import("./pages/ChallengeAdminTaskDetailPage").then((module) => ({ default: module.ChallengeAdminTaskDetailPage })));
const ChallengeDetailPage = lazy(() => import("./pages/ChallengeDetailPage").then((module) => ({ default: module.ChallengeDetailPage })));
const ChallengeLeaderboardIndexPage = lazy(() => import("./pages/ChallengeLeaderboardIndexPage").then((module) => ({ default: module.ChallengeLeaderboardIndexPage })));
const ChallengeLeaderboardPage = lazy(() => import("./pages/ChallengeLeaderboardPage").then((module) => ({ default: module.ChallengeLeaderboardPage })));
const ChallengeListPage = lazy(() => import("./pages/ChallengeListPage").then((module) => ({ default: module.ChallengeListPage })));
const ChallengePeerReviewAuditPage = lazy(() => import("./pages/ChallengePeerReviewAuditPage").then((module) => ({ default: module.ChallengePeerReviewAuditPage })));
const ChallengePeerReviewPage = lazy(() => import("./pages/ChallengePeerReviewPage").then((module) => ({ default: module.ChallengePeerReviewPage })));
const ChallengeTaskAnswerPage = lazy(() => import("./pages/ChallengeTaskAnswerPage").then((module) => ({ default: module.ChallengeTaskAnswerPage })));
const ChallengeTaskDetailPage = lazy(() => import("./pages/ChallengeTaskDetailPage").then((module) => ({ default: module.ChallengeTaskDetailPage })));
const ForbiddenPage = lazy(() => import("./pages/ForbiddenPage").then((module) => ({ default: module.ForbiddenPage })));
const ForgotPasswordPage = lazy(() => import("./pages/ForgotPasswordPage").then((module) => ({ default: module.ForgotPasswordPage })));
const HelpCenterPage = lazy(() => import("./pages/HelpCenterPage").then((module) => ({ default: module.HelpCenterPage })));
const HelpDocumentEditorPage = lazy(() => import("./pages/HelpDocumentEditorPage").then((module) => ({ default: module.HelpDocumentEditorPage })));
const HelpDocumentManagePage = lazy(() => import("./pages/HelpDocumentManagePage").then((module) => ({ default: module.HelpDocumentManagePage })));
const LeaderboardHomePage = lazy(() => import("./pages/LeaderboardHomePage").then((module) => ({ default: module.LeaderboardHomePage })));
const LeaderboardSeasonHistoryDetailPage = lazy(() => import("./pages/LeaderboardSeasonHistoryDetailPage").then((module) => ({ default: module.LeaderboardSeasonHistoryDetailPage })));
const LeaderboardSeasonHistoryPage = lazy(() => import("./pages/LeaderboardSeasonHistoryPage").then((module) => ({ default: module.LeaderboardSeasonHistoryPage })));
const LoginPage = lazy(() => import("./pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const MyProfilePage = lazy(() => import("./pages/MyProfilePage").then((module) => ({ default: module.MyProfilePage })));
const MySubmissionsPage = lazy(() => import("./pages/MySubmissionsPage").then((module) => ({ default: module.MySubmissionsPage })));
const ProblemDetailPage = lazy(() => import("./pages/ProblemDetailPage").then((module) => ({ default: module.ProblemDetailPage })));
const ProblemListPage = lazy(() => import("./pages/ProblemListPage").then((module) => ({ default: module.ProblemListPage })));
const RegisterPage = lazy(() => import("./pages/RegisterPage").then((module) => ({ default: module.RegisterPage })));
const SeasonLeaderboardPage = lazy(() => import("./pages/SeasonLeaderboardPage").then((module) => ({ default: module.SeasonLeaderboardPage })));
const SeasonProblemLeaderboardPage = lazy(() => import("./pages/SeasonProblemLeaderboardPage").then((module) => ({ default: module.SeasonProblemLeaderboardPage })));
const SubmissionDetailPage = lazy(() => import("./pages/SubmissionDetailPage").then((module) => ({ default: module.SubmissionDetailPage })));
const TeamPage = lazy(() => import("./pages/TeamPage").then((module) => ({ default: module.TeamPage })));
const TeamProjectHistoryPage = lazy(() => import("./pages/TeamProjectHistoryPage").then((module) => ({ default: module.TeamProjectHistoryPage })));

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ThemeProvider>
          <Suspense fallback={<div className="state-line">正在加载页面...</div>}>
          <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route element={<AppLayout />}>
            <Route path="/" element={<Navigate to="/problems" replace />} />
            <Route path="/forbidden" element={<ForbiddenPage />} />
            <Route path="/problems" element={<ProtectedRoute><ProblemListPage /></ProtectedRoute>} />
            <Route path="/problems/:id" element={<ProtectedRoute><ProblemDetailPage /></ProtectedRoute>} />
            <Route path="/challenges" element={<ChallengeListPage />} />
            <Route path="/challenges/:id" element={<ProtectedRoute><ChallengeDetailPage /></ProtectedRoute>} />
            <Route path="/challenges/:challengeId/peer-review" element={<ProtectedRoute><ChallengePeerReviewPage /></ProtectedRoute>} />
            <Route path="/challenges/:challengeId/peer-review-audit" element={<ProtectedRoute allowedRoles={[2, 3]}><ChallengePeerReviewAuditPage /></ProtectedRoute>} />
            <Route path="/leaderboards" element={<LeaderboardHomePage />} />
            <Route path="/leaderboards/users" element={<SeasonLeaderboardPage />} />
            <Route path="/leaderboards/users/problems/:problemId" element={<SeasonProblemLeaderboardPage />} />
            <Route path="/leaderboards/challenges" element={<ChallengeLeaderboardIndexPage />} />
            <Route path="/leaderboards/history" element={<ProtectedRoute allowedRoles={[2, 3]}><LeaderboardSeasonHistoryPage /></ProtectedRoute>} />
            <Route path="/leaderboards/history/:seasonId" element={<ProtectedRoute allowedRoles={[2, 3]}><LeaderboardSeasonHistoryDetailPage /></ProtectedRoute>} />
            <Route path="/teams" element={<ProtectedRoute><TeamPage /></ProtectedRoute>} />
            <Route path="/teams/:teamId/projects/:projectId/history" element={<ProtectedRoute><TeamProjectHistoryPage /></ProtectedRoute>} />
            <Route path="/help" element={<ProtectedRoute><HelpCenterPage /></ProtectedRoute>} />
            <Route path="/help/:slug" element={<ProtectedRoute><HelpCenterPage /></ProtectedRoute>} />
            <Route path="/help/manage" element={<ProtectedRoute allowedRoles={[2, 3]}><HelpDocumentManagePage /></ProtectedRoute>} />
            <Route path="/help/manage/new" element={<ProtectedRoute allowedRoles={[2, 3]}><HelpDocumentEditorPage /></ProtectedRoute>} />
            <Route path="/help/manage/:id" element={<ProtectedRoute allowedRoles={[2, 3]}><HelpDocumentEditorPage /></ProtectedRoute>} />
            <Route
              path="/challenges/:challengeId/admin"
              element={(
                <ProtectedRoute>
                  <ChallengeAdminSummaryPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/challenges/:challengeId/admin/tasks/:taskId"
              element={(
                <ProtectedRoute>
                  <ChallengeAdminTaskDetailPage />
                </ProtectedRoute>
              )}
            />
            <Route path="/challenges/:challengeId/leaderboard" element={<ChallengeLeaderboardPage />} />
            <Route path="/challenges/:challengeId/tasks/:taskId" element={<ProtectedRoute><ChallengeTaskDetailPage /></ProtectedRoute>} />
            <Route path="/challenges/:challengeId/tasks/:taskId/answer" element={<ProtectedRoute><ChallengeTaskAnswerPage /></ProtectedRoute>} />
            <Route
              path="/submissions/my"
              element={(
                <ProtectedRoute>
                  <MySubmissionsPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/profile/me"
              element={(
                <ProtectedRoute>
                  <MyProfilePage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/account/settings"
              element={(
                <ProtectedRoute>
                  <AccountSettingsPage />
                </ProtectedRoute>
              )}
            />
            {/* Personal season records are suspended pending explicit product requirements. */}
            <Route path="/account/competition" element={<Navigate to="/profile/me" replace />} />
            <Route
              path="/submissions"
              element={(
                <ProtectedRoute>
                  <Navigate to="/submissions/my" replace />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/submissions/:id"
              element={(
                <ProtectedRoute>
                  <SubmissionDetailPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/problems"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminProblemListPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/problems/new"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminProblemEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/problems/:id/edit"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminProblemEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/problems/:id/test-cases"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminTestCaseEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/challenges"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminChallengeListPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/challenges/new"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminChallengeEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/challenges/:id/edit"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminChallengeEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/challenges/:id/tasks/new"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminChallengeTaskEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/challenges/:challengeId/tasks/:taskId/edit"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminChallengeTaskEditorPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/leaderboard-seasons"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminLeaderboardSeasonPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/teams"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <AdminTeamListPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/teams/:teamId"
              element={(
                <ProtectedRoute allowedRoles={[2, 3]}>
                  <TeamPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/users"
              element={(
                <ProtectedRoute allowedRoles={[3]}>
                  <AdminUserListPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/users/:userId/profile"
              element={(
                <ProtectedRoute allowedRoles={[3]}>
                  <MyProfilePage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/submissions"
              element={(
                <ProtectedRoute allowedRoles={[3]}>
                  <AdminSubmissionsPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/site-settings"
              element={(
                <ProtectedRoute allowedRoles={[3]}>
                  <AdminSiteSettingsPage />
                </ProtectedRoute>
              )}
            />
            <Route
              path="/admin/security-audit"
              element={(
                <ProtectedRoute allowedRoles={[3]}>
                  <AdminSecurityAuditPage />
                </ProtectedRoute>
              )}
            />
          </Route>
          </Routes>
          </Suspense>
        </ThemeProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
