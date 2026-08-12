// Shared types for the Sports Platform

// Tenant types
export interface Tenant {
  id: string;
  name: string;
  shortName: string;
  slug: string;
  status: TenantStatus;
  subscription: Subscription;
  branding: TenantBranding;
  createdAt: string;
  updatedAt: string;
}

export type TenantStatus = 'DRAFT' | 'ACTIVE' | 'SUSPENDED' | 'ARCHIVED';

export interface Subscription {
  id: string;
  plan: SubscriptionPlan;
  status: SubscriptionStatus;
  startDate: string;
  endDate: string;
  features: string[];
}

export type SubscriptionPlan = 'STARTER' | 'PROFESSIONAL' | 'ENTERPRISE';
export type SubscriptionStatus = 'DRAFT' | 'ACTIVE' | 'EXPIRING' | 'GRACE_PERIOD' | 'READ_ONLY' | 'SUSPENDED' | 'ARCHIVED';

export interface TenantBranding {
  logo: string | null;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
}

// Sport types
export interface Sport {
  id: string;
  code: string;
  name: string;
  description: string;
  icon: string;
  active: boolean;
  currentVersion: string;
}

// Competition types
export interface Competition {
  id: string;
  tenantId: string;
  sportId: string;
  name: string;
  edition: string;
  status: CompetitionStatus;
  format: CompetitionFormat;
  startDate: string;
  endDate: string;
  participantCount: number;
  createdAt: string;
}

export type CompetitionStatus = 'DRAFT' | 'REGISTRATION' | 'ACTIVE' | 'COMPLETED' | 'CANCELLED';

export interface CompetitionFormat {
  type: FormatType;
  config: Record<string, unknown>;
}

export type FormatType = 'ROUND_ROBIN' | 'KNOCKOUT' | 'GROUP_PLUS_KNOCKOUT' | 'BEST_OF_SERIES' | 'MULTI_ROUND_LEADERBOARD' | 'HEAT_QUALIFICATION' | 'FIGHT_CARD';

// Contest types
export interface Contest {
  id: string;
  competitionId: string;
  stageId: string;
  status: ContestStatus;
  participants: ContestParticipant[];
  segments: ContestSegment[];
  startTime: string | null;
  venue: string | null;
  score: ContestScore | null;
}

export type ContestStatus = 'SCHEDULED' | 'CHECKED_IN' | 'LIVE' | 'COMPLETED' | 'CANCELLED' | 'POSTPONED';

export interface ContestParticipant {
  id: string;
  participantId: string;
  participantType: 'TEAM' | 'ATHLETE';
  name: string;
  seed: number | null;
  score: number | null;
  rank: number | null;
}

export interface ContestSegment {
  id: string;
  name: string;
  order: number;
  status: SegmentStatus;
  duration: number | null;
  events: ContestEvent[];
}

export type SegmentStatus = 'PENDING' | 'ACTIVE' | 'COMPLETED' | 'SKIPPED';

export interface ContestEvent {
  id: string;
  segmentId: string;
  type: string;
  participantId: string | null;
  value: number | string | null;
  timestamp: string;
  official: boolean;
}

export interface ContestScore {
  home: number;
  away: number;
  display: string;
}

// User and Auth types
export interface User {
  id: string;
  email: string;
  name: string;
  role: UserRole;
  tenantId: string | null;
  permissions: string[];
}

export type UserRole = 'PLATFORM_OWNER' | 'PLATFORM_SUPPORT' | 'TENANT_OWNER' | 'COMPETITION_ADMIN' | 'REGISTRATION_OFFICER' | 'FIXTURE_MANAGER' | 'HEAD_OFFICIAL' | 'SCORER' | 'RESULT_APPROVER' | 'MEDIA_MANAGER' | 'VIEWER';

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

// API Response types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
}

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, string[]>;
}

// Live Scoring types
export interface LiveEventSubmission {
  contestId: string;
  eventId: string;
  type: string;
  participantId?: string;
  value?: number | string;
  timestamp: string;
  segmentId?: string;
  clientId: string;
}

export interface CorrectionRequest {
  originalEventId: string;
  correctionType: 'REVERSE' | 'MODIFY' | 'ADD';
  newValue?: number | string;
  reason: string;
  approverId?: string;
}

// Result types
export interface ResultApproval {
  contestId: string;
  status: 'PENDING' | 'APPROVED' | 'REJECTED';
  submittedBy: string;
  submittedAt: string;
  approvedBy?: string;
  approvedAt?: string;
  rejectionReason?: string;
}

// Zod schemas for validation
import { z } from 'zod';

export const TenantSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(200),
  shortName: z.string().min(1).max(50),
  slug: z.string().min(1).max(100).regex(/^[a-z0-9-]+$/),
  status: z.enum(['DRAFT', 'ACTIVE', 'SUSPENDED', 'ARCHIVED']),
});

export const CompetitionSchema = z.object({
  id: z.string().uuid(),
  tenantId: z.string().uuid(),
  sportId: z.string().uuid(),
  name: z.string().min(1).max(200),
  edition: z.string().min(1).max(50),
  status: z.enum(['DRAFT', 'REGISTRATION', 'ACTIVE', 'COMPLETED', 'CANCELLED']),
  format: z.object({
    type: z.enum(['ROUND_ROBIN', 'KNOCKOUT', 'GROUP_PLUS_KNOCKOUT', 'BEST_OF_SERIES', 'MULTI_ROUND_LEADERBOARD', 'HEAT_QUALIFICATION', 'FIGHT_CARD']),
    config: z.record(z.unknown()),
  }),
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
});

export const LiveEventSubmissionSchema = z.object({
  contestId: z.string().uuid(),
  eventId: z.string().uuid(),
  type: z.string().min(1),
  participantId: z.string().uuid().optional(),
  value: z.union([z.number(), z.string()]).optional(),
  timestamp: z.string().datetime(),
  segmentId: z.string().uuid().optional(),
  clientId: z.string().uuid(),
});