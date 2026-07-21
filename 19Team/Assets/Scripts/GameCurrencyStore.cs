using System;
using UnityEngine;

public static class GameCurrencyStore
{
    public const int MaxPaws = 5;
    public const int RecoverySeconds = 10 * 60;
    public const string PawKey = "mg1_paws";
    public const string BoneKey = "mg1_bones_total";
    private const string NextRecoveryKey = "game_paws_next_recovery_utc";
    private const string EntryReservationKey = "game_paw_entry_reservation";

    public static event Action Changed;

    public static int GetPaws()
    {
        EnsureInitialized();
        ApplyRecovery();
        return Mathf.Clamp(PlayerPrefs.GetInt(PawKey, MaxPaws), 0, MaxPaws);
    }

    public static int GetBones() => Mathf.Max(0, PlayerPrefs.GetInt(BoneKey, 0));

    public static int SecondsUntilNextPaw()
    {
        int paws = GetPaws();
        if (paws >= MaxPaws) return 0;
        long next = ReadNextRecovery();
        return Mathf.Max(0, (int)(next - Now));
    }

    public static bool TrySpendPaw(bool reserveForGameScene = false)
    {
        int paws = GetPaws();
        if (paws <= 0) return false;

        bool wasFull = paws >= MaxPaws;
        PlayerPrefs.SetInt(PawKey, paws - 1);
        if (wasFull || ReadNextRecovery() <= 0) WriteNextRecovery(Now + RecoverySeconds);
        if (reserveForGameScene) PlayerPrefs.SetInt(EntryReservationKey, 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    public static bool ConsumeEntryReservation()
    {
        if (PlayerPrefs.GetInt(EntryReservationKey, 0) <= 0) return false;
        PlayerPrefs.SetInt(EntryReservationKey, 0);
        PlayerPrefs.Save();
        return true;
    }

    public static int AddBones(int amount)
    {
        int total = Mathf.Max(0, GetBones() + amount);
        PlayerPrefs.SetInt(BoneKey, total);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return total;
    }

    public static void SetBones(int amount)
    {
        PlayerPrefs.SetInt(BoneKey, Mathf.Max(0, amount));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetPaws(int amount)
    {
        int paws = Mathf.Clamp(amount, 0, MaxPaws);
        PlayerPrefs.SetInt(PawKey, paws);
        if (paws >= MaxPaws) PlayerPrefs.DeleteKey(NextRecoveryKey);
        else if (ReadNextRecovery() <= 0) WriteNextRecovery(Now + RecoverySeconds);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void EnsureInitialized()
    {
        if (!PlayerPrefs.HasKey(PawKey)) PlayerPrefs.SetInt(PawKey, MaxPaws);
        int paws = Mathf.Clamp(PlayerPrefs.GetInt(PawKey), 0, MaxPaws);
        PlayerPrefs.SetInt(PawKey, paws);
        if (paws < MaxPaws && ReadNextRecovery() <= 0) WriteNextRecovery(Now + RecoverySeconds);
    }

    private static void ApplyRecovery()
    {
        int paws = Mathf.Clamp(PlayerPrefs.GetInt(PawKey, MaxPaws), 0, MaxPaws);
        if (paws >= MaxPaws)
        {
            PlayerPrefs.DeleteKey(NextRecoveryKey);
            return;
        }

        long next = ReadNextRecovery();
        if (next <= 0) next = Now + RecoverySeconds;
        long now = Now;
        if (now < next)
        {
            WriteNextRecovery(next);
            return;
        }

        int recovered = 1 + (int)((now - next) / RecoverySeconds);
        int newPaws = Mathf.Min(MaxPaws, paws + recovered);
        PlayerPrefs.SetInt(PawKey, newPaws);
        if (newPaws >= MaxPaws) PlayerPrefs.DeleteKey(NextRecoveryKey);
        else WriteNextRecovery(next + (long)recovered * RecoverySeconds);
        PlayerPrefs.Save();
        if (newPaws != paws) Changed?.Invoke();
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static long ReadNextRecovery()
    {
        string raw = PlayerPrefs.GetString(NextRecoveryKey, "0");
        return long.TryParse(raw, out long value) ? value : 0;
    }
    private static void WriteNextRecovery(long value) => PlayerPrefs.SetString(NextRecoveryKey, value.ToString());
}
