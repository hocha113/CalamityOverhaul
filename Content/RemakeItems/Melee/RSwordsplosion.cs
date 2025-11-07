using CalamityOverhaul.Content.MeleeModify.Core;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSwordsplosion : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<SwordsplosionHeld>();
    }

    internal class SwordsplosionHeld : BaseKnife
    {
        public override int TargetID => CWRItemOverride.GetCalItemID("Swordsplosion");
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
            ShootSpeed = 16;
        }

        public override void Shoot() {
            int type = CWRRef.GetRandomProjectileType();
            int damage = Projectile.damage;
            float knockback = Projectile.knockBack;
            float projSpeed = Main.rand.Next(22, 30);

            for (int i = 0; i < 16; i++) {
                Vector2 spawnPos = GetRandomSpawnPosition(Owner, i);
                Vector2 spawnDist = GetMouseDistance(spawnPos, 1f);
                spawnDist = AdjustDistanceForSpeed(spawnDist, projSpeed);

                float speedX = spawnDist.X + Main.rand.NextFloat(-7.2f, 7.2f);
                float speedY = spawnDist.Y + Main.rand.NextFloat(-7.2f, 7.2f);
                Projectile.NewProjectile(Source, spawnPos.X, spawnPos.Y, speedX, speedY, type, damage, knockback, Owner.whoAmI, 2f, 0f);
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

        public override bool PreInOwner() {
            if (Main.rand.NextBool(5 * UpdateRate)) {
                int swingDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.RainbowTorch, Owner.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
                Main.dust[swingDust].noGravity = true;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }
    }
}
