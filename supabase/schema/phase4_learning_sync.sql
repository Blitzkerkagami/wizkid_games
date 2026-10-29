-- WizKid Phase 4: authenticated offline-to-cloud learning-data sync.
-- Apply this file in the Supabase SQL Editor before enabling Unity cloud sync.
-- The mobile app needs only the project URL and sb_publishable_... key.

begin;

create table if not exists public.learner_profiles (
    player_id uuid primary key,
    owner_id uuid not null references auth.users(id) on delete cascade,
    display_name text not null check (char_length(display_name) between 1 and 80),
    age integer not null check (age between 3 and 120),
    wizard_index integer not null default 0 check (wizard_index between 0 and 100),
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null,
    client_revision bigint not null default 1 check (client_revision > 0),
    cloud_updated_at timestamptz not null default now(),
    unique (player_id, owner_id)
);

create table if not exists public.gameplay_sessions (
    session_id uuid primary key,
    player_id uuid not null,
    owner_id uuid not null references auth.users(id) on delete cascade,
    activity text not null check (char_length(activity) between 1 and 80),
    skill text not null check (char_length(skill) between 1 and 80),
    level integer not null check (level between 1 and 5),
    started_at_utc timestamptz not null,
    ended_at_utc timestamptz,
    total_questions integer not null check (total_questions between 0 and 1000),
    attempted_questions integer not null default 0 check (attempted_questions between 0 and 1000),
    correct_answers integer not null default 0 check (correct_answers between 0 and 1000),
    score_percent integer check (score_percent between 0 and 100),
    passed boolean,
    lives_remaining integer not null default 3 check (lives_remaining between 0 and 3),
    completion_status text not null check (completion_status in ('in_progress', 'completed', 'failed', 'abandoned')),
    installation_id uuid not null,
    app_version text not null check (char_length(app_version) between 1 and 40),
    client_revision bigint not null default 1 check (client_revision > 0),
    cloud_updated_at timestamptz not null default now(),
    unique (session_id, player_id, owner_id),
    foreign key (player_id, owner_id)
        references public.learner_profiles(player_id, owner_id) on delete cascade
);

create table if not exists public.question_attempts (
    attempt_id uuid primary key,
    session_id uuid not null,
    player_id uuid not null,
    owner_id uuid not null references auth.users(id) on delete cascade,
    question_id text not null check (char_length(question_id) between 1 and 160),
    activity text not null check (char_length(activity) between 1 and 80),
    skill text not null check (char_length(skill) between 1 and 80),
    level integer not null check (level between 1 and 5),
    difficulty integer not null check (difficulty between 1 and 5),
    competency text not null default '' check (char_length(competency) <= 160),
    attempt_number integer not null check (attempt_number between 1 and 1000),
    selected_answer_index integer,
    correct_answer_index integer not null,
    is_correct boolean not null,
    timed_out boolean not null default false,
    response_time_ms bigint not null check (response_time_ms >= 0),
    attempted_at_utc timestamptz not null,
    client_revision bigint not null default 1 check (client_revision > 0),
    cloud_updated_at timestamptz not null default now(),
    foreign key (session_id, player_id, owner_id)
        references public.gameplay_sessions(session_id, player_id, owner_id) on delete cascade
);

create table if not exists public.level_results (
    result_id uuid primary key,
    session_id uuid not null,
    player_id uuid not null,
    owner_id uuid not null references auth.users(id) on delete cascade,
    activity text not null check (char_length(activity) between 1 and 80),
    skill text not null check (char_length(skill) between 1 and 80),
    level integer not null check (level between 1 and 5),
    total_questions integer not null check (total_questions between 0 and 1000),
    attempted_questions integer not null check (attempted_questions between 0 and 1000),
    correct_answers integer not null check (correct_answers between 0 and 1000),
    score_percent integer not null check (score_percent between 0 and 100),
    passed boolean not null,
    lives_remaining integer not null check (lives_remaining between 0 and 3),
    completed_at_utc timestamptz not null,
    client_revision bigint not null default 1 check (client_revision > 0),
    cloud_updated_at timestamptz not null default now(),
    unique (session_id),
    foreign key (session_id, player_id, owner_id)
        references public.gameplay_sessions(session_id, player_id, owner_id) on delete cascade
);

