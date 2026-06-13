using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>掉落物目标工厂</summary>
    internal class ItemTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Item;

        public override int HoverPriority => 50;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;
            const int expandMargin = 12;

            for (int i = 0; i < Main.maxItems; i++) {
                Item item = Main.item[i];
                if (!IsScannableItem(item)) continue;

                Rectangle hitbox = item.Hitbox;
                if (hitbox.Width < 12 || hitbox.Height < 12) {
                    hitbox = new Rectangle((int)item.Center.X - 6, (int)item.Center.Y - 6, 12, 12);
                }
                hitbox.Inflate(expandMargin, expandMargin);
                if (!hitbox.Contains(mouseWorld.ToPoint())) continue;

                float dx = mouseWorld.X - item.Center.X;
                float dy = mouseWorld.Y - item.Center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            return bestIndex < 0 ? null : new ItemScannable(bestIndex);
        }

        internal static bool IsScannableItem(Item item) {
            return item != null && item.active && !item.IsAir
                && item.type > ItemID.None && item.stack > 0;
        }
    }
}
