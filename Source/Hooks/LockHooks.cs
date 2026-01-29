using System;
using BeatSaberMarkupLanguage;
using CustomCampaigns.Campaign.Missions;
using CustomCampaigns.Managers;
using HarmonyLib;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[HarmonyPatch]
public class LockHooks : IInitializable, IDisposable {
    #pragma warning disable 0649
    [Inject] private readonly MissionLevelDetailViewController _viewCtr;
    #pragma warning restore 0649
    private static Button _playButton = null;

    public void Initialize() {
        _playButton = _viewCtr.GetField<Button, MissionLevelDetailViewController>("_playButton");
    }
    public void Dispose() {
        // Probably not needed
        _playButton = null;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CustomCampaignManager), "OnDidSelectMissionNode")]
    private static void HookMissionSelect(CustomCampaignManager __instance, MissionNodeVisualController missionNodeVisualController) {
        APConnection.CampaignValidity validity = APConnection.CheckCampaignValid(__instance.Campaign.info.name);
        if (validity == APConnection.CampaignValidity.NotAP) return;
        if (validity == APConnection.CampaignValidity.WrongCampaign) {
            DenyTrackWrongCampaign();
            return;
        }
        Mission m = (missionNodeVisualController.missionNode.missionData as CustomMissionDataSO)?.mission;
        BeatmapLevel level = m.FindSong();
        BeatmapKey key = new(level.levelID[CustomLevelLoader.kCustomLevelPrefixId.Length..], m.TryGetMissionData().beatmapCharacteristic, m.TryGetMissionData().beatmapDifficulty);
        if (level == null) return; // Download button is being shown
        DenyTrackIfNotUnlockedAsync(key);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CustomCampaignManager), "OnSongsLoaded")]
    private static void HookSongsLoaded(CustomCampaignManager __instance) {
        if (APConnection.CheckCampaignValid(__instance.Campaign.info.name) == APConnection.CampaignValidity.NotAP) return;
        _playButton.interactable = false;
        _playButton.SetButtonText("Reopen campaign");
    }

    private static void DenyTrackWrongCampaign() {
        _playButton.interactable = false;
        _playButton.SetButtonText("AP: Wrong campaign or not connected");
    }

    private static async void DenyTrackIfNotUnlockedAsync(BeatmapKey key) {
        if (await APConnection.HaveSong(key)) {
            _playButton.interactable = true;
            _playButton.SetButtonText("PLAY");
        } else {
            _playButton.interactable = false;
            _playButton.SetButtonText("AP: Missing Song Item");
        }
    }
}
