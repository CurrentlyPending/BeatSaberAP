using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BeatSaberAP;
using CustomCampaigns.UI.FlowCoordinators;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class APConnection {

    public static ArchipelagoSession session = null;
    private static DeathLinkService dlservice = null;
    private static Dictionary<uint,string> NodeToIdent = null;
    private static Dictionary<string,uint> IdentToNode = null;
    public static readonly List<string> song_unlocks = [];
    public static string CampaignName { get; private set; }
    public static int song_items_received = 0;

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
        session.Items.ItemReceived += RecvItem;

        // Refresh all songs after connecting
        SongCore.Loader.Instance.RefreshSongs(false);
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

        uint levelid = await Plugin.GetMapIDFromHashAsync(key.levelId);
        string levelIdHex = levelid.ToString("X");
        string characteristic = key.beatmapCharacteristic.serializedName;

        // Find ANY keystr that matches this levelid + characteristic (ignoring difficulty)
        var matchingEntry = IdentToNode.FirstOrDefault(kvp =>
            kvp.Key.StartsWith(levelIdHex + "_" + characteristic + "_")
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
        if (item.ItemName == "Progressive Song Unlock") {
            song_items_received++;


            if (song_items_received <= NodeToIdent.Count) {
                int nodeToUnlock = song_items_received; // Progressive: item 1 unlocks node 1, etc.
                if (NodeToIdent.ContainsKey((uint)nodeToUnlock)) {
                    song_unlocks.Add(NodeToIdent[(uint)nodeToUnlock]);
                }
            }
        }
        Plugin.Log.Info("Received item " + item.ItemId + " (" + song_items_received + " total song items)");
    }

    public static async Task<bool> HaveSong(BeatmapKey key) {
        string ident = await GenerateIdentAsync(key);
        Plugin.Log.Debug("Queried info for:" + ident);
        return song_unlocks.Contains(ident);
    }
    public static bool IsSongUnlocked(string ident) {
        return song_unlocks.Contains(ident);
    }

    public enum CampaignValidity {
        NotAP,
        WrongCampaign,
        Correct
    }
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

    public static async Task<string> GenerateIdentAsync(BeatmapKey key) {
        // Normalize level id (remove common prefixes that appear in gameplay)
        string raw = key.levelId ?? string.Empty;
        if (raw.StartsWith("custom_level_", StringComparison.OrdinalIgnoreCase)) {
            raw = raw.Substring("custom_level_".Length);
        } else if (raw.StartsWith("level_", StringComparison.OrdinalIgnoreCase)) {
            raw = raw.Substring("level_".Length);
        }

        // Use normalized id for map lookup
        uint levelid = await Plugin.GetMapIDFromHashAsync(raw);
        Plugin.Log.Info(levelid.ToString("X") + "_" + key.beatmapCharacteristic.SerializedName() + "_" + ((int)key.difficulty));
        return levelid.ToString("X") + "_" + key.beatmapCharacteristic.SerializedName() + "_" + ((int)key.difficulty);
    }
}
