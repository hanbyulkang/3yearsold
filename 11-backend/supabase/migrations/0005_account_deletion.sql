-- ============================================================
-- 계정 삭제와 원장 보존의 충돌 해결
--
-- 문제: 0004에서 profiles를 auth.users에 cascade로 묶자, 원장이 있는 계정은
--       탈퇴 자체가 실패하게 됐다. ledger가 cascade로 DELETE되는데
--       append-only 트리거가 그 DELETE를 막기 때문이다.
--
-- 요구: 와이어프레임 G-01 — "탈퇴 시 기부 집행 기록은 증빙 목적상 익명화 보존"
--       즉 지우는 게 아니라 **신원만 끊고 금액·출처·시각은 남겨야** 한다.
--
-- 해법: ledger.user_id를 nullable로 두고 계정 삭제 시 null로 끊는다.
--       금액·출처·시각은 그대로 남으므로 집행 증빙이 유지되고,
--       개인과의 연결은 사라지므로 삭제 요구도 충족한다.
-- ============================================================

-- ---------- 1. 원장에서 신원만 끊을 수 있게 한다 ----------

alter table ledger alter column user_id drop not null;

alter table ledger drop constraint ledger_user_id_fkey;
alter table ledger
  add constraint ledger_user_id_fkey
  foreign key (user_id) references profiles(user_id) on delete set null;

-- ---------- 2. 트리거: 익명화만 예외로 허용한다 ----------
--
-- append-only는 그대로다. 허용하는 UPDATE는 단 하나,
-- "user_id를 null로 끊는 것"뿐이며 금액·출처·시각은 건드릴 수 없다.
create or replace function block_ledger_mutation() returns trigger
language plpgsql as $$
begin
  if tg_op = 'UPDATE'
     and old.user_id is not null
     and new.user_id is null
     and new.currency   is not distinct from old.currency
     and new.delta      is not distinct from old.delta
     and new.origin     is not distinct from old.origin
     and new.ref        is not distinct from old.ref
     and new.created_at is not distinct from old.created_at
  then
    return new;   -- 계정 삭제에 따른 익명화. 이것만 통과시킨다.
  end if;

  raise exception '원장은 append-only입니다. % 시도가 차단되었습니다 (id=%)',
    tg_op, coalesce(old.id, new.id);
end;
$$;

-- ---------- 3. 파생 뷰에서 익명 항목을 제외한다 ----------
-- 잔액·랭킹은 살아있는 계정의 것만 계산한다. 익명 항목은 집행 증빙으로만 남는다.

drop view if exists balances;
create view balances as
select user_id, currency, sum(delta)::int as amount
from ledger
where user_id is not null
group by user_id, currency;

drop view if exists ranking_scores;
create view ranking_scores as
select user_id,
       date_trunc('week', created_at) as week,
       sum(delta)::int as score
from ledger
where user_id is not null
  and currency = 'point'
  and delta > 0
  and origin in ('play', 'level')
group by user_id, date_trunc('week', created_at);

-- ---------- 4. 안전장치 ----------
-- 익명화 항목이 실수로 특정 사용자에게 다시 붙는 일을 막는다.
create or replace function ledger_no_reassign() returns trigger
language plpgsql as $$
begin
  if old.user_id is null and new.user_id is not null then
    raise exception '익명화된 원장 항목은 다시 계정에 연결할 수 없습니다 (id=%)', old.id;
  end if;
  return new;
end;
$$;

drop trigger if exists ledger_no_reassign_trg on ledger;
create trigger ledger_no_reassign_trg before update on ledger
  for each row execute function ledger_no_reassign();
