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
    /// 星穹裂隙：星尘龙的头锥凿穿现实撕开的一道口子。
    /// 三相 = 撕开 8 帧（斜向裂口自中点向两端扩张，无伤害）/ 星涌 26 帧
    /// （伤害窗，每约 9 帧一段星涌脉冲，裂口喷洒星屑坠光）/ 弥合 12 帧
    /// （裂口收拢，星尘余晖驻留，无伤害）。
    /// 材质：星穹裂口（虚空暗芯 + 星辉裂缘 + 星屑坠光）
    /// </summary>
    internal class GsStardustDragonRiftProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color StarCyan = new(110, 220, 255);
        private static readonly Color StarPale = new(224, 248, 255);
        private static readonly Color VoidInk = new(24, 20, 52);

        private const int TearFrames = 8;
        private const int GushFrames = 26;
        private const int SealFrames = 12;
        private const int TotalFrames = TearFrames + GushFrames + SealFrames;
        /// <summary>裂口半长</summary>
        private const float RiftHalf = 58f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Gushing => Elapsed >= TearFrames && Elapsed < TearFrames + GushFrames;

        private bool Sealing => Elapsed >= TearFrames + GushFrames;

        private float Seed => Projectile.identity * 0.7789f % MathHelper.TwoPi;

        /// <summary>裂口固定斜角（identity 定相，撕开即定不再变）</summary>
        private float RiftAngle => Seed * 0.35f - 0.55f;

        /// <summary>裂口开度 0~1</summary>
        private float OpenT {
            get {
                if (Elapsed < TearFrames) {
                    float t = Elapsed / (float)TearFrames;
                    return t * t;
                }
                if (Sealing) {
                    return MathHelper.Clamp(Projectile.timeLeft / (float)SealFrames, 0f, 1f);
                }
                return 1f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //星涌 26 帧内约 3 段脉冲
            Projectile.localNPCHitCooldown = 9;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center,
                StarCyan.ToVector3() * (Gushing ? 0.55f : 0.25f));
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = -0.4f },
                    Projectile.Center);
            }
            if (Elapsed == TearFrames) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.2f },
                    Projectile.Center);
            }
            //星涌相：裂口喷洒星屑坠光（重力星尘，往裂口下方洒）
            if (Gushing) {
                if (Main.rand.NextBool(2)) {
                    Vector2 along = RiftAngle.ToRotationVector2()
                        * Main.rand.NextFloat(-RiftHalf * 0.8f, RiftHalf * 0.8f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + along,
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f),
                            Main.rand.NextFloat(1.5f, 3.5f)),
                        Main.rand.NextBool() ? StarCyan : StarPale,
                        Main.rand.NextFloat(0.22f, 0.38f))?.Configure(true, Main.rand.Next(16, 26));
                }
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(40f, 40f),
                        new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                        StarCyan, Main.rand.NextFloat(0.09f, 0.15f))?.Configure(16, 0.8f);
                }
            }
            //弥合相：星尘余晖上飘驻留
            else if (Sealing && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(36f, 26f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                    StarPale, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(20, 0.7f);
            }
        }

        /// <summary>只有星涌相结算伤害</summary>
        public override bool? CanDamage() => Gushing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(130f, 130f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.8f, 4f),
                    StarCyan, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (soft == null || glow == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float open = OpenT;
            if (open <= 0.02f) {
                return false;
            }
            float ang = RiftAngle;
            float len = RiftHalf * 2f * MathHelper.Clamp(open * 1.15f, 0f, 1f);
            //裂口宽随开度呼吸
            float gape = (Gushing ? 15f : 9f) * open
                * (1f + 0.1f * (float)Math.Sin(Elapsed * 0.5f + Seed));

            //虚空暗芯（真 alpha 压暗，透出「里面不是这个世界」）
            Main.EntitySpriteDraw(soft, pos, null, VoidInk * (0.85f * open), ang,
                soft.Size() / 2f, new Vector2(len * 0.92f / soft.Width, gape / soft.Height),
                SpriteEffects.None, 0);
            //星辉裂缘：上下两条亮边（加色，两端由贴图软边收口）
            Vector2 rim = (ang + MathHelper.PiOver2).ToRotationVector2() * (gape * 0.42f);
            for (int s = -1; s <= 1; s += 2) {
                Main.EntitySpriteDraw(soft, pos + rim * s, null,
                    (StarCyan with { A = 0 }) * (0.85f * open), ang, soft.Size() / 2f,
                    new Vector2(len / soft.Width, 3.4f / soft.Height), SpriteEffects.None, 0);
            }
            //裂芯白热线（星涌期最亮）
            Main.EntitySpriteDraw(soft, pos, null,
                (StarPale with { A = 0 }) * ((Gushing ? 0.75f : 0.4f) * open), ang,
                soft.Size() / 2f, new Vector2(len * 0.8f / soft.Width, 1.8f / soft.Height),
                SpriteEffects.None, 0);
            //两端星芒钉（裂口端点收口）
            for (int s = -1; s <= 1; s += 2) {
                Vector2 tip = pos + ang.ToRotationVector2() * (len * 0.5f * s);
                Main.EntitySpriteDraw(star, tip, null,
                    (StarPale with { A = 0 }) * (0.7f * open), Seed + Elapsed * 0.04f * s,
                    star.Size() / 2f, 0.08f * open, SpriteEffects.None, 0);
            }
            //星涌辉光（加色底光，脉冲节拍呼吸）
            float gushPulse = Gushing
                ? 0.5f + 0.3f * (float)Math.Sin(Elapsed * 0.7f + Seed) : 0.28f;
            Main.EntitySpriteDraw(glow, pos, null,
                (StarCyan with { A = 0 }) * (gushPulse * open), 0f, glow.Size() / 2f,
                new Vector2(1.3f, 0.9f) * open, SpriteEffects.None, 0);
            return false;
        }
    }
}
