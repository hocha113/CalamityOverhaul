using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>
    /// 弹幕生成库：全部内置权威端守卫，图案参数一律在生成时写入弹幕ai，
    /// 生成后行为是确定函数，各端图案一致
    /// </summary>
    internal static class EmpressCast
    {
        /// <summary>是否可生成（仅服务端/单机）</summary>
        private static bool Authority => !VaultUtils.isClient;

        /// <summary>棱彩弹：mode 0直线 1定转率螺旋 2悬滞蓄释 3限时缓追踪</summary>
        public static void Bolt(NPC npc, Vector2 pos, Vector2 vel, int damage, int mode, float hue, float param = 0f) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<EmpressPrismBolt>(), damage, 0f, Main.myPlayer, mode, hue % 1f, param);
        }

        /// <summary>以太枪骑：telegraph 帧后沿 angle 贯穿</summary>
        public static void Lance(NPC npc, Vector2 pos, float angle, int damage, float hue, int telegraph) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressLance>(), damage, 0f, Main.myPlayer, angle, hue % 1f, telegraph);
        }

        /// <summary>光剑：hover 帧悬停瞄准后齐射，target=锁定玩家索引</summary>
        public static void Blade(NPC npc, Vector2 pos, int hover, int damage, float hue, int targetIndex) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressBlade>(), damage, 0f, Main.myPlayer, hover, hue % 1f, targetIndex);
        }

        /// <summary>日舞光束：锚在女皇，baseAngle 起始，sweep 每帧旋切</summary>
        public static void Sunray(NPC npc, float baseAngle, float sweep, int damage) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<EmpressSunray>(), damage, 0f, Main.myPlayer, baseAngle, npc.whoAmI, sweep);
        }

        /// <summary>永恒虹瓣：curve 每帧曲率，gain 加速档</summary>
        public static void Petal(NPC npc, Vector2 pos, Vector2 vel, int damage, float curve, float hue, float gain = 1f) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<EmpressPetal>(), damage, 0f, Main.myPlayer, curve, hue % 1f, gain);
        }

        /// <summary>极光帘幕：drift 横漂速度，life 寿命</summary>
        public static void Aurora(NPC npc, Vector2 pos, float phase, float drift, int life, int damage) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressAuroraVeil>(), damage, 0f, Main.myPlayer, phase, drift, life);
        }

        /// <summary>辉光爆放：纯演出，各端可见</summary>
        public static void Radiance(NPC npc, Vector2 pos, float radius, int life, float hue) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressRadiance>(), 0, 0f, Main.myPlayer, radius, life, hue % 1f);
        }

        /// <summary>清空本Boss的全部敌对弹幕（转阶段/大招/死亡的公平阀）</summary>
        public static void ClearHostileProjectiles(NPC npc) {
            if (!Authority) {
                return;
            }
            int bolt = ModContent.ProjectileType<EmpressPrismBolt>();
            int lance = ModContent.ProjectileType<EmpressLance>();
            int blade = ModContent.ProjectileType<EmpressBlade>();
            int sunray = ModContent.ProjectileType<EmpressSunray>();
            int petal = ModContent.ProjectileType<EmpressPetal>();
            int veil = ModContent.ProjectileType<EmpressAuroraVeil>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.hostile) {
                    continue;
                }
                if (p.type == bolt || p.type == lance || p.type == blade
                    || p.type == sunray || p.type == petal || p.type == veil) {
                    p.Kill();
                }
            }
        }
    }
}
