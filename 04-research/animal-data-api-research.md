# 조사 — 보호견 데이터 공개 API 2종 검증

PRD §4 보호견 추천 카드에 넣을 실데이터의 출처를 확인한 기록입니다.
**이 문서의 "사실" 항목은 전부 2026-07-21에 실제 HTTP 호출로 확인했습니다.** 검색 결과 요약만 보고 적은 내용은 없습니다.

- 조사일: 2026-07-21
- 조사자: Claude (hanbyulkang 세션)
- 관련 결정: D-002 (실제 보호견은 추천 카드로 연결)
- 인증키: 이 Vault에 저장하지 않습니다. 팀 규칙 §8에 따라 로컬 환경변수로만 보관합니다.

---

## 0. 결론 먼저

| | 국가 API | 서울 vPetInfo |
|---|---|---|
| 물량 (개) | **4,521** (공고중 1,344) | **24** (개 15 · 고양이 9) |
| 성격 정보 | `specialMark` 한 줄, 품질 편차 큼 | **1,800~8,300자 서술** |
| 이름 | 없음 (유기번호만) | **있음** (경자, 순금, 벅스) |
| 영상 | 없음 | **10건에 유튜브** |
| 공고 기한 | `noticeEdt` 있음 | 없음 |

**제안**: 둘 다 쓴다. 서울 24마리는 추천 카드의 품질 기준(성격·영상·이름이 모두 있음), 국가 API는 규모와 지역 커버리지. *(제안이며 팀 결정 아님 — 검증 필요)*

---

## 1. 사실 — 국가 API (구조동물 조회)

`https://apis.data.go.kr/1543061/abandonmentPublicService_v2`

### 1.1 게이트웨이 응답 3종 (디버깅 지표)
| 응답 | 의미 |
|---|---|
| `API not found` | 경로 오타 — **`_v2` 접미사 누락이 대표 원인** |
| `Unauthorized` (HTTP 401, text/plain) | 경로는 존재하나 미승인/전파 지연 |
| 정상 JSON | 성공 |

> 활용신청 직후 전 오퍼레이션이 401이었으나 수 분 뒤 자동 해소됨. **신청 직후 401은 정상이며 대기하면 된다.**

### 1.2 오퍼레이션 5종 (전부 200 확인)
| 오퍼레이션 | 필수 파라미터 | 실측 |
|---|---|---|
| `abandonmentPublic_v2` | 없음 | 개 전체 4,521건 |
| `sido_v2` | 없음 | 16건 |
| `sigungu_v2` | `upr_cd` | 서울 27건 |
| `shelter_v2` | `upr_cd`, `org_cd` | `careRegNo`, `careNm` |
| `kind_v2` | `up_kind_cd` | 개 206종 |

### 1.3 파라미터
```
upkind : 417000(개) | 422400(고양이) | 429900(기타)
state  : notice(공고중) | protect(보호중) | 생략(전체)
bgnde / endde : 20260701 형식
upr_cd / org_cd / care_reg_no : 지역·보호소 필터
numOfRows / pageNo : 페이징 · _type=json (미지정 시 XML)
```
실측 건수(개): **공고중 1,344 / 보호중 1,900 / 전체 4,521**

### 1.4 응답 필드 (실데이터)
```
desertionNo   448567202601065        ← 고유 ID
happenDt / happenPlace               발견일 · 발견장소
kindFullNm / kindNm / kindCd / upKindNm
colorCd  age("2019(년생)")  weight("7(Kg)")   ← 숫자가 문자열
noticeNo  noticeSdt → noticeEdt      공고 기간 (통상 10일)
processState  보호중 / 공고중 / 종료(반환) / 종료(입양)
sexCd  M / F / Q(미상)               neuterYn  Y / N / U(미상)
specialMark   "짧게 미용됨, 샴푸 냄새, 백내장 약간 보임"
careRegNo / careNm / careTel / careAddr / careOwnerNm
orgNm  popfile1 / popfile2  updTm
healthChk / vaccinationChk           ← 일부 레코드에만 존재 (optional)
```

### 1.5 품질 실측 (100건 샘플)
- `specialMark` 채워진 비율 **100%**, `popfile1` 존재 **100%**
- 이미지 직접 접근 HTTP 200 (182KB)
- `sexCd` M 53 / F 46 / Q 1 · `neuterYn` N 62 / U 32 / Y 6 → **'미상' UI 처리 필수**

---

## 2. 사실 — 서울 vPetInfo (서울동물복지지원센터)

`http://openapi.seoul.go.kr:8088/{KEY}/json/vPetInfo/{시작}/{끝}/`

### 2.1 필드
```
SEQ  ANIMAL_NM(이름)  ADMISSION_DT  ANIMAL_TYPE(DOG/CAT)
ANIMAL_BREED  ANIMAL_SEX(M/W)  ANIMAL_BRITH_YMD  WEIGHT_KG
ADOPT_STATUS  FOSTER_STATUS  MOVIE_URL(유튜브)  CONT(HTML 장문)
```

### 2.2 `CONT` 구조 — 성격 서술의 원본
24건 전부 성격 정보를 포함. 섹션이 반쯤 표준화되어 있음:

| 섹션 | 보유 |
|---|---|
| `[성격]` (사람 친화력 / 타동물 친화력 / 에너지 레벨 / 좋아하는 것 / 보호자 필요 교육) | 10 |
| `[건강 특이사항]` | 11 |
| `[이상적인 가정]` | 11 |
| `[보호 센터]` | 10 |
| `[입양신청]` | 9 |
| `[입소 배경]` · `[담당자들의 애정 한마디]` | 2 |

