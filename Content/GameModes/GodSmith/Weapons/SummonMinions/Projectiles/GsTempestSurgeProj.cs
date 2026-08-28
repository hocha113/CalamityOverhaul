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
    /// 合流潮涌：龙卷与鲨的杀意合成一面横扫的浪墙。
    /// 三相 = 隆起 8 帧（浪体从水脊隆起，无伤害）/ 横扫 32 帧（伤害窗，
    /// 浪冠卷曲、冠顶溅沫、浪后拖雾）/ 消散 10 帧（浪体塌落成雾，无伤害）。
    /// 浪头 = 冠弧收口，浪尾 = 雾带收口，不做平切贴条。材质：深海涌浪 + 白沫浪冠
    /// </summary>
    internal class GsTempestSurgeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color SeaDeep = new(28, 66, 140);
        private static readonly Color SeaBody = new(64, 140, 220);
        private static readonly Color FoamWhite = new(230, 246, 255);

        private const int SwellFrames = 8;
        private const int SweepFrames = 32;
        private const int FadeFrames = 10;
        private const int TotalFrames = SwellFrames + SweepFrames + FadeFrames;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Sweeping => Elapsed >= SwellFrames && Elapsed < SwellFrames + SweepFrames;

        private bool Fading => Elapsed >= SwellFrames + SweepFrames;

        private float Seed => Projectile.identity * 0.6329f % MathHelper.TwoPi;

        /// <summary>浪高进度：隆起段升起，消散段塌落</summary>
        private float HeightT {
            get {
                if (Elapsed < SwellFrames) {
                    float t = Elapsed / (float)SwellFrames;
                    return t * t;
                }
                if (Fading) {
                    return MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
                }
                return 1f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 110;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //横扫全程每目标至多两段
            Projectile.localNPCHitCooldown = 25;
        }

        public override void AI() {
            //隆起期原地蓄浪（抵消位移但保住横扫速度），消散期滞停衰减
            if (Elapsed < SwellFrames) {
                Projectile.position -= Projectile.velocity;
            }
            else if (Fading) {
                Projectile.velocity *= 0.82f;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, SeaBody.ToVector3() * 0.3f);
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, Pitch = -0.3f },
                    Projectile.Center);
            }
            if (Elapsed == SwellFrames) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = -0.4f },
                    Projectile.Center);
            }
            //横扫相：冠顶溅沫 + 浪后拖雾
            if (Sweeping) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + new Vector2(
                            Main.rand.NextFloat(-14f, 24f) * Math.Sign(Projectile.velocity.X),
                            -52f * HeightT),
                        new Vector2(Projectile.velocity.X * 0.4f, -Main.rand.NextFloat(1.5f, 3.4f)),
                        FoamWhite, Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(12, 20));
                }
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + new Vector2(
                            -Math.Sign(Projectile.velocity.X) * Main.rand.NextFloat(20f, 40f),
                            Main.rand.NextFloat(-30f, 30f)),
                        Vector2.Zero, SeaBody, 0.14f)?.Configure(14, 0.5f);
                }
            }
            //消散相：塌浪成雾
            else if (Fading && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 40f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    FoamWhite, Main.rand.NextFloat(0.1f, 0.16f))?.Configure(16, 0.5f);
            }
        }

        /// <summary>只有横扫相结算伤害</summary>
        public override bool? CanDamage() => Sweeping ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = 104f * HeightT;
            Rectangle wall = new((int)(Projectile.Center.X - 32f),
                (int)(Projectile.Center.Y + 54f - h), 64, (int)h);
            return wall.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Projectile.velocity.X * 0.5f, 0f)
                        + Main.rand.NextVector2Circular(2.5f, 2.5f),
                    i % 2 == 0 ? FoamWhite : SeaBody,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float h = HeightT;
            if (h <= 0.02f) {
                return false;
            }
            int dir = Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
            Vector2 basePos = Projectile.Center + new Vector2(0f, 54f) - Main.screenPosition;
            float lean = dir * 0.22f;
            float wob = 0.04f * (float)Math.Sin(Elapsed * 0.4f + Seed);

            //浪体三层：深海垫底 → 涌浪主体 → 迎面亮壁（自下而上、前倾）
            Main.EntitySpriteDraw(soft, basePos - new Vector2(0f, 42f * h), null,
                SeaDeep * (0.8f * h), lean + wob, soft.Size() / 2f,
                new Vector2(56f / soft.Width, 96f * h / soft.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, basePos - new Vector2(dir * 6f, 48f * h), null,
                SeaBody * (0.75f * h), lean * 1.3f + wob, soft.Size() / 2f,
                new Vector2(40f / soft.Width, 86f * h / soft.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, basePos - new Vector2(dir * 12f, 40f * h), null,
                (SeaBody with { A = 0 }) * (0.4f * h), lean * 1.5f + wob, soft.Size() / 2f,
                new Vector2(20f / soft.Width, 70f * h / soft.Height), SpriteEffects.None, 0);
            //浪冠：冠顶卷弧（向行进方向翻卷收口）+ 冠沫亮线
            Vector2 crest = basePos - new Vector2(-dir * 4f, 96f * h);
            Main.EntitySpriteDraw(soft, crest, null, FoamWhite * (0.85f * h),
                lean + dir * (0.9f + wob * 2f), soft.Size() / 2f,
                new Vector2(34f / soft.Width, 7f / soft.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, crest + new Vector2(dir * 12f, 6f), null,
                (FoamWhite with { A = 0 }) * (0.7f * h), lean + dir * 1.35f, soft.Size() / 2f,
                new Vector2(18f / soft.Width, 4f / soft.Height), SpriteEffects.None, 0);
            //浪尾雾带（行进反侧，真 alpha 淡层收口）
            Main.EntitySpriteDraw(soft, basePos - new Vector2(dir * 34f, 30f * h), null,
                SeaDeep * (0.35f * h), lean * 0.6f, soft.Size() / 2f,
                new Vector2(30f / soft.Width, 54f * h / soft.Height), SpriteEffects.None, 0);
            //水辉底光
            Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, 40f * h), null,
                (SeaBody with { A = 0 }) * (0.35f * h), 0f, glow.Size() / 2f,
                new Vector2(0.9f, 1.3f * h), SpriteEffects.None, 0);
            return false;
        }
    }
}
