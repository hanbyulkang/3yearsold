using UnityEngine;

namespace MiniGame1
{
    // 플레이스홀더 사운드 합성 — match-3.wav 등 실제 음원 도착 전까지의 임시.
    // MG1GameManager의 AudioClip 필드가 비어 있을 때만 사용된다.
    public static class MG1AudioSynth
    {
        const int Rate = 44100;

        // 짧은 상승 블립 — 블록 팝
        public static AudioClip Pop()
        {
            return Render("mg1_pop", 0.12f, t =>
            {
                float f = Mathf.Lerp(520f, 780f, t / 0.12f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * Decay(t, 0.12f);
            });
        }

        // 부드러운 클릭 — 스왑
        public static AudioClip Swap()
        {
            return Render("mg1_swap", 0.07f, t =>
                Mathf.Sin(2f * Mathf.PI * 340f * t) * Decay(t, 0.07f) * 0.7f);
        }

        // 두 음 화음 — 특수·브랜드 블록
        public static AudioClip Special()
        {
            return Render("mg1_special", 0.25f, t =>
            {
                float a = Mathf.Sin(2f * Mathf.PI * 660f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 990f * t);
                return (a + b) * 0.5f * Decay(t, 0.25f);
            });
        }

        // 상승 아르페지오 — 피버 시작
        public static AudioClip Fever()
        {
            float[] notes = { 523f, 659f, 784f, 1047f }; // C E G C
            return Render("mg1_fever", 0.45f, t =>
            {
                int idx = Mathf.Min(notes.Length - 1, (int)(t / 0.11f));
                float lt = t - idx * 0.11f;
                return Mathf.Sin(2f * Mathf.PI * notes[idx] * lt) * Decay(lt, 0.11f);
            });
        }

        // 3음 징글 — 결과 화면
        public static AudioClip Result()
        {
            float[] notes = { 784f, 988f, 1175f }; // G B D
            return Render("mg1_result", 0.6f, t =>
            {
                int idx = Mathf.Min(notes.Length - 1, (int)(t / 0.18f));
                float lt = t - idx * 0.18f;
                return Mathf.Sin(2f * Mathf.PI * notes[idx] * lt) * Decay(lt, 0.2f);
            });
        }

        // 잔잔한 4코드 아르페지오 루프 (~9.6초) — BGM 플레이스홀더 (match-3.wav 도착 시 교체)
        public static AudioClip Bgm()
        {
            float[][] chords =
            {
                new[] { 261.6f, 329.6f, 392.0f, 523.3f }, // C
                new[] { 196.0f, 246.9f, 392.0f, 493.9f }, // G
                new[] { 220.0f, 261.6f, 329.6f, 440.0f }, // Am
                new[] { 174.6f, 220.0f, 349.2f, 440.0f }, // F
            };
            int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 1 };
            const float noteDur = 0.3f;
            const float chordDur = noteDur * 8f; // 2.4s × 4코드 = 9.6s
            return Render("mg1_bgm", chordDur * chords.Length, t =>
            {
                int chord = Mathf.Min(chords.Length - 1, (int)(t / chordDur));
                float ct = t - chord * chordDur;
                int noteIdx = Mathf.Min(7, (int)(ct / noteDur));
                float nt = ct - noteIdx * noteDur;
                float f = chords[chord][pattern[noteIdx]];
                float tone = Mathf.Sin(2f * Mathf.PI * f * nt) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * f * 2f * nt) * 0.15f;
                float env = Mathf.Clamp01(nt / 0.02f) * Mathf.Pow(1f - nt / noteDur, 1.6f);
                return tone * env * 0.5f;
            });
        }

        static float Decay(float t, float dur) => Mathf.Clamp01(1f - t / dur) * Mathf.Clamp01(t / 0.005f);

        static AudioClip Render(string name, float dur, System.Func<float, float> wave)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f) * 0.5f;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
