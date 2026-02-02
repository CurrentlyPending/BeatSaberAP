using BeatSaberAP;
using HarmonyLib;
using IPA.Utilities;
using System;
using System.Linq;
using System.Reflection;

[HarmonyPatch]
public static class EventHooks {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StandardLevelScenesTransitionSetupDataSO), "Finish")]
    private static void OnLevelFinished(StandardLevelScenesTransitionSetupDataSO __instance, LevelCompletionResults levelCompletionResults) {
        // Check if level was completed (not failed/quit)
        Plugin.Log.Info($"Level finished with end state: {levelCompletionResults.levelEndStateType}");
        if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
            return;

        // Get the level info
        BeatmapKey beatmapKey = __instance.beatmapKey;

        // Check if this is from your AP playlist
        // if (!IsFromAPPlaylist(beatmapKey)) return;

        // Send completion to Archipelago
        APConnection.CheckLocation(beatmapKey);
        Plugin.Log.Info($"Level cleared; requesting CheckLocation for beatmapKey '{beatmapKey.levelId.ToString()}'");
    }
    /* Old Mission based hooks - kept for reference
    private static void OnLevelFailed(MissionLevelScenesTransitionSetupDataSO setupdata) {
        GameplayCoreSceneSetupData gc_setupdata = setupdata.GetProperty<GameplayCoreSceneSetupData, LevelScenesTransitionSetupDataSO>("gameplayCoreSceneSetupData");
        string cause = $"Failed to clear {gc_setupdata.beatmapLevel.songName}";
        APConnection.SendDeathLink(cause);
    }
    private static void OnLevelCleared(MissionLevelScenesTransitionSetupDataSO setupdata, MissionCompletionResults results) {
        GameplayCoreSceneSetupData gc_setupdata = setupdata.GetProperty<GameplayCoreSceneSetupData, LevelScenesTransitionSetupDataSO>("gameplayCoreSceneSetupData");
        Plugin.Log.Info($"Level cleared; requesting CheckLocation for beatmapKey '{gc_setupdata.beatmapKey}'");
        APConnection.CheckLocation(gc_setupdata.beatmapKey);
    }
    */
}
