/* D. 연결 루프 — AI 추천 · 참여 (PRD §4.3, §4.4) */
(function (S) {

S.push({
  id: 'd01-home',
  no: 'D-01',
  group: 'D. 추천',
  prd: '§4.3 · §4.4',
  title: '추천 홈 — 다음 한 걸음',
  purpose: 'AI가 지금 이 유저에게 맞는 다음 행동 하나만 제안.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">추천</span></div>
    <div class="body">
      <div class="aibox">
        <div class="cap">AI · 다음 한 걸음</div>
        <div class="s">
          매일 산책을 거르지 않으시네요. 지금은 <b>주말 봉사 한 번</b>이 잘 맞아 보여요.
          서울 노원구 보호소가 주말 산책 봉사자를 찾고 있어요.
        </div>
        <div class="btn pri wide" data-goto="d05-apply">봉사 신청 보기</div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">나에게 맞는 보호견</span><span class="btn sm" data-goto="d02-dogs">전체</span></div>
        <div class="row" style="gap:8px">
          <div class="col center grow"><div class="img" style="height:64px;width:100%"><span>사진</span></div><div class="xs">보리 · 노원구</div></div>
          <div class="col center grow"><div class="img" style="height:64px;width:100%"><span>사진</span></div><div class="xs">콩이 · 도봉구</div></div>
          <div class="col center grow"><div class="img" style="height:64px;width:100%"><span>사진</span></div><div class="xs">누리 · 성북구</div></div>
        </div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">참여 방법 알아보기</span><span class="btn sm" data-goto="d04-ways">보기</span></div>
        <div class="s">후원 · 봉사 · 임시보호 · 입양 — 어느 단계에 있어도 괜찮아요</div>
      </div>
    </div>
    <div class="tabbar">
      <div class="tab"><span class="ic">■</span>마당</div>
      <div class="tab"><span class="ic">■</span>게임</div>
      <div class="tab on"><span class="ic">■</span>추천</div>
      <div class="tab"><span class="ic">■</span>후원</div>
      <div class="tab"><span class="ic">■</span>내정보</div>
    </div>
  `,
  notes: [
    '"다음 한 걸음"은 항상 1개만(§4.4). 온보딩 설문 + 돌봄·플레이 데이터가 입력, 설문 재요청 없음(§4.3).',
    '추천 근거 문구는 LLM 생성 — 유저 행동을 구체적으로 인용하게 프롬프트 구성.'
  ]
});

S.push({
  id: 'd02-dogs',
  no: 'D-02',
  group: 'D. 추천',
  prd: '§4.3',
  title: '보호견 추천 목록',
  purpose: '카드마다 추천 이유 + 보호소 지역(시·군·구) 표시. 지역 필터는 선택.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">나에게 맞는 보호견</span></div>
    <div class="body">
      <div class="row wrap" style="gap:5px">
        <span class="chip" data-toggle>지역 선택</span>
        <span class="chip on" data-toggle>서울 노원구</span>
        <span class="chip" data-toggle>전체</span>
      </div>
      <div class="card" data-goto="d03-dog-detail">
        <div class="row top">
          <div class="img" style="width:76px;height:76px;flex:none"><span>사진</span></div>
          <div class="grow col" style="gap:3px">
            <div class="row between"><span class="h2">보리</span><span class="badge">노원구</span></div>
            <div class="s">믹스 · 추정 3세 · 12kg · 여아</div>
          </div>
        </div>
        <div class="aibox">
          <div class="cap">왜 추천하나요</div>
          <div class="s">혼자 있는 시간을 잘 견디는 아이예요. 평일에 집을 비우신다고 하셨죠.</div>
        </div>
      </div>
      <div class="card" data-goto="d03-dog-detail">
        <div class="row top">
          <div class="img" style="width:76px;height:76px;flex:none"><span>사진</span></div>
          <div class="grow col" style="gap:3px">
            <div class="row between"><span class="h2">콩이</span><span class="badge">도봉구</span></div>
            <div class="s">시바 믹스 · 추정 2세 · 9kg · 남아</div>
          </div>
        </div>
        <div class="aibox">
          <div class="cap">왜 추천하나요</div>
          <div class="s">단추와 성격이 비슷해요. 낯을 가리지만 산책을 아주 좋아하는 아이예요.</div>
        </div>
      </div>
      <div class="box fill center">
        <div class="s">선택한 지역에 더 없어요 — 필터를 풀면 12마리를 더 볼 수 있어요</div>
        <div class="btn sm" style="align-self:center">전체 보기</div>
      </div>
    </div>
  `,
  notes: [
    'GPS 미사용·거리 계산 없음(§4.3). 위치는 보호소의 시·군·구 표시뿐, 지역 필터는 유저가 직접 선택.',
    '보호견 데이터는 국가동물보호정보시스템 공공 API 동기화(§6.4) — 보호소 입력 없음.',
    '추천 이유는 사용자별 재생성 — 같은 보호견도 사람마다 다른 이유(§4.3).'
  ]
});

S.push({
  id: 'd03-dog-detail',
  no: 'D-03',
  group: 'D. 추천',
  prd: '§4.3',
  title: '보호견 상세',
  purpose: '보호소 보유 사진 + 공고 데이터 + AI 재구성 소개문.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">보리</span></div>
    <div class="body">
      <div class="img" style="height:180px"><span>보호소 보유 사진</span></div>
      <div class="row wrap" style="gap:5px">
        <span class="badge">믹스</span><span class="badge">추정 3세</span><span class="badge">12kg</span>
        <span class="badge">여아</span><span class="badge">보호 시작 2026-03-02</span>
      </div>
      <div class="aibox">
        <div class="cap">AI가 소개해요</div>
        <div class="s">
          공고에는 "겁 많음, 검정, 믹스" 세 줄뿐이지만 — 보리는 처음 보는 사람 앞에서 몸을 낮추다가도,
          간식을 내밀면 조심스럽게 다가오는 아이예요. 혼자 있는 시간을 잘 견뎌서,
          평일 낮에 집을 비우는 당신의 생활에도 무리가 없어요.
        </div>
        <div class="xs">보호소 공고 데이터를 바탕으로 당신의 설문에 맞춰 작성했어요</div>
      </div>
      <div class="card flat">
        <div class="row between"><span class="s">보호소</span><span class="s b">노원구 동물보호센터</span></div>
        <div class="row between"><span class="s">지역</span><span class="s">서울 노원구</span></div>
        <div class="row between"><span class="s">공고번호</span><span class="s mono">서울-노원-2026-00127</span></div>
      </div>
    </div>
    <div class="footer">
      <div class="row" style="gap:8px">
        <div class="btn sec grow" data-goto="e02-designate">이 아이 후원</div>
        <div class="btn pri grow" data-goto="d04-ways">만나러 가는 방법</div>
      </div>
    </div>
  `,
  notes: [
    'AI 소개문은 공고 원본 필드만 근거로 생성 — 없는 사실(건강 상태 등) 창작 금지, 프롬프트에 원본 필드 화이트리스트 주입.',
    '원본 공고 정보도 접을 수 있게 병기(AI 문구와 사실 구분 가능해야 함).'
  ]
});

S.push({
  id: 'd04-ways',
  no: 'D-04',
  group: 'D. 추천',
  prd: '§4.4 · §1.2',
  title: '참여 방식 추천',
  purpose: '후원·봉사·임보·입양 중 AI 추천 1개를 상단에. 입양만 장벽을 낮추지 않는다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">참여 방법</span></div>
    <div class="body">
      <div class="card sel">
        <div class="row between"><span class="h2">봉사</span><span class="badge ai">지금 추천</span></div>
        <div class="s">주 1회, 보호소에서 산책·청소를 도와요. 매일 산책을 거르지 않는 당신에게 잘 맞아요.</div>
        <div class="btn pri wide" data-goto="d05-apply">신청하기</div>
      </div>
      <div class="card">
        <div class="h3">후원</div>
        <div class="s">뼈다귀나 물품으로 지금 바로 참여할 수 있어요</div>
      </div>
      <div class="card">
        <div class="h3">임시보호</div>
        <div class="s">입양 전, 정해진 기간 동안 집에서 돌봐요. 사료·병원비는 보호소가 부담해요</div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">입양</span><span class="badge">준비 확인 필요</span></div>
        <div class="s">평생을 함께하는 결정이라 절차를 쉽게 만들지 않아요. 준비 상태를 먼저 같이 확인해요.</div>
        <div class="btn sm" style="align-self:flex-start">준비 상태 확인하기</div>
      </div>
    </div>
  `,
  notes: [
    '추천 1개 선정은 설문+행동 데이터 기반 LLM 판단. 나머지 선택지도 항상 열어둠(§4.4 — 어느 단계든 실패 아님).',
    '입양은 성장치 게이트 없음(§4.5) — AI 준비 상태 진단 + 교육적 안내. 준비 부족 시 대안 행동 제시.'
  ]
});

S.push({
  id: 'd05-apply',
  no: 'D-05',
  group: 'D. 추천',
  prd: '§6.4',
  title: '봉사·임시보호 신청',
  purpose: '간단한 신청 폼. 신청 후 보호소가 연락.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">봉사 신청</span></div>
    <div class="body">
      <div class="card flat">
        <div class="row between"><span class="s">보호소</span><span class="s b">노원구 동물보호센터</span></div>
        <div class="row between"><span class="s">활동</span><span class="s">주말 산책 봉사 (2시간)</span></div>
        <div class="row between"><span class="s">모집</span><span class="s">이번 주 토 · 4명 중 2자리 남음</span></div>
      </div>
      <div class="col"><div class="label">이름</div><div class="field">김지민</div></div>
      <div class="col"><div class="label">연락처</div><div class="field">010-1234-5678</div></div>
      <div class="col"><div class="label">희망일</div><div class="field">7월 26일 (토) 오전</div></div>
      <div class="s">신청하면 보호소에서 1~2일 내에 연락드려요. 준비물은 편한 옷과 운동화면 충분해요.</div>
    </div>
    <div class="footer">
      <div class="btn pri wide">신청하기</div>
    </div>
  `,
  notes: [
    '신청 데이터는 보호소 등록 연락처로 전달(메일·문자) — 보호소용 화면은 만들지 않는다. 보호견 정보도 공공 API 동기화라 보호소 입력 없음(§6.4).',
    '같은 폼 패턴을 임시보호 신청에 재사용(항목만 교체).'
  ]
});

S.push({
  id: 'd06-survey-edit',
  no: 'D-06',
  group: 'D. 추천',
  prd: '§4.3',
  title: '설문 수정',
  purpose: '여건이 바뀌면 유저가 직접 수정. 재수집은 없다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">내 설문</span></div>
    <div class="body">
      <div class="s">여기 답변이 견종·보호견·참여 추천에 그대로 쓰여요. 여건이 바뀌면 고쳐주세요.</div>
      <div class="card flat">
        <div class="li"><div class="grow"><div class="xs">Q1 기본 여건</div><div class="s">28세 · 원룸 · 혼자</div></div><span class="btn sm">수정</span></div>
        <div class="li"><div class="grow"><div class="xs">Q2 함께할 시간</div><div class="s">2~4시간</div></div><span class="btn sm">수정</span></div>
        <div class="li"><div class="grow"><div class="xs">Q3 월 지출</div><div class="s">5~10만원</div></div><span class="btn sm">수정</span></div>
        <div class="li"><div class="grow"><div class="xs">Q4 행동 문제가 생기면</div><div class="s">"왜 짖는지 먼저 찾아볼 것 같아요…"</div></div><span class="btn sm">수정</span></div>
        <div class="li"><div class="grow"><div class="xs">Q5 원하는 하루</div><div class="s">"퇴근하고 같이 산책하는 하루"</div></div><span class="btn sm">수정</span></div>
      </div>
      <div class="xs">수정하면 추천이 새로 계산돼요</div>
    </div>
  `,
  notes: [
    '수정 시 분석 레코드 재생성 → 견종 추천은 유지, 보호견·참여 추천만 갱신.',
    '수정 이력 보관(추천 변화 디버깅용).'
  ]
});

})(window.SCREENS);
