-- ============================================================
-- 견종 화이트리스트 (와이어프레임 A-09)
--
-- "견종은 사전 정의 목록에서만 — LLM 응답을 화이트리스트 검증."
-- LLM이 목록에 없는 견종을 만들어내면 캐릭터견 생성이 깨지므로,
-- 목록을 서버에 두고 응답을 대조한다. 클라에 복제하지 않는다(A-05·B-04).
--
-- personality 기본값은 A-10의 "견종 선택에 따라 기본값 프리필"에 쓴다.
--   activity 활동성 / timid 겁많음 / affection 애정표현  (각 1~5)
-- 이 값이 돌봄 요구량 계산과 AI 페르소나 연출의 입력이 된다.
-- ============================================================

insert into config (key, value) values
('breeds', jsonb_build_object(
  -- 항상 후보에 포함할 견종. 3D 에셋이 준비된 견종을 고정해 데모에서 캐릭터견이
  -- 반드시 렌더되도록 한다. 에셋이 늘어나면 이 배열만 비우거나 바꾼다.
  'pinned', jsonb_build_array('보더콜리'),
  'list', jsonb_build_array(
    jsonb_build_object('name','믹스견',        'activity',3,'timid',3,'affection',4),
    jsonb_build_object('name','진돗개',        'activity',4,'timid',2,'affection',3),
    jsonb_build_object('name','시바견',        'activity',4,'timid',2,'affection',2),
    jsonb_build_object('name','포메라니안',     'activity',4,'timid',3,'affection',4),
    jsonb_build_object('name','말티즈',        'activity',3,'timid',3,'affection',5),
    jsonb_build_object('name','푸들',          'activity',4,'timid',2,'affection',4),
    jsonb_build_object('name','시츄',          'activity',2,'timid',2,'affection',4),
    jsonb_build_object('name','치와와',        'activity',3,'timid',4,'affection',4),
    jsonb_build_object('name','닥스훈트',       'activity',3,'timid',3,'affection',3),
    jsonb_build_object('name','요크셔테리어',   'activity',4,'timid',3,'affection',4),
    jsonb_build_object('name','비글',          'activity',5,'timid',1,'affection',4),
    jsonb_build_object('name','웰시코기',       'activity',4,'timid',2,'affection',4),
    jsonb_build_object('name','보더콜리',       'activity',5,'timid',1,'affection',3),
    jsonb_build_object('name','골든 리트리버',  'activity',4,'timid',1,'affection',5),
    jsonb_build_object('name','래브라도 리트리버','activity',4,'timid',1,'affection',5),
    jsonb_build_object('name','사모예드',       'activity',4,'timid',2,'affection',4)
  )
))
on conflict (key) do update set value = excluded.value, updated_at = now();
