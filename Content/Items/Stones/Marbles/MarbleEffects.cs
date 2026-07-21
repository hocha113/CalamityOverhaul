using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>
    /// 大理石共享投射物。<br/>
    /// Shockwave ai[0]=计时(传0)，ai[1]=最大半径px(≤0→120)；全圆命中一次，寿命24tick；音效由生成方播<br/>
    /// Shard 无ai；重力翻滚，落地弹一次再碎，穿透2，寿命90tick
    /// </summary>
    internal class MarbleShockwave : ModProjectile, IAdditiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int Life = 24;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private float MaxRadius => Projectile.ai[1] <= 0f ? 120f : Projectile.ai[1];
        private float Progress => MathHelper.Clamp((Life - Projectile.timeLeft) / (float)Life, 0f, 1f);
        private float Radius => MathHelper.SmoothStep(8f, MaxRadius, Progress);

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0]++;

            if (Projectile.ai[0] == 1f && !VaultUtils.isServer) {
                int n = (int)MathHelper.Clamp(MaxRadius / 16f, 6f, 14f);
                for (int i = 0; i < n; i++) {
                    float lane = Main.rand.NextFloat(-1f, 1f);
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + new Vector2(lane * MaxRadius * 0.3f, 0f)
                        , new Vector2(lane * 3.2f, Main.rand.NextFloat(-2.6f, -0.8f))
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(26, 0.7f, 0.05f);
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                        , new Vector2(lane * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(2.5f, 6f))
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.5f, 0.85f))
                        .Configure(Main.rand.Next(22, 34));
                }
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * (1f - Progress) * 0.8f);
        }

        //全圆判定，localNPCHitCooldown=-1 每目标一次
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f)
                    , new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f))
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(18, 28));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D spoke = CWRAsset.Line.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float fade = 1f - Progress;
            float scale = Radius / (ring.Width * 0.5f);

            Color gold = GraniteMarbleVFX.MarbleGold; gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore; core.A = 0;

            spriteBatch.Draw(ring, pos, null, gold * fade * 0.85f, Projectile.rotation, ring.Size() / 2f, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, pos, null, core * fade * 0.6f, Projectile.rotation, ring.Size() / 2f, scale * 0.8f, SpriteEffects.None, 0f);

            float flash = MathF.Max(0f, 1f - Progress * 3.2f);
            if (flash > 0f) {
                spriteBatch.Draw(glow, pos, null, core * flash * 0.9f, 0f, glow.Size() / 2f, MaxRadius / 90f * (0.6f + Progress * 2f), SpriteEffects.None, 0f);
            }

            //径向裂纹，whoAmI 播种
            const int spokes = 7;
            float seed = Projectile.whoAmI * 2.3999f;
            Vector2 spokeOrigin = spoke.Size() / 2f;
            for (int i = 0; i < spokes; i++) {
                float ang = seed + MathHelper.TwoPi * i / spokes + (i % 2) * 0.21f;
                float len = Radius * (0.74f + 0.22f * MathF.Sin(seed * 3f + i * 2.4f));
                Vector2 dir = ang.ToRotationVector2();
                float reach = len / spoke.Height;
                spriteBatch.Draw(spoke, pos + dir * len * 0.5f, null, gold * fade * 0.75f
                    , ang + MathHelper.PiOver2, spokeOrigin, new Vector2(0.085f + 0.05f * fade, reach), SpriteEffects.None, 0f);
                spriteBatch.Draw(spoke, pos + dir * len * 0.5f, null, core * fade * 0.5f
                    , ang + MathHelper.PiOver2, spokeOrigin, new Vector2(0.04f, reach * 0.9f), SpriteEffects.None, 0f);
            }
        }

        bool IWarpDrawable.CanDrawCustom() => false;
        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }
        void IWarpDrawable.Warp() {
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float scale = Radius / (ring.Width * 0.5f);
            Color warp = new Color(50, 50, 50) * (1f - Progress) * 0.7f;
            Main.spriteBatch.Draw(ring, Projectile.Center - Main.screenPosition, null, warp, Projectile.rotation
                , ring.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>石屑，落地弹一次后碎</summary>
    internal class MarbleShard : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.32f;
            Projectile.velocity.X *= 0.99f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X);
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.35f);

            if (Main.rand.NextBool(4) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Projectile.velocity * 0.1f
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.25f, 0.45f)).Configure(22, 0.6f, 0.04f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                    , -Projectile.velocity.UnitVector().RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 4.5f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(18, 28));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.5f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.5f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.5f) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.45f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = Main.rand.NextFloat(0.2f, 0.45f) }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Vector2.Zero
                        , GraniteMarbleVFX.MarbleDust, 0.5f).Configure(24, 0.7f, 0.05f);
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                            , new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1.5f, 3.5f))
                            , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.6f))
                            .Configure(Main.rand.Next(16, 26));
                    }
                }
                return false;
            }
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.3f, 0.5f)).Configure(20, 0.7f, 0.05f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.75f))
                    .Configure(Main.rand.Next(20, 30));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D sliver = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = sliver.Size() / 2f;
            Color gold = GraniteMarbleVFX.MarbleGold; gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore; core.A = 0;

            spriteBatch.Draw(glow, pos, null, gold * 0.45f, 0f, glow.Size() / 2f, 0.32f, SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, gold * 0.9f, Projectile.rotation, origin, new Vector2(0.30f, 0.62f), SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, gold * 0.7f, Projectile.rotation + 1.1f, origin, new Vector2(0.2f, 0.4f), SpriteEffects.None, 0f);
            spriteBatch.Draw(sliver, pos, null, core * 0.85f, Projectile.rotation, origin, new Vector2(0.15f, 0.5f), SpriteEffects.None, 0f);
        }
    }
}
