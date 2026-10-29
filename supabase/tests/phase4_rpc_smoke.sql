-- Plain-Postgres smoke test for the atomic RPC and RLS ownership boundary.
-- This runs inside a transaction and leaves no test data behind.

begin;
insert into auth.users(id) values
    ('11111111-1111-1111-1111-111111111111'),
    ('22222222-2222-2222-2222-222222222222');
set local role authenticated;
select set_config('request.jwt.claim.sub', '11111111-1111-1111-1111-111111111111', true);

select public.sync_learning_batch($json$
{
  "profiles": [{
    "player_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "display_name": "Test Learner",
    "age": 7,
    "wizard_index": 1,
    "created_at_utc": "2026-10-15T00:00:00Z",
    "updated_at_utc": "2026-10-15T00:00:00Z",
    "client_revision": 1
  }],
  "sessions": [{
    "session_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    "player_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "activity": "Quiz",
    "skill": "Counting",
    "level": 1,
    "started_at_utc": "2026-10-15T00:01:00Z",
    "ended_at_utc": "2026-10-15T00:02:00Z",
    "total_questions": 1,
    "attempted_questions": 1,
    "correct_answers": 1,
    "score_percent": 100,
    "passed": 1,
    "lives_remaining": 3,
    "completion_status": "completed",
    "installation_id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "app_version": "0.1",
    "client_revision": 2
  }],
  "attempts": [{
    "attempt_id": "dddddddd-dddd-dddd-dddd-dddddddddddd",
    "session_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    "player_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "question_id": "count-1",
    "activity": "Quiz",
    "skill": "Counting",
    "level": 1,
    "difficulty": 1,
    "competency": "one-to-one",
    "attempt_number": 1,
    "selected_answer_index": 0,
    "correct_answer_index": 0,
    "is_correct": true,
    "timed_out": false,
    "response_time_ms": 1200,
    "attempted_at_utc": "2026-10-15T00:01:30Z",
    "client_revision": 1
  }],
  "results": [{
    "result_id": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
    "session_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    "player_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "activity": "Quiz",
    "skill": "Counting",
    "level": 1,
    "total_questions": 1,
    "attempted_questions": 1,
    "correct_answers": 1,
    "score_percent": 100,
    "passed": true,
    "lives_remaining": 3,
    "completed_at_utc": "2026-10-15T00:02:00Z",
    "client_revision": 1
  }]
}
$json$::jsonb);

do $$
begin
    if (select count(*) from public.learner_profiles) <> 1
       or (select count(*) from public.gameplay_sessions) <> 1
       or (select count(*) from public.question_attempts) <> 1
       or (select count(*) from public.level_results) <> 1 then
        raise exception 'RPC did not insert the complete learning batch.';
    end if;
end;
$$;

select set_config('request.jwt.claim.sub', '22222222-2222-2222-2222-222222222222', true);

do $$
begin
    if (select count(*) from public.learner_profiles) <> 0
       or (select count(*) from public.gameplay_sessions) <> 0
       or (select count(*) from public.question_attempts) <> 0
       or (select count(*) from public.level_results) <> 0 then
        raise exception 'RLS exposed another owner''s learning data.';
    end if;
end;
$$;

rollback;