create index if not exists learner_profiles_owner_idx
    on public.learner_profiles(owner_id);
create index if not exists gameplay_sessions_owner_started_idx
    on public.gameplay_sessions(owner_id, started_at_utc desc);
create index if not exists gameplay_sessions_player_started_idx
    on public.gameplay_sessions(player_id, started_at_utc desc);
create index if not exists question_attempts_owner_time_idx
    on public.question_attempts(owner_id, attempted_at_utc desc);
create index if not exists question_attempts_player_time_idx
    on public.question_attempts(player_id, attempted_at_utc desc);
create index if not exists question_attempts_skill_level_time_idx
    on public.question_attempts(skill, level, attempted_at_utc desc);
create index if not exists level_results_owner_time_idx
    on public.level_results(owner_id, completed_at_utc desc);
create index if not exists level_results_player_time_idx
    on public.level_results(player_id, completed_at_utc desc);

alter table public.learner_profiles enable row level security;
alter table public.gameplay_sessions enable row level security;
alter table public.question_attempts enable row level security;
alter table public.level_results enable row level security;

revoke all on table public.learner_profiles from anon;
revoke all on table public.gameplay_sessions from anon;
revoke all on table public.question_attempts from anon;
revoke all on table public.level_results from anon;

grant select, insert, update on table public.learner_profiles to authenticated;
grant select, insert, update on table public.gameplay_sessions to authenticated;
grant select, insert, update on table public.question_attempts to authenticated;
grant select, insert, update on table public.level_results to authenticated;

drop policy if exists learner_profiles_select_own on public.learner_profiles;
create policy learner_profiles_select_own on public.learner_profiles
    for select to authenticated
    using ((select auth.uid()) = owner_id);
drop policy if exists learner_profiles_insert_own on public.learner_profiles;
create policy learner_profiles_insert_own on public.learner_profiles
    for insert to authenticated
    with check ((select auth.uid()) = owner_id);
drop policy if exists learner_profiles_update_own on public.learner_profiles;
create policy learner_profiles_update_own on public.learner_profiles
    for update to authenticated
    using ((select auth.uid()) = owner_id)
    with check ((select auth.uid()) = owner_id);

drop policy if exists gameplay_sessions_select_own on public.gameplay_sessions;
create policy gameplay_sessions_select_own on public.gameplay_sessions
    for select to authenticated
    using ((select auth.uid()) = owner_id);
drop policy if exists gameplay_sessions_insert_own on public.gameplay_sessions;
create policy gameplay_sessions_insert_own on public.gameplay_sessions
    for insert to authenticated
    with check ((select auth.uid()) = owner_id);
drop policy if exists gameplay_sessions_update_own on public.gameplay_sessions;
create policy gameplay_sessions_update_own on public.gameplay_sessions
    for update to authenticated
    using ((select auth.uid()) = owner_id)
    with check ((select auth.uid()) = owner_id);

drop policy if exists question_attempts_select_own on public.question_attempts;
create policy question_attempts_select_own on public.question_attempts
    for select to authenticated
    using ((select auth.uid()) = owner_id);
drop policy if exists question_attempts_insert_own on public.question_attempts;
create policy question_attempts_insert_own on public.question_attempts
    for insert to authenticated
    with check ((select auth.uid()) = owner_id);
drop policy if exists question_attempts_update_own on public.question_attempts;
create policy question_attempts_update_own on public.question_attempts
    for update to authenticated
    using ((select auth.uid()) = owner_id)
    with check ((select auth.uid()) = owner_id);

drop policy if exists level_results_select_own on public.level_results;
create policy level_results_select_own on public.level_results
    for select to authenticated
    using ((select auth.uid()) = owner_id);
