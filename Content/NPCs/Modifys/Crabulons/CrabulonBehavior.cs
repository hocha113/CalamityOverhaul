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
    //菌生蟹行为系统
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

        //处理基础更新，这些逻辑在任何状态下都应该执行
        public void UpdateBasics() {
            UpdateTimers();
            physics.UpdateGroundDistance();
            CheckHover();
        }

        //处理主AI逻辑，这部分在骑乘状态下会被跳过
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

        //更新计时器
        private void UpdateTimers() {
            if (owner.ai[7] > 0) owner.ai[7]--;
            if (owner.ai[8] > 0) owner.ai[8]--;
            if (owner.ai[10] > 0) owner.ai[10]--;
            if (owner.dontTurnTo > 0) owner.dontTurnTo--;
        }

        //检查鼠标悬停
        private void CheckHover() {
            owner.hoverNPC = npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1));
            if (owner.hoverNPC) {
                Item item = Main.LocalPlayer.GetItem();
                if (item.type == ModContent.ItemType<MushroomSaddle>() && item.ModItem is MushroomSaddle saddle) {
                    saddle.ModifyCrabulon = owner;
                }
            }
        }

        //处理消化状态
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

        //创建消化粒子效果
        private void CreateDigestionParticles() {
            if (owner.ai[8] == CrabulonConstants.ParticleEffectTime1) {
                SpawnNutritionalParticles(CrabulonConstants.ParticleCount1);
            }
            else if (owner.ai[8] == CrabulonConstants.ParticleEffectTime2) {
                SpawnNutritionalParticles(CrabulonConstants.ParticleCount2);
            }
        }

        //处理蹲伏状态
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

        //处理蹲伏逻辑
        private void HandleCrouching() {
            if (owner.ai[9] < CrabulonConstants.CrouchAnimationMax) {
                owner.ai[9] += CrabulonConstants.CrouchAnimationSpeed;
            }
            else if (Main.GameUpdateCount % CrabulonConstants.HealInterval == 0 && npc.life < npc.lifeMax) {
                HealNPC();
            }
        }

        //治疗NPC
        private void HealNPC() {
            if (!VaultUtils.isClient) {
                npc.life += CrabulonConstants.HealAmount;
                npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
                npc.netUpdate = true;
            }

            SpawnNutritionalParticles(CrabulonConstants.HealParticleCount);
        }

        //处理传送逻辑
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

        //执行传送
        private void PerformTeleport() {
            owner.ai[6] = 0;
            npc.Center = owner.Owner.Center + new Vector2(0, CrabulonConstants.TeleportSpawnHeight);
            SoundEngine.PlaySound(SoundID.Item8, npc.Center);

            for (int i = 0; i < CrabulonConstants.TeleportEffectCount; i++) {
                CreateTeleportDust();
            }
        }

        //创建传送粒子
        private void CreateTeleportDust() {
            Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
            int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
            Main.dust[dust].velocity *= 0.5f;
            Main.dust[dust].velocity.Y *= 300f / Main.rand.NextFloat(160, 230);
            Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(owner.DyeItemID);
        }

        //处理跟随或攻击行为
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

            return false;
        }

        //处理横向移动
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

        //处理攻击跳跃
        private void ProcessAttackJump() {
            npc.ai[0] = 3f;
            if (npc.velocity.Y == 0) {
                npc.velocity.Y -= 12;
            }
            else {
                npc.velocity.Y += 0.2f;
            }

            CreateAttackJumpEffect();
        }

        //创建攻击跳跃效果
        private void CreateAttackJumpEffect() {
            if (!VaultUtils.isServer) {
                SpawnNutritionalParticles(CrabulonConstants.HealParticleCount);
            }
        }

        //处理纵向移动
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

        //更新朝向
        private void UpdateDirection() {
            if (owner.dontTurnTo <= 0f) {
                npc.spriteDirection = npc.direction;
            }
        }

        //生成营养粒子
        private void SpawnNutritionalParticles(int count) {
            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = npc.position + new Vector2(Main.rand.Next(npc.width), Main.rand.Next(npc.height));
                PRTLoader.NewParticle<PRT_Nutritional>(spawnPos, Vector2.Zero);
            }
        }
    }
}
