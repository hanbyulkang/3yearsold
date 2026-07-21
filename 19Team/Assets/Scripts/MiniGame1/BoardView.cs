using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGame1
{
    // BoardModel의 결과(CascadeStep 목록)를 순서대로 재생하는 뷰.
    // 규칙 판정은 전혀 하지 않는다 — 모델이 준 스텝을 애니메이션으로 옮길 뿐.
    public class BoardView : MonoBehaviour
    {
        const float SwapDur = 0.12f;
        const float ClearDur = 0.16f;
        const float FallDur = 0.14f;

        public event Action<CascadeStep, int> CascadeStepResolved; // (step, cascadeIndex)
        public event Action ResolveFinished;
        public event Action PlayerActed;
        public event Action MoveCommitted; // 유효한 스왑 1회 = 이동 1 소모

        // 블록 터짐 파티클 (CFX 프리팹, 매치 폭발 1종만 — PRD §9-5 성능 예산)
        public GameObject PopFxPrefab;

        // 다음에 드롭될 브랜드 블록의 로고 (시즌 로테이션 — 드롭 직전에 교체된다)
        public void SetBrandLogo(Sprite s) => _brandLogo = s;

        BoardModel _model;
        RectTransform _root;
        BlockView[,] _views;
        float _cellSize;
        Sprite _brandLogo;
        bool _locked;
        int _runningAnims;
        Coroutine _hintRoutine;
        readonly List<BlockView> _hinted = new List<BlockView>();

        public bool IsResolving => _locked;

        public void Build(BoardModel model, RectTransform root, float cellSize, Sprite brandLogo)
        {
            _model = model; _root = root; _cellSize = cellSize; _brandLogo = brandLogo;
            Clear();
            _views = new BlockView[model.Size, model.Size];
            for (int x = 0; x < model.Size; x++)
                for (int y = 0; y < model.Size; y++)
                    SpawnView(new Cell(x, y), model.Get(x, y));
            RestartHintTimer();
        }

        public void Clear()
        {
            StopAllCoroutines();
            _hintRoutine = null;
            _hinted.Clear();
            _locked = false;
            _runningAnims = 0;
            if (_root == null) return;
            for (int i = _root.childCount - 1; i >= 0; i--)
                Destroy(_root.GetChild(i).gameObject);
        }

        Vector2 CellPos(Cell c)
        {
            float half = (_model.Size - 1) / 2f;
            return new Vector2((c.X - half) * _cellSize, (c.Y - half) * _cellSize);
        }

        BlockView SpawnView(Cell c, int code, bool dropIn = false)
        {
            var v = BlockView.Create(_root, this, c, code, _cellSize, _brandLogo);
            var rt = (RectTransform)v.transform;
            rt.anchoredPosition = dropIn ? CellPos(new Cell(c.X, c.Y)) + new Vector2(0, _cellSize * 2f) : CellPos(c);
            _views[c.X, c.Y] = v;
            if (dropIn) StartCoroutine(MoveTo(rt, CellPos(c), FallDur));
            return v;
        }

        public void RequestSwap(Cell a, Cell b)
        {
            if (_locked) return;
            if (b.X < 0 || b.X >= _model.Size || b.Y < 0 || b.Y >= _model.Size) return;
            PlayerActed?.Invoke();
            ClearHint();
            var outcome = _model.TrySwap(a, b);
            StartCoroutine(PlaySwap(a, b, outcome));
        }

        IEnumerator PlaySwap(Cell a, Cell b, SwapOutcome outcome)
        {
            _locked = true;
            var va = _views[a.X, a.Y];
            var vb = _views[b.X, b.Y];
            yield return AnimateSwapViews(va, vb, a, b);

            if (!outcome.Valid)
            {
                yield return AnimateSwapViews(va, vb, b, a); // 원위치 (§2.1)
                _locked = false;
                RestartHintTimer();
                yield break;
            }

            MoveCommitted?.Invoke();
            int cascade = 0;
            foreach (var step in outcome.Steps)
            {
                CascadeStepResolved?.Invoke(step, cascade);
                SpawnPopFx(step);

                foreach (var c in step.Cleared)
                {
                    var v = _views[c.X, c.Y];
                    if (v != null) { _views[c.X, c.Y] = null; StartCoroutine(ShrinkAndDestroy(v)); }
                }
                foreach (var sp in step.Spawned)
                {
                    var v = _views[sp.Pos.X, sp.Pos.Y];
                    if (v == null) v = SpawnView(sp.Pos, sp.Code);
                    else v.SetCode(sp.Code, _brandLogo);
                    StartCoroutine(Pop((RectTransform)v.transform));
                }
                yield return WaitAnims();

                foreach (var f in step.Falls)
                {
                    var v = _views[f.From.X, f.From.Y];
                    if (v == null) continue;
                    _views[f.From.X, f.From.Y] = null;
                    _views[f.To.X, f.To.Y] = v;
                    v.Cell = f.To;
                    StartCoroutine(MoveTo((RectTransform)v.transform, CellPos(f.To), FallDur));
                }
                foreach (var r in step.Refills)
                    SpawnView(r.Pos, r.Code, dropIn: true);
                yield return WaitAnims();
                cascade++;
            }

            if (_model.ShuffleIfStuck())
                RefreshAllFromModel(); // 가능한 이동 없음 → 자동 셔플 (§2.1, 페널티 없음)

            _locked = false;
            ResolveFinished?.Invoke();
            RestartHintTimer();
        }

        void RefreshAllFromModel()
        {
            for (int x = 0; x < _model.Size; x++)
                for (int y = 0; y < _model.Size; y++)
                {
                    var v = _views[x, y];
                    if (v != null && v.Code != _model.Get(x, y))
                    {
                        v.SetCode(_model.Get(x, y), _brandLogo);
                        StartCoroutine(Pop((RectTransform)v.transform));
                    }
                }
        }

        // ---- 힌트 (§2.1: 4초 무입력 시 가능한 매치 1개 하이라이트) ----

        public void RestartHintTimer()
        {
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            if (!gameObject.activeInHierarchy) return;
            _hintRoutine = StartCoroutine(HintAfterDelay());
        }

        IEnumerator HintAfterDelay()
        {
            yield return new WaitForSeconds(MG1Config.HintDelaySec);
            if (_locked) yield break;
            if (_model.TryFindMove(out var a, out var b))
            {
                var va = _views[a.X, a.Y];
                var vb = _views[b.X, b.Y];
                if (va != null) { _hinted.Add(va); StartCoroutine(Pulse((RectTransform)va.transform)); }
                if (vb != null) { _hinted.Add(vb); StartCoroutine(Pulse((RectTransform)vb.transform)); }
            }
        }

        void ClearHint()
        {
            foreach (var v in _hinted)
                if (v != null) v.transform.localScale = Vector3.one;
            _hinted.Clear();
        }

        // 스텝당 최대 3개 지점만 — WebGL 성능 예산 (§9-5)
        void SpawnPopFx(CascadeStep step)
        {
            if (PopFxPrefab == null || step.Cleared.Count == 0) return;
            var cam = Camera.main;
            int count = Mathf.Min(3, step.Cleared.Count);
            int stride = Mathf.Max(1, step.Cleared.Count / count);
            for (int i = 0; i < step.Cleared.Count && count > 0; i += stride, count--)
            {
                var c = step.Cleared[i];
                var v = _views[c.X, c.Y];
                if (v == null) continue;
                Vector3 pos = v.transform.position;
                if (cam != null) pos += (cam.transform.position - pos).normalized * 1.5f;
                var fx = Instantiate(PopFxPrefab, pos, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 0.6f;
                ReduceCameraShake(fx);
                Destroy(fx, 3f);
            }
        }

        static void ReduceCameraShake(GameObject effectObject)
        {
            CartoonFX.CFXR_Effect effect = effectObject.GetComponent<CartoonFX.CFXR_Effect>();
            if (effect?.cameraShake == null) return;
            effect.cameraShake.shakeStrength *= 0.25f;
        }

        // ---- 애니메이션 유틸 ----

        IEnumerator AnimateSwapViews(BlockView va, BlockView vb, Cell fromA, Cell toB)
        {
            if (va != null) StartCoroutine(MoveTo((RectTransform)va.transform, CellPos(toB), SwapDur));
            if (vb != null) StartCoroutine(MoveTo((RectTransform)vb.transform, CellPos(fromA), SwapDur));
            yield return WaitAnims();
            if (va != null && vb != null)
            {
                _views[fromA.X, fromA.Y] = vb; vb.Cell = fromA;
                _views[toB.X, toB.Y] = va; va.Cell = toB;
            }
        }

        IEnumerator WaitAnims()
        {
            while (_runningAnims > 0) yield return null;
        }

        IEnumerator MoveTo(RectTransform rt, Vector2 target, float dur)
        {
            _runningAnims++;
            Vector2 start = rt.anchoredPosition;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (rt == null) { _runningAnims--; yield break; }
                float k = t / dur;
                rt.anchoredPosition = Vector2.Lerp(start, target, k * k * (3f - 2f * k));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = target;
            _runningAnims--;
        }

        IEnumerator ShrinkAndDestroy(BlockView v)
        {
            _runningAnims++;
            var tr = v.transform;
            for (float t = 0; t < ClearDur; t += Time.deltaTime)
            {
                if (tr == null) break;
                tr.localScale = Vector3.one * (1f - t / ClearDur);
                yield return null;
            }
            if (v != null) Destroy(v.gameObject);
            _runningAnims--;
        }

        IEnumerator Pop(RectTransform rt)
        {
            if (rt == null) yield break;
            rt.localScale = Vector3.one * 0.3f;
            for (float t = 0; t < 0.15f; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t / 0.15f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator Pulse(RectTransform rt)
        {
            while (rt != null && _hinted.Count > 0 && !_locked)
            {
                float s = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }
    }
}
