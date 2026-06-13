using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>菌生蟹行为 AI</summary>
    internal class CrabulonBehavior
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;
        private readonly CrabulonPhysics physics;

        public CrabulonBehavior(NPC npc, ModifyCrabulon owner, CrabulonPhysics physics) {
            this.npc = npc;
            this.owner = owner;
            this.physics = physics;
        }

        public void UpdateBasics() {
            UpdateTimers();
            physics.UpdateGroundDistance();
            CheckHover();
            physics.CheckAndFixStuckPosition();
        }

        //骑乘时跳过
        public bool ProcessAI() {
            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.CrabulonIndex = npc.whoAmI;
            }

            npc.noGravity = false;
            npc.noTileCollide = false;

            physics.UpdateJumpHeight();

            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.IsMount = false;
            }

            physics.ClampToWorldBounds();

            if (ProcessDigestion()) {
                return false;
            }

            if (ProcessCrouch()) {
                return false;
            }

            if (ProcessTeleport()) {
                return false;
            }

            return ProcessFollowOrAttack();
        }

        private void UpdateTimers() {
            if (owner.ai[7] > 0) owner.ai[7]--;
            if (owner.ai[8] > 0) owner.ai[8]--;
            if (owner.ai[10] > 0) owner.ai[10]--;
            if (owner.dontTurnTo > 0) owner.dontTurnTo--;
        }

        private void CheckHover() {
            if (Main.dedServ) {
                return;//悬停仅本地客户端
            }
            owner.hoverNPC = npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1));
            if (owner.hoverNPC) {
                Item item = Main.LocalPlayer.GetItem();
                if (item.type == ModContent.ItemType<MushroomSaddle>() && item.ModItem is MushroomSaddle saddle) {
                    saddle.ModifyCrabulon = owner;
                }
            }
        }

        private bool ProcessDigestion() {
            if (owner.ai[8] <= 0) {
                return false;
            }

            if (!VaultUtils.isServer) {
                CreateDigestionParticles();
            }

            npc.velocity.X /= 2;
            if (npc.collideY) {
                npc.velocity.Y /= 2;
            }
            npc.ai[0] = 0f;
            return true;
        }

        private void CreateDigestionParticles() {
            if (owner.ai[8] == CrabulonConstants.ParticleEffectTime1) {
                SpawnNutritionalParticles(CrabulonConstants.ParticleCount1);
            }
            else if (owner.ai[8] == CrabulonConstants.ParticleEffectTime2) {
                SpawnNutritionalParticles(CrabulonConstants.ParticleCount2);
            }
        }

        private bool ProcessCrouch() {
            if (owner.Crouch) {
                HandleCrouching();
                npc.velocity.X /= 2;
                if (npc.collideY) {
                    npc.velocity.Y /= 2;
                }
                npc.ai[0] = 0f;
                return true;
            }

            if (owner.ai[9] > 0) {
                owner.ai[9]--;
                npc.ai[0] = 0f;
                return true;
            }

            return false;
        }

        private void HandleCrouching() {
            if (owner.ai[9] < CrabulonConstants.CrouchAnimationMax) {
                owner.ai[9] += CrabulonConstants.CrouchAnimationSpeed;
            }
            else if (Main.GameUpdateCount % CrabulonConstants.HealInterval == 0 && npc.life < npc.lifeMax) {
                HealNPC();
            }
        }

        private void HealNPC() {
            if (!VaultUtils.isClient) {
                npc.life += CrabulonConstants.HealAmount;
                npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
                npc.netUpdate = true;
            }

            SpawnNutritionalParticles(CrabulonConstants.HealParticleCount);
        }

        private bool ProcessTeleport() {
            if (owner.Owner.Distance(npc.Center) <= CrabulonConstants.TeleportDistance) {
                return false;
            }

            if (++owner.ai[6] > CrabulonConstants.TeleportDelay) {
                PerformTeleport();
                return true;
            }

            return false;
        }

        //位置仅权威端改，客户端跟 NPC 同步
        private void PerformTeleport() {
            owner.ai[6] = 0;
            if (VaultUtils.isClient) {
                return;
            }
            npc.Center = owner.Owner.Center + new Vector2(0, CrabulonConstants.TeleportSpawnHeight);
            npc.netUpdate = true;
            owner.Networking.BroadcastTeleportEffect();
        }

        private bool ProcessFollowOrAttack() {
            Vector2 targetPos = owner.Owner.Center;
            float moveSpeed = CrabulonConstants.MoveSpeed;
            float inertia = CrabulonConstants.MoveInertia;
            float followDistance = CrabulonConstants.FollowDistance;

            owner.TargetNPC = npc.Center.FindClosestNPC(CrabulonConstants.SearchRange, false);
            if (owner.TargetNPC != null) {
                targetPos = owner.TargetNPC.Center;
                followDistance = CrabulonConstants.AttackFollowDistance;
                moveSpeed = CrabulonConstants.AttackMoveSpeed;
            }

            Vector2 toDis = targetPos - npc.Center;

            if (!Collision.CanHitLine(targetPos, 10, 10, npc.Center, 10, 10)) {
                npc.noTileCollide = true;
            }

            ProcessHorizontalMovement(toDis, followDistance, moveSpeed, inertia);
            ProcessVerticalMovement(targetPos);
            physics.AutoStepClimbing();
            UpdateDirection();

            if (owner.TargetNPC != null) {
                JumpFloorEffect();
            }

            return false;
        }

        private void ProcessHorizontalMovement(Vector2 toDis, float followDistance, float moveSpeed, float inertia) {
            if (Math.Abs(toDis.X) > followDistance && npc.velocity.Y <= 0) {
                if (toDis.X > 0) {
                    npc.velocity.X = (npc.velocity.X * inertia + moveSpeed) / (inertia + 1f);
                    npc.direction = 1;
                }
                else {
                    npc.velocity.X = (npc.velocity.X * inertia - moveSpeed) / (inertia + 1f);
                    npc.direction = -1;
                }
                npc.ai[0] = 1f;
            }
            else {
                npc.velocity.X *= 0.9f;
                npc.ai[0] = 0f;

                if (owner.TargetNPC != null) {
                    ProcessAttackJump();
                }
            }
        }

        private void ProcessAttackJump() {
            npc.ai[0] = 3f;
            if (npc.velocity.Y == 0) {
                npc.velocity.Y -= 12;
            }
            else {
                npc.velocity.Y += 0.2f;
            }
        }

        private void ProcessVerticalMovement(Vector2 targetPos) {
            if (npc.collideY && targetPos.Y < npc.Bottom.Y - 400 && npc.velocity.Y > -20) {
                npc.velocity.Y = CrabulonConstants.JumpVelocity;
            }

            if (targetPos.Y < npc.Bottom.Y) {
                owner.ai[7] = CrabulonConstants.VerticalChaseTime;
            }
            else if (npc.collideY) {
                owner.ai[10] = CrabulonConstants.PlatformFallTime;
            }
        }

        private void UpdateDirection() {
            if (owner.dontTurnTo <= 0f) {
                npc.spriteDirection = npc.direction;
            }
        }

        private void SpawnNutritionalParticles(int count) {
            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = npc.position + new Vector2(Main.rand.Next(npc.width), Main.rand.Next(npc.height));
                PRTLoader.NewParticle<PRT_Nutritional>(spawnPos, Vector2.Zero);
            }
        }

        private void JumpFloorEffect() {
            if (!npc.collideY) {
                owner.ai[3] += Math.Abs(npc.velocity.Y);
                if (npc.velocity.Y < 0) {
                    owner.ai[3] = 0;
                    owner.ai[4] = 0;
                }
                if (owner.ai[3] > owner.ai[4] && npc.velocity.Y > 0) {
                    owner.ai[4] = owner.ai[3];
                }
                return;
            }

            if (npc.oldVelocity.Y > 2f && owner.ai[4] > 10) {
                CreateImpactEffects(Math.Clamp(owner.ai[4] * 10, 10, 600));
            }

            owner.ai[3] = 0;
            owner.ai[4] = 0;
        }

        private void CreateImpactEffects(float impactStrength) {
            if (!VaultUtils.isServer) {
                float volume = CrabulonConstants.ImpactSoundVolume + Math.Min(
                    impactStrength / CrabulonConstants.ImpactVolumeMultiplier,
                    0.5f
                );
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = volume }, npc.Center);

                int dustCount = (int)MathHelper.Clamp(
                    impactStrength / CrabulonConstants.ImpactDustDivisor,
                    CrabulonConstants.MinDustCount,
                    CrabulonConstants.MaxDustCount
                );

                for (int i = 0; i < dustCount; i++) {
                    CreateImpactDust(impactStrength);
                }
            }

            if (!VaultUtils.isClient) {
                CreateImpactProjectile(impactStrength);
            }
        }

        private void CreateImpactDust(float impactStrength) {
            Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
            int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
            Main.dust[dust].velocity *= 0.5f;
            Main.dust[dust].velocity.Y *= impactStrength / Main.rand.NextFloat(160, 230);
            Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(owner.DyeItemID);
        }

        private void CreateImpactProjectile(float impactStrength) {
            int baseDmg = CrabulonConstants.BaseDamage + (int)(impactStrength / CrabulonConstants.DamagePerImpact);

            Projectile.NewProjectile(
                npc.FromObjectGetParent(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<CrabulonFriendHitbox>(),
                baseDmg,
                CrabulonConstants.ImpactKnockback,
                Main.myPlayer,
                npc.whoAmI
            );
        }
    }
}
