-- ============================================================
-- RLS 방어선 테스트
--
-- 전제: Unity WebGL은 anon key가 그대로 노출된다.
--       공격자는 anon 권한으로 임의의 SQL을 던질 수 있다.
--       Supabase는 anon에게 테이블 권한을 넉넉히 주므로,
--       실제 방어선은 RLS 하나뿐이다. 그것만으로 막히는지 확인한다.
-- ============================================================

\set ON_ERROR_STOP on

-- Supabase와 동일한 조건: anon에게 테이블 권한을 넉넉히 준다.
-- 막는 것은 권한이 아니라 RLS여야 한다.
grant select, insert, update, delete on all tables in schema public to anon;
grant usage, select on all sequences in schema public to anon;
grant execute on function ledger_append(uuid, currency, int, ledger_origin, text) to anon;

do $$
declare
  u          uuid := '11111111-1111-1111-1111-111111111111';
  other      uuid := '22222222-2222-2222-2222-222222222222';
  blocked    boolean;
  n          int;
  before_bal int;
  -- 쓰기가 막혔는지 판정한다. RLS는 INSERT/UPDATE에서 예외를 던지기도 하고,
  -- 정책 필터로 대상 행이 사라져 0행이 되기도 한다. 둘 다 "막힘"으로 본다.
begin
  raise notice '';
  raise notice '=== RLS 방어선 테스트 (anon 권한, 테이블 권한은 전부 부여됨) ===';

  select amount into before_bal from balances where user_id = u and currency = 'point';

  set local role anon;
  perform set_config('request.jwt.claim.sub', u::text, true);

  ---------------------------------------------------------------
  -- 1. 원장 직접 INSERT — 재화 무한 생성
  ---------------------------------------------------------------
  blocked := false;
  begin
    insert into ledger (user_id, currency, delta, origin) values (u, 'point', 999999, 'play');
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 1: 원장 직접 INSERT 성공 — 재화 무한 생성 가능'; end if;
  raise notice '  [PASS] 1 원장 직접 INSERT 차단';

  ---------------------------------------------------------------
  -- 2. 원장 UPDATE — 기존 항목 부풀리기
  ---------------------------------------------------------------
  blocked := false;
  begin
    update ledger set delta = 999999 where user_id = u;
    get diagnostics n = row_count;
    blocked := (n = 0);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 2: 원장 UPDATE 성공'; end if;
  raise notice '  [PASS] 2 원장 UPDATE 차단';

  ---------------------------------------------------------------
  -- 3. 캐릭터 레벨 직접 조작
  ---------------------------------------------------------------
  blocked := false;
  begin
    update characters set level = 99, exp = 99999 where user_id = u;
    get diagnostics n = row_count;
    blocked := (n = 0);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 3: 레벨 직접 조작 성공'; end if;
  raise notice '  [PASS] 3 캐릭터 레벨 조작 차단';

  ---------------------------------------------------------------
  -- 4. 발바닥 직접 충전
  ---------------------------------------------------------------
  blocked := false;
  begin
    update paw_state set count = 5, next_refill_at = null where user_id = u;
    get diagnostics n = row_count;
    blocked := (n = 0);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 4: 발바닥 직접 충전 성공'; end if;
  raise notice '  [PASS] 4 발바닥 직접 충전 차단';

  ---------------------------------------------------------------
  -- 5. 게임 점수 직접 삽입 (서버 검증 우회)
  ---------------------------------------------------------------
  blocked := false;
  begin
    insert into game_sessions (user_id, game, moves, claimed_score, verified_score, accepted)
    values (u, 'mg1', '[]'::jsonb, 99999, 99999, true);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 5: 검증된 점수 직접 삽입 성공'; end if;
  raise notice '  [PASS] 5 게임 점수 직접 삽입 차단';

  ---------------------------------------------------------------
  -- 6. 돌봄 기록 위조 (경험치 부당 획득)
  ---------------------------------------------------------------
  blocked := false;
  begin
    insert into care_logs (user_id, care_date, care_type, slot_no)
    values (u, current_date, 'walk', 99);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 6: 돌봄 기록 위조 성공'; end if;
  raise notice '  [PASS] 6 돌봄 기록 위조 차단';

  ---------------------------------------------------------------
  -- 7. 타인 데이터 조회
  ---------------------------------------------------------------
  select count(*) into n from ledger where user_id = other;
  if n <> 0 then raise exception 'FAIL 7: 타인의 원장이 보임 (%건)', n; end if;
  select count(*) into n from analyses where user_id = other;
  if n <> 0 then raise exception 'FAIL 7b: 타인의 분석 결과가 보임'; end if;
  raise notice '  [PASS] 7 타인 데이터 격리';

  ---------------------------------------------------------------
  -- 8. 보호견 데이터 변조 (가짜 보호견 등록)
  ---------------------------------------------------------------
  blocked := false;
  begin
    insert into shelter_animals (seq, name, animal_type) values (999999, '가짜', 'DOG');
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 8: 보호견 데이터 위조 성공'; end if;
  raise notice '  [PASS] 8 보호견 쓰기 차단 (CRON 전용)';

  ---------------------------------------------------------------
  -- 9. 서버 설정 변조 (전환 비율 조작)
  ---------------------------------------------------------------
  blocked := false;
  begin
    update config set value = '{"jerky_to_point": 999999}'::jsonb where key = 'economy';
    get diagnostics n = row_count;
    blocked := (n = 0);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 9: 전환 비율 조작 성공'; end if;
  raise notice '  [PASS] 9 서버 설정 변조 차단';

  ---------------------------------------------------------------
  -- 10. 허용된 경로는 정상 동작해야 한다 (과잉 차단 확인)
  ---------------------------------------------------------------
  insert into survey_responses (user_id, question_id, value)
  values (u, 'q1', '{"age":"20대"}'::jsonb)
  on conflict (user_id, question_id) do update set value = excluded.value;
  raise notice '  [PASS] 10 설문 저장은 정상 허용 (과잉 차단 아님)';

  reset role;

  select amount into n from balances where user_id = u and currency = 'point';
  if n <> before_bal then
    raise exception 'FAIL 11: 공격 후 잔액이 변함 (% → %)', before_bal, n;
  end if;
  raise notice '  [PASS] 11 공격 전후 잔액 불변 (%)', n;

  raise notice '';
  raise notice '=== 전체 통과 ===';
exception when others then
  reset role;
  raise;
end;
$$;
