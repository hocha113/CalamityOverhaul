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
    internal class RDevastation : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Devastation>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<DevastationHeld>();
    }

    internal class DevastationHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Devastation>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "CatastropheClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 3;
            Length = 82;
            SwingData.starArg = 68;
            SwingData.baseSwingSpeed = 3.5f;
            ShootSpeed = 12;
            CuttingFrmeInterval = 5;
            AnimationMaxFrme = 11;
            ShootSpeed = 11;
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3 * UpdateRate)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy);
            }
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            Vector2 position = ShootSpanPos;
            Vector2 velocity = ShootVelocity;
            int type = GetRandomProjectileType();
            int damage = Projectile.damage;
            float knockback = Projectile.knockBack;
            Player player = Owner;
            Projectile.NewProjectile(Source, position.X, position.Y, velocity.X, velocity.Y, type, damage, knockback, Main.myPlayer);

            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            Vector2 mouseDist = GetMouseDistance(realPlayerPos, player.gravDir);
            float projSpeed = Item.shootSpeed;
            float mouseDistance = projSpeed / mouseDist.Length();
            mouseDist *= mouseDistance;

            for (int i = 0; i < 4; i++) {
                Vector2 spawnPos = GetRandomSpawnPosition(player, i);
                Vector2 spawnDist = GetMouseDistance(spawnPos, 1f);
                spawnDist = AdjustDistanceForSpeed(spawnDist, projSpeed);

                for (int j = 0; j < 3; j++) {
                    float speedX = spawnDist.X + Main.rand.NextFloat(-0.8f, 0.8f);
                    float speedY = spawnDist.Y + Main.rand.NextFloat(-0.8f, 0.8f);
                    int projectileType = GetProjectileTypeByIndex(j);
                    Projectile.NewProjectile(Source, spawnPos.X, spawnPos.Y, speedX, speedY, projectileType, damage / 2, knockback, player.whoAmI, 0f, Main.rand.Next(5 - j * 2));
                }
            }
        }

        private int GetRandomProjectileType() {
            switch (Main.rand.Next(6)) {
                case 1: return ModContent.ProjectileType<GalaxyBlast>();
                case 2: return ModContent.ProjectileType<GalaxyBlastType2>();
                case 3: return ModContent.ProjectileType<GalaxyBlastType3>();
                default: return ModContent.ProjectileType<GalaxyBlast>();
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
            float spawnY = player.MountedCenter.Y - 600f - 100 * index;
            spawnX = (spawnX + player.Center.X) / 2f + Main.rand.Next(-200, 201);
            return new Vector2(spawnX, spawnY);
        }

        private Vector2 AdjustDistanceForSpeed(Vector2 distance, float speed) {
            distance.Y = Math.Abs(distance.Y) < 20f ? 20f : distance.Y;
            float distanceLength = distance.Length();
            return distance * (speed / distanceLength);
        }

        private int GetProjectileTypeByIndex(int index) {
            switch (index) {
                case 0: return ModContent.ProjectileType<GalaxyBlast>();
                case 1: return ModContent.ProjectileType<GalaxyBlastType2>();
                case 2: return ModContent.ProjectileType<GalaxyBlastType3>();
                default: return ModContent.ProjectileType<GalaxyBlast>();
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 60);
            target.AddBuff(BuffID.OnFire, 180);
            target.AddBuff(BuffID.Frostburn, 120);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Ichor, 60);
            target.AddBuff(BuffID.OnFire, 180);
            target.AddBuff(BuffID.Frostburn, 120);
        }
    }
}
