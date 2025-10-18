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
    internal class FishPeng : FishSkill
    {
        public override int UnlockFishID => ItemID.Pengfish;
        public override int DefaultCooldown => 60 * (8 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 18;
        private int spawnTimer = 0;
        private static int MaxActivePenguins => 3 + HalibutData.GetDomainLayer() / 2;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Active(player) || Cooldown > 0) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            if (++spawnTimer <= 3 + HalibutData.GetDomainLayer() / 2) {
                int existingCount = CountActivePenguins(player);
                int maxCount = MaxActivePenguins + HalibutData.GetDomainLayer() * 2;

                if (existingCount < maxCount) {
                    NPC target = FindNearestEnemy(player);
                    if (target != null) {
                        SpawnFallingPenguin(player, source, target, damage, knockback);
                    }
                }
            }
            else {
                spawnTimer = 0;
                SetCooldown();
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private void SpawnFallingPenguin(Player player, EntitySource_ItemUse_WithAmmo source, NPC target, int damage, float knockback) {
            Vector2 targetPos = target.Center;
            Vector2 spawnPos = targetPos + new Vector2(Main.rand.NextFloat(-100f, 100f), -600f);

            Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<FallingPenguin>(),
                (int)(damage * (2.25f + HalibutData.GetDomainLayer() * 0.55f)),
                knockback * 1.5f,
                player.whoAmI,
                0
            );

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, spawnPos);
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

        private int CountActivePenguins(Player player) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<FallingPenguin>()) {
                    count++;
                }
            }
            return count;
        }
    }

    internal class FallingPenguin : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Penguin;

        private enum PenguinState
        {
            Falling,
            Descending,
            Impact,
            Bouncing,
            Waddling,
            Disappearing
        }

        private PenguinState State {
            get => (PenguinState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TargetID => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        private float rotation = 0f;
        private float rotationSpeed = 0f;
        private Vector2 targetPosition = Vector2.Zero;
        private int bounceCount = 0;
        private const int MaxBounces = 2;
        private const float Gravity = 0.6f;
        private const float MaxFallSpeed = 24f;
        private const float TargetingStrength = 0.15f;
        private const int ImpactRadius = 120;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 10;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
        }

        public override void AI() {
            StateTimer++;

            switch (State) {
                case PenguinState.Falling:
                    FallingPhase();
                    break;
                case PenguinState.Descending:
                    DescendingPhase();
                    break;
                case PenguinState.Impact:
                    ImpactPhase();
                    break;
                case PenguinState.Bouncing:
                    BouncingPhase();
                    break;
                case PenguinState.Waddling:
                    WaddlingPhase();
                    break;
                case PenguinState.Disappearing:
                    DisappearingPhase();
                    break;
            }

            rotation += rotationSpeed;

            float lightIntensity = 0.4f + (float)Math.Sin(StateTimer * 0.2f) * 0.1f;
            Lighting.AddLight(Projectile.Center, lightIntensity * 0.7f, lightIntensity * 0.8f, lightIntensity * 1.0f);
        }

        private void FallingPhase() {
            if (StateTimer == 1) {
                rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);

                NPC target = GetTarget();
                if (target != null) {
                    targetPosition = target.Center;
                }
            }

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            NPC currentTarget = GetTarget();
            if (currentTarget != null) {
                targetPosition = currentTarget.Center;
                Vector2 toTarget = targetPosition - Projectile.Center;
                toTarget.Y = 0;

                if (toTarget.LengthSquared() > 0) {
                    Vector2 targetVelocity = toTarget.SafeNormalize(Vector2.Zero) * TargetingStrength;
                    Projectile.velocity.X += targetVelocity.X;
                    Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -8f, 8f);
                }
            }

            rotationSpeed += Main.rand.NextFloat(-0.02f, 0.02f);
            rotationSpeed = MathHelper.Clamp(rotationSpeed, -0.5f, 0.5f);

            if (StateTimer % 8 == 0) {
                SpawnFallDust();
            }

            if (StateTimer > 20) {
                State = PenguinState.Descending;
                StateTimer = 0;
            }
        }

        private void DescendingPhase() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            NPC currentTarget = GetTarget();
            if (currentTarget != null) {
                targetPosition = currentTarget.Center;
                Vector2 toTarget = targetPosition - Projectile.Center;
                toTarget.Y = 0;

                if (toTarget.LengthSquared() > 0) {
                    Vector2 targetVelocity = toTarget.SafeNormalize(Vector2.Zero) * TargetingStrength;
                    Projectile.velocity.X += targetVelocity.X;
                    Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -8f, 8f);
                }
            }

            float speedFactor = Math.Abs(Projectile.velocity.Y) / MaxFallSpeed;
            rotationSpeed = MathHelper.Lerp(rotationSpeed, Projectile.velocity.X * 0.05f, 0.1f);

            if (StateTimer % 4 == 0) {
                SpawnDescendDust();
            }
        }

        private void ImpactPhase() {
            if (StateTimer == 1) {
                CreateImpactEffect();
                DamageNearbyEnemies();

                SoundEngine.PlaySound(SoundID.Item14 with {
                    Volume = 0.8f,
                    Pitch = -0.3f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.NPCHit11 with {
                    Volume = 0.6f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }

            Projectile.velocity *= 0.8f;
            rotation += rotationSpeed;
            rotationSpeed *= 0.9f;

            if (StateTimer >= 10) {
                if (bounceCount < MaxBounces && Math.Abs(Projectile.velocity.Y) > 2f) {
                    State = PenguinState.Bouncing;
                    StateTimer = 0;
                }
                else {
                    State = PenguinState.Waddling;
                    StateTimer = 0;
                    rotation = 0f;
                }
            }
        }

        private void BouncingPhase() {
            Projectile.velocity.Y += Gravity * 0.8f;
            Projectile.velocity.X *= 0.95f;

            rotation += rotationSpeed;
            rotationSpeed *= 0.95f;

            if (StateTimer % 5 == 0) {
                SpawnBounceDust();
            }
        }

        private void WaddlingPhase() {
            Projectile.velocity.Y = 11f;
            Projectile.velocity.X *= 0.92f;
            VaultUtils.ClockFrame(ref Projectile.frame, 6, 11);
            Projectile.spriteDirection = Projectile.direction = Math.Sign(Projectile.velocity.X);


            if (Math.Abs(Projectile.velocity.X) < 0.5f) {
                Projectile.velocity.X = Main.rand.NextFloat(-2f, 2f);
            }

            rotation = (float)Math.Sin(StateTimer * 0.3f) * 0.15f;

            if (StateTimer % 10 == 0) {
                SpawnWaddleDust();
            }

            if (StateTimer >= 60) {
                State = PenguinState.Disappearing;
                StateTimer = 0;
            }
        }

        private void DisappearingPhase() {
            Projectile.velocity *= 0.9f;
            Projectile.scale = Math.Max(0f, 1f - StateTimer / 20f);
            Projectile.alpha = (int)(255 * StateTimer / 20f);

            if (StateTimer % 2 == 0) {
                SpawnDisappearDust();
            }

            if (StateTimer >= 20 || Projectile.scale <= 0.1f) {
                Projectile.Kill();
            }
        }

        private NPC GetTarget() {
            int id = (int)TargetID;
            if (id < 0 || id >= Main.maxNPCs) return null;

            NPC target = Main.npc[id];
            if (!target.active || !target.CanBeChasedBy()) return null;

            return target;
        }

        private void CreateImpactEffect() {
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 16f);

                Dust impact = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Snow,
                    velocity,
                    100,
                    new Color(200, 220, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                impact.noGravity = Main.rand.NextBool();
            }

            for (int i = 0; i < 20; i++) {
                Dust ice = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.IceTorch,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                ice.velocity = Main.rand.NextVector2Circular(10f, 10f);
                ice.noGravity = true;
            }

            for (int i = 0; i < 3; i++) {
                int radius = 30 + i * 25;
                for (int j = 0; j < 16; j++) {
                    float angle = MathHelper.TwoPi * j / 16f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        pos,
                        DustID.Snow,
                        angle.ToRotationVector2() * 4f,
                        100,
                        new Color(180, 200, 240),
                        Main.rand.NextFloat(1.2f, 2f)
                    );
                    ring.noGravity = true;
                }
            }
        }

        private void DamageNearbyEnemies() {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < ImpactRadius) {
                        float damageMultiplier = 1f - (distance / ImpactRadius) * 0.5f;
                        int damage = (int)(Projectile.damage * damageMultiplier);

                        Vector2 knockbackDir = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        float knockbackForce = Projectile.knockBack * (1.5f - distance / ImpactRadius);

                        npc.SimpleStrikeNPC(damage, Math.Sign(knockbackDir.X),
                            false, knockbackForce);

                        npc.AddBuff(BuffID.Frostburn, 120);
                        npc.AddBuff(BuffID.Slow, 180);

                        if (!VaultUtils.isSinglePlayer) {
                            NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, i, damage,
                                knockbackForce, Math.Sign(knockbackDir.X));
                        }
                    }
                }
            }
        }

        private void SpawnFallDust() {
            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Snow,
                    -Projectile.velocity * 0.3f,
                    100,
                    new Color(220, 230, 255),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                trail.noGravity = true;
                trail.fadeIn = 0.8f;
            }
        }

        private void SpawnDescendDust() {
            Dust desc = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Snow,
                -Projectile.velocity * 0.4f,
                100,
                new Color(200, 220, 255),
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            desc.noGravity = true;
            desc.fadeIn = 1f;

            if (Main.rand.NextBool(3)) {
                Dust ice = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.IceTorch,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    default,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                ice.noGravity = true;
            }
        }

        private void SpawnBounceDust() {
            for (int i = 0; i < 3; i++) {
                Dust bounce = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(10, 5),
                    20, 5,
                    DustID.Snow,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                bounce.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f));
            }
        }

        private void SpawnWaddleDust() {
            if (Main.rand.NextBool(2)) {
                Dust waddle = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(8, 4),
                    16, 4,
                    DustID.Snow,
                    Scale: Main.rand.NextFloat(0.8f, 1.2f)
                );
                waddle.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f));
            }
        }

        private void SpawnDisappearDust() {
            for (int i = 0; i < 2; i++) {
                Dust disappear = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.IceTorch,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    new Color(180, 200, 240, 150),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                disappear.noGravity = true;
                disappear.fadeIn = 0.9f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == PenguinState.Descending || State == PenguinState.Bouncing) {
                bool hitGround = Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f && oldVelocity.Y > 0;

                if (hitGround) {
                    State = PenguinState.Impact;
                    StateTimer = 0;
                    Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
                    bounceCount++;

                    return false;
                }

                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.6f;
                }
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 120);
            target.AddBuff(BuffID.Slow, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadNPC(NPCID.Penguin);
            Texture2D texture = TextureAssets.Npc[NPCID.Penguin].Value;

            if (texture == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle rectangle = texture.GetRectangle(Projectile.frame, 12);
            Vector2 origin = rectangle.Size() / 2f;
            SpriteEffects spriteEffects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color drawColor = Projectile.GetAlpha(lightColor);

            if (State == PenguinState.Falling || State == PenguinState.Descending) {
                float speedGlow = Math.Abs(Projectile.velocity.Y) / MaxFallSpeed;
                drawColor = Color.Lerp(drawColor, new Color(200, 220, 255), speedGlow * 0.4f);
            }

            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 2.5f;
                Vector2 shadowPos = drawPos + new Vector2(0, shadowOffset);
                Color shadowColor = new Color(100, 120, 150, 80) * (1f - i * 0.3f) * (1f - Projectile.alpha / 255f);

                sb.Draw(
                    texture,
                    shadowPos,
                    rectangle,
                    shadowColor,
                    rotation,
                    origin,
                    Projectile.scale * 0.9f,
                    spriteEffects,
                    0
                );
            }

            sb.Draw(
                texture,
                drawPos,
                rectangle,
                drawColor,
                rotation,
                origin,
                Projectile.scale,
                spriteEffects,
                0
            );

            if (State == PenguinState.Descending) {
                float glowIntensity = 0.3f + (float)Math.Sin(StateTimer * 0.3f) * 0.2f;
                Color glowColor = new Color(150, 180, 255) * glowIntensity * (1f - Projectile.alpha / 255f);

                sb.Draw(
                    texture,
                    drawPos,
                    rectangle,
                    glowColor,
                    rotation,
                    origin,
                    Projectile.scale * 1.1f,
                    spriteEffects,
                    0
                );
            }

            return false;
        }
    }
}
