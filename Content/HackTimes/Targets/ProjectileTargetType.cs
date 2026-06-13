using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>弹幕目标工厂</summary>
    internal class ProjectileTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Projectile;

        public override int HoverPriority => 60;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;
            const int expandMargin = 12;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.type == ProjectileID.None) continue;

                Rectangle hitbox = projectile.Hitbox;
                if (hitbox.Width < 8 || hitbox.Height < 8) {
                    hitbox = new Rectangle((int)projectile.Center.X - 4, (int)projectile.Center.Y - 4, 8, 8);
                }
                hitbox.Inflate(expandMargin, expandMargin);
                if (!hitbox.Contains(mouseWorld.ToPoint())) continue;

                float dx = mouseWorld.X - projectile.Center.X;
                float dy = mouseWorld.Y - projectile.Center.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            return bestIndex < 0 ? null : new ProjectileScannable(bestIndex);
        }
    }
}
