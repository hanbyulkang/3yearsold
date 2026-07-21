-- ============================================================
-- 클라이언트 헬퍼 + 뷰 RLS 우회 수정
--
-- [보안 수정] Postgres 뷰는 기본이 owner 권한 실행이라, ledger에 RLS가 있어도
-- balances·ranking_scores 뷰를 통하면 **모든 유저의 잔액이 보인다.**
-- security_invoker로 바꿔 뷰가 호출자의 RLS를 따르게 한다.
--
-- [헬퍼] Unity 클라가 "내 발바닥·뼈다귀"를 읽을 안전한 경로.
-- 파라미터로 user_id를 받지 않고 auth.uid()만 쓴다 — 남의 상태를 찍어볼 수 없다.
-- ============================================================

alter view balances       set (security_invoker = true);
alter view ranking_scores set (security_invoker = true);

-- 내 발바닥 상태 (시간 회복 반영). 로그인한 본인 것만.
create or replace function my_paw_status()
returns paw_state
language plpgsql
security definer
set search_path = public
as $$
declare
  v uuid := auth.uid();
begin
  if v is null then raise exception '로그인이 필요합니다'; end if;
  return paw_sync(v);
end;
$$;

-- 내 뼈다귀 잔액
create or replace function my_bones()
returns int
language sql stable
security definer
set search_path = public
as $$
  select coalesce(sum(delta), 0)::int
    from ledger
   where user_id = auth.uid() and currency = 'point'
$$;

-- MG1 데모 지급 상한 (D-023). 세션당 이 이상 지급되지 않는다.
update config
   set value = value || jsonb_build_object('mg1_session_bone_cap', 30),
       updated_at = now()
 where key = 'economy';
