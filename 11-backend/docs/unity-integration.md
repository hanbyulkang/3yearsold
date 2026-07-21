# Unity 연동 가이드

백엔드는 배포돼 있고 실제로 돕니다. 이 문서는 **Unity에서 무엇을 어떤 순서로 부르면 되는지**만 다룹니다.

- 프로젝트: `balang` (ap-northeast-1)
- 클라이언트 코드: `19Team/Assets/Scripts/Backend/`
- 최종 갱신: 2026-07-22

---

## 0. 설정 — 이제 아무것도 안 해도 됩니다

`BackendConfig.asset`이 없으면 **코드 기본값(balang 프로젝트 + 데모 계정)** 으로
동작합니다. 씬을 열고 Play를 누르면:

1. `DemoNavBar`가 자동 생성 — 화면 상단 우측에 마당/퍼즐/2048/추천 이동 바
2. 데모 계정(`demo@dplus-demo.app`) 자동 로그인 — 설문·분석·추천이 시드돼 있음
3. D 추천 씬은 로딩 후 **실제 추천 3마리**, MG1은 발바닥·뼈다귀 서버 연동

A-01 로그인 화면이 생기면 BackendConfig에서 DemoEmail만 비우면 됩니다.

### 직접 설정하려면 — 설정 에셋

`Create ▸ D+ ▸ Backend Config` 로 `Assets/Resources/BackendConfig.asset` 을 만들고 두 값을 채웁니다.

| 필드 | 값 |
|---|---|
| `Url` | `https://<project-ref>.supabase.co` |
| `AnonKey` | Supabase 대시보드 ▸ Settings ▸ API ▸ `anon` |

**anon 키는 공개 전제입니다.** WebGL 빌드는 문자열이 그대로 노출되므로, 방어는 키가 아니라 RLS가 합니다(`0003_rls.sql`).

**절대 넣지 마세요**: `service_role` 키, DB 비밀번호, OpenAI 키. 이것들이 노출되면 재화를 무한 생성할 수 있습니다.

---

## 1. A 온보딩 (A-01 ~ A-11)

```csharp
// A-01 로그인
await SupabaseClient.SignIn(email, password);

// A-03~A-07 문항마다 즉시 저장 (이탈 후 이어하기)
await OnboardingApi.SaveAnswer(userId, "q1", "{\"age\":\"30대\",\"housing\":\"아파트\",\"household\":\"혼자\"}");
await OnboardingApi.SaveAnswer(userId, "q4", "\"짖으면 이유를 먼저 찾아볼래요\"");   // 서술형은 JSON 문자열

// A-06·A-07 되묻기 (선택) — null이면 그냥 넘어간다
string probe = await OnboardingApi.Probe("q4", "짖거나 물건을 망가뜨린다면?", answer);

// A-08 분석 (5~7초 — 로딩 화면 필요)
var result = await OnboardingApi.Analyze();

// A-09 견종 3개 표시
foreach (var b in result.breeds) {
    // b.name, b.reason, b.imageUrl, b.personality
    // b.attribution 이 null이 아니면 작가명·라이선스를 화면에 노출해야 한다
}
```

### 응답 예시

```json
{
  "analysisId": "…",
  "summary": "아파트에 배우자와 함께 살며 하루 4~8시간 함께할 수 있는 분입니다.",
  "breeds": [
    { "name": "보더콜리",
      "reason": "당신이 쓰신 '주말엔 같이 산책 많이 다니고'처럼…",
      "imageUrl": "https://…/storage/v1/object/public/breeds/border-collie.jpg",
      "personality": { "activity": 5, "timid": 1, "affection": 3 },
      "attribution": null },
    { "name": "골든 리트리버", "…": "…",
      "attribution": { "author": "…", "license": "CC BY 3.0" } }
  ],
  "participation": { "recommended": "learn", "readiness": "preparing", "reason": "…" }
}
```

### 지켜야 할 것

- **`attribution`이 null이 아니면 반드시 표기합니다.** CC BY 계열의 의무 사항입니다. null이면 CC0·퍼블릭 도메인이라 표기가 필요 없습니다.
- **`personality`가 A-10 프리필 값입니다.** 견종을 고르면 이 값으로 성격 슬라이더를 채웁니다.
- **`Analyze()`는 온보딩에서 1회만** 부릅니다. 결과를 D 추천이 공유합니다 (PRD §4.3 단일 엔진). 화면마다 다시 부르면 매번 다른 결과가 나와 일관성이 깨집니다.
- 분석이 실패해도 **설문 응답은 서버에 남아 있습니다.** 재시도만 하면 됩니다.

---

## 2. D 추천 (D-01 ~ D-03)

```csharp
var recs = await SupabaseClient.Invoke<RecResult>("recommend");
// recs.picks[i].animal_seq, .reason  (3마리)
```

설문을 다시 요구하지 않습니다. 온보딩의 분석 결과를 그대로 씁니다.

보호견 상세 정보는 REST로 조회합니다:
```
GET /rest/v1/shelter_animals?seq=in.(62,47,510)&select=seq,name,breed,sex,weight_kg,movie_url,traits,content_raw
```

