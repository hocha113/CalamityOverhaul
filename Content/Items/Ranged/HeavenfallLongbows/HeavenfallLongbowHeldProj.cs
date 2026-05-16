using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class HeavenfallLongbowHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "HeavenfallLongbowProj";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<HeavenfallLongbow>();
        public override bool CanFire => Projectile.ai[2] == 0 && DownLeft || Projectile.ai[2] == 1 && DownRight;
        private HeavenfallLongbow HFBow => (HeavenfallLongbow)Owner.GetItem().ModItem;
        private int Time = 30;
        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 116;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0f, 0.7f, 0.5f);
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 4);
            if (Owner == null || Owner.HeldItem?.type != ItemType<HeavenfallLongbow>()) {
                Projectile.Kill();
                return;
            }

            StickToOwner();
            if (Projectile.IsOwnedByLocalPlayer() && Owner.ownedProjectileCounts[ProjectileType<VientianePunishment>()] <= 0) {
                SpanProj();
            }

            //充能时绕弓体洒落微型棱镜碎片 (代表能量在凝聚)
            if (!VaultUtils.isServer && Time % 5 == 0 && HFBow.ChargeValue > 0) {
                float chargeRatio = MathHelper.Clamp(HFBow.ChargeValue / 200f, 0f, 1f);
                float orbitRadius = MathHelper.Lerp(60f, 22f, chargeRatio); //充能越满, 越聚向中心
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = ang.ToRotationVector2() * orbitRadius;
                Vector2 inwardVel = -offset.SafeNormalize(Vector2.Zero) * (1.2f + chargeRatio * 1.8f);
                Color col = VaultUtils.MultiStepColorLerp(
                    Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors);
                PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                    Projectile.Center + offset, inwardVel, col,
                    Main.rand.NextFloat(0.4f, 0.75f), Main.rand.Next(18, 28),
                    Main.rand.NextFloat(2f, 4f), shortStretch: true));
            }

            Time++;
        }

        public void SpanProj() {
            Vector2 ver = Projectile.rotation.ToRotationVector2();
            ShootState shootState = Owner.GetShootState();
            if (Projectile.ai[2] == 0) {
                if (Time > 10) {
                    SoundEngine.PlaySound("CalamityMod/Sounds/Item/HeavenlyGaleFire".GetSound(), Projectile.Center);
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, ver * 20, ProjectileType<InfiniteArrow>()
                        , shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI);
                    HFBow.ChargeValue += 5;
                    Time = 0;
                }
            }
            else {
                if (Time > 15) {
                    SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Vector2 spanPos = Projectile.Center + new Vector2(0, -633) + new Vector2(Main.MouseWorld.X - Owner.position.X, 0) * Main.rand.NextFloat(0.3f, 0.45f);
                        Vector2 vr3 = spanPos.To(Main.MouseWorld).UnitVector().RotateRandom(12 * CWRUtils.atoR) * 23;
                        Projectile.NewProjectile(Projectile.FromObjectGetParent(), spanPos, vr3, ProjectileType<ParadiseArrow>()
                            , (int)(shootState.WeaponDamage * 0.5f), shootState.WeaponKnockback, Owner.whoAmI);
                    }
                    HFBow.ChargeValue += 3;
                    Time = 0;
                }
            }
        }

        public void StickToOwner() {
            HFBow.Item.damage = 9999;

            if (HFBow.ChargeValue > 200) {
                HFBow.ChargeValue = 200;
            }

            if (CanFire) {
                Projectile.timeLeft = 2;
                float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
            }

            Projectile.position = Owner.GetPlayerStabilityCenter() - Projectile.Size / 2f + ToMouse.UnitVector() * 25;
            Projectile.rotation = ToMouseA;
            Projectile.spriteDirection = Projectile.direction = Math.Sign(ToMouse.X);
            Owner.ChangeDir(Projectile.direction);
            Owner.heldProj = Projectile.whoAmI;
        }

        public override void PostDraw(Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                mainValue.GetRectangle(Projectile.frame, 5),
                Color.White,
                Projectile.rotation,
                mainValue.GetOrig(5),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
        }

        public override bool PreDraw(ref Color lightColor) {
            //先用 Aura 着色器画背景棱镜光环, 替代旧版 8x 叠画
            DrawPrismAura();

            //本体: 普通绘制
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                mainValue.GetRectangle(Projectile.frame, 5),
                lightColor,
                Projectile.rotation,
                mainValue.GetOrig(5),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );

            return false;
        }

        private void DrawPrismAura() {
            float chargeRatio = MathHelper.Clamp(HFBow.ChargeValue / 200f, 0f, 1f);
            if (chargeRatio < 0.05f) {
                return;
            }

            Effect shader = EffectLoader.HeavenfallPrismTrail?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || glow == null || noise == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(chargeRatio * (0.55f + 0.45f * chargeRatio));
            shader.Parameters["coreIntensity"]?.SetValue(0.4f + chargeRatio * 0.6f);
            shader.Parameters["dispersion"]?.SetValue(0.05f);
            shader.Parameters["flowSpeed"]?.SetValue(0.5f);
            shader.Parameters["hueOffset"]?.SetValue(0f);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.CurrentTechnique = shader.Techniques["Aura"];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

            //充能越满, 光环越大且更亮 (随心跳脉动)
            float pulse = 1f + 0.08f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);
            float baseSize = 110f + chargeRatio * 70f;
            float scale = baseSize / glow.Width * pulse;

            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            //还原为 PreDraw 调用前的常规批次状态 (Deferred + AlphaBlend)
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override bool? CanDamage() => false;
    }
}
