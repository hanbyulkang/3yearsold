-- ============================================================
-- 미니게임 서버 검증 (와이어프레임 C-01 ~ C-03)
--
-- 팀원의 LocalMockRewardClient(PlayerPrefs)를 대체할 서버 경로다.
-- 핵심은 "클라가 점수를 조작하면 잡히는가"(C-02, §5.5).
-- ============================================================

\set ON_ERROR_STOP on

do $$
declare
  u   uuid := '77777777-7777-7777-7777-777777777777';
  r   jsonb;
  n   int;
  sid uuid;
begin
  raise notice '';
  raise notice '=== 미니게임 서버 검증 ===';

  insert into auth.users (id, email) values (u, 'player@example.com') on conflict do nothing;
  insert into profiles (user_id) select id from auth.users on conflict do nothing;
  insert into characters (user_id, name, breed, personality)
  values (u, '보리', '보더콜리', '{"activity":5,"timid":1,"affection":3}')
  on conflict (user_id) do nothing;

  ---------------------------------------------------------------
  -- 1. 게임 시작 — 발바닥 차감 + 서버가 시드 발급
  ---------------------------------------------------------------
  perform paw_sync(u);
  update paw_state set count = 5, next_refill_at = null where user_id = u;

  r := game_start(u, 'mg1');
  sid := (r->>'sessionId')::uuid;
  if (r->>'seed') is null then raise exception 'FAIL 1: 시드가 발급되지 않음'; end if;
  select count into n from paw_state where user_id = u;
  if n <> 4 then raise exception 'FAIL 1b: 발바닥이 차감되지 않음 (%)', n; end if;
  raise notice '  [PASS] 1 게임 시작 — 발바닥 5→4, 서버 시드 발급';

  ---------------------------------------------------------------
  -- 2. 발바닥이 없으면 시작할 수 없다 (C-04 충전 시트로 유도)
  ---------------------------------------------------------------
  update paw_state set count = 0, next_refill_at = now() + interval '30 minutes' where user_id = u;
  begin
    perform game_start(u, 'mg1');
    raise exception 'FAIL 2: 발바닥 0인데 게임이 시작됨';
  exception when check_violation then null;
  end;
  raise notice '  [PASS] 2 발바닥 0이면 시작 차단';
  update paw_state set count = 5 where user_id = u;

  ---------------------------------------------------------------
  -- 3. 점수를 조작하면 지급되지 않는다 (클라 신뢰 금지)
  ---------------------------------------------------------------
  r := game_start(u, 'mg1'); sid := (r->>'sessionId')::uuid;
  -- 서버 재계산 300점인데 클라가 99999를 주장
  r := game_submit(u, sid, '[]'::jsonb, 99999, 300, 15);
  if (r->>'accepted')::boolean then raise exception 'FAIL 3: 조작된 점수가 승인됨'; end if;
  if (r->>'pointsAwarded')::int <> 0 then raise exception 'FAIL 3b: 조작인데 지급됨'; end if;
  select count(*) into n from ledger where user_id = u and origin = 'play';
  if n <> 0 then raise exception 'FAIL 3c: 조작 제출이 원장에 남음'; end if;
  raise notice '  [PASS] 3 점수 조작 시 미지급 (세션 기록은 남음)';

  ---------------------------------------------------------------
  -- 4. 정직한 제출은 지급된다
  ---------------------------------------------------------------
  r := game_start(u, 'mg1'); sid := (r->>'sessionId')::uuid;
  r := game_submit(u, sid, '[]'::jsonb, 300, 300, 15);
  if not (r->>'accepted')::boolean then raise exception 'FAIL 4: 정직한 제출이 거부됨'; end if;
  if (r->>'pointsAwarded')::int <> 15 then raise exception 'FAIL 4b: 지급액 불일치 (%)', r; end if;
  raise notice '  [PASS] 4 정직한 제출 → 뼈다귀 15 지급';

  ---------------------------------------------------------------
  -- 5. 같은 세션 재제출은 중복 지급되지 않는다
  ---------------------------------------------------------------
  r := game_submit(u, sid, '[]'::jsonb, 300, 300, 15);
  if (r->>'pointsAwarded')::int <> 0 then raise exception 'FAIL 5: 재제출로 중복 지급됨'; end if;
  select count(*) into n from ledger where user_id = u and origin = 'play';
  if n <> 1 then raise exception 'FAIL 5b: play 원장이 %건 (1이어야 함)', n; end if;
  raise notice '  [PASS] 5 재제출 중복 지급 차단 (멱등)';

  ---------------------------------------------------------------
  -- 6. 남의 세션은 제출할 수 없다
  ---------------------------------------------------------------
  begin
    perform game_submit('22222222-2222-2222-2222-222222222222', sid, '[]'::jsonb, 300, 300, 15);
    raise exception 'FAIL 6: 남의 세션 제출이 통과됨';
  exception when others then
    if sqlerrm like 'FAIL 6%' then raise; end if;
  end;
  raise notice '  [PASS] 6 타인 세션 제출 차단';

  ---------------------------------------------------------------
  -- 7. 랭킹에는 play 유래가 집계된다 (§5.5)
  ---------------------------------------------------------------
  select coalesce(sum(score), 0) into n from ranking_scores
   where user_id = u and week = date_trunc('week', now());
  if n < 15 then raise exception 'FAIL 7: 게임 보상이 랭킹에 반영되지 않음 (%)', n; end if;
  raise notice '  [PASS] 7 게임 보상이 랭킹에 집계 (play 유래)';

  raise notice '';
  raise notice '=== 전체 통과 ===';
end;
$$;
