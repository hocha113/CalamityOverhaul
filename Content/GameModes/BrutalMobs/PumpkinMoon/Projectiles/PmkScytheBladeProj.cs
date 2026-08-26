using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 轮盘镰刃（南瓜王签名技单元）：ai[0]=锚定南瓜王索引 ai[1]=档位×100+槽位 ai[2]=轮盘基准角。
    /// 基准角由权威端一次掷定随生成包同步，全体镰刃共享基准并匀速公转（几何由同步量确定性推得）。
    /// 缺口=WheelGapSlots 个连续槽位从不生成（物理缺口），预告期由首个实槽位在缺口方位绘制
    /// 安全辉光（亮出缺口方位）；轮径固定，圈内圈外恒安全。判定窗=点燃可见窗；
    /// 锚死亡即全轮溃散。机制零速度注入，与 boss 旗标无关（无补偿项）
    /// </summary>
    internal class PmkScytheBladeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingScythe;

        internal const int WheelSlots = 12;
        /// <summary>具名缺口：生成循环跳过的连续槽位数（90° 安全扇区）</summary>
        internal const int WheelGapSlots = 3;
        /// <summary>轮盘半径（固定，圈内圈外恒安全的具名边界）</summary>
        private const float WheelRadius = 212f;
        /// <summary>公转角速度（弧度/帧，档位只调转速）</summary>
        private static readonly float[] SpinByTier = [0.016f, 0.019f, 0.022f];
        /// <summary>就位预告帧（小 Boss 契约 ≥40）</summary>
        private const int TelegraphFrames = 48;
        private const int LitFrames = 300;
        private const int FadeFrames = 22;
        /// <summary>整轮完整时长（NPC 侧 busy 计时用）</summary>
        internal const int TotalFrames = TelegraphFrames + LitFrames + FadeFrames;

        private static readonly Color ScytheFlame = new Color(255, 150, 40);
        private static readonly Color GapSafe = new Color(255, 236, 170, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int Tier => (int)MathHelper.Clamp((int)Projectile.ai[1] / 100, 1, 3);
        private int Slot => (int)Projectile.ai[1] % 100;
        private float BaseAngle => Projectile.ai[2];
        private float Spin => SpinByTier[Tier - 1];
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private bool Lit => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + LitFrames;

        private float SlotStep => MathHelper.TwoPi / WheelSlots;

        /// <summary>当前公转角</summary>
        private float OrbitAngle(int backFrames = 0) => BaseAngle + Slot * SlotStep + Spin * (Elapsed - backFrames);

        /// <summary>缺口中心方位（槽位 0..GapSlots-1 的角度中点，随轮盘同速公转）</summary>
        private float GapCenterAngle => BaseAngle + (WheelGapSlots - 1) * 0.5f * SlotStep + Spin * Elapsed;

        /// <summary>就位半径：预告期自内向外撑开</summary>
        private float OrbitRadius {
            get {
                float p = MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);
                return WheelRadius * (0.5f + 0.5f * (1f - (1f - p) * (1f - p) * (1f - p)));
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalFrames;
            }

            //锚定校验（index+type，槽位不是身份）：南瓜王没了轮盘即溃散
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != NPCID.Pumpking) {
                Projectile.Kill();
                return;
            }

            //几何全部由同步量确定性推得（锚中心+基准角+槽位+匀速自旋）
            Projectile.Center = anchor.Center + OrbitAngle().ToRotationVector2() * OrbitRadius;
            //判定窗=点燃可见窗
            Projectile.hostile = Lit;
            //镰刃自旋（纯视觉）
            Projectile.rotation += 0.3f;

            //首个实槽位担任报幕员
            if (Slot == WheelGapSlots && !Main.dedServ) {
                if (Elapsed == 1) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = -0.6f }, anchor.Center);
                }
                else if (Elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 3 }, anchor.Center);
                }
            }

            if (!Main.dedServ) {
                if (Lit && Main.rand.NextBool(4)) {
                    Dust flame = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.Torch, -OrbitAngle().ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Spin * 60f * 0.4f,
                        110, default, 1f);
                    flame.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, ScytheFlame.ToVector3() * (Lit ? 0.4f : 0.18f));
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            //可见度与判定窗同一时间轴
            float vis;
            if (elapsed < TelegraphFrames) {
                vis = 0.3f + 0.55f * (elapsed / (float)TelegraphFrames);
            }
            else if (Lit) {
                vis = 1f;
            }
            else {
                vis = MathHelper.Clamp(1f - (elapsed - TelegraphFrames - LitFrames) / (float)FadeFrames, 0f, 1f);
            }
            if (vis <= 0.01f) {
                return false;
            }

            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives()) {
                return false;
            }
            Vector2 wheelCenter = anchor.Center;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frameCount = Math.Max(1, Main.projFrames[ProjectileID.FlamingScythe]);
            Rectangle frame = tex.Frame(1, frameCount, 0, elapsed / 4 % frameCount);
            Vector2 origin = frame.Size() / 2f;
            //预告期偏冷余烬色、点燃转炽白：状态变化可读
            float litT = Lit ? 1f : 0.45f;
            Color tint = Color.Lerp(Color.Lerp(lightColor, ScytheFlame, 0.4f), Color.White, 0.35f * litT);

            //公转拖影：同贴图后置相位重画（旋转拖尾，横轴粗细=本体量级）
            float radius = OrbitRadius;
            for (int k = 2; k >= 1; k--) {
                Vector2 ghostPos = wheelCenter + OrbitAngle(k * 5).ToRotationVector2() * radius - Main.screenPosition;
                Main.EntitySpriteDraw(tex, ghostPos, frame, tint * (0.4f - 0.14f * k) * vis,
                    Projectile.rotation - k * 0.3f, origin, 0.94f - 0.1f * k, SpriteEffects.None, 0);
            }

            //本体（原版南瓜王镰刃贴图，实体层）
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, frame, tint * vis, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            //预告期：首个实槽位在缺口方位绘制安全辉光（亮出缺口方位）
            if (elapsed < TelegraphFrames && Slot == WheelGapSlots) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float gapAngle = GapCenterAngle;
                Vector2 gapPos = wheelCenter + gapAngle.ToRotationVector2() * radius - Main.screenPosition;
                float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);
                Main.EntitySpriteDraw(glow, gapPos, null, GapSafe * (0.5f * pulse * vis),
                    gapAngle + MathHelper.PiOver2, glow.Size() / 2f, new Vector2(1.9f, 0.8f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 110, default, 1f);
                ember.noGravity = true;
            }
        }
    }
}
