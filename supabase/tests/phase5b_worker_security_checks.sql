-- Run after phase5b_forecasting_worker.sql.

select indexname, indexdef
from pg_indexes
where schemaname = 'public'
  and indexname = 'learner_forecasts_source_version_uidx';
-- Expected: one UNIQUE index covering owner, player, skill, model, and attempt count.

select
    has_table_privilege('anon', 'public.learner_skill_features', 'select')
        as anon_can_read_features,
    has_table_privilege('anon', 'public.learner_forecasts', 'insert')
        as anon_can_insert_forecasts,
    has_table_privilege('authenticated', 'public.learner_forecasts', 'insert')
        as authenticated_can_insert_forecasts,
    has_table_privilege('service_role', 'public.learner_skill_features', 'select')
        as service_can_read_features,
    has_table_privilege('service_role', 'public.learner_forecasts', 'insert')
        as service_can_insert_forecasts;
-- Expected: false, false, false, true, true.

