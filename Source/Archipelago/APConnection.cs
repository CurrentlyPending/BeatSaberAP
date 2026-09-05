using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BeatSaberAP;
using HMUI;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BeatSaberAP.Archipelago;
using UnityEngine;
public static class APConnection {

    public static ArchipelagoSession session = null;
    private static DeathLinkService dlservice = null;
    private static Dictionary<uint,string> NodeToIdent = null;
    private static Dictionary<string,uint> IdentToNode = null;
    public static readonly List<string> SongUnlocks = [];
    public static readonly List<int> MapTypeCounts = new List<int>();
    public static Dictionary<string, string> LocationNameToMnemonic = new Dictionary<string, string>();
    public static GameMode GameMode { get; private set; }
    public static int NumGrades { get; private set; }
    private static List<int> StartingNodesList = new List<int>();
    public static string CampaignName { get; private set; }
    public static int song_items_received = 0;

    public static List<int> category_items_received = new List<int>(); // 0: speed, 1: tech, 2: midspeed, 3: acc

    private static readonly Dictionary<string, string> _identCache = new();
    private static Task _identBuildTask;
    public static volatile bool _identReady = false;

    static readonly FieldInfo tableViewField =
    typeof(LevelCollectionTableView)
        .GetField("_tableView", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool ConnectAndGetSlotData(string host, int port, string slot, string password) {
        session = ArchipelagoSessionFactory.CreateSession(host, port);
        LoginResult result = session.TryConnectAndLogin("Beat Saber", slot, ItemsHandlingFlags.AllItems, new(0, 6, 2), null, null, password);
        if (!result.Successful) {
            Plugin.Log.Error("Could not connect to Archipelago!");
            return false;
        }
        Plugin.Log.Info("Connected to Archipelago");
        Dictionary<string, string> conninfo = new();
        conninfo["host"] = host;
        conninfo["port"] = port.ToString();
        conninfo["slot"] = slot;
        conninfo["password"] = password;
        string json_conn = JsonConvert.SerializeObject(conninfo);
        System.IO.File.WriteAllText("AP_ConnInfo.json", json_conn);
        var success = (LoginSuccessful)result;
        if ((long)success.SlotData["DeathLink"] == 1) {
            dlservice = session.CreateDeathLinkService();
            dlservice.EnableDeathLink();
            dlservice.OnDeathLinkReceived += RecvDeathLink;
        }

        
        GameMode = (GameMode)JsonConvert.DeserializeObject<int>(success.SlotData["game_mode"].ToString());
        Plugin.Log.Info("GameMode:" + GameMode);
        CampaignName = (string)success.SlotData["campaign_name"];
        NodeToIdent = JsonConvert.DeserializeObject<Dictionary<uint, string>>(success.SlotData["node_to_keystr"].ToString());
        IdentToNode = JsonConvert.DeserializeObject<Dictionary<string, uint>>(success.SlotData["keystr_to_node"].ToString());
        StartingNodesList.AddRange(JsonConvert.DeserializeObject<List<int>>(success.SlotData["start_songs"].ToString()));
        // this is never used unless its not these two
        if (GameMode is not (GameMode.PresetAcc or GameMode.PresetPass)) {
            MapTypeCounts.AddRange(
                JsonConvert.DeserializeObject<List<int>>(success.SlotData["map_type_counts"].ToString()));
        }
        NumGrades = JsonConvert.DeserializeObject<int>(success.SlotData["num_grades"].ToString());
        LocationNameToMnemonic = JsonConvert.DeserializeObject<Dictionary<string, string>>(success.SlotData["location_name_to_mnemonic"].ToString());

        //Sync Item Counts
        song_items_received = 0;
        category_items_received = new List<int>() { 0, 0, 0, 0 }; // speed, tech, midspeed, acc
        //First song is always unlocked

        Plugin.Log.Info("Songs already in inventory: ");

        if (GameMode is GameMode.PresetPass or GameMode.PresetAcc) {
            // Process progressive global song unlocks
            foreach (ItemInfo i in session.Items.AllItemsReceived) {
                if (i.ItemName == "Progressive Song Unlock") {
                    song_items_received++;
                    if (song_items_received <= NodeToIdent.Count) {
                        int nodeToUnlock = song_items_received; // Progressive: item 1 unlocks node 1, etc.
                        uint nodeKey = (uint)nodeToUnlock;

                        if (NodeToIdent.ContainsKey(nodeKey)) {
                            var ident = NodeToIdent[nodeKey];
                            SongUnlocks.Add(ident);
                            Plugin.Log.Info("Unlocked: " + ident);
                        }
                    }
                }
            }
            Plugin.Log.Info($"Connection established, {SongUnlocks.Count} songs in inventory.");
        } else {
            // Unlock starting songs for each category (node id could be different based on generation, so read from slot data)
            foreach (int i in StartingNodesList) {
                Plugin.Log.Info("Node unlocked: " + i.ToString());
                if (NodeToIdent.TryGetValue((uint)i, out var ident)) {
                    Plugin.Log.Info(ident);
                    SongUnlocks.Add(ident);
                }
            }

            // Process per-category progressive unlocks
            foreach (ItemInfo i in session.Items.AllItemsReceived) {
                var categoryIndex = i.ItemName switch {
                    "Progressive Speed Unlock" => 0,
                    "Progressive Tech Unlock" => 1,
                    "Progressive Midspeed Unlock" => 2,
                    "Progressive Acc Unlock" => 3,
                    _ => -1
                };
                if (categoryIndex >= 0) {
                    int count = ++category_items_received[categoryIndex];
                    int offset = MapTypeCounts.Take(categoryIndex).Sum();
                    uint nodeKey = (uint)(count + offset);

                    if (count <= MapTypeCounts[categoryIndex] && NodeToIdent.ContainsKey(nodeKey)) {
                        var ident = NodeToIdent[nodeKey];
                        SongUnlocks.Add(ident);
                        Plugin.Log.Info("Unlocked: " + ident);
                    }
                }
            }

            int total_unlocks_received = category_items_received.Sum();
            Plugin.Log.Info($"Connection established, {StartingNodesList.Count + total_unlocks_received} songs in inventory.");
        }
        session.Items.ItemReceived += RecvItem;

        return true;
    }

    private static void RecvDeathLink(DeathLink deathLink) {
        Plugin.Log.Info(deathLink.Cause);
        DeathlinkClass.ForceFail();
    }

    public static void SendDeathLink(string cause) {
        if (dlservice == null) return;
        DeathLink dl = new(session.Players.ActivePlayer.Alias, cause);
        dlservice.SendDeathLink(dl);
    }

    public static async void CheckLocation(BeatmapKey key, string rank) {
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

        var matchingEntry = IdentToNode.FirstOrDefault(kvp =>
            kvp.Key.StartsWith(levelIdHex + "_" + characteristic + "_" + difficulty, StringComparison.OrdinalIgnoreCase)
        );

        if (matchingEntry.Key == null) {
            Plugin.Log.Warn($"No AP location found for map {levelIdHex}_{characteristic}");
            return;
        }

        List<string> Grades = new List<string>{ "C", "B", "A", "S", "SS" };
        int gradeIndex = Grades.IndexOf(rank);

        long[] locationsToCheck = new long[6];

        if (GameMode == GameMode.PresetPass) {
            session.Locations.CompleteLocationChecks(matchingEntry.Value);
        } else if (GameMode == GameMode.PresetAcc) {
            for (int i = 0; i <= gradeIndex; i++) locationsToCheck[i] = (matchingEntry.Value * 6 + i + 1);
            session.Locations.CompleteLocationChecks(locationsToCheck);
        } else {
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
            session.Locations.CompleteLocationChecks(locationsToCheck);

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
            song_items_received++;

            if (song_items_received <= NodeToIdent.Count) {
                int nodeToUnlock = song_items_received; // Progressive: item 1 unlocks node 1, etc.
                
                if (NodeToIdent.ContainsKey((uint)nodeToUnlock)) {
                    SongUnlocks.Add(NodeToIdent[(uint)nodeToUnlock]);
                }
                Plugin.Log.Info("Currently received songs: ");
                for(int i=0; i < SongUnlocks.Count; i++) {
                    Plugin.Log.Info(SongUnlocks[i]);
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
            int count = ++category_items_received[categoryIndex];
            int offset = MapTypeCounts.Take(categoryIndex).Sum();

            if (count <= MapTypeCounts[categoryIndex] && NodeToIdent.ContainsKey((uint)count)) {
                SongUnlocks.Add(NodeToIdent[(uint)count + (uint)offset]);
                Plugin.Log.Info(SongUnlocks[count + offset]);
            }
        }

        if (item.ItemName == "Victory") {
            ArchipelagoClientState victoryState = ArchipelagoClientState.ClientGoal;
            session.SetClientState(victoryState);
        }
        Plugin.Log.Info("Received item " + item.ItemId + " (" + song_items_received + " total song items)");
        TriggerSongListRefresh();
    }

    static void TriggerSongListRefresh() {
        MainThreadDispatcher.Enqueue(() => {
            var table = Resources
                .FindObjectsOfTypeAll<LevelCollectionTableView>()
                .FirstOrDefault();

            if (table == null)
                return;

            var tv = tableViewField.GetValue(table) as TableView;
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

    public enum CampaignValidity {
        NotAP,
        WrongCampaign,
        Correct
    }
    public static bool TryGetIdent(string levelId, out string ident) {
        lock (_identCache) {
            return _identCache.TryGetValue(levelId, out ident);
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
                    lock (_identCache) {
                        _identCache[level.levelID] = ident;
                    }
                } catch (Exception ex) {
                    Plugin.Log.Warn($"Ident build failed for {level.levelID}: {ex}");
                }
            }

            _identReady = true;
        });
        Plugin.Log.Info("Identity Build Complete.");
    }

    public static bool AreIdentsReady => _identReady;

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
