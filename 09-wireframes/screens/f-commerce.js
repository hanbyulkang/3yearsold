/* F. 커머스 — 스킨 · 실물 세트 · 육포 (PRD §7) */
(function (S) {

S.push({
  id: 'f01-shop',
  no: 'F-01',
  group: 'F. 커머스',
  prd: '§7.2',
  title: '상점 홈',
  purpose: '스킨(육포) / 실물 세트(실결제) / 쿠폰(포인트) 3분류. 뽑기 없음.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">상점</span><div class="hud"><span class="cur">P 12,680</span><span class="cur">육포 12</span></div></div>
    <div class="body">
      <div class="row" style="gap:6px">
        <span class="chip on">스킨</span><span class="chip">실물 옷+스킨</span><span class="chip">쿠폰 교환</span>
      </div>
      <div class="row" style="gap:8px">
        <div class="card grow center" data-goto="f02-skin">
          <div class="img" style="height:80px"><span>스킨</span></div>
          <div class="h3">노란 우비</div>
          <div class="s">육포 8</div>
        </div>
        <div class="card grow center" data-goto="f03-set">
          <div class="img" style="height:80px"><span>세트</span></div>
          <div class="h3">겨울 패딩 세트</div>
          <div class="row center" style="justify-content:center;gap:4px"><span class="s">39,000원</span><span class="badge ok">기부 연동</span></div>
        </div>
      </div>
      <div class="row" style="gap:8px">
        <div class="card grow center">
          <div class="img" style="height:80px"><span>스킨</span></div>
          <div class="h3">체크 목도리</div>
          <div class="s">육포 5</div>
        </div>
        <div class="card grow center">
          <div class="img" style="height:80px"><span>스킨</span></div>
          <div class="h3">기본 반다나</div>
          <div class="s">1,500 P</div>
        </div>
      </div>
      <div class="xs">모든 상품은 확정 구매예요 — 뽑기·랜덤박스는 없어요</div>
    </div>
  `,
  notes: [
    '확률형 아이템 없음(§4.5·§7.4) — 기획에서도 추가 금지.',
    '일부 기본 스킨은 포인트 구매 가능(§5.4), 프리미엄은 육포.'
  ]
});

S.push({
  id: 'f02-skin',
  no: 'F-02',
  group: 'F. 커머스',
  prd: '§7.4 · §7.5A',
  title: '스킨 구매 (육포)',
  purpose: 'A 플로우 — 게임 내 즉시 구매. 매출 일부 적립 비율 명시.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">노란 우비</span></div>
    <div class="body">
      <div class="img" style="height:200px"><span>단추 착용 미리보기</span></div>
      <div class="card">
        <div class="row between"><span class="h2">노란 우비</span><span class="h2">육포 8</span></div>
        <div class="s">비 오는 날 마당 연출이 바뀌어요</div>
      </div>
      <div class="box fill">
        <div class="s">이 구매액의 <b>10%</b>는 공동 창고에 적립되어 보호소 기부에 쓰여요</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide">육포 8로 구매</div>
      <div class="xs center">디지털 상품은 사용(착용) 후 청약철회가 제한돼요</div>
    </div>
  `,
  notes: [
    '적립 비율(가안 10%)은 결제 화면 명시 필수(§7.4).',
    '구매·지급·적립 전부 Edge Function 단일 트랜잭션 + 원장(§5.5).'
  ]
});

S.push({
  id: 'f03-set',
  no: 'F-03',
  group: 'F. 커머스',
  prd: '§6.2 · §7.5B',
  title: '실물 옷 + 스킨 세트',
  purpose: 'B 플로우 — 판매 연동 기부 게이지 + 자사몰 새 탭 결제.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">겨울 패딩 세트</span></div>
    <div class="body">
      <div class="img" style="height:160px"><span>실물 옷 + 착용 스킨</span></div>
      <div class="card">
        <div class="row between"><span class="h2">겨울 패딩 세트</span><span class="h2">39,000원</span></div>
        <div class="s">실물 옷 배송 + 같은 디자인 스킨 + 3,900 P 적립</div>
      </div>
      <div class="card">
        <div class="h3">500벌 팔리면, 보호소에 방한용품 100세트</div>
        <div class="gauge"><i style="width:68%"></i></div>
        <div class="row between"><span class="s mono">342/500벌</span><span class="s">구매 시 내 기여 +0.2%</span></div>
        <div class="xs">단추가 입는 이 옷과 같은 옷이 실제 보호견에게 갑니다</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="f04-checkout">자사몰에서 구매 (새 탭)</div>
      <div class="xs center">실물 옷은 일반 반품 규정, 스킨은 지급 후 철회 제한 — 각각 다른 규정이 적용돼요</div>
    </div>
  `,
  notes: [
    '결제는 자사몰 새 탭(§7.6) — iframe 금지(PG 3DS·SameSite·X-Frame-Options 문제).',
    '청약철회 이원 고지(§7.7)는 결제 전 필수.',
    '게이지 반영은 웹훅 수신 후 즉시 — 구매 직후 게임 복귀 시 "내 기여" 갱신 연출.'
  ]
});

S.push({
  id: 'f04-checkout',
  no: 'F-04',
  group: 'F. 커머스',
  prd: '§7.6',
  title: '자사몰 이동 → 지급 대기 → 완료',
  purpose: 'B 플로우의 상태 3단계를 한 화면에서. 팝업 차단 폴백 포함.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">구매 진행</span></div>
    <div class="body">
      <div class="card">
        <div class="row between"><span class="h3">1. 자사몰로 이동</span><span class="badge ok">완료</span></div>
        <div class="xs">새 탭이 안 열렸다면 ↓</div>
        <div class="row" style="gap:8px">
          <div class="btn sm">링크 다시 열기</div>
          <div class="img" style="width:56px;height:56px;flex:none"><span>QR</span></div>
        </div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">2. 자사몰에서 결제</span><span class="badge">진행 중</span></div>
        <div class="xs">결제를 마치면 이 화면이 자동으로 바뀌어요</div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">3. 스킨 지급</span><span class="badge">대기</span></div>
      </div>
      <div class="divider"></div>
      <div class="aibox">
        <div class="cap">완료 상태 (지급 후)</div>
        <div class="s">단추: (새 옷을 입고 빙글 돌아요) 옷이 도착했어요! 판매 게이지도 +0.2% 올랐어요.</div>
      </div>
    </div>
  `,
  notes: [
    'window.open은 버튼 클릭 제스처 컨텍스트에서만(§7.6) — 아니면 팝업 차단.',
    '지급 알림: Supabase Realtime 수신, 유실 시 탭 복귀에 intents 폴링 폴백. 웹훅은 HMAC 서명 검증 + order_id 멱등, order_token TTL 30분, 30분 CRON 재대조(§7.6 실패 처리 표 전부 구현 대상).'
  ]
});

S.push({
  id: 'f05-jerky',
  no: 'F-05',
  group: 'F. 커머스',
  prd: '§7.7',
  title: '육포 충전',
  purpose: '과금. 월 한도·미성년 동의 등 준법 요건이 붙는 지점.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">육포 충전</span><div class="hud"><span class="cur">육포 12</span></div></div>
    <div class="body">
      <div class="card"><div class="row between"><span class="h3">육포 10</span><span class="btn sm">1,100원</span></div></div>
      <div class="card"><div class="row between"><span class="h3">육포 55</span><span class="btn sm">5,500원</span></div></div>
      <div class="card"><div class="row between"><span class="h3">육포 120</span><span class="btn sm">11,000원</span></div></div>
      <div class="card flat">
        <div class="row between"><span class="s">이번 달 결제</span><span class="s mono">16,500 / 100,000원</span></div>
        <div class="gauge sm"><i style="width:17%"></i></div>
      </div>
      <div class="honest">
        만 19세 미만은 법정대리인 동의 후 결제할 수 있으며 별도의 월 한도가 적용돼요.
        동의 없는 미성년자 결제는 취소될 수 있어요.
      </div>
      <div class="xs">카드 정보는 게임 서버에 저장되지 않아요 (결제사 처리) · 육포 매출 일부는 공동 창고에 적립돼요</div>
    </div>
  `,
  notes: [
    '미성년 판정 = 가입 생년월일. 동의 절차는 결제 직전 별도 플로우(본인인증 포함) — 이 화면에서 분기.',
    '월 한도는 서버 강제(클라 표시용 아님). 한도 초과 시 결제 버튼 비활성.',
    '육포→포인트 1:100(가안)·육포→발바닥 전환 UI는 각 사용 지점(C-04 등)에 위치, 역방향 없음(§5.2).'
  ]
});

})(window.SCREENS);
