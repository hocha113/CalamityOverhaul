using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 虹彩终幕：棱镜剑群的处决仪式。四相 = 展扇 10 帧（六柄虚像剑在目标上方
    /// 扇形列阵，虹彩渐显，无伤害）/ 连刺 24 帧（每 4 帧一柄剑序贯贯穿，
    /// 伤害窗，剑走剑消）/ 碎光 8 帧（棱镜碎片炸散，无伤害）/ 余彩 10 帧（虹彩光尘驻留）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验。
    /// 材质：微光棱镜光刃（hue 沿剑序流转，绘制以 identity 定相禁随机）
    /// </summary>
    internal class GsEmpressFinaleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int FanFrames = 10;
        private const int PlungeFrames = 24;
        private const int ShatterFrames = 8;
        private const int AfterFrames = 10;
        private const int TotalFrames = FanFrames + PlungeFrames + ShatterFrames + AfterFrames;
        private const int BladeCount = 6;
        /// <summary>每柄剑的贯穿间隔</summary>
        private const int PlungeGap = PlungeFrames / BladeCount;
        /// <summary>扇阵悬高</summary>
        private const float FanHeight = 96f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Fanning => Elapsed < FanFrames;

        private bool Plunging => Elapsed >= FanFrames && Elapsed < FanFrames + PlungeFrames;

        private bool Shattering => Elapsed >= FanFrames + PlungeFrames
            && Elapsed < FanFrames + PlungeFrames + ShatterFrames;

        private float Seed => Projectile.identity * 0.8563f % MathHelper.TwoPi;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        /// <summary>剑序虹彩：hue 沿剑序均分色环（identity 定相）</summary>
        private Color BladeHue(int index, float lum = 0.62f)
            => Main.hslToRgb((Seed / MathHelper.TwoPi + index / (float)BladeCount) % 1f, 1f, lum);

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //连刺节拍：一剑一段
            Projectile.localNPCHitCooldown = PlungeGap;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //展扇期咬住目标，连刺起锚定
            if (Fanning) {
                NPC target = BoundTarget;
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center,
                BladeHue(Elapsed / PlungeGap % BladeCount).ToVector3() * 0.4f);
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item162 with { Volume = 0.55f, Pitch = 0.2f },
                    Projectile.Center);
            }
            //连刺节拍音：每柄剑落下时一声棱鸣
            if (Plunging && (Elapsed - FanFrames) % PlungeGap == 0) {
                int idx = (Elapsed - FanFrames) / PlungeGap;
                SoundEngine.PlaySound(SoundID.Item163 with {
                    Volume = 0.4f, Pitch = -0.3f + idx * 0.12f
                }, Projectile.Center);
            }
            //碎光首帧：棱镜炸裂
            if (Elapsed == FanFrames + PlungeFrames) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.1f },
                    Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    float ang = Seed + i / 12f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5.5f),
                        BladeHue(i % BladeCount, 0.7f),
                        Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            //余彩相：虹彩光尘缓浮
            if (Elapsed > FanFrames + PlungeFrames + ShatterFrames && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(34f, 40f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    BladeHue(Main.rand.Next(BladeCount), 0.7f),
                    Main.rand.NextFloat(0.08f, 0.13f))?.Configure(16, 0.7f);
            }
        }

        /// <summary>只有连刺相结算伤害</summary>
        public override bool? CanDamage() => Plunging ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(92f, 124f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            int idx = Math.Clamp((Elapsed - FanFrames) / PlungeGap, 0, BladeCount - 1);
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(1f, 3f)),
                    BladeHue(idx, 0.72f), Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (soft == null || glow == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fanT = MathHelper.Clamp(Elapsed / (float)FanFrames, 0f, 1f);
            int plungedCount = Plunging ? (Elapsed - FanFrames) / PlungeGap
                : Elapsed >= FanFrames + PlungeFrames ? BladeCount : 0;

            //扇阵与待发剑：未落下的剑悬在上方弧线（虹彩渐显 + 悬浮呼吸）
            for (int i = 0; i < BladeCount; i++) {
                if (i < plungedCount) {
                    continue;
                }
                float spread = (i - (BladeCount - 1) * 0.5f) * 0.42f;
                Vector2 fanPos = pos + new Vector2(
                    MathF.Sin(spread) * 74f,
                    -FanHeight * fanT - MathF.Cos(spread) * 12f
                        + 3f * MathF.Sin(Elapsed * 0.25f + Seed + i));
                float bladeRot = spread * 0.5f + MathHelper.PiOver2;
                Color hue = BladeHue(i);
                //剑体 = 长条光刃 + 尖端星闪
                Main.EntitySpriteDraw(soft, fanPos, null,
                    (hue with { A = 0 }) * (0.75f * fanT), bladeRot,
                    soft.Size() / 2f, new Vector2(30f / soft.Width, 5f / soft.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(soft, fanPos, null,
                    (Color.White with { A = 0 }) * (0.5f * fanT), bladeRot,
                    soft.Size() / 2f, new Vector2(20f / soft.Width, 2f / soft.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, fanPos + (bladeRot + MathHelper.Pi).ToRotationVector2() * 15f,
                    null, (hue with { A = 0 }) * (0.7f * fanT),
                    Seed + i, star.Size() / 2f, 0.14f, SpriteEffects.None, 0);
            }

            //本帧正在贯穿的剑：从扇位拉到目标下方的贯穿光痕
            if (Plunging) {
                int idx = (Elapsed - FanFrames) / PlungeGap;
                float within = (Elapsed - FanFrames) % PlungeGap / (float)PlungeGap;
                float spread = (idx - (BladeCount - 1) * 0.5f) * 0.42f;
                Vector2 from = pos + new Vector2(MathF.Sin(spread) * 74f, -FanHeight);
                Vector2 to = pos + new Vector2(-MathF.Sin(spread) * 26f, 66f);
                Vector2 mid = Vector2.Lerp(from, to, within);
                Color hue = BladeHue(idx, 0.7f);
                float pierceRot = (to - from).ToRotation();
                //贯穿光痕（拉长的剑影 + 白芯）
                Main.EntitySpriteDraw(soft, mid, null, (hue with { A = 0 }) * 0.9f, pierceRot,
                    soft.Size() / 2f, new Vector2(72f / soft.Width, 5.5f / soft.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(soft, mid, null, (Color.White with { A = 0 }) * 0.7f,
                    pierceRot, soft.Size() / 2f,
                    new Vector2(48f / soft.Width, 2.2f / soft.Height), SpriteEffects.None, 0);
                //出点星闪
                Main.EntitySpriteDraw(star, to, null, (hue with { A = 0 }) * (0.8f * within),
                    Seed - idx, star.Size() / 2f, 0.18f * within, SpriteEffects.None, 0);
            }

            //碎光相：棱镜环闪
            if (Shattering) {
                float t = (Elapsed - FanFrames - PlungeFrames) / (float)ShatterFrames;
                Main.EntitySpriteDraw(glow, pos, null,
                    (Color.White with { A = 0 }) * (0.7f * (1f - t)), 0f,
                    glow.Size() / 2f, 0.6f + 1.1f * t, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
