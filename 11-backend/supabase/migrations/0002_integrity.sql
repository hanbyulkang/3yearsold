-- ============================================================
-- 경제 무결성 (PRD §5.5)
--
-- 여기가 이 백엔드의 핵심이다. Unity WebGL 클라는 코드가 통째로 노출되므로
-- "클라가 거짓말한다"를 전제로 서버가 전부 다시 계산하고 막는다.
-- ============================================================

-- ---------- 1. 원장은 수정·삭제할 수 없다 ----------
-- append-only를 애플리케이션 규율이 아니라 DB 제약으로 만든다.

create or replace function block_ledger_mutation() returns trigger
language plpgsql as $$
begin
  raise exception '원장은 append-only입니다. % 시도가 차단되었습니다 (id=%)',
    tg_op, coalesce(old.id, new.id);
end;
$$;

create trigger ledger_no_update before update on ledger
  for each row execute function block_ledger_mutation();

create trigger ledger_no_delete before delete on ledger
  for each row execute function block_ledger_mutation();


-- ---------- 2. 재화 증감의 유일한 통로 ----------
-- 잔액 음수 차단 + 멱등 + 일일 획득 상한을 한 곳에서 강제한다.
-- Edge Function은 이 함수만 호출한다. 테이블 직접 INSERT는 RLS로 막는다(0003).

