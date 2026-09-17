using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using BeatSaberAP;
using HMUI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BeatSaberAP.Archipelago;
using UnityEngine;
public static class APConnection {

    public static ArchipelagoSession Session;
    private static DeathLinkService _dlService;
    private static Dictionary<uint,string> _nodeToIdent;
    private static Dictionary<string,uint> _identToNode;
    public static readonly List<string> SongUnlocks = [];
    private static readonly List<int> MapTypeCounts = [];
    private static GameMode GameMode { get; set; }
    public static int NumGrades { get; private set; }
    private static List<int> _startingNodesList = [];
    public static string CampaignName { get; private set; }
    private static int _songItemsReceived;

    private static List<int> _categoryItemsReceived = []; // 0: speed, 1: tech, 2: midspeed, 3: acc

    private static readonly Dictionary<string, string> IdentCache = new();
    private static Task _identBuildTask;
    public static volatile bool IdentReady;

    private static readonly FieldInfo TableViewField =
    typeof(LevelCollectionTableView)
        .GetField("_tableView", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool ConnectAndGetSlotData(string host, int port, string slot, string password) {
        Session = ArchipelagoSessionFactory.CreateSession(host, port);
        LoginResult result = Session.TryConnectAndLogin("Beat Saber", slot, ItemsHandlingFlags.AllItems, new(0, 6, 2), null, null, password);
        if (!result.Successful) {
            Plugin.Log.Error("Could not connect to Archipelago!");
            return false;
        }
        Plugin.Log.Info("Connected to Archipelago");
        Dictionary<string, string> conninfo = new() {
            ["host"] = host,
            ["port"] = port.ToString(),
            ["slot"] = slot,
            ["password"] = password
        };
        string jsonConn = JsonConvert.SerializeObject(conninfo);
        System.IO.File.WriteAllText("AP_ConnInfo.json", jsonConn);
        var success = (LoginSuccessful)result;
        if ((long)success.SlotData["DeathLink"] == 1) {
            _dlService = Session.CreateDeathLinkService();
            _dlService.EnableDeathLink();
            _dlService.OnDeathLinkReceived += RecvDeathLink;
        }

        
        GameMode = (GameMode)JsonConvert.DeserializeObject<int>(success.SlotData["game_mode"].ToString());
        Plugin.Log.Debug("GameMode:" + GameMode);
        CampaignName = (string)success.SlotData["campaign_name"];
        _nodeToIdent = JsonConvert.DeserializeObject<Dictionary<uint, string>>(success.SlotData["node_to_keystr"].ToString());
        _identToNode = JsonConvert.DeserializeObject<Dictionary<string, uint>>(success.SlotData["keystr_to_node"].ToString());
        _startingNodesList.AddRange(JsonConvert.DeserializeObject<List<int>>(success.SlotData["start_songs"].ToString()));
        // this is never used unless its not these two
        if (GameMode is not (GameMode.PresetAcc or GameMode.PresetPass)) {
            MapTypeCounts.AddRange(
                JsonConvert.DeserializeObject<List<int>>(success.SlotData["map_type_counts"].ToString()));
        }
        NumGrades = JsonConvert.DeserializeObject<int>(success.SlotData["num_grades"].ToString());
        JsonConvert.DeserializeObject<Dictionary<string, string>>(success.SlotData["location_name_to_mnemonic"].ToString());

        //Sync Item Counts
        _songItemsReceived = 0;
        _categoryItemsReceived = new List<int>() { 0, 0, 0, 0 }; // speed, tech, midspeed, acc
        //First song is always unlocked
        // Unlock starting songs for each category (node id could be different based on generation, so read from slot data)
        Plugin.Log.Info("Songs already in inventory: ");
        foreach (int i in _startingNodesList) {
            Plugin.Log.Info("Inital Song unlocked: " + i);
            if (_nodeToIdent.TryGetValue((uint)i, out var ident)) {
                Plugin.Log.Info(ident);
                SongUnlocks.Add(ident);
            }
        }
     
        if (GameMode is GameMode.PresetPass or GameMode.PresetAcc) {
            Plugin.Log.Debug("Processing Song Unlocks...");
            // Process progressive global song unlocks
            foreach (ItemInfo i in Session.Items.AllItemsReceived) {
                Plugin.Log.Trace("Item: " + i.ItemName);
                if (i.ItemName != "Progressive Song Unlock") continue;
                _songItemsReceived++;
                if (_songItemsReceived > _nodeToIdent.Count) continue;
                int nodeToUnlock = _songItemsReceived; // Progressive: item 1 unlocks node 1, etc.
                uint nodeKey = (uint)nodeToUnlock;

                if (!_nodeToIdent.ContainsKey(nodeKey)) continue;
                var ident = _nodeToIdent[nodeKey];
                SongUnlocks.Add(ident);
                Plugin.Log.Info("Unlocked: " + ident);
            }
            Plugin.Log.Info($"Connection established, {SongUnlocks.Count} songs in inventory.");
        } else {

            // Process per-category progressive unlocks
            foreach (ItemInfo i in Session.Items.AllItemsReceived) {
                var categoryIndex = i.ItemName switch {
                    "Progressive Speed Unlock" => 0,
                    "Progressive Tech Unlock" => 1,
                    "Progressive Midspeed Unlock" => 2,
                    "Progressive Acc Unlock" => 3,
                    _ => -1
                };
                if (categoryIndex < 0) continue;
                int count = ++_categoryItemsReceived[categoryIndex];
                int offset = MapTypeCounts.Take(categoryIndex).Sum();
                uint nodeKey = (uint)(count + offset);

                if (count > MapTypeCounts[categoryIndex] || !_nodeToIdent.ContainsKey(nodeKey)) continue;
                var ident = _nodeToIdent[nodeKey];
                SongUnlocks.Add(ident);
                Plugin.Log.Info("Unlocked: " + ident);
            }

            int totalUnlocksReceived = _categoryItemsReceived.Sum();
            Plugin.Log.Info($"Connection established, {_startingNodesList.Count + totalUnlocksReceived} songs in inventory.");
        }
        Session.Items.ItemReceived += RecvItem;

        return true;
    }

    private static void RecvDeathLink(DeathLink deathLink) {
        Plugin.Log.Info(deathLink.Cause);
        DeathlinkClass.ForceFail();
    }

    public static void SendDeathLink(string cause) {
        if (_dlService == null) return;
        DeathLink dl = new(Session.Players.ActivePlayer.Alias, cause);
        _dlService.SendDeathLink(dl);
    }

    public static async void CheckLocation(BeatmapKey key, string rank) {
        Plugin.Log.Debug("checking location" + rank);
        // Use GenerateIdentAsync which properly handles the prefix stripping
        string ident = await GenerateIdentAsync(key);

        // Extract just the levelid hex and characteristic from the ident
        string[] parts = ident.Split('_');
        if (parts.Length < 2) {
            Plugin.Log.Warn($"Invalid ident format: {ident}");
            return;
        }

        string levelIdHex = parts[0];
        string characteristic = parts[1];
        string difficulty = parts[2];

        if (parts[0] == "OST") {
            levelIdHex = parts[0] + "_" + parts[1];
            characteristic = parts[2];
            difficulty = parts[3];
        }

        var matchingEntry = _identToNode.FirstOrDefault(kvp =>
            kvp.Key.StartsWith(levelIdHex + "_" + characteristic + "_" + difficulty, StringComparison.OrdinalIgnoreCase)
        );

        if (matchingEntry.Key == null) {
            Plugin.Log.Warn($"No AP location found for map {levelIdHex}_{characteristic}");
            return;
        }

        List<string> grades = ["C", "B", "A", "S", "SS"];
        int gradeIndex = grades.IndexOf(rank);

        long[] locationsToCheck = new long[6];

        switch (GameMode)
        {
            case GameMode.PresetPass:
                await Session.Locations.CompleteLocationChecksAsync(matchingEntry.Value);
                break;
            case GameMode.PresetAcc:
            {
                for (int i = 0; i <= gradeIndex; i++) locationsToCheck[i] = (matchingEntry.Value * 6 + i + 1);
                await Session.Locations.CompleteLocationChecksAsync(locationsToCheck);
                break;
            }
            default:
            {
                // Generate location id for all 4 map categories based on how the apworld does it
                int nodeValue = (int)matchingEntry.Value;
                int cumulativeCount = 0;
                int baseId = 0;
                int localIndex = 0;

                for (int i = 0; i < 4; i++) {
                    if (nodeValue < cumulativeCount + MapTypeCounts[i]) {
                        baseId = 1000 + (i * 1000);
                        localIndex = nodeValue - cumulativeCount;
                        break;
                    }
                    cumulativeCount += MapTypeCounts[i];
                }

                for (int i = 0; i <= gradeIndex; i++) locationsToCheck[i] = (baseId + (localIndex * 6) + i + 1);
                await Session.Locations.CompleteLocationChecksAsync(locationsToCheck);
                break;
            }
        }

        Plugin.Log.Info("Checked location " + matchingEntry.Value + " with ident " + matchingEntry.Key);
    }

    private static void RecvItem(ReceivedItemsHelper helper) {
        ItemInfo item = helper.DequeueItem();
        Plugin.Log.Info($"Item index: {helper.Index}");


        Plugin.Log.Info("Printing currently received items: ");
        foreach(ItemInfo i in helper.AllItemsReceived) {

            Plugin.Log.Info(i.ItemDisplayName);
        }

        if (item.ItemName == "Progressive Song Unlock") {
            _songItemsReceived++;

            if (_songItemsReceived <= _nodeToIdent.Count) {
                int nodeToUnlock = _songItemsReceived; // Progressive: item 1 unlocks node 1, etc.
                
                if (_nodeToIdent.ContainsKey((uint)nodeToUnlock)) {
                    SongUnlocks.Add(_nodeToIdent[(uint)nodeToUnlock]);
                }
                Plugin.Log.Info("Currently received songs: ");
                foreach (var t in SongUnlocks)
                {
                    Plugin.Log.Info(t);
                }

            }
        }
        var categoryIndex = item.ItemName switch {
            "Progressive Speed Unlock" => 0,
            "Progressive Tech Unlock" => 1,
            "Progressive Midspeed Unlock" => 2,
            "Progressive Acc Unlock" => 3,
            _ => -1
        };
        if (categoryIndex >= 0) {
            int count = ++_categoryItemsReceived[categoryIndex];
            int offset = MapTypeCounts.Take(categoryIndex).Sum();

            if (count <= MapTypeCounts[categoryIndex] && _nodeToIdent.ContainsKey((uint)count)) {
                SongUnlocks.Add(_nodeToIdent[(uint)count + (uint)offset]);
                Plugin.Log.Info(SongUnlocks[count + offset]);
            }
        }

        if (item.ItemName == "Victory") {
            ArchipelagoClientState victoryState = ArchipelagoClientState.ClientGoal;
            Session.SetClientState(victoryState);
        }
        Plugin.Log.Info("Received item " + item.ItemId + " (" + _songItemsReceived + " total song items)");
        TriggerSongListRefresh();
    }

    static void TriggerSongListRefresh() {
        MainThreadDispatcher.Enqueue(() => {
            var table = Resources
                .FindObjectsOfTypeAll<LevelCollectionTableView>()
                .FirstOrDefault();

            if (table == null)
                return;

            var tv = TableViewField.GetValue(table) as TableView;
            tv?.ReloadData();
        });
    }


    public static async Task<bool> HaveSong(BeatmapKey key) {
        string ident = await GenerateIdentAsync(key);
        Plugin.Log.Debug("Queried info for:" + ident);
        return SongUnlocks.Contains(ident);
    }
    public static bool IsSongUnlocked(string ident) {
        Plugin.Log.Debug("Checking unlock for: " + ident);
        return SongUnlocks.Contains(ident);
    }

    public static bool TryGetIdent(string levelId, out string ident) {
        lock (IdentCache) {
            return IdentCache.TryGetValue(levelId, out ident);
        }
    }

    
    public static void MakePlayerFail() {
        var gameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().FirstOrDefault();

        gameplayManager?.HandleGameEnergyDidReach0();
    }
    

    public static void StartIdentBuild(IEnumerable<BeatmapLevel> levels) {
        if (_identBuildTask != null)
            return;

        Plugin.Log.Info("Starting building identities...");

        _identBuildTask = Task.Run(async () => {
            foreach (var level in levels) {
                try {
                    var key = level.GetBeatmapKeys().FirstOrDefault();
                    if (key == null)
                        continue;

                    var ident = await GenerateIdentAsync(key);
                    // Plugin.Log.Info($"Caching ident for {level.songName}: {ident}");

                    if (ident.StartsWith("OST_") == false && SongUnlocks.Any(s => s.StartsWith(ident.Split("_")[0] + "_", StringComparison.OrdinalIgnoreCase))) {
                        Plugin.Log.Info("Song already unlocked: " + ident);
                    }
                    lock (IdentCache) {
                        IdentCache[level.levelID] = ident;
                    }
                } catch (Exception ex) {
                    Plugin.Log.Warn($"Ident build failed for {level.levelID}: {ex}");
                }
            }

            IdentReady = true;
        });
        Plugin.Log.Info("Identity Build Complete.");
    }

    public static bool AreIdentsReady => IdentReady;

    public static async Task<string> GenerateIdentAsync(BeatmapKey key) {
        // Normalize level id (remove common prefixes that appear in gameplay)
        string raw = key.levelId ?? string.Empty;
        bool isCustomLevel = false;

        if (raw.StartsWith("custom_level_", StringComparison.OrdinalIgnoreCase)) {
            raw = raw.Substring("custom_level_".Length);
            isCustomLevel = true;
        } else if (raw.StartsWith("level_", StringComparison.OrdinalIgnoreCase)) {
            raw = raw.Substring("level_".Length);
        }

        string identifier;

        if (isCustomLevel) {
            // For custom maps, use BeatSaver ID
            uint levelid = await Plugin.GetMapIDFromHashAsync(raw);
            identifier = levelid.ToString("X");
        } else {
            // For official maps, use the levelID directly (after removing prefix)
            // Format: "OST_<levelId>" to distinguish from custom maps
            identifier = "OST_" + raw;
        }

        string fullIdent = identifier + "_" + key.beatmapCharacteristic.SerializedName() + "_" + ((int)key.difficulty);
        // Plugin.Log.Info($"Generated ident: {fullIdent}");
        return fullIdent;
    }
}