`RecData.cs`의 목업을 이 응답으로 교체하면 됩니다.

---

## 3. MG1 미니게임 (C-01 ~ C-03) — ✅ 연동 완료

`MG1GameManager`가 `Backend.ServerRewardClient`를 쓰도록 이미 교체돼 있습니다.
발바닥 차감·뼈다귀 지급이 서버 원장에 기록되고, 오프라인이면 로컬 추정값으로
조용히 동작합니다 (데모가 네트워크에 인질 잡히지 않게).

지급은 **세션 상한 방식**입니다 (D-023) — MG1은 피버·특수블록 등 클라 전용
로직이라 서버 리플레이가 불가능해, 세션당 상한(30) 안에서 지급합니다.
**랭킹(C-05)을 켜기 전에 반드시 리플레이 검증으로 교체해야 합니다.**

### (참고) 원래 목표였던 완전 검증 경로

```csharp
// C-01 시작 — 발바닥 차감 + 서버가 시드 발급
var s = await GameApi.Start("mg1");
if (s.code == "NO_PAW") { /* C-04 충전 시트 */ }
// s.seed 로 보드를 만든다. s.board 에 규격(7x8, 5색, 20수)이 온다.

// C-02 플레이 — 이동을 기록해 둔다
moves.Add(new GameApi.Move(r, c, down: false));

// C-03 결과 — 서버가 같은 시드로 재계산한다
var res = await GameApi.Submit(s.sessionId, moves.ToArray(), localScore);
// res.verifiedScore 를 화면에 쓴다. res.pointsAwarded 가 실제 지급된 뼈다귀.
```

### `IRewardClient`를 바꿔야 합니다

현재 인터페이스는 동기입니다:

```csharp
int GetPaws();
bool TrySpendPaw();
int GrantPointsForScore(int score);
```

네트워크 호출을 동기 시그니처에 넣으면 메인 스레드가 멈춥니다. **async로 바꾸고** `GameApi`에 위임하는 것을 권합니다:

```csharp
Task<int> GetPawsAsync();
Task<GameApi.StartResult> StartAsync();          // TrySpendPaw 대체 — 세션도 함께 받는다
Task<GameApi.SubmitResult> SubmitAsync(string sessionId, Move[] moves, int score);
```

`TrySpendPaw()`가 `StartAsync()`로 합쳐지는 이유: 발바닥 차감과 세션 생성이 **한 트랜잭션**이어야 합니다. 따로 두면 차감만 되고 세션이 안 생기는 경우가 생깁니다.

`LocalMockRewardClient`는 그대로 두고 스위치로 전환하면 오프라인 개발도 유지됩니다.

### 보드 로직이 서버와 같아야 합니다

`_shared/game.ts`의 PRNG(mulberry32)·매치 판정·연쇄 배수를 C#에서 **똑같이** 구현해야 점수가 일치합니다. 다르면 정직하게 플레이해도 `accepted=false`가 나옵니다.

불일치 시 `GameApi.Submit`이 경고 로그를 남깁니다. 그 경우 서버 값(`verifiedScore`)이 정답입니다.

---

## 4. 엔드포인트 목록

| 함수 | 인증 | 용도 |
|---|---|---|
| `survey-probe` | 사용자 | 설문 되묻기 (실패해도 200 + probe:null) |
| `survey-analyze` | 사용자 | AI 상황 분석 (단일 엔진) |
| `recommend` | 사용자 | 보호견 추천 3마리 |
| `game-start` | 사용자 | 발바닥 차감 + 시드 발급 |
| `game-submit` | 사용자 | 점수 서버 재계산 + 지급 |
| `shelter-sync` | service_role | 보호견 동기화 (CRON) |
| `shelter-traits` | service_role | 성격 구조화 (CRON) |

`service_role` 함수는 **클라이언트에서 부르지 않습니다.** CRON이나 서버에서만 호출합니다.

---

## 5. 클라이언트가 하지 말아야 할 것

와이어프레임 주석과 PRD §5.5가 정한 것들입니다.

- **재화 잔액을 클라에서 계산하지 않습니다.** 서버 조회값을 표시만 합니다 (B-01).
- **레벨 곡선·금액 구간·전환 비율 상수를 클라에 복제하지 않습니다** (A-05·B-04). 서버가 내려주는 값을 씁니다.
- **점수를 클라가 확정하지 않습니다** (C-02). 서버 재계산값이 정답입니다.
- **목욕 거부는 실패가 아닙니다** (B-02). 이벤트명에 `fail`을 쓰지 않습니다.
- **"유기견"이라고 쓰지 않습니다.** UI·푸시 문구 전부 "보호견"입니다 (부록 A).

---

## 6. 현재 데이터 상태

| | |
|---|---|
| 보호견 | 24마리 (입양문의가능 11) |
| 보호견 성격(traits) | 17마리 — 입양문의가능 전건 완료 |
| 견종 | 10종 + 사진 (Storage `breeds` 버킷) |
| 계정 | 0 (테스트 계정 정리 완료) |
