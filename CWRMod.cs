using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public class CWRMod : Mod
    {
        internal static CWRMod Instance;
        internal static int GameLoadCount;
        internal Mod musicMod = null;
        internal Mod betterWaveSkipper = null;
        internal Mod fargowiltasSouls = null;
        internal Mod catalystMod = null;
        internal Mod weaponOut = null;

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

            //加载一次ID列表，从这里加载可以保障所有内容已经添加好了
            CWRIDs.Load();
        }

        public override void Load() {
            Instance = this;
            GameLoadCount++;
            new InWorldBossPhase().Load();

            FindMod();
            ModGanged.Load();

            LoadClient();

            //加载头部资源Icon
            //HEHead.LoadHaedIcon();

            CWRParticleHandler.Load();
            EffectsRegistry.LoadEffects();
            On_Main.DrawInfernoRings += PeSystem.CWRDrawForegroundParticles;

            base.Load();
        }

        public override void Unload() {
            CWRParticleHandler.Unload();
            On_Main.DrawInfernoRings -= PeSystem.CWRDrawForegroundParticles;
            base.Unload();
        }

        public void FindMod() {
            ModLoader.TryGetMod("CalamityModMusic", out musicMod);
            ModLoader.TryGetMod("BetterWaveSkipper", out betterWaveSkipper);
            ModLoader.TryGetMod("FargowiltasSouls", out fargowiltasSouls);
            ModLoader.TryGetMod("CatalystMod", out catalystMod);
            ModLoader.TryGetMod("WeaponOut", out weaponOut);
        }

        public void LoadClient() {
            if (Main.dedServ)
                return;

            MusicLoader.AddMusicBox(Instance, MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/BuryTheLight")
                , Find<ModItem>("FoodStallChair").Type, Find<ModTile>("FoodStallChair").Type, 0);
        }
    }
}