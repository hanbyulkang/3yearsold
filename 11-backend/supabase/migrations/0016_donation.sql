-- ============================================================
-- 후원 (E-01 ~ E-04, PRD §6)
--
-- 원칙 (팀원이 DonData.cs에 못박아 둔 것)
--  · 잔액·기부·배분은 전부 원장에서만 움직인다 (§5.5). 클라 계산 금지.
--  · 공동 창고 게이지는 "모금액"이 아니라 참여량 집계다 (§6.1).
--  · 미달성 캠페인도 결과를 공개한다 (§6.5). 조용히 지우지 않는다.
--  · 데모는 모의 기부다. DONATION_MODE=mock 라벨을 숨기지 않는다 (§6.5).
--  · 증서 명의는 주간 랭킹 1위 — 과금 유래 제외 (§5.5·§6.3).
-- ============================================================

-- ---------- 공동 창고 캠페인 ----------
create table if not exists warehouse_campaigns (
  id           text primary key,
  title        text not null,           -- "6월 사료 200kg"
  goal_note    text not null,           -- "달성 시 사료 200kg 기부"
  goal_units   int  not null check (goal_units > 0),   -- 참여량 목표 (뼈다귀 총합)
  status       text not null default 'active'
               check (status in ('active', 'fulfilled', 'closed_short')),
  -- 집행 결과 (완료·미달 종료 시 채워진다). 미달도 공개한다 (§6.5).
  executed_note text,
  receipt_url   text,                   -- 보호소 수령 확인 사진 (§6.4)
  receipt_caption text,
  created_at   timestamptz not null default now()
);

-- ---------- 지정 후원 대상 ----------
create table if not exists donation_targets (
  id           text primary key,
  name         text not null,           -- "보리" 또는 "도봉구 보호소 전체"
  region       text,                    -- 시·군·구
  animal_seq   int references shelter_animals(seq) on delete set null,  -- 특정 보호견이면
  note         text,                    -- "봉사자 부족" 등
  -- 순환 배분(§6.5): 최근 배분받은 대상은 잠시 우선순위가 내려간다
  last_allocated_at timestamptz,
  active       boolean not null default true
);

-- ---------- 후원 원장 뷰 ----------
-- 재화 이동은 전부 ledger에 origin='donate'로 기록된다(이미 존재).
-- 여기서는 후원 관련 파생값만 뷰로 노출한다.

-- 내 공동 창고 기여 = origin=donate 이고 ref가 warehouse: 로 시작하는 차감의 합
create or replace function my_warehouse_contribution()
returns int
language sql stable
security definer
set search_path = public
as $$
  select coalesce(-sum(delta), 0)::int
    from ledger
   where user_id = auth.uid()
     and origin = 'donate'
     and ref like 'warehouse:%'
$$;

-- 캠페인 전체 참여량 = 모든 유저의 warehouse:{id} 차감 합
create or replace function campaign_progress(p_campaign text)
returns jsonb
language plpgsql
stable
security definer
set search_path = public
as $$
declare
  v_units int;
  v_goal  int;
  v_participants int;
begin
  select goal_units into v_goal from warehouse_campaigns where id = p_campaign;
  if v_goal is null then return null; end if;

  select coalesce(-sum(delta), 0)::int, count(distinct user_id)
    into v_units, v_participants
    from ledger
   where origin = 'donate' and ref = 'warehouse:' || p_campaign and delta < 0;

  return jsonb_build_object(
    'campaign', p_campaign,
    'units', v_units,
    'goal', v_goal,
    'percent', least(100, (v_units * 100) / v_goal),
    'participants', v_participants
  );
end;
$$;

-- ---------- 공동 창고 기부 ----------
create or replace function donate_to_warehouse(p_campaign text, p_amount int)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;
  if p_amount <= 0 then raise exception '기부 금액은 1 이상이어야 합니다'; end if;
  if not exists (select 1 from warehouse_campaigns where id = p_campaign and status = 'active') then
    raise exception '진행 중인 캠페인이 아닙니다: %', p_campaign;
  end if;

  -- 뼈다귀(point) 차감. 잔액 부족은 ledger_append가 막는다.
  -- ref에 campaign을 넣어 기여 집계·순환 배분의 근거로 삼는다.
  perform ledger_append(v_user, 'point', -p_amount, 'donate', 'warehouse:' || p_campaign);

  return jsonb_build_object('ok', true, 'amount', p_amount,
                            'progress', campaign_progress(p_campaign),
                            'myContribution', my_warehouse_contribution());
end;
$$;

-- ---------- 지정 후원 배분 ----------
create or replace function allocate_to_target(p_target text, p_amount int)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_name text;
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;
  if p_amount <= 0 then raise exception '배분 금액은 1 이상이어야 합니다'; end if;

  select name into v_name from donation_targets where id = p_target and active;
  if not found then raise exception '후원 대상이 아닙니다: %', p_target; end if;

  perform ledger_append(v_user, 'point', -p_amount, 'donate', 'target:' || p_target);

  -- 순환 배분(§6.5): 방금 배분받은 대상의 우선순위를 내린다
  update donation_targets set last_allocated_at = now() where id = p_target;

  return jsonb_build_object('ok', true, 'target', v_name, 'amount', p_amount);
end;
$$;

-- ---------- 주간 랭킹 1위 (증서 명의, §6.3) ----------
-- ranking_scores는 play·level만 집계한다(과금 유래 제외) — 이미 그렇게 정의됨(0005).
create or replace function weekly_top_holder()
returns jsonb
language sql stable
security definer
set search_path = public
as $$
  select jsonb_build_object('user_id', user_id, 'score', score)
    from ranking_scores
   where week = date_trunc('week', now())
   order by score desc
   limit 1
$$;

-- ---------- RLS ----------
alter table warehouse_campaigns enable row level security;
alter table donation_targets    enable row level security;
create policy public_read on warehouse_campaigns for select using (auth.uid() is not null);
create policy public_read on donation_targets    for select using (auth.uid() is not null);
-- 쓰기는 함수·service_role만

-- ---------- 시드 (데모용) ----------
-- 정직성: 완료 1건 + 미달 종료 1건을 둔다 (§6.5 — 미달도 공개)
insert into warehouse_campaigns (id, title, goal_note, goal_units, status, executed_note, receipt_caption) values
  ('jul-food-200kg', '7월 사료 200kg', '달성 시 사료 200kg 기부', 100000, 'active', null, null),
  ('jun-food-200kg', '6월 사료 200kg', '달성 시 사료 200kg 기부', 80000, 'fulfilled',
   '노원구 동물보호센터 · 7월 2일 수령 · 집행액 480,000원 · 참여 1,240명', '수령 사진'),
  ('jun-winter', '6월 방한용품 캠페인', '달성 시 방한용품 500벌', 50000, 'closed_short',
   '312/500벌로 종료 — 약정에 따라 브랜드가 200세트로 축소 집행했어요. 결과를 그대로 공개합니다.', null)
on conflict (id) do update set
  title = excluded.title, goal_note = excluded.goal_note, goal_units = excluded.goal_units,
  status = excluded.status, executed_note = excluded.executed_note, receipt_caption = excluded.receipt_caption;

insert into donation_targets (id, name, region, note) values
  ('t-nowon', '노원구 동물보호센터', '서울 노원구', '이번 달 후원 참여 12명'),
  ('t-dobong', '도봉구 보호소 전체', '서울 도봉구', '봉사자 부족')
on conflict (id) do update set
  name = excluded.name, region = excluded.region, note = excluded.note;
