/* E. 후원 — 공동 창고 · 집행 증명 (PRD §5.4, §6) */
(function (S) {

S.push({
  id: 'e01-donate',
  no: 'E-01',
  group: 'E. 후원',
  prd: '§6.1 · §5.4',
  title: '후원 홈 — 공동 창고 · 사료 기부',
  purpose: '포인트를 공동 창고에 적립. 게이지는 모금액이 아니라 참여량.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">후원</span><div class="hud"><span class="cur">P 12,680</span></div></div>
    <div class="body">
      <div class="card">
        <div class="row between"><span class="h2">이번 달 공동 창고</span><span class="badge">참여량 집계</span></div>
        <div class="gauge"><i style="width:68%"></i></div>
        <div class="row between">
          <span class="s">전체 참여 68%</span>
          <span class="s">달성 시 사료 200kg 기부</span>
        </div>
        <div class="s">내 기여: 3,400 P</div>
      </div>
      <div class="card">
        <div class="h3">사료 기부하기</div>
        <div class="row" style="gap:6px">
          <span class="chip">500 P</span><span class="chip on">1,000 P</span><span class="chip">3,000 P</span>
        </div>
        <div class="btn pri wide">1,000 P 기부</div>
      </div>
      <div class="honest">모의 기부 — 데모 빌드에서는 실물이 발송되지 않아요</div>
      <div class="row" style="gap:8px">
        <div class="btn grow" data-goto="e02-designate">보호소 지정 후원</div>
        <div class="btn grow" data-goto="e03-report">집행 내역</div>
      </div>
    </div>
    <div class="tabbar">
      <div class="tab"><span class="ic">■</span>마당</div>
      <div class="tab"><span class="ic">■</span>게임</div>
      <div class="tab"><span class="ic">■</span>추천</div>
      <div class="tab on"><span class="ic">■</span>후원</div>
      <div class="tab"><span class="ic">■</span>내정보</div>
    </div>
  `,
  notes: [
    '게이지 라벨은 "모금액" 금지 — 참여량·판매 진행률로 표기(§6.1, §6.5). 실제 재원은 과금·판매.',
    '"모의 기부" 라벨은 DONATION_MODE=mock일 때 필수 노출, 숨김 분기 금지(§6.5 정직성 규칙).',
    '기부 차감·적립은 Edge Function + 원장. 기부 전환 구간은 사후 정산 홀드(§5.5).'
  ]
});

S.push({
  id: 'e02-designate',
  no: 'E-02',
  group: 'E. 후원',
  prd: '§5.4 · §6.5',
  title: '지정 후원',
  purpose: '특정 보호소·보호견에 포인트 배분.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">지정 후원</span><div class="hud"><span class="cur">P 12,680</span></div></div>
    <div class="body">
      <div class="card sel">
        <div class="row top">
          <div class="img" style="width:56px;height:56px;flex:none"><span>사진</span></div>
          <div class="grow col" style="gap:2px">
            <div class="row between"><span class="h3">보리</span><span class="badge">노원구 동물보호센터</span></div>
            <div class="s">이번 달 후원 참여 12명</div>
          </div>
        </div>
      </div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:56px;height:56px;flex:none"><span>사진</span></div>
          <div class="grow col" style="gap:2px">
            <div class="row between"><span class="h3">도봉구 보호소 전체</span><span class="badge">봉사자 부족</span></div>
            <div class="s">이번 달 후원 참여 4명</div>
          </div>
        </div>
      </div>
      <div class="col">
        <div class="label">배분할 포인트</div>
        <div class="field">2,000 P</div>
      </div>
      <div class="xs">배분처가 몰리지 않게 같은 보호소에 연속으로 배분되지 않아요 (순환 배분)</div>
    </div>
    <div class="footer">
      <div class="btn pri wide">보리에게 2,000 P 배분</div>
    </div>
  `,
  notes: [
    '포인트는 재원이 아니라 분배 의사 — 집행액은 판매·과금 재원에서 순환 배분 규칙으로 산정(§6.1, §6.5).',
    '배분 트랜잭션은 Edge Function + 원장(§5.5).'
  ]
});

S.push({
  id: 'e03-report',
  no: 'E-03',
  group: 'E. 후원',
  prd: '§6.5',
  title: '집행 내역 · 결과 리포트',
  purpose: '전건 공개 — 금액·수혜처·일자·증빙 사진 + 미집행 이월분.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">기부 집행 내역</span></div>
    <div class="body">
      <div class="card">
        <div class="row between"><span class="h3">6월 사료 200kg</span><span class="badge ok">집행 완료</span></div>
        <div class="row top">
          <div class="img" style="width:76px;height:56px;flex:none"><span>수령 사진</span></div>
          <div class="grow col" style="gap:2px">
            <div class="s">노원구 동물보호센터 · 7월 2일 수령</div>
            <div class="xs">집행액 480,000원 · 참여 1,240명</div>
          </div>
        </div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">6월 방한용품 캠페인</span><span class="badge warn">목표 미달 종료</span></div>
        <div class="s">312/500벌로 종료 — 약정에 따라 브랜드가 200세트로 축소 집행했어요. 결과를 그대로 공개합니다.</div>
      </div>
      <div class="card flat">
        <div class="row between"><span class="s">미집행 이월분</span><span class="s mono">120,000원</span></div>
        <div class="xs">다음 달 집행분에 합산돼요</div>
      </div>
    </div>
  `,
  notes: [
    '전건 공개 + 미달성 캠페인도 결과 공개(§6.5 — 조용히 사라지는 캠페인 금지).',
    '수령 확인 사진은 보호소가 외부 채널(문자·메일 링크)로 제출 — 앱 범위 밖. 등록되면 기여 유저에게 리포트 푸시.'
  ]
});

S.push({
  id: 'e04-certificate',
  no: 'E-04',
  group: 'E. 후원',
  prd: '§6.3 · §6.5',
  title: '기부 증서',
  purpose: '주간 랭킹 1위 명의. 명예는 시간으로만 얻는다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">기부 증서</span><span class="act">공유</span></div>
    <div class="body" style="justify-content:center">
      <div class="card center" style="gap:10px;padding:24px 16px">
        <div class="xs">D+ 기부 증서 · 2026년 7월 3주</div>
        <div class="h1">멍멍이집사 님</div>
        <div class="s">전체 유저의 참여로 모인 사료 200kg이<br>노원구 동물보호센터에 전달되었습니다</div>
        <div class="divider" style="width:60%"></div>
        <div class="xs">명의는 주간 랭킹 1위에게 드립니다.<br>랭킹은 플레이·돌봄 점수만 집계합니다 (결제 전환분 제외)</div>
        <div class="row center" style="gap:8px;justify-content:center">
          <span class="badge">스폰서 · OO펫푸드</span>
        </div>
      </div>
    </div>
  `,
  notes: [
    '명의 산정은 §5.5 랭킹 규칙(과금 유래 제외) 그대로 — 증서에 그 사실 명기.',
    '공유 이미지는 서버 렌더(위변조 방지).'
  ]
});

})(window.SCREENS);
