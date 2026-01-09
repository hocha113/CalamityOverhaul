using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Storage
{
    /// <summary>
    /// 原版箱子的存储提供者实现
    /// </summary>
    public class ChestStorageProvider : IStorageProvider
    {
        private readonly Chest _chest;
        private readonly int _chestIndex;

        public string Identifier => "Vanilla.Chest";
        public Point16 Position { get; }
        public Vector2 WorldCenter => Position.ToWorldCoordinates() + new Vector2(16, 16);
        public Rectangle HitBox => new Rectangle(Position.X * 16, Position.Y * 16, 32, 32);

        public bool IsValid {
            get {
                if (_chestIndex < 0 || _chestIndex >= Main.maxChests) {
                    return false;
                }
                Chest chest = Main.chest[_chestIndex];
                return chest != null && chest.x == Position.X && chest.y == Position.Y;
            }
        }

        public bool HasSpace {
            get {
                if (!IsValid) {
                    return false;
                }
                foreach (Item item in _chest.item) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        public ChestStorageProvider(Chest chest, int chestIndex) {
            _chest = chest;
            _chestIndex = chestIndex;
            Position = new Point16(chest.x, chest.y);
        }

        /// <summary>
        /// 从箱子索引创建存储提供者
        /// </summary>
        public static ChestStorageProvider FromIndex(int chestIndex) {
            if (chestIndex < 0 || chestIndex >= Main.maxChests) {
                return null;
            }
            Chest chest = Main.chest[chestIndex];
            if (chest == null) {
                return null;
            }
            return new ChestStorageProvider(chest, chestIndex);
        }

        /// <summary>
        /// 从世界坐标查找箱子并创建存储提供者
        /// </summary>
        public static ChestStorageProvider FromPosition(Point16 position) {
            int index = Chest.FindChest(position.X, position.Y);
            if (index < 0) {
                return null;
            }
            return FromIndex(index);
        }

        public bool CanAcceptItem(Item item) {
            if (!IsValid || item == null || item.IsAir) {
                return false;
            }
            return _chest.CanItemBeAddedToChest(item);
        }

        public bool DepositItem(Item item) {
            if (!CanAcceptItem(item)) {
                return false;
            }
            _chest.AddItem(item, true);
            return true;
        }

        public Item WithdrawItem(int itemType, int count) {
            if (!IsValid || count <= 0) {
                return new Item();
            }

            int remaining = count;
            Item result = new Item(itemType, 0);

            foreach (Item slotItem in _chest.item) {
                if (slotItem == null || slotItem.IsAir || slotItem.type != itemType) {
                    continue;
                }

                int take = System.Math.Min(remaining, slotItem.stack);
                slotItem.stack -= take;
                result.stack += take;
                remaining -= take;

                if (slotItem.stack <= 0) {
                    slotItem.TurnToAir();
                }

                if (remaining <= 0) {
                    break;
                }
            }

            if (result.stack > 0) {
                result.type = itemType;
            }
            return result;
        }

        public IEnumerable<Item> GetStoredItems() {
            if (!IsValid) {
                yield break;
            }
            foreach (Item item in _chest.item) {
                if (item != null && !item.IsAir) {
                    yield return item;
                }
            }
        }

        public long GetItemCount(int itemType) {
            if (!IsValid) {
                return 0;
            }
            long count = 0;
            foreach (Item item in _chest.item) {
                if (item != null && !item.IsAir && item.type == itemType) {
                    count += item.stack;
                }
            }
            return count;
        }

        public void PlayDepositAnimation() {
            if (IsValid) {
                _chest.eatingAnimationTime = 20;
            }
        }
    }
}
