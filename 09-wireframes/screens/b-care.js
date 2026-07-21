/* B. 돌봄 루프 — 마당 · 돌봄 (PRD §4.1) */
(function (S) {

S.push({
  id: 'b01-yard',
  no: 'B-01',
  group: 'B. 돌봄',
  prd: '§4.1',
  title: '홈 — 마당',
  purpose: '앱의 기본 화면. 마당 + 캐릭터견 + 오늘 남은 돌봄.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar">
      <span class="t">단추의 마당</span>
      <div class="hud">
        <span class="cur">🐾 3/5</span><span class="cur">P 12,400</span><span class="cur">육포 12</span>
      </div>
    </div>
    <div class="body">
      <div class="row between">
        <div class="col" style="gap:2px">
          <div class="h3">단추 · Lv.7</div>
          <div class="s">컨디션 좋음</div>
        </div>
        <div class="col grow" style="gap:2px">
          <div class="xs">친밀도</div>
          <div class="gauge sm"><i style="width:64%"></i></div>
        </div>
      </div>
      <div class="yard" style="min-height:330px">
        <div class="dog">캐릭터견<br>(아이소메트릭 마당 · 탭하면 반응)</div>
      </div>
      <div class="card flat">
        <div class="row between">
          <span class="h3">오늘의 돌봄 3개 남음</span>
          <span class="btn sm" data-goto="b02-care">전체 보기</span>
        </div>
        <div class="row wrap" style="gap:5px">
          <span class="chip">밥 주기</span><span class="chip">산책 1/2</span><span class="chip on">쓰다듬기 완료</span>
        </div>
      </div>
      <div class="row" style="gap:8px">
        <div class="btn grow" data-goto="b03-feed">쓰다듬기</div>
        <div class="btn grow" data-goto="c01-hub">미니게임</div>
      </div>
    </div>
    <div class="tabbar">
      <div class="tab on"><span class="ic">■</span>마당</div>
      <div class="tab"><span class="ic">■</span>게임</div>
      <div class="tab"><span class="ic">■</span>추천</div>
      <div class="tab"><span class="ic">■</span>후원</div>
      <div class="tab"><span class="ic">■</span>내정보</div>
    </div>
  `,
  notes: [
    '돌봄은 발바닥 소모 없음 — 발바닥은 미니게임 입장 전용(§4.2). 돌봄 버튼에 재화 아이콘 붙이지 말 것.',
    '마당 오브젝트(똥 방치 등)는 상태 데이터로 렌더 — 방치 표현 상한은 "조금 시무룩"(§1.2 원칙 2).',
    '재화 잔액은 서버 조회값 표시만. 증감 계산은 전부 Edge Function(§5.5).'
  ]
});

S.push({
  id: 'b02-care',
  no: 'B-02',
  group: 'B. 돌봄',
  prd: '§4.1',
  title: '오늘의 돌봄 (7종)',
  purpose: '성격에 따라 요구량이 달라지는 일일 체크리스트.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">오늘의 돌봄</span><span class="act">4/7 완료</span></div>
    <div class="body">
      <div class="card flat">
        <div class="li"><span class="badge ok">완료</span><div class="grow"><div class="h3">밥 주기</div><div class="xs">매일 · 친밀도 +10</div></div></div>
        <div class="li"><span class="badge">남음</span><div class="grow"><div class="h3">산책 1/2</div><div class="xs">활동성이 높아 하루 2회를 원해요</div></div><span class="btn sm">하기</span></div>
        <div class="li"><span class="badge">남음</span><div class="grow"><div class="h3">똥 치우기</div><div class="xs">방치하면 마당 청결도가 내려가요</div></div><span class="btn sm">하기</span></div>
        <div class="li"><span class="badge ok">완료</span><div class="grow"><div class="h3">쓰다듬기·놀아주기</div><div class="xs">상시 · 성격별 반응</div></div></div>
        <div class="li"><span class="badge">내일</span><div class="grow"><div class="h3">목욕</div><div class="xs">2~3일 주기 · 겁이 많아 거부할 수 있어요</div></div></div>
        <div class="li"><span class="badge ok">완료</span><div class="grow"><div class="h3">빗질</div><div class="xs">짧은 주기 · 교감 보정</div></div></div>
        <div class="li"><span class="badge ok">완료</span><div class="grow"><div class="h3">원반던지기</div><div class="xs">마당 미니 인터랙션</div></div></div>
      </div>
      <div class="s">오늘 요구량을 모두 채우면 일일 돌봄 완주 포인트를 받아요</div>
    </div>
  `,
  notes: [
    '요구량은 성격 파라미터에서 계산(활동성 4 → 산책 2회). 완주 판정·포인트 지급은 서버.',
    '목욕 거부는 실패가 아니라 분기 — 응답 200 {result:"declined"}, 분석 이벤트명에 fail 금지.'
  ]
});

S.push({
  id: 'b03-feed',
  no: 'B-03',
  group: 'B. 돌봄',
  prd: '§4.1',
  title: '돌봄 실행 (바텀시트)',
  purpose: '돌봄 공통 실행 패턴 — 실행 → 캐릭터견 반응 → 경험치.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body pad0">
      <div class="yard" style="min-height:400px;border:0;border-radius:0">
        <div class="dog">밥그릇 앞으로 다가오는 연출</div>
      </div>
    </div>
    <div class="overlay">
      <div class="sheet">
        <div class="h2">밥 주기</div>
        <div class="aibox">
          <div class="cap">단추</div>
          <div class="s">(꼬리를 흔들며 그릇 앞을 빙글빙글 돌아요)</div>
        </div>
        <div class="row between">
          <span class="s">친밀도 경험치 +10</span>
          <span class="s">발바닥 소모 없음</span>
        </div>
        <div class="btn pri wide">밥 주기</div>
      </div>
    </div>
  `,
  notes: [
    '7종 돌봄이 같은 시트 패턴 재사용 — 행동명·연출·경험치만 교체.',
    '경험치 지급은 서버 멱등 처리(같은 요구 슬롯에 중복 지급 금지).'
  ]
});

S.push({
  id: 'b04-levelup',
  no: 'B-04',
  group: 'B. 돌봄',
  prd: '§5.3',
  title: '레벨업',
  purpose: '레벨업 일시금(주 포인트 경로) + 해금 안내.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="justify-content:center;gap:14px">
      <div class="center col" style="gap:4px">
        <div class="h1">Lv.8이 됐어요!</div>
        <div class="s">단추가 그만큼 마음을 열었다는 뜻이에요</div>
      </div>
      <div class="img" style="height:140px"><span>레벨업 연출</span></div>
      <div class="card">
        <div class="row between"><span class="h3">포인트 보상</span><span class="h2 mono">+900 P</span></div>
        <div class="divider"></div>
        <div class="row between"><span class="s">해금</span><span class="s">마당 타일 2종 · 스킨 슬롯 +1</span></div>
        <div class="row between"><span class="s">일일 획득 상한</span><span class="s mono">1,200 → 1,350 P</span></div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="b01-yard">마당으로</div>
    </div>
  `,
  notes: [
    '레벨업 일시금·상한 수치는 서버 응답값 표시 — 클라에 곡선 상수 복제 금지.',
    'origin=level로 원장 기록(§5.5). 랭킹 집계에 포함되는 유래.'
  ]
});

S.push({
  id: 'b05-return',
  no: 'B-05',
  group: 'B. 돌봄',
  prd: '§4.1',
  title: '오랜만의 재회 (복귀)',
  purpose: '방치 후 복귀. 책망 대신 반가움 + 회복 보너스.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="justify-content:center;gap:14px">
      <div class="yard" style="min-height:260px">
        <div class="dog">달려와서 반기는 연출</div>
      </div>
      <div class="center col" style="gap:4px">
        <div class="h1">단추가 엄청 반가워해요!</div>
        <div class="s">4일 만이에요 · 컨디션이 조금 내려가 Lv.7이 됐어요</div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">재회 보너스</span><span class="badge ok">3일간</span></div>
        <div class="s">경험치 2배로 원래 레벨까지 금방 돌아갈 수 있어요. 마당과 스킨은 그대로예요.</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="b01-yard">단추 만나러 가기</div>
    </div>
  `,
  notes: [
    '하락 규칙(서버): 72시간 유예 → 이후 하루 최대 1레벨 → 성장 단계 하한 밑으로 불가(§4.1).',
    '문구 금지: 책망·굶주림·위험 표현. "시무룩"이 상한.',
    '복귀 푸시도 동일 톤 정책.'
  ]
});

S.push({
  id: 'b06-closet',
  no: 'B-06',
  group: 'B. 돌봄',
  prd: '§5.4 · §7.4',
  title: '마당 꾸미기 · 옷장',
  purpose: '포인트로 마당 해금, 보유 스킨 착용. 상점으로 연결.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">꾸미기</span><div class="hud"><span class="cur">P 12,400</span></div></div>
    <div class="body">
      <div class="row" style="gap:6px">
        <span class="chip on">옷장</span><span class="chip">마당</span>
      </div>
      <div class="row" style="gap:8px">
        <div class="card grow center">
          <div class="img" style="height:70px"><span>스킨</span></div>
          <div class="h3">노란 우비</div>
          <div class="badge ok">착용 중</div>
        </div>
        <div class="card grow center">
          <div class="img" style="height:70px"><span>스킨</span></div>
          <div class="h3">겨울 패딩</div>
          <div class="badge">실물 세트</div>
        </div>
      </div>
      <div class="row" style="gap:8px">
        <div class="card grow center">
          <div class="img" style="height:70px"><span>타일</span></div>
          <div class="h3">잔디 타일</div>
          <div class="btn sm">800 P 해금</div>
        </div>
        <div class="card grow center">
          <div class="img" style="height:70px"><span>타일</span></div>
          <div class="h3">나무 울타리</div>
          <div class="badge">Lv.9 해금</div>
        </div>
      </div>
      <div class="btn gho wide" data-goto="f01-shop">새 스킨 보러 가기 → 상점</div>
    </div>
  `,
  notes: [
    '해금 차감은 Edge Function, 원장 기록(§5.5).',
    '실물 세트 스킨은 회수 유예 정책 대상(§7.6 취소·반품) — 소유 플래그에 출처 구분.'
  ]
});

})(window.SCREENS);
