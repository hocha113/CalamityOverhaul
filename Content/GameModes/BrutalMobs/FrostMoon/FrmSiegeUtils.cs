using CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon
{
    /// <summary>
    /// 霜月攻城矩阵的共享工具：落点地表扫描、投放天花板扫描、同型弹幕并发计数、
    /// 迫击炮一发的标准生成（小怪与圣诞坦克齐放共用同一配方）。无任何可变状态
    /// </summary>
    internal static class FrmSiegeUtils
    {
        /// <summary>
        /// 迫击炮一发（仅权威端调用）：弹着标记环（预告实体，可见时长=飞行帧）+
        /// 定时长抛物线炮弹（纯视觉载体）。弹道解算：位移项 v=d/T，重力项回扣 g(T+1)/2
        /// （AI 每帧先加重力后位移，T 帧重力位移合计 g·T(T+1)/2）；
        /// 弹幕不吃 GameModeNPC 提速层，无需补偿
        /// </summary>
        internal static void SpawnMortarShot(NPC npc, Vector2 mark, int flight, float scale, int damage) {
            Projectile.NewProjectile(npc.GetSource_FromAI(), mark, Vector2.Zero,
                ModContent.ProjectileType<FrmMortarBlastProj>(), damage, 1f, Main.myPlayer,
                flight, scale);
            Vector2 muzzle = npc.Top + new Vector2(0f, -10f);
            Vector2 d = mark - muzzle;
            Vector2 launch = new Vector2(d.X / flight,
                d.Y / flight - FrmPresentShellProj.Gravity * (flight + 1) * 0.5f);
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, launch,
                ModContent.ProjectileType<FrmPresentShellProj>(), 0, 0f, Main.myPlayer, flight);
        }
        /// <summary>自世界坐标向下扫描首个实心物块，命中返回其上表面 Y（世界坐标）</summary>
        internal static bool TryFindGroundY(Vector2 from, int maxTiles, out float groundY) {
            groundY = 0f;
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            if (tx < 5 || tx > Main.maxTilesX - 5) {
                return false;
            }
            int limit = ty + maxTiles;
            if (limit > Main.maxTilesY - 5) {
                limit = Main.maxTilesY - 5;
            }
            for (int y = ty < 5 ? 5 : ty; y <= limit; y++) {
                if (WorldGen.SolidTile(tx, y)) {
                    groundY = y * 16f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>自世界坐标向上扫描首个实心物块，返回可用的投放顶部 Y（无遮挡时取满高度）</summary>
        internal static float FindDropTopY(Vector2 from, float maxRise) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            int steps = (int)(maxRise / 16f);
            float topY = from.Y - maxRise;
            for (int i = 1; i <= steps; i++) {
                int y = ty - i;
                if (y < 5) {
                    break;
                }
                if (WorldGen.SolidTile(tx, y)) {
                    //停在天花板下方一格，投放物不出生在物块里
                    topY = (y + 1) * 16f + 4f;
                    break;
                }
            }
            return topY;
        }

        /// <summary>存活同型弹幕计数（触发时点算，自愈无漂移）</summary>
        internal static int CountProjOfType(int projType) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == projType) {
                    count++;
                }
            }
            return count;
        }
    }
}
