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

    static void Prefix(ref IReadOnlyList<BeatmapLevel> beatmapLevels) {
        if (originalLevels == null) {

            //Ensures SetData has had some time to load in some songs
            if(beatmapLevels.Count > 50) {
                originalLevels = beatmapLevels;
                APConnection.StartIdentBuild(originalLevels);
            } else {
                beatmapLevels = Array.Empty<BeatmapLevel>();
                return;
            }
            
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
                Plugin.Log.Warn("Ident not yet built, could not generate beatmap list");
                return false; // ident not ready yet
            }

            string hexId = ident.Split('_')[0];
            return APConnection.song_unlocks.Any(s => s.StartsWith(hexId + "_"));
        }).ToList();

        Plugin.Log.Info($"Filtered playlist from {originalLevels.Count} to {beatmapLevels.Count} beatmaps.");

        for (int i = 0; i < beatmapLevels.Count; i++) {
            var level = beatmapLevels[i];
            if (!APConnection.TryGetIdent(level.levelID, out var ident)) {
                Plugin.Log.Warn("Ident not yet built, could not generate beatmap list");
                continue; // ident not ready yet
            }
            Plugin.Log.Debug($"Included beatmap {i}: levelID='{level.levelID}', ident='{ident}'");
        }
    }
}