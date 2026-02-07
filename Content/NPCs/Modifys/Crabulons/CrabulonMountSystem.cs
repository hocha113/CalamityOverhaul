using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹骑乘系统
    /// </summary>
    internal class CrabulonMountSystem
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;
        private readonly CrabulonPhysics physics;

        public CrabulonMountSystem(NPC npc, ModifyCrabulon owner, CrabulonPhysics physics) {
            this.npc = npc;
            this.owner = owner;
            this.physics = physics;
        }

        //获取骑乘位置
        public Vector2 GetMountPosition() {
            float yOffset = owner.ai[9] > 0 ? owner.ai[9] : npc.gfxOffY;
            return npc.Top + new Vector2(0, yOffset);
        }

        //关闭骑乘状态
        public void Dismount() {
            if (!owner.Owner.Alives()) {
                return;
            }

            owner.Mount = false;
            owner.DontMount = CrabulonConstants.DismountCooldown;
            owner.MountACrabulon = false;
            owner.Owner.fullRotation = 0;
            owner.Owner.velocity.Y -= 5;
            owner.SendNetWork();
        }

        //处理骑乘AI
        public bool ProcessMountAI() {
            if (owner.DontMount > 0) {
                owner.DontMount--;
            }

            if (!owner.Mount) {
                return HandleMountRequest();
            }

            return HandleMountedMovement();
        }

        //处理上马请求
        private bool HandleMountRequest() {
            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.MountCrabulon = null;
                owner.CrabulonPlayer.IsMount = false;
            }

            if (!ShouldStartMount() && !owner.MountACrabulon) {
                return true;
            }

            if (VaultUtils.isSinglePlayer) {
                owner.MountACrabulon = true;
                owner.SendNetWork();
            }
            else if (VaultUtils.isClient) {
                ShowMultiplayerWarning();
            }

            PlayMountSound();

            if (owner.MountACrabulon) {
                ProcessMountAnimation();
            }

            return true;
        }

        //判断是否应该开始上马
        private bool ShouldStartMount() {
            return owner.Owner.whoAmI == Main.myPlayer
                && owner.SaddleItem.Alives()
                && owner.DontMount <= 0
                && owner.hoverNPC
                && owner.rightPressed;
        }

        //显示多人游戏警告
        private void ShowMultiplayerWarning() {
            CombatText text = Main.combatText[CombatText.NewText(
                owner.Owner.Hitbox,
                Color.GreenYellow,
                ModifyCrabulon.DontDismountText.Value
            )];
            text.text = ModifyCrabulon.DontDismountText.Value;
            text.lifeTime = 320;
        }

        //播放上马音效
        private void PlayMountSound() {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ToMount with {
                PitchRange = (-0.1f, 0.1f),
                Volume = Main.rand.NextFloat(0.6f, 0.8f)
            }, owner.Owner.Center);
        }

        //处理上马动画
        private void ProcessMountAnimation() {
            owner.Owner.RemoveAllGrapplingHooks();
            owner.Owner.mount.Dismount(owner.Owner);

            Vector2 toMount = GetMountPosition() - owner.Owner.Center;
            owner.Owner.velocity = toMount.SafeNormalize(Vector2.Zero) * 8;

            owner.Owner.CWR().IsRotatingDuringDash = true;
            owner.Owner.CWR().RotationDirection = Math.Sign(owner.Owner.velocity.X);
            owner.Owner.CWR().PendingDashRotSpeedMode = 0.06f;
            owner.Owner.CWR().PendingDashVelocity = owner.Owner.velocity;

            if (++owner.ai[5] > CrabulonConstants.MountTimeout || toMount.Length() < owner.Owner.width) {
                CompleteMountProcess();
            }
        }

        //完成上马流程
        private void CompleteMountProcess() {
            owner.ai[5] = 0f;
            owner.Mount = true;
            owner.MountACrabulon = false;

            if (owner.CrabulonPlayer != null) {
                owner.CrabulonPlayer.MountCrabulon = owner;
            }

            if (owner.Owner.whoAmI == Main.myPlayer) {
                owner.SendNetWork();
            }

            owner.NetAISend();
        }

        //处理骑乘状态下的移动
        private bool HandleMountedMovement() {
            //鞍具被移除时自动下马
            if (!owner.SaddleItem.Alives()) {
                Dismount();
                return true;
            }

            if (owner.CrabulonPlayer != null) {
                CrabulonPlayer.CloseDuringDash(owner.Owner);
                owner.CrabulonPlayer.MountCrabulon = owner;
                owner.CrabulonPlayer.IsMount = true;
            }

            owner.Owner.RemoveAllGrapplingHooks();
            owner.Owner.mount.Dismount(owner.Owner);
            owner.Owner.Center = GetMountPosition();
            owner.Owner.fallStart2 = owner.Owner.fallStart = (int)(owner.Owner.position.Y / 16f);

            if (owner.ai[9] > 0) {
                owner.ai[9]--;
                npc.ai[0] = 0f;
                return false;
            }

            ProcessMountedInput();
            UpdateMountAnimation();

            if (CheckDismountInput()) {
                Dismount();
            }

            return false;
        }

        //处理骑乘输入
        private void ProcessMountedInput() {
            float accel = CrabulonConstants.BaseAcceleration + owner.Owner.runAcceleration;
            float maxSpeed = CalculateMaxSpeed();
            float friction = CrabulonConstants.Friction;

            Vector2 input = GetPlayerInput();
            owner.Owner.velocity = Vector2.Zero;

            HandleDownPlatform();
            HandleJump(maxSpeed);
            HandleHorizontalMovement(input.X, accel, maxSpeed, friction);

            JumpFloorEffect();
            physics.AutoStepClimbing();
        }

        //计算最大速度
        private float CalculateMaxSpeed() {
            return MathHelper.Clamp(
                CrabulonConstants.BaseSpeed * owner.Owner.moveSpeed * owner.Owner.maxRunSpeed / 6f,
                CrabulonConstants.MinSpeed,
                CrabulonConstants.MaxSpeed
            );
        }

        //获取玩家输入
        private Vector2 GetPlayerInput() {
            Vector2 input = Vector2.Zero;
            if (owner.Owner.controlRight) input.X += 1f;
            if (owner.Owner.controlLeft) input.X -= 1f;
            return input;
        }

        //处理下平台
        private void HandleDownPlatform() {
            if (!owner.Owner.controlDown || Collision.SolidCollision(npc.position, npc.width, npc.height + 20)) {
                return;
            }
            if (!owner.Owner.controlJump) {
                npc.velocity.Y += 0.2f;
                if (npc.velocity.Y < 12) {
                    npc.velocity.Y = 12;
                }
            }
        }

        //处理跳跃
        private void HandleJump(float maxSpeed) {
            if (owner.Owner.controlJump && npc.collideY) {
                npc.velocity.Y = MathHelper.Clamp(
                    maxSpeed * CrabulonConstants.MountJumpMultiplier,
                    CrabulonConstants.MinMountJump,
                    CrabulonConstants.MaxMountJump
                );
            }
        }

        //处理横向移动
        private void HandleHorizontalMovement(float inputX, float accel, float maxSpeed, float friction) {
            if (inputX != 0f) {
                npc.velocity.X = MathHelper.Clamp(npc.velocity.X + inputX * accel, -maxSpeed, maxSpeed);
            }
            else {
                npc.velocity.X *= friction;
                if (Math.Abs(npc.velocity.X) < 0.1f) {
                    npc.velocity.X = 0f;
                }
            }
        }

        //更新骑乘动画
        private void UpdateMountAnimation() {
            npc.ai[0] = Math.Abs(npc.velocity.X) > 0.1f ? 1f : 0f;
            if (Math.Abs(npc.velocity.Y) > 1f) {
                npc.ai[0] = 3f;
            }

            if (physics.JumpHeightSetFrame > 0) {
                npc.ai[0] = 1f;
            }

            if (owner.dontTurnTo <= 0f) {
                npc.spriteDirection = npc.direction = Math.Sign(npc.velocity.X);
            }
        }

        //检查下马输入
        private bool CheckDismountInput() {
            return owner.Owner.whoAmI == Main.myPlayer && owner.hoverNPC && owner.rightPressed;
        }

        //落地冲击效果
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
                owner.NetAISend();
                return;
            }

            if (npc.oldVelocity.Y > 2f && owner.ai[4] > CrabulonConstants.MinImpactDistance) {
                CreateImpactEffects(owner.ai[4]);
            }

            owner.ai[3] = 0;
            owner.ai[4] = 0;
            owner.NetAISend();
        }

        //创建冲击效果
        private void CreateImpactEffects(float impactStrength) {
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

            if (owner.Owner.whoAmI == Main.myPlayer) {
                CreateImpactProjectile(impactStrength);
            }
        }

        //创建冲击粒子
        private void CreateImpactDust(float impactStrength) {
            Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
            int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
            Main.dust[dust].velocity *= 0.5f;
            Main.dust[dust].velocity.Y *= impactStrength / Main.rand.NextFloat(160, 230);
            Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(owner.DyeItemID);
        }

        //创建冲击弹幕
        private void CreateImpactProjectile(float impactStrength) {
            float multiplicative = owner.Owner.GetDamage(DamageClass.Generic).Multiplicative;
            int baseDmg = CrabulonConstants.BaseDamage + (int)(impactStrength / CrabulonConstants.DamagePerImpact);
            baseDmg = (int)(baseDmg * multiplicative);

            Projectile.NewProjectile(
                npc.FromObjectGetParent(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<CrabulonFriendHitbox>(),
                baseDmg,
                CrabulonConstants.ImpactKnockback,
                owner.Owner.whoAmI,
                npc.whoAmI
            );
        }
    }
}
