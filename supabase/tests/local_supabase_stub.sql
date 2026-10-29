-- Local-only objects that let a plain PostgreSQL instance validate the Phase 4 SQL.
-- Supabase already provides these roles, schema, table, and helper in real projects.

do $$ begin
    if not exists (select 1 from pg_roles where rolname = 'anon') then
        create role anon nologin;
    end if;
    if not exists (select 1 from pg_roles where rolname = 'authenticated') then
        create role authenticated nologin;
    end if;
    if not exists (select 1 from pg_roles where rolname = 'service_role') then
        create role service_role nologin bypassrls;
    end if;
end $$;

create schema auth;
create table auth.users (
    id uuid primary key
);

create function auth.uid()
returns uuid
language sql
stable
as $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;

grant usage on schema auth to anon, authenticated;
grant execute on function auth.uid() to anon, authenticated;
