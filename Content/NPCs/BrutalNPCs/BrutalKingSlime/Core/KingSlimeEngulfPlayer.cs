using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core
{
    /// <summary>
    /// 吞没投技·被吞玩家本人客户端：钉身入腹/锁操控/按同步拍结算剧本伤害/高压弹射/异常软释放。
    /// 位移与控制只在被吞玩家自己的客户端施加(玩家位置客户端权威)，其余端靠原版玩家同步看到
    /// </summary>
    internal class KingSlimeEngulfPlayer : ModPlayer
    {
        /// <summary>正吞着自己的王(NPC索引)，-1无；仅本人客户端维护</summary>
        private int grabNpcIndex = -1;
        /// <summary>已结算的挤压拍序号(只前向，防回卷重复结算)</summary>
        private int squeezeApplied;
        /// <summary>入腹初处理已做(清钩爪/下坐骑等一次性动作)</summary>
        private bool entryDone;
        /// <summary>本次吞没已弹射</summary>
        private bool ejected;
        /// <summary>弹射/释放后的缓落保护剩余帧(持续清坠落距离)</summary>
        private int softFallTimer;

        /// <summary>本人当前被吞着(锁控依据)</summary>
        internal bool IsEngulfed => grabNpcIndex >= 0 && !ejected;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //缓落保护：弹射后一段时间内不积累坠落伤害
            if (softFallTimer > 0) {
                softFallTimer--;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            NPC holder = FindHolder(out KingSlimeAI king);
            if (holder == null) {
                if (grabNpcIndex >= 0) {
                    //抓取态消失：正常弹射后收尾，或异常断投的软释放
                    if (!ejected) {
                        SoftRelease();
                    }
                    ResetSession();
                }
                return;
            }

            int grabPhase = (int)king.ai[KingSlimeEngulfState.SlotGrabPhase];

            if (!entryDone) {
                EnterBelly();
            }
            grabNpcIndex = holder.whoAmI;

            if (grabPhase is 1 or 2 && !ejected) {
                PinInside(holder, king);
                //挤压拍：按服务端计数只前向结算
                int beat = (int)king.ai[KingSlimeEngulfState.SlotSqueeze];
                if (beat > squeezeApplied) {
                    squeezeApplied = beat;
                    ApplyScriptedHurt(holder, (int)(holder.defDamage * 0.3f));
                    KingSlimePerformancePlayer.RequestEngulfShake(5.5f, 14);
                }
            }
            else if (grabPhase == 3 && !ejected) {
                Eject(holder);
            }
        }

        /// <summary>找正吞着本人的王：验接管在场+状态为吞没+受害者槽指向自己</summary>
        private NPC FindHolder(out KingSlimeAI holderAI) {
            holderAI = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!KingSlimeAI.TryGetKingAI(npc, out KingSlimeAI king)) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)KingSlimeStateIndex.Engulf) {
                    continue;
                }
                if ((int)king.ai[KingSlimeEngulfState.SlotVictim] - 1 == Player.whoAmI) {
                    holderAI = king;
                    return npc;
                }
            }
            return null;
        }

        /// <summary>入腹一次性处理：断位移挂点，清速度残留</summary>
        private void EnterBelly() {
            entryDone = true;
            ejected = false;
            squeezeApplied = 0;
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();
            Player.StopVanityActions();
            Player.velocity = Vector2.Zero;
        }

        /// <summary>每帧钉在凝胶腹心：位置/速度/坠落距离/环境伤害屏蔽</summary>
        private void PinInside(NPC holder, KingSlimeAI king) {
            //腹心锚定底部并随压扁比例下沉：深压时人跟着沉进扁塌的凝胶而不是从顶上露出来
            float squash = king.StateContext != null
                ? MathHelper.Clamp(king.StateContext.VisualSquash, 0.3f, 1.6f) : 1f;
            float bob = MathF.Sin(Main.GameUpdateCount * 0.16f + Player.whoAmI) * 3f;
            Vector2 belly = new Vector2(holder.Center.X,
                holder.Bottom.Y - holder.height * squash * 0.45f + bob);
            Player.Center = belly;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            //环境伤害屏蔽：被裹住期间只吃剧本挤压伤(剧本伤结算前会临时揭盾)
            Player.immune = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }
        }

        /// <summary>
        /// 剧本伤害：走常规Hurt在本人客户端结算(原版受伤包自动广播)。
        /// 公平阀：留命余量不足则跳过本拍——满血玩家绝不会被一套投技处死，残血终结击减免留命
        /// </summary>
        private void ApplyScriptedHurt(NPC holder, int raw) {
            if (raw <= 0 || Player.dead) {
                return;
            }
            if (Player.statLife <= raw + 30) {
                return;
            }
            //Hurt在immune=true时直接跳过，剧本伤需临时揭盾；Hurt自会重挂受伤无敌
            Player.immune = false;
            Player.immuneTime = 0;
            Player.Hurt(PlayerDeathReason.ByNPC(holder.whoAmI), raw, 0,
                cooldownCounter: -1, dodgeable: false, knockback: 0f);
        }

        /// <summary>高压喷出：终结一击(最重)+大弹射+足额无敌帧+缓落保护。
        /// 震屏不走运镜通道——启停器在相位3已释放镜头，状态侧EjectFX的普通震屏此时生效</summary>
        private void Eject(NPC holder) {
            ejected = true;
            int dir = Math.Sign(Player.Center.X - holder.Center.X);
            if (dir == 0) {
                dir = holder.direction != 0 ? holder.direction : 1;
            }
            ApplyScriptedHurt(holder, (int)(holder.defDamage * 0.5f));
            Player.velocity = new Vector2(dir * 11.5f, -12.5f);
            Player.SetImmuneTimeForAllTypes(100);
            softFallTimer = 110;
        }

        /// <summary>异常断投的软释放：无伤脱出+无敌帧+缓落，不留任何锁定残余</summary>
        private void SoftRelease() {
            Player.velocity = new Vector2(0f, -6f);
            Player.SetImmuneTimeForAllTypes(80);
            softFallTimer = 90;
        }

        private void ResetSession() {
            grabNpcIndex = -1;
            entryDone = false;
            ejected = false;
            squeezeApplied = 0;
        }

        /// <summary>被吞期间锁全输入+禁持物(运镜输入锁之外的硬保险)</summary>
        public override void SetControls() {
            if (!IsEngulfed) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
            Player.noItems = true;
        }

        /// <summary>
        /// 死亡帧收尾：死亡后PostUpdate不再执行(tML钩子时序)，
        /// 锁定复位与运镜停止必须在这里兜底，否则残留到复活后
        /// </summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (grabNpcIndex >= 0) {
                ResetSession();
                softFallTimer = 0;
            }
            if (CutsceneDirector.CurrentClip is KingSlimeEngulfCutscene) {
                CutsceneDirector.Stop();
            }
        }
    }
}
