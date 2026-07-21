-- ============================================================
-- 캐릭터견 생성 (A-09·A-10) + 스킨 상점 (F-01·F-02)
--
-- 둘 다 "클라가 값을 정해서 보내면 서버가 검증한다"는 같은 구조다.
--   · 견종은 breeds 테이블에 있는 것만 (A-09 화이트리스트)
--   · 성격 기본값은 서버가 breeds에서 꺼낸다 (A-10 프리필)
--   · 스킨 가격은 서버 카탈로그가 정한다. 클라가 보낸 가격을 믿지 않는다
-- ============================================================

-- ---------- 캐릭터견 생성 ----------
create or replace function create_character(
  p_name        text,
  p_breed       text,
  p_personality jsonb default null
) returns characters
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_b    breeds;
  v_p    jsonb;
  v_row  characters;
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;
  if coalesce(trim(p_name), '') = '' then raise exception '이름을 입력해주세요'; end if;

  -- 견종 화이트리스트 (A-09). 목록에 없으면 캐릭터를 만들 수 없다.
  select * into v_b from breeds where name = p_breed;
  if not found then raise exception '선택할 수 없는 견종입니다: %', p_breed; end if;

  -- 성격은 클라가 조정할 수 있으나 범위는 서버가 강제한다.
  -- 값을 안 보내면 견종 기본값(A-10 프리필)을 쓴다.
  v_p := jsonb_build_object(
    'activity',  least(5, greatest(1, coalesce((p_personality->>'activity')::int,  v_b.activity))),
    'timid',     least(5, greatest(1, coalesce((p_personality->>'timid')::int,     v_b.timid))),
    'affection', least(5, greatest(1, coalesce((p_personality->>'affection')::int, v_b.affection)))
  );

  insert into characters (user_id, name, breed, personality)
  values (v_user, trim(p_name), p_breed, v_p)
  on conflict (user_id) do update
    -- 생성 후 변경 불가가 원칙이나(A-10), 온보딩을 다시 태우는 데모 편의를 위해
    -- 덮어쓰기를 허용한다. 레벨·경험치는 보존한다.
    set name = excluded.name, breed = excluded.breed, personality = excluded.personality
  returning * into v_row;

  return v_row;
end;
$$;

-- ---------- 스킨 카탈로그 ----------
create table if not exists skins (
  sku          text primary key,
  title        text not null,
  kind         text not null check (kind in ('skin', 'set', 'coupon')),
  -- 가격은 둘 중 하나만 있다. 육포 상품은 jerky_price, 실물 세트는 krw_price.
  jerky_price  int check (jerky_price > 0),
  krw_price    int check (krw_price > 0),
  description  text,
  sort_order   int not null default 0,
  active       boolean not null default true,
  constraint skins_one_price check (num_nonnulls(jerky_price, krw_price) = 1)
);

create table if not exists skins_owned (
  user_id     uuid not null references profiles(user_id) on delete cascade,
  sku         text not null references skins(sku) on delete cascade,
  source      text not null check (source in ('jerky', 'point', 'commerce', 'event')),
  created_at  timestamptz not null default now(),
  primary key (user_id, sku)
);

alter table skins       enable row level security;
alter table skins_owned enable row level security;
create policy public_read on skins       for select using (auth.uid() is not null);
create policy own_read    on skins_owned for select using (user_id = auth.uid());
-- 쓰기 정책 없음 — 구매는 아래 함수로만 (클라가 스킨을 직접 만들 수 없다)

insert into skins (sku, title, kind, jerky_price, krw_price, description, sort_order) values
  ('skin-raincoat',  '노란 우비',      'skin', 8,    null, '비 오는 날 마당 연출이 바뀌어요', 1),
  ('set-winter',     '겨울 패딩 세트', 'set',  null, 39000, '실물 옷 배송 + 같은 디자인 스킨', 2),
  ('skin-scarf',     '체크 목도리',    'skin', 5,    null, '목에 두르는 포근한 목도리',      3),
  ('skin-cap',       '노란 캡모자',    'skin', 6,    null, '산책 나갈 때 씌워주세요',        4)
on conflict (sku) do update set
  title = excluded.title, kind = excluded.kind,
  jerky_price = excluded.jerky_price, krw_price = excluded.krw_price,
  description = excluded.description, sort_order = excluded.sort_order;

-- ---------- 육포로 스킨 구매 ----------
-- 가격을 클라에서 받지 않는다. sku만 받고 서버 카탈로그에서 가격을 읽는다.
create or replace function buy_skin_with_jerky(p_sku text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user  uuid := auth.uid();
  v_skin  skins;
  v_after int;
begin
  if v_user is null then raise exception '로그인이 필요합니다'; end if;

  select * into v_skin from skins where sku = p_sku and active;
  if not found then raise exception '판매 중인 상품이 아닙니다: %', p_sku; end if;
  if v_skin.jerky_price is null then
    raise exception '육포로 구매할 수 없는 상품입니다 (실물 결제 상품)';
  end if;

  if exists (select 1 from skins_owned where user_id = v_user and sku = p_sku) then
    return jsonb_build_object('alreadyOwned', true, 'sku', p_sku);
  end if;

  -- 잔액 부족은 ledger_append가 막는다 (음수 잔액 차단)
  perform ledger_append(v_user, 'jerky', -v_skin.jerky_price, 'shop', 'skin:' || p_sku);

  insert into skins_owned (user_id, sku, source) values (v_user, p_sku, 'jerky');

  select coalesce(sum(delta), 0)::int into v_after
    from ledger where user_id = v_user and currency = 'jerky';

  return jsonb_build_object('sku', p_sku, 'spent', v_skin.jerky_price, 'jerkyLeft', v_after);
end;
$$;

-- 내 육포 잔액 (상점 헤더용)
create or replace function my_jerky() returns int
language sql stable
security definer
set search_path = public
as $$
  select coalesce(sum(delta), 0)::int
    from ledger where user_id = auth.uid() and currency = 'jerky'
$$;
