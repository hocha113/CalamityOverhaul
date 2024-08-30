global using Microsoft.Xna.Framework;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Newtonsoft.Json.Linq;
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
        #region Date
        internal static CWRMod Instance;
        internal static int GameLoadCount;
        internal static bool Suitableversion_improveGame { get; private set; }
        internal static List<Mod> LoadMods { get; private set; }
        internal static List<ILoader> ILoaders { get; private set; }
        internal static List<BaseRItem> RItemInstances { get; private set; } = [];
        internal static List<EctypeItem> EctypeItemInstance { get; private set; } = [];
        internal static List<NPCCustomizer> NPCCustomizerInstances { get; private set; } = [];
        internal static Dictionary<int, BaseRItem> RItemIndsDict { get; private set; } = [];
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_Shoot_Hook { get; private set; }
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_CanUse_Hook { get; private set; }
        internal static GlobalHookList<GlobalItem> CWR_InItemLoader_Set_UseItem_Hook { get; private set; }
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

        internal enum CallType
        {
            SupertableRecipeDate,
        }
        #endregion

        public override object Call(params object[] args) {
            int contentCount = args.Length;
            if (contentCount <= 0) {
                Instance.Logger.Info("Call was made with no parameters.");
                return null;
            }

            CallType callType = default;
            //如果第一个类型选择参数都不对，那么直接返回
            if (Enum.IsDefined(typeof(CallType), args[0])) {
                callType = (CallType)args[0];
            }
            else {
                Instance.Logger.Info("Call was made without the correct CallType.");
                return null;
            }

            //如果要使用这个call，那么它最好在Load环节就调用一次，这样才能保证欧米茄能正常获取到值
            if (callType == CallType.SupertableRecipeDate) {
                if (contentCount < 2) {
                    Instance.Logger.Info("Call-SupertableRecipeDate was made without additional parameters.");
                    return null;
                }

                if (args[1] is string[] addPms) {
                    SupertableUI.ModCall_OtherRpsData_StringList.Add(addPms);
                }
                else {
                    Instance.Logger.Info("Call-SupertableRecipeDate was made with incorrect parameter types.");
                    return null;
                }
            }

            return null;
        }

        public override void PostSetupContent() {
            LoadMods = ModLoader.Mods.ToList();

            {
                RItemInstances = [];//这里直接进行初始化，便不再需要进行UnLoad卸载
                List<Type> rItemIndsTypes = CWRUtils.GetSubclasses(typeof(BaseRItem));
                //($"一共获取到{rItemIndsTypes.Count}个待挑选元素Type").DompInConsole();
                foreach (Type type in rItemIndsTypes) {
                    //($"指向元素{type}进行分析").DompInConsole();
                    if (type != typeof(BaseRItem)) {
                        object obj = Activator.CreateInstance(type);
                        if (obj is BaseRItem inds) {
                            //($"元素{type}成功转换为object并进行分析").DompInConsole();
                            if (inds.CanLoad()) {
                                //($"正在初始化元素{type}").DompInConsole();
                                inds.SetReadonlyTargetID = inds.TargetID;//这里默认加载一次，在多数情况使其下不用重写Load()方法
                                inds.Load();
                                inds.SetStaticDefaults();
                                if (inds.TargetID != 0) {
                                    //($"成功加入元素{type}").DompInConsole();
                                    //("______________________________").DompInConsole();
                                    RItemInstances.Add(inds);
                                }//最后再判断一下TargetID是否为0，因为如果这是一个有效的Ritem实例，那么它的TargetID就不可能为0，否则将其添加进去会导致LoadRecipe部分报错
                                else {
                                    //($"元素{type}的TargetID返回0，载入失败").DompInConsole();
                                }
                            }
                            else {
                                //($"元素{type}CanLoad返回false").DompInConsole();
                            }
                        }
                        else {
                            //($"元素{type}转换BaseRItem失败").DompInConsole();
                        }
                    }
                    else {
                        //($"元素{type}是{typeof(BaseRItem)}").DompInConsole();
                    }
                }
                //($"{RItemInstances.Count}个元素已经装载进RItemInstances").DompInConsole();
            }

            {
                EctypeItemInstance = [];
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
                NPCCustomizerInstances = [];//这里直接进行初始化，便不再需要进行UnLoad卸载
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
                RItemIndsDict = [];
                foreach (BaseRItem ritem in RItemInstances) {
                    RItemIndsDict.Add(ritem.SetReadonlyTargetID, ritem);
                }
                ($"{RItemIndsDict.Count} key pair is loaded into the RItemIndsDict").DompInConsole();
            }

            {
                GlobalHookList<GlobalItem> getItemLoaderHookTargetValue(string key)
                    => (GlobalHookList<GlobalItem>)typeof(ItemLoader).GetField(key, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                CWR_InItemLoader_Set_Shoot_Hook = getItemLoaderHookTargetValue("HookShoot");
                CWR_InItemLoader_Set_CanUse_Hook = getItemLoaderHookTargetValue("HookCanUseItem");
                CWR_InItemLoader_Set_UseItem_Hook = getItemLoaderHookTargetValue("HookUseItem");
            }

            {
                Suitableversion_improveGame = false;
                if (improveGame != null) {
                    Suitableversion_improveGame = improveGame.Version >= new Version(1, 7, 1, 7);
                }
            }

            //加载一次ID列表，从这里加载可以保障所有内容已经添加好了
            CWRLoad.Load();
            foreach (var i in ILoaders) {
                i.SetupData();
                if (!Main.dedServ) {
                    i.LoadAsset();
                }
            }
        }

        public override void Load() {
            Instance = this;
            FindMod();
            ILoaders = CWRUtils.GetSubInterface<ILoader>();
            foreach (var setup in ILoaders) {
                setup.LoadData();
                setup.DompLoadText();
            }
            LoadClient();
            GameLoadCount++;
        }

        public override void Unload() {
            UnLoadClient();
            foreach (var setup in ILoaders) {
                setup.UnLoadData();
                setup.DompUnLoadText();
            }
            emptyMod();
            LoadMods = null;
            ILoaders = null;
            RItemInstances = null;
            EctypeItemInstance = null;
            NPCCustomizerInstances = null;
            RItemIndsDict = null;
            CWR_InItemLoader_Set_Shoot_Hook = null;
            CWR_InItemLoader_Set_CanUse_Hook = null;
            CWR_InItemLoader_Set_UseItem_Hook = null;
            Instance = null;
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) => CWRNetCode.HandlePacket(this, reader, whoAmI);

        private void emptyMod() {
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
        }

        public void FindMod() {
            emptyMod();
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
        }

        public void LoadClient() {
            if (Main.dedServ)
                return;

            EffectLoader.LoadEffects();
            ILMainMenuModification.Load();
            Filters.Scene["CWRMod:TungstenSky"] = new Filter(new TungstenSkyDate("FilterMiniTower").UseColor(0.5f, 0f, 0.5f).UseOpacity(0.2f), EffectPriority.VeryHigh);
            SkyManager.Instance["CWRMod:TungstenSky"] = new TungstenSky();
        }

        public void UnLoadClient() {
            if (Main.dedServ)
                return;

            EffectLoader.UnLoad();
            ILMainMenuModification.Unload();
        }
    }
}