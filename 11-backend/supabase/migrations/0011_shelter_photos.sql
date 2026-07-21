-- ============================================================
-- 보호견 대표 사진 (폴백)
--
-- vPetInfo에는 사진 필드가 없다. CONT 속 <img>는 보호소 담당자 PC의
-- 로컬 경로(C:\Users\...)라 쓸 수 없고, 서울시 사이트는 개방 API가 아니다.
--
-- 그래서 실제 개체 사진 대신 **견종 대표 사진**(라이선스 확보, Storage)을
-- 서버가 매핑해 내려준다. 화면은 반드시 "견종 대표 사진"임을 표기한다 —
-- 실제 그 아이의 사진처럼 보이면 입양 상담에서 사고가 난다 (D-03 정직성).
--
-- photo_is_breed_placeholder=false인 행이 생기면(실사진 확보 시)
-- 화면 라벨을 빼면 된다.
-- ============================================================

alter table shelter_animals add column if not exists photo_url text;
alter table shelter_animals add column if not exists photo_is_breed_placeholder boolean not null default true;

-- breed 문자열 → Storage 견종 사진. 새 견종·표기가 오면 여기만 늘린다.
create or replace function breed_placeholder_photo(p_breed text, p_type text)
returns text
language sql immutable
as $$
  select 'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/' ||
    case
      when p_type = 'CAT' then 'cat-shorthair.jpg'
      when p_breed like '%보더콜리%' then 'border-collie.jpg'
      when p_breed like '%리트리버%' then 'golden-retriever.jpg'
      when p_breed like '%비글%' then 'beagle.jpg'
      when p_breed like '%코기%' then 'welsh-corgi.jpg'
      when p_breed like '%푸들%' or p_breed like '%푸dle%' then 'poodle.jpg'
      when p_breed like '%포메%' then 'pomeranian.jpg'
      when p_breed like '%말티%' then 'maltese.jpg'
      when p_breed like '%시츄%' or p_breed like '%시추%' then 'shih-tzu.jpg'
      when p_breed like '%진도%' or p_breed like '%진돗%' then 'jindo.jpg'
      else 'mixed.jpg'
    end
$$;

update shelter_animals
   set photo_url = breed_placeholder_photo(coalesce(breed, ''), animal_type),
       photo_is_breed_placeholder = true
 where photo_url is null;

-- 동기화가 새 개체를 넣을 때도 자동으로 채워지게 한다
create or replace function shelter_photo_default() returns trigger
language plpgsql as $$
begin
  if new.photo_url is null then
    new.photo_url := breed_placeholder_photo(coalesce(new.breed, ''), new.animal_type);
    new.photo_is_breed_placeholder := true;
  end if;
  return new;
end;
$$;

drop trigger if exists shelter_photo_default_trg on shelter_animals;
create trigger shelter_photo_default_trg
  before insert or update of breed on shelter_animals
  for each row execute function shelter_photo_default();
