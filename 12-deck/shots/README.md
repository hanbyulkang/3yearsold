# shots/ — 화면 캡처 자리

발표 자료의 목업 프레임(`.mock`)은 이 폴더의 파일을 **자동으로 찾아** 채운다.
파일이 없으면 점선 자리표시가 그대로 남으므로, 준비되는 화면부터 하나씩 넣으면 된다.

## 넣는 법

화면 번호를 **소문자·하이픈**으로 저장한다. 확장자는 `png` → `jpg` → `webp` 순으로 탐색한다.

```
shots/a-02.png   shots/a-08.png   shots/a-09.png
shots/b-01.png   shots/b-02.png   shots/b-05.png
shots/c-01.png   shots/c-02.png   shots/c-03.png
shots/d-01.png   shots/d-02.png   shots/d-04.png
shots/e-01.png   shots/e-03.png   shots/e-04.png
shots/f-01.png   shots/f-03.png   shots/f-04.png
shots/a-06.png   (설문 Q4 — 08번 슬라이드)
```

## 캡처 규격

- 비율 **393 × 852** (모바일 세로). 프레임이 `object-fit: cover`라 다른 비율은 잘린다.
- 2배 해상도(786 × 1704) 권장 — 빔프로젝터에서 뭉개지지 않는다.
- 상태바(9:41 · 100%)까지 포함해서 잡으면 프레임 안에서 자연스럽다.

## 화면을 더 넣고 싶다면

`index.html`에서 목업 한 칸은 이 한 줄이다. 복사해서 번호만 바꾸면 된다.

```html
<div class="mock" data-no="G-01" data-nm="마이페이지" data-shot="g-01"></div>
```

데스크톱 비율이 필요하면 `class="mock wide"`를 쓴다.
