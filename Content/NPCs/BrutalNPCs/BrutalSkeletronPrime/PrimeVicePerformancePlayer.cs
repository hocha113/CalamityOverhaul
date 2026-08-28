using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 老虎钳处刑玩家侧：仅被抓玩家自己的客户端运行位置锁定、运镜启停与伤害节拍结算。<br/>
    /// 玩家位置客户端权威，服务器不写；伤害走本地 Hurt（原版舌头自伤模型），
    /// 满血不可被处死：累计硬帽 + 每拍致死钳位
    /// </summary>
    internal class PrimeVicePerformancePlayer : ModPlayer
    {
        /// <summary>累计伤害硬帽，占玩家最大生命比例</summary>
        private const float MaxComboFraction = 0.6f;
        /// <summary>释放/断投后的无敌帧</summary>
        private const int ReleaseImmuneFrames = 90;

        private bool grabLatched;      //本地已进入锁定
        private bool releaseTossDone;  //释放弹射已施加
        private int appliedBeatMask;   //已结算节拍位
        private int comboDamageDealt;  //本次投技累计实伤

        /// <summary>投技运镜震动，仅本剪辑激活时生效（本地）</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not PrimeViceExecutionCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            HeadPrimeAI headAI = FindExecutionHead(out NPC head);
            bool mine = headAI != null && headAI.GrabTargetIndex == Player.whoAmI;

            UpdateCutscene(mine ? head : null);

            if (!mine) {
                Disengage();
                return;
            }

            int t = headAI.ViceExecutionTick;
            if (!grabLatched) {
                grabLatched = true;
                releaseTossDone = false;
                appliedBeatMask = 0;
                comboDamageDealt = 0;
                OnGrabStart();
            }

            if (t < PrimeViceExecutionState.PinEnd) {
                //先结算节拍再锁位，抵消 Hurt 附带的击退
                ApplyBeats(headAI, head, t);
                if (!Player.dead) {
                    LockPlayerAt(PrimeViceExecutionState.PlayerAnchorFor(headAI, t));
                }
            }
            else if (!releaseTossDone) {
                releaseTossDone = true;
                ReleaseToss();
            }
            //释放后到状态退出前：自由身，等 Disengage 清标记
        }

        public override void UpdateDead() {
            //被抓期间死亡（外源DoT等）：PostUpdate 不再运行，此处清残留
            grabLatched = false;
            releaseTossDone = false;
            appliedBeatMask = 0;
            comboDamageDealt = 0;
        }

        #region 锁定与释放

        /// <summary>抓取瞬间的一次性清理：钩爪、坐骑、滑轮、残速</summary>
        private void OnGrabStart() {
            Player.RemoveAllGrapplingHooks();
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.pulley = false;
            Player.velocity = Vector2.Zero;
        }

        /// <summary>锁定到脚本锚点，演出期免伤由节拍统一结算</summary>
        private void LockPlayerAt(Vector2 center) {
            Player.Center = center;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.gravity = 0f;
            Player.immune = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }
        }

        /// <summary>钉地结束：向侧上弹出释放，给足无敌帧</summary>
        private void ReleaseToss() {
            float sideX = PrimeViceExecutionState.GetSideX();
            Player.velocity = new Vector2(sideX * 4.5f, -7.5f);
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.SetImmuneTimeForAllTypes(ReleaseImmuneFrames);
            Player.immune = true;
        }

        /// <summary>任何原因脱离投技（正常落幕/断投/状态被撕走）时的兜底清理</summary>
        private void Disengage() {
            if (!grabLatched) {
                return;
            }
            grabLatched = false;
            Player.fallStart = (int)(Player.position.Y / 16f);
            if (!releaseTossDone) {
                //没走到释放拍的异常断投：清残速并给同额无敌
                Player.velocity = Vector2.Zero;
                Player.SetImmuneTimeForAllTypes(ReleaseImmuneFrames);
                Player.immune = true;
            }
            releaseTossDone = false;
            appliedBeatMask = 0;
            comboDamageDealt = 0;
        }

        #endregion

        #region 伤害节拍

        /// <summary>前向推进结算节拍；回退不重放，缺臂拍无伤</summary>
        private void ApplyBeats(HeadPrimeAI headAI, NPC head, int t) {
            if (Player.dead) {
                return;
            }
            var beats = PrimeViceExecutionState.Beats;
            for (int i = 0; i < beats.Length; i++) {
                if (t < beats[i].Tick || (appliedBeatMask & 1 << i) != 0) {
                    continue;
                }
                appliedBeatMask |= 1 << i;
                //锁存掩码之外再验本端臂存活，杜绝抓取瞬间臂被击杀后的幽灵伤害
                if (beats[i].RequiredMask != 0
                    && ((headAI.GrabArmsMask & beats[i].RequiredMask) == 0
                        || !AnyMaskArmAlive(beats[i].RequiredMask))) {
                    continue;
                }
                ApplyBeatDamage(head, beats[i].Fraction);
            }
        }

        /// <summary>掩码对应的臂在本端是否仍存活</summary>
        private static bool AnyMaskArmAlive(int requiredMask) {
            if ((requiredMask & PrimeViceExecutionState.MaskSaw) != 0 && IsArmAlive(CWRWorld.primeSaw, NPCID.PrimeSaw)) {
                return true;
            }
            if ((requiredMask & PrimeViceExecutionState.MaskCannon) != 0 && IsArmAlive(CWRWorld.primeCannon, NPCID.PrimeCannon)) {
                return true;
            }
            if ((requiredMask & PrimeViceExecutionState.MaskLaser) != 0 && IsArmAlive(CWRWorld.primeLaser, NPCID.PrimeLaser)) {
                return true;
            }
            return false;
        }

        private static bool IsArmAlive(int index, int npcType) {
            return index >= 0 && index < Main.maxNPCs
                && Main.npc[index].active && Main.npc[index].type == npcType;
        }

        /// <summary>单拍伤害：钳伤系数 → 难度修正 → 累计硬帽 → 致死钳位留命</summary>
        private void ApplyBeatDamage(NPC head, float fraction) {
            NPC source = ResolveVice() ?? head;
            int raw = HeadPrimeAI.SetMultiplier((int)(source.defDamage * fraction));

            int capLeft = (int)(Player.statLifeMax2 * MaxComboFraction) - comboDamageDealt;
            if (raw <= 0 || capLeft <= 0) {
                return;
            }
            raw = Math.Min(raw, capLeft);
            //满血玩家不得被一套投技处死：任何一拍都不允许致死
            if (raw >= Player.statLife) {
                raw = Player.statLife - 1;
            }
            if (raw <= 0) {
                return;
            }

            double dealt = Player.Hurt(PlayerDeathReason.ByNPC(source.whoAmI), raw, 0);
            comboDamageDealt += (int)dealt + 1;
        }

        private static NPC ResolveVice() {
            int idx = CWRWorld.primeVice;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC vice = Main.npc[idx];
                if (vice.active && vice.type == NPCID.PrimeVice) {
                    return vice;
                }
            }
            return null;
        }

        #endregion

        #region 检索与运镜

        /// <summary>处于投技状态且接管在场的机械骷髅王头；多头同刑时优先返回抓着本地玩家的那个</summary>
        private static HeadPrimeAI FindExecutionHead(out NPC head) {
            head = null;
            if (!CWRWorld.HasBoss) {//世上无 Boss 时不必扫表
                return null;
            }
            HeadPrimeAI found = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.SkeletronPrime || HeadPrimeAI.IsMechdusa(npc)) {
                    continue;
                }
                if (HeadPrimeAI.GetStateIndex(npc) != PrimeStateIndex.ViceExecution) {
                    continue;
                }
                HeadPrimeAI ai = npc.GetOverride<HeadPrimeAI>();
                if (ai == null) {
                    continue;
                }
                if (ai.GrabTargetIndex == Main.myPlayer) {
                    head = npc;
                    return ai;
                }
                if (found == null) {
                    head = npc;
                    found = ai;
                }
            }
            return found;
        }

        /// <summary>本地启停投技运镜，仅被抓玩家</summary>
        private static void UpdateCutscene(NPC head) {
            bool playing = CutsceneDirector.CurrentClip is PrimeViceExecutionCutscene;
            if (head != null) {
                //restartSameClip:false，已播则复用
                if (!playing) {
                    CutsceneDirector.Play<PrimeViceExecutionCutscene, NPC>(head, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        #endregion
    }
}
