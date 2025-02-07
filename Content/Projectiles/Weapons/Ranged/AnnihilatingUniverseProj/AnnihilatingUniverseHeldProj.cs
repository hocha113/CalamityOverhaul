using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class AnnihilatingUniverseHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "AnnihilatingUniverseProj/AnnihilatingUniverseBow";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<AnnihilatingUniverse>();
        public override bool CanFire => (Projectile.ai[2] == 0 && Owner.PressKey()) || (Projectile.ai[2] == 1 && Owner.PressKey(false));
        private float Time;
        private float Time2;
        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 116;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0f, 0.7f, 0.5f);
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);
            if (Owner == null || Owner.HeldItem?.type != ModContent.ItemType<AnnihilatingUniverse>()) {
                Projectile.Kill();
                return;
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }

            StickToOwner();

            Time++;
            Time2++;
        }

        public void SpanProj() {
            ShootState shootState = Owner.GetShootState();
            if (Projectile.ai[2] == 0) {
                if (Time > 30) {
                    SoundEngine.PlaySound(HeavenlyGale.FireSound, Projectile.Center);
                    Time2 = 0;
                    Time = 0;
                }
                if (Time2 % 5 == 0 && Time2 > 0 && Time2 < 20) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 offset = (Projectile.rotation + Main.rand.Next(-35, 35) * CWRUtils.atoR).ToRotationVector2() * 56;
                        Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center + offset, Projectile.rotation.ToRotationVector2() * (17 + i)
                        , ModContent.ProjectileType<CelestialObliterationArrow>()
                        , shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI);
                    }
                }
            }
            else {
                int types = ModContent.ProjectileType<CosmicEddies>();
                if (!Main.projectile.Any((Projectile n) => n.Alives() && n.ai[2] == 0 && n.type == types)) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Projectile.rotation.ToRotationVector2() * 15
                        , types, (int)(shootState.WeaponDamage * 1.25f), shootState.WeaponKnockback, Owner.whoAmI, Projectile.whoAmI);
                }
            }
        }

        public void StickToOwner() {
            if (CanFire) {
                Projectile.timeLeft = 2;
                Owner.itemTime = 2;
                Owner.itemAnimation = 2;
                float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
            }
            Projectile.position = Owner.GetPlayerStabilityCenter() - Projectile.Size / 2f + ToMouse.UnitVector() * 5;
            Projectile.rotation = ToMouse.ToRotation();
            Projectile.spriteDirection = Projectile.direction = Math.Sign(ToMouse.X);
            Owner.ChangeDir(Projectile.direction);
            Owner.heldProj = Projectile.whoAmI;
        }

        public override void PostDraw(Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                lightColor,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                Projectile.scale,
                Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
            return false;
        }

        public override bool? CanDamage() => false;
    }
}
