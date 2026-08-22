using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 投技·攫取：巨鹿垂首伏低，独眼由暗转血红，胸前聚出一只攫取巨手
    /// 长预兆后沿直线掠向目标，命中即转入 EyeGrab 携抓演出；扑空则爪散影碎，
    /// 巨鹿僵直喘息露出大破绽。二阶段专属，带长冷却
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.SeizeHunt, typeof(DeerclopsStateContext))]
    internal class DeerclopsSeizeHuntState : DeerclopsStateBase
    {
        public override string StateName => "SeizeHunt";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.SeizeHunt;

        /// <summary>攫取手成形帧(服务端生成弹幕)</summary>
        internal const int HandSpawn = 8;
        /// <summary>预兆总长(手在此帧起飞)，满足≥40可读前摇</summary>
        internal const int Telegraph = 46;
        /// <summary>扑空僵直时长，躲开投技的奖励窗</summary>
        internal const int WhiffRecover = 55;
        /// <summary>保底超时</summary>
        internal const int HardEnd = 260;
        /// <summary>投技冷却(tick)，约25秒</summary>
        internal const int GrabCooldownTicks = 1500;
        /// <summary>选招距离上限</summary>
        internal const float SelectMaxDist = 1200f;

        //服务端缓存的攫取手索引与身份(客户端不依赖)
        private int handIndex = -1;
        private int handIdentity = -1;
        /// <summary>≥0进入扑空僵直(各端按本地观察推进，权威切态在服务端)</summary>
        private int recoverTimer = -1;
        /// <summary>本次攫取已命中(服务端)，OnExit据此保留ai[1]交给携抓态</summary>
        private bool caught;

        /// <summary>服务端选招判据：二阶段+冷却完毕+目标可及+非时停</summary>
        internal static bool GrabReady(DeerclopsStateContext context) {
            if (!context.IsPhase2 || WorldFreezeSystem.IsActive) {
                return false;
            }
            Player target = context.Target;
            if (!target.Alives() || target.creativeGodMode) {
                return false;
            }
            if (context.Npc.Distance(target.Center) > SelectMaxDist) {
                return false;
            }
            return (int)Main.GameUpdateCount - context.GrabLastEndStamp >= GrabCooldownTicks;
        }

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            handIndex = -1;
            handIdentity = -1;
            recoverTimer = -1;
            caught = false;
            //锁定目标：与状态切换同包同步
            if (!VaultUtils.isClient) {
                context.Npc.ai[1] = context.Npc.target + 1;
                context.Npc.netUpdate = true;
            }
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            Player target = DeerclopsEyeGrabState.GrabTarget(npc);

            //扑空僵直：伏低喘息，独眼黯淡，躲开的人赢得输出窗
            if (recoverTimer >= 0) {
                recoverTimer++;
                context.AnimMode = DeerAnimMode.Crouch;
                context.EyeGlow = 0.12f;
                context.EyeHeat = MathHelper.Lerp(1f, 0.2f, recoverTimer / (float)WhiffRecover);
                if (recoverTimer == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.55f, Pitch = -0.5f }, npc.Center);
                }
                if (!VaultUtils.isClient && recoverTimer >= WhiffRecover) {
                    DeerclopsEyeGrabState.StampCooldown(context);
                    return new DeerclopsStalkState();
                }
                return null;
            }

            //预兆与掠夺期共通：风雪退去，独眼转红，"世界澄澈=它盯上你了"
            context.VeilTarget = 0.12f;
            context.EyeHeat = 1f;
            context.EyeGlow = MathHelper.Lerp(0.25f, 0.95f, MathHelper.Clamp(Timer / (float)Telegraph, 0f, 1f));

            if (Timer <= Telegraph) {
                context.AnimMode = DeerAnimMode.Crouch;
                //面向锁定的攫取目标(而非主AI可能中途换掉的npc.target)
                if (target != null) {
                    float dx = target.Center.X - npc.Center.X;
                    if (Math.Abs(dx) > 24f) {
                        npc.direction = npc.spriteDirection = Math.Sign(dx);
                    }
                }

                if (Timer == 4 && !Main.dedServ) {
                    //影渗低嘶，与暗影之手同族但更沉
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.75f, Pitch = -0.95f }, npc.Center);
                }
                if (Timer == 26 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.8f, Pitch = -0.8f }, npc.Center);
                }
                //暗影向胸前聚拢(本端)
                if (!Main.dedServ && Timer > HandSpawn && Main.rand.NextBool(2)) {
                    Vector2 chest = npc.Bottom + new Vector2(npc.spriteDirection * 60f, -90f) * npc.scale;
                    Vector2 spawn = chest + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 150f);
                    Dust dust = Dust.NewDustPerfect(spawn, DustID.Shadowflame, (chest - spawn) * 0.06f, 140, default, Main.rand.NextFloat(0.9f, 1.5f));
                    dust.noGravity = true;
                }

                //服务端成形攫取手
                if (Timer == HandSpawn && !VaultUtils.isClient) {
                    if (target != null) {
                        handIndex = DeerSeizeHandProj.SpawnSeizeHand(npc, (int)npc.ai[1] - 1, Telegraph - HandSpawn);
                        handIdentity = handIndex >= 0 ? Main.projectile[handIndex].identity : -1;
                    }
                    if (handIndex < 0) {
                        recoverTimer = 0;
                    }
                }
                return null;
            }

            //掠夺期：手在飞，boss保持伏低紧盯
            context.AnimMode = DeerAnimMode.Crouch;

            if (!VaultUtils.isClient) {
                Projectile hand = ValidHand();
                //目标失效或手已消亡→扑空
                if (target == null || hand == null) {
                    recoverTimer = 0;
                    return null;
                }
                //命中判定：已起飞的手掌覆盖到目标，抓住了
                bool launched = hand.velocity.LengthSquared() > 16f;
                if (launched && Utils.CenteredRectangle(hand.Center, new Vector2(120f, 120f)).Intersects(target.Hitbox)) {
                    caught = true;
                    return new DeerclopsEyeGrabState();
                }
            }
            else {
                //客户端仅表现：观察到手消亡则本地进入僵直演出，权威切态仍听服务端
                if (!AnySeizeHandAlive(npc)) {
                    recoverTimer = 0;
                    return null;
                }
            }

            //保底超时
            if (Timer > HardEnd && !VaultUtils.isClient) {
                DeerclopsEyeGrabState.StampCooldown(context);
                return new DeerclopsStalkState();
            }
            return null;
        }

        public override void OnExit(DeerclopsStateContext context) {
            base.OnExit(context);
            //转入EyeGrab时ai[1]保留(携抓要用)；其余一切出口清零
            if (!VaultUtils.isClient && !caught) {
                context.Npc.ai[1] = 0f;
                context.Npc.netUpdate = true;
            }
        }

        /// <summary>服务端校验缓存的手仍有效(防槽位复用；已进消散计时也视作没了)</summary>
        private Projectile ValidHand() {
            if (handIndex < 0 || handIndex >= Main.maxProjectiles) {
                return null;
            }
            Projectile proj = Main.projectile[handIndex];
            if (!proj.active || proj.identity != handIdentity
                || proj.type != Terraria.ModLoader.ModContent.ProjectileType<DeerSeizeHandProj>()
                || proj.timeLeft <= 18) {
                return null;
            }
            return proj;
        }

        /// <summary>客户端观察：场上是否还有属于该boss目标的攫取手</summary>
        private static bool AnySeizeHandAlive(NPC npc) {
            int type = Terraria.ModLoader.ModContent.ProjectileType<DeerSeizeHandProj>();
            int targetIdx = (int)npc.ai[1] - 1;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == targetIdx) {
                    return true;
                }
            }
            return false;
        }
    }
}
