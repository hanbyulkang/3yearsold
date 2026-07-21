#!/usr/bin/env bash
# 백엔드 검증 전체 실행 — DB를 매번 새로 만들어 재현 가능하게 한다.
#
#   ./tests/run.sh          스키마·RLS·파서 (오프라인)
#   E2E=1 ./tests/run.sh    + 실제 vPetInfo API 호출까지
#
# 요구: 로컬 PostgreSQL. Docker가 없어 supabase start를 못 쓰는 환경을 전제한다.
# 운영은 Supabase이며, auth 스키마만 로컬 스텁으로 대체한다.
set -euo pipefail
cd "$(dirname "$0")/.."

DB=${DB:-dplus_test}
PSQL="psql -q -v ON_ERROR_STOP=1 -d $DB"

echo "▶ 테스트 DB 재생성: $DB"
dropdb --if-exists "$DB"
createdb "$DB"

echo "▶ 로컬 auth 스텁 (운영 배포 대상 아님)"
$PSQL -f tests/00_local_auth_stub.sql

echo "▶ 마이그레이션"
for f in supabase/migrations/*.sql; do
  echo "   $(basename "$f")"
  $PSQL -f "$f"
done

echo "▶ 시드"
$PSQL -f supabase/seed.sql

echo "▶ Auth 연동 · 경제 무결성 · RLS 방어선"
$PSQL -f tests/integrity_test.sql 2>&1 | sed 's/^psql:.*NOTICE:  //'
$PSQL -f tests/auth_test.sql      2>&1 | sed 's/^psql:.*NOTICE:  //'
$PSQL -f tests/game_test.sql      2>&1 | sed 's/^psql:.*NOTICE:  //'
$PSQL -f tests/rls_test.sql       2>&1 | sed 's/^psql:.*NOTICE:  //'

echo "▶ 파서 단위 테스트 (픽스처 기반, 오프라인)"
deno test --allow-read supabase/functions/_shared/ 2>&1 | tail -5

if [ "${E2E:-0}" = "1" ]; then
  echo "▶ 전 구간 검증 (실제 vPetInfo API 호출)"
  : "${SEOUL_API_KEY:?SEOUL_API_KEY 가 필요합니다 — .env.local 참조}"
  DB="$DB" deno run --allow-net --allow-env --allow-run tests/e2e_shelter_sync.ts
fi

echo
echo "✅ 전체 통과"
