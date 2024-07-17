using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.RemakeItems.Vanilla;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class SniperRifleOnSpan : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
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
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        private int timenum = 1000;
        private int timenum2;
        private int rot = 60;
        public override void AI() {
            Projectile.MaxUpdates = 1;
            Projectile flowProj = null;
            if (timenum >= 932) {
                timenum--;
            }
            if (rot > 0) {
                rot--;
            }
            if (Projectile.timeLeft <= 2) {
                Projectile.timeLeft = 2;
            }
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.maxProjectiles) {
                flowProj = Main.projectile[(int)Projectile.ai[1]];
            }
            if (flowProj == null) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = flowProj.Center;
            Projectile.rotation = flowProj.rotation;

            if (++Projectile.ai[0] > 10) {
                onFire = true;
            }
            if (!Owner.PressKey()) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            float lastdamage = 0;
            if (timenum > 972) {
                lastdamage = 0.1f;
            }
            else if (timenum < 972 && timenum > 942) {
                lastdamage = ((972 - timenum) * 20 + 344) / (float)RSniperRifle.BaseDamage;
            }
            else if (timenum < 942) {
                lastdamage = ((942 - timenum) * 340 + 644) / (float)RSniperRifle.BaseDamage;
            }
            if (lastdamage > 6) {
                lastdamage = 6;
            }

            if (Owner.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun)) {
                if (onFire) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        ShootState shootState = Owner.GetShootState("CWRGunShoot");
                        int ammo = shootState.AmmoTypes;
                        if (ammo == ProjectileID.Bullet || ammo == ModContent.ProjectileType<MarksmanShot>()) {
                            ammo = ProjectileID.BulletHighVelocity;
                        }
                        else {
                            lastdamage *= 0.8f;
                        }
                        baseFeederGun.Recoil = 3;
                        baseFeederGun.GunPressure = 0.5f;
                        baseFeederGun.ControlForce = 0.05f;
                        baseFeederGun.UpdateMagazineContents();
                        int proj = Projectile.NewProjectile(shootState.Source, Projectile.Center + new Vector2(0, -5),
                        (UnitToMouseV * 15).RotatedByRandom(rot * 0.01f), ammo,
                        (int)(shootState.WeaponDamage * lastdamage), 0, Projectile.owner);
                        if (proj > 0 && proj < Main.maxProjectiles) {
                            Main.projectile[proj].CWR().GetHitAttribute.OnHitBlindArmor = true;
                        }
                    }
                    SoundEngine.PlaySound(new("CalamityMod/Sounds/Item/TankCannon") { Pitch = Projectile.ai[2] }, Projectile.Center);
                    baseFeederGun.CreateRecoil();
                    baseFeederGun.CaseEjection();
                    baseFeederGun.SpawnGunFireDust(Owner.Center, baseFeederGun.ShootVelocity);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float angle = ToMouseA;
            float blinkage = 0;
            if (Projectile.timeLeft >= MaxCharge * 1.5f) {
                blinkage = (float)Math.Sin(MathHelper.Clamp((Projectile.timeLeft - MaxCharge * 1.5f) / 15f, 0, 1)
                    * MathHelper.PiOver2 + MathHelper.PiOver2);
            }
            Effect effect = Filters.Scene["CalamityMod:SpreadTelegraph"].GetShader().Shader;
            effect.Parameters["centerOpacity"].SetValue(ChargeProgress + 0.1f);
            effect.Parameters["mainOpacity"].SetValue((float)Math.Sqrt((300 - Projectile.timeLeft)) * 0.15f);
            effect.Parameters["halfSpreadAngle"].SetValue(Spread / 3f);
            effect.Parameters["edgeColor"].SetValue(Color.Lerp(Color.Red, Color.Red, blinkage).ToVector3());
            effect.Parameters["centerColor"].SetValue(Color.Lerp(Color.DarkRed, Color.DarkRed, blinkage).ToVector3());
            effect.Parameters["edgeBlendLength"].SetValue(0.0007f);
            effect.Parameters["edgeBlendStrength"].SetValue(0.007f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/InvisibleProj").Value;

            Main.EntitySpriteDraw(texture, Owner.MountedCenter - Main.screenPosition + UnitToMouseV * 59 + new Vector2(0, -5)
                , null, Color.DarkRed, angle, new Vector2(texture.Width / 2f, texture.Height / 2f), 4000f, 0, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
