using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// Entry point. Runs automatically after the first scene loads, so pressing Play in any scene
    /// (even an empty one) builds the whole game. No scene setup, prefabs or assets required.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.I != null) return;
            var go = new GameObject("NexusStrike");
            go.AddComponent<GameManager>();
        }
    }
}
