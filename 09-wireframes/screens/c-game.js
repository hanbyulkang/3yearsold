/* C. 미니게임 — 발바닥 소모 · 포인트 획득 (PRD §4.2, §5) */
(function (S) {

S.push({
  id: 'c01-hub',
  no: 'C-01',
  group: 'C. 미니게임',
  prd: '§4.2',
  title: '미니게임 허브',
  purpose: '입장 1회 = 발바닥 1개. MG1만 플레이 가능, MG2·3은 준비 중.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">미니게임</span><div class="hud"><span class="cur">🐾 3/5</span><span class="cur">P 12,400</span></div></div>
    <div class="body">
      <div class="s">입장할 때 발바닥 1개를 사용해요 · 다음 회복까지 1:24</div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>MG1</span></div>
          <div class="grow col" style="gap:3px">
            <div class="h2">3매치 퍼즐</div>
            <div class="s">간식을 3개씩 맞춰요 · 클리어 시 최대 300 P</div>
          </div>
        </div>
        <div class="btn pri wide" data-goto="c02-match3">플레이 (🐾 1)</div>
      </div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>MG2</span></div>
          <div class="grow col" style="gap:3px">
            <div class="row between"><span class="h2">산책 리듬게임</span><span class="badge">준비 중</span></div>
          </div>
        </div>
      </div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>MG3</span></div>
          <div class="grow col" style="gap:3px">
            <div class="row between"><span class="h2">간식 타이밍게임</span><span class="badge">준비 중</span></div>
          </div>
        </div>
      </div>
    </div>
    <div class="tabbar">
      <div class="tab"><span class="ic">■</span>마당</div>
      <div class="tab on"><span class="ic">■</span>게임</div>
      <div class="tab"><span class="ic">■</span>추천</div>
      <div class="tab"><span class="ic">■</span>후원</div>
      <div class="tab"><span class="ic">■</span>내정보</div>
    </div>
  `,
  notes: [
    '발바닥 차감은 입장 시점 서버 처리. 회복 시각(next_refill_at)도 서버가 내려줌.',
    '발바닥 0이면 플레이 버튼이 충전 시트(C-04)를 연다.'
  ]
});

S.push({
  id: 'c02-match3',
  no: 'C-02',
  group: 'C. 미니게임',
  prd: '§4.2',
  title: 'MG1 — 3매치 플레이',
  purpose: '이동 수 제한 방식(타이머 없음, §1.2 원칙 2).',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar">
      <span class="back">✕</span>
      <span class="t">3매치</span>
      <span class="act">남은 이동 12 · 점수 4,200</span>
    </div>
    <div class="body">
      <div class="row between">
        <span class="s">목표: 뼈다귀 블록 20개</span>
        <span class="s mono">14/20</span>
      </div>
      <div class="board">
        <div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div><div class="cell">🦴</div><div class="cell">🍪</div><div class="cell">🎾</div><div class="cell">🍖</div>
        <div class="cell">🍪</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div><div class="cell">🦴</div><div class="cell">🍪</div><div class="cell">🎾</div>
        <div class="cell">🎾</div><div class="cell">🍪</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div><div class="cell">🦴</div><div class="cell">🍪</div>
        <div class="cell">🦴</div><div class="cell">🎾</div><div class="cell">🍪</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div><div class="cell">🦴</div>
        <div class="cell">🍖</div><div class="cell">🦴</div><div class="cell">🎾</div><div class="cell">🍪</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div>
        <div class="cell">🍪</div><div class="cell">🎾</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🎾</div><div class="cell">🍪</div><div class="cell">🦴</div>
        <div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🍪</div><div class="cell">🎾</div><div class="cell">🦴</div><div class="cell">🍖</div><div class="cell">🍪</div>
      </div>
      <div class="s center">응원하는 캐릭터견이 하단에 표시</div>
    </div>
  `,
  notes: [
    '시간 제한 없음 — 이동 수 제한만(§1.2 원칙 2). 목표 미달도 점수 비례 포인트 지급, "지는 판" 없음.',
    '점수·클리어 판정은 서버 검증(플레이 로그 제출 → Edge Function 재계산). 클라 신뢰 금지(§5.5).'
  ]
});

S.push({
  id: 'c03-result',
  no: 'C-03',
  group: 'C. 미니게임',
  prd: '§4.2 · §5.3',
  title: '결과 · 보상',
  purpose: '포인트 지급 + 다음 행동(기부) 연결.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="justify-content:center;gap:14px">
      <div class="center col" style="gap:4px">
        <div class="h1">클리어!</div>
        <div class="s">점수 7,800 · 이동 4회 남김</div>
      </div>
      <div class="card center">
        <div class="h2 mono">+280 P</div>
        <div class="s">보유 포인트 12,680 P</div>
      </div>
      <div class="aibox">
        <div class="cap">단추</div>
        <div class="s">(신나서 폴짝폴짝) 이 포인트로 보호소 친구들 사료를 채워줄 수 있대요!</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="e01-donate">사료 기부하러 가기</div>
      <div class="row" style="gap:8px">
        <div class="btn sec grow">한 번 더 (🐾 1)</div>
        <div class="btn gho grow" data-goto="c01-hub">허브로</div>
      </div>
    </div>
  `,
  notes: [
    '보상은 포인트 단일(§4.2) — 별도 "사료 재화" 없음.',
    '지급은 origin=play로 원장 기록, 일일 획득 상한 서버 체크(§5.5).'
  ]
});

S.push({
  id: 'c04-paw-refill',
  no: 'C-04',
  group: 'C. 미니게임',
  prd: '§5.2',
  title: '발바닥 부족 (충전 시트)',
  purpose: '시간 회복 대기 or 육포 충전. 포인트 충전은 없다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body pad0">
      <div class="img" style="height:100%;border:0;border-radius:0"><span>허브 화면 (배경)</span></div>
    </div>
    <div class="overlay">
      <div class="sheet">
        <div class="h2">발바닥이 다 떨어졌어요</div>
        <div class="row between">
          <span class="s">🐾 0/5</span>
          <span class="s">다음 회복까지 32분</span>
        </div>
        <div class="btn wide">육포 1개로 발바닥 2개 충전</div>
        <div class="btn gho wide">기다릴게요</div>
        <div class="xs">발바닥은 시간이 지나면 자동으로 회복돼요. 포인트로는 충전할 수 없어요.</div>
      </div>
    </div>
  `,
  notes: [
    '포인트→발바닥 차단은 §5.2 확정(무한 인플레 루프 방지). 우회 UI 만들지 말 것.',
    '육포 1=발바닥 2는 가안 — 밸런스 시트 확정 대상.',
    '충전 트랜잭션은 Edge Function, 원장 기록.'
  ]
});

S.push({
  id: 'c05-ranking',
  no: 'C-05',
  group: 'C. 미니게임',
  prd: '§5.5 · §6.5',
  title: '주간 랭킹',
  purpose: '1위 = 이번 주 기부 증서 명의. 과금 유래 점수 제외.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">주간 랭킹</span><span class="act">7/21~7/27</span></div>
    <div class="body">
      <div class="card flat">
        <div class="li"><span class="h3 mono">1</span><div class="grow"><div class="h3">멍멍이집사</div><div class="xs">24,300점</div></div><span class="badge ok">증서 명의</span></div>
        <div class="li"><span class="h3 mono">2</span><div class="grow"><div class="h3">산책왕</div><div class="xs">21,100점</div></div></div>
        <div class="li"><span class="h3 mono">3</span><div class="grow"><div class="h3">단추아빠</div><div class="xs">19,800점</div></div></div>
        <div class="li"><span class="h3 mono">47</span><div class="grow"><div class="h3 b">나</div><div class="xs">6,200점</div></div></div>
      </div>
      <div class="s">이번 주 1위는 이번 기부 증서에 이름이 올라가요. 랭킹 점수는 플레이·돌봄으로 얻은 포인트만 집계합니다 — 결제로 전환한 포인트는 포함되지 않아요.</div>
    </div>
  `,
  notes: [
    '집계: 해당 주 원장에서 origin=play·level 양수 엔트리 합. 사용(기부) 차감분은 빼지 않음 — 기부하면 순위가 떨어지는 역인센티브 방지.',
    '과금 유래(purchase·convert) 제외는 §5.5 확정.'
  ]
});

})(window.SCREENS);
