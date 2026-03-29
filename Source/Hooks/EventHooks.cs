using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
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
        Plugin.Log.Info($"Level finished with end state: {levelCompletionResults.levelEndStateType} using modifiers: {__instance.gameplayModifiers}");

        string practiceResult = __instance.practiceSettings == null ? "null" : "not null";
        Plugin.Log.Info($"Practice settings is {practiceResult}");

        if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed 
            && ArchipelagoLevelIndex.ArchipelagoLevels.Contains(__instance.beatmapLevel)) APConnection.SendDeathLink("too many bloqs :(");


    

        if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared || !__instance.gameplayModifiers.IsWithoutModifiers())
             return;
        
        // Get the level info
        BeatmapKey beatmapKey = __instance.beatmapKey;
        string rank = RankModel.GetRankName(levelCompletionResults.rank);
        // Send completion to Archipelago
        APConnection.CheckLocation(beatmapKey, rank);
        Plugin.Log.Info($"Level cleared; requesting CheckLocation for beatmapKey '{beatmapKey.levelId.ToString()}'");
    }
}
