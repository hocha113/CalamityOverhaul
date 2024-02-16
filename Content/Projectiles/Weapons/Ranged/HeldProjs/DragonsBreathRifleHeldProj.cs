using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.CWRUtils;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DragonsBreathRifleHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "DragonsBreathRifle";

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.scale = 1;
            Projectile.damage = 588;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.hide = true;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void OnKill(int timeLeft) {
            if (Owner != null && Projectile.IsOwnedByLocalPlayer())
                Projectile.rotation = Owner.Center.To(Main.MouseWorld).ToRotation();
        }

        public override void OnSpawn(IEntitySource source) {
            if (Owner != null && Projectile.IsOwnedByLocalPlayer())
                Projectile.rotation = Owner.Center.To(Main.MouseWorld).ToRotation();
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.localAI[2]);
            writer.Write(toMou.X);
            writer.Write(toMou.Y);
            writer.Write(spanSmogsBool);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.localAI[0] = reader.ReadInt32();
            Projectile.localAI[1] = reader.ReadInt32();
            Projectile.localAI[2] = reader.ReadInt32();
            toMou.X = reader.ReadSingle();
            toMou.Y = reader.ReadSingle();
            spanSmogsBool = reader.ReadBoolean();
        }

        Player Owner => GetPlayerInstance(Projectile.owner);
        Vector2 toMou = Vector2.Zero;
        Vector2 oldMou = Vector2.Zero;
        bool spanSmogsBool = false;
        public override void AI() {
            ThisTimeValue++;
            Projectile.localAI[0]++;

            if (Owner == null || Owner.HeldItem?.type != ModContent.ItemType<DragonsBreathRifle>()) {
                Projectile.Kill();
                return;
            }

            else {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    if (Status == 0) {
                        if (Projectile.owner != Main.myPlayer) return;
                        if (PlayerInput.Triggers.Current.MouseLeft) Projectile.timeLeft = 2;
                        else Projectile.Kill();
                    }
                    if (Status == 1) {
                        if (Projectile.owner != Main.myPlayer) return;
                        if (PlayerInput.Triggers.Current.MouseRight) Projectile.timeLeft = 2;
                        else Projectile.Kill();
                    }
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                if (oldMou != Main.MouseWorld) {
                    toMou = Owner.Center.To(Main.MouseWorld);
                    Projectile.netUpdate = true;
                    oldMou = Main.MouseWorld;
                }
            }

            Owner.direction = toMou.X > 0 ? 1 : -1;
            Projectile.EntityToRot(toMou.ToRotation(), 0.2f);
            Vector2 rotOffset = Projectile.rotation.ToRotationVector2() * -30f;
            Projectile.Center = Owner.Center + rotOffset;
            Owner.heldProj = Projectile.whoAmI;

            Vector2 speed = Projectile.rotation.ToRotationVector2() * 12f;
            Vector2 offset = rotOffset;
            Vector2 shootPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 146;

            if (Status == 0) {
                if (ThisTimeValue % 60 == 0) {
                    Projectile.localAI[2] = 0;
                }

                if (ThisTimeValue % 30 == 0 && ThisTimeValue % 60 != 0 && !Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
                }

                if (Projectile.localAI[0] % 6 == 0 && Projectile.localAI[2] < 3) {
                    for (int i = 0; i < 2; i++) {
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Vector2 vr = (Projectile.rotation - MathHelper.ToRadians(Main.rand.NextFloat(80, 100)) * Owner.direction).ToRotationVector2() * Main.rand.NextFloat(3, 7) + Owner.velocity;
                            Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
                        }
                    }
                    spanSmogsBool = true;
                    ShootFire(shootPos);
                    SpawnGunDust(Projectile, shootPos, speed);
                    Projectile.rotation += MathHelper.ToRadians(-15) * Owner.direction;
                    Owner.velocity += speed * -0.2f;
                    SoundEngine.PlaySound(in SoundID.Item38, shootPos);
                    Projectile.localAI[2]++;
                    Projectile.netUpdate = true;
                }
            }
            if (Status == 1) {
                if (ThisTimeValue % 30 == 0) {
                    spanSmogsBool = true;
                    SpawnGunDust(Projectile, shootPos, speed);
                    SpawnSomgDust(shootPos, speed);
                    ShootFire2(shootPos);
                    SoundEngine.PlaySound(in SoundID.Item74, shootPos);
                    Projectile.netUpdate = true;
                }
            }
        }

        int fireType => ModContent.ProjectileType<DragonsBreathRound>();
        int fireCross => ModContent.ProjectileType<DragonFireRupture>();

        public void ShootFire(Vector2 shootPos) {
            if (Main.myPlayer != Projectile.owner) return;

            int AmmoTypes = ProjectileID.WoodenArrowFriendly;
            float scaleFactor11 = 14f;
            int weaponDamage2 = Owner.GetWeaponDamage(Owner.ActiveItem());
            float weaponKnockback2 = Owner.ActiveItem().knockBack;
            bool haveAmmo = Owner.PickAmmo(Owner.ActiveItem(), out AmmoTypes, out scaleFactor11, out weaponDamage2, out weaponKnockback2, out _, Main.rand.NextBool(3));
            weaponKnockback2 = Owner.GetWeaponKnockback(Owner.ActiveItem(), weaponKnockback2);

            if (haveAmmo) {
                for (int i = 0; i < 6; i++) {
                    float angleOffset = MathHelper.ToRadians(-3 + i);
                    Vector2 rotatedVel = (Projectile.rotation + angleOffset).ToRotationVector2() * Main.rand.Next(12, 15);
                    Projectile.NewProjectile(Owner.parent(), shootPos, rotatedVel, AmmoTypes, weaponDamage2, weaponKnockback2, Owner.whoAmI);
                    Projectile.NewProjectile(Projectile.parent(), shootPos, rotatedVel, fireType, (int)(weaponDamage2 * 1.2f), weaponKnockback2 * 1.3f, Owner.whoAmI);
                }
            }
        }

        public void ShootFire2(Vector2 shootPos) {
            if (Main.myPlayer != Projectile.owner) return;

            for (int i = 0; i < 3; i++) {
                float angleOffset = MathHelper.ToRadians(-6 + i * 6);
                Vector2 rotatedVel = (Projectile.rotation + angleOffset).ToRotationVector2() * 13f;
                Projectile.NewProjectile(Projectile.parent(), shootPos, rotatedVel, fireCross, (int)(Projectile.damage * 0.75f), Projectile.knockBack, Owner.whoAmI);
            }
        }

        public static void SpawnGunDust(Projectile projectile, Vector2 pos, Vector2 velocity, int splNum = 1) {
            if (Main.myPlayer != projectile.owner) return;

            pos += velocity.SafeNormalize(Vector2.Zero) * projectile.width * projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = 262;
                        break;
                    case 1:
                    case 2:
                        dustID = 54;
                        break;
                    default:
                        dustID = 53;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                if (Main.rand.NextBool(4)) {
                    dustVel = Vector2.Lerp(dustVel, -Vector2.UnitY * dustVel.Length(), Main.rand.NextFloat(0.6f, 0.85f)) * 0.9f;
                }
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        private void SpawnSomgDust(Vector2 pos, Vector2 velocity) {
            if (Main.myPlayer != Projectile.owner) return;

            Dust.NewDust(pos, 16, 16, DustID.Smoke);

            for (int i = 0; i < 66; i++) {
                Vector2 vr = (velocity.ToRotation() + MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))).ToRotationVector2() * Main.rand.Next(3, 16);
                Dust.NewDust(pos, 3, 3, DustID.Smoke, vr.X, vr.Y, 15);
                int dust = Dust.NewDust(pos, 3, 3, DustID.AmberBolt, vr.X, vr.Y, 15);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Owner == null) return false;

            SpriteEffects spriteEffects = toMou.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation;

            Texture2D mainValue = GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                WDEpos(Projectile.Center),
                null,
                Color.White,
                drawRot,
                new Vector2(13, mainValue.Height * 0.5f),
                Projectile.scale,
                spriteEffects,
                0
                );
            return false;
        }
    }
}
