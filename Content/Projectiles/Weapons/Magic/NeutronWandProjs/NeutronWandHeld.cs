using CalamityOverhaul.Content.Items.Magic.Extras;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs
{
    internal class NeutronWandHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        public override int targetCayItem => NeutronWand.PType;
        public override int targetCWRItem => NeutronWand.PType;

        private int fireIndex;
        private float colers;
        private bool colers2;
        private Vector2 firePos;
        public override void SetRangedProperty() {
            HandDistance = 52;
            HandDistanceY = -20;
            HandFireDistance = 52;
            GunPressure = 0;
            Recoil = 0;
            ControlForce = 0;
            AngleFirearmRest = -20;
            ArmRotSengsBackNoFireOffset = -20;
            ShootPosToMouLengValue = 20;
            CanRightClick = true;
        }

        public override void PostInOwnerUpdate() {
            if (onFireR) {
                if (!colers2) {
                    colers2 = true;
                    if (colers <= 0) {
                        firePos = ToMouse + Owner.GetPlayerStabilityCenter();
                    }
                    SoundEngine.PlaySound(SoundID.Item77, Projectile.Center);
                }
                if (colers < 1f) {
                    ShootCoolingValue = 2;
                    colers += 0.01f;
                }
            }
            else {
                if (colers > 0) {
                    colers -= 0.015f;
                }
                fireIndex = 0;
                colers2 = false;
            }

            firePos = Vector2.Lerp(firePos, ToMouse + Owner.GetPlayerStabilityCenter(), 0.1f);
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 11);
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item88 with { Pitch = -0.6f }, Projectile.Center);
            }
            else if (onFireR) {
                SoundStyle sound = Item.UseSound.Value;
                SoundEngine.PlaySound(sound with { Pitch = -0.1f + fireIndex * 0.15f }, Projectile.Center);
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity * (0.6f + i * 0.1f)
                , ModContent.ProjectileType<NeutronMagchStar>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            int newdamage = (int)(WeaponDamage * (1 + fireIndex * 0.15f));
            for (int i = 0; i < 3; i++) {
                Vector2 shootPos = firePos;
                shootPos.X += (i - 1) * fireIndex * 30;
                shootPos.Y += Main.rand.Next(-113, 33);
                Projectile.NewProjectile(Source, shootPos, new Vector2(0, 1)
                , ModContent.ProjectileType<NeutronWandExplode>(), newdamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            if (++fireIndex > 3) {
                fireIndex = 0;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, CWRUtils.GetRec(TextureValue, Projectile.frame, 12), lightColor
                , Projectile.rotation + MathHelper.PiOver4 * DirSign, CWRUtils.GetOrig(TextureValue, 12), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            if (colers > 0) {
                Vector2 origPos = firePos - Main.screenPosition;
                Vector2 drawVr = new Vector2(0, 1);
                drawMatric("NormalMatrix", origPos, new Vector2(0.25f, 0.8f) * colers, Main.GameUpdateCount / 20f, 1f, fireIndex == 3);
                drawMatric("NormalMatrix", origPos + drawVr * 133 * colers * colers, new Vector2(0.15f, 0.6f) * colers, Main.GameUpdateCount / 15f, 0.9f, fireIndex == 1);
                drawMatric("NormalMatrix", origPos + drawVr * 266 * colers * colers, new Vector2(0.15f, 0.5f) * colers, Main.GameUpdateCount / 5f, 0.8f, fireIndex == 2);
                drawMatric("NormalMatrix", origPos + drawVr * -133 * colers * colers, new Vector2(0.15f, 0.6f) * colers, Main.GameUpdateCount / 15f, 0.9f, fireIndex == 1);
                drawMatric("NormalMatrix", origPos + drawVr * -266 * colers * colers, new Vector2(0.15f, 0.5f) * colers, Main.GameUpdateCount / 5f, 0.8f, fireIndex == 2);
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
            Main.spriteBatch.Draw(texRing, drawpos, rec, Color.White, MathHelper.PiOver2, rec.Size() / 2, size, SpriteEffects.None, 0);
            Main.spriteBatch.ResetBlendState();
        }
    }
}