drop policy if exists level_results_insert_own on public.level_results;
create policy level_results_insert_own on public.level_results
    for insert to authenticated
    with check ((select auth.uid()) = owner_id);
drop policy if exists level_results_update_own on public.level_results;
create policy level_results_update_own on public.level_results
    for update to authenticated
    using ((select auth.uid()) = owner_id)
    with check ((select auth.uid()) = owner_id);

create or replace function public.sync_learning_batch(payload jsonb)
returns jsonb
language plpgsql
security invoker
set search_path = ''
as $$
declare
    v_owner_id uuid := (select auth.uid());
    v_item jsonb;
    v_profiles integer := 0;
    v_sessions integer := 0;
    v_attempts integer := 0;
    v_results integer := 0;
begin
    if v_owner_id is null then
        raise exception 'Authentication is required.' using errcode = '42501';
    end if;

    if jsonb_typeof(payload) <> 'object' then
        raise exception 'payload must be a JSON object.' using errcode = '22023';
    end if;

    if jsonb_array_length(coalesce(payload->'profiles', '[]'::jsonb)) > 100
       or jsonb_array_length(coalesce(payload->'sessions', '[]'::jsonb)) > 100
       or jsonb_array_length(coalesce(payload->'attempts', '[]'::jsonb)) > 100
       or jsonb_array_length(coalesce(payload->'results', '[]'::jsonb)) > 100 then
        raise exception 'Each payload array is limited to 100 records.' using errcode = '22023';
    end if;

    for v_item in select value from jsonb_array_elements(coalesce(payload->'profiles', '[]'::jsonb)) loop
        insert into public.learner_profiles (
            player_id, owner_id, display_name, age, wizard_index,
            created_at_utc, updated_at_utc, client_revision, cloud_updated_at
        ) values (
            (v_item->>'player_id')::uuid, v_owner_id,
            left(v_item->>'display_name', 80), (v_item->>'age')::integer,
            (v_item->>'wizard_index')::integer,
            (v_item->>'created_at_utc')::timestamptz,
            (v_item->>'updated_at_utc')::timestamptz,
            (v_item->>'client_revision')::bigint, now()
        )
        on conflict (player_id) do update set
            display_name = excluded.display_name,
            age = excluded.age,
            wizard_index = excluded.wizard_index,
            updated_at_utc = excluded.updated_at_utc,
            client_revision = excluded.client_revision,
            cloud_updated_at = now()
        where public.learner_profiles.owner_id = v_owner_id
          and excluded.client_revision >= public.learner_profiles.client_revision;
        v_profiles := v_profiles + 1;
    end loop;

    for v_item in select value from jsonb_array_elements(coalesce(payload->'sessions', '[]'::jsonb)) loop
        insert into public.gameplay_sessions (
            session_id, player_id, owner_id, activity, skill, level,
            started_at_utc, ended_at_utc, total_questions, attempted_questions,
            correct_answers, score_percent, passed, lives_remaining,
            completion_status, installation_id, app_version, client_revision,
            cloud_updated_at
        ) values (
            (v_item->>'session_id')::uuid, (v_item->>'player_id')::uuid, v_owner_id,
            left(v_item->>'activity', 80), left(v_item->>'skill', 80),
            (v_item->>'level')::integer, (v_item->>'started_at_utc')::timestamptz,
            nullif(v_item->>'ended_at_utc', '')::timestamptz,
            (v_item->>'total_questions')::integer,
            (v_item->>'attempted_questions')::integer,
            (v_item->>'correct_answers')::integer,
            nullif((v_item->>'score_percent')::integer, -1),
            case when (v_item->>'passed')::integer = -1 then null
                 else (v_item->>'passed')::integer = 1 end,
            (v_item->>'lives_remaining')::integer,
            v_item->>'completion_status', (v_item->>'installation_id')::uuid,
            left(v_item->>'app_version', 40), (v_item->>'client_revision')::bigint,
            now()
        )
        on conflict (session_id) do update set
            ended_at_utc = excluded.ended_at_utc,
            attempted_questions = excluded.attempted_questions,
            correct_answers = excluded.correct_answers,
            score_percent = excluded.score_percent,
            passed = excluded.passed,
            lives_remaining = excluded.lives_remaining,
            completion_status = excluded.completion_status,
            app_version = excluded.app_version,
            client_revision = excluded.client_revision,
            cloud_updated_at = now()
        where public.gameplay_sessions.owner_id = v_owner_id
          and excluded.client_revision >= public.gameplay_sessions.client_revision;
        v_sessions := v_sessions + 1;
    end loop;

    for v_item in select value from jsonb_array_elements(coalesce(payload->'attempts', '[]'::jsonb)) loop
        insert into public.question_attempts (
            attempt_id, session_id, player_id, owner_id, question_id, activity,
            skill, level, difficulty, competency, attempt_number,
            selected_answer_index, correct_answer_index, is_correct, timed_out,
            response_time_ms, attempted_at_utc, client_revision, cloud_updated_at
        ) values (
            (v_item->>'attempt_id')::uuid, (v_item->>'session_id')::uuid,
            (v_item->>'player_id')::uuid, v_owner_id,
            left(v_item->>'question_id', 160), left(v_item->>'activity', 80),
            left(v_item->>'skill', 80), (v_item->>'level')::integer,
            (v_item->>'difficulty')::integer, left(coalesce(v_item->>'competency', ''), 160),
            (v_item->>'attempt_number')::integer,
            nullif((v_item->>'selected_answer_index')::integer, -1),
            (v_item->>'correct_answer_index')::integer,
            (v_item->>'is_correct')::boolean, (v_item->>'timed_out')::boolean,
            (v_item->>'response_time_ms')::bigint,
            (v_item->>'attempted_at_utc')::timestamptz,
            (v_item->>'client_revision')::bigint, now()
        )
        on conflict (attempt_id) do update set
            cloud_updated_at = now()
        where public.question_attempts.owner_id = v_owner_id
          and excluded.client_revision >= public.question_attempts.client_revision;
        v_attempts := v_attempts + 1;
    end loop;

    for v_item in select value from jsonb_array_elements(coalesce(payload->'results', '[]'::jsonb)) loop
        insert into public.level_results (
            result_id, session_id, player_id, owner_id, activity, skill, level,
            total_questions, attempted_questions, correct_answers, score_percent,
            passed, lives_remaining, completed_at_utc, client_revision,
            cloud_updated_at
        ) values (
            (v_item->>'result_id')::uuid, (v_item->>'session_id')::uuid,
            (v_item->>'player_id')::uuid, v_owner_id,
            left(v_item->>'activity', 80), left(v_item->>'skill', 80),
            (v_item->>'level')::integer, (v_item->>'total_questions')::integer,
            (v_item->>'attempted_questions')::integer,
            (v_item->>'correct_answers')::integer,
            (v_item->>'score_percent')::integer, (v_item->>'passed')::boolean,
            (v_item->>'lives_remaining')::integer,
            (v_item->>'completed_at_utc')::timestamptz,
            (v_item->>'client_revision')::bigint, now()
        )
        on conflict (session_id) do update set
            attempted_questions = excluded.attempted_questions,
            correct_answers = excluded.correct_answers,
            score_percent = excluded.score_percent,
            passed = excluded.passed,
            lives_remaining = excluded.lives_remaining,
            completed_at_utc = excluded.completed_at_utc,
            client_revision = excluded.client_revision,
            cloud_updated_at = now()
        where public.level_results.owner_id = v_owner_id
          and excluded.client_revision >= public.level_results.client_revision;
        v_results := v_results + 1;
    end loop;

    return jsonb_build_object(
        'profiles', v_profiles,
        'sessions', v_sessions,
        'attempts', v_attempts,
        'results', v_results
    );
end;
$$;

revoke all on function public.sync_learning_batch(jsonb) from public;
revoke all on function public.sync_learning_batch(jsonb) from anon;
grant execute on function public.sync_learning_batch(jsonb) to authenticated;

commit;
