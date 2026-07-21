using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    /// <summary>
    /// MagicStorage 弱引用层，模式同 <see cref="CWRRef"/>
    /// </summary>
    internal static class MSRef
    {
        /// <summary>最低兼容版本</summary>
        internal static Version TargetVersion => new(0, 7, 0, 11);
        /// <summary>模组可用且核心委托已就绪</summary>
        internal static bool Has { get; private set; }

        #region 编译委托缓存
        //类型守卫用
        private static Type storageComponentType;
        private static Type storageHeartType;
        //GetHeart 虚方法，基类委托可覆盖全部组件
        private static Func<object, object> getHeartFunc;
        //TEStorageHeart
        private static Func<object, IEnumerable> getStorageUnitsFunc;
        private static Func<object, IEnumerable<Item>> getStoredItemsFunc;
        private static Action<object, Item> depositItemFunc;
        private static Func<object, Item, bool, Item> withdrawFunc;
        //TEAbstractStorageUnit，热路径
        private static Func<object, bool> unitInactiveFunc;
        private static Func<object, bool> unitIsFullFunc;
        private static Func<object, Item, bool> unitHasSpaceInStackForFunc;
        //SecuritySystem
        private static Func<Player, int, bool> canPlayerAccessFunc;
        #endregion

        #region 加载与卸载
        internal static void Load() {
            Has = false;

            Mod mod = CWRMod.Instance.magicStorage;
            if (mod == null || mod.Version < TargetVersion) {
                return;
            }

            const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags PubStatic = BindingFlags.Public | BindingFlags.Static;

            storageComponentType = GetModType(mod, "MagicStorage.Components.TEStorageComponent");
            storageHeartType = GetModType(mod, "MagicStorage.Components.TEStorageHeart");
            Type unitType = GetModType(mod, "MagicStorage.Components.TEAbstractStorageUnit");
            Type securityType = GetModType(mod, "MagicStorage.Common.Systems.SecuritySystem");

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

            canPlayerAccessFunc = Compile<Func<Player, int, bool>>(
                GetMethod(securityType, "CanPlayerAccessImmediately", PubStatic), "SecuritySystem.CanPlayerAccessImmediately");

            //核心委托齐才算可用
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
        }

        internal static void Unload() {
            Has = false;
            storageComponentType = null;
            storageHeartType = null;
            getHeartFunc = null;
            getStorageUnitsFunc = null;
            getStoredItemsFunc = null;
            depositItemFunc = null;
            withdrawFunc = null;
            unitInactiveFunc = null;
            unitIsFullFunc = null;
            unitHasSpaceInStackForFunc = null;
            canPlayerAccessFunc = null;
            loggedFailures.Clear();
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

        /// <summary>反射成员→委托，自动插类型转换</summary>
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

        //异常只打一次，防刷屏
        private static readonly HashSet<string> loggedFailures = [];
        private static void LogException(string context, Exception ex) {
            string key = $"{context}|{ex.GetType().Name}";
            if (loggedFailures.Add(key)) {
                CWRMod.Instance.Logger.Warn($"MSRef failed at {context}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        #endregion

        #region 存储核心访问
        /// <summary>经虚分派取 StorageHeart</summary>
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

        /// <summary>item 为 null 时只查是否有未满单元</summary>
        internal static bool HeartHasSpace(object heart, Item item) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart)) {
                return false;
            }
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

        /// <summary>范围内找有空间的存储核心</summary>
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

        /// <summary>指定位置的存储核心</summary>
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

        /// <summary>存入，剩余量由调用方读 item</summary>
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

        /// <summary>按类型与数量取出</summary>
        internal static Item WithdrawFromHeart(object heart, int itemType, int count) {
            Item toWithdraw = new Item();
            toWithdraw.SetDefaults(itemType);
            toWithdraw.stack = count;
            return WithdrawFromHeart(heart, toWithdraw);
        }

        /// <summary>按 Item 取出</summary>
        internal static Item WithdrawFromHeart(object heart, Item toWithdraw) {
            if (!Has || heart == null || !storageHeartType.IsInstanceOfType(heart)) {
                return new Item();
            }
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

        /// <summary>枚举核心内物品</summary>
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

        /// <summary>统计某类型总数</summary>
        internal static long GetItemCount(object heart, int itemType) {
            long count = 0;
            foreach (Item item in GetStoredItems(heart)) {
                if (item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }
        #endregion
    }

    internal class MSRefLoader : ICWRLoader
    {
        void ICWRLoader.LoadData() => MSRef.Load();
        void ICWRLoader.UnLoadData() => MSRef.Unload();
    }
}
