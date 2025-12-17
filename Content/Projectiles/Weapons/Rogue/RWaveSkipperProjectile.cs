using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    public class RWaveSkipperProjectile : ModProjectile, ILocalizedModType
    {
        public override string Texture => "CalamityMod/Items/Weapons/Rogue/WaveSkipper";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public int Time = 0;
        public int TimeUnderground = 0;
        public bool PostExitTiles = false;
        public bool InitialTileHit = false;
        public bool InsideTiles = false;
        public Vector2 SavedOldVelocity;
        public Vector2 NPCDestination;
        public bool SetPierce = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.penetrate = 6;
            Projectile.DamageType = CWRRef.GetRogueDamageClass();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.tileCollide = true;
            Projectile.MaxUpdates = 2;
        }

        public override void AI() {
            if (!SetPierce) {
                SetProjectilePenetration();
            }

            UpdateProjectileRotation();
            UpdateDustEffects();

            if (!InitialTileHit && Time > 45) {
                SlowDownProjectile();
            }

            if (InitialTileHit && !InsideTiles && !PostExitTiles) {
                HandleTileHit();
            }

            if (PostExitTiles) {
                HandleExitTiles();
            }

            if (InsideTiles) {
                TrackNearestNPC();
                HandleUndergroundBehavior();
            }
        }

        private void SetProjectilePenetration() {
            Projectile.penetrate = Projectile.GetProjStealthStrike() ? 4 : 2;
            SetPierce = true;
        }

        private void UpdateProjectileRotation() {
            Time++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        private void UpdateDustEffects() {
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 20), Main.rand.NextBool() ? 288 : 207);
                dust.scale = Main.rand.NextFloat(0.3f, 0.55f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.5f;
            }
        }

        private void SlowDownProjectile() {
            if (Projectile.velocity.Y < 0) {
                Projectile.velocity.Y *= 0.95f;
            }
            Projectile.velocity.Y += 0.05f;
            Projectile.velocity.X *= 0.99f;
        }

        private void HandleTileHit() {
            Projectile.extraUpdates = 4;

            if (!Projectile.GetProjStealthStrike()) {
                Projectile.timeLeft = 200;
            }

            PlayTileHitSound();
            CreateDustExplosion();
            PostExitTiles = true;
        }

        private void PlayTileHitSound() {
            SoundEngine.PlaySound(SoundID.NPCHit11 with { Volume = 1.3f, Pitch = 1.1f }, Projectile.Center);
        }

        private void CreateDustExplosion() {
            for (int i = 0; i <= 55; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 6.5f, Main.rand.NextBool() ? 207 : 216
                    , Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(30f)) * Main.rand.NextFloat(1.3f, 2.9f)
                    , 0, default, Main.rand.NextFloat(1.5f, 2.8f));
                dust.noGravity = true;
            }
        }

        private void HandleExitTiles() {
            Player Owner = Main.player[Projectile.owner];
            float playerDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 2 == 0 && playerDist < 1400f) {
                PRT_Sparkle spark = new PRT_Sparkle(Projectile.Center - Projectile.velocity * 8f
                    , -Projectile.velocity * 0.1f, Color.Yellow, Color.White, 0.2f, 22);
                PRTLoader.AddParticle(spark);
            }
        }

        private void TrackNearestNPC() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].CanBeChasedBy(Projectile.GetSource_FromThis(), false)) {
                    NPCDestination = Main.npc[i].Center + Main.npc[i].velocity * 5f;
                }
            }
        }

        private void HandleUndergroundBehavior() {
            TimeUnderground++;
            AddLightingEffect();
            PlayUndergroundSound();

            float returnSpeed = 10;
            float acceleration = 0.2f;

            if (TimeUnderground > 25) {
                AdjustProjectileVelocity(returnSpeed, acceleration);
            }
        }

        private void AddLightingEffect() {
            Vector3 DustLight = new Vector3(0.171f, 0.124f, 0.086f);
            Lighting.AddLight(Projectile.Center + Projectile.velocity, DustLight * 4);
        }

        private void PlayUndergroundSound() {
            if (Time % 15 == 0 && TimeUnderground < 120) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void AdjustProjectileVelocity(float returnSpeed, float acceleration) {
            float xDist = NPCDestination.X - Projectile.Center.X;
            float yDist = NPCDestination.Y - Projectile.Center.Y;
            float dist = (float)Math.Sqrt(xDist * xDist + yDist * yDist);
            dist = returnSpeed / dist;

            xDist *= dist;
            yDist *= dist;

            AdjustVelocityTowardsTarget(xDist, yDist, acceleration);
        }

        private void AdjustVelocityTowardsTarget(float xDist, float yDist, float acceleration) {
            if (Projectile.velocity.X < xDist) {
                Projectile.velocity.X = AdjustVelocity(Projectile.velocity.X, xDist, acceleration);
            }
            else if (Projectile.velocity.X > xDist) {
                Projectile.velocity.X = AdjustVelocity(Projectile.velocity.X, xDist, -acceleration);
            }

            if (Projectile.velocity.Y < yDist) {
                Projectile.velocity.Y = AdjustVelocity(Projectile.velocity.Y, yDist, acceleration);
            }
            else if (Projectile.velocity.Y > yDist) {
                Projectile.velocity.Y = AdjustVelocity(Projectile.velocity.Y, yDist, -acceleration);
            }
        }

        private float AdjustVelocity(float velocity, float target, float adjustment) {
            velocity += adjustment;
            if (velocity < 0f && target > 0f) {
                velocity += adjustment;
            }
            else if (velocity > 0f && target < 0f) {
                velocity -= adjustment;
            }
            return velocity;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SavedOldVelocity = oldVelocity;
            Projectile.tileCollide = false;
            if (!InitialTileHit) //Enter ground
            {
                for (int i = 0; i <= 25; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Projectile.velocity * 3, Main.rand.NextBool() ? 207 : 216, -Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(30f)) * Main.rand.NextFloat(0.3f, 0.9f), 0, default, Main.rand.NextFloat(1.3f, 1.8f));
                    dust.noGravity = true;
                }
                Projectile.velocity = oldVelocity * 0.7f;
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.5f, Pitch = 1.1f }, Projectile.Center);
                InitialTileHit = true;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, Projectile.GetProjStealthStrike() ? 4 : 2);
            return false;
        }
    }
}
