-- ============================================================
-- Supabase Auth 연동 테스트 (0004_auth.sql)
--
-- 가입 → 프로필 자동 생성 → 탈퇴 시 데이터 정리까지 확인한다.
-- ============================================================

\set ON_ERROR_STOP on

do $$
declare
  u    uuid := '44444444-4444-4444-4444-444444444444';
  u2   uuid := '55555555-5555-5555-5555-555555555555';
  u3   uuid := '66666666-6666-6666-6666-666666666666';
  ghost uuid := '99999999-9999-9999-9999-999999999999';
  n    int;
  bd   date;
  blocked boolean;
begin
  raise notice '';
  raise notice '=== Auth 연동 테스트 ===';

  ---------------------------------------------------------------
  -- 1. 가입하면 프로필이 자동으로 생긴다
  ---------------------------------------------------------------
  insert into auth.users (id, email) values (u, 'new@example.com');
  select count(*) into n from profiles where user_id = u;
  if n <> 1 then raise exception 'FAIL 1: 가입 후 프로필이 생성되지 않음'; end if;
  raise notice '  [PASS] 1 가입 시 프로필 자동 생성';

  ---------------------------------------------------------------
  -- 2. 생년월일이 가입 메타데이터에서 들어온다 (와이어프레임 A-01)
  ---------------------------------------------------------------
  insert into auth.users (id, email, raw_user_meta_data)
  values (u2, 'kid@example.com', '{"birth_date":"2010-03-15"}'::jsonb);
  select birth_date into bd from profiles where user_id = u2;
  if bd is distinct from date '2010-03-15' then
    raise exception 'FAIL 2: 생년월일이 전달되지 않음 (%)', bd;
  end if;
  raise notice '  [PASS] 2 가입 메타데이터의 생년월일 반영 (결제 한도 판단용)';

  ---------------------------------------------------------------
  -- 3. 존재하지 않는 계정으로 프로필을 만들 수 없다
  ---------------------------------------------------------------
  blocked := false;
  begin
    perform ensure_profile(ghost);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 3: 유령 계정으로 프로필 생성됨'; end if;

  blocked := false;
  begin
    insert into profiles (user_id) values (ghost);
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 3b: FK 없이 프로필이 삽입됨'; end if;
  raise notice '  [PASS] 3 유령 계정 차단 (FK + ensure_profile 가드)';

  ---------------------------------------------------------------
  -- 4. ensure_profile은 멱등이다
  ---------------------------------------------------------------
  perform ensure_profile(u);
  perform ensure_profile(u);
  select count(*) into n from profiles where user_id = u;
  if n <> 1 then raise exception 'FAIL 4: ensure_profile이 중복 생성 (%)', n; end if;
  raise notice '  [PASS] 4 ensure_profile 멱등';

  ---------------------------------------------------------------
  -- 5. 탈퇴하면 프로필도 사라진다
  ---------------------------------------------------------------
  delete from auth.users where id = u2;
  select count(*) into n from profiles where user_id = u2;
  if n <> 0 then raise exception 'FAIL 5: 탈퇴 후에도 프로필이 남음'; end if;
  raise notice '  [PASS] 5 탈퇴 시 프로필 cascade 삭제';

  ---------------------------------------------------------------
  -- 6. 원장이 있어도 탈퇴할 수 있고, 기록은 익명으로 남는다 (G-01)
  ---------------------------------------------------------------
  -- "탈퇴 시 기부 집행 기록은 증빙 목적상 익명화 보존" — 지우는 게 아니라
  -- 신원만 끊고 금액·출처·시각은 남긴다 (0005_account_deletion.sql).
  perform ledger_append(u, 'point', 100, 'level', 'auth-test');
  select count(*) into n from ledger where user_id = u;
  if n = 0 then raise exception 'FAIL 6: 준비 실패 — 원장 항목이 없음'; end if;

  delete from auth.users where id = u;

  select count(*) into n from profiles where user_id = u;
  if n <> 0 then raise exception 'FAIL 6: 탈퇴 후 프로필이 남음'; end if;

  select count(*) into n from ledger where user_id = u;
  if n <> 0 then raise exception 'FAIL 6b: 원장에 신원이 남아 있음 (%건)', n; end if;

  select count(*) into n from ledger where user_id is null and ref = 'auth-test';
  if n <> 1 then raise exception 'FAIL 6c: 집행 증빙이 사라짐 — 익명 보존 실패 (%건)', n; end if;
  raise notice '  [PASS] 6 탈퇴 시 원장은 익명 보존 (금액·출처·시각 유지)';

  -- 이후 테스트용 살아있는 계정 (u2는 5번에서 이미 탈퇴시켰다)
  insert into auth.users (id, email) values (u3, 'live@example.com');
  perform ledger_append(u3, 'point', 300, 'level', 'live-1');

  ---------------------------------------------------------------
  -- 7. 익명화된 항목은 되돌릴 수 없다
  ---------------------------------------------------------------
  blocked := false;
  begin
    update ledger set user_id = u3 where user_id is null and ref = 'auth-test';
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 7: 익명 항목이 다른 계정에 재연결됨'; end if;
  raise notice '  [PASS] 7 익명화 항목 재연결 차단';

  ---------------------------------------------------------------
  -- 8. 익명 항목은 잔액·랭킹에서 빠진다
  ---------------------------------------------------------------
  select count(*) into n from balances where user_id is null;
  if n <> 0 then raise exception 'FAIL 8: 잔액 뷰에 익명 항목이 집계됨'; end if;
  select count(*) into n from ranking_scores where user_id is null;
  if n <> 0 then raise exception 'FAIL 8b: 랭킹에 익명 항목이 집계됨'; end if;
  raise notice '  [PASS] 8 익명 항목은 잔액·랭킹 집계 제외';

  ---------------------------------------------------------------
  -- 9. append-only는 그대로다 (예외가 구멍이 되지 않았는지)
  ---------------------------------------------------------------
  blocked := false;
  begin
    update ledger set delta = 999999 where user_id = u3;
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 9: 익명화 예외가 금액 변조를 열어줌'; end if;

  blocked := false;
  begin
    update ledger set user_id = null, delta = 999999 where user_id = u3;
  exception when others then blocked := true;
  end;
  if not blocked then raise exception 'FAIL 9b: 익명화와 함께 금액 변조가 통과됨'; end if;
  raise notice '  [PASS] 9 익명화 예외가 append-only를 뚫지 않음';

  raise notice '';
  raise notice '=== 전체 통과 ===';
end;
$$;
