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
    /// MagicStorage的弱引用访问层，不持有任何MagicStorage的编译期类型
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

        #region 编译委托缓存
        //类型缓存，调用前实例类型守卫
        private static Type storageComponentType;
        private static Type storageHeartType;
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
        //SecuritySystem的静态方法
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

            //核心存取能力，物流存取所需
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
        /// 向存储核心存入物品，成功调用返回true（剩余数量语义由调用方）
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
        #endregion
    }

    internal class MSRefLoader : ICWRLoader
    {
        void ICWRLoader.LoadData() => MSRef.Load();
        void ICWRLoader.UnLoadData() => MSRef.Unload();
    }
}
