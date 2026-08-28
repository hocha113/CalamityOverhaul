using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 姿态可视载体（纯表现实体，永不伤害）。ai[0]=模式 ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 近身体术的姿态前摇按 M3 需要可见信号，而客户端 PostAI 早退，尘与光必须由
    /// 同步弹幕实体承载（M8）：本体逐帧贴住宿主，来源死亡/槽位复用即自毁。
    /// 模式：0=寄居蟹缩壳蓄力（壳光渐亮）、1=眩壳惩罚窗（晕斑绕壳）、2=真菌鱼聚力
    /// </summary>
    internal class MushroomChargeGlowProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeShellCharge = 0;
        internal const int ModeShellStun = 1;
        internal const int ModeFishGather = 2;

        /// <summary>缩壳蓄力帧数（M3 姿态前摇下限 ≥24）</summary>
        internal const int ShellChargeFrames = 26;
        /// <summary>眩壳惩罚窗帧数</summary>
        internal const int ShellStunFrames = 20;
        /// <summary>真菌鱼聚力前摇总帧数（末 12 帧为聚力段，M3 下限 ≥24）</summary>
        internal const int FishGatherTotalFrames = 24;

        /// <summary>各模式可见期之外的余量帧：盖住 NPC 侧回读校验的时序差并做退淡</summary>
        private const int FadeTailFrames = 6;

        private static readonly int[] ModeFrames = [ShellChargeFrames, ShellStunFrames, FishGatherTotalFrames];

        private int Mode => (int)Projectile.ai[0];
        private int Duration => ModeFrames[Math.Clamp(Mode, 0, ModeFrames.Length - 1)];
        private int TotalLife => Duration + FadeTailFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        private bool TryGetHost(out NPC host) {
            host = null;
            int srcPacked = (int)Projectile.ai[2];
            int src = (srcPacked & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[src];
            if (!npc.active || npc.type != srcPacked >> 8) {
                return false;//槽位复用不放行
            }
            host = npc;
            return true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
            }

            //来源校验：宿主死亡即消散（可见信号与实际威胁同生共死）
            if (!TryGetHost(out NPC host)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = host.Center;

            if (Main.dedServ || Elapsed >= Duration) {
                return;
            }
            float progress = Elapsed / (float)Duration;
            switch (Mode) {
                case ModeShellCharge when Main.rand.NextBool(2):
                case ModeFishGather when Main.rand.NextBool(2): {
                    //聚拢孢尘：从外圈吸向壳心，越临近提交吸得越急
                    Vector2 dir = Main.rand.NextVector2Unit();
                    Dust dust = Dust.NewDustPerfect(host.Center + dir * Main.rand.NextFloat(20f, 40f),
                        DustID.GlowingMushroom, -dir * (1.2f + 2f * progress), 120, default, 0.95f);
                    dust.noGravity = true;
                    break;
                }
                case ModeShellStun when Main.rand.NextBool(4): {
                    Dust dust = Dust.NewDustPerfect(host.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), -6f),
                        DustID.GlowingMushroom, new Vector2(0f, -0.6f), 150, default, 0.8f);
                    dust.noGravity = true;
                    break;
                }
            }
            Lighting.AddLight(host.Center, MushroomSporeBoltProj.SporeBright.ToVector3()
                * (Mode == ModeShellStun ? 0.12f : 0.08f + 0.2f * progress));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!TryGetHost(out NPC host)) {
                return false;
            }
            int elapsed = Elapsed;
            int duration = Duration;
            float fade = elapsed >= duration
                ? MathHelper.Clamp(1f - (elapsed - duration) / (float)FadeTailFrames, 0f, 1f)
                : MathHelper.Clamp(elapsed / 6f, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Vector2 center = host.Center - Main.screenPosition;
            float progress = MathHelper.Clamp(elapsed / (float)duration, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            if (Mode == ModeShellStun) {
                //眩壳：三粒晕斑绕壳顶缓旋（惩罚窗的可读标记）
                for (int i = 0; i < 3; i++) {
                    float a = Main.GlobalTimeWrappedHourly * 5f + i * MathHelper.TwoPi / 3f;
                    Vector2 pos = center + new Vector2(MathF.Cos(a) * 16f, -host.height * 0.5f - 8f + MathF.Sin(a) * 4f);
                    Main.EntitySpriteDraw(glow, pos, null,
                        (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.6f * fade), 0f,
                        glow.Size() / 2f, 0.09f, SpriteEffects.None, 0);
                }
                return false;
            }

            //蓄力/聚力：暗底外壳 + 亮芯随进度渐亮（壳光渐亮的可见承诺）
            float bodyScale = MathF.Max(host.width, host.height) / (float)rim.Width;
            Main.EntitySpriteDraw(rim, center, null,
                MushroomSporeBoltProj.SporeDeep * (0.55f * fade * progress), 0f,
                rim.Size() / 2f, bodyScale * (1.5f - 0.3f * progress), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null,
                (MushroomSporeBoltProj.SporeBright with { A = 0 }) * ((0.25f + 0.55f * progress) * fade * pulse),
                0f, glow.Size() / 2f, 0.35f + 0.3f * progress, SpriteEffects.None, 0);
            return false;
        }
    }
}
