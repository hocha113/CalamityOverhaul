using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class HeavenfallLongbowHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "HeavenfallLongbowProj";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<HeavenfallLongbow>();

        private Player Owners => CWRUtils.GetPlayerInstance(Projectile.owner);
        private HeavenfallLongbow HFBow => (HeavenfallLongbow)Owners.HeldItem.ModItem;
        private Vector2 toMou = Vector2.Zero;

        private ref float Time => ref Projectile.ai[0];

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

            if (Owners == null || Owners.HeldItem?.type != ItemType<HeavenfallLongbow>()) {
                Projectile.Kill();
                return;
            }

            StickToOwner();
            if (Projectile.IsOwnedByLocalPlayer() && Owners.ownedProjectileCounts[ProjectileType<VientianePunishment>()] == 0)
                SpanProj();

            Time++;
        }

        public void SpanProj() {
            Vector2 vr = Projectile.rotation.ToRotationVector2();
            int weaponDamage2 = Owners.GetWeaponDamage(Owners.ActiveItem());
            float weaponKnockback2 = Owners.GetWeaponKnockback(Owners.ActiveItem(), Owners.ActiveItem().knockBack);
            if (Projectile.ai[2] == 0) {
                if (Time > 10) {
                    SoundEngine.PlaySound(HeavenlyGale.FireSound, Projectile.Center);
                    Owners.PickAmmo(Owners.ActiveItem(), out _, out _, out weaponDamage2, out weaponKnockback2, out _);
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr * 20, ProjectileType<InfiniteArrow>(), weaponDamage2, weaponKnockback2, Owners.whoAmI);
                    HFBow.ChargeValue += 5;
                    Time = 0;
                }
            }
            else {
                if (Time > 15) {
                    SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Owners.PickAmmo(Owners.ActiveItem(), out _, out _, out weaponDamage2, out weaponKnockback2, out _);
                        Vector2 spanPos = Projectile.Center + new Vector2(0, -633) + new Vector2(Main.MouseWorld.X - Owners.position.X, 0) * Main.rand.NextFloat(0.3f, 0.45f);
                        Vector2 vr3 = spanPos.To(Main.MouseWorld).UnitVector().RotateRandom(12 * CWRUtils.atoR) * 23;
                        Projectile.NewProjectile(Projectile.parent(), spanPos, vr3, ProjectileType<ParadiseArrow>(), (int)(weaponDamage2 * 0.5f), weaponKnockback2, Owners.whoAmI);
                    }
                    HFBow.ChargeValue += 3;
                    Time = 0;
                }
            }
        }

        public void StickToOwner() {
            HFBow.Item.damage = 9999;

            if (HFBow.ChargeValue > 200)
                HFBow.ChargeValue = 200;

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
            Projectile.position = Owners.RotatedRelativePoint(Owners.MountedCenter, true) - Projectile.Size / 2f + toMou.UnitVector() * 25;
            Projectile.rotation = toMou.ToRotation();
            Projectile.spriteDirection = Projectile.direction = Math.Sign(toMou.X);
            Owners.ChangeDir(Projectile.direction);
            Owners.heldProj = Projectile.whoAmI;
        }

        public override void PostDraw(Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frame, 5),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 5),
                Projectile.scale,
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Color drawColor2 = CWRUtils.MultiStepColorLerp(Projectile.ai[0] % 15 / 15f, HeavenfallLongbow.rainbowColors);
            if (HFBow.ChargeValue < 200)
                drawColor2 = CWRUtils.MultiStepColorLerp(HFBow.ChargeValue / 200f, HeavenfallLongbow.rainbowColors);
            float slp2 = HFBow.ChargeValue / 300f;
            if (slp2 > 1)
                slp2 = 1;
            if (slp2 < 0.1f)
                slp2 = 0;

            Main.spriteBatch.SetAdditiveState();
            for (int i = 0; i < 8; i++)
                Main.EntitySpriteDraw(
                    mainValue,
                    Projectile.Center - Main.screenPosition,
                    CWRUtils.GetRec(mainValue, Projectile.frame, 5),
                    drawColor2,
                    Projectile.rotation,
                    CWRUtils.GetOrig(mainValue, 5),
                    Projectile.scale * (1 + slp2 * 0.08f),
                    Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                    );
            Main.spriteBatch.ResetBlendState();

            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frame, 5),
                lightColor,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 5),
                Projectile.scale,
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );

            return false;
        }

        public override bool? CanDamage() => false;
    }
}
