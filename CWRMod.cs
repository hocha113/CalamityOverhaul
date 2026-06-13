global using InnoVault;
global using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public class CWRMod : Mod
    {
        #region Data
        // 饿汉式单例：跨 Mod/外部可能在 Load 前访问 Instance，惰性会 NRE
        private static CWRMod instance;
        internal static CWRMod Instance {
            get {
                instance ??= (CWRMod)ModLoader.GetMod("CalamityOverhaul");
                return instance;
            }
        }
        internal static List<ICWRLoader> ILoaders { get; private set; } = [];
        internal Mod calamity = null;
        internal Mod musicMod = null;
        internal Mod betterWaveSkipper = null;
        internal Mod fargowiltasSouls = null;
        internal Mod catalystMod = null;
        internal Mod weaponOut = null;
        internal Mod weaponOutLite = null;
        internal Mod weaponDisplay = null;
        internal Mod weaponDisplayLite = null;
        internal Mod magicBuilder = null;
        internal Mod magicStorage = null;
        internal Mod improveGame = null;
        internal Mod luiafk = null;
        internal Mod terrariaOverhaul = null;
        internal Mod thoriumMod = null;
        internal Mod narakuEye = null;
        internal Mod coolerItemVisualEffect = null;
        internal Mod gravityDontFlipScreen = null;
        internal Mod infernum = null;
        internal Mod ddmod = null;
        internal Mod coralite = null;
        internal Mod bossChecklist = null;
        internal Mod calamityEntropy = null;
        internal Mod fargowiltasCrossmod = null;
        internal Mod luminance = null;
        internal Mod woTM = null;
        internal Mod noxusBoss = null;
        internal Mod subworldLibrary = null;
        internal Mod fargowiltas = null;

        #endregion

        /// <summary>跨 Mod ModCall 入口</summary>
        public override object Call(params object[] args) => ModCall.Hander(args);

        public override void PostSetupContent() {
            // 内容注册完毕后再建 ID 查找表
            CWRLoad.Setup();
            CWRID.PreloadAll();
            foreach (var load in ILoaders) {
                try {
                    load.SetupData();
                    if (!Main.dedServ) {
                        load.LoadAsset();
                    }
                } catch {
                    Logger.Error("An error occurred while post-setup " + load.GetType().Name);
                }
            }
        }

        public override void Load() {
            FindMod();

            // 跨 Mod Hook 不依赖灾厄，灾厄专属在 LoadComders 内守卫
            ModGanged.Load();

            CWRRef.Load();
            ILoaders = VaultUtils.GetDerivedInstances<ICWRLoader>();
            foreach (var load in ILoaders) {
                try {
                    load.LoadData();
                } catch {
                    Logger.Error("An error occurred while loading " + load.GetType().Name);
                }
            }
        }

        public override void Unload() {
            // 逆序卸载 ICWRLoader → 清空联动 Mod 引用 → 释放静态查找表
            foreach (var load in ILoaders) {
                try {
                    load.UnLoadData();
                } catch {
                    Logger.Error("An error occurred while unloading " + load.GetType().Name);
                }
            }

            EmptyMod();
            ILoaders?.Clear();
            CWRLoad.UnLoad();
            CWRID.UnLoad();
            CWRRef.UnLoad();
        }

        /// <summary>网络包分发至 CWRNetWork</summary>
        public override void HandlePacket(BinaryReader reader, int whoAmI) => CWRNetWork.HandlePacket(this, reader, whoAmI);

        private void EmptyMod() {
            calamity = null;
            musicMod = null;
            betterWaveSkipper = null;
            fargowiltasSouls = null;
            catalystMod = null;
            weaponOut = null;
            weaponDisplay = null;
            magicBuilder = null;
            improveGame = null;
            luiafk = null;
            terrariaOverhaul = null;
            thoriumMod = null;
            narakuEye = null;
            coolerItemVisualEffect = null;
            gravityDontFlipScreen = null;
            infernum = null;
            ddmod = null;
            coralite = null;
            bossChecklist = null;
            calamityEntropy = null;
            fargowiltasCrossmod = null;
            luminance = null;
            woTM = null;
            noxusBoss = null;
            subworldLibrary = null;
            fargowiltas = null;
        }

        /// <summary>缓存可选联动 Mod 引用，Load/Unload 前调用</summary>
        public void FindMod() {
            EmptyMod();
            ModLoader.TryGetMod("CalamityMod", out calamity);
            ModLoader.TryGetMod("CalamityModMusic", out musicMod);
            ModLoader.TryGetMod("BetterWaveSkipper", out betterWaveSkipper);
            ModLoader.TryGetMod("FargowiltasSouls", out fargowiltasSouls);
            ModLoader.TryGetMod("CatalystMod", out catalystMod);
            ModLoader.TryGetMod("WeaponOut", out weaponOut);
            ModLoader.TryGetMod("WeaponOutLite", out weaponOutLite);
            ModLoader.TryGetMod("WeaponDisplay", out weaponDisplay);
            ModLoader.TryGetMod("WeaponDisplayLite", out weaponDisplayLite);
            ModLoader.TryGetMod("MagicBuilder", out magicBuilder);
            ModLoader.TryGetMod("MagicStorage", out magicStorage);
            ModLoader.TryGetMod("ImproveGame", out improveGame);
            ModLoader.TryGetMod("miningcracks_take_on_luiafk", out luiafk);
            ModLoader.TryGetMod("TerrariaOverhaul", out terrariaOverhaul);
            ModLoader.TryGetMod("ThoriumMod", out thoriumMod);
            ModLoader.TryGetMod("NarakuEye", out narakuEye);
            ModLoader.TryGetMod("CoolerItemVisualEffect", out coolerItemVisualEffect);
            ModLoader.TryGetMod("GravityDontFlipScreen", out gravityDontFlipScreen);
            ModLoader.TryGetMod("InfernumMode", out infernum);
            ModLoader.TryGetMod("DDmod", out ddmod);
            ModLoader.TryGetMod("Coralite", out coralite);
            ModLoader.TryGetMod("BossChecklist", out bossChecklist);
            ModLoader.TryGetMod("CalamityEntropy", out calamityEntropy);
            ModLoader.TryGetMod("FargowiltasCrossmod", out fargowiltasCrossmod);
            ModLoader.TryGetMod("Luminance", out luminance);
            ModLoader.TryGetMod("WoTM", out woTM);
            ModLoader.TryGetMod("NoxusBoss", out noxusBoss);
            ModLoader.TryGetMod("SubworldLibrary", out subworldLibrary);
            ModLoader.TryGetMod("Fargowiltas", out fargowiltas);
        }
    }
}