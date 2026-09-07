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

        /// <summary>万华镜鞭击：tip 落点，fireDelay 帧后抽下，strong 强击</summary>
        public static void Whip(NPC npc, Vector2 tip, int damage, int fireDelay, bool strong) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), tip, Vector2.Zero, ModContent.ProjectileType<EmpressWhipCrack>(),
                damage, 0f, Main.myPlayer, tip.X, tip.Y, EmpressWhipCrack.PackAi2(npc.whoAmI, fireDelay, strong));
        }

        /// <summary>光痕：在玩家脚下留一道</summary>
        public static void Echo(NPC npc, Player victim, int damage) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), victim.Center, Vector2.Zero, ModContent.ProjectileType<EmpressEcho>(),
                damage, 0f, Main.myPlayer, victim.whoAmI, npc.whoAmI);
        }

        /// <summary>光蝶：spawnBar 出生小节序号（合掌奇偶由它推）</summary>
        public static void Lacewing(NPC npc, Vector2 pos, Vector2 vel, int damage, int targetIndex, int spawnBar) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel, ModContent.ProjectileType<EmpressLacewing>(),
                damage, 0f, Main.myPlayer, targetIndex, npc.whoAmI, spawnBar);
        }

        /// <summary>月屑：零伤害暗盘，life 寿命帧</summary>
        public static void MoonShard(NPC npc, Vector2 pos, Vector2 vel, int life) {
            if (!Authority) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel, ModContent.ProjectileType<EmpressMoonShard>(),
                0, 0f, Main.myPlayer, npc.whoAmI, life);
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

        /// <summary>是否本 Boss 的敌对弹幕类型（月屑零伤害，但清场时一并处理）</summary>
        public static bool IsHostileType(int type) {
            return type == ModContent.ProjectileType<EmpressLightBolt>()
                || type == ModContent.ProjectileType<EmpressTrackBeam>()
                || type == ModContent.ProjectileType<EmpressLance>()
                || type == ModContent.ProjectileType<EmpressWhipCrack>()
                || type == ModContent.ProjectileType<EmpressEcho>()
                || type == ModContent.ProjectileType<EmpressLacewing>()
                || type == ModContent.ProjectileType<EmpressMoonShard>();
        }

        /// <summary>
        /// 退潮式清场（转阶段/投技/死亡的公平阀）：光球与长枪压寿命几帧内老化消失；
        /// 伤害窗靳在寿命尾端的（光束、鞭、光痕）直接结束，光痕不许在清场时碎出光球；光蝶与月屑立即收
        /// </summary>
        public static void ClearHostileProjectiles(NPC npc, int fadeFrames = 8) {
            if (!Authority) {
                return;
            }
            int bolt = ModContent.ProjectileType<EmpressLightBolt>();
            int lance = ModContent.ProjectileType<EmpressLance>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !IsHostileType(p.type)) {
                    continue;
                }
                if (p.type == bolt || p.type == lance) {
                    p.timeLeft = System.Math.Min(p.timeLeft, fadeFrames);
                    p.netUpdate = true;
                    continue;
                }
                p.Kill();
            }
        }
    }
}
