-- ============================================================
-- 미니게임 세션 (와이어프레임 C-01 ~ C-03)
--
-- 서버 재계산이 가능하려면 보드 시드를 서버가 발급해야 한다.
-- 클라가 시드를 정하면 유리한 판을 골라 만들 수 있다.
-- ============================================================

alter table game_sessions add column if not exists seed bigint;
alter table game_sessions add column if not exists started_at timestamptz not null default now();
alter table game_sessions add column if not exists submitted_at timestamptz;
alter table game_sessions alter column moves set default '[]'::jsonb;
alter table game_sessions alter column claimed_score set default 0;

-- ---------- 게임 시작 — 발바닥 차감 + 시드 발급 ----------
create or replace function game_start(p_user uuid, p_game text default 'mg1')
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_paw  paw_state;
  v_id   uuid;
  v_seed bigint;
begin
  -- 입장 시점에 차감한다 (와이어프레임 C-01)
  v_paw := paw_consume(p_user);

  -- 시드는 서버가 만든다. 클라가 고른 시드를 받지 않는다.
  v_seed := (random() * 2147483647)::bigint;

  insert into game_sessions (user_id, game, seed, moves, claimed_score)
  values (p_user, p_game, v_seed, '[]'::jsonb, 0)
  returning id into v_id;

  return jsonb_build_object('sessionId', v_id, 'seed', v_seed,
                            'paw', v_paw.count, 'nextRefillAt', v_paw.next_refill_at);
end;
$$;

-- ---------- 결과 확정 ----------
-- 점수 재계산은 Edge Function(_shared/game.ts)이 하고, 여기서는 결과를 못 박고 지급한다.
-- 지급은 origin='play', ref=세션id 로 멱등 (같은 세션 중복 제출 차단).
create or replace function game_submit(
  p_user     uuid,
  p_session  uuid,
  p_moves    jsonb,
  p_claimed  int,
  p_verified int,
  p_points   int
) returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_sess     game_sessions;
  v_accepted boolean := (p_claimed = p_verified);
  v_awarded  int := 0;
begin
  select * into v_sess from game_sessions
   where id = p_session and user_id = p_user for update;
  if not found then raise exception '세션을 찾을 수 없습니다'; end if;

  if v_sess.submitted_at is not null then
    -- 이미 제출된 세션. 재지급 없이 기존 결과를 돌려준다.
    return jsonb_build_object('accepted', v_sess.accepted, 'verifiedScore', v_sess.verified_score,
                              'pointsAwarded', 0, 'duplicate', true);
  end if;

  update game_sessions
     set moves = p_moves, claimed_score = p_claimed, verified_score = p_verified,
         accepted = v_accepted, submitted_at = now()
   where id = p_session;

  -- 점수를 조작했으면 지급하지 않는다. 다만 세션은 기록으로 남는다.
  if v_accepted and p_points > 0 then
    -- 일일 상한 초과는 ledger_append가 막는다. 그 경우 게임은 성공이되 지급만 0이다.
    begin
      perform ledger_append(p_user, 'point', p_points, 'play', p_session::text);
      v_awarded := p_points;
    exception when others then
      v_awarded := 0;
    end;
  end if;

  return jsonb_build_object('accepted', v_accepted, 'verifiedScore', p_verified,
                            'claimedScore', p_claimed, 'pointsAwarded', v_awarded);
end;
$$;
