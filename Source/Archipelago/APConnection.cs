using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BeatSaberAP;
using HMUI;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using IPA.Utilities;

public static class APConnection {

    public static ArchipelagoSession session = null;
    private static DeathLinkService dlservice = null;
    private static Dictionary<uint,string> NodeToIdent = null;
    private static Dictionary<string,uint> IdentToNode = null;
    public static readonly List<string> song_unlocks = [];
    public static string CampaignName { get; private set; }
    public static int song_items_received = 0;

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
        Dictionary<string,string> conninfo = new();
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
        CampaignName = (string)success.SlotData["campaign_name"];
        NodeToIdent = JsonConvert.DeserializeObject<Dictionary<uint,string>>(success.SlotData["node_to_keystr"].ToString());
        IdentToNode = JsonConvert.DeserializeObject<Dictionary<string,uint>>(success.SlotData["keystr_to_node"].ToString());
        song_unlocks.AddRange(JsonConvert.DeserializeObject<List<string>>(success.SlotData["start_songs"].ToString()));

        //Sync Item Counts
        song_items_received = 0;
        //First song is always unlocked

        Plugin.Log.Info("Songs already in inventory: ");
        Plugin.Log.Info(song_unlocks[0]);
        foreach (ItemInfo i in session.Items.AllItemsReceived) {
            if (i.ItemName == "Progressive Song Unlock") {
                song_items_received++;
                if (song_items_received <= NodeToIdent.Count) {
                    int nodeToUnlock = song_items_received; // Progressive: item 1 unlocks node 1, etc.

                    if (NodeToIdent.ContainsKey((uint)nodeToUnlock)) {
                        song_unlocks.Add(NodeToIdent[(uint)nodeToUnlock]);
                    }
                    Plugin.Log.Info(song_unlocks[nodeToUnlock]);
                }
            }
        }
        Plugin.Log.Info($"Connection established, {song_items_received+1} songs in inventory.");

        session.Items.ItemReceived += RecvItem;

        return true;
    }

    private static void RecvDeathLink(DeathLink deathLink) {
        #warning TODO receive deathlink
    }

    public static void SendDeathLink(string cause) {
        if (dlservice == null) return;
        DeathLink dl = new(session.Players.ActivePlayer.Alias, cause);
        dlservice.SendDeathLink(dl);
    }

    public static async void CheckLocation(BeatmapKey key) {
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

        // Find ANY keystr that matches this levelid + characteristic (ignoring difficulty)
        var matchingEntry = IdentToNode.FirstOrDefault(kvp =>
            kvp.Key.StartsWith(levelIdHex + "_" + characteristic + "_", StringComparison.OrdinalIgnoreCase)
        );

        if (matchingEntry.Key == null) {
            Plugin.Log.Warn($"No AP location found for map {levelIdHex}_{characteristic}");
            return;
        }

        Plugin.Log.Info("Checked location " + matchingEntry.Value + " with ident " + matchingEntry.Key);
        session.Locations.CompleteLocationChecks(matchingEntry.Value);
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
                    song_unlocks.Add(NodeToIdent[(uint)nodeToUnlock]);
                }
                Plugin.Log.Info("Currently received songs: ");
                for(int i=0; i < song_unlocks.Count; i++) {
                    Plugin.Log.Info(song_unlocks[i]);
                }

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
        return song_unlocks.Contains(ident);
    }
    public static bool IsSongUnlocked(string ident) {
        Plugin.Log.Debug("Checking unlock for: " + ident);
        return song_unlocks.Contains(ident);
    }

    public enum CampaignValidity {
        NotAP,
        WrongCampaign,
        Correct
    }
    /*
    public static CampaignValidity CheckCampaignValid(string name) {
        string selected = CustomCampaignFlowCoordinator.CustomCampaignManager.Campaign.info.name;
        if (!selected.StartsWith("AP Campaign, ")) return CampaignValidity.NotAP; // None of our business
        if (selected != APConnection.CampaignName) {
            // User selected wrong campaign, so mismatch, or CampaignName not initialized, so not connected
            return CampaignValidity.WrongCampaign;
        }
        // Campaign is correct
        return CampaignValidity.Correct;
    }
    */
    public static bool TryGetIdent(string levelId, out string ident) {
        lock (_identCache) {
            return _identCache.TryGetValue(levelId, out ident);
        }
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
                    Plugin.Log.Info($"Caching ident for {level.songName}: {ident}");

                    if (ident.StartsWith("OST_") == false && song_unlocks.Any(s => s.StartsWith(ident.Split("_")[0] + "_", StringComparison.OrdinalIgnoreCase))) {
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
        Plugin.Log.Info($"Generated ident: {fullIdent}");
        return fullIdent;
    }
}
