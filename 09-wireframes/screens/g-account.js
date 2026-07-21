/* G. 내정보 (PRD §5.5, §4.4) */
(function (S) {

S.push({
  id: 'g01-my',
  no: 'G-01',
  group: 'G. 내정보',
  prd: '§4.4',
  title: '마이페이지',
  purpose: '내 상태 요약 + 메뉴 허브.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">내정보</span></div>
    <div class="body">
      <div class="card">
        <div class="row">
          <div class="img" style="width:48px;height:48px;flex:none"><span>프로필</span></div>
          <div class="grow col" style="gap:2px">
            <div class="h3">단추아빠</div>
            <div class="xs">단추 Lv.8 · 함께한 지 34일</div>
          </div>
        </div>
        <div class="row" style="gap:6px">
          <span class="cur">🐾 3/5</span><span class="cur">🦴 12,680</span><span class="cur">육포 12</span>
        </div>
      </div>
      <div class="card flat">
        <div class="row between"><span class="s">나의 참여 단계</span><span class="badge ai">봉사</span></div>
        <div class="xs">후원 → <b>봉사</b> → 임시보호 → 입양 · 어느 단계에 있어도 괜찮아요</div>
      </div>
      <div class="card flat">
        <div class="li"><div class="grow s">뼈다귀 내역</div><span class="xs" data-goto="g02-ledger">›</span></div>
        <div class="li"><div class="grow s">내 참여 기록 (후원·봉사·구매)</div><span class="xs">›</span></div>
        <div class="li"><div class="grow s">내 설문 수정</div><span class="xs" data-goto="d06-survey-edit">›</span></div>
        <div class="li"><div class="grow s">알림 설정</div><span class="xs" data-goto="g03-settings">›</span></div>
        <div class="li"><div class="grow s">계정 · 약관</div><span class="xs">›</span></div>
      </div>
    </div>
    <div class="tabbar">
      <div class="tab"><span class="ic">■</span>마당</div>
      <div class="tab"><span class="ic">■</span>게임</div>
      <div class="tab"><span class="ic">■</span>추천</div>
      <div class="tab"><span class="ic">■</span>후원</div>
      <div class="tab on"><span class="ic">■</span>내정보</div>
    </div>
  `,
  notes: [
    '참여 단계는 AI 추천 엔진의 현재 판단값 표시 — 유저가 수동 변경 불가.',
    '탈퇴 시 기부 집행 기록은 증빙 목적상 익명화 보존 고지 필요.'
  ]
});

S.push({
  id: 'g02-ledger',
  no: 'G-02',
  group: 'G. 내정보',
  prd: '§5.5',
  title: '뼈다귀 내역',
  purpose: '원장 조회. 항목마다 출처(origin) 표시.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">뼈다귀 내역</span><span class="act">잔액 12,680 🦴</span></div>
    <div class="body">
      <div class="row" style="gap:5px">
        <span class="chip on">전체</span><span class="chip">획득</span><span class="chip">사용</span>
      </div>
      <div class="card flat">
        <div class="li"><div class="grow"><div class="s">3매치 클리어</div><div class="xs">7/21 21:02 · 플레이</div></div><span class="s mono b">+280</span></div>
        <div class="li"><div class="grow"><div class="s">사료 기부</div><div class="xs">7/21 20:40 · 공동 창고</div></div><span class="s mono">-1,000</span></div>
        <div class="li"><div class="grow"><div class="s">레벨업 보상 (Lv.8)</div><div class="xs">7/20 09:12 · 레벨</div></div><span class="s mono b">+900</span></div>
        <div class="li"><div class="grow"><div class="s">육포 전환</div><div class="xs">7/19 18:30 · 결제 전환 · 랭킹 미집계</div></div><span class="s mono b">+500</span></div>
        <div class="li"><div class="grow"><div class="s">일일 돌봄 완주</div><div class="xs">7/19 08:00 · 돌봄</div></div><span class="s mono b">+150</span></div>
      </div>
    </div>
  `,
  notes: [
    '원장(append-only)의 파생 잔액 표시 — 클라 합산 금지.',
    'origin(play·level·purchase·convert) 노출, convert 항목엔 "랭킹 미집계" 표기(§5.5).'
  ]
});

S.push({
  id: 'g03-settings',
  no: 'G-03',
  group: 'G. 내정보',
  prd: '§4.1 · §7.7',
  title: '설정 · 알림',
  purpose: '알림·결제 한도·계정.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">설정</span></div>
    <div class="body">
      <div class="card flat">
        <div class="li"><div class="grow"><div class="s">돌봄 리마인드</div><div class="xs">"단추가 산책 갈 준비 됐대요" 같은 알림</div></div><span class="badge ok">켜짐</span></div>
        <div class="li"><div class="grow"><div class="s">기부 집행 리포트</div><div class="xs">수령 확인 시 결과 알림</div></div><span class="badge ok">켜짐</span></div>
        <div class="li"><div class="grow"><div class="s">캠페인 소식</div></div><span class="badge">꺼짐</span></div>
      </div>
      <div class="card flat">
        <div class="li"><div class="grow s">월 결제 한도</div><span class="s mono">100,000원 ›</span></div>
        <div class="li"><div class="grow s">계정 관리 · 탈퇴</div><span class="xs">›</span></div>
        <div class="li"><div class="grow s">약관 · 개인정보처리방침</div><span class="xs">›</span></div>
      </div>
    </div>
  `,
  notes: [
    '푸시 카피 정책: 죄책감·압박 문구 금지(§4.1) — "굶고 있어요" 류 금지, 리마인드는 긍정 톤만. 카피 리뷰 체크리스트에 포함.',
    '한도 하향은 즉시, 상향은 지연 적용(충동 과금 방지). 미성년은 별도 상한 고정(§7.7).'
  ]
});

})(window.SCREENS);
