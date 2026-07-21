-- ============================================================
-- 육포 충전 (F-05, PRD §7.7)
--
-- 결제 한도는 클라 표시용이 아니라 **서버가 강제**한다.
-- 지금은 모든 사용자에게 같은 기본값 하나다. PRD §7.7의 연령별 한도는
-- 실제 PG를 붙이는 시점에 넣는다 — 모의 결제에는 적용할 대상이 없다.
--
-- 실제 PG 연동은 없다(자사몰 미구축). 데모는 모의 결제이며,
-- 화면에 그 사실을 항상 표기한다 (§6.5 정직성 규칙과 같은 태도).
-- ============================================================

create table if not exists jerky_packs (
  sku         text primary key,
  jerky       int  not null check (jerky > 0),
  krw         int  not null check (krw > 0),
  bonus_note  text,                        -- "+10 보너스" 등
  best        boolean not null default false,   -- pack-card-best 자산으로 강조
  sort_order  int not null default 0,
  active      boolean not null default true
);

create table if not exists payment_limits (
  user_id      uuid primary key references profiles(user_id) on delete cascade,
  monthly_cap  int not null check (monthly_cap >= 0),
  -- 상향은 즉시 적용하지 않는다 (충동 과금 방지, 와이어프레임 G-03)
  pending_cap  int,
  pending_at   timestamptz,
  updated_at   timestamptz not null default now()
);

create table if not exists jerky_purchases (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null references profiles(user_id) on delete cascade,
  sku         text not null references jerky_packs(sku),
  krw         int not null,
  jerky       int not null,
  mock        boolean not null default true,   -- 데모 모의 결제 표시
  created_at  timestamptz not null default now()
);

create index if not exists jerky_purchases_month on jerky_purchases (user_id, created_at desc);

-- ---------- 이번 달 결제액 ----------
create or replace function monthly_spent(p_user uuid default auth.uid())
returns int
language sql stable
security definer
set search_path = public
as $$
  select coalesce(sum(krw), 0)::int from jerky_purchases
   where user_id = p_user and created_at >= date_trunc('month', now())
$$;

-- ---------- 한도 조회 (F-05 게이지) ----------
create or replace function my_payment_limit()
returns jsonb
language plpgsql
stable
security definer
set search_path = public
as $$
declare
  v_user  uuid := auth.uid();
  v_cap   int;
  v_spent int;
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;
  v_spent := monthly_spent(v_user);

  select monthly_cap into v_cap from payment_limits where user_id = v_user;
  v_cap := coalesce(v_cap, 500000);

  return jsonb_build_object(
    'cap', v_cap, 'spent', v_spent,
    'remaining', greatest(0, v_cap - v_spent),
    'percent', case when v_cap = 0 then 100 else least(100, (v_spent * 100) / v_cap) end);
end;
$$;

