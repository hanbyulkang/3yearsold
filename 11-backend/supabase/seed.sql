-- 서버 설정. 클라에 상수를 복제하지 않는다 (와이어프레임 A-05·B-04).
-- 밸런스 수치는 전부 가안이며 밸런스 시트 확정 대상이다 (PRD §5.3·C-04 주석).

insert into config (key, value) values
('economy', jsonb_build_object(
  'jerky_to_point',     100,    -- 육포 1 = 포인트 100 (가안, PRD §5.3)
  'jerky_to_paw',         2,    -- 육포 1 = 발바닥 2 (가안, 와이어프레임 C-04)
  'paw_refill_minutes',  30,
  'paw_max',              5,
  'daily_point_cap',   3000,    -- play·care 합산 일일 상한 (PRD §5.5)
  'care_exp',            10
)),
('level_curve', jsonb_build_object(
  'exp_base',           100,    -- 레벨 n → n+1 필요 경험치 = base * n
  'reward_base',         50,    -- 레벨업 일시금 = base * level
  -- 방치 하락 규칙 (와이어프레임 B-05). 굶주림·위험 연출 없이 비용만 만든다.
  'decay_grace_hours',   72,
  'decay_max_per_day',    1,
  'decay_floor',          1
))
on conflict (key) do update set value = excluded.value, updated_at = now();
