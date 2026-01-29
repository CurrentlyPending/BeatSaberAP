using BeatSaberAP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

[HarmonyPatch(typeof(LevelCollectionTableView), nameof(LevelCollectionTableView.SetData))]
static class HideEverythingTest {
    static void Prefix(ref IReadOnlyList<BeatmapLevel> beatmapLevels) {
        if (APConnection.session == null) { 
            beatmapLevels = new BeatmapLevel[0];
            Plugin.Log.Info("Hiding all songs, no AP session.");
            return;
        }
        int unlocked = APConnection.song_items_received;
        Plugin.Log.Info($"Hiding songs, unlocked count: {unlocked}");
        beatmapLevels = beatmapLevels.Where(level => {
            var key = level.GetBeatmapKeys().FirstOrDefault();
            Plugin.Log.Info($"Checking song with key {key}");
            if (key == null) {
                return false;
            }
            var identTask = APConnection.GenerateIdentAsync(key);
            identTask.Wait();
            var ident = identTask.Result;
            bool isUnlocked = APConnection.IsSongUnlocked(ident);
            if (isUnlocked) {
                Plugin.Log.Info($"Showing song: {level.songName} by {level.songAuthorName} (ident: {ident})");
            } else {
                Plugin.Log.Info($"Hiding song: {level.songName} by {level.songAuthorName} (ident: {ident})");
            }
            return isUnlocked;
        }).ToList();
    }
}