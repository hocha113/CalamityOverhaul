using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DaemonsFlameHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DaemonsFlameBow";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DaemonsFlame>();

        private Player Owners => CWRUtils.GetPlayerInstance(Projectile.owner);
        private Vector2 toMou = Vector2.Zero;
        private ref float Time => ref Projectile.ai[0];

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
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);
            if (Owners == null) {
                Projectile.Kill();
                return;
            }
            if (Projectile.IsOwnedByLocalPlayer())
                SpanProj();
            StickToOwner();
            Time++;
        }

        public void SpanProj() {
            int ArrowTypes = ProjectileID.WoodenArrowFriendly;
            float scaleFactor11 = 14f;
            int weaponDamage2 = Owners.GetWeaponDamage(Owners.ActiveItem());
            float weaponKnockback2 = Owners.ActiveItem().knockBack;
            if (Time % 15 == 0 && toMou != Vector2.Zero) {
                bool haveAmmo = Owners.PickAmmo(Owners.ActiveItem(), out ArrowTypes, out scaleFactor11, out weaponDamage2, out weaponKnockback2, out _);
                weaponKnockback2 = Owners.GetWeaponKnockback(Owners.ActiveItem(), weaponKnockback2);

                if (haveAmmo) {
                    if (CalamityUtils.CheckWoodenAmmo(ArrowTypes, Owners)) {
                        int types = ModContent.ProjectileType<FateCluster>();
                        for (int i = 0; i < 4; i++) {
                            Vector2 vr = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * Main.rand.Next(8, 18);
                            Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                            int doms = Projectile.NewProjectile(
                                Projectile.parent(),
                                pos,
                                vr,
                                types,
                                weaponDamage2,
                                weaponKnockback2,
                                Owners.whoAmI
                                );
                            Projectile newDoms = Main.projectile[doms];
                            newDoms.DamageType = DamageClass.Ranged;
                            newDoms.timeLeft = 120;
                            newDoms.ai[0] = 1;
                        }
                    }
                    else {
                        for (int i = 0; i < 4; i++) {
                            Vector2 vr = Projectile.rotation.ToRotationVector2() * Main.rand.Next(20, 30);
                            Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                            Projectile.NewProjectile(
                                Projectile.parent(),
                                pos,
                                vr,
                                ArrowTypes,
                                weaponDamage2,
                                weaponKnockback2,
                                Owners.whoAmI
                                );
                        }

                        Projectile.NewProjectile(
                                Projectile.parent(),
                                Projectile.Center,
                                Projectile.rotation.ToRotationVector2() * 18,
                                ModContent.ProjectileType<DaemonsFlameArrow>(),
                                weaponDamage2 * 2,
                                weaponKnockback2,
                                Owners.whoAmI
                                );
                    }
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
            if (Owners.PressKey()) {
                Projectile.timeLeft = 2;
                Owners.itemTime = 2;
                Owners.itemAnimation = 2;
                float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owners.direction;
                Owners.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
            }
            Projectile.position = Owners.RotatedRelativePoint(Owners.MountedCenter, true) - Projectile.Size / 2f + toMou.UnitVector() * 16;
            Projectile.rotation = toMou.ToRotation();
            Projectile.spriteDirection = Projectile.direction = Math.Sign(toMou.X);
            Owners.ChangeDir(Projectile.direction);
            Owners.heldProj = Projectile.whoAmI;
        }

        private void DrawBow(Texture2D value, Color lightColor) {
            Main.EntitySpriteDraw(
                value,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(value, Projectile.frameCounter, 4),
                lightColor,
                Projectile.rotation,
                CWRUtils.GetOrig(value, 4),
                Projectile.scale,
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically
                );
        }

        public override void PostDraw(Color lightColor) {
            Texture2D men = CWRUtils.GetT2DValue(Texture + "Glow");
            DrawBow(men, Color.White);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Texture2D fire = CWRUtils.GetT2DValue(Texture + "Fire");
            Vector2 rotVr = Projectile.rotation.ToRotationVector2();
            Main.EntitySpriteDraw(
                fire,
                Projectile.Center - Main.screenPosition + rotVr * 20,
                CWRUtils.GetRec(fire, Projectile.frameCounter, 4),
                Color.White,
                0,
                CWRUtils.GetOrig(fire, 4) + new Vector2(0, 12),
                Projectile.scale,
                Owners.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally
                );
            DrawBow(mainValue, lightColor);
            return false;
        }

        public override bool? CanDamage() => false;
    }
}
