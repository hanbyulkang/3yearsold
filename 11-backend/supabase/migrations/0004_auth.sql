-- ============================================================
-- Supabase Auth 연동
--
-- profiles를 auth.users에 묶고, 가입 시 자동 생성되게 한다.
-- 클라이언트가 profiles를 직접 만들지 않는다 — RLS에 INSERT 정책이 없고,
-- 만들 수 있게 하면 남의 user_id로 행을 만드는 경로가 생긴다.
-- ============================================================

-- ---------- 1. 계정 삭제가 데이터까지 지우도록 묶는다 ----------
alter table profiles
  add constraint profiles_user_id_fkey
  foreign key (user_id) references auth.users(id) on delete cascade;

-- ---------- 2. 가입 시 profiles 자동 생성 ----------
--
-- 생년월일은 가입 시 1회 받는다 (와이어프레임 A-01).
-- 결제 한도·법정대리인 동의 판단에만 쓰고 기능 분기에는 쓰지 않는다 (D-014).
create or replace function handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (user_id, birth_date)
  values (
    new.id,
    -- 가입 폼에서 넘긴 메타데이터. 없으면 null로 두고 결제 시점에 다시 받는다.
    nullif(new.raw_user_meta_data ->> 'birth_date', '')::date
  )
  on conflict (user_id) do nothing;
  return new;
exception when others then
  -- 프로필 생성 실패가 회원가입 자체를 막으면 안 된다.
  -- 누락된 프로필은 첫 API 호출에서 ensure_profile()이 채운다.
  raise warning 'handle_new_user 실패 (user_id=%): %', new.id, sqlerrm;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function handle_new_user();

-- ---------- 3. 누락 보정 ----------
-- 트리거 이전에 가입한 계정, 혹은 트리거가 실패한 계정을 위한 안전망.
-- Edge Function이 요청 처음에 호출한다.
create or replace function ensure_profile(p_user uuid)
returns profiles
language plpgsql
security definer
set search_path = public
as $$
declare
  v profiles;
begin
  select * into v from profiles where user_id = p_user;
  if found then return v; end if;

  -- 실제로 존재하는 계정만 만든다. 임의의 uuid로 프로필을 만들 수 없다.
  if not exists (select 1 from auth.users where id = p_user) then
    raise exception '존재하지 않는 계정입니다';
  end if;

  insert into profiles (user_id) values (p_user)
  on conflict (user_id) do nothing;

  select * into v from profiles where user_id = p_user;
  return v;
end;
$$;

-- 기존 계정 보정 (배포 시점에 이미 가입한 사용자)
insert into profiles (user_id)
select id from auth.users
on conflict (user_id) do nothing;
