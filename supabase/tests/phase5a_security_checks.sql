-- Run after phase5a_forecasting_foundation.sql.

select relname, relrowsecurity
from pg_class
where relnamespace = 'public'::regnamespace
  and relname = 'learner_forecasts';
-- Expected: learner_forecasts, relrowsecurity = true.

select table_name, privilege_type
from information_schema.role_table_grants
where table_schema = 'public'
  and grantee = 'anon'
  and table_name in (
      'learner_forecasts',
      'learner_skill_features',
      'current_learner_forecasts'
  );
-- Expected: zero rows.

select
    has_table_privilege('authenticated', 'public.learner_forecasts', 'select')
        as authenticated_can_read_forecasts,
    has_table_privilege('authenticated', 'public.learner_forecasts', 'insert')
        as authenticated_can_insert_forecasts,
    has_table_privilege('service_role', 'public.learner_forecasts', 'insert')
        as service_role_can_insert_forecasts;
-- Expected: true, false, true.

select c.relname, c.reloptions
from pg_class as c
where c.relnamespace = 'public'::regnamespace
  and c.relname in ('learner_skill_features', 'current_learner_forecasts')
order by c.relname;
-- Expected: both views include security_invoker=true.

select policyname, roles, cmd
from pg_policies
where schemaname = 'public'
  and tablename = 'learner_forecasts';
-- Expected: one authenticated SELECT policy.
