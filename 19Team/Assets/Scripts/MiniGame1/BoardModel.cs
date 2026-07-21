using System;
using System.Collections.Generic;

namespace MiniGame1
{
    // 순수 C# 보드 로직 — UnityEngine 비의존 (mini-game-1-prd.md §6.2).
    // 좌표계: (x, y), y=0이 보드 맨 아래. 블록은 y가 작은 쪽으로 떨어진다.
    public struct Cell : IEquatable<Cell>
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell o) => X == o.X && Y == o.Y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => X * 397 ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public struct FallMove { public Cell From, To; }
    public struct Refill { public Cell Pos; public int Code; }
    public struct SpecialSpawn { public Cell Pos; public int Code; }

    // 연쇄 1단계에서 일어난 일 전부 — 뷰는 이 목록을 순서대로 재생만 한다.
    public class CascadeStep
    {
        public List<Cell> Cleared = new List<Cell>();
        public List<int> ClearedCodes = new List<int>(); // Cleared와 같은 순서 — 수집 목표 집계용
        public List<SpecialSpawn> Spawned = new List<SpecialSpawn>();
        public List<FallMove> Falls = new List<FallMove>();
        public List<Refill> Refills = new List<Refill>();
        public int MatchedBlocks;   // 색 매치로 제거 (10점)
        public int SpecialBlocks;   // 특수 블록 발동으로 제거 (15점)
        public int BrandBlocks;     // 브랜드 블록 (개당 보너스)
    }

    public class SwapOutcome
    {
        public bool Valid;
        public List<CascadeStep> Steps = new List<CascadeStep>();
    }

    public class BoardModel
    {
        public const int Empty = -1;
        public const int RocketH = 100;  // 가로 한 줄 제거
        public const int RocketV = 101;  // 세로 한 줄 제거
        public const int Bomb = 102;     // 3x3 제거
        public const int Magic = 103;    // 같은 종류 전체 제거
        public const int Brand = 104;    // 브랜드 특수 블록 (§3.1)

        public readonly int Size;
        readonly int _types;
        readonly Random _rng;
        readonly int[,] _grid;
        bool _brandQueued;

        public BoardModel(int size, int types, Random rng)
        {
            Size = size; _types = types; _rng = rng;
            _grid = new int[size, size];
            FillInitial();
        }

        public int Get(int x, int y) => _grid[x, y];
        public int Get(Cell c) => _grid[c.X, c.Y];
        public static bool IsNormal(int code) => code >= 0 && code < 100;
        public static bool IsActivatable(int code) => code == RocketH || code == RocketV || code == Bomb || code == Magic;

        public bool HasBrandOnBoard()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (_grid[x, y] == Brand) return true;
            return false;
        }

        // 다음 리필 때 브랜드 블록 1개를 끼워 넣는다 (동시 최대 1개는 호출부가 보장)
        public void QueueBrandDrop() => _brandQueued = true;

        void FillInitial()
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    _grid[x, y] = RandomCodeAvoidingMatch(x, y);
        }

        int RandomCodeAvoidingMatch(int x, int y)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int c = _rng.Next(_types);
                bool h = x >= 2 && _grid[x - 1, y] == c && _grid[x - 2, y] == c;
                bool v = y >= 2 && _grid[x, y - 1] == c && _grid[x, y - 2] == c;
                if (!h && !v) return c;
            }
            return _rng.Next(_types);
        }

        static bool IsAdjacent(Cell a, Cell b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

        void SwapCodes(Cell a, Cell b)
        {
            int t = _grid[a.X, a.Y];
            _grid[a.X, a.Y] = _grid[b.X, b.Y];
            _grid[b.X, b.Y] = t;
        }

        public SwapOutcome TrySwap(Cell a, Cell b)
        {
            var outcome = new SwapOutcome();
            if (!InBounds(a.X, a.Y) || !InBounds(b.X, b.Y) || !IsAdjacent(a, b)) return outcome;

            int ca = _grid[a.X, a.Y], cb = _grid[b.X, b.Y];
            if (ca == Empty || cb == Empty) return outcome;

            SwapCodes(a, b);

            // 특수 블록이 스왑에 포함되면 매치 없이도 즉시 발동한다 (§2.3)
            var forced = new HashSet<Cell>();
            int magicTarget = Empty;
            if (IsActivatable(ca)) { forced.Add(b); if (ca == Magic) magicTarget = IsNormal(cb) ? cb : PickRandomNormalOnBoard(); }
            if (IsActivatable(cb)) { forced.Add(a); if (cb == Magic && magicTarget == Empty) magicTarget = IsNormal(ca) ? ca : PickRandomNormalOnBoard(); }

            if (forced.Count == 0 && !HasAnyMatch())
            {
                SwapCodes(a, b); // 매치 실패 → 원위치
                return outcome;
            }

            outcome.Valid = true;
            outcome.Steps = ResolveCascades(forced, magicTarget, a, b);
            return outcome;
        }

        int PickRandomNormalOnBoard()
        {
            var found = new List<int>();
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (IsNormal(_grid[x, y]) && !found.Contains(_grid[x, y])) found.Add(_grid[x, y]);
            return found.Count > 0 ? found[_rng.Next(found.Count)] : 0;
        }

        // ---- 매치 탐색 ----

        struct Run { public int Code; public List<Cell> Cells; public bool Horizontal; }

        List<Run> FindRuns()
        {
            var runs = new List<Run>();
            for (int y = 0; y < Size; y++)
            {
                int x = 0;
                while (x < Size)
                {
                    int c = _grid[x, y];
                    if (!IsNormal(c)) { x++; continue; }
                    int end = x;
                    while (end + 1 < Size && _grid[end + 1, y] == c) end++;
                    if (end - x + 1 >= 3)
                    {
                        var cells = new List<Cell>();
                        for (int i = x; i <= end; i++) cells.Add(new Cell(i, y));
                        runs.Add(new Run { Code = c, Cells = cells, Horizontal = true });
                    }
                    x = end + 1;
                }
            }
            for (int x = 0; x < Size; x++)
            {
                int y = 0;
                while (y < Size)
                {
                    int c = _grid[x, y];
                    if (!IsNormal(c)) { y++; continue; }
                    int end = y;
                    while (end + 1 < Size && _grid[x, end + 1] == c) end++;
                    if (end - y + 1 >= 3)
                    {
                        var cells = new List<Cell>();
                        for (int i = y; i <= end; i++) cells.Add(new Cell(x, i));
                        runs.Add(new Run { Code = c, Cells = cells, Horizontal = false });
                    }
                    y = end + 1;
                }
            }
            return runs;
        }

        public bool HasAnyMatch() => FindRuns().Count > 0;

        // ---- 연쇄 해소 ----

        List<CascadeStep> ResolveCascades(HashSet<Cell> forcedTriggers, int magicTarget, Cell? swapA, Cell? swapB)
        {
            var steps = new List<CascadeStep>();
            bool first = true;
            for (int guard = 0; guard < 30; guard++)
            {
                var runs = FindRuns();
                if (runs.Count == 0 && forcedTriggers.Count == 0) break;

                var step = new CascadeStep();
                var matched = new HashSet<Cell>();
                foreach (var run in runs) foreach (var c in run.Cells) matched.Add(c);

                // 특수 블록 생성 (§2.3) — 스왑 셀 우선, 셀은 지우지 않고 특수로 변환
                var spawnCells = new HashSet<Cell>();
                DecideSpecialSpawns(runs, first ? swapA : null, first ? swapB : null, step, spawnCells);
                foreach (var s in spawnCells) matched.Remove(s);

                // 특수 발동 확산 (연쇄 트리거 포함)
                var exploded = new HashSet<Cell>();
                var queue = new Queue<Cell>();
                var processed = new HashSet<Cell>();
                foreach (var c in forcedTriggers) if (IsActivatable(Get(c))) queue.Enqueue(c);
                forcedTriggers.Clear();
                foreach (var c in matched) if (IsActivatable(Get(c))) queue.Enqueue(c);

                while (queue.Count > 0)
                {
                    var s = queue.Dequeue();
                    if (processed.Contains(s)) continue;
                    processed.Add(s);
                    exploded.Add(s);
                    foreach (var e in EffectCells(s, ref magicTarget))
                    {
                        if (matched.Contains(e) || spawnCells.Contains(e)) continue;
                        int code = Get(e);
                        if (code == Empty) continue;
                        if (IsActivatable(code) && !processed.Contains(e)) queue.Enqueue(e);
                        exploded.Add(e);
                    }
                }

                // 브랜드 블록: 인접 매치·폭발에 닿으면 터진다 (§3.1)
                var brandSet = new HashSet<Cell>();
                foreach (var c in AllCellsOf(Brand))
                {
                    if (exploded.Contains(c)) { brandSet.Add(c); continue; }
                    foreach (var n in Neighbors4(c))
                        if (matched.Contains(n) || exploded.Contains(n)) { brandSet.Add(c); break; }
                }
                foreach (var b in brandSet) exploded.Remove(b);

                foreach (var c in matched) { step.Cleared.Add(c); step.ClearedCodes.Add(Get(c)); step.MatchedBlocks++; }
                foreach (var c in exploded) { step.Cleared.Add(c); step.ClearedCodes.Add(Get(c)); step.SpecialBlocks++; }
                foreach (var c in brandSet) { step.Cleared.Add(c); step.ClearedCodes.Add(Get(c)); step.BrandBlocks++; }

                if (step.Cleared.Count == 0 && step.Spawned.Count == 0) break;

                foreach (var c in step.Cleared) _grid[c.X, c.Y] = Empty;
                foreach (var sp in step.Spawned) _grid[sp.Pos.X, sp.Pos.Y] = sp.Code;

                ApplyGravityAndRefill(step);
                steps.Add(step);
                first = false;
            }
            return steps;
        }

        void DecideSpecialSpawns(List<Run> runs, Cell? swapA, Cell? swapB, CascadeStep step, HashSet<Cell> spawnCells)
        {
            // 교차(L/T) → 폭죽 공
            for (int i = 0; i < runs.Count; i++)
                for (int j = i + 1; j < runs.Count; j++)
                {
                    if (runs[i].Code != runs[j].Code || runs[i].Horizontal == runs[j].Horizontal) continue;
                    foreach (var c in runs[i].Cells)
                        if (runs[j].Cells.Contains(c) && !spawnCells.Contains(c))
                        {
                            spawnCells.Add(c);
                            step.Spawned.Add(new SpecialSpawn { Pos = c, Code = Bomb });
                            goto nextPair;
                        }
                    nextPair: ;
                }

            foreach (var run in runs)
            {
                bool taken = false;
                foreach (var c in run.Cells) if (spawnCells.Contains(c)) { taken = true; break; }
                if (taken) continue;

                int code;
                if (run.Cells.Count >= 5) code = Magic;
                else if (run.Cells.Count == 4) code = run.Horizontal ? RocketH : RocketV;
                else continue;

                Cell pos = run.Cells[run.Cells.Count / 2];
                if (swapA.HasValue && run.Cells.Contains(swapA.Value)) pos = swapA.Value;
                else if (swapB.HasValue && run.Cells.Contains(swapB.Value)) pos = swapB.Value;
                spawnCells.Add(pos);
                step.Spawned.Add(new SpecialSpawn { Pos = pos, Code = code });
            }
        }

        IEnumerable<Cell> EffectCells(Cell s, ref int magicTarget)
        {
            int code = Get(s);
            var result = new List<Cell>();
            if (code == RocketH)
                for (int x = 0; x < Size; x++) result.Add(new Cell(x, s.Y));
            else if (code == RocketV)
                for (int y = 0; y < Size; y++) result.Add(new Cell(s.X, y));
            else if (code == Bomb)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (InBounds(s.X + dx, s.Y + dy)) result.Add(new Cell(s.X + dx, s.Y + dy));
            else if (code == Magic)
            {
                if (!IsNormal(magicTarget)) magicTarget = PickRandomNormalOnBoard();
                foreach (var c in AllCellsOf(magicTarget)) result.Add(c);
                magicTarget = Empty; // 연쇄로 또 나오는 매직은 새로 뽑는다
            }
            return result;
        }

        IEnumerable<Cell> AllCellsOf(int code)
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (_grid[x, y] == code) yield return new Cell(x, y);
        }

        IEnumerable<Cell> Neighbors4(Cell c)
        {
            if (InBounds(c.X - 1, c.Y)) yield return new Cell(c.X - 1, c.Y);
            if (InBounds(c.X + 1, c.Y)) yield return new Cell(c.X + 1, c.Y);
            if (InBounds(c.X, c.Y - 1)) yield return new Cell(c.X, c.Y - 1);
            if (InBounds(c.X, c.Y + 1)) yield return new Cell(c.X, c.Y + 1);
        }

        void ApplyGravityAndRefill(CascadeStep step)
        {
            for (int x = 0; x < Size; x++)
            {
                int write = 0;
                for (int y = 0; y < Size; y++)
                {
                    if (_grid[x, y] == Empty) continue;
                    if (y != write)
                    {
                        _grid[x, write] = _grid[x, y];
                        _grid[x, y] = Empty;
                        step.Falls.Add(new FallMove { From = new Cell(x, y), To = new Cell(x, write) });
                    }
                    write++;
                }
            }

            var emptySlots = new List<Cell>();
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (_grid[x, y] == Empty) emptySlots.Add(new Cell(x, y));

            int brandIndex = -1;
            if (_brandQueued && emptySlots.Count > 0 && !HasBrandOnBoard())
            {
                brandIndex = _rng.Next(emptySlots.Count);
                _brandQueued = false;
            }
            for (int i = 0; i < emptySlots.Count; i++)
            {
                var c = emptySlots[i];
                int code = i == brandIndex ? Brand : _rng.Next(_types);
                _grid[c.X, c.Y] = code;
                step.Refills.Add(new Refill { Pos = c, Code = code });
            }
        }

        // ---- 가능한 이동 · 셔플 (§2.1) ----

        public bool TryFindMove(out Cell a, out Cell b)
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                {
                    var c = new Cell(x, y);
                    if (IsActivatable(_grid[x, y]))
                    {
                        // 특수 블록은 아무 방향 스왑으로 발동 가능
                        if (InBounds(x + 1, y)) { a = c; b = new Cell(x + 1, y); return true; }
                        if (InBounds(x, y + 1)) { a = c; b = new Cell(x, y + 1); return true; }
                    }
                    foreach (var n in new[] { new Cell(x + 1, y), new Cell(x, y + 1) })
                    {
                        if (!InBounds(n.X, n.Y)) continue;
                        if (!IsNormal(_grid[x, y]) || !IsNormal(_grid[n.X, n.Y])) continue;
                        SwapCodes(c, n);
                        bool match = HasAnyMatch();
                        SwapCodes(c, n);
                        if (match) { a = c; b = n; return true; }
                    }
                }
            a = default; b = default;
            return false;
        }

        // 가능한 이동이 없을 때 일반 블록만 섞는다. 특수·브랜드는 자리 유지.
        public bool ShuffleIfStuck()
        {
            if (TryFindMove(out _, out _)) return false;
            var cells = new List<Cell>();
            var codes = new List<int>();
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (IsNormal(_grid[x, y])) { cells.Add(new Cell(x, y)); codes.Add(_grid[x, y]); }

            for (int attempt = 0; attempt < 60; attempt++)
            {
                for (int i = codes.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (codes[i], codes[j]) = (codes[j], codes[i]);
                }
                for (int i = 0; i < cells.Count; i++) _grid[cells[i].X, cells[i].Y] = codes[i];
                if (!HasAnyMatch() && TryFindMove(out _, out _)) return true;
            }
            return true; // 마지막 배치라도 반영 (즉시 매치는 다음 스왑에서 해소됨)
        }
    }
}
