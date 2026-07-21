-- ============================================================
-- 로컬 검증 전용 스텁 — 운영(Supabase)에는 배포하지 않는다.
--
-- Supabase가 기본 제공하는 것을 흉내낸다:
--   · auth 스키마와 auth.uid()
--   · anon 역할
--   · anon에게 public 테이블 전체 권한 부여
--
-- 마지막 항목이 중요하다. Supabase는 anon에게 테이블 권한을 넉넉히 주고
-- **RLS만으로 접근을 통제한다.** 따라서 로컬 테스트도 같은 조건이어야
-- "권한이 없어서 막힌 것"과 "RLS가 막은 것"을 혼동하지 않는다.
-- ============================================================

create schema if not exists auth;

create or replace function auth.uid() returns uuid
language sql stable as $$
  select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid
$$;

do $$ begin
  if not exists (select 1 from pg_roles where rolname = 'anon') then
    create role anon nologin;
  end if;
end $$;

grant usage on schema public, auth to anon;
