using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishMud : FishSkill
    {
        public override int UnlockFishID => ItemID.Mudfish;
        public override int DefaultCooldown => 60;
        private const int MaxMudfishSentries = 5;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Active(player)) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            if (Cooldown <= 0) {
                SetCooldown();
                int existingCount = CountActiveSentries(player);
                int maxCount = MaxMudfishSentries + HalibutData.GetDomainLayer() * 2;

                if (existingCount < maxCount) {
                    SpawnMudfishSentry(player, source, damage, knockback);
                }
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private void SpawnMudfishSentry(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            NPC target = FindNearestEnemy(player);
            if (target == null) return;

            Vector2 spawnPos = FindValidGroundPosition(player, target);
            if (spawnPos == Vector2.Zero) {
                spawnPos = player.Bottom;
            }

            Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<MudfishSentry>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.15f)),
                knockback * 0.8f,
                player.whoAmI,
                0
            );
        }

        private Vector2 FindValidGroundPosition(Player player, NPC target) {
            Vector2 targetPos = target.Center;
            Vector2 dirToTarget = (targetPos - player.Center).SafeNormalize(Vector2.Zero);

            for (int attempt = 0; attempt < 10; attempt++) {
                float distance = Main.rand.NextFloat(200f, 400f);
                float angleOffset = Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 testDir = dirToTarget.RotatedBy(angleOffset);
                Vector2 testPos = player.Center + testDir * distance;

                for (int y = 0; y < 50; y++) {
                    Vector2 checkPos = testPos + new Vector2(0, y * 16);
                    Point tilePos = checkPos.ToTileCoordinates();

                    if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                        Tile tile = Main.tile[tilePos.X, tilePos.Y];
                        if (tile.HasSolidTile()) {
                            return new Vector2(checkPos.X, tilePos.Y * 16 - 16);
                        }
                    }
                }
            }

            return Vector2.Zero;
        }

        private NPC FindNearestEnemy(Player player) {
            NPC closest = null;
            float closestDist = 1200f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(player.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        private int CountActiveSentries(Player player) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<MudfishSentry>()) {
                    count++;
                }
            }
            return count;
        }
    }

    internal class MudfishSentry : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Mudfish;

        private enum SentryState
        {
            Rising,
            Emerging,
            Idle,
            Attacking,
            TurningDown,
            Submerging
        }

        private SentryState State {
            get => (SentryState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TargetID => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        private float emergingProgress = 0f;
        private float bodyWiggle = 0f;
        private float mouthOpenness = 0f;
        private int attackCooldown = 0;
        private int shotsFired = 0;
        private bool isUnderground = false;
        private float targetRotation = 0f;

        private const int RisingDuration = 20;
        private const int EmergeDuration = 30;
        private const int IdleDuration = 40;
        private const int AttackDuration = 120;
        private const int TurningDownDuration = 20;
        private const int SubmergeDuration = 30;
        private const int AttackCooldownMax = 25;
        private const int ShotCount = 4;
        private const float RisingSpeed = 4f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = false;
        }

        public override void AI() {
            if (StateTimer == 0 && State == SentryState.Rising) {
                isUnderground = CheckIfUnderground();

                if (!isUnderground) {
                    State = SentryState.Emerging;
                    StateTimer = 0;
                }
            }

            StateTimer++;
            bodyWiggle += 0.12f;

            if (Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                Projectile.position.Y -= 8f;
            }

            switch (State) {
                case SentryState.Rising:
                    RisingPhase();
                    break;
                case SentryState.Emerging:
                    EmergingPhase();
                    break;
                case SentryState.Idle:
                    IdlePhase();
                    break;
                case SentryState.Attacking:
                    AttackingPhase();
                    break;
                case SentryState.TurningDown:
                    TurningDownPhase();
                    break;
                case SentryState.Submerging:
                    SubmergingPhase();
                    break;
            }

            Projectile.velocity = Vector2.Zero;

            float mudLight = 0.5f + (float)Math.Sin(bodyWiggle * 2f) * 0.2f;
            Lighting.AddLight(Projectile.Center, mudLight * 0.6f, mudLight * 0.5f, mudLight * 0.3f);
        }

        private bool CheckIfUnderground() {
            Point tilePos = Projectile.Center.ToTileCoordinates();

            for (int y = tilePos.Y; y >= tilePos.Y - 20; y--) {
                if (WorldGen.InWorld(tilePos.X, y)) {
                    Tile tile = Main.tile[tilePos.X, y];
                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType]) {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsOnSurface() {
            Point tilePos = Projectile.Center.ToTileCoordinates();

            for (int y = tilePos.Y; y >= tilePos.Y - 3; y--) {
                if (WorldGen.InWorld(tilePos.X, y)) {
                    Tile tile = Main.tile[tilePos.X, y];
                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType]) {
                        return true;
                    }
                }
            }
            return false;
        }

        private void RisingPhase() {
            Projectile.Center += new Vector2(0, -RisingSpeed);

            emergingProgress = Math.Min(StateTimer / RisingDuration, 0.3f);

            if (StateTimer % 4 == 0) {
                SpawnRisingDust();
            }

            if (IsOnSurface() || StateTimer >= RisingDuration * 3) {
                State = SentryState.Emerging;
                StateTimer = 0;
            }
        }

        private void EmergingPhase() {
            if (StateTimer == 1) {
                SpawnEmergeDust();
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.8f,
                    Pitch = -0.3f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.6f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }

            emergingProgress = Math.Min(StateTimer / EmergeDuration, 1f);
            emergingProgress = EaseOutElastic(emergingProgress);

            if (StateTimer % 2 == 0) {
                SpawnEmergeDust();
            }

            if (StateTimer >= EmergeDuration) {
                State = SentryState.Idle;
                StateTimer = 0;
                emergingProgress = 1f;
            }
        }

        private void IdlePhase() {
            mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.15f);
            targetRotation = MathHelper.Lerp(targetRotation, 0f, 0.1f);
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);

            NPC target = FindTarget();
            if (target != null) {
                State = SentryState.Attacking;
                StateTimer = 0;
                shotsFired = 0;
                TargetID = target.whoAmI;
                return;
            }

            if (StateTimer >= IdleDuration) {
                State = SentryState.TurningDown;
                StateTimer = 0;
            }

            if (StateTimer % 15 == 0) {
                SpawnIdleBubble();
            }
        }

        private void AttackingPhase() {
            if (Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                return;
            }

            if (attackCooldown > 0) {
                attackCooldown--;
                mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.15f);
            }

            NPC target = GetTarget();
            if (target == null || !target.active) {
                State = SentryState.Idle;
                StateTimer = 0;
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;
            targetRotation = toTarget.ToRotation();
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.2f);

            if (attackCooldown == 0 && shotsFired < ShotCount) {
                ShootMudBall(target);
                attackCooldown = AttackCooldownMax;
                shotsFired++;
                mouthOpenness = 1f;
            }

            if (shotsFired >= ShotCount || StateTimer >= AttackDuration) {
                State = SentryState.TurningDown;
                StateTimer = 0;
            }

            if (StateTimer % 4 == 0) {
                SpawnAttackDust();
            }
        }

        private void TurningDownPhase() {
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.5f,
                    Pitch = -0.6f
                }, Projectile.Center);
            }

            mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.2f);

            targetRotation = MathHelper.PiOver2;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);

            if (StateTimer % 3 == 0) {
                SpawnTurningDust();
            }

            if (StateTimer >= TurningDownDuration) {
                State = SentryState.Submerging;
                StateTimer = 0;
            }
        }

        private void SubmergingPhase() {
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.7f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }

            Projectile.Center += new Vector2(0, 12.5f);

            emergingProgress = Math.Max(1f - StateTimer / SubmergeDuration, 0f);

            if (StateTimer >= SubmergeDuration) {
                Projectile.Kill();
            }
        }

        private void ShootMudBall(NPC target) {
            if (Projectile.owner != Main.myPlayer) return;

            Player owner = Main.player[Projectile.owner];
            float shootAngle = Projectile.rotation;
            Vector2 shootDirection = shootAngle.ToRotationVector2();
            Vector2 shootPos = Projectile.Center + shootDirection * 15f * emergingProgress;

            Vector2 toTarget = target.Center - shootPos;
            float distance = toTarget.Length();

            toTarget = toTarget.SafeNormalize(Vector2.Zero);

            float gravity = 0.25f;
            float speed = Math.Min(distance / 25f, 18f);

            Vector2 velocity = toTarget * speed;

            if (distance > 100f) {
                float time = distance / speed;
                float dropCompensation = 0.5f * gravity * time * time / distance;
                velocity.Y -= dropCompensation * speed;
            }

            velocity = velocity.RotatedByRandom(0.08f);

            Projectile.NewProjectile(
                owner.GetSource_FromThis(),
                shootPos,
                velocity,
                ModContent.ProjectileType<MudBall>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );

            SoundEngine.PlaySound(SoundID.Item85 with {
                Volume = 0.6f,
                Pitch = -0.2f
            }, shootPos);

            for (int i = 0; i < 10; i++) {
                Vector2 particleVel = velocity.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.4f, 0.9f);
                Dust shoot = Dust.NewDustPerfect(
                    shootPos,
                    DustID.Mud,
                    particleVel,
                    100,
                    new Color(100, 80, 60),
                    Main.rand.NextFloat(1.4f, 2.2f)
                );
                shoot.noGravity = Main.rand.NextBool();
            }
        }

        private NPC FindTarget() {
            float range = 700f + HalibutData.GetDomainLayer() * 100f;
            NPC closest = null;
            float closestDist = range;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        private NPC GetTarget() {
            int id = (int)TargetID;
            if (id < 0 || id >= Main.maxNPCs) return null;

            NPC target = Main.npc[id];
            if (!target.active || !target.CanBeChasedBy()) return null;

            return target;
        }

        private void SpawnRisingDust() {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(10f, 20f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(1f, 3f));

                Dust rising = Dust.NewDustPerfect(
                    pos,
                    DustID.Mud,
                    vel,
                    100,
                    new Color(85, 65, 50),
                    Main.rand.NextFloat(1.3f, 2f)
                );
                rising.noGravity = true;
                rising.fadeIn = 0.6f;
            }
        }

        private void SpawnEmergeDust() {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-25f, 25f), Main.rand.NextFloat(0, 12f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -3f));

                Dust emerge = Dust.NewDustPerfect(
                    pos,
                    DustID.Mud,
                    vel,
                    100,
                    new Color(90, 70, 50),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                emerge.noGravity = Main.rand.NextBool();
            }

            if (Main.rand.NextBool(2)) {
                Dust chunk = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(20, 8),
                    40, 16,
                    DustID.Mud,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                chunk.velocity = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-7f, -4f));
            }
        }

        private void SpawnTurningDust() {
            Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-10f, 10f));

            Dust turning = Dust.NewDustPerfect(
                pos,
                DustID.Mud,
                Main.rand.NextVector2Circular(2f, 2f),
                100,
                new Color(95, 75, 60),
                Main.rand.NextFloat(1f, 1.8f)
            );
            turning.noGravity = true;
            turning.fadeIn = 0.7f;
        }

        private void SpawnIdleBubble() {
            Vector2 bubblePos = Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), -20f * emergingProgress);

            Dust bubble = Dust.NewDustPerfect(
                bubblePos,
                DustID.TintableDust,
                new Vector2(0, -1.2f),
                100,
                new Color(140, 160, 180, 120),
                Main.rand.NextFloat(1f, 1.5f)
            );
            bubble.noGravity = true;
            bubble.fadeIn = 0.8f;
        }

        private void SpawnAttackDust() {
            Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-15f, 15f), -12f * emergingProgress);

            Dust attack = Dust.NewDustPerfect(
                pos,
                DustID.Mud,
                Main.rand.NextVector2Circular(2f, 2f),
                100,
                new Color(110, 90, 70),
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            attack.noGravity = true;
        }

        private static float EaseOutElastic(float x) {
            const float c4 = (2f * MathF.PI) / 3f;
            return x == 0 ? 0 : x == 1 ? 1 :
                (float)(Math.Pow(2, -10 * x) * Math.Sin((x * 10 - 0.75) * c4) + 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(ItemID.Mudfish);
            Texture2D texture = TextureAssets.Item[ItemID.Mudfish].Value;

            if (texture == null) return false;

            Vector2 drawOrigin = Projectile.Center - Main.screenPosition;
            Vector2 fishOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            float drawScale = emergingProgress * 1.2f;
            float yOffset = (1f - emergingProgress) * texture.Height;
            Vector2 drawPos = drawOrigin - new Vector2(0, yOffset);

            float wiggleRotation = (float)Math.Sin(bodyWiggle) * 0.1f * emergingProgress;
            float totalRotation = Projectile.rotation + wiggleRotation + MathHelper.PiOver4;

            if (State == SentryState.TurningDown || State == SentryState.Submerging) {
                wiggleRotation *= 0.5f;
            }

            Color mudColor = lightColor;
            mudColor = Color.Lerp(mudColor, new Color(100, 80, 60), 0.4f);

            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 3f;
                Vector2 shadowPos = drawPos + new Vector2((float)Math.Sin(bodyWiggle + i) * 3f, shadowOffset);
                Color shadowColor = new Color(50, 40, 30, 100) * (1f - i * 0.3f) * emergingProgress;

                sb.Draw(
                    texture,
                    shadowPos,
                    null,
                    shadowColor,
                    totalRotation,
                    fishOrigin,
                    drawScale,
                    SpriteEffects.None,
                    0
                );
            }

            sb.Draw(
                texture,
                drawPos,
                null,
                mudColor,
                totalRotation,
                fishOrigin,
                drawScale,
                SpriteEffects.None,
                0
            );

            if (State == SentryState.Attacking && mouthOpenness > 0.3f) {
                Color glowColor = new Color(160, 120, 80) * (mouthOpenness * 0.8f);
                sb.Draw(
                    texture,
                    drawPos,
                    null,
                    glowColor,
                    totalRotation,
                    fishOrigin,
                    drawScale * 1.08f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }

    internal class MudBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float rotation = 0f;
        private float scale = 1f;
        private const float Gravity = 0.25f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void AI() {
            Projectile.velocity.Y += Gravity;
            rotation += Projectile.velocity.Length() * 0.06f;

            scale = 1f + (float)Math.Sin(Projectile.timeLeft * 0.15f) * 0.12f;

            if (Projectile.timeLeft % 2 == 0) {
                SpawnTrailDust();
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.18f);
        }

        private void SpawnTrailDust() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                DustID.Mud,
                -Projectile.velocity * 0.25f,
                100,
                new Color(90, 75, 55),
                Main.rand.NextFloat(1f, 1.6f)
            );
            trail.noGravity = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.Item50 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);

            SpawnSplatDust();

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                return true;
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                Projectile.velocity.X = -oldVelocity.X * 0.4f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 180);

            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.6f,
                Pitch = -0.4f
            }, Projectile.Center);

            SpawnSplatDust();
        }

        private void SpawnSplatDust() {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust splat = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Mud,
                    vel,
                    100,
                    new Color(85, 70, 50),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                splat.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.instance.LoadItem(ItemID.MudBlock);
            Texture2D mudTexture = TextureAssets.Item[ItemID.MudBlock].Value;

            Color mudColor = Projectile.GetAlpha(lightColor);
            mudColor = Color.Lerp(mudColor, new Color(100, 80, 60), 0.5f);

            for (int i = 0; i < 3; i++) {
                float angle = rotation + i * MathHelper.TwoPi / 3f;
                Vector2 offset = angle.ToRotationVector2() * 3f;

                sb.Draw(
                    mudTexture,
                    drawPos + offset,
                    null,
                    mudColor * 0.7f,
                    rotation,
                    mudTexture.Size() / 2f,
                    scale * 0.28f,
                    SpriteEffects.None,
                    0
                );
            }

            sb.Draw(
                mudTexture,
                drawPos,
                null,
                mudColor,
                rotation,
                mudTexture.Size() / 2f,
                scale * 0.35f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
