-- ============================================================
-- 출처 표기 플래그 반전 수정
--
-- 0007에서 attribution_required를 거꾸로 넣었다.
-- CC0·퍼블릭 도메인(표기 불필요)에 true가, CC BY 계열(표기 의무)에 false가 들어갔다.
--
-- 저작권 문제라 값을 라이선스에서 다시 계산한다. 앞으로도 어긋나지 않도록
-- 사람이 넣는 값이 아니라 라이선스에서 파생되게 둔다.
-- ============================================================

update breeds
   set attribution_required = not (
     lower(coalesce(image_license, '')) like '%cc0%'
     or lower(coalesce(image_license, '')) like '%public domain%'
     or lower(coalesce(image_license, '')) like '%pdm%'
   );

-- 라이선스가 바뀌면 플래그도 따라가게 한다. 손으로 맞추면 또 어긋난다.
create or replace function sync_attribution_required() returns trigger
language plpgsql as $$
begin
  new.attribution_required := not (
    lower(coalesce(new.image_license, '')) like '%cc0%'
    or lower(coalesce(new.image_license, '')) like '%public domain%'
    or lower(coalesce(new.image_license, '')) like '%pdm%'
  );
  return new;
end;
$$;

drop trigger if exists breeds_attribution on breeds;
create trigger breeds_attribution before insert or update of image_license on breeds
  for each row execute function sync_attribution_required();
