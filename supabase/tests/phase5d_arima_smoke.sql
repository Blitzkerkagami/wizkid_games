-- Plain-Postgres Phase 5D time-series smoke test. Leaves no test data.

begin;

insert into auth.users(id)
values ('55555555-5555-4555-8555-555555555555');

insert into public.learner_profiles (
    player_id, owner_id, display_name, age, wizard_index,
    created_at_utc, updated_at_utc, client_revision
) values (
    'aaaaaaaa-5555-4555-8555-aaaaaaaaaaaa',
    '55555555-5555-4555-8555-555555555555',
    'ARIMA Test Learner', 7, 1, now(), now(), 1
);

insert into public.gameplay_sessions (
    session_id, player_id, owner_id, activity, skill, level,
    started_at_utc, ended_at_utc, total_questions, attempted_questions,
    correct_answers, score_percent, passed, lives_remaining,
    completion_status, installation_id, app_version, client_revision
)
select
    ('bbbbbbbb-5555-4555-8555-' || lpad(sample_number::text, 12, '0'))::uuid,
    'aaaaaaaa-5555-4555-8555-aaaaaaaaaaaa'::uuid,
    '55555555-5555-4555-8555-555555555555'::uuid,
    'Quiz', 'Counting', least(5, sample_number),
    now() - ((7 - sample_number) || ' days')::interval,
    now() - ((7 - sample_number) || ' days')::interval + interval '5 minutes',
    5, 5, sample_number - 1, (sample_number - 1) * 20,
    sample_number >= 4, case when sample_number >= 4 then 2 else 0 end,
    case when sample_number >= 4 then 'completed' else 'failed' end,
    'cccccccc-5555-4555-8555-cccccccccccc'::uuid,
    '0.1', 1
from generate_series(1, 6) as samples(sample_number);

insert into public.question_attempts (
    attempt_id, session_id, player_id, owner_id, question_id,
    activity, skill, level, difficulty, competency, attempt_number,
    selected_answer_index, correct_answer_index, is_correct, timed_out,
    response_time_ms, attempted_at_utc, client_revision
)
select
    gen_random_uuid(),
    ('bbbbbbbb-5555-4555-8555-' || lpad(session_number::text, 12, '0'))::uuid,
    'aaaaaaaa-5555-4555-8555-aaaaaaaaaaaa'::uuid,
    '55555555-5555-4555-8555-555555555555'::uuid,
    'arima-question-' || session_number || '-' || question_number,
    'Quiz', 'Counting', least(5, session_number), least(5, session_number),
    'number-sense', question_number, 0, 0,
    question_number < session_number, false, 3000,
    now() - ((7 - session_number) || ' days')::interval
        + (question_number || ' seconds')::interval,
    1
from generate_series(1, 6) as sessions(session_number)
cross join generate_series(1, 5) as questions(question_number);

do $$
declare
    readiness record;
    series_count integer;
begin
    select * into readiness
    from public.learner_skill_features
    where owner_id = '55555555-5555-4555-8555-555555555555'
      and skill = 'Counting';

    select count(*) into series_count
    from public.learner_skill_arima_series
    where owner_id = '55555555-5555-4555-8555-555555555555'
      and skill = 'Counting';

    if readiness.is_forecast_eligible is not true
       or readiness.forecast_session_count <> 6
       or readiness.sessions_needed <> 0
       or series_count <> 6 then
        raise exception 'Unexpected Phase 5D output: readiness=%, series=%',
            row_to_json(readiness), series_count;
    end if;
end;
$$;

rollback;
