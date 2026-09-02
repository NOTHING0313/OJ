import type {
  ChallengeLeaderboardSummary,
  LeaderboardSeasonPublicSummary,
  SeasonLeaderboard
} from "../../api/leaderboardsApi";
import fixtures from "./themePreviewFixtures.json";
import type { ProblemDetailViewModel } from "../problems/ProblemDetailView";
import type { HelpDocument, HelpDocumentListItem } from "../../api/helpDocumentsApi";

export const problemPreviewFixture = fixtures.problem as ProblemDetailViewModel;
export const helpPreviewFixture = fixtures.help as { documents: HelpDocumentListItem[]; document: HelpDocument };

export const leaderboardPreviewFixture: {
  globalLeaderboard: SeasonLeaderboard;
  summary: LeaderboardSeasonPublicSummary;
  challenges: ChallengeLeaderboardSummary[];
} = fixtures.leaderboard as {
  globalLeaderboard: SeasonLeaderboard;
  summary: LeaderboardSeasonPublicSummary;
  challenges: ChallengeLeaderboardSummary[];
};
