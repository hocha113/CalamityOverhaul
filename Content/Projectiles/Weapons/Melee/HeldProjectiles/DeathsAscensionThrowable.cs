using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class DeathsAscensionThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DeathsAscension";

        private bool inOut;
        private bool outFive;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 17;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetThrowable() {
            Projectile.DamageType = DamageClass.Melee;
            HandOnTwringMode = -105;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.scale = 1.5f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return CWRUtils.CircularHitboxCollision(Projectile.Center, 125, targetHitbox);
        }

        public override void OnThrowing() {
            SetDirection();
            base.OnThrowing();
        }

        public override bool PreThrowOut() {
            outFive = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
            return true;
        }

        public override void FlyToMovementAI() {
            float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * DirSign * SafeGravDir;
            float rot2 = (MathHelper.PiOver2 * SafeGravDir - MathHelper.ToRadians(DirSign > 0 ? -20 : 200)) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot2 * -DirSign);

            if (DownLeft && Projectile.ai[2] < 60 && !inOut) {
                Owner.direction = Math.Sign(ToMouse.X);
                Projectile.ChasingBehavior(InMousePos, 33);
                Projectile.rotation += 0.6f * Owner.direction;
                if (Projectile.Distance(InMousePos) < 66) {
                    Projectile.ai[2]++;
                }
            }
            else {
                inOut = true;
                Vector2 toProj = Owner.Center.To(Projectile.Center);
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.1f, 10);
                }
                for (int i = 0; i < 13; i++) {
                    SparkParticle spark = new SparkParticle(Owner.Center, toProj.UnitVector() * 3
                        , false, 9, 3.3f, Color.DarkBlue);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                Projectile.velocity = Vector2.Zero;
                Owner.Center = Vector2.Lerp(Owner.Center, Projectile.Center, 0.12f);
                Owner.velocity = toProj.UnitVector();
                if (Projectile.Distance(Owner.Center) < 86) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, Owner.velocity
                        , ModContent.ProjectileType<DeathsAscensionBreakSwing>()
                        , Projectile.damage * 3, Projectile.knockBack, Owner.whoAmI, 0f, 0f);
                    Owner.GivePlayerImmuneState(30);
                    Projectile.Kill();
                }
                Projectile.rotation -= 0.6f * Owner.direction;
            }
            if (Projectile.soundDelay <= 0) {
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.2f }, Projectile.Center);
                Projectile.soundDelay = 10;
            }
        }

        public override void DrawThrowable(Color lightColor) {
            Vector2 orig = TextureValue.Size() / 2;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex, Owner, null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + new Vector2(33, 33);
                Color color = Color.DarkBlue * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(TextureValue, drawPos, null, color * Projectile.Opacity * 0.65f
                    , Projectile.oldRot[k], orig, Projectile.scale, spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, spriteEffects, 0);


            if (outFive) {
                Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Particles/SemiCircularSmear").Value;
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                Main.EntitySpriteDraw(color: Color.Fuchsia * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: Projectile.Center - Main.screenPosition
                    , sourceRectangle: null, rotation: Projectile.rotation - CWRUtils.PiOver5 + MathHelper.Pi
                    , scale: Projectile.scale * 1.15f, effects: SpriteEffects.None);
                Main.spriteBatch.ExitShaderRegion();
            }
        }
    }
}
