using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class AnnihilatingUniverseHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "AnnihilatingUniverseProj/AnnihilatingUniverseBow";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<AnnihilatingUniverse>();

        private Player Owners => CWRUtils.GetPlayerInstance(Projectile.owner);
        private Vector2 toMou = Vector2.Zero;
        private ref float Time => ref Projectile.ai[0];
        private ref float Time2 => ref Projectile.ai[1];
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
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

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(toMou);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            toMou = reader.ReadVector2();
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0f, 0.7f, 0.5f);

            if (Owners == null || Owners.HeldItem?.type != ModContent.ItemType<AnnihilatingUniverse>()) {
                Projectile.Kill();
                return;
            }

            if (Projectile.IsOwnedByLocalPlayer())
                SpanProj();
            StickToOwner();
            Time++;
            Time2++;
        }

        public void SpanProj() {
            int weaponDamage2 = Owners.GetWeaponDamage(Owners.ActiveItem());
            float weaponKnockback2 = Owners.ActiveItem().knockBack;
            if (Projectile.ai[2] == 0) {
                if (Time > 30) {
                    SoundEngine.PlaySound(HeavenlyGale.FireSound, Projectile.Center);
                    bool haveAmmo = Owners.PickAmmo(Owners.ActiveItem(), out _, out _, out weaponDamage2, out weaponKnockback2, out _);
                    weaponKnockback2 = Owners.GetWeaponKnockback(Owners.ActiveItem(), weaponKnockback2);
                    Time2 = 0;
                    Time = 0;
                }
                if (Time2 % 5 == 0 && Time2 > 0 && Time2 < 20) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 offset = (Projectile.rotation + Main.rand.Next(-35, 35) * CWRUtils.atoR).ToRotationVector2() * 56;
                        Projectile.NewProjectile(Projectile.parent(), Projectile.Center + offset, Projectile.rotation.ToRotationVector2() * (17 + i)
                        , ModContent.ProjectileType<CelestialObliterationArrow>(), weaponDamage2, weaponKnockback2, Owners.whoAmI);
                    }
                }
            }
            else {
                int types = ModContent.ProjectileType<CosmicEddies>();
                if (!Main.projectile.Any((Projectile n) => n.Alives() && n.ai[2] == 0 && n.type == types)) {
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, Projectile.rotation.ToRotationVector2() * 15
                        , types, (int)(weaponDamage2 * 1.25f), weaponKnockback2, Owners.whoAmI, Projectile.whoAmI);
                }
            }
        }

        public void StickToOwner() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 oldToMou = toMou;
                toMou = Owners.Center.To(Main.MouseWorld);
                if (oldToMou != toMou) {
                    Projectile.netUpdate = true;
                }
            }
            if ((Projectile.ai[2] == 0 && Owners.PressKey()) || (Projectile.ai[2] == 1 && Owners.PressKey(false))) {
                Projectile.timeLeft = 2;
                Owners.itemTime = 2;
                Owners.itemAnimation = 2;
                float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owners.direction;
                Owners.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
            }
            Projectile.position = Owners.RotatedRelativePoint(Owners.MountedCenter, true) - Projectile.Size / 2f + toMou.UnitVector() * 5;
            Projectile.rotation = toMou.ToRotation();
            Projectile.spriteDirection = Projectile.direction = Math.Sign(toMou.X);
            Owners.ChangeDir(Projectile.direction);
            Owners.heldProj = Projectile.whoAmI;
        }

        public override void PostDraw(Color lightColor) {
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                Projectile.scale,
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
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
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
            return false;
        }

        public override bool? CanDamage() => false;
    }
}
