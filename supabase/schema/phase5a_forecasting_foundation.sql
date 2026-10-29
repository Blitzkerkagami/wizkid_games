-- WizKid Phase 5A: forecasting features and secure forecast storage.
-- Prerequisite: phase4_learning_sync.sql

begin;

create index if not exists question_attempts_forecasting_idx
    on public.question_attempts(
        owner_id,
        player_id,
        skill,
        attempted_at_utc desc,
        attempt_id desc
    ) include (
        session_id,
        level,
        difficulty,
        is_correct,
        timed_out,
        response_time_ms
    );

create table if not exists public.learner_forecasts (
    forecast_id uuid primary key default gen_random_uuid(),
    owner_id uuid not null references auth.users(id) on delete cascade,
    player_id uuid not null,
    skill text not null check (char_length(skill) between 1 and 80),
    model_version text not null check (char_length(model_version) between 1 and 80),
    generated_at timestamptz not null default now(),
    valid_until timestamptz not null,
    source_attempt_count integer not null check (source_attempt_count >= 10),
    expected_score_percent numeric(5,2) not null
        check (expected_score_percent between 0 and 100),
    mastery_probability numeric(5,4) not null
        check (mastery_probability between 0 and 1),
    confidence numeric(5,4) not null
        check (confidence between 0 and 1),
    trend_label text not null
        check (trend_label in ('improving', 'stable', 'declining')),
    risk_level text not null
        check (risk_level in ('low', 'medium', 'high')),
    recommended_level integer not null check (recommended_level between 1 and 5),
    recommendation text not null check (char_length(recommendation) between 1 and 500),
    feature_snapshot jsonb not null default '{}'::jsonb
        check (jsonb_typeof(feature_snapshot) = 'object'),
    foreign key (player_id, owner_id)
        references public.learner_profiles(player_id, owner_id) on delete cascade
);

create index if not exists learner_forecasts_owner_player_time_idx
    on public.learner_forecasts(owner_id, player_id, generated_at desc);
create index if not exists learner_forecasts_owner_player_skill_time_idx
    on public.learner_forecasts(owner_id, player_id, skill, generated_at desc);
create index if not exists learner_forecasts_expiry_idx
    on public.learner_forecasts(valid_until);

alter table public.learner_forecasts enable row level security;

revoke all on table public.learner_forecasts from public;
revoke all on table public.learner_forecasts from anon;
revoke insert, update, delete on table public.learner_forecasts from authenticated;
grant select on table public.learner_forecasts to authenticated;
grant all on table public.learner_forecasts to service_role;

drop policy if exists learner_forecasts_select_own on public.learner_forecasts;
create policy learner_forecasts_select_own
on public.learner_forecasts
for select
to authenticated
using ((select auth.uid()) = owner_id);

create or replace view public.learner_skill_features
with (security_invoker = true)
as
with ranked_attempts as (
    select
        qa.owner_id,
        qa.player_id,
        qa.skill,
        qa.session_id,
        qa.level,
        qa.difficulty,
        qa.is_correct,
        qa.timed_out,
        qa.response_time_ms,
        qa.attempted_at_utc,
        row_number() over (
            partition by qa.owner_id, qa.player_id, qa.skill
            order by qa.attempted_at_utc desc, qa.attempt_id desc
        ) as recency_rank
    from public.question_attempts as qa
),
aggregated as (
    select
        owner_id,
        player_id,
        skill,
        count(*)::integer as total_attempts,
        count(distinct session_id)::integer as total_sessions,
        count(*) filter (where is_correct)::integer as correct_attempts,
        count(*) filter (where timed_out)::integer as timed_out_attempts,
        round(avg(is_correct::integer)::numeric, 4) as overall_accuracy,
        round(avg(response_time_ms)::numeric, 2) as average_response_time_ms,
        round(avg(difficulty)::numeric, 2) as average_difficulty,
        max(level)::integer as highest_level_attempted,
        min(attempted_at_utc) as first_attempt_at,
        max(attempted_at_utc) as last_attempt_at,
        count(*) filter (where recency_rank <= 10)::integer as recent_attempt_count,
        round(
            avg(is_correct::integer) filter (where recency_rank <= 10)::numeric,
            4
        ) as recent_accuracy,
        round(
            avg(response_time_ms) filter (where recency_rank <= 10)::numeric,
            2
        ) as recent_average_response_time_ms,
        count(*) filter (where recency_rank between 11 and 20)::integer
            as previous_attempt_count,
        round(
            avg(is_correct::integer) filter (where recency_rank between 11 and 20)::numeric,
            4
        ) as previous_accuracy
    from ranked_attempts
    group by owner_id, player_id, skill
),
features as (
    select
        aggregated.*,
        case
            when previous_attempt_count >= 5
                then round(recent_accuracy - previous_accuracy, 4)
            else null
        end as accuracy_trend_delta
    from aggregated
)
select
    owner_id,
    player_id,
    skill,
    total_attempts,
    total_sessions,
    correct_attempts,
    timed_out_attempts,
    overall_accuracy,
    average_response_time_ms,
    average_difficulty,
    highest_level_attempted,
    first_attempt_at,
    last_attempt_at,
    recent_attempt_count,
    recent_accuracy,
    recent_average_response_time_ms,
    previous_attempt_count,
    previous_accuracy,
    accuracy_trend_delta,
    (total_attempts >= 10 and total_sessions >= 2) as is_forecast_eligible,
    greatest(0, 10 - total_attempts)::integer as attempts_needed,
    case
        when total_attempts < 10 then 'needs_more_attempts'
        when total_sessions < 2 then 'needs_more_sessions'
        else 'ready'
    end as data_quality_status,
    case
        when accuracy_trend_delta is null then 'insufficient_history'
        when accuracy_trend_delta >= 0.10 then 'improving'
        when accuracy_trend_delta <= -0.10 then 'declining'
        else 'stable'
    end as observed_trend
from features;

revoke all on table public.learner_skill_features from public;
revoke all on table public.learner_skill_features from anon;
grant select on table public.learner_skill_features to authenticated;
grant select on table public.learner_skill_features to service_role;

create or replace view public.current_learner_forecasts
with (security_invoker = true)
as
select distinct on (owner_id, player_id, skill)
    forecast_id,
    owner_id,
    player_id,
    skill,
    model_version,
    generated_at,
    valid_until,
    source_attempt_count,
    expected_score_percent,
    mastery_probability,
    confidence,
    trend_label,
    risk_level,
    recommended_level,
    recommendation
from public.learner_forecasts
where valid_until > now()
order by owner_id, player_id, skill, generated_at desc, forecast_id desc;

revoke all on table public.current_learner_forecasts from public;
revoke all on table public.current_learner_forecasts from anon;
grant select on table public.current_learner_forecasts to authenticated;
grant select on table public.current_learner_forecasts to service_role;

commit;
