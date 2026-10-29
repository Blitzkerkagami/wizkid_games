-- Run in the Supabase SQL Editor after phase4_learning_sync.sql.
-- Every query should return the expected value shown in its comment.

select relname, relrowsecurity
from pg_class
where relnamespace = 'public'::regnamespace
  and relname in ('learner_profiles', 'gameplay_sessions', 'question_attempts', 'level_results')
order by relname;
-- Expected: four rows, all relrowsecurity = true.

select table_name, privilege_type
from information_schema.role_table_grants
where table_schema = 'public'
  and grantee = 'anon'
  and table_name in ('learner_profiles', 'gameplay_sessions', 'question_attempts', 'level_results');
-- Expected: zero rows.

select tablename, policyname, roles, cmd
from pg_policies
where schemaname = 'public'
  and tablename in ('learner_profiles', 'gameplay_sessions', 'question_attempts', 'level_results')
order by tablename, cmd;
-- Expected: SELECT, INSERT, and UPDATE policies for authenticated on every table.

select has_function_privilege('anon', 'public.sync_learning_batch(jsonb)', 'execute') as anon_can_sync,
       has_function_privilege('authenticated', 'public.sync_learning_batch(jsonb)', 'execute') as authenticated_can_sync;
-- Expected: anon_can_sync = false, authenticated_can_sync = true.
