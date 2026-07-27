using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>精密瞄具，零散布，连中 15 层射裁决射线必暴贯线，脱靶清零</summary>
    internal sealed class PrecisionOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //精密狙击红
        public override Color TintColor => new(255, 90, 90);

        private const int CalibrationCap = 15;
        private int calibration;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1f;
            ctx.CritAdd += 4;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            calibration++;
            if (calibration < CalibrationCap) {
                //每两层一声校准滴答
                if (calibration % 2 == 0 && Main.netMode != NetmodeID.Server) {
                    float pitch = MathHelper.Lerp(-0.2f, 0.8f, calibration / (float)CalibrationCap);
                    SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.22f, Pitch = pitch }, target.Center);
                }
                return;
            }

            calibration = 0;
            FireJudgmentRay(beam.Projectile, target);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            //主束未命中消亡 = 脱靶清零
            if (beam.IsDerived || beam.Projectile.numHits > 0) return;
            if (calibration > 0 && Main.netMode != NetmodeID.Server && beam.Projectile.owner == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.25f, Pitch = -0.7f }, beam.Projectile.Center);
            }
            calibration = 0;
        }

        private static void FireJudgmentRay(Projectile source, NPC throughTarget) {
            if (source.owner != Main.myPlayer) return;
            Player owner = Main.player[source.owner];
            if (owner == null || !owner.active) return;

            Vector2 dir = (throughTarget.Center - owner.Center).SafeNormalize(Vector2.UnitX);
            int dmg = Math.Max(source.damage * 5, 1);
            Projectile.NewProjectile(source.GetSource_FromThis(),
                owner.Center + dir * 30f, dir,
                ModContent.ProjectileType<SHPCJudgmentRayProj>(),
                dmg, 4f, source.owner);
        }
    }

    /// <summary>裁决射线，前 8 帧必暴贯线；SHPCJudgmentRay.fx</summary>
    internal sealed class SHPCJudgmentRayProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 30;
        private const int DamageWindow = 8;
        private const float MaxLength = 1500f;
        private const float HitWidth = 26f;

        private static readonly Color RayCore = new(255, 240, 235);
        private static readonly Color RayEdge = new(255, 45, 60);

        private Vector2 rayDir;
        private float rayLength;
        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一线一结算
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                rayDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Zero;
                ResolveLength();
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.8f, Pitch = 0.55f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);
                    SpawnMuzzleBurst();
                }
                SHPCNaturalFx.Shake(3.5f);
            }

            int age = Lifetime - Projectile.timeLeft;
            fadeAlpha = 1f - age / (float)Lifetime;
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Projectile.Center + rayDir * (rayLength * i / 3f),
                    RayEdge.ToVector3() * 0.6f * fadeAlpha);
            }
        }

        /// <summary>32px 步进探墙定长度</summary>
        private void ResolveLength() {
            rayLength = 120f;
            while (rayLength < MaxLength) {
                Vector2 probe = Projectile.Center + rayDir * (rayLength + 32f);
                if (!Collision.CanHitLine(Projectile.Center, 1, 1, probe, 1, 1)) break;
                rayLength += 32f;
            }
        }

        private void SpawnMuzzleBurst() {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = rayDir.RotatedBy(Main.rand.NextFloat(-0.45f, 0.45f)) * Main.rand.NextFloat(3f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + rayDir * 20f, vel,
                    RayEdge, Main.rand.NextFloat(0.6f, 1.2f)).Configure(true, Main.rand.Next(10, 20));
            }
            //终点耀斑
            Vector2 endPos = Projectile.Center + rayDir * rayLength;
            PRTLoader.NewParticle<PRT_StarPulseRing>(endPos, Vector2.Zero, RayEdge with { A = 0 }, 0.05f).Configure(0.05f, 0.5f, 20);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(endPos, Main.rand.NextVector2CircularEdge(5f, 5f),
                    RayCore, Main.rand.NextFloat(0.5f, 1.0f)).Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override bool? CanDamage() => Lifetime - Projectile.timeLeft <= DamageWindow;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                Projectile.Center, Projectile.Center + rayDir * rayLength, HitWidth, ref _);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //裁决必暴
            modifiers.SetCrit();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                    Main.rand.NextVector2CircularEdge(5f, 5f),
                    RayCore, Main.rand.NextFloat(0.7f, 1.3f)).Configure(RayEdge, Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCJudgmentRay?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            int age = Lifetime - Projectile.timeLeft;
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["lifeProgress"]?.SetValue(MathHelper.Clamp(age / (float)Lifetime, 0f, 1f));
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["rayLength"]?.SetValue(rayLength);
            shader.Parameters["coreColor"]?.SetValue(RayCore.ToVector3());
            shader.Parameters["edgeColor"]?.SetValue(RayEdge.ToVector3());

            //quad 沿射线拉伸，origin 左边中点 = coords.x
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                rayDir.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(rayLength, 64f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) return;
            //枪口十字耀星
            Vector2 muzzleScreen = Projectile.Center - Main.screenPosition;
            float flash = MathF.Pow(fadeAlpha, 1.6f);
            spriteBatch.Draw(star, muzzleScreen, null, RayCore * flash * 0.9f,
                (float)Main.timeForVisualEffects * 0.04f, star.Size() * 0.5f, 0.16f * flash, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, muzzleScreen, null, RayEdge * flash * 0.6f,
                -(float)Main.timeForVisualEffects * 0.03f, star.Size() * 0.5f, 0.27f * flash, SpriteEffects.None, 0f);
        }
    }
}
