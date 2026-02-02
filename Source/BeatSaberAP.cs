using HarmonyLib;
using IPA;
using IPA.Logging;
using SiraUtil.Zenject;
using SongDetailsCache;
using SongDetailsCache.Structs;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace BeatSaberAP
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        private Harmony harmony = null!;
        private static Task<SongDetails> sdtask;
        public static IPA.Logging.Logger Log { get; private set; }

        // setup that does not require game code
        // this is only called once ever, so do once-ever initialization
        [Init]
        public Plugin(IPA.Logging.Logger logger, Zenjector zenjector)
        {
            Log = logger;
            Log.Debug("BeatSaberAP plugin running!");

            zenjector.Install<InjectInstaller>(Location.Menu);
        }

        [OnStart]
        public void OnStart()
        {
            // setup that requires game code
            // Load patches from any class annotated with @HarmonyPatch
            harmony = Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);

            sdtask = SongDetails.Init();
            new GameObject("BeatSaberAP_MainThreadDispatcher")
                .AddComponent<MainThreadDispatcher>();
            ArchipelagoLevelIndex.Initialize();
        }

        public static async Task<uint> GetMapIDFromHashAsync(string hash) {
            Plugin.Log.Debug($"GetMapIDFromHashAsync: input hash='{hash}'");
            Plugin.Log.Debug("Hash length: " + hash.Length);
            var sd = await sdtask;
            bool found = sd.songs.FindByHash(hash, out Song s);
            uint mapId = found ? s.mapId : 0;
            Plugin.Log.Debug($"GetMapIDFromHashAsync: hash='{hash}', found={found}, mapId=0x{mapId:X} ({mapId})");
            return mapId;
        }

        [OnExit]
        public void OnExit()
        {
            // teardown
            harmony.UnpatchSelf();
        }
    }
}
