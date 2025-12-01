using MagicStorage.Common.Systems;
using MagicStorage.Components;
using System;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    internal class MSRef
    {
        private static MethodInfo _depositItemMethod;
        [JITWhenModsEnabled("MagicStorage")]
        internal static MethodInfo DepositItemMethod {
            get {
                if (CWRMod.Instance.magicStorage == null) {
                    return null;
                }
                _depositItemMethod ??= typeof(TEStorageHeart).GetMethod("DepositItem");
                return _depositItemMethod;
            }
        }

        [JITWhenModsEnabled("MagicStorage")]
        internal static object FindMagicStorage(Item item, Point16 position, int maxFindChestMode) {//所以，对外返回obj，或者是其他不需要引用外部程序集的已有类型，这样才能避免触发编译错误
            if (CWRMod.Instance.magicStorage == null || CWRMod.Instance.magicStorage.Version < new Version(0, 7, 0, 11)) {//0.7.0.11
                return null;
            }
            //在一定范围内查找 Magic Storage 的存储核心
            for (int x = position.X - (maxFindChestMode / 16); x <= position.X + (maxFindChestMode / 16); x++) {
                for (int y = position.Y - (maxFindChestMode / 16); y <= position.Y + (maxFindChestMode / 16); y++) {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Point16 checkPos = new Point16(x, y);
                    if (TileEntity.ByPosition.TryGetValue(checkPos, out TileEntity te) && te is TEStorageHeart heart) {
                        //检查安全系统权限
                        if (!SecuritySystem.CanPlayerAccessImmediately(Main.LocalPlayer, -1))
                            continue;

                        //检查存储核心是否还有容量
                        bool hasSpace = false;
                        foreach (var unit in heart.GetStorageUnits()) {
                            if (!unit.Inactive && (unit.HasSpaceInStackFor(item) || !unit.IsFull)) {
                                hasSpace = true;
                                break;
                            }
                        }

                        if (hasSpace)
                            return heart;
                    }
                }
            }

            return null;
        }
    }
}
