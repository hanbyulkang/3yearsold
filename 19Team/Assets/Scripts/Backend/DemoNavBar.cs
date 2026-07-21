using UnityEngine;

namespace Backend
{
    /// <summary>
    /// Keeps the demo backend session warm without creating the old scene-navigation UI.
    /// Scene changes are now owned by the project's real UI.
    /// </summary>
    public static class DemoNavBar
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapSession()
        {
            _ = AppSession.EnsureSignedIn();
        }
    }
}
