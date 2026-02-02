using BeatSaberAP;
using SongCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

internal static class ArchipelagoLevelIndex {
    public static bool IsReady { get; private set; }
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
    }

    private static void OnSongsLoaded(Loader loader, ConcurrentDictionary<string, BeatmapLevel> levels) {
        BeatSaberAP.Plugin.Log.Info("OnSongsLoaded Called");
        SongCore.Loader.SongsLoadedEvent -= OnSongsLoaded;
        CustomLevels = levels.Values.ToList();

        _levelsModel = SongCore.Loader.BeatmapLevelsModelSO;
        Build();

    }

    public static void Build() {
        if (IsReady || CustomLevels == null)
            return;

        BeatSaberAP.Plugin.Log.Info("Building Level Index...");

        

        var repo = _levelsModel.ostAndExtrasBeatmapLevelsRepository;

        var result = new List<BeatmapLevel>();
        foreach (var pack in repo.beatmapLevelPacks)
            result.AddRange(pack.AllBeatmapLevels());

        AllLevels = result.Concat(CustomLevels).ToList();

        BeatSaberAP.Plugin.Log.Info($"Total Levels Loaded: {AllLevels.Count}");
        IsReady = true;
    }
}