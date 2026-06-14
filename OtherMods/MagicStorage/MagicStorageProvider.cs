using InnoVault.Storages;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.OtherMods.MagicStorage
{
    /// <summary>
    /// Magic Storage存储提供者工厂
    /// </summary>
    public class MagicStorageProviderFactory : IStorageProviderFactory
    {
        public string Identifier => "MagicStorage.StorageHeart";
        public int Priority => 10;
        public bool IsAvailable => MSRef.Has;

        public IEnumerable<IStorageProvider> FindStorageProviders(Point16 position, int range, Item item) {
            var provider = MagicStorageProvider.FindNearPosition(position, range, item);
            if (provider != null) {
                yield return provider;
            }
        }

        public IStorageProvider GetStorageProviders(Point16 position, Item item)
            => MagicStorageProvider.GetAtPosition(position, item);
    }

    /// <summary>
    /// Magic Storage模组的存储核心提供者，
    /// 与MagicStorage的所有交互统一经由<see cref="MSRef"/>的反射缓存完成，无编译期依赖
    /// </summary>
    public class MagicStorageProvider : IStorageProvider
    {
        private readonly object _storageHeart;
        private readonly Point16 _position;

        public string Identifier => "MagicStorage.StorageHeart";
        public Point16 Position => _position;
        public Vector2 WorldCenter => Position.ToWorldCoordinates() + new Vector2(24, 24);
        public Rectangle HitBox => new Rectangle(Position.X * 16, Position.Y * 16, 48, 48);

        public bool IsValid => _storageHeart != null
            && TileEntity.ByPosition.TryGetValue(_position, out TileEntity te)
            && te == _storageHeart;

        public bool HasSpace => IsValid && MSRef.HeartHasSpace(_storageHeart, null);

        public MagicStorageProvider(object storageHeart, Point16 position) {
            _storageHeart = storageHeart;
            _position = position;
        }

        /// <summary>
        /// 在指定范围内查找Magic Storage存储核心
        /// </summary>
        public static MagicStorageProvider FindNearPosition(Point16 position, int range, Item item) {
            if (MSRef.FindMagicStorage(item, position, range) is not TileEntity heart) {
                return null;
            }
            return new MagicStorageProvider(heart, heart.Position);
        }

        /// <summary>
        /// 获取指定位置的Magic Storage存储核心
        /// </summary>
        public static MagicStorageProvider GetAtPosition(Point16 position, Item item) {
            if (MSRef.GetMagicStorage(item, position) is not TileEntity heart) {
                return null;
            }
            return new MagicStorageProvider(heart, heart.Position);
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }
            return MSRef.HeartHasSpace(_storageHeart, item);
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }
            return MSRef.DepositIntoHeart(_storageHeart, item);
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid) {
                return new Item();
            }
            return MSRef.WithdrawFromHeart(_storageHeart, itemType, count);
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (var item in MSRef.GetStoredItems(_storageHeart)) {
                yield return item;
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }
            return MSRef.GetItemCount(_storageHeart, itemType);
        }

        public void PlayDepositAnimation() {
            //Magic Storage没有吞噬动画
        }
    }
}
