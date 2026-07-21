-- ============================================================
-- 보호견 실사진 (0011·0012 대체)
--
-- [정정] 0011은 "vPetInfo에 사진이 없다"고 단정하고 견종 대표 사진으로,
-- 0012는 유튜브 썸네일로 폴백했다. **둘 다 불필요했다.**
-- 서울 열린데이터광장에 `vPetImg` 서비스가 따로 있고, 우리 24마리 전건에
-- 실사진이 있다 (THUMB 24 + IMG 186, 전부 HTTP 200).
--
--   http://openapi.seoul.go.kr:8088/{KEY}/json/vPetImg/{start}/{end}/
--   { SEQ, IMG_TYPE: THUMB|IMG, IMG_NUM, IMG_URL }
--
-- SEQ가 vPetInfo와 같은 키다. THUMB이 마리당 1장이라 목록 대표 사진으로 쓰고,
-- IMG는 상세 화면 갤러리로 쓴다.
-- ============================================================

create table if not exists shelter_animal_photos (
  seq        int  not null references shelter_animals(seq) on delete cascade,
  img_type   text not null check (img_type in ('THUMB', 'IMG')),
  img_num    int  not null,
  img_url    text not null,
  synced_at  timestamptz not null default now(),
  primary key (seq, img_type, img_num)
);

create index if not exists shelter_photos_thumb
  on shelter_animal_photos (seq) where img_type = 'THUMB';

alter table shelter_animal_photos enable row level security;
create policy public_read on shelter_animal_photos for select using (auth.uid() is not null);
-- 쓰기 정책 없음 — CRON(service_role)만 동기화한다

-- 대표 사진은 THUMB에서 파생한다. 손으로 넣지 않는다.
create or replace function sync_animal_thumb(p_seq int) returns void
language sql
security definer
set search_path = public
as $$
  update shelter_animals a
     set photo_url = p.img_url,
         photo_is_breed_placeholder = false
    from shelter_animal_photos p
   where a.seq = p_seq and p.seq = p_seq and p.img_type = 'THUMB'
     and p.img_num = (select min(img_num) from shelter_animal_photos
                       where seq = p_seq and img_type = 'THUMB');
$$;

-- 사진이 들어오면 대표 사진을 자동 갱신
create or replace function shelter_photo_sync_trg() returns trigger
language plpgsql as $$
begin
  if new.img_type = 'THUMB' then perform sync_animal_thumb(new.seq); end if;
  return new;
end;
$$;

drop trigger if exists shelter_photos_after_insert on shelter_animal_photos;
create trigger shelter_photos_after_insert
  after insert or update on shelter_animal_photos
  for each row execute function shelter_photo_sync_trg();

-- 0012의 유튜브 승격 트리거는 더 이상 쓰지 않는다.
-- 실사진이 없는 개체만 견종 대표 사진으로 남긴다 (최후 폴백).
create or replace function shelter_photo_default() returns trigger
language plpgsql as $$
begin
  -- 실사진(THUMB)이 이미 있으면 건드리지 않는다
  if exists (select 1 from shelter_animal_photos
              where seq = new.seq and img_type = 'THUMB') then
    return new;
  end if;
  if new.photo_url is null or new.photo_is_breed_placeholder then
    new.photo_url := breed_placeholder_photo(coalesce(new.breed, ''), new.animal_type);
    new.photo_is_breed_placeholder := true;
  end if;
  return new;
end;
$$;
