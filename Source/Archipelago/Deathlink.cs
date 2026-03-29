using IPA.Utilities;
using SiraUtil.Affinity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

static class DeathlinkClass {

    public static void ForceFail() {
        var gameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().FirstOrDefault();
        if (gameplayManager == null) return;

        gameplayManager._initData.SetField(nameof(gameplayManager._initData.continueGameplayWith0Energy), false);
        gameplayManager.HandleGameEnergyDidReach0();
    }


}
