using BS_Utils.Gameplay;
using IPA.Utilities;
using SiraUtil.Affinity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BeatSaberAP.Archipelago {
    internal static class GoalMap {
        private static int BloqKeys = 0;

        public static void UpdateGoalMap() {
            BloqKeys++;

            if (APConnection.LockGoalMap) {
                if (BloqKeys < APConnection.BloqKeysRequired) {
                    Plugin.Log.Info($"Currently at {BloqKeys}/{APConnection.BloqKeysRequired} bloq keys.");
                } else {
                    //such a stupid line but i have no idea how to do this better so whatever
                    APConnection.SongUnlocks.Add(APConnection.NodeToIdent[(uint)APConnection.GoalNodeId]);
                }
            }
        }
    }
}
