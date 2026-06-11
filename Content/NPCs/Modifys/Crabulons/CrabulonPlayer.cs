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
        /// <summary>
        /// 存在的菌生蟹索引，如果为-1则表示没有
        /// </summary>
        public int CrabulonIndex;
        /// <summary>
        /// 骑乘的菌生蟹实例，如果没有骑乘，则为null
        /// </summary>
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
            //骑乘标记的有效性兜底：蟹失效或已不处于骑乘状态时立即退出，防止玩家被永久隐藏
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
                return false;//没加载好内容，直接返回
            }
            if (!player.Alives()) {
                return false;//玩家无效，直接返回
            }
            if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer) || crabulonPlayer == null) {
                return false;//找不到实例，直接返回
            }
            return crabulonPlayer.IsMount;
        }
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            players = players.Where(p => !PlayerIsMount(p));//删掉关于骑乘玩家的绘制
            return true;
        }
    }

    /// <summary>
    /// 骑乘菌生蟹时接管玩家运动。
    /// 运动只由骑手客户端权威模拟，并通过原版玩家同步管线自然广播，
    /// 其余端对该玩家的本地模拟（基于已同步的按键）也会执行相同的参数调整，保证平滑。
    /// 蟹本体则在NPC的AI中吸附到玩家身上，见<see cref="CrabulonMountSystem"/>
    /// </summary>
    internal class CrabulonMountPlayer : ModPlayer
    {
        private float fallDistance;
        private float fallDistancePeak;
        private float oldVelocityY;

        //当前骑乘的菌生蟹，未骑乘返回null
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

            //骑乘期间无摔落伤害、禁用飞行与额外跳跃
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

            //巨兽跳跃：站定时直接起跳，绕过原版跳跃以获得与体型匹配的弹射力度
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

            //用蟹的箱体约束玩家位移：玩家自身箱体的碰撞随后由引擎正常执行，最终取两者交集，蟹不会插进墙里
            Vector2 crabPos = CrabulonMountSystem.GetAttachedBoxPosition(Player, npc);
            bool fallThrough = Player.controlDown;
            Vector2 constrained = Collision.TileCollision(crabPos, Player.velocity, npc.width, npc.height, fallThrough, fallThrough, (int)Player.gravDir);

            //横向受阻时尝试大台阶攀爬，保留巨蟹跨越地形的手感
            if (constrained.X != Player.velocity.X && TryClimbStep(npc, ref crabPos)) {
                constrained = Collision.TileCollision(crabPos, Player.velocity, npc.width, npc.height, fallThrough, fallThrough, (int)Player.gravDir);
            }

            Player.velocity = constrained;

            TrackFallImpact(riding);
            oldVelocityY = Player.velocity.Y;
        }

        //大台阶攀爬：探测前方台阶高度，逐帧抬升玩家（蟹随之吸附）
        private bool TryClimbStep(NPC npc, ref Vector2 crabPos) {
            int direction = Math.Sign(Player.velocity.X);
            if (direction == 0) {
                return false;
            }

            int maxClimbLevel = CrabulonConstants.MaxStepHeight / CrabulonConstants.StepCheckInterval;
            float climbHeight = 0f;

            for (int i = 1; i <= maxClimbLevel; i++) {
                Vector2 checkPos = crabPos - new Vector2(0, i * CrabulonConstants.StepCheckInterval);
                //抬升路径被堵死，无法攀爬
                if (Collision.SolidCollision(checkPos, npc.width, npc.height)) {
                    return false;
                }
                //在这一高度上前方有通行空间，攀爬目标找到
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

        //跟踪坠落距离，落地时触发冲击效果
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

        //落地冲击：特效各端本地播放，伤害弹幕只由骑手端生成（弹幕为所有者权威，天然同步）
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
