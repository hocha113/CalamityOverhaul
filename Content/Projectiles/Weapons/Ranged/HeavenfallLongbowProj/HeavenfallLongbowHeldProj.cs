﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class HeavenfallLongbowHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "HeavenfallLongbowProj";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<HeavenfallLongbow>();
        public override bool CanFire => (Projectile.ai[2] == 0 && DownLeft) || (Projectile.ai[2] == 1 && DownRight);
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

            Time++;
        }

        public void SpanProj() {
            Vector2 ver = Projectile.rotation.ToRotationVector2();
            ShootState shootState = Owner.GetShootState();
            if (Projectile.ai[2] == 0) {
                if (Time > 10) {
                    SoundEngine.PlaySound(HeavenlyGale.FireSound, Projectile.Center);
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
                VaultUtils.GetOrig(mainValue, 5),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Color drawColor2 = VaultUtils.MultiStepColorLerp(Projectile.ai[0] % 15 / 15f, HeavenfallLongbow.rainbowColors);
            if (HFBow.ChargeValue < 200)
                drawColor2 = VaultUtils.MultiStepColorLerp(HFBow.ChargeValue / 200f, HeavenfallLongbow.rainbowColors);
            float slp2 = HFBow.ChargeValue / 300f;
            if (slp2 > 1)
                slp2 = 1;
            if (slp2 < 0.1f)
                slp2 = 0;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < 8; i++)
                Main.EntitySpriteDraw(
                    mainValue,
                    Projectile.Center - Main.screenPosition,
                    mainValue.GetRectangle(Projectile.frame, 5),
                    drawColor2,
                    Projectile.rotation,
                    VaultUtils.GetOrig(mainValue, 5),
                    Projectile.scale * (1 + slp2 * 0.08f),
                    Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                    );
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                mainValue.GetRectangle(Projectile.frame, 5),
                lightColor,
                Projectile.rotation,
                VaultUtils.GetOrig(mainValue, 5),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );

            return false;
        }

        public override bool? CanDamage() => false;
    }
}
