using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 缚魂之腕状态机：魂魄计数/格挡/掌攫蓄势全在实例字段（禁 static 可变状态）。<br/>
    /// 权威划分：收魂与格挡判定在拥有者客户端（命中钩子只在 owner 端跑），
    /// 弹幕灭杀请求经 <see cref="SoulbindingArmNet"/> 交服务端执行，
    /// 诅咒领域减益由服务端/单机节流扫描施加（AddBuff 骑原版同步）。
    /// 远端镜像计数由信道喂，其余端只做演出
    /// </summary>
    internal class SoulbindingArmPlayer : ModPlayer
    {
        #region 常量
        /// <summary>魂环容量</summary>
        public const int MaxSouls = 8;
        /// <summary>诅咒领域半径（px），减益判定与边界演出同源</summary>
        public const float DomainRadius = 380f;
        /// <summary>魂环轨道半径（px）</summary>
        public const float RingRadius = 76f;
        /// <summary>格挡判定半径：魂环外缘再让一手</summary>
        public const float BlockRadius = RingRadius + 34f;
        /// <summary>满环凝聚帧数，凝聚完成才放手</summary>
        public const int ChargeFrames = 45;
        /// <summary>收魂飞行演出帧数</summary>
        public const int StreakFrames = 24;
        /// <summary>掌攫索敌半径（px）</summary>
        public const float TargetSearchRange = 1100f;
        /// <summary>领域内本人伤害倍率，与 Tooltip 的 8% 同源</summary>
        public const float DomainOwnerAmp = 1.08f;
        /// <summary>格挡内置冷却（tick）＝1 秒</summary>
        public const int BlockCooldownFrames = 60;

        /// <summary>领域减益扫描间隔（按玩家错帧）</summary>
        private const int CurseScanInterval = 12;
        /// <summary>缚魂咒单次施加时长，须大于扫描间隔防闪断</summary>
        private const int CurseDuration = 95;
        /// <summary>联机魂数慢速对账间隔（覆盖丢包与后入场）</summary>
        private const int ResyncInterval = 300;
        #endregion

        #region 状态（全实例字段）
        /// <summary>本帧装备生效，UpdateAccessory 逐帧点亮</summary>
        public bool DomainActive;
        /// <summary>当前魂魄数：拥有者权威，远端为信道镜像</summary>
        public int SoulCount;
        /// <summary>满环凝聚计时（仅拥有者推进）</summary>
        public int ChargeTimer;
        /// <summary>装备物品引用，掌攫弹幕的生成源</summary>
        internal Item SourceItem;

        /// <summary>收魂飞行演出（纯本端视觉，起点→魂环的吸入弧）</summary>
        public struct SoulStreak
        {
            public Vector2 From;
            public long StartTick;
        }
        /// <summary>在途收魂演出，绘制期推进、过期即除</summary>
        public readonly List<SoulStreak> Streaks = [];

        /// <summary>魂环视觉收拢度 0~1（每帧向满环状态缓动，纯演出）</summary>
        internal float VisualConverge;
        /// <summary>魂环累计旋转相位（演出，暂停冻结）</summary>
        internal float SpinPhase;
        /// <summary>格挡冷却余帧：拥有者权威，远端由 BlockFx 转播喂镜像（驱动魂环变暗）</summary>
        internal int BlockCooldown;

        private int resyncTimer;
        #endregion

        public override void ResetEffects() {
            DomainActive = false;
            SourceItem = null;
        }

        public override void UpdateDead() {
            //死亡魂魄散尽：各端确定性自清，拥有者补一次广播
            if (SoulCount > 0 || ChargeTimer > 0) {
                ChargeTimer = 0;
                SetSoulsAsOwner(0);
            }
            Streaks.Clear();
        }

        public override void PostUpdateEquips() {
            if (!DomainActive) {
                //卸下即散：装备状态经原版同步各端一致，确定性自清不发包
                SoulCount = 0;
                ChargeTimer = 0;
                return;
            }
            //渲染层帧戳：任一装备者活跃即放行 RenderHandle，无人装备时全表扫描早退
            SoulbindingArmRender.RenderStamp.Stamp();
            CurseScanAuthority();
        }

        public override void PostUpdate() {
            if (Main.dedServ || !DomainActive) {
                return;
            }
            //冷却在各端本地递减（远端镜像由 BlockFx 喂初值，驱动可见冷却）
            if (BlockCooldown > 0) {
                BlockCooldown--;
            }
            AmbientClientFx();

            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            OwnerBlockScan();
            OwnerChargeLogic();
            OwnerResync();
        }

        #region 领域增伤（判伤端＝拥有者客户端，仅放大领域主人自己的伤害）
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!DomainActive || !Player.WithinRange(target.Center, DomainRadius)) {
                return;
            }
            modifiers.FinalDamage *= DomainOwnerAmp;
        }
        #endregion

        #region 收魂（owner 端命中钩子）
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中钩子只在拥有者端运行，双保险再门一次
            if (!DomainActive || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //只认领域内的真击杀，雕像怪不出魂
            if (target.life > 0 || target.friendly || target.lifeMax <= 5 || target.SpawnedFromStatue) {
                return;
            }
            if (!Player.WithinRange(target.Center, DomainRadius + 40f)) {
                return;
            }
            GainSoul(target.Center);
        }

        private void GainSoul(Vector2 fromPos) {
            if (SoulCount >= MaxSouls) {
                return;
            }
            SoulCount++;
            SoulbindingArmNet.SendGain(Player.whoAmI, SoulCount, fromPos);
            SoulbindingArmRender.GainFx(this, fromPos);
        }
        #endregion

        #region 格挡（owner 判定，服务端执行灭杀）
        private void OwnerBlockScan() {
            if (SoulCount <= 0 || BlockCooldown > 0) {
                return;
            }
            //非冷却期 2 帧节流（挡弹半径 110px，快弹两帧位移仍在余量内）
            if (Main.GameUpdateCount % 2 != 0) {
                return;
            }
            //每次至多挡一发，与服务端限频一致
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.friendly || proj.damage <= 0) {
                    continue;
                }
                if (!Player.WithinRange(proj.Center, BlockRadius)) {
                    continue;
                }
                BlockProjectile(proj);
                break;
            }
        }

        private void BlockProjectile(Projectile proj) {
            SoulCount--;
            BlockCooldown = BlockCooldownFrames;
            Vector2 pos = proj.Center;

            if (Main.netMode == NetmodeID.SinglePlayer) {
                proj.Kill();
            }
            else {
                //先发请求（身份捕获要求弹幕仍 active），再本地视觉消隐求即时手感；
                //权威灭杀由服务端 Kill 广播落地。不再本地 active=false 先斩后奏：
                //请求被拒时消隐到期自愈复显，魂的悲观锁即上面的 1s 格挡冷却
                SoulbindingArmNet.SendBlockRequest(proj, SoulCount, pos);
                proj.GetGlobalProjectile<SoulbindingBlockHideGlobal>().HideFrames = BlockCooldownFrames;
            }
            LaunchCounterFlame(pos);
            SoulbindingArmRender.BlockPopFx(pos);
        }

        /// <summary>咒焰反掷：被吞的弹幕化作焰球，掷向格挡点附近最近的敌人（无敌可掷则不出弹）</summary>
        private void LaunchCounterFlame(Vector2 pos) {
            NPC target = null;
            float bestDistSq = SoulbindingCounterFlame.SeekRange * SoulbindingCounterFlame.SeekRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(pos, npc.Center);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    target = npc;
                }
            }
            if (target == null) {
                return;
            }
            var source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc("SoulbindingArm");
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic)
                .ApplyTo(SoulbindingCounterFlame.BaseDamage);
            Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.UnitX) * 5f;
            Projectile.NewProjectile(source, pos, vel,
                ModContent.ProjectileType<SoulbindingCounterFlame>(),
                damage, 2f, Player.whoAmI, target.whoAmI, target.type);
        }
        #endregion

        #region 掌攫（owner 端蓄势与出手）
        private void OwnerChargeLogic() {
            if (SoulCount < MaxSouls) {
                ChargeTimer = 0;
                return;
            }
            //已有巨手在场则持满待发
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<SoulbindingGhostHand>()] > 0) {
                return;
            }

            ChargeTimer++;
            if (ChargeTimer == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.4f }, Player.Center);
            }
            if (ChargeTimer < ChargeFrames) {
                return;
            }
            //凝聚完成后每 10 帧索敌一次，出现目标即扑
            if ((ChargeTimer - ChargeFrames) % 10 != 0) {
                return;
            }
            NPC target = FindExecutionTarget();
            if (target == null) {
                return;
            }
            LaunchGhostHand(target);
            ChargeTimer = 0;
            SetSoulsAsOwner(0);
        }

        /// <summary>当前最强敌人：生命上限计分，Boss 四倍权重</summary>
        private NPC FindExecutionTarget() {
            NPC best = null;
            float bestScore = 0f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (!Player.WithinRange(npc.Center, TargetSearchRange)) {
                    continue;
                }
                float score = npc.lifeMax * (npc.boss ? 4f : 1f);
                if (score > bestScore) {
                    bestScore = score;
                    best = npc;
                }
            }
            return best;
        }

        private void LaunchGhostHand(NPC target) {
            var source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc("SoulbindingArm");
            Vector2 spawn = Player.Center + new Vector2(-Player.direction * 26f, -10f);
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic)
                .ApplyTo(SoulbindingGhostHand.GrabBaseDamage);
            //目标身份进生成参数随生成包走，生成后不再补写（生成包时序契约）
            Projectile.NewProjectile(source, spawn, Vector2.Zero,
                ModContent.ProjectileType<SoulbindingGhostHand>(),
                damage, 6f, Player.whoAmI,
                target.whoAmI, target.type, 0f);
        }
        #endregion

        #region 领域减益（服务端/单机权威）
        private void CurseScanAuthority() {
            if (VaultUtils.isClient) {
                return;
            }
            //节流：按玩家错帧摊开扫描
            if (Main.GameUpdateCount % CurseScanInterval != (uint)(Player.whoAmI % CurseScanInterval)) {
                return;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (!Player.WithinRange(npc.Center, DomainRadius)) {
                    continue;
                }
                npc.AddBuff(ModContent.BuffType<SoulbindCurseDebuff>(), CurseDuration);
            }
        }
        #endregion

        #region 同步与杂项
        /// <summary>拥有者改魂数并广播；非拥有者端只本地写（用于确定性自清）</summary>
        private void SetSoulsAsOwner(int count) {
            SoulCount = count;
            if (Player.whoAmI == Main.myPlayer) {
                SoulbindingArmNet.SendState(Player.whoAmI, count);
            }
        }

        private void OwnerResync() {
            if (Main.netMode != NetmodeID.MultiplayerClient || SoulCount <= 0) {
                return;
            }
            if (++resyncTimer < ResyncInterval) {
                return;
            }
            resyncTimer = 0;
            SoulbindingArmNet.SendState(Player.whoAmI, SoulCount);
        }

        /// <summary>领域边缘飘入的灵雾：更新期生成（暂停不累积），各端本地各自出</summary>
        private void AmbientClientFx() {
            if (!Main.rand.NextBool(4)) {
                return;
            }
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 dir = ang.ToRotationVector2();
            Vector2 pos = Player.Center + dir * (DomainRadius - Main.rand.NextFloat(30f));
            PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, -dir * Main.rand.NextFloat(0.6f, 1.4f),
                SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(0.7f, 1.2f))
                ?.Configure(Main.rand.Next(20, 34));
        }
        #endregion
    }

    /// <summary>
    /// 格挡本地消隐：拥有者客户端把被格挡的敌方弹幕藏起来并停掉对己判定，
    /// 等服务端权威 Kill 广播落地；请求被拒时消隐到期自愈复显（无状态污染）。
    /// 仅格挡发起端写入，纯本端表现字段不跨网络
    /// </summary>
    internal sealed class SoulbindingBlockHideGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>本地消隐余帧，&gt;0 期间不绘制、不判定对玩家的命中</summary>
        internal int HideFrames;

        public override void PostAI(Projectile projectile) {
            if (HideFrames > 0) {
                HideFrames--;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) => HideFrames <= 0;

        public override bool CanHitPlayer(Projectile projectile, Player target) => HideFrames <= 0;
    }
}
