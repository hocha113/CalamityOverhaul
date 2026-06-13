using CalamityOverhaul.Content.Industrials.ElectricPowers;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    internal class CrabulonPlayer : PlayerOverride
    {
        /// <summary>已驯蟹索引，-1 无</summary>
        public int CrabulonIndex;
        /// <summary>骑乘蟹实例，未骑乘为 null</summary>
        public ModifyCrabulon MountCrabulon;
        private bool oldIsMount;
        public bool IsMount;
        public List<ModifyCrabulon> ModifyCrabulons = [];
        public override void ResetEffects() => CrabulonIndex = -1;
        public static void CloseDuringDash(Player player) {
            CWRPlayer modPlayer = player.CWR();
            player.fullRotation = 0;
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.RotationDirection = player.direction;
            modPlayer.DashCooldownCounter = 95;
            modPlayer.CustomCooldownCounter = 90;
        }
        public override void PostUpdate() {
            //骑乘标记兜底，防玩家永久隐藏
            if (IsMount && (MountCrabulon == null || !MountCrabulon.npc.Alives() || !MountCrabulon.Mount)) {
                IsMount = false;
            }

            if (IsMount) {
                if (Player.CountProjectilesOfID<ElectricMinRocketHeld>() > 0) {
                    foreach (var p in Main.ActiveProjectiles) {
                        if (p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<ElectricMinRocketHeld>()) {
                            p.Kill();
                        }
                    }
                }
            }
            else {
                ModifyCrabulon.mountPlayerHeldProj = -1;
                MountCrabulon = null;
                if (oldIsMount) {
                    CloseDuringDash(Player);
                }
            }

            oldIsMount = IsMount;

            ModifyCrabulons.Clear();
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.boss || npc.type != CWRID.NPC_Crabulon) {
                    continue;
                }
                ModifyCrabulons.Add(npc.GetOverride<ModifyCrabulon>());
            }
        }
        private static bool PlayerIsMount(Player player) {
            if (!VaultLoad.LoadenContent) {
                return false;
            }
            if (!player.Alives()) {
                return false;
            }
            if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer) || crabulonPlayer == null) {
                return false;
            }
            return crabulonPlayer.IsMount;
        }
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            players = players.Where(p => !PlayerIsMount(p));//骑乘玩家改由蟹侧绘制
            return true;
        }
    }

    /// <summary>
    /// 骑蟹时接管玩家运动，权威在骑手客户端；
    /// 蟹吸附见<see cref="CrabulonMountSystem"/>
    /// </summary>
    internal class CrabulonMountPlayer : ModPlayer
    {
        private float fallDistance;
        private float fallDistancePeak;
        private float oldVelocityY;

        //未骑乘返回 null
        private ModifyCrabulon Riding {
            get {
                if (!Player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)
                    || crabulonPlayer == null
                    || !crabulonPlayer.IsMount
                    || crabulonPlayer.MountCrabulon == null
                    || !crabulonPlayer.MountCrabulon.npc.Alives()) {
                    return null;
                }
                return crabulonPlayer.MountCrabulon;
            }
        }

        public override void PostUpdateEquips() {
            if (Riding == null) {
                return;
            }

            //无摔落伤、禁飞与额外跳
            Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
            Player.wingTime = 0;
            Player.rocketTime = 0;
            Player.blockExtraJumps = true;

            if (Player.whoAmI == Main.myPlayer) {
                Player.RemoveAllGrapplingHooks();
                if (Player.mount.Active) {
                    Player.mount.Dismount(Player);
                }
            }
        }

        public override void PostUpdateRunSpeeds() {
            var riding = Riding;
            if (riding == null) {
                return;
            }

            float maxSpeed = MathHelper.Clamp(
                CrabulonConstants.BaseSpeed * Player.moveSpeed * Player.maxRunSpeed / 6f,
                CrabulonConstants.MinSpeed,
                CrabulonConstants.MaxSpeed
            );

            Player.maxRunSpeed = maxSpeed;
            Player.accRunSpeed = maxSpeed;
            Player.runAcceleration = CrabulonConstants.BaseAcceleration + Player.runAcceleration;
            Player.runSlowdown = CrabulonConstants.MountRunSlowdown;

            //巨兽跳：站定起跳，绕过原版跳跃曲线
            if (Player.controlJump && Player.velocity.Y == 0f) {
                Player.velocity.Y = MathHelper.Clamp(
                    maxSpeed * CrabulonConstants.MountJumpMultiplier,
                    CrabulonConstants.MinMountJump,
                    CrabulonConstants.MaxMountJump
                );
                Player.jump = 0;
            }
        }

        public override void PreUpdateMovement() {
            var riding = Riding;
            if (riding == null) {
                oldVelocityY = Player.velocity.Y;
                fallDistance = fallDistancePeak = 0f;
                return;
            }

            NPC npc = riding.npc;

            //蟹箱体约束位移，与玩家碰撞取交集
            Vector2 crabPos = CrabulonMountSystem.GetAttachedBoxPosition(Player, npc);
            bool fallThrough = Player.controlDown;
            Vector2 constrained = Collision.TileCollision(crabPos, Player.velocity, npc.width, npc.height, fallThrough, fallThrough, (int)Player.gravDir);

            //横向受阻时大台阶攀爬
            if (constrained.X != Player.velocity.X && TryClimbStep(npc, ref crabPos)) {
                constrained = Collision.TileCollision(crabPos, Player.velocity, npc.width, npc.height, fallThrough, fallThrough, (int)Player.gravDir);
            }

            Player.velocity = constrained;

            TrackFallImpact(riding);
            oldVelocityY = Player.velocity.Y;
        }

        //大台阶攀爬，蟹随吸附抬升
        private bool TryClimbStep(NPC npc, ref Vector2 crabPos) {
            int direction = Math.Sign(Player.velocity.X);
            if (direction == 0) {
                return false;
            }

            int maxClimbLevel = CrabulonConstants.MaxStepHeight / CrabulonConstants.StepCheckInterval;
            float climbHeight = 0f;

            for (int i = 1; i <= maxClimbLevel; i++) {
                Vector2 checkPos = crabPos - new Vector2(0, i * CrabulonConstants.StepCheckInterval);
                if (Collision.SolidCollision(checkPos, npc.width, npc.height)) {
                    return false;
                }
                Vector2 forwardPos = checkPos + new Vector2(direction * 8, 0);
                if (!Collision.SolidCollision(forwardPos, npc.width, npc.height)) {
                    climbHeight = i * CrabulonConstants.StepCheckInterval;
                    break;
                }
            }

            if (climbHeight <= 0f) {
                return false;
            }

            float climb = Math.Min(climbHeight, CrabulonConstants.MountClimbSpeed);
            Vector2 lifted = crabPos - new Vector2(0, climb);
            if (Collision.SolidCollision(lifted, npc.width, npc.height)) {
                return false;
            }

            Player.position.Y -= climb;
            crabPos = lifted;
            return true;
        }

        private void TrackFallImpact(ModifyCrabulon riding) {
            if (Player.velocity.Y != 0f) {
                if (Player.velocity.Y < 0f) {
                    fallDistance = 0f;
                    fallDistancePeak = 0f;
                }
                else {
                    fallDistance += Player.velocity.Y;
                    if (fallDistance > fallDistancePeak) {
                        fallDistancePeak = fallDistance;
                    }
                }
                return;
            }

            if (oldVelocityY > 2f && fallDistancePeak > CrabulonConstants.MinImpactDistance) {
                CreateImpactEffects(riding, fallDistancePeak);
            }

            fallDistance = 0f;
            fallDistancePeak = 0f;
        }

        //特效各端本地，伤害弹幕仅骑手端
        private void CreateImpactEffects(ModifyCrabulon riding, float impactStrength) {
            NPC npc = riding.npc;

            if (!Main.dedServ) {
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
                    Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
                    int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
                    Main.dust[dust].velocity *= 0.5f;
                    Main.dust[dust].velocity.Y *= impactStrength / Main.rand.NextFloat(160, 230);
                    Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(riding.DyeItemID);
                }
            }

            if (Player.whoAmI == Main.myPlayer) {
                float multiplicative = Player.GetDamage(DamageClass.Generic).Multiplicative;
                int baseDmg = CrabulonConstants.BaseDamage + (int)(impactStrength / CrabulonConstants.DamagePerImpact);
                baseDmg = (int)(baseDmg * multiplicative);

                Projectile.NewProjectile(
                    npc.FromObjectGetParent(),
                    npc.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<CrabulonFriendHitbox>(),
                    baseDmg,
                    CrabulonConstants.ImpactKnockback,
                    Player.whoAmI,
                    npc.whoAmI
                );
            }
        }
    }
}
