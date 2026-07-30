using UnityEngine;

public static class LocalSaveResetOnce
{
    private const string ResetVersionKey = "local_save_reset_version";
    private const int CurrentResetVersion = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnce()
    {
        if (PlayerPrefs.GetInt(ResetVersionKey, 0) >= CurrentResetVersion) return;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt(GameCurrencyStore.PawKey, GameCurrencyStore.MaxPaws);
        PlayerPrefs.SetInt(ResetVersionKey, CurrentResetVersion);
        PlayerPrefs.Save();
        Debug.Log($"[Local Save] PlayerPrefs data was reset once for version {CurrentResetVersion}.");
    }
}