실제 문장 예시:
> "처음 낯을 살짝 가리지만 곧 스킨십을 받으며 친한 직원에게는 달려들어 꼬리펠러를 할 정도로 사람을 좋아하는 친구입니다."
> "에너지가 넘치는 개너자이저 강아지 '순심' 입니다."

### 2.3 상태값
`ADOPT_STATUS`: 입양문의가능(11) / 입양진행중 / 신청마감 / 입양완료 / 미표출
`FOSTER_STATUS`: 임시보호가능 — **PRD 참여 퍼널의 '임보' 단계와 직접 연결됨**

---

## 3. 해석

1. **성격이 구조화 필드로 있는 전국 단위 공개 API는 확인되지 않았다.** 경기데이터드림·서울 열린데이터광장의 다른 데이터셋을 확인했으나 성격 컬럼은 없었다. 전국 단위에서 성격에 가장 가까운 것은 국가 API의 `specialMark` 자유서술 한 줄이다.
2. **`specialMark`의 품질 편차 자체가 AI의 근거가 된다.** 같은 100건 안에 "엄청 순하고, 사람 잘따름"처럼 성격이 담긴 것과 "관리번호 6-088M"처럼 정보가 없는 것이 공존한다. 후자를 품종·나이·발견 정황과 결합해 읽을 수 있는 카드로 만드는 것이 AI가 하는 판단이다.
3. **서울 `CONT`는 정규식으로 파싱할 수 없다.** HTML 원문이고 템플릿이 두 종류(단문 ~1,850자 / 장문 ~6,000자), 섹션 마커가 `[성격]`과 `○ 성격`으로 뒤섞여 있다. **LLM 파싱이 필요하다.**
4. **국가 API에는 형제·가족 관계가 암묵적으로 들어 있다.** `happenPlace` + `happenDt`가 같은 레코드를 묶으면 한배 새끼와 어미가 드러난다. 실측 사례: 창원 단감농장에서 어미 1마리("자견 9두와 함께 입소")와 새끼 9마리가 같은 날 입소. *(가설 — 이 묶음 규칙이 전국적으로 유효한지는 검증 필요)*

---

## 4. 미확인 / 검증 필요

- 서울 vPetInfo의 갱신 주기 (24건이 얼마나 자주 바뀌는지)
- 국가 API 이미지 URL의 핫링크 허용 여부 및 상업적 이용 조건
- `processState`가 `종료(입양)`으로 바뀌는 시점의 지연
- 경기·인천 등 타 지자체에 성격 데이터가 있는지 (포털 접근이 외부에서 차단되어 미확인)

---

## 5. 제약 (구현 시 반드시 반영)

| # | 제약 | 대응 |
|---|---|---|
| 1 | **개별 조회 API 없음** — `desertionNo` 단건 조회 오퍼레이션이 없음 | 목록 조회 후 매칭. 추적 대상은 자체 DB에 스냅샷 저장 필수 |
| 2 | 국가 API 일일 10,000건 (개발계정) | 캐싱 필수. 데모 중 소진 방지 |
| 3 | **이미지가 HTTP** (`http://openapi.animal.go.kr`) | HTTPS 페이지에서 mixed content 차단 → 서버 프록시 경유 필수 |
| 4 | **이미지 파일명에 대괄호** `...297[1].jpg` (20건 중 3건) | `encodeURIComponent` 처리 필수 |
| 5 | 숫자가 문자열 (`age`, `weight`) — `2026(60일미만)(년생)` 형태도 존재 | 파싱 유틸 필요 |
| 6 | `items.item`이 결과 1건일 때 배열이 아닌 단일 객체 | 정규화 함수로 항상 배열 보장 |

---

## 6. 출처

- title: 농림축산식품부 농림축산검역본부_국가동물보호정보시스템 구조동물 조회 서비스
- url_or_path: https://www.data.go.kr/data/15098931/openapi.do
- accessed_at: 2026-07-21
- source_type: official
- claim: 전국 보호 중인 개 4,521건(공고중 1,344건)과 §1.4의 필드를 제공한다
- confidence: high (실제 HTTP 호출로 확인)
- used_for: PRD §4 보호견 추천 카드의 전국 데이터 소스

- title: 서울동물복지지원센터 반려동물 입양 정보 (서비스명 `vPetInfo`)
- url_or_path: https://data.seoul.go.kr/dataList/OA-22646/S/1/datasetView.do
- accessed_at: 2026-07-21
- source_type: official
- claim: 24마리에 대해 이름·성격 서술·유튜브 영상·임시보호 가능 여부를 제공한다
- confidence: high (실제 HTTP 호출로 확인)
- used_for: 추천 카드 콘텐츠 품질 기준, 임보 단계 연결

- title: 서울특별시 열린데이터광장 Open API 이용 안내
- url_or_path: https://data.seoul.go.kr/together/guide/useGuide.do
- accessed_at: 2026-07-21
- source_type: official
- claim: 인증키 발급 후 `{KEY}/{타입}/{서비스명}/{시작}/{끝}/` 형식으로 호출한다
- confidence: high
- used_for: vPetInfo 호출 방식

- title: 샘플 데이터 (국가 API 20건 · 서울 24건)
- url_or_path: `05-data/sample-national-rescue-dogs.json`, `05-data/sample-seoul-shelter-pets.json`
- accessed_at: 2026-07-21
- source_type: sample
- claim: 위 필드 구조와 품질 수치의 원본
- confidence: high
- used_for: API 쿼터 소모 없이 UI·프롬프트 개발
