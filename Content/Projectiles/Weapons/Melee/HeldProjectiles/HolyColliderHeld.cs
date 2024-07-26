using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class HolyColliderHeld : BaseSwing, IDrawWarp
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        public override string gradientTexturePath => CWRConstant.Masking + "HolyColliderEffectColorBar";
        int dirs;
        public override void SetSwingProperty() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            distanceToOwner = 30;
            trailTopWidth = 50;
            canDrawSlashTrail = true;
            Length = 140;
        }

        public override void SwingAI() {
            if (Time == 0) {
                dirs = Owner.direction;
                Rotation = MathHelper.ToRadians(-33 * Owner.direction);
                startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                speed = MathHelper.ToRadians(4) / SetSwingSpeed(1);
            }
            if (Time == 10 * updateCount && Projectile.IsOwnedByLocalPlayer()) {
                float lengs = ToMouse.Length();
                if (lengs < Length * Projectile.scale) {
                    lengs = Length * Projectile.scale;
                }
                Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
                Vector2 unitToM = UnitToMouseV;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos, Vector2.Zero
                    , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage / 6, Projectile.knockBack, Owner.whoAmI);
                for (int i = 0; i < lengs / 12; i++) {
                    DRK_LavaFire lavaFire = new DRK_LavaFire();
                    lavaFire.Velocity = ToMouse.UnitVector() * 2;
                    lavaFire.Position = Owner.GetPlayerStabilityCenter() + unitToM * (1 + i) * 12;
                    lavaFire.Scale = Main.rand.NextFloat(0.8f, 1.2f);
                    lavaFire.Color = Color.White;
                    lavaFire.ai[0] = 1;
                    lavaFire.ai[1] = 0;
                    lavaFire.minLifeTime = 22;
                    lavaFire.maxLifeTime = 30;
                    DRKLoader.AddParticle(lavaFire);
                }
            }
            if (Time < 10 * SetSwingSpeed(1)) {
                Length *= 1 + 0.08f / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 + 0.1f / updateCount;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale += 0.012f;
            }
            else {
                Length *= 1 - 0.01f / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 - 0.1f / updateCount / SetSwingSpeed(1);
                vector = startVector.RotatedBy(Rotation) * Length;
            }
            if (Time >= 22 * updateCount * SetSwingSpeed(1)) {
                Projectile.Kill();
            }
            if (Time % updateCount == updateCount - 1) {
                Length = MathHelper.Clamp(Length, 160, 180);
            }
            Owner.direction = dirs;
        }

        //模拟出一个勉强符合物理逻辑的命中粒子效果，最好不要动这些，这个效果是我凑出来的，我也不清楚这具体的数学逻辑，代码太乱了
        private void HitEffect(Entity target, bool theofSteel) {
            if (theofSteel) {
                SoundEngine.PlaySound(MurasamaEcType.InorganicHit with { Pitch = 0.25f }, target.Center);
            }
            else {
                SoundEngine.PlaySound(MurasamaEcType.OrganicHit with { Pitch = 1.15f }, target.Center);
            }

            int sparkCount = 13;
            Vector2 toTarget = Owner.Center.To(target.Center);
            Vector2 norlToTarget = toTarget.GetNormalVector();
            int ownerToTargetSetDir = Math.Sign(toTarget.X);
            if (ownerToTargetSetDir != DirSign) {
                ownerToTargetSetDir = -1;
            }
            else {
                ownerToTargetSetDir = 1;
            }

            if (rotSpeed > 0) {
                norlToTarget *= -1;
            }
            if (rotSpeed < 0) {
                norlToTarget *= 1;
            }

            float rotToTargetSpeedSengs = rotSpeed * 3 * ownerToTargetSetDir;
            Vector2 rotToTargetSpeedTrengsVumVer = norlToTarget.RotatedBy(-rotToTargetSpeedSengs) * 13;
            if (Projectile.ai[0] == 3) {
                rotToTargetSpeedTrengsVumVer = Projectile.velocity.RotatedBy(rotToTargetSpeedSengs);
            }

            int pysCount = DRKLoader.GetParticlesCount(DRKLoader.GetParticleType(typeof(PRK_Spark)));
            if (pysCount > 120) {
                sparkCount = 10;
            }
            if (pysCount > 220) {
                sparkCount = 8;
            }
            if (pysCount > 350) {
                sparkCount = 6;
            }
            if (pysCount > 500) {
                sparkCount = 3;
            }

            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = rotToTargetSpeedTrengsVumVer.RotatedByRandom(0.35f) * Main.rand.NextFloat(0.3f, 1.6f);
                int sparkLifetime2 = Main.rand.Next(18, 30);
                float sparkScale2 = Main.rand.NextFloat(0.65f, 1.2f);
                Color sparkColor2 = Main.rand.NextBool(3) ? Color.OrangeRed : Color.DarkRed;

                if (Projectile.ai[0] == 0 || Projectile.ai[0] == 1) {
                    sparkVelocity2 *= 0.8f;
                    sparkScale2 *= 0.9f;
                    sparkLifetime2 = Main.rand.Next(13, 25);
                }
                else if (Projectile.ai[0] == 3) {
                    sparkVelocity2 *= 1.28f;
                }
                else if (Projectile.ai[0] == 4 || Projectile.ai[0] == 5) {
                    sparkVelocity2 *= 1.28f;
                    sparkScale2 *= 1.19f;
                    sparkLifetime2 = Main.rand.Next(23, 35);
                }

                if (theofSteel) {
                    sparkColor2 = Main.rand.NextBool(3) ? Color.Gold : Color.Goldenrod;
                }

                PRK_Spark spark = new PRK_Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                DRKLoader.AddParticle(spark);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468), Projectile.velocity / 3
                        , ModContent.ProjectileType<HolyColliderHolyFires>(), (int)(Projectile.damage * 0.5f), Projectile.knockBack, Owner.whoAmI);
                    for (int j = 0; j < 3; j++) {
                        Vector2 pos = Owner.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468);
                        Vector2 particleSpeed = pos.To(Owner.Center).UnitVector() * 7;
                        BaseParticle energyLeak = new DRK_HolyColliderLight(pos, particleSpeed
                            , Main.rand.NextFloat(0.5f, 0.7f), Color.Gold, 90, 1, 1.5f, hueShift: 0.0f);
                        DRKLoader.AddParticle(energyLeak);
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect(target, CWRLoad.NPCValue.TheofSteel[target.type]);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect(target, false);
        }

        public override void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "KnifeRendering").Value;

            effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
            effect.Parameters["sampleTexture"].SetValue(TrailTexture);
            effect.Parameters["gradientTexture"].SetValue(GradientTexture);
            //应用shader，并绘制顶点
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }
        }

        bool IDrawWarp.canDraw() => true;

        bool IDrawWarp.noBlueshift() => true;

        void IDrawWarp.Warp() {
            List<CustomVertexInfo> bars = new List<CustomVertexInfo>();
            GetCurrentTrailCount(out float count);

            float w = 1f;
            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f)
                    continue;

                float factor = 1f - i / count;
                Vector2 Center = Owner.GetPlayerStabilityCenter();
                float r = oldRotate[i] % 6.18f;
                float dir = (r >= 3.14f ? r - 3.14f : r + 3.14f) / MathHelper.TwoPi;
                Vector2 Top = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] + trailTopWidth + oldDistanceToOwner[i]);
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) * 1.25f + oldDistanceToOwner[i]);

                bars.Add(new CustomVertexInfo(Top, new Color(dir, w, 0f, 15), new Vector3(factor, 0f, w)));
                bars.Add(new CustomVertexInfo(Bottom, new Color(dir, w, 0f, 15), new Vector3(factor, 1f, w)));
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone);

            Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);
            Matrix model = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)) * Main.GameViewMatrix.TransformationMatrix;
            Effect effect = EffectsRegistry.KnifeDistortion;
            effect.Parameters["uTransform"].SetValue(model * projection);
            Main.graphics.GraphicsDevice.Textures[0] = TrailTexture;
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            if (bars.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * 16 * Projectile.spriteDirection;
            Vector2 v = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            Main.EntitySpriteDraw(texture, v - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, new Rectangle?(rect)
                , Color.White, drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
