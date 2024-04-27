using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul
{
    public class CWRMod : Mod
    {
        internal static CWRMod Instance;
        internal static int GameLoadCount;
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_Shoot_Hook;
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_CanUse_Hook;
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_UseItem_Hook;

        internal Mod musicMod = null;
        internal Mod betterWaveSkipper = null;
        internal Mod fargowiltasSouls = null;
        internal Mod catalystMod = null;
        internal Mod weaponOut = null;
        internal Mod weaponDisplay = null;
        internal Mod magicBuilder = null;
        internal Mod improveGame = null;
        internal Mod luiafk = null;
        internal Mod terrariaOverhaul = null;
        internal Mod thoriumMod = null;

        internal List<Mod> LoadMods = new List<Mod>();
        internal static List<BaseRItem> RItemInstances = new List<BaseRItem>();
        internal static List<EctypeItem> EctypeItemInstance = new List<EctypeItem>();
        internal static List<NPCCustomizer> NPCCustomizerInstances = new List<NPCCustomizer>();
        internal static Dictionary<int, BaseRItem> RItemIndsDict = new Dictionary<int, BaseRItem>();

        public override void PostSetupContent() {
            LoadMods = ModLoader.Mods.ToList();

            {
                RItemInstances = new List<BaseRItem>();//这里直接进行初始化，便不再需要进行UnLoad卸载
                List<Type> rItemIndsTypes = CWRUtils.GetSubclasses(typeof(BaseRItem));
                foreach (Type type in rItemIndsTypes) {
                    if (type != typeof(BaseRItem)) {
                        object obj = Activator.CreateInstance(type);
                        if (obj is BaseRItem inds) {
                            inds.Load();
                            inds.SetStaticDefaults();
                            //最后再判断一下TargetID是否为0，因为如果这是一个有效的Ritem实例，那么它的TargetID就不可能为0，否则将其添加进去会导致LoadRecipe部分报错
                            if (inds.TargetID != 0)
                                RItemInstances.Add(inds);
                        }
                    }
                }
            }

            {
                EctypeItemInstance = new List<EctypeItem>();
                List<Type> ectypeIndsTypes = CWRUtils.GetSubclasses(typeof(BaseRItem));
                foreach (Type type in ectypeIndsTypes) {
                    if (type != typeof(EctypeItem)) {
                        object obj = Activator.CreateInstance(type);
                        if (obj is EctypeItem inds) {
                            EctypeItemInstance.Add(inds);
                        }
                    }
                }
            }

            {
                NPCCustomizerInstances = new List<NPCCustomizer>();//这里直接进行初始化，便不再需要进行UnLoad卸载
                List<Type> npcCustomizerIndsTypes = CWRUtils.GetSubclasses(typeof(NPCCustomizer));
                foreach (Type type in npcCustomizerIndsTypes) {
                    if (type != typeof(NPCCustomizer)) {
                        object obj = Activator.CreateInstance(type);
                        if (obj is NPCCustomizer inds) {
                            NPCCustomizerInstances.Add(inds);
                        }
                    }
                }
            }

            {
                RItemIndsDict = new Dictionary<int, BaseRItem>();
                foreach (BaseRItem ritem in RItemInstances) {
                    RItemIndsDict.Add(ritem.SetReadonlyTargetID, ritem);
                }
            }

            {
                GlobalHookList<GlobalItem> getItemLoaderHookTargetValue(string key) 
                    => (GlobalHookList<GlobalItem>)typeof(ItemLoader).GetField(key, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                CWR_InItemLoader_Set_Shoot_Hook = getItemLoaderHookTargetValue("HookShoot");
                CWR_InItemLoader_Set_CanUse_Hook = getItemLoaderHookTargetValue("HookCanUseItem");
                CWR_InItemLoader_Set_UseItem_Hook = getItemLoaderHookTargetValue("HookUseItem");
            }

            //加载一次ID列表，从这里加载可以保障所有内容已经添加好了
            CWRIDs.Load();
        }

        public override void Load() {
            Instance = this;
            
            FindMod();
            LoadClient();
            ModGanged.Load();
            CWRParticleHandler.Load();
            new InWorldBossPhase().Load();
            EffectsRegistry.LoadEffects();
            CWRKeySystem.LoadKeyDate(this);
            On_Main.DrawInfernoRings += PeSystem.CWRDrawForegroundParticles;
            GameLoadCount++;
            base.Load();
        }

        public override void Unload() {
            CWRParticleHandler.Unload();
            CWRKeySystem.Unload();
            On_Main.DrawInfernoRings -= PeSystem.CWRDrawForegroundParticles;
            ILMainMenuModification.Unload();
            base.Unload();
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) => CWRNetCode.HandlePacket(this, reader, whoAmI);

        public void FindMod() {
            ModLoader.TryGetMod("CalamityModMusic", out musicMod);
            ModLoader.TryGetMod("BetterWaveSkipper", out betterWaveSkipper);
            ModLoader.TryGetMod("FargowiltasSouls", out fargowiltasSouls);
            ModLoader.TryGetMod("CatalystMod", out catalystMod);
            ModLoader.TryGetMod("WeaponOut", out weaponOut);
            ModLoader.TryGetMod("WeaponDisplay", out weaponDisplay);
            ModLoader.TryGetMod("MagicBuilder", out magicBuilder);
            ModLoader.TryGetMod("ImproveGame", out improveGame);
            ModLoader.TryGetMod("miningcracks_take_on_luiafk", out luiafk);
            ModLoader.TryGetMod("TerrariaOverhaul", out terrariaOverhaul);
            ModLoader.TryGetMod("ThoriumMod", out thoriumMod);
        }

        public void LoadClient() {
            if (Main.dedServ)
                return;

            ILMainMenuModification.Load();
            Filters.Scene["CWRMod:TungstenSky"] = new Filter(new TungstenSkyDate("FilterMiniTower").UseColor(0.5f, 0f, 0.5f).UseOpacity(0.2f), EffectPriority.VeryHigh);
            SkyManager.Instance["CWRMod:TungstenSky"] = new TungstenSky();
        }
    }
}