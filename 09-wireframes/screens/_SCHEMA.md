# 화면 정의 스키마 (screens/*.js 공통 계약)

각 파일은 IIFE로 `window.SCREENS` 배열에 화면 객체를 push한다.

```js
(function (S) {
S.push({
  id: 'b01-yard',            // DOM id. 소문자-하이픈. 번호 접두어 유지
  no: 'B-01',                // 정렬 키. 그룹문자-2자리
  group: 'B. 돌봄 루프',      // 좌측 네비 그룹명. 파일 내 전부 동일
  prd: '§4.1',               // 근거가 되는 PRD 절
  title: '홈 — 캐릭터견 마당',
  purpose: '한 문장. 이 화면이 존재하는 이유와 설계 제약',
  html: `...`,               // 프레임 내부 마크업 (아래 구조 참고)
  wide: false,               // true면 데스크톱 900×620 프레임 (보호소 어드민 전용)
  tall: false,               // true면 852px 고정 해제 (긴 문서형 화면)
  notes: ['개발 주석1', '주석2']  // 노란 박스. API·상태·엣지케이스·서버 권위 지점
});
})(window.SCREENS);
```

## 프레임 내부 표준 구조

```html
<div class="sb"><span>9:41</span><span></span><span>100%</span></div>   <!-- 상태바 -->
<div class="appbar"><span class="back">←</span><span class="t">타이틀</span><span class="act">우측</span></div>
<div class="body"> ... 스크롤 영역 ... </div>
<div class="footer"> ... 고정 CTA ... </div>       <!-- 선택 -->
<div class="tabbar"> ... 5탭 ... </div>            <!-- 선택, footer와 함께 쓰지 않음 -->
```

**하단 탭 5개(고정)**: 마당 / 게임 / 추천 / 후원 / 내정보

```html
<div class="tabbar">
  <div class="tab on"><span class="ic">■</span>마당</div>
  <div class="tab"><span class="ic">■</span>게임</div>
  <div class="tab"><span class="ic">■</span>추천</div>
  <div class="tab"><span class="ic">■</span>후원</div>
  <div class="tab"><span class="ic">■</span>내정보</div>
</div>
```

**재화 HUD(§5.1, 마당·게임 계열 상단 고정)**

```html
<div class="hud">
  <span class="cur">🐾 3/5</span>      <!-- 발바닥: 미니게임 입장 전용 -->
  <span class="cur">P 12,400</span>    <!-- 포인트 -->
  <span class="cur">육포 12</span>      <!-- 과금 재화 -->
</div>
```

## 사용 가능한 클래스 (wireframe.css에 정의됨 — 새 CSS 추가 금지, 인라인 style은 폭/높이 조정에만)

| 분류 | 클래스 |
|---|---|
| 레이아웃 | `row` `row top` `row wrap` `row between` `col` `grow` `divider` |
| 타이포 | `h1` `h2` `h3` `p` `s` `xs` `b` `center` `mono` |
| 컨테이너 | `box` `box fill` `card` `card sel` `card flat` |
| 자리표시 | `img`(내부 `<span>라벨</span>`) `yard`(내부 `.dog`) `board`/`cell`(3매치) `flow`(ASCII 흐름도) |
| 버튼 | `btn` `btn pri` `btn sec` `btn gho` `btn dis` `btn sm` `btn wide` |
| 입력 | `field` `field area` `field area tall` `label` `opt` `opt on` `opt chk` |
| 표시 | `chip` `chip on` `chip ai` `badge` `badge ai` `badge warn` `badge ok` `gauge`/`gauge sm`(내부 `<i style="width:%">`) |
| 특수 | `evidence`(근거 카드: `.cap`/`.txt`/`.src`) `aibox`(LLM 영역: `.cap`) `honest`(정직성·준법 경고) `li`/`thumb`(리스트) `tbl`(표) `overlay`/`overlay mid`+`sheet`(모달·바텀시트) |
| 이동 | 아무 요소에 `data-goto="다른화면id"` — 클릭 시 해당 화면으로 스크롤 |

## 작성 규칙

1. **화면은 실제 UI만 담는다.** PRD의 설계 근거·정책 설명을 화면으로 만들지 않는다 — 그런 내용은 `notes`나 PRD 참조로 남긴다. 안내 전용 화면(재화 설명, 서비스 철학 페이지 등) 금지.
2. **간결하게.** 화면당 핵심 요소 4~6개. 긴 설명 박스 대신 한 줄 캡션. 텍스트는 더미 없이 실제 카피로 채우되 짧게.
3. **용어**: "유기견" 금칙 → **보호견**. 유저의 가상견은 **캐릭터견**. 재화는 **발바닥·포인트·육포** 3종뿐. (부록 A)
4. **§1.2 원칙 2**: 죽음·안락사·질병·부상 연출 금지. 방치 표현은 "조금 시무룩해졌어요"가 상한.
5. **§6.5 / §7.7**: 모의 기부 라벨·결제 준법 고지는 해당 화면에서만 `honest` 박스 1개로.
6. `notes`는 화면당 2~4개. 개발자가 실제로 막히는 지점(API·멱등·실패 처리·서버 권위)만.
7. 백틱 문자열 안에 백틱·`${` 를 쓰지 않는다.
