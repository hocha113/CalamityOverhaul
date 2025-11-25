using MagicStorage.Common.Systems;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    internal class MSRef
    {
        internal static TEStorageHeart FindMagicStorage(Item item, Point16 position, int maxFindChestMode) {
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
