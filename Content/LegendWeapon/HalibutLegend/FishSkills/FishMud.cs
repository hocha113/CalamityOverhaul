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
        public override int DefaultCooldown => 300;
        private const int MaxMudfishSentries = 8;

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
            if (spawnPos == Vector2.Zero) return;

            Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<MudfishSentry>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.15f)),
                knockback * 0.8f,
                player.whoAmI,
                target.whoAmI
            );
        }

        private Vector2 FindValidGroundPosition(Player player, NPC target) {
            Vector2 targetPos = target.Center;
            Vector2 dirToTarget = (targetPos - player.Center).SafeNormalize(Vector2.Zero);
            
            for (int attempt = 0; attempt < 10; attempt++) {
                float distance = Main.rand.NextFloat(200f, 400f);
                float angleOffset = Main.rand.NextFloat(-0.5f, 0.5f);
                Vector2 testDir = dirToTarget.RotatedBy(angleOffset);
                Vector2 testPos = player.Center + testDir * distance;

                for (int y = 0; y < 50; y++) {
                    Vector2 checkPos = testPos + new Vector2(0, y * 16);
                    Point tilePos = checkPos.ToTileCoordinates();

                    if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                        Tile tile = Main.tile[tilePos.X, tilePos.Y];
                        if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                            return new Vector2(checkPos.X, tilePos.Y * 16 - 8);
                        }
                    }
                }
            }

            return Vector2.Zero;
        }

        private NPC FindNearestEnemy(Player player) {
            NPC closest = null;
            float closestDist = 1000f;

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
            Emerging,
            Idle,
            Attacking,
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
        private const int EmergeDuration = 45;
        private const int IdleDuration = 60;
        private const int AttackDuration = 90;
        private const int SubmergeDuration = 35;
        private const int AttackCooldownMax = 30;
        private const int ShotCount = 3;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 15;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            StateTimer++;
            bodyWiggle += 0.12f;

            switch (State) {
                case SentryState.Emerging:
                    EmergingPhase();
                    break;
                case SentryState.Idle:
                    IdlePhase();
                    break;
                case SentryState.Attacking:
                    AttackingPhase();
                    break;
                case SentryState.Submerging:
                    SubmergingPhase();
                    break;
            }

            Projectile.velocity = Vector2.Zero;
            
            float mudLight = 0.3f + (float)Math.Sin(bodyWiggle * 2f) * 0.1f;
            Lighting.AddLight(Projectile.Center, mudLight * 0.4f, mudLight * 0.35f, mudLight * 0.2f);
        }

        private void EmergingPhase() {
            if (StateTimer == 1) {
                SpawnEmergeDust();
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.7f,
                    Pitch = -0.3f
                }, Projectile.Center);
                
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.5f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }

            emergingProgress = Math.Min(StateTimer / (float)EmergeDuration, 1f);
            emergingProgress = EaseOutElastic(emergingProgress);

            if (StateTimer % 3 == 0) {
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

            NPC target = FindTarget();
            if (target != null) {
                State = SentryState.Attacking;
                StateTimer = 0;
                TargetID = target.whoAmI;
                return;
            }

            if (StateTimer >= IdleDuration) {
                State = SentryState.Submerging;
                StateTimer = 0;
            }

            if (StateTimer % 20 == 0) {
                SpawnIdleBubble();
            }
        }

        private void AttackingPhase() {
            if (attackCooldown > 0) {
                attackCooldown--;
                mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.2f);
            }

            NPC target = GetTarget();
            if (target == null || !target.active) {
                State = SentryState.Idle;
                StateTimer = 0;
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, toTarget.ToRotation(), 0.15f);

            if (attackCooldown == 0 && StateTimer % (AttackDuration / ShotCount) < 2) {
                ShootMudBall(target);
                attackCooldown = AttackCooldownMax;
                mouthOpenness = 1f;
            }

            if (StateTimer >= AttackDuration) {
                State = SentryState.Submerging;
                StateTimer = 0;
            }

            if (StateTimer % 5 == 0) {
                SpawnAttackDust();
            }
        }

        private void SubmergingPhase() {
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.6f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }

            emergingProgress = Math.Max(1f - StateTimer / (float)SubmergeDuration, 0f);
            emergingProgress = EaseInQuad(emergingProgress);

            if (StateTimer % 2 == 0) {
                SpawnSubmergeDust();
            }

            if (StateTimer >= SubmergeDuration) {
                Projectile.Kill();
            }
        }

        private void ShootMudBall(NPC target) {
            if (Projectile.owner != Main.myPlayer) return;

            Player owner = Main.player[Projectile.owner];
            Vector2 shootPos = Projectile.Center + new Vector2(0, -20f * emergingProgress);
            Vector2 toTarget = target.Center - shootPos;
            float distance = toTarget.Length();
            
            toTarget = toTarget.SafeNormalize(Vector2.Zero);
            
            float gravity = 0.3f;
            float speed = Math.Min(distance / 30f, 16f);
            float angle = (float)Math.Atan2(toTarget.Y, toTarget.X);
            
            if (distance > 100f) {
                float time = distance / speed;
                float yOffset = 0.5f * gravity * time * time;
                angle = (float)Math.Atan2(toTarget.Y - yOffset / distance, toTarget.X);
            }

            Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
            velocity = velocity.RotatedByRandom(0.12f);

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
                Volume = 0.5f,
                Pitch = -0.2f
            }, shootPos);

            for (int i = 0; i < 8; i++) {
                Vector2 particleVel = velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.3f, 0.8f);
                Dust shoot = Dust.NewDustPerfect(
                    shootPos,
                    DustID.Mud,
                    particleVel,
                    100,
                    new Color(100, 80, 60),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                shoot.noGravity = Main.rand.NextBool();
            }
        }

        private NPC FindTarget() {
            float range = 600f + HalibutData.GetDomainLayer() * 50f;
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

        private void SpawnEmergeDust() {
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(0, 10f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -2f));
                
                Dust emerge = Dust.NewDustPerfect(
                    pos,
                    DustID.Mud,
                    vel,
                    100,
                    new Color(90, 70, 50),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                emerge.noGravity = Main.rand.NextBool();
            }

            if (Main.rand.NextBool(3)) {
                Dust chunk = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(15, 5),
                    30, 10,
                    DustID.Mud,
                    Scale: Main.rand.NextFloat(2f, 3f)
                );
                chunk.velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -3f));
            }
        }

        private void SpawnSubmergeDust() {
            Vector2 pos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(-5f, 5f));
            Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(1f, 3f));
            
            Dust submerge = Dust.NewDustPerfect(
                pos,
                DustID.Mud,
                vel,
                100,
                new Color(80, 65, 45),
                Main.rand.NextFloat(1.2f, 2f)
            );
            submerge.noGravity = false;
        }

        private void SpawnIdleBubble() {
            Vector2 bubblePos = Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -15f * emergingProgress);
            
            Dust bubble = Dust.NewDustPerfect(
                bubblePos,
                DustID.TintableDust,
                new Vector2(0, -1f),
                100,
                new Color(120, 140, 160, 100),
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            bubble.noGravity = true;
            bubble.fadeIn = 0.6f;
        }

        private void SpawnAttackDust() {
            Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), -10f * emergingProgress);
            
            Dust attack = Dust.NewDustPerfect(
                pos,
                DustID.Mud,
                Main.rand.NextVector2Circular(1.5f, 1.5f),
                100,
                new Color(110, 90, 70),
                Main.rand.NextFloat(0.9f, 1.5f)
            );
            attack.noGravity = true;
        }

        private float EaseOutElastic(float x) {
            const float c4 = (2f * MathF.PI) / 3f;
            return x == 0 ? 0 : x == 1 ? 1 : 
                (float)(Math.Pow(2, -10 * x) * Math.Sin((x * 10 - 0.75) * c4) + 1);
        }

        private float EaseInQuad(float x) {
            return x * x;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(ItemID.Mudfish);
            Texture2D texture = TextureAssets.Item[ItemID.Mudfish].Value;
            
            Vector2 drawOrigin = Projectile.Bottom - Main.screenPosition;
            Vector2 fishOrigin = texture.Size() / 2f;
            fishOrigin.Y = texture.Height;

            float drawScale = emergingProgress;
            float yOffset = (1f - emergingProgress) * texture.Height;
            Vector2 drawPos = drawOrigin - new Vector2(0, yOffset);

            float wiggleRotation = (float)Math.Sin(bodyWiggle) * 0.08f * emergingProgress;
            float totalRotation = Projectile.rotation + wiggleRotation;

            Color mudColor = lightColor;
            mudColor = Color.Lerp(mudColor, new Color(100, 80, 60), 0.3f);

            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 2f;
                Vector2 shadowPos = drawPos + new Vector2((float)Math.Sin(bodyWiggle + i) * 2f, shadowOffset);
                Color shadowColor = new Color(60, 50, 40, 80) * (1f - i * 0.25f) * emergingProgress;

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
                Color glowColor = new Color(140, 100, 60) * (mouthOpenness * 0.7f);
                sb.Draw(
                    texture,
                    drawPos,
                    null,
                    glowColor,
                    totalRotation,
                    fishOrigin,
                    drawScale * 1.05f,
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
        private const float Gravity = 0.3f;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Projectile.velocity.Y += Gravity;
            rotation += Projectile.velocity.Length() * 0.05f;
            
            scale = 1f + (float)Math.Sin(Projectile.timeLeft * 0.2f) * 0.1f;

            if (Projectile.velocity.Y > 0 && Projectile.timeLeft % 3 == 0) {
                SpawnTrailDust();
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.15f);
        }

        private void SpawnTrailDust() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                DustID.Mud,
                -Projectile.velocity * 0.2f,
                100,
                new Color(90, 75, 55),
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            trail.noGravity = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.Item50 with {
                Volume = 0.4f,
                Pitch = -0.3f
            }, Projectile.Center);

            SpawnSplatDust();
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);
            
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, Projectile.Center);

            SpawnSplatDust();
        }

        private void SpawnSplatDust() {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust splat = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Mud,
                    vel,
                    100,
                    new Color(85, 70, 50),
                    Main.rand.NextFloat(1.2f, 2.2f)
                );
                splat.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            
            Texture2D mudTexture = TextureAssets.Item[ItemID.MudBlock].Value;
            
            Color mudColor = Projectile.GetAlpha(lightColor);
            mudColor = Color.Lerp(mudColor, new Color(100, 80, 60), 0.4f);

            for (int i = 0; i < 3; i++) {
                float angle = rotation + i * MathHelper.TwoPi / 3f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                
                sb.Draw(
                    mudTexture,
                    drawPos + offset,
                    null,
                    mudColor * 0.6f,
                    rotation,
                    mudTexture.Size() / 2f,
                    scale * 0.25f,
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
                scale * 0.3f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
