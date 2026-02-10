using BeatSaberAP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

[HarmonyPatch(typeof(LevelCollectionTableView), nameof(LevelCollectionTableView.SetData))]
static class PlaylistLoadHook {
    static IReadOnlyList<BeatmapLevel> originalLevels;
    static readonly Dictionary<string, string> identCache = new();
    private static bool levelsReady = false;

    static void Prefix(ref IReadOnlyList<BeatmapLevel> beatmapLevels) {

        Plugin.Log.Info($"[PlaylistLoadHook] Called with {beatmapLevels?.Count ?? -1} songs. originalLevels is {(originalLevels == null ? "null" : "set")}. Session is {(APConnection.session == null ? "null" : "connected")}");

        if (ArchipelagoLevelIndex.AllLevels != null && levelsReady == false) {
            originalLevels = ArchipelagoLevelIndex.AllLevels;
            Plugin.Log.Info($"Initializing with {ArchipelagoLevelIndex.AllLevels.Count} songs");
            APConnection.StartIdentBuild(originalLevels);
            levelsReady = true;
        }

        if (!ArchipelagoLevelIndex.IsReady) { 
            BeatSaberAP.Plugin.Log.Info("Building ArchipelagoLevelIndex...");
        }

        if (APConnection.session == null) {
            beatmapLevels = Array.Empty<BeatmapLevel>();
            return;
        }
        
        if (!APConnection._identReady) {
            beatmapLevels = Array.Empty<BeatmapLevel>(); 
            Plugin.Log.Info("Identity cache constructing...");
            return;
        }

        beatmapLevels = originalLevels.Where(level => {
            if (!APConnection.TryGetIdent(level.levelID, out var ident)) { 
                Plugin.Log.Warn($"Could not get Ident for {level.songName}, hiding it from beatmap list");
                return false; // ident not ready yet
            }

            // Extract the identifier portion (everything before characteristic)
            // For custom maps: "43A2E_Standard_4" -> "43A2E"
            // For official maps: "OST_100Bills_Standard_2" -> "OST_100Bills"
            string mapIdentifier;
            if (ident.StartsWith("OST_")) {
                // For official maps, we need to get "OST_<levelid>"
                // Split and take first 2 parts: ["OST", "100Bills", "Standard", "2"]
                var parts = ident.Split('_');
                if (parts.Length < 3) {
                    Plugin.Log.Warn($"Invalid official map ident format: {ident}");
                    return false;
                }
                mapIdentifier = parts[0] + "_" + parts[1]; // "OST_100Bills"
            } else {
                // For custom maps, just take the hex ID (first part)
                mapIdentifier = ident.Split('_')[0]; // "43A2E"
            }

            // Check if any unlocked song matches this map identifier
            bool isUnlocked = APConnection.song_unlocks.Any(s => s.StartsWith(mapIdentifier + "_", StringComparison.OrdinalIgnoreCase));

            if (isUnlocked) {
                Plugin.Log.Debug($"Map unlocked - levelID: {level.levelID}, ident: {ident}, identifier: {mapIdentifier}");
            }

            return isUnlocked;
        }).ToList();

        Plugin.Log.Info($"Filtered playlist from {originalLevels.Count} to {beatmapLevels.Count} beatmaps.");

        for (int i = 0; i < beatmapLevels.Count; i++) {
            var level = beatmapLevels[i];
            if (!APConnection.TryGetIdent(level.levelID, out var ident)) {
                Plugin.Log.Warn("[NON DESTRUCTIVE] Ident not yet built, could not generate beatmap list");
                continue; // ident not ready yet
            }
            Plugin.Log.Debug($"Included beatmap {i}: levelID='{level.levelID}', ident='{ident}'");
        }

        
    }
}