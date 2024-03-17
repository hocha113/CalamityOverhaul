using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SniperRifleOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public Player Owner => Main.player[Projectile.owner];
        private Vector2 toMou => Owner.Center.To(Main.MouseWorld);
        public const float MaxCharge = 1000f;
        public float ChargeProgress => 15 * (MaxCharge - timenum) / MaxCharge;
        public float Spread => MathHelper.PiOver2 * (1 - (float)Math.Pow(ChargeProgress, 1.5) * 0.95f);
        private bool onFire;
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 0.2f;
        }

        public override bool? CanDamage() => false;

        int timenum = 1000;
        int rot = 60;
        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile owner = null;
            if (timenum >= 932) timenum--;
            if (rot > 0) rot--;
            if (Projectile.timeLeft <= 2) Projectile.timeLeft = 2;
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.maxProjectiles) {
                owner = Main.projectile[(int)Projectile.ai[1]];
            }
            if (owner == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.rotation = owner.rotation;

            if (++Projectile.ai[0] > 10) {
                onFire = true;
            }
            if (!player.PressKey()) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            int AmmoType = 0;
            if (Owner.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun0)) {
                AmmoType = baseFeederGun0.AmmoTypes;
                if (AmmoType == ProjectileID.Bullet) {
                    AmmoType = ProjectileID.BulletHighVelocity;
                }
            }
            if (Projectile.IsOwnedByLocalPlayer() && onFire && AmmoType != 0) {
                SoundEngine.PlaySound(new("CalamityMod/Sounds/Item/TankCannon") { PitchVariance = 0f }, Projectile.Center);
                int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center + new Vector2(0, -5), 
                    (toMou.SafeNormalize(Vector2.Zero) * 15).RotatedBy(Main.rand.NextFloat(rot * -0.01f, rot * 0.01f))
                , AmmoType, Main.player[Projectile.owner].HeldItem.damage, 0, Projectile.owner);
                if (Main.projectile[proj].penetrate > 1) {
                    Main.projectile[proj].penetrate = 1;
                    Main.projectile[proj].penetrate.Domp();
                }
                if (Owner.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun)) {
                    baseFeederGun.Recoil = 3;
                    baseFeederGun.GunPressure = 0.5f;
                    baseFeederGun.ControlForce = 0.05f;
                    baseFeederGun.CreateRecoil();
                    baseFeederGun.UpdateMagazineContents();
                    baseFeederGun.SpawnGunFireDust(Owner.Center, Projectile.rotation.ToRotationVector2());
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float angle = (Owner.Calamity().mouseWorld - Owner.MountedCenter).ToRotation();
            float blinkage = 0;
            if (Projectile.timeLeft >= MaxCharge * 1.5f) {
                blinkage = (float)Math.Sin(MathHelper.Clamp((Projectile.timeLeft - MaxCharge * 1.5f) / 15f, 0, 1) * MathHelper.PiOver2 + MathHelper.PiOver2);
            }
            Effect effect = Filters.Scene["CalamityMod:SpreadTelegraph"].GetShader().Shader;
            effect.Parameters["centerOpacity"].SetValue(ChargeProgress + 0.1f);
            effect.Parameters["mainOpacity"].SetValue((float)Math.Sqrt((300 - Projectile.timeLeft)) * 0.15f);
            effect.Parameters["halfSpreadAngle"].SetValue(Spread / 2f);
            effect.Parameters["edgeColor"].SetValue(Color.Lerp(Color.Red, Color.Red, blinkage).ToVector3());
            effect.Parameters["centerColor"].SetValue(Color.Lerp(Color.DarkRed, Color.DarkRed, blinkage).ToVector3());
            effect.Parameters["edgeBlendLength"].SetValue(0.0007f);
            effect.Parameters["edgeBlendStrength"].SetValue(0.007f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/InvisibleProj").Value;

            Main.EntitySpriteDraw(texture, Owner.MountedCenter - Main.screenPosition + toMou.SafeNormalize(Vector2.Zero) * 59 + new Vector2(0,-5), null, Color.DarkRed, angle, new Vector2(texture.Width / 2f, texture.Height / 2f), 4000f, 0, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
