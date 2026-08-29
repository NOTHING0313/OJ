import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "./AppLayout";
import { AuthProvider } from "./auth/AuthContext";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { AdminChallengeEditorPage } from "./pages/AdminChallengeEditorPage";
import { AdminChallengeListPage } from "./pages/AdminChallengeListPage";
import { AdminChallengeTaskEditorPage } from "./pages/AdminChallengeTaskEditorPage";
import { AdminSubmissionsPage } from "./pages/AdminSubmissionsPage";
import { AdminSiteSettingsPage } from "./pages/AdminSiteSettingsPage";
import { AdminProblemEditorPage } from "./pages/AdminProblemEditorPage";
import { AdminProblemListPage } from "./pages/AdminProblemListPage";
import { AdminTestCaseEditorPage } from "./pages/AdminTestCaseEditorPage";
import { AdminUserListPage } from "./pages/AdminUserListPage";
import { AdminSecurityAuditPage } from "./pages/AdminSecurityAuditPage";
import { AdminTeamListPage } from "./pages/AdminTeamListPage";
import { AdminLeaderboardSeasonPage } from "./pages/AdminLeaderboardSeasonPage";
import { AccountSettingsPage } from "./pages/AccountSettingsPage";
import { AccountCompetitionPage } from "./pages/AccountCompetitionPage";
import { LeaderboardSeasonHistoryPage } from "./pages/LeaderboardSeasonHistoryPage";
import { LeaderboardSeasonHistoryDetailPage } from "./pages/LeaderboardSeasonHistoryDetailPage";
import { ChallengeAdminSummaryPage } from "./pages/ChallengeAdminSummaryPage";
import { ChallengeAdminTaskDetailPage } from "./pages/ChallengeAdminTaskDetailPage";
import { ChallengeDetailPage } from "./pages/ChallengeDetailPage";
import { ChallengeLeaderboardIndexPage } from "./pages/ChallengeLeaderboardIndexPage";
import { ChallengeLeaderboardPage } from "./pages/ChallengeLeaderboardPage";
import { ChallengePeerReviewPage } from "./pages/ChallengePeerReviewPage";
import { ChallengePeerReviewAuditPage } from "./pages/ChallengePeerReviewAuditPage";
import { ChallengeListPage } from "./pages/ChallengeListPage";
import { ChallengeTaskAnswerPage } from "./pages/ChallengeTaskAnswerPage";
import { ChallengeTaskDetailPage } from "./pages/ChallengeTaskDetailPage";
import { ForbiddenPage } from "./pages/ForbiddenPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { SeasonLeaderboardPage } from "./pages/SeasonLeaderboardPage";
import { SeasonProblemLeaderboardPage } from "./pages/SeasonProblemLeaderboardPage";
import { LeaderboardHomePage } from "./pages/LeaderboardHomePage";
import { LoginPage } from "./pages/LoginPage";
import { MySubmissionsPage } from "./pages/MySubmissionsPage";
import { MyProfilePage } from "./pages/MyProfilePage";
import { ProblemDetailPage } from "./pages/ProblemDetailPage";
import { ProblemListPage } from "./pages/ProblemListPage";
import { RegisterPage } from "./pages/RegisterPage";
import { SubmissionDetailPage } from "./pages/SubmissionDetailPage";
import { ThemeProvider } from "./theme/ThemeContext";
import { TeamPage } from "./pages/TeamPage";
import { TeamProjectHistoryPage } from "./pages/TeamProjectHistoryPage";
import { HelpCenterPage } from "./pages/HelpCenterPage";
import { HelpDocumentManagePage } from "./pages/HelpDocumentManagePage";
import { HelpDocumentEditorPage } from "./pages/HelpDocumentEditorPage";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <ThemeProvider>
          <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route element={<AppLayout />}>
            <Route path="/" element={<Navigate to="/problems" replace />} />
            <Route path="/forbidden" element={<ForbiddenPage />} />
            <Route path="/problems" element={<ProblemListPage />} />
            <Route path="/problems/:id" element={<ProblemDetailPage />} />
            <Route path="/challenges" element={<ChallengeListPage />} />
            <Route path="/challenges/:id" element={<ChallengeDetailPage />} />
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
            <Route path="/challenges/:challengeId/tasks/:taskId" element={<ChallengeTaskDetailPage />} />
            <Route path="/challenges/:challengeId/tasks/:taskId/answer" element={<ChallengeTaskAnswerPage />} />
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
            <Route path="/account/competition" element={<ProtectedRoute><AccountCompetitionPage /></ProtectedRoute>} />
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
        </ThemeProvider>
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>
);
