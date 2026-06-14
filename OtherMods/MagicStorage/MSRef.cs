using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    /// <summary>
    /// MagicStorage的弱引用访问层，不持有任何MagicStorage的编译期类型。
    /// 所有成员在加载期反射定位一次，并编译为表达式树委托，调用开销接近直接调用且无装箱，
    /// 模式与<see cref="CWRRef"/>一致
    /// </summary>
    internal static class MSRef
    {
        /// <summary>
        /// 兼容的最低MagicStorage版本
        /// </summary>
        internal static Version TargetVersion => new(0, 7, 0, 11);
        /// <summary>
        /// MagicStorage是否存在、版本兼容且核心存取委托可用，物流存取以此为准
        /// </summary>
        internal static bool Has { get; private set; }
        /// <summary>
        /// StoragePlayer 与制作界面联动是否就绪，为假时仅背包速取与终焉工作台联动降级，核心存取仍由 <see cref="Has"/> 保证
        /// </summary>
        internal static bool LinkageReady { get; private set; }

        #region 编译委托缓存
        //类型缓存，用于调用前的实例类型守卫，防止编译委托内部的强制转换抛出异常
        private static Type storageComponentType;
        private static Type storageHeartType;
        private static Type craftingUIStateType;
        //TEStorageComponent.GetHeart()是虚方法，经基类编译的委托同样走虚分派，
        //可覆盖StorageHeart、RemoteAccess、StorageAccess、CraftingAccess等所有组件
        private static Func<object, object> getHeartFunc;
        //TEStorageHeart成员
        private static Func<object, IEnumerable> getStorageUnitsFunc;
        private static Func<object, IEnumerable<Item>> getStoredItemsFunc;
        private static Action<object, Item> depositItemFunc;
        private static Func<object, Item, bool, Item> withdrawFunc;
        //TEAbstractStorageUnit成员，处于逐单元遍历的热路径上
        private static Func<object, bool> unitInactiveFunc;
        private static Func<object, bool> unitIsFullFunc;
        private static Func<object, Item, bool> unitHasSpaceInStackForFunc;
        //StoragePlayer模板与成员，实例通过player.GetModPlayer(template)获取
        private static ModPlayer storagePlayerTemplate;
        private static Func<ModPlayer, object> getStorageHeartFunc;
        private static Func<ModPlayer, object> getCraftingAccessFunc;
        //TECraftingAccess.stations
        private static Func<object, List<Item>> craftingStationsFunc;
        //SecuritySystem与MagicUI的静态方法
        private static Func<Player, int, bool> canPlayerAccessFunc;
        private static Func<bool> isCraftingUIOpenFunc;
        //制作界面UI成员，缺失时仅联动定位/配方同步降级，不影响存储功能
        private static Func<object> craftingUIFunc;
        private static Func<Recipe> selectedRecipeFunc;
        private static Func<object, object> recipePanelFunc;
        #endregion

        #region 加载与卸载
        internal static void Load() {
            Has = false;
            linkageBroken = false;
            linkageFailureCount = 0;

            Mod mod = CWRMod.Instance.magicStorage;
            if (mod == null || mod.Version < TargetVersion) {
                return;
            }

            const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags PubStatic = BindingFlags.Public | BindingFlags.Static;
            const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            storageComponentType = GetModType(mod, "MagicStorage.Components.TEStorageComponent");
            storageHeartType = GetModType(mod, "MagicStorage.Components.TEStorageHeart");
            craftingUIStateType = GetModType(mod, "MagicStorage.UI.States.CraftingUIState");
            Type unitType = GetModType(mod, "MagicStorage.Components.TEAbstractStorageUnit");
            Type craftingAccessType = GetModType(mod, "MagicStorage.Components.TECraftingAccess");
            Type securityType = GetModType(mod, "MagicStorage.Common.Systems.SecuritySystem");
            Type magicUIType = GetModType(mod, "MagicStorage.Common.Systems.MagicUI");
            Type craftingGUIType = GetModType(mod, "MagicStorage.CraftingGUI");

            getHeartFunc = Compile<Func<object, object>>(
                GetMethod(storageComponentType, "GetHeart", Pub), "TEStorageComponent.GetHeart");
            getStorageUnitsFunc = Compile<Func<object, IEnumerable>>(
                GetMethod(storageHeartType, "GetStorageUnits", Pub), "TEStorageHeart.GetStorageUnits");
            getStoredItemsFunc = Compile<Func<object, IEnumerable<Item>>>(
                GetMethod(storageHeartType, "GetStoredItems", Pub), "TEStorageHeart.GetStoredItems");
            depositItemFunc = Compile<Action<object, Item>>(
                GetMethod(storageHeartType, "DepositItem", Pub), "TEStorageHeart.DepositItem");
            withdrawFunc = Compile<Func<object, Item, bool, Item>>(
                GetMethod(storageHeartType, "Withdraw", Pub), "TEStorageHeart.Withdraw");

            unitInactiveFunc = Compile<Func<object, bool>>(
                GetProperty(unitType, "Inactive", Pub), "TEAbstractStorageUnit.Inactive");
            unitIsFullFunc = Compile<Func<object, bool>>(
                GetProperty(unitType, "IsFull", Pub), "TEAbstractStorageUnit.IsFull");
            unitHasSpaceInStackForFunc = Compile<Func<object, Item, bool>>(
                GetMethod(unitType, "HasSpaceInStackFor", Pub), "TEAbstractStorageUnit.HasSpaceInStackFor");

            if (!ModContent.TryFind(mod.Name, "StoragePlayer", out storagePlayerTemplate)) {
                CWRUtils.LogFailedLoad("StoragePlayer", "MagicStorage.StoragePlayer");
            }
            Type storagePlayerType = storagePlayerTemplate?.GetType();
            getStorageHeartFunc = Compile<Func<ModPlayer, object>>(
                GetMethod(storagePlayerType, "GetStorageHeart", Pub), "StoragePlayer.GetStorageHeart");
            getCraftingAccessFunc = Compile<Func<ModPlayer, object>>(
                GetMethod(storagePlayerType, "GetCraftingAccess", Pub), "StoragePlayer.GetCraftingAccess");

            craftingStationsFunc = Compile<Func<object, List<Item>>>(
                GetField(craftingAccessType, "stations", Pub), "TECraftingAccess.stations");

            canPlayerAccessFunc = Compile<Func<Player, int, bool>>(
                GetMethod(securityType, "CanPlayerAccessImmediately", PubStatic), "SecuritySystem.CanPlayerAccessImmediately");
            isCraftingUIOpenFunc = Compile<Func<bool>>(
                GetMethod(magicUIType, "IsCraftingUIOpen", PubStatic), "MagicUI.IsCraftingUIOpen");

            craftingUIFunc = Compile<Func<object>>(
                GetField(magicUIType, "craftingUI", AnyStatic), "MagicUI.craftingUI");
            selectedRecipeFunc = Compile<Func<Recipe>>(
                GetField(craftingGUIType, "selectedRecipe", AnyStatic), "CraftingGUI.selectedRecipe");
            recipePanelFunc = Compile<Func<object, object>>(
                GetField(craftingUIStateType, "recipePanel", AnyInstance), "CraftingUIState.recipePanel");

            //核心存取能力，物流存取所需，与 StoragePlayer/制作界面联动解耦
            Has = storageComponentType != null
                && storageHeartType != null
                && getHeartFunc != null
                && getStorageUnitsFunc != null
                && getStoredItemsFunc != null
                && depositItemFunc != null
                && withdrawFunc != null
                && unitInactiveFunc != null
                && unitIsFullFunc != null
                && unitHasSpaceInStackForFunc != null
                && canPlayerAccessFunc != null;

            //联动能力，缺失时背包速取与终焉工作台联动各自降级，不影响核心存取
            LinkageReady = Has
                && storagePlayerTemplate != null
                && getStorageHeartFunc != null
                && getCraftingAccessFunc != null
                && craftingStationsFunc != null
                && isCraftingUIOpenFunc != null;
        }

        internal static void Unload() {
            Has = false;
            LinkageReady = false;
            storageComponentType = null;
            storageHeartType = null;
            craftingUIStateType = null;
            getHeartFunc = null;
            getStorageUnitsFunc = null;
            getStoredItemsFunc = null;
            depositItemFunc = null;
            withdrawFunc = null;
            unitInactiveFunc = null;
            unitIsFullFunc = null;
            unitHasSpaceInStackForFunc = null;
            storagePlayerTemplate = null;
            getStorageHeartFunc = null;
            getCraftingAccessFunc = null;
            craftingStationsFunc = null;
            canPlayerAccessFunc = null;
            isCraftingUIOpenFunc = null;
            craftingUIFunc = null;
            selectedRecipeFunc = null;
            recipePanelFunc = null;
            loggedFailures.Clear();
            oldSelectedItemType = ItemID.None;
            craftingUIWasOpen = false;
            hasSupertableStation = false;
            linkageBroken = false;
            linkageFailureCount = 0;
        }

        private static Type GetModType(Mod mod, string fullName) {
            Type type = mod.Code.GetType(fullName);
            if (type == null) {
                CWRUtils.LogFailedLoad(fullName, fullName);
            }
            return type;
        }

        private static MethodInfo GetMethod(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null) {
                CWRUtils.LogFailedLoad(name, $"{type.FullName}.{name}");
            }
            return method;
        }

        private static FieldInfo GetField(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            FieldInfo field = type.GetField(name, flags);
            if (field == null) {
                CWRUtils.LogFailedLoad(name, $"{type.FullName}.{name}");
            }
            return field;
        }

        private static PropertyInfo GetProperty(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            PropertyInfo property = type.GetProperty(name, flags);
            if (property == null) {
                CWRUtils.LogFailedLoad(name, $"{type.FullName}.{name}");
            }
            return property;
        }

        /// <summary>
        /// 把反射成员编译成指定签名的委托：
        /// 委托参数与成员签名间自动插入类型转换（首个参数视为实例，静态成员除外），
        /// 调用开销接近直接调用且没有Invoke的参数数组分配与装箱
        /// </summary>
        private static TDelegate Compile<TDelegate>(MemberInfo member, string context) where TDelegate : Delegate {
            if (member == null) {
                return null;
            }
            try {
                MethodInfo invoke = typeof(TDelegate).GetMethod("Invoke");
                ParameterInfo[] delegateParams = invoke.GetParameters();
                ParameterExpression[] parameters = new ParameterExpression[delegateParams.Length];
                for (int i = 0; i < delegateParams.Length; i++) {
                    parameters[i] = Expression.Parameter(delegateParams[i].ParameterType);
                }

                Expression body;
                if (member is MethodInfo method) {
                    Expression instance = null;
                    int argOffset = 0;
                    if (!method.IsStatic) {
                        instance = Expression.Convert(parameters[0], method.DeclaringType);
                        argOffset = 1;
                    }
                    ParameterInfo[] methodParams = method.GetParameters();
                    Expression[] args = new Expression[methodParams.Length];
                    for (int i = 0; i < methodParams.Length; i++) {
                        args[i] = Expression.Convert(parameters[i + argOffset], methodParams[i].ParameterType);
                    }
                    body = Expression.Call(instance, method, args);
                }
                else if (member is PropertyInfo property) {
                    Expression instance = property.GetMethod.IsStatic
                        ? null : Expression.Convert(parameters[0], property.DeclaringType);
                    body = Expression.Property(instance, property);
                }
                else if (member is FieldInfo field) {
                    Expression instance = field.IsStatic
                        ? null : Expression.Convert(parameters[0], field.DeclaringType);
                    body = Expression.Field(instance, field);
                }
                else {
                    return null;
                }

                if (invoke.ReturnType != typeof(void) && body.Type != invoke.ReturnType) {
                    body = Expression.Convert(body, invoke.ReturnType);
                }
                return Expression.Lambda<TDelegate>(body, parameters).Compile();
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Warn($"MSRef: failed to compile accessor for {context}: {ex.Message}");
                return null;
            }
        }

        //一次性异常日志，防止每帧/每tick刷屏
        private static readonly HashSet<string> loggedFailures = [];
        private static void LogException(string context, Exception ex) {
            string key = $"{context}|{ex.GetType().Name}";
            if (loggedFailures.Add(key)) {
                CWRMod.Instance.Logger.Warn($"MSRef failed at {context}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        #endregion

        #region 存储核心访问
        /// <summary>
        /// 从TileEntity获取关联的StorageHeart，
        /// 通过基类虚方法分派支持StorageHeart、RemoteAccess、StorageAccess、CraftingAccess等全部组件
        /// </summary>
        internal static object GetHeartFromTileEntity(TileEntity te) {
            if (!Has || te == null || !storageComponentType.IsInstanceOfType(te)) {
                return null;
            }
            try {
                return getHeartFunc(te);
            } catch (Exception ex) {
                LogException(nameof(GetHeartFromTileEntity), ex);
                return null;
            }
        }

        /// <summary>
        /// 检查存储核心是否有空间存放物品，item传null时只检查是否存在未满的存储单元
        /// </summary>
        internal static bool HeartHasSpace(object heart, Item item) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart)) {
                return false;
            }
            //检查安全系统权限
            if (!canPlayerAccessFunc(Main.LocalPlayer, -1)) {
                return false;
            }
            try {
                foreach (object unit in getStorageUnitsFunc(heart)) {
                    if (unitInactiveFunc(unit)) {
                        continue;
                    }
                    if (!unitIsFullFunc(unit)) {
                        return true;
                    }
                    if (item != null && unitHasSpaceInStackForFunc(unit, item)) {
                        return true;
                    }
                }
            } catch (Exception ex) {
                LogException(nameof(HeartHasSpace), ex);
            }
            return false;
        }

        /// <summary>
        /// 在指定范围内查找有空间的MagicStorage存储核心（包括各类远程端口），找不到返回null
        /// </summary>
        internal static object FindMagicStorage(Item item, Point16 position, int maxFindChestMode) {
            if (!Has) {
                return null;
            }

            int range = maxFindChestMode / 16;
            for (int x = position.X - range; x <= position.X + range; x++) {
                for (int y = position.Y - range; y <= position.Y + range; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    if (!TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity te)) {
                        continue;
                    }
                    object heart = GetHeartFromTileEntity(te);
                    if (heart != null && HeartHasSpace(heart, item)) {
                        return heart;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定位置处关联的、有空间的存储核心，没有则返回null
        /// </summary>
        internal static object GetMagicStorage(Item item, Point16 position) {
            if (!Has || !TileEntity.ByPosition.TryGetValue(position, out TileEntity te)) {
                return null;
            }
            object heart = GetHeartFromTileEntity(te);
            if (heart != null && HeartHasSpace(heart, item)) {
                return heart;
            }
            return null;
        }

        /// <summary>
        /// 向存储核心存入物品，成功调用返回true（剩余数量语义由调用方处理）
        /// </summary>
        internal static bool DepositIntoHeart(object heart, Item item) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart) || item == null || item.IsAir) {
                return false;
            }
            try {
                depositItemFunc(heart, item);
                return true;
            } catch (Exception ex) {
                LogException(nameof(DepositIntoHeart), ex);
                return false;
            }
        }

        /// <summary>
        /// 从存储核心取出指定类型与数量的物品
        /// </summary>
        internal static Item WithdrawFromHeart(object heart, int itemType, int count) {
            Item toWithdraw = new Item();
            toWithdraw.SetDefaults(itemType);
            toWithdraw.stack = count;
            return WithdrawFromHeart(heart, toWithdraw);
        }

        /// <summary>
        /// 从存储核心取出指定物品
        /// </summary>
        internal static Item WithdrawFromHeart(object heart, Item toWithdraw) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart)) {
                return new Item();
            }
            //检查安全系统权限
            if (!canPlayerAccessFunc(Main.LocalPlayer, -1)) {
                return new Item();
            }
            try {
                return withdrawFunc(heart, toWithdraw, false) ?? new Item();
            } catch (Exception ex) {
                LogException(nameof(WithdrawFromHeart), ex);
                return new Item();
            }
        }

        /// <summary>
        /// 枚举指定存储核心内的物品
        /// </summary>
        internal static IEnumerable<Item> GetStoredItems(object heart) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart)) {
                return [];
            }
            try {
                return getStoredItemsFunc(heart) ?? [];
            } catch (Exception ex) {
                LogException(nameof(GetStoredItems), ex);
                return [];
            }
        }

        /// <summary>
        /// 枚举本地玩家当前连接的存储核心内的物品
        /// </summary>
        public static IEnumerable<Item> GetStoredItems() => GetStoredItems(GetLocalPlayerHeart());

        /// <summary>
        /// 统计指定存储核心内某类型物品的总数
        /// </summary>
        internal static long GetItemCount(object heart, int itemType) {
            long count = 0;
            foreach (Item item in GetStoredItems(heart)) {
                if (item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        /// <summary>
        /// 获取本地玩家当前连接的存储核心，没有连接返回null
        /// </summary>
        private static object GetLocalPlayerHeart() {
            if (!LinkageReady) {
                return null;
            }
            try {
                return getStorageHeartFunc(Main.LocalPlayer.GetModPlayer(storagePlayerTemplate));
            } catch (Exception ex) {
                LogException(nameof(GetLocalPlayerHeart), ex);
                return null;
            }
        }

        /// <summary>
        /// 获取玩家当前连接的制作核心的制作站列表，未连接返回null
        /// </summary>
        private static List<Item> GetCraftingStations(Player player) {
            if (!LinkageReady) {
                return null;
            }
            object craftingAccess = getCraftingAccessFunc(player.GetModPlayer(storagePlayerTemplate));
            if (craftingAccess == null) {
                return null;
            }
            return craftingStationsFunc(craftingAccess);
        }
        #endregion

        #region 制作界面访问
        /// <summary>
        /// 魔法存储的制作界面当前是否打开
        /// </summary>
        public static bool IsCraftingUIOpen() => LinkageReady && isCraftingUIOpenFunc();

        /// <summary>
        /// 获取魔法存储制作界面当前选中的配方，不可用时返回null
        /// </summary>
        public static Recipe GetSelectedRecipe() {
            if (!LinkageReady || selectedRecipeFunc == null || !isCraftingUIOpenFunc()) {
                return null;
            }
            return selectedRecipeFunc();
        }

        /// <summary>
        /// 获取当前选中配方的结果物品
        /// </summary>
        public static Item GetSelectedRecipeResultItem() => GetSelectedRecipe()?.createItem;

        /// <summary>
        /// 获取魔法存储制作界面配方面板右侧的锚点位置，用于联动UI的摆放
        /// </summary>
        public static bool TryGetCraftingPagePosition(out Vector2 position, out CalculatedStyle dimensions) {
            position = Vector2.Zero;
            dimensions = default;

            if (!LinkageReady || craftingUIFunc == null || recipePanelFunc == null || !isCraftingUIOpenFunc()) {
                return false;
            }

            object craftingUI = craftingUIFunc();
            if (craftingUI == null || !craftingUIStateType.IsInstanceOfType(craftingUI)) {
                return false;
            }
            if (recipePanelFunc(craftingUI) is not UIElement recipePanel) {
                return false;
            }

            dimensions = recipePanel.GetDimensions();
            position = new Vector2(dimensions.X + dimensions.Width, dimensions.Y);
            return true;
        }
        #endregion

        #region 终焉工作台联动
        private static int oldSelectedItemType;
        private static bool craftingUIWasOpen;
        private static bool hasSupertableStation;
        private static uint nextStationScanTime;
        //制作站列表的扫描间隔（帧），降低每帧反射遍历的开销
        private const uint StationScanInterval = 12;
        //联动逻辑连续异常达到上限后本次会话内熔断，避免每帧抛异常拖垮游戏
        private static bool linkageBroken;
        private static int linkageFailureCount;
        private const int MaxLinkageFailures = 3;

        internal static void UpdateUI() {
            if (!LinkageReady || linkageBroken || Main.gameMenu) {
                return;
            }
            try {
                UpdateUIInner();
                linkageFailureCount = 0;
            } catch (Exception ex) {
                LogException(nameof(UpdateUI), ex);
                if (++linkageFailureCount >= MaxLinkageFailures) {
                    linkageBroken = true;
                    CWRMod.Instance.Logger.Warn("MSRef: Supertable-MagicStorage UI linkage disabled for this session after repeated failures");
                    //熔断时收起因联动打开的UI，避免残留
                    if (SupertableUI.Instance != null && SupertableUI.Instance.Active && SupertableUI.TramTP == null) {
                        SupertableUI.Instance.Active = false;
                    }
                }
            }
        }

        private static void UpdateUIInner() {
            bool magicStorageOpen = isCraftingUIOpenFunc();
            if (!magicStorageOpen) {
                if (craftingUIWasOpen) {
                    craftingUIWasOpen = false;
                    hasSupertableStation = false;
                    oldSelectedItemType = ItemID.None;
                }
                //如果魔法存储界面关闭了，且UI是因为联动打开的（TramTP为null），则关闭
                if (SupertableUI.Instance.Active && SupertableUI.TramTP == null) {
                    SupertableUI.Instance.Active = false;
                }
                return;
            }

            //界面刚打开或到达扫描间隔时才重扫制作站列表，结果缓存到下次扫描
            if (!craftingUIWasOpen || Main.GameUpdateCount >= nextStationScanTime) {
                craftingUIWasOpen = true;
                nextStationScanTime = Main.GameUpdateCount + StationScanInterval;
                hasSupertableStation = ScanSupertableStation();
            }

            if (hasSupertableStation) {
                //如果终焉工作台UI没打开，则打开它
                if (!SupertableUI.Instance.Active) {
                    SupertableUI.TramTP = null;
                    SupertableUI.Instance.Active = true;
                    if (TryGetCraftingPagePosition(out Vector2 pos, out _)) {
                        SupertableUI.Instance.DrawPosition = pos;
                    }
                    oldSelectedItemType = ItemID.None;//强制一次配方同步
                }
                //如果已经打开，并且来自某个实体，先关闭，防止污染数据
                else {
                    SupertableUI.TramTP?.CloseUI(Main.LocalPlayer);
                }

                //同步配方选择
                SyncSelectedRecipe();
            }
            else if (SupertableUI.Instance.Active && SupertableUI.TramTP == null) {
                //如果不包含终焉工作台，且UI是因为联动打开的，则关闭
                SupertableUI.Instance.Active = false;
            }
        }

        /// <summary>
        /// 检查当前连接的制作核心是否放入了终焉物质转换仪
        /// </summary>
        private static bool ScanSupertableStation() {
            List<Item> stations = GetCraftingStations(Main.LocalPlayer);
            if (stations == null) {
                return false;
            }
            int targetType = ModContent.ItemType<TransmutationOfMatterItem>();
            for (int i = 0; i < stations.Count; i++) {
                if (stations[i].type == targetType) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 将魔法存储制作界面选中的配方同步到终焉工作台
        /// </summary>
        private static void SyncSelectedRecipe() {
            if (!SupertableUI.Instance.Active) {
                return;
            }
            Item selectedItem = GetSelectedRecipeResultItem();
            if (selectedItem == null || selectedItem.type <= ItemID.None) {
                return;
            }
            if (oldSelectedItemType == selectedItem.type) {
                return;
            }
            var sidebar = SupertableUI.Instance.SidebarManager;
            if (sidebar == null) {
                return;
            }
            //检查当前选中的配方是否与魔法存储选中的物品一致
            if ((sidebar.SelectedRecipe?.RecipeData.Target) == selectedItem.type) {
                return;
            }
            //查找对应的配方
            for (int i = 0; i < sidebar.RecipeElements.Count; i++) {
                var element = sidebar.RecipeElements[i];
                if (element.RecipeData.Target == selectedItem.type) {
                    //更新选中状态
                    sidebar.SelectedRecipe = element;
                    SupertableUI.Instance.RecipeNavigator?.SetRecipeByData(element.RecipeData);
                    sidebar.ScrollToRecipe(i);
                    break;
                }
            }
            oldSelectedItemType = selectedItem.type;
        }
        #endregion
    }

    internal class MSRefLoader : ICWRLoader
    {
        void ICWRLoader.LoadData() => MSRef.Load();
        void ICWRLoader.UnLoadData() => MSRef.Unload();
    }

    internal class MSRefSystem : ModSystem
    {
        public override void UpdateUI(GameTime gameTime) => MSRef.UpdateUI();
    }
}
