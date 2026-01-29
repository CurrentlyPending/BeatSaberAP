using BeatSaberAP;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using TMPro;
using UnityEngine;

[ViewDefinition("BeatSaberAP.connection-options.bsml")]
[HotReload(RelativePathToLayout = @"connection-options.bsml")]
public class ArchipelagoViewController : BSMLAutomaticViewController {
    string host = "";
    string port = "";
    string slot = "";
    string pass = "";
    #pragma warning disable 0649
    [UIComponent("hostOut")] readonly TextMeshProUGUI hostOut;
    [UIComponent("portOut")] readonly TextMeshProUGUI portOut;
    [UIComponent("slotOut")] readonly TextMeshProUGUI slotOut;
    [UIComponent("passOut")] readonly TextMeshProUGUI passOut;

    #pragma warning restore 0649
    [UIComponent("connectionStatus")] public TextMeshProUGUI connStatus;

    [UIAction("enteredHost")]
    void EnteredHostValue(string entered) {
        host = entered;
        hostOut.text = host;
    }
    [UIAction("enteredPort")]
    void EnteredPortValue(string entered) {
        port = entered;
        portOut.text = port;
    }
    [UIAction("enteredSlot")]
    void EnteredSlotValue(string entered) {
        slot = entered;
        slotOut.text = slot;
    }
    [UIAction("enteredPass")]
    void EnteredPassValue(string entered) {
        pass = entered;
        passOut.text = pass;
    }

    [UIAction("clickedConnect")]
    void ConnectButtonPress() {
        if (APConnection.ConnectAndGetSlotData(host, Convert.ToInt32(port), slot, pass))
            connStatus.text = "Connected Successfully";
        else
            connStatus.text = "Connection failed";
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        // Try to read last connection info
        try {
            Dictionary<string,string> conninfo = JsonConvert.DeserializeObject<Dictionary<string,string>>(System.IO.File.ReadAllText("AP_ConnInfo.json"));
            EnteredSlotValue(conninfo["slot"]);
            EnteredHostValue(conninfo["host"]);
            EnteredPortValue(conninfo["port"]);
            EnteredPassValue(conninfo["password"]);
        } catch (Exception e) {
            BeatSaberAP.Plugin.Log.Info("Error accessing last connection info. This is non-fatal. Error details below:");
            BeatSaberAP.Plugin.Log.Info(e.ToString());
        }
    }
}
