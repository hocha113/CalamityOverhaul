using CalamityMod.Dusts;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SandVortexOfTheDecayedSea : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "SandVortexOfTheDecayedSea";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 308;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.mana = 6;
            Item.shoot = ModContent.ProjectileType<DecayedSeaOrb>();
            Item.shootSpeed = 6;
            Item.UseSound = SoundID.Item20;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 2, 0, 5);
            Item.SetHeldProj<SandVortexOfTheDecayedSeaHeld>();
        }
    }

    internal class SandVortexOfTheDecayedSeaHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "SandVortexOfTheDecayedSea";
        public override int TargetID => ModContent.ItemType<SandVortexOfTheDecayedSea>();
        public override void SetMagicProperty() {
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void PreInOwner() {
            if (fireIndex > 0) {
                fireIndex--;
            }
        }

        public override void FiringShoot() {
            SoundStyle sound = SoundID.NPCDeath13;
            SoundEngine.PlaySound(sound with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Vector2 ver = new Vector2(ShootVelocity.X * (0.6f + i * 0.12f), ShootVelocity.Y * Main.rand.NextFloat(0.6f, 1.2f));
                Projectile.NewProjectile(Source, ShootPos, ver, ModContent.ProjectileType<DecayedSeaOrb>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, UseAmmoItemType);
            }
        }

        public override void SetShootAttribute() => fireIndex = 30;

        public override bool PreGunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Color color = Color.GreenYellow;
            color.A = 0;
            float slp = 1 + 0.014f * fireIndex;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return true;
        }
    }

    internal class DecayedSeaOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "DecayedSeaOrb";
        private HashSet<NPC> onHitNPCs = [];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 6;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 500;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }
        public override void AI() {
            Projectile.velocity.Y += 0.01f;
            Projectile.rotation += Projectile.velocity.X * 0.01f;
            if (!Main.dedServ) {
                if (Main.rand.NextBool(5)) {
                    int dustnumber = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, 0f, 150, Color.Gold, 1f);
                    Main.dust[dustnumber].velocity *= 0.3f;
                    Main.dust[dustnumber].noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, Color.PaleGoldenrod.ToVector3() * 1.75f * Main.essScale);
            }

            if (Projectile.timeLeft < 200) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                NPC target = Projectile.Center.FindClosestNPC(1600, false, false, onHitNPCs);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1f, 0.3f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!onHitNPCs.Contains(target)) {
                onHitNPCs.Add(target);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.4f;
            }
            if (CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            CreateDustEffect((int)CalamityDusts.SulphurousSeaAcid, 120);
        }

        private void CreateDustEffect(int dustType, int amount) {
            for (int i = 0; i < amount; i++) {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , dustType, 0f, -2f, 0, default, 0.8f);
                Dust dust = Main.dust[dustIndex];
                dust.noGravity = true;
                dust.position.X += Main.rand.Next(-150, 151) * 0.05f - 1.5f;
                dust.position.Y += Main.rand.Next(-150, 151) * 0.05f - 1.5f;

                if (dust.position != Projectile.Center) {
                    dust.velocity = Projectile.DirectionTo(dust.position) * 6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = Projectile.rotation;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, drawRot, drawOrigin, Projectile.scale, spriteEffects, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, drawRot, drawOrigin, Projectile.scale, spriteEffects, 0);
            return false;
        }
    }
}
