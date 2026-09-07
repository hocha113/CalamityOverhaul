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
        private static bool Authority => !VaultUtils.isClient;

        /// <summary>光球：mode 见 <see cref="EmpressBoltMode"/>，param 模式参数（旋转弧度/追踪玩家索引）</summary>
        public static void Bolt(NPC npc, Vector2 pos, Vector2 vel, int damage, EmpressBoltMode mode, float param = 0f) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<EmpressLightBolt>(), damage, 0f, Main.myPlayer, (int)mode, param, npc.whoAmI);
        }

        /// <summary>追踪光束：angle 起始朝向，mode 见 <see cref="EmpressBeamMode"/>，extraFire 发射后延长帧（持续/屏障用）</summary>
        public static void Beam(NPC npc, Vector2 pos, float angle, int damage, EmpressBeamMode mode = EmpressBeamMode.Normal, int extraFire = 0) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressTrackBeam>(), damage, 0f, Main.myPlayer, angle, (int)mode + extraFire * 10, npc.whoAmI);
        }

        /// <summary>以太长枪：angle 初始朝向，mode 见 <see cref="EmpressLanceMode"/></summary>
        public static void Lance(NPC npc, Vector2 pos, float angle, int damage, EmpressLanceMode mode) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressLance>(), damage, 0f, Main.myPlayer, angle, (int)mode, npc.whoAmI);
        }

        /// <summary>瞬现长枪：pos 线心，angle 线向，fireDelay 帧后整线致命 6f</summary>
        public static void Hitscan(NPC npc, Vector2 pos, float angle, int damage, int fireDelay) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressHitscanLance>(), damage, 0f, Main.myPlayer, angle, fireDelay, npc.whoAmI);
        }

        /// <summary>熔光扇：startAngle 起始角，sweep 充能期扫过弧度（带符号）</summary>
        public static void Fan(NPC npc, float startAngle, float sweep, int damage) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<EmpressMeltingFan>(), damage, 0f, Main.myPlayer, startAngle, sweep, npc.whoAmI);
        }

        /// <summary>冲击波：零伤害推开</summary>
        public static void Shockwave(NPC npc, Vector2 pos) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressShockwave>(), 0, 0f, Main.myPlayer, npc.whoAmI);
        }

        /// <summary>极光帘幕：drift 横漂速度，life 寿命（入场/死亡演出装饰）</summary>
        public static void Aurora(NPC npc, Vector2 pos, float phase, float drift, int life, int damage) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressAuroraVeil>(), damage, 0f, Main.myPlayer, phase, drift, life);
        }

        /// <summary>光绫束缚：零伤害缚定视觉，victim=受缚玩家索引，life=寿命帧</summary>
        public static void LightBind(NPC npc, Vector2 pos, int victim, int life, float hue) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressLightBind>(), 0, 0f, Main.myPlayer, victim, life, hue % 1f);
        }

        /// <summary>散掉指定玩家身上的光绫（投技提前中断的兜底）</summary>
        public static void KillLightBind(int victim) {
            if (!Authority) {
                return;
            }
            int type = ModContent.ProjectileType<EmpressLightBind>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == victim) {
                    p.Kill();
                }
            }
        }

        /// <summary>辉光爆放：纯演出，各端可见</summary>
        public static void Radiance(NPC npc, Vector2 pos, float radius, int life, float hue) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<EmpressRadiance>(), 0, 0f, Main.myPlayer, radius, life, hue % 1f);
        }

        /// <summary>是否本 Boss 的敌对弹幕类型</summary>
        public static bool IsHostileType(int type) {
            return type == ModContent.ProjectileType<EmpressLightBolt>()
                || type == ModContent.ProjectileType<EmpressTrackBeam>()
                || type == ModContent.ProjectileType<EmpressLance>()
                || type == ModContent.ProjectileType<EmpressHitscanLance>()
                || type == ModContent.ProjectileType<EmpressMeltingFan>();
        }

        /// <summary>
        /// 退潮式清场（转阶段/投技/死亡的公平阀）：不瞬灭，寿命压到 fadeFrames 内让弹幕几帧内老化消失，
        /// 光束与扇直接结束（它们的伤害窗靳在寿命尾端，不能留）
        /// </summary>
        public static void ClearHostileProjectiles(NPC npc, int fadeFrames = 8) {
            if (!Authority) {
                return;
            }
            int beam = ModContent.ProjectileType<EmpressTrackBeam>();
            int fan = ModContent.ProjectileType<EmpressMeltingFan>();
            int hitscan = ModContent.ProjectileType<EmpressHitscanLance>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !IsHostileType(p.type)) {
                    continue;
                }
                if (p.type == beam || p.type == fan || p.type == hitscan) {
                    p.Kill();
                    continue;
                }
                p.timeLeft = System.Math.Min(p.timeLeft, fadeFrames);
                p.netUpdate = true;
            }
        }
    }
}