create or replace function ledger_append(
  p_user     uuid,
  p_currency currency,
  p_delta    int,
  p_origin   ledger_origin,
  p_ref      text default null
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
  v_balance int;
  v_id      bigint;
  v_cap     int;
  v_earned  int;
begin
  if p_delta = 0 then
    raise exception '증감이 0인 원장 항목은 기록하지 않습니다';
  end if;

  -- 같은 유저의 원장 조작을 직렬화한다. 동시 요청으로 잔액 검사를 우회하는 것을 막는다.
  perform pg_advisory_xact_lock(hashtextextended(p_user::text, 0));

  -- 멱등: 같은 (유저, 출처, 참조)는 한 번만. 웹훅 재전송·중복 제출 방어.
  if p_ref is not null then
    select id into v_id from ledger
     where user_id = p_user and origin = p_origin and ref = p_ref;
    if found then
      return v_id;   -- 이미 반영됨. 조용히 기존 id를 돌려준다.
    end if;
  end if;

  -- 차감이면 잔액을 확인한다. 음수 잔액은 어떤 경로로도 만들 수 없다.
  if p_delta < 0 then
    select coalesce(sum(delta), 0) into v_balance
      from ledger where user_id = p_user and currency = p_currency;
    if v_balance + p_delta < 0 then
      raise exception '잔액 부족: % 보유 %, 요청 %', p_currency, v_balance, p_delta
        using errcode = 'check_violation';
    end if;
  end if;

  -- 일일 획득 상한 (PRD §5.5). 레벨별 상한은 config에서 읽는다 — 클라에 상수를 두지 않는다.
  if p_delta > 0 and p_currency = 'point' and p_origin in ('play', 'care') then
    select (value->>'daily_point_cap')::int into v_cap from config where key = 'economy';
    if v_cap is not null then
      select coalesce(sum(delta), 0) into v_earned
        from ledger
       where user_id = p_user and currency = 'point' and delta > 0
         and origin in ('play', 'care')
         and created_at >= date_trunc('day', now());
      if v_earned + p_delta > v_cap then
        raise exception '일일 획득 상한 초과: 오늘 %, 상한 %', v_earned, v_cap
          using errcode = 'check_violation';
      end if;
    end if;
  end if;

  insert into ledger (user_id, currency, delta, origin, ref)
  values (p_user, p_currency, p_delta, p_origin, p_ref)
  returning id into v_id;

  return v_id;
end;
$$;


-- ---------- 3. 재화 전환 (PRD §5.2 — 전부 단방향) ----------
-- 포인트 → 육포, 포인트 → 발바닥은 함수 자체가 존재하지 않는다.
-- 구조적으로 불가능하게 두는 것이 조건문으로 막는 것보다 안전하다.
--   · 포인트→육포 금지: 환금성 차단
--   · 포인트→발바닥 금지: 발바닥→게임→포인트→발바닥 무한 루프 차단

create or replace function convert_jerky_to_point(p_user uuid, p_jerky int)
returns int
language plpgsql
security definer
set search_path = public
as $$
declare
  v_rate  int;
  v_point int;
begin
  if p_jerky <= 0 then
    raise exception '전환 수량은 1 이상이어야 합니다';
  end if;

  select (value->>'jerky_to_point')::int into v_rate from config where key = 'economy';
  if v_rate is null then
    raise exception 'config.economy.jerky_to_point 가 없습니다';
  end if;

  v_point := p_jerky * v_rate;

  perform ledger_append(p_user, 'jerky', -p_jerky, 'convert', null);
  -- 전환으로 생긴 포인트는 origin=convert. 랭킹에서 제외된다(과금 유래).
  perform ledger_append(p_user, 'point', v_point, 'convert', null);

  return v_point;
end;
$$;


-- ---------- 4. 발바닥 (PRD §5.1) ----------
-- 시간 회복. 회복 시각은 서버가 계산해서 내려준다 (와이어프레임 C-01).

create or replace function paw_sync(p_user uuid)
returns paw_state
language plpgsql
security definer
set search_path = public
as $$
declare
  v_state    paw_state;
  v_interval interval;
  v_gained   int;
begin
  select (value->>'paw_refill_minutes')::int * interval '1 minute'
    into v_interval from config where key = 'economy';
  v_interval := coalesce(v_interval, interval '30 minutes');

  select * into v_state from paw_state where user_id = p_user for update;
  if not found then
    insert into paw_state (user_id, count) values (p_user, 5) returning * into v_state;
    return v_state;
  end if;

  -- 회복 시각이 지났으면 지난 만큼 채운다. 상한 5 (§5.1).
  if v_state.count < 5 and v_state.next_refill_at is not null and now() >= v_state.next_refill_at then
    v_gained := 1 + floor(extract(epoch from (now() - v_state.next_refill_at)) /
                          extract(epoch from v_interval))::int;
    v_state.count := least(5, v_state.count + v_gained);
    v_state.next_refill_at := case when v_state.count >= 5 then null else now() + v_interval end;
    update paw_state set count = v_state.count, next_refill_at = v_state.next_refill_at,
                         updated_at = now()
     where user_id = p_user returning * into v_state;
  end if;

  return v_state;
end;
$$;

create or replace function paw_consume(p_user uuid)
returns paw_state
language plpgsql
security definer
set search_path = public
as $$
declare
  v_state    paw_state;
  v_interval interval;
begin
  v_state := paw_sync(p_user);

  if v_state.count <= 0 then
    raise exception '발바닥이 부족합니다' using errcode = 'check_violation';
  end if;

  select (value->>'paw_refill_minutes')::int * interval '1 minute'
    into v_interval from config where key = 'economy';
  v_interval := coalesce(v_interval, interval '30 minutes');

  update paw_state
     set count = count - 1,
         -- 가득 차 있었으면 이제부터 회복 타이머가 돈다.
         next_refill_at = coalesce(next_refill_at, now() + v_interval),
         updated_at = now()
   where user_id = p_user
  returning * into v_state;

  return v_state;
end;
$$;


-- ---------- 5. 돌봄 기록 (와이어프레임 B-03 멱등) ----------
-- 같은 요구 슬롯에 경험치·포인트를 두 번 주지 않는다.

create or replace function care_perform(
  p_user      uuid,
  p_care_type text,
  p_slot_no   int,
  p_result    text default 'done'
) returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_today date := (now() at time zone 'Asia/Seoul')::date;
  v_exp   int;
begin
  -- 목욕 거부는 실패가 아니라 분기다 (와이어프레임 B-02).
  if p_result not in ('done', 'declined') then
    raise exception 'result 는 done 또는 declined 만 허용합니다';
  end if;

  insert into care_logs (user_id, care_date, care_type, slot_no, result)
  values (p_user, v_today, p_care_type, p_slot_no, p_result)
  on conflict (user_id, care_date, care_type, slot_no) do nothing;

  if not found then
    -- 이미 처리된 슬롯. 중복 지급 없이 현재 상태만 돌려준다.
    return jsonb_build_object('result', p_result, 'granted', false);
  end if;

  if p_result = 'declined' then
    return jsonb_build_object('result', 'declined', 'granted', false);
  end if;

  select (value->>'care_exp')::int into v_exp from config where key = 'economy';
  v_exp := coalesce(v_exp, 10);

  update characters set exp = exp + v_exp, last_care_at = now() where user_id = p_user;

  return jsonb_build_object('result', 'done', 'granted', true, 'exp', v_exp);
end;
$$;
