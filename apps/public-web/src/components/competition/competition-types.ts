export type CompetitionView =
  | 'overview' | 'fixtures' | 'results' | 'standings' | 'groups'
  | 'bracket' | 'series' | 'leaderboard' | 'medals' | 'participants'
  | 'statistics';

export type CompetitionFixture = {
  id: string;
  name?: string | null;
  contestShape: string;
  status: string;
  startTime?: string | null;
  venue?: string | null;
  participants: Array<{ participantId: string; name: string }>;
};

export type CompetitionResult = {
  id: string;
  contestId: string;
  contestName: string;
  participantId: string;
  participantName: string;
  rank?: number | null;
  position?: string | null;
  score?: number | null;
  scoreDisplay?: string | null;
  time?: string | null;
  status: string;
};

export type StandingRow = {
  id: string;
  groupId: string;
  groupName?: string | null;
  participantId: string;
  participantName: string;
  position: number;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  pointsFor: number;
  pointsAgainst: number;
  pointDifferential: number;
  points: number;
  metadata?: string | null;
};

export type PlayoffGame = {
  id: string;
  seriesGameNumber: number;
  status: string;
  homeParticipantName?: string | null;
  awayParticipantName?: string | null;
  homeScore?: string | null;
  awayScore?: string | null;
  winnerName?: string | null;
};

export type PlayoffMatch = {
  id: string;
  contestId?: string | null;
  seriesId?: string | null;
  bestOf: number;
  isAggregateTie: boolean;
  isThirdPlace: boolean;
  score1?: string | null;
  score2?: string | null;
  matchNumber: number;
  participant1Name?: string | null;
  participant2Name?: string | null;
  winnerName?: string | null;
  status: string;
  seriesGames: PlayoffGame[];
};

export type PlayoffRound = {
  id: string;
  roundNumber: number;
  name: string;
  matches: PlayoffMatch[];
};

export type Playoffs = {
  id: string;
  competitionId: string;
  name: string;
  status: string;
  championName?: string | null;
  rounds: PlayoffRound[];
};

export type CompetitionDetail = {
  id: string;
  name: string;
  description?: string | null;
  sportId: string;
  sport?: { id: string; code: string; name: string } | null;
  region?: string | null;
  status: string;
  publishedAt?: string | null;
  ruleset?: { id: string; name: string; versionNumber: number } | null;
  labels: Record<string, string>;
  availableTabs: CompetitionView[];
  presentation?: {
    defaultTab?: CompetitionView | null;
    scoreVariant?: string | null;
    standingsVariant?: string | null;
    leaderboardVariant?: string | null;
  } | null;
  editions: Array<{
    id: string; name: string; startDate?: string | null; endDate?: string | null;
    status: string;
    stages: Array<{ id: string; name: string; stageType: string; sortOrder: number }>;
  }>;
  fixtures: CompetitionFixture[];
  results: CompetitionResult[];
  standings: StandingRow[];
  bracket?: { id: string; name: string; status: string; roundCount: number; championName?: string | null } | null;
  leaderboard?: Array<{ participantId: string; participantName: string; rank?: number | null; score?: number | null; scoreDisplay?: string | null }> | null;
  medals: Array<{ participantId: string; participantName: string; gold: number; silver: number; bronze: number; total: number }>;
  participants: Array<{ id: string; name: string }>;
  sponsors: Array<{ id: string; name: string; logoUrl?: string | null; website?: string | null; tier: string; placement: string }>;
  statistics: {
    contestCount: number; completedContests: number; liveContests: number;
    upcomingContests: number; participantCount: number;
    publishedResultCount: number; scoringEventCount: number;
  };
};
