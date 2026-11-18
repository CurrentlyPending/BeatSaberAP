using BeatSaberAP;
using SongCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

internal static class ArchipelagoLevelIndex {
    public static bool IsReady { get; set; }
    public static IReadOnlyList<BeatmapLevel> CustomLevels { get; private set; }
    public static IReadOnlyList<BeatmapLevel> AllLevels { get; private set; }
    public static IReadOnlyList<BeatmapLevel> ArchipelagoLevels { get; private set; }

    // BeatmapLevelsModel will be set lazily on first Build call
    private static BeatmapLevelsModel _levelsModel;

    /// <summary>
    /// Call this early in your mod to hook SongCore
    /// </summary>
    public static void Initialize() {
        SongCore.Loader.SongsLoadedEvent += OnSongsLoaded;
        SongCore.Loader.OnLevelPacksRefreshed += Build;
    }

    private static void OnSongsLoaded(Loader loader, ConcurrentDictionary<string, BeatmapLevel> levels) {
        BeatSaberAP.Plugin.Log.Info("OnSongsLoaded Called");
        SongCore.Loader.SongsLoadedEvent -= OnSongsLoaded;
        
        Build();

        // stuffs loaded by now, should be safe to call AbpsHandler
        ApbsHandler.HandleApbsOnInit();
    }

    public static void Build() {
        BeatSaberAP.Plugin.Log.Info("Build Called");
        IsReady = false;
        PlaylistLoadHook.levelsReady = false;

        BeatSaberAP.Plugin.Log.Info("Building Level Index...");

        BeatmapLevelsModel _levelsModel = SongCore.Loader.BeatmapLevelsModelSO;
        var repo = _levelsModel.ostAndExtrasBeatmapLevelsRepository;

        var result = new List<BeatmapLevel>();
        foreach (var pack in repo.beatmapLevelPacks)
            result.AddRange(pack.AllBeatmapLevels());

        CustomLevels = SongCore.Loader.CustomLevelsPack.AllBeatmapLevels();
        AllLevels = result.Concat(CustomLevels).ToList();

        BeatSaberAP.Plugin.Log.Info($"Total Levels Loaded: {AllLevels.Count}");
        IsReady = true;
    }
}