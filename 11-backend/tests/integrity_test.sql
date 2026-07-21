-- ============================================================
-- 경제 무결성 공격 테스트
--
-- "클라가 거짓말한다"를 전제로, 실제로 막히는지 확인한다.
-- 실패하면 즉시 예외를 던진다 (psql -v ON_ERROR_STOP=1 와 함께 실행).
-- ============================================================

\set QUIET on
\set ON_ERROR_STOP on

-- 테스트용 유저
insert into profiles (user_id) values
  ('11111111-1111-1111-1111-111111111111'),
  ('22222222-2222-2222-2222-222222222222')
on conflict do nothing;

do $$
declare
  u  uuid := '11111111-1111-1111-1111-111111111111';
  u2 uuid := '22222222-2222-2222-2222-222222222222';
  v_id  bigint;
  v_id2 bigint;
  v_bal int;
  v_ok  boolean;
  n     int;
begin
  raise notice '';
  raise notice '=== 경제 무결성 테스트 ===';

  ---------------------------------------------------------------
  -- 1. 원장 append-only
  ---------------------------------------------------------------
  v_id := ledger_append(u, 'point', 500, 'level', 'lv-2');

  v_ok := false;
  begin
    update ledger set delta = 999999 where id = v_id;
  exception when others then v_ok := true;
  end;
  if not v_ok then raise exception 'FAIL 1a: 원장 UPDATE가 차단되지 않음'; end if;
  raise notice '  [PASS] 1a 원장 UPDATE 차단';

  v_ok := false;
  begin
    delete from ledger where id = v_id;
  exception when others then v_ok := true;
  end;
  if not v_ok then raise exception 'FAIL 1b: 원장 DELETE가 차단되지 않음'; end if;
  raise notice '  [PASS] 1b 원장 DELETE 차단';

  ---------------------------------------------------------------
  -- 2. 잔액 음수 차단 — 가진 것보다 많이 쓰기
  ---------------------------------------------------------------
  v_ok := false;
  begin
    perform ledger_append(u, 'point', -10000, 'shop', null);
  exception when others then v_ok := true;
  end;
  if not v_ok then raise exception 'FAIL 2: 잔액을 초과한 차감이 통과됨'; end if;
  select amount into v_bal from balances where user_id = u and currency = 'point';
  if v_bal <> 500 then raise exception 'FAIL 2: 실패한 차감이 잔액을 바꿈 (%)', v_bal; end if;
  raise notice '  [PASS] 2  잔액 초과 차감 차단 (잔액 % 유지)', v_bal;

  ---------------------------------------------------------------
  -- 3. 멱등 — 같은 (유저·출처·참조)는 한 번만
  ---------------------------------------------------------------
  v_id2 := ledger_append(u, 'point', 500, 'level', 'lv-2');   -- 위와 동일한 ref
  if v_id2 <> v_id then raise exception 'FAIL 3: 중복 지급이 새 항목을 만듦'; end if;
  select amount into v_bal from balances where user_id = u and currency = 'point';
  if v_bal <> 500 then raise exception 'FAIL 3: 중복 지급으로 잔액이 늘어남 (%)', v_bal; end if;
  raise notice '  [PASS] 3  멱등 — 웹훅 재전송·중복 제출 방어';

  ---------------------------------------------------------------
  -- 4. 일일 획득 상한 (config: 3000)
  ---------------------------------------------------------------
  perform ledger_append(u2, 'point', 2900, 'play', 'g-1');
  v_ok := false;
  begin
    perform ledger_append(u2, 'point', 200, 'play', 'g-2');   -- 합 3100 > 3000
  exception when others then v_ok := true;
  end;
  if not v_ok then raise exception 'FAIL 4: 일일 상한이 강제되지 않음'; end if;
  raise notice '  [PASS] 4  일일 획득 상한 강제';

  -- 상한은 play·care에만. 레벨업 일시금은 막지 않는다.
  perform ledger_append(u2, 'point', 5000, 'level', 'lv-9');
  raise notice '  [PASS] 4b 레벨업 지급은 상한 대상 아님';

  ---------------------------------------------------------------
  -- 5. 단방향 전환 (PRD §5.2)
  ---------------------------------------------------------------
  perform ledger_append(u, 'jerky', 10, 'topup', 'pay-1');
  n := convert_jerky_to_point(u, 3);
  if n <> 300 then raise exception 'FAIL 5: 전환 비율 오류 (%)', n; end if;
  select amount into v_bal from balances where user_id = u and currency = 'jerky';
  if v_bal <> 7 then raise exception 'FAIL 5: 육포가 차감되지 않음 (%)', v_bal; end if;
  raise notice '  [PASS] 5  육포→포인트 전환 (육포 % 남음)', v_bal;

  -- 역방향 함수는 아예 존재하지 않아야 한다 (구조적 차단)
  if exists (select 1 from information_schema.routines
              where routine_schema = 'public'
                and routine_name in ('convert_point_to_jerky', 'convert_point_to_paw')) then
    raise exception 'FAIL 5b: 역방향 전환 함수가 존재함 — 환금성·무한루프 위험';
  end if;
  raise notice '  [PASS] 5b 역방향 전환 함수 부재 (환금성·무한루프 차단)';

  ---------------------------------------------------------------
  -- 6. 랭킹 — 과금 유래 제외, 기부 차감은 점수를 깎지 않음
  ---------------------------------------------------------------
  -- u 현재: level 500, convert 300  → 랭킹은 level 500만
  select coalesce(score, 0) into n from ranking_scores
   where user_id = u and week = date_trunc('week', now());
  if n <> 500 then
    raise exception 'FAIL 6: 랭킹에 과금 유래가 섞임 (기대 500, 실제 %)', n;
  end if;
  raise notice '  [PASS] 6  랭킹에서 과금 유래(convert) 제외';

  -- 기부로 포인트를 써도 랭킹 점수는 그대로 (역인센티브 방지, 와이어프레임 C-05)
  perform ledger_append(u, 'point', -400, 'donate', 'don-1');
  select coalesce(score, 0) into n from ranking_scores
   where user_id = u and week = date_trunc('week', now());
  if n <> 500 then
    raise exception 'FAIL 6b: 기부가 랭킹 점수를 깎음 (%)', n;
  end if;
  raise notice '  [PASS] 6b 기부해도 랭킹 점수 유지';

  ---------------------------------------------------------------
  -- 7. 발바닥 — 소비·하한
  ---------------------------------------------------------------
  perform paw_sync(u);
  for i in 1..5 loop perform paw_consume(u); end loop;
  select count into n from paw_state where user_id = u;
  if n <> 0 then raise exception 'FAIL 7: 발바닥이 0이 아님 (%)', n; end if;

  v_ok := false;
  begin
    perform paw_consume(u);
  exception when others then v_ok := true;
  end;
  if not v_ok then raise exception 'FAIL 7b: 발바닥 0에서 소비가 통과됨'; end if;
  if (select next_refill_at from paw_state where user_id = u) is null then
    raise exception 'FAIL 7c: 회복 시각이 설정되지 않음';
  end if;
  raise notice '  [PASS] 7  발바닥 소비·하한·회복시각 설정';

  ---------------------------------------------------------------
  -- 8. 돌봄 멱등 + 거부는 실패가 아님
  ---------------------------------------------------------------
  insert into characters (user_id, name, breed, personality)
  values (u, '두부', '믹스견', '{"activity":3,"timid":2,"affection":4}')
  on conflict do nothing;

  if (care_perform(u, 'walk', 1) ->> 'granted')::boolean is not true then
    raise exception 'FAIL 8: 첫 돌봄이 지급되지 않음';
  end if;
  if (care_perform(u, 'walk', 1) ->> 'granted')::boolean is not false then
    raise exception 'FAIL 8b: 같은 슬롯에 중복 지급됨';
  end if;
  raise notice '  [PASS] 8  돌봄 멱등 (같은 슬롯 중복 지급 차단)';

  if (care_perform(u, 'bath', 1, 'declined') ->> 'result') <> 'declined' then
    raise exception 'FAIL 8c: 목욕 거부가 declined로 기록되지 않음';
  end if;
  if exists (select 1 from care_logs where result = 'fail') then
    raise exception 'FAIL 8d: fail 상태가 존재함 (PRD §1.2 원칙 2 위반)';
  end if;
  raise notice '  [PASS] 8b 목욕 거부는 declined (실패 아님)';

  raise notice '';
  raise notice '=== 전체 통과 ===';
end;
$$;
