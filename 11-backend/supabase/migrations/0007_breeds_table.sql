-- ============================================================
-- 견종 마스터 (와이어프레임 A-09 견종 3개 추천 · 선택)
--
-- config.breeds에 이름·성격만 두던 것을 테이블로 옮긴다.
-- 화면이 사진을 보여줘야 하는데 config JSON에 이미지·라이선스까지
-- 밀어넣으면 관리가 안 되기 때문이다.
--
-- 이미지 출처: Wikimedia Commons에서 **상업적 이용이 허용되는 라이선스만**
-- 골라 받아 Supabase Storage(public bucket `breeds`)에 올렸다.
-- 외부 링크를 그대로 쓰면 원본이 삭제되거나 핫링크가 막힐 때 화면이 깨진다.
--
-- CC BY 계열은 **출처 표기가 의무**다. attribution_required가 true인 견종은
-- 화면에 작가명과 라이선스를 함께 노출해야 한다.
-- ============================================================

create table if not exists breeds (
  name                 text primary key,
  slug                 text not null unique,
  -- A-10 성격 프리필 값. 돌봄 요구량과 AI 페르소나 연출의 입력이 된다.
  activity             int not null check (activity between 1 and 5),
  timid                int not null check (timid between 1 and 5),
  affection            int not null check (affection between 1 and 5),
  sort_order           int not null default 0,
  image_url            text,
  image_license        text,
  image_license_url    text,
  image_author         text,
  image_source_url     text,
  -- false면 CC0·퍼블릭 도메인이라 표기 없이 쓸 수 있다
  attribution_required boolean not null default true,
  created_at           timestamptz not null default now()
);

alter table breeds enable row level security;
create policy public_read on breeds for select using (auth.uid() is not null);
-- 쓰기 정책 없음 — 마이그레이션과 service_role로만 바꾼다

insert into breeds (name, slug, activity, timid, affection, sort_order,
                    image_url, image_license, image_license_url, image_author,
                    image_source_url, attribution_required)
values
  ('보더콜리', 'border-collie', 5, 1, 3, 1,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/border-collie.jpg', 'CC0', 'http://creativecommons.org/publicdomain/zero/1.0/deed.en', 'Karen Arnold',
   'https://commons.wikimedia.org/wiki/File:Border-collie-dog-1365525954Aa6.jpg', true),
  ('골든 리트리버', 'golden-retriever', 4, 1, 5, 2,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/golden-retriever.jpg', 'CC BY 3.0', 'https://creativecommons.org/licenses/by/3.0', 'MichaelMcPhee',
   'https://commons.wikimedia.org/wiki/File:Callie_the_golden_retriever_puppy.jpg', false),
  ('비글', 'beagle', 5, 1, 4, 3,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/beagle.jpg', 'CC BY 4.0', 'https://creativecommons.org/licenses/by/4.0', 'Trougnouf (Benoit Brummer)',
   'https://commons.wikimedia.org/wiki/File:Beagle_in_Viroinval_(DSC04556).jpg', false),
  ('웰시코기', 'welsh-corgi', 4, 2, 4, 4,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/welsh-corgi.jpg', 'CC0', 'http://creativecommons.org/publicdomain/zero/1.0/deed.en', 'Huoadg5888Minor edits made by Subsidiary account',
   'https://commons.wikimedia.org/wiki/File:Fawn_and_white_Welsh_Corgi_puppy_standing_on_rear_legs_and_sticking_out_the_tongue.jpg', true),
  ('푸들', 'poodle', 4, 2, 4, 5,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/poodle.jpg', 'CC BY-SA 3.0', 'https://creativecommons.org/licenses/by-sa/3.0', 'Томасина',
   'https://commons.wikimedia.org/wiki/File:Toy_Poodle_in_Riga_1.JPG', false),
  ('포메라니안', 'pomeranian', 4, 3, 4, 6,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/pomeranian.jpg', 'Public domain', '', 'Cshashaty at English Wikipedia',
   'https://commons.wikimedia.org/wiki/File:Pomeranian_at_play.JPG', true),
  ('말티즈', 'maltese', 3, 3, 5, 7,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/maltese.jpg', 'CC BY-SA 3.0', 'http://creativecommons.org/licenses/by-sa/3.0/', 'Sannse',
   'https://commons.wikimedia.org/wiki/File:Maltese_600.jpg', false),
  ('시츄', 'shih-tzu', 2, 2, 4, 8,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/shih-tzu.jpg', 'CC BY 2.0', 'https://creativecommons.org/licenses/by/2.0', 'danny O.',
   'https://commons.wikimedia.org/wiki/File:Shih_Tzu_portrait_show_dog.jpg', false),
  ('진돗개', 'jindo', 4, 2, 3, 9,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/jindo.jpg', 'Public domain', '', 'en:User:Jojeda1981 (uploadet by TBjornstad 16:40, 5 June 2007 (UTC))',
   'https://commons.wikimedia.org/wiki/File:Turbo_Jindo_Gae.jpg', true),
  ('믹스견', 'mixed', 3, 3, 4, 10,
   'https://buzeurukwscushcryksn.supabase.co/storage/v1/object/public/breeds/mixed.jpg', 'CC BY 2.0', 'https://creativecommons.org/licenses/by/2.0', 'Gopal Aggarwal from INDIA',
   'https://commons.wikimedia.org/wiki/File:Dog_body_language_new_sound_tilting_head.jpg', false)
on conflict (name) do update set
  slug = excluded.slug, activity = excluded.activity, timid = excluded.timid,
  affection = excluded.affection, sort_order = excluded.sort_order,
  image_url = excluded.image_url, image_license = excluded.image_license,
  image_license_url = excluded.image_license_url, image_author = excluded.image_author,
  image_source_url = excluded.image_source_url,
  attribution_required = excluded.attribution_required;

-- config.breeds.list는 더 이상 정본이 아니다. 고정 견종 설정만 남긴다.
update config
   set value = jsonb_build_object('pinned', coalesce(value->'pinned', '["보더콜리"]'::jsonb),
                                  'note', '견종 목록은 breeds 테이블이 정본이다 (0007)'),
       updated_at = now()
 where key = 'breeds';
