-- ============================================================
-- CRON 스케줄 (pg_cron + pg_net)
--
-- 지금까지 shelter-sync·shelter-traits를 손으로 호출해 왔다.
-- 데모 당일에 누가 잊으면 보호견 목록이 낡은 채로 나간다.
--
-- 보안: service_role 키를 SQL에 박지 않는다. Vault(supabase_vault)에 넣고
-- 실행 시점에 꺼내 쓴다 — 마이그레이션 파일이 저장소에 남기 때문이다.
-- ============================================================

create extension if not exists pg_cron;
create extension if not exists pg_net;

-- ---------- 잡 등록 헬퍼 ----------
-- Edge Function을 service_role 권한으로 호출한다.
-- 키는 Vault에서 읽으므로, 키가 없으면 잡이 조용히 실패하는 대신 로그를 남긴다.
create or replace function cron_invoke_function(p_name text)
returns void
language plpgsql
security definer
set search_path = public, extensions, vault
as $$
declare
  v_key text;
  v_url text;
begin
  select decrypted_secret into v_key
    from vault.decrypted_secrets where name = 'service_role_key';
  select decrypted_secret into v_url
    from vault.decrypted_secrets where name = 'project_url';

  if v_key is null or v_url is null then
    raise warning 'cron_invoke_function(%): Vault에 service_role_key/project_url이 없습니다', p_name;
    return;
  end if;

  perform net.http_post(
    url     := v_url || '/functions/v1/' || p_name,
    headers := jsonb_build_object(
                 'Content-Type', 'application/json',
                 'Authorization', 'Bearer ' || v_key),
    body    := '{}'::jsonb,
    timeout_milliseconds := 120000   -- shelter-traits는 LLM 호출이라 오래 걸린다
  );
end;
$$;

-- ---------- 스케줄 ----------
-- 시간은 UTC 기준이다 (KST = UTC+9).

-- 보호견 동기화 — 매일 새벽 4시 KST (19:00 UTC).
-- 보호소 공고가 업무시간에 갱신되므로 하루 한 번이면 충분하다.
select cron.unschedule('shelter-sync-daily') where exists (
  select 1 from cron.job where jobname = 'shelter-sync-daily');
select cron.schedule('shelter-sync-daily', '0 19 * * *',
                     $$select cron_invoke_function('shelter-sync')$$);

-- 성격 구조화 — 동기화 30분 뒤 (19:30 UTC).
-- 새로 들어온 개체만 처리하므로 평소엔 즉시 끝난다.
select cron.unschedule('shelter-traits-daily') where exists (
  select 1 from cron.job where jobname = 'shelter-traits-daily');
select cron.schedule('shelter-traits-daily', '30 19 * * *',
                     $$select cron_invoke_function('shelter-traits')$$);
