global using InnoVault;
global using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public class CWRMod : Mod
    {
        #region Data
        //-HoCha113 - 2024/9/19/ 3:45
        //不要使用惰性加载，这是愚蠢的，要知道有的Mod会在外部调用这个，
        //或者有的钩子是往InnoVault上挂载的，那个时候这个单例很可能还没来得及加载，然后把一切都毁掉
        //-Chram - 2024/9/20/13:45
        //不，只要注意就行，这个字段被调用的频率极高，使用惰性加载是个不错的习惯，我们只需要自己注意
        //并提醒别人不要在错误的线程上调用这个单例就行了
        //-HoCha113 - 2024/9/20/ 14:32
        //神皇在上，这是异端发言，你不能把整个系统的安危寄托在所有人可以遵守开发守则上，况且我们根本没有那个东西
        internal static CWRMod Instance { get; private set; }
        internal static List<ICWRLoader> ILoaders { get; private set; } = [];
        internal Mod calamity = null;
        internal Mod musicMod = null;
        internal Mod betterWaveSkipper = null;
        internal Mod fargowiltasSouls = null;
        internal Mod catalystMod = null;
        internal Mod weaponOut = null;
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

        #endregion

        public override object Call(params object[] args) => ModCall.Hander(args);

        public override void PostSetupContent() {
            //加载一次ID列表，从这里加载可以保障所有内容已经添加好了
            CWRLoad.Setup();
            foreach (var load in ILoaders) {
                load.SetupData();
                if (!Main.dedServ) {
                    load.LoadAsset();
                }
            }
        }

        public override void Load() {
            Instance = this;
            FindMod();

            ILoaders = VaultUtils.GetDerivedInstances<ICWRLoader>();
            foreach (var load in ILoaders) {
                load.LoadData();
            }
        }

        public override void Unload() {
            foreach (var load in ILoaders) {
                load.UnLoadData();
            }

            EmptyMod();
            ILoaders?.Clear();
            CWRLoad.UnLoad();
            Instance = null;
        }

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
        }

        public void FindMod() {
            EmptyMod();
            ModLoader.TryGetMod("CalamityMod", out calamity);
            ModLoader.TryGetMod("CalamityModMusic", out musicMod);
            ModLoader.TryGetMod("BetterWaveSkipper", out betterWaveSkipper);
            ModLoader.TryGetMod("FargowiltasSouls", out fargowiltasSouls);
            ModLoader.TryGetMod("CatalystMod", out catalystMod);
            ModLoader.TryGetMod("WeaponOut", out weaponOut);
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
        }
    }
}