-- ---------- 충전 (모의 결제) ----------
-- 실제 PG가 붙으면 이 함수는 웹훅 뒤로 옮긴다. 지금은 한도 강제와
-- 원장 기록이 제대로 도는지를 증명하는 것이 목적이다.
create or replace function purchase_jerky(p_sku text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user  uuid := auth.uid();
  v_pack  jerky_packs;
  v_limit jsonb;
  v_id    uuid;
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;

  select * into v_pack from jerky_packs where sku = p_sku and active;
  if not found then raise exception '판매 중인 상품이 아닙니다: %', p_sku; end if;

  -- 한도 초과는 서버가 막는다. 클라가 버튼을 비활성화하는 것과 별개다.
  v_limit := my_payment_limit();
  if (v_limit->>'remaining')::int < v_pack.krw then
    raise exception '이번 달 결제 한도를 초과합니다 (남은 한도 %원)', v_limit->>'remaining'
      using errcode = 'check_violation';
  end if;

  insert into jerky_purchases (user_id, sku, krw, jerky, mock)
  values (v_user, p_sku, v_pack.krw, v_pack.jerky, true)
  returning id into v_id;

  -- 육포 지급은 원장으로 (§5.5). ref로 멱등·추적 근거를 남긴다.
  perform ledger_append(v_user, 'jerky', v_pack.jerky, 'topup', 'pack:' || v_id::text);

  return jsonb_build_object(
    'ok', true, 'sku', p_sku, 'jerky', v_pack.jerky, 'krw', v_pack.krw,
    'mock', true, 'limit', my_payment_limit());
end;
$$;

-- ---------- 한도 변경 (G-03: 하향 즉시 / 상향 지연) ----------
create or replace function set_payment_cap(p_cap int)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user  uuid := auth.uid();
  v_cur   int;
  -- 냉각 기간은 서버가 정한다. 클라가 넘기게 두면 0으로 보내
  -- 상향 지연(G-03)을 무력화할 수 있다.
  v_delay interval := interval '24 hours';
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;
  if p_cap < 0 then raise exception '한도는 0 이상이어야 합니다'; end if;

  insert into payment_limits (user_id, monthly_cap) values (v_user, 500000)
  on conflict (user_id) do nothing;

  select monthly_cap into v_cur from payment_limits where user_id = v_user;

  if p_cap <= v_cur then
    -- 하향은 즉시 — 스스로 거는 제동은 미루지 않는다
    update payment_limits set monthly_cap = p_cap, pending_cap = null,
                              pending_at = null, updated_at = now()
     where user_id = v_user;
    return jsonb_build_object('applied', 'immediate', 'cap', p_cap);
  end if;

  update payment_limits set pending_cap = p_cap, pending_at = now() + v_delay,
                            updated_at = now()
   where user_id = v_user;
  return jsonb_build_object('applied', 'deferred', 'cap', v_cur,
                            'pendingCap', p_cap, 'pendingAt', now() + v_delay);
end;
$$;

-- 예약된 상향 적용 (CRON)
create or replace function apply_pending_caps() returns int
language plpgsql
security definer
set search_path = public
as $$
declare v_n int;
begin
  with upd as (
    update payment_limits
       set monthly_cap = pending_cap, pending_cap = null, pending_at = null, updated_at = now()
     where pending_cap is not null and pending_at <= now()
    returning 1)
  select count(*) into v_n from upd;
  return v_n;
end;
$$;

-- ---------- RLS ----------
alter table jerky_packs      enable row level security;
alter table payment_limits   enable row level security;
alter table jerky_purchases  enable row level security;
drop policy if exists public_read on jerky_packs;
drop policy if exists own_read    on payment_limits;
drop policy if exists own_read    on jerky_purchases;
create policy public_read on jerky_packs     for select using (auth.uid() is not null);
create policy own_read    on payment_limits  for select using (user_id = auth.uid());
create policy own_read    on jerky_purchases for select using (user_id = auth.uid());
-- 쓰기 정책 없음 — 충전·한도 변경은 위 함수로만

-- ---------- 시드 ----------
insert into jerky_packs (sku, jerky, krw, bonus_note, best, sort_order) values
  ('jerky-10',  10,  1200,  null,          false, 1),
  ('jerky-30',  30,  3300,  '+2 보너스',    false, 2),
  ('jerky-60',  60,  6300,  '+6 보너스',    true,  3),
  ('jerky-120', 120, 12000, '+15 보너스',   false, 4)
on conflict (sku) do update set
  jerky = excluded.jerky, krw = excluded.krw, bonus_note = excluded.bonus_note,
  best = excluded.best, sort_order = excluded.sort_order;

-- CRON: 예약된 한도 상향 적용 (매일 05:00 KST)
-- 로컬 검증 DB에는 pg_cron이 없다. 없으면 건너뛴다 — 마이그레이션이
-- 운영·로컬 양쪽에서 돌아야 테스트를 할 수 있다.
do $cron$
begin
  if exists (select 1 from pg_extension where extname = 'pg_cron') then
    perform cron.unschedule('apply-pending-caps')
      where exists (select 1 from cron.job where jobname = 'apply-pending-caps');
    perform cron.schedule('apply-pending-caps', '0 20 * * *',
                          'select apply_pending_caps()');
  else
    raise notice 'pg_cron 없음 — 한도 상향 CRON 등록을 건너뜁니다 (로컬 검증 환경)';
  end if;
end
$cron$;
