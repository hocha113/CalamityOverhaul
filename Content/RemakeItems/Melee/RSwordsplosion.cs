using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSwordsplosion : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Swordsplosion>();

        public override void SetDefaults(Item item) => item.SetKnifeHeld<SwordsplosionHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class SwordsplosionHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Swordsplosion>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Swordsplosion_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 90;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3;
            ShootSpeed = 11;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 16;
             
        }

        public override void Shoot() {
            Vector2 position = ShootSpanPos;
            Vector2 velocity = ShootVelocity;
            int type = GetRandomProjectileType();
            int damage = Projectile.damage;
            float knockback = Projectile.knockBack;
            Player player = Owner;

            float projSpeed = Main.rand.Next(22, 30);
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            Vector2 mouseDist = GetMouseDistance(realPlayerPos, player.gravDir);
            float mouseDistance = projSpeed / mouseDist.Length();
            mouseDist *= mouseDistance;

            for (int i = 0; i < 16; i++) {
                Vector2 spawnPos = GetRandomSpawnPosition(player, i);
                Vector2 spawnDist = GetMouseDistance(spawnPos, 1f);
                spawnDist = AdjustDistanceForSpeed(spawnDist, projSpeed);

                float speedX = spawnDist.X + Main.rand.NextFloat(-7.2f, 7.2f);
                float speedY = spawnDist.Y + Main.rand.NextFloat(-7.2f, 7.2f);
                Projectile.NewProjectile(Source, spawnPos.X, spawnPos.Y, speedX, speedY, type, damage, knockback, player.whoAmI, 2f, 0f);
            }
        }

        private int GetRandomProjectileType() {
            switch (Main.rand.Next(4)) {
                case 0: return ModContent.ProjectileType<SwordsplosionBlue>();
                case 1: return ModContent.ProjectileType<SwordsplosionGreen>();
                case 2: return ModContent.ProjectileType<SwordsplosionPurple>();
                case 3: return ModContent.ProjectileType<SwordsplosionRed>();
                default: return ModContent.ProjectileType<SwordsplosionBlue>();
            }
        }

        private Vector2 GetMouseDistance(Vector2 playerPos, float gravDir) {
            float mouseXDist = Main.mouseX + Main.screenPosition.X - playerPos.X;
            float mouseYDist = Main.mouseY + Main.screenPosition.Y - playerPos.Y;
            if (gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - playerPos.Y;
            }
            return new Vector2(mouseXDist, mouseYDist);
        }

        private Vector2 GetRandomSpawnPosition(Player player, int index) {
            float spawnX = player.position.X + player.width * 0.5f + Main.rand.Next(201) * -player.direction + (Main.mouseX + Main.screenPosition.X - player.position.X);
            float spawnY = player.MountedCenter.Y - 100 * index;
            spawnX = (spawnX + player.Center.X) / 2f + Main.rand.Next(-200, 201);
            return new Vector2(spawnX, spawnY);
        }

        private Vector2 AdjustDistanceForSpeed(Vector2 distance, float speed) {
            distance.Y = Math.Abs(distance.Y) < 20f ? 20f : distance.Y;
            float distanceLength = distance.Length();
            return distance * (speed / distanceLength);
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(5 * UpdateRate)) {
                int swingDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.RainbowTorch, Owner.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
                Main.dust[swingDust].noGravity = true;
            }
            return base.PreInOwnerUpdate();
        }
    }
}
