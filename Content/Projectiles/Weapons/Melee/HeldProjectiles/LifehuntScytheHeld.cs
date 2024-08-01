using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class LifehuntScytheHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "LifehuntScythe";
        public float[] oldrot = new float[7];
        private Vector2 startVector;
        private Vector2 vector;
        public ref float Length => ref Projectile.localAI[0];
        public ref float Rot => ref Projectile.localAI[1];
        public float Timer;
        private float speed;
        private float SwingSpeed;
        private float glow;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }
        public override bool ShouldUpdatePosition() => false;
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.width = 46;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Rot = MathHelper.ToRadians(1);
            Length = 62;
        }

        public float SetSwingSpeed(float speed) => speed / Owner.GetAttackSpeed(DamageClass.Melee);

        public static Vector2 PolarVector(float radius, float theta) => new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * radius;

        public void InOnwer() {
            SetDirection();
            if (Projectile.ai[0] % 2 == 0) {
                if (Timer++ == 0) {
                    speed = MathHelper.ToRadians(1);
                    startVector = PolarVector(1, Projectile.velocity.ToRotation() - ((MathHelper.PiOver2 + 0.6f) * Projectile.spriteDirection));
                    vector = startVector * Length;
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                }
                if (Timer == (int)(4 * SwingSpeed)) {
                    int max = (int)Projectile.ai[1];
                    for (int i = 0; i < max; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, (MathHelper.TwoPi / max * i).ToRotationVector2() * (13 + max),
                        ModContent.ProjectileType<LifeScythe1>(), (int)(Projectile.damage * 0.75f), Projectile.knockBack / 2, Projectile.owner);
                    }
                }
                if (Timer < 6 * SwingSpeed) {
                    Rot += speed / SwingSpeed * Projectile.spriteDirection;
                    speed += 0.1f;
                    speed += (1.2f - SwingSpeed) * 0.1f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Rot += speed / SwingSpeed * Projectile.spriteDirection;
                    speed *= 0.7f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 25 * SwingSpeed) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.ai[0] == 1) {
                if (Timer++ == 0) {
                    speed = MathHelper.ToRadians(1);
                    Projectile.velocity = PolarVector(5, (Main.MouseWorld - Owner.Center).ToRotation());
                    startVector = PolarVector(1, (Main.MouseWorld - Owner.Center).ToRotation() + ((MathHelper.PiOver2 + 0.6f) * Owner.direction));
                    vector = startVector * Length;
                    SoundEngine.PlaySound(SoundID.Item71, Projectile.position);
                }

                if (Timer < 6 * SwingSpeed) {
                    Rot -= speed / SwingSpeed * Projectile.spriteDirection;
                    speed += 0.1f;
                    speed += (1.2f - SwingSpeed) * 0.1f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Rot -= speed / SwingSpeed * Projectile.spriteDirection;
                    speed *= 0.7f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 25 * SwingSpeed) {
                    Projectile.Kill();
                }
            }
        }

        public override void AI() {
            if (Owner.noItems || Owner.CCed || Owner.dead || !Owner.active) {
                Projectile.Kill();
            }
            SetHeld();
            SwingSpeed = SetSwingSpeed(1.2f);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Projectile.spriteDirection = Math.Sign(ToMouse.X);
            if (Projectile.ai[0] < 2) {
                Projectile.rotation = Projectile.spriteDirection == 1
                    ? (Projectile.Center - Owner.Center).ToRotation() + MathHelper.PiOver4
                    : (Projectile.Center - Owner.Center).ToRotation() - MathHelper.Pi - MathHelper.PiOver4;
                glow += 0.03f;
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, (Owner.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            if (Projectile.IsOwnedByLocalPlayer()) {
                InOnwer();
            }
            if (Timer > 1) {
                Projectile.alpha = 0;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + vector;
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                oldrot[k] = oldrot[k - 1];
            }
            oldrot[0] = Projectile.rotation;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.ActiveItem().type == ModContent.ItemType<GuardianTerra>() && Projectile.numHits == 0) {
                int proj = Projectile.NewProjectile(new EntitySource_ItemUse(Owner, Owner.ActiveItem()), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 130;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteEffects spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new(texture.Width / 2f, texture.Height / 2f);
            Vector2 trialOrigin = new(texture.Width / 2f - 6, Projectile.height / 2f);
            int shader = ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex;
            Vector2 tovmgs = PolarVector(20, (Projectile.Center - Owner.Center).ToRotation());

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(shader, Owner, null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - tovmgs - Main.screenPosition + trialOrigin + new Vector2(0f, Projectile.gfxOffY);
                Color color = Color.LimeGreen * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color * Projectile.Opacity * glow, oldrot[k], origin, Projectile.scale, spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, Projectile.Center - tovmgs - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, null
                , Projectile.GetAlpha(CWRUtils.MultiStepColorLerp(0.6f, Color.White, lightColor)), Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);
            return false;
        }
    }
}
