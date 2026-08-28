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
    /// 交叉视线：双瞳协议的结算体。机械双瞳的凝视在目标身上交汇成 X 形爆闪，
    /// 三相 = 汇聚 8 帧（红绿两道视线从两肩方向收束，无伤害）/ 爆闪 6 帧（伤害窗，
    /// X 形白热十字 + 咒焰引燃）/ 余像 14 帧（十字残像褪色，咒绿烬粒上飘）。
    /// 材质：机械瞳孔的相干视线（红激光 + 咒焰绿），非无名发光体
    /// </summary>
    internal class GsOpticCrossrayProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        //红瞳激光 / 咒焰绿 / 白热芯
        private static readonly Color RetRed = new(255, 84, 74);
        private static readonly Color SpazGreen = new(128, 255, 96);
        private static readonly Color HotCore = new(255, 244, 224);

        private const int GatherFrames = 8;
        private const int FlashFrames = 6;
        private const int FadeFrames = 14;
        private const int TotalFrames = GatherFrames + FlashFrames + FadeFrames;
        /// <summary>爆闪判定半径</summary>
        private const float BlastRadius = 62f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InFlash => Elapsed >= GatherFrames && Elapsed < GatherFrames + FlashFrames;

        private bool Fading => Elapsed >= GatherFrames + FlashFrames;

        private float Seed => Projectile.identity * 0.6173f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //爆闪窗内每目标只结算一次
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center,
                Color.Lerp(RetRed, SpazGreen, 0.5f).ToVector3() * (InFlash ? 0.7f : 0.3f));
            //汇聚相首帧：双瞳锁定音
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.45f, Pitch = 0.35f },
                    Projectile.Center);
            }
            //爆闪首帧：十字炸裂音 + 双色火花对喷
            if (Elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.1f },
                    Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    bool red = i % 2 == 0;
                    float ang = Seed + MathHelper.PiOver4 + i / 10f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6f),
                        red ? RetRed : SpazGreen,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            //余像相：咒绿烬粒上飘
            if (Fading && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    SpazGreen, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(16, 0.7f);
            }
        }

        /// <summary>只有爆闪窗结算伤害</summary>
        public override bool? CanDamage() => InFlash ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(BlastRadius * 2f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //幽花之瞳的咒焰引燃（双瞳里的绿瞳职责）
            target.AddBuff(BuffID.CursedInferno, 150);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                    i % 2 == 0 ? RetRed : SpazGreen,
                    Main.rand.NextFloat(0.26f, 0.4f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (soft == null || glow == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //X 形双臂角：identity 定相的斜十字
            float armAngA = Seed * 0.1f + MathHelper.PiOver4;
            float armAngB = armAngA + MathHelper.PiOver2;

            if (Elapsed < GatherFrames) {
                //汇聚相：红绿两道视线从远端收束进中心（长度收缩 = 收口）
                float t = Elapsed / (float)GatherFrames;
                float reach = MathHelper.Lerp(150f, 22f, t * t);
                float alpha = 0.25f + 0.55f * t;
                DrawSightLine(soft, pos, armAngA, reach, RetRed * alpha);
                DrawSightLine(soft, pos, armAngB + MathHelper.Pi, reach, SpazGreen * alpha);
                Main.EntitySpriteDraw(glow, pos, null,
                    (HotCore with { A = 0 }) * (0.3f * t), 0f, glow.Size() / 2f,
                    0.35f * t, SpriteEffects.None, 0);
                return false;
            }

            float fade = Fading
                ? MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f) : 1f;
            float flashBoost = InFlash ? 1f : 0.55f;
            //X 十字四臂（红绿各占一对角，爆闪时白热盖顶）
            float armLen = InFlash
                ? MathHelper.Lerp(70f, 118f, (Elapsed - GatherFrames) / (float)FlashFrames)
                : 118f;
            DrawCrossArm(soft, pos, armAngA, armLen, RetRed * (0.85f * fade * flashBoost));
            DrawCrossArm(soft, pos, armAngB, armLen, SpazGreen * (0.85f * fade * flashBoost));
            if (InFlash) {
                DrawCrossArm(soft, pos, armAngA, armLen * 0.6f, (HotCore with { A = 0 }) * 0.8f);
                DrawCrossArm(soft, pos, armAngB, armLen * 0.6f, (HotCore with { A = 0 }) * 0.8f);
            }
            //中心星芒 + 底光
            float corePulse = 1f + 0.15f * (float)Math.Sin(Elapsed * 0.8f + Seed);
            Main.EntitySpriteDraw(flare, pos, null,
                (HotCore with { A = 0 }) * (0.85f * fade * flashBoost), Seed + Elapsed * 0.02f,
                flare.Size() / 2f, 0.34f * corePulse * fade, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null,
                (Color.Lerp(RetRed, SpazGreen, 0.5f) with { A = 0 }) * (0.4f * fade), 0f,
                glow.Size() / 2f, 1.1f * fade, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>汇聚相视线：外端向中心奔来的细光条（origin 设在外端实现收束）</summary>
        private void DrawSightLine(Texture2D soft, Vector2 center, float ang, float reach, Color color) {
            Vector2 outer = center + ang.ToRotationVector2() * reach;
            Vector2 scale = new(reach / soft.Width, 5f / soft.Height);
            Main.EntitySpriteDraw(soft, outer, null, color, ang + MathHelper.Pi,
                new Vector2(0f, soft.Height / 2f), scale, SpriteEffects.None, 0);
        }

        /// <summary>X 臂：过中心的双向光条，端点淡出由贴图自身软边完成</summary>
        private void DrawCrossArm(Texture2D soft, Vector2 center, float ang, float len, Color color) {
            Vector2 scale = new(len * 2f / soft.Width, 7f / soft.Height);
            Main.EntitySpriteDraw(soft, center, null, color, ang,
                soft.Size() / 2f, scale, SpriteEffects.None, 0);
        }
    }
}
