using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Particles;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class PhoenixFlameBarrageHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PhoenixFlameBarrage";
        public override int targetCayItem => ModContent.ItemType<PhoenixFlameBarrage>();
        public override int targetCWRItem => ModContent.ItemType<PhoenixFlameBarrageEcType>();

        private float sengs;
        public override void SetMagicProperty() {
            HandDistance = 30;
            HandFireDistance = 72;
            HandFireDistanceY = -10;
        }

        private static void DrawTreasureBagEffect(SpriteBatch spriteBatch, Texture2D tex, ref float drawTimer, Vector2 position
            , Rectangle? rect, Color color, float rot, Vector2 origin, float scale, SpriteEffects effects = 0) {
            float time = Main.GlobalTimeWrappedHourly;
            float timer = drawTimer / 240f + time * 0.04f;
            time %= 4f;
            time /= 2f;
            if (time >= 1f)
                time = 2f - time;
            time = time * 0.5f + 0.5f;
            for (float i = 0f; i < 1f; i += 0.25f) {
                float radians = (i + timer) * MathHelper.TwoPi;
                spriteBatch.Draw(tex, position + new Vector2(0f, 8f).RotatedBy(radians) * time, rect, new Color(color.R, color.G, color.B, 50), rot, origin, scale, effects, 0);
            }
            for (float i = 0f; i < 1f; i += 0.34f) {
                float radians = (i + timer) * MathHelper.TwoPi;
                spriteBatch.Draw(tex, position + new Vector2(0f, 4f).RotatedBy(radians) * time, rect, new Color(color.R, color.G, color.B, 77), rot, origin, scale, effects, 0);
            }
        }

        public override void PostInOwnerUpdate() {
            if (CanFire) {
                if (sengs < 1f) {
                    sengs += 0.05f;
                }
            }
            else {
                if (sengs > 0) {
                    sengs -= 0.05f;
                }
            }
        }

        public override void FiringShoot() {
            int type = ModContent.ProjectileType<PhantomPhoenix>();
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = GunShootPos;
                spanPos += CWRUtils.randVr(120, 150);
                Projectile.NewProjectileDirect(Source, spanPos, ShootVelocity
                , type, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }

        private void drawMatric(string texkey, Vector2 drawpos, Vector2 size, float rotation, float uOpacity, bool set) {
            Texture2D texRing = CWRUtils.GetT2DValue(CWRConstant.Masking + texkey);
            Effect effect = Filters.Scene["CWRMod:neutronRingShader"].GetShader().Shader;
            effect.Parameters["uTime"].SetValue(rotation);
            effect.Parameters["cosine"].SetValue((float)Math.Cos(rotation));
            effect.Parameters["uColor"].SetValue(Color.White.ToVector3());
            effect.Parameters["uImageSize1"].SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            effect.Parameters["uOpacity"].SetValue(uOpacity);
            effect.Parameters["set"].SetValue(set && onFireR);
            effect.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, BlendState.Additive, Main.DefaultSamplerState, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            var rec = CWRUtils.GetRec(texRing, -texRing.Width / 2, -texRing.Height / 2, texRing.Width * 2, texRing.Height * 2);
            Main.spriteBatch.Draw(texRing, drawpos, rec, Color.OrangeRed
                , MathHelper.Pi + Projectile.rotation
                , rec.Size() / 2, size, SpriteEffects.None, 0);
            Main.spriteBatch.ResetBlendState();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            if (CanFire) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

                DrawTreasureBagEffect(Main.spriteBatch, TextureValue, ref Time, Projectile.Center - Main.screenPosition, null
                    , Color.OrangeRed * Projectile.Opacity * 0.6f, Projectile.rotation + MathHelper.PiOver4 * DirSign
                    , TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            }

            if (sengs > 0) {
                Vector2 origPos = Owner.GetPlayerStabilityCenter() - Main.screenPosition;
                Vector2 drawVr = new Vector2(0, 1);
                drawMatric("NormalMatrix", origPos, new Vector2(0.25f, 0.8f) * sengs, Main.GameUpdateCount / 20f, 1f, false);
            }

            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4 * DirSign, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class PhantomPhoenix : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "PhantomPhoenix";

        private ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 1220;
        }

        public override bool? CanHitNPC(NPC target) {
            return Time < 15 * Projectile.extraUpdates ? false : base.CanHitNPC(target);
        }

        public override bool PreAI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 6, 7);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Time > 30) {
                NPC target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    if (Time < 90) {
                        Projectile.ChasingBehavior2(target.Center, 1, 0.08f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }

            if (Projectile.Distance(Main.LocalPlayer.Center) < 1400) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = Projectile.velocity * 0.2f,
                    Position = Projectile.Center + CWRUtils.randVr(6),
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    maxLifeTime = 60,
                    minLifeTime = 30
                };
                PRTLoader.AddParticle(lavaFire);
            }

            Time++;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 420);
        }

        public override void OnKill(int timeLeft) {
            float OrbSize = Main.rand.NextFloat(0.5f, 0.8f);
            Particle orb = new GenericBloom(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8, true);
            GeneralParticleHandler.SpawnParticle(orb);
            Particle orb2 = new GenericBloom(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8, true);
            GeneralParticleHandler.SpawnParticle(orb2);
            Projectile.Explode();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, Projectile.frame, 8)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(mainValue, 8), Projectile.scale
                , Projectile.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
            return false;
        }
    }
}
