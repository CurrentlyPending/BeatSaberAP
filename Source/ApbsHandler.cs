using BeatSaberAP;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using IPA.Utilities;


public static class ApbsHandler {
    public static void HandleApbsOnInit() {
        Task.Run(() => {
            // Delay to ensure other initializations are done
            System.Threading.Thread.Sleep(5000);
            HandleAbpsInternal();
        });
    }
    private static void HandleAbpsInternal() {
        Plugin.Log.Info("Handling .apbs files on initialization...");

        // Adjust if your playlists folder path differs
        string playlistsPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Playlists"
        );
        Plugin.Log.Info($"Checking in {playlistsPath} for .apbs files to process...");

        if (!Directory.Exists(playlistsPath)) {
            Plugin.Log.Info("Playlists folder not found, skipping .apbs processing.");
            return;
        }

        foreach (var f in Directory.GetFiles(playlistsPath))
            Plugin.Log.Info(f);

        string[] apbsFiles = Directory.GetFiles(playlistsPath, "*.apbs");

        if (apbsFiles.Length == 0) {
            Plugin.Log.Info("No .apbs files found.");
        }

        foreach (string apbsPath in apbsFiles) {
            try {
                ProcessApbsFile(apbsPath, playlistsPath);
            } catch (Exception ex) {
                Plugin.Log.Warn($"Failed to process {apbsPath}: {ex}");
            }
        }
    }
    private static void ProcessApbsFile(string apbsPath, string playlistsPath) {
        Plugin.Log.Info($"Processing {apbsPath}...");

        string zipPath = Path.ChangeExtension(apbsPath, ".zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        File.Move(apbsPath, zipPath);

        using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
            // Verify there's at least one .bplist file before extracting
            bool hasBplist = archive.Entries
                .Any(e => e.FullName.EndsWith(".bplist", StringComparison.OrdinalIgnoreCase));

            if (!hasBplist)
                throw new FileNotFoundException("No .bplist found in archive!");

            // Extract ALL entries
            foreach (ZipArchiveEntry entry in archive.Entries) {
                // Skip directory entries (they have no filename)
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                if (!entry.FullName.EndsWith(".bplist", StringComparison.OrdinalIgnoreCase))
                    continue;

                string outputPath = Path.Combine(
                    playlistsPath,
                    Path.GetFileName(entry.FullName)
                );

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                entry.ExtractToFile(outputPath);
                Plugin.Log.Info($"Extracted: {entry.FullName}");
            }
        }

        File.Delete(zipPath);
    }
}