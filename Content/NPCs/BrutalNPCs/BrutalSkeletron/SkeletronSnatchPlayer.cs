using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>
    /// 合掌拍捉受害者本地侧：读同步来的头部抓取广播，在被抓玩家自己的客户端施加位置钉笼、<br/>
    /// 节拍伤害（走 Hurt 常规路径）、运镜与输入锁；服务器与旁观客户端不做任何接管<br/>
    /// 玩家位移是客户端权威，钉笼只能也只需在受害者本机做
    /// </summary>
    internal class SkeletronSnatchPlayer : ModPlayer
    {
        //以下字段仅在 Player.whoAmI == Main.myPlayer 的实例上有意义
        private bool locked;
        private int lockTicks;
        private int headIndex = -1;
        private int subLatch = -1;
        /// <summary>本次投技吃到的全部伤害（含弹幕），终结伤害预算用</summary>
        private int hurtTaken;

        /// <summary>本地保底：抓取锁定的绝对最长帧数（服务端异常时自行脱困）</summary>
        private const int MaxLockTicks = 420;

        #region 全端一致的抓取事实查询

        /// <summary>该玩家当前是否被拍捉夹持（由同步 NPC 状态推导，各端一致）</summary>
        internal static bool IsSnatched(Player player) {
            NPC head = FindSnatchingHead(player.whoAmI);
            if (head == null) {
                return false;
            }
            int sub = (int)head.ai[SkeletronAiSlots.HeadParamB];
            return sub >= SkeletronPalmSnatchState.SubClamp && sub <= SkeletronPalmSnatchState.SubSlam;
        }

        /// <summary>正抓着该玩家的骷髅王头；无则 null</summary>
        internal static NPC FindSnatchingHead(int playerWhoAmI) {
            NPC head = CheckHead(SkeletronHeadAI.ActiveHeadIndex, playerWhoAmI);
            if (head != null) {
                return head;
            }
            //多头兜底（槽位登记只记最后活跃头）
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.SkeletronHead) {
                    continue;
                }
                head = CheckHead(npc.whoAmI, playerWhoAmI);
                if (head != null) {
                    return head;
                }
            }
            return null;
        }

        private static NPC CheckHead(int index, int playerWhoAmI) {
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[index];
            if (!npc.active || npc.type != NPCID.SkeletronHead) {
                return null;
            }
            if ((int)npc.ai[SkeletronAiSlots.HeadStateSlot] != (int)SkeletronStateIndex.PalmSnatch) {
                return null;
            }
            if ((int)npc.ai[SkeletronAiSlots.HeadParamA] != playerWhoAmI + 1) {
                return null;
            }
            //确认接管在场（原版骷髅王不认这套广播语义）
            if (!npc.TryGetOverride(out SkeletronHeadAI _)) {
                return null;
            }
            return npc;
        }

        #endregion

        /// <summary>被夹持期间禁用物品（回忆药水等瞬移逃逸口），各端从同步状态推同一结论</summary>
        public override bool CanUseItem(Item item) {
            return !IsSnatched(Player);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (!locked) {
                TryBeginLock();
                //残留运镜兜底（异常中断后清场）
                if (!locked && CutsceneDirector.CurrentClip is SkeletronSnatchCutscene) {
                    CutsceneDirector.Stop();
                }
                return;
            }

            lockTicks++;

            NPC head = CheckHead(headIndex, Player.whoAmI);
            int sub = head != null ? (int)head.ai[SkeletronAiSlots.HeadParamB] : -1;

            //异常出口：广播被清/状态被切/头消失/本地超时 → 静默释放（无终结伤害）
            if (head == null || sub >= SkeletronPalmSnatchState.SubWhiff || lockTicks > MaxLockTicks) {
                Release(null, finisher: false);
                return;
            }

            //终结信号：砸地完成 → 结算终结伤害并弹出
            if (sub >= SkeletronPalmSnatchState.SubRecover) {
                Release(head, finisher: true);
                return;
            }

            ProcessBeats(head, sub);
            PinToCage(head);
        }

        /// <summary>死亡帧 PostUpdate 不再执行，死中清场走这里</summary>
        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer && locked) {
                Release(null, finisher: false);
            }
        }

        /// <summary>累计投技期间实际吃到的伤害（含环轰弹幕），供终结预算判断</summary>
        public override void OnHurt(Player.HurtInfo info) {
            if (locked && Player.whoAmI == Main.myPlayer) {
                hurtTaken += info.Damage;
            }
        }

        #region 锁定生命周期

        private void TryBeginLock() {
            NPC head = FindSnatchingHead(Player.whoAmI);
            if (head == null || Player.dead) {
                return;
            }
            int sub = (int)head.ai[SkeletronAiSlots.HeadParamB];
            if (sub < SkeletronPalmSnatchState.SubClamp || sub > SkeletronPalmSnatchState.SubSlam) {
                return;
            }

            locked = true;
            lockTicks = 0;
            headIndex = head.whoAmI;
            subLatch = sub;
            hurtTaken = 0;

            //断钩爪、下坐骑（新输入由运镜输入锁拦截）
            Player.RemoveAllGrapplingHooks();
            if (Player.mount.Active) {
                Player.mount.Dismount(Player);
            }
            Player.fallStart = (int)(Player.position.Y / 16f);

            //夹持顿帧伤害：走 Hurt 常规路径，留命钳制
            ApplyBeatDamage(head, SkeletronDirector.SnatchClampDamage);

            //运镜只在受害者本机播放
            CutsceneDirector.Play<SkeletronSnatchCutscene, NPC>(head, restartSameClip: false);
            RequestShake(7f, 14);
        }

        /// <summary>释放：终结节拍（可选）→ 弹出 + 足额无敌帧 + 停运镜</summary>
        private void Release(NPC head, bool finisher) {
            if (finisher && head != null && !Player.dead) {
                //预算阀：整套投技伤害超限则跳过终结伤害（满血不可能被一套处死）
                int budget = (int)(Player.statLifeMax2 * SkeletronDirector.SnatchDamageBudget);
                if (hurtTaken < budget) {
                    ApplyBeatDamage(head, SkeletronDirector.SnatchSlamDamage);
                }
                //砸地弹出
                Player.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -6.5f);
                RequestShake(9f, 18);
            }

            locked = false;
            headIndex = -1;
            subLatch = -1;
            lockTicks = 0;

            //足额释放保护
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SkeletronDirector.SnatchReleaseImmune);
            Player.SetImmuneTimeForAllTypes(SkeletronDirector.SnatchReleaseImmune);
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (CutsceneDirector.CurrentClip is SkeletronSnatchCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>节拍伤害：难度缩放 + 留命钳制 + 尊重无敌帧，在受害者本机结算</summary>
        private void ApplyBeatDamage(NPC head, int baseDamage) {
            if (Player.immune || Player.dead) {
                return;
            }
            int damage = head.GetAttackDamage_ScaledByStrength(baseDamage);
            //留命钳制：护甲只会再降低实伤，钳完必然 ≥1 HP
            damage = Math.Min(damage, Math.Max(0, Player.statLife - 1));
            if (damage <= 0) {
                return;
            }
            Player.Hurt(PlayerDeathReason.ByNPC(head.whoAmI), damage, 0);
        }

        /// <summary>子相位前进沿的本机镜头反馈</summary>
        private void ProcessBeats(NPC head, int sub) {
            if (sub == subLatch) {
                return;
            }
            subLatch = sub;
            switch (sub) {
                case SkeletronPalmSnatchState.SubLift:
                    RequestShake(3.5f, 10);
                    break;
                case SkeletronPalmSnatchState.SubWindup:
                    RequestShake(5f, 14);
                    break;
                case SkeletronPalmSnatchState.SubSlam:
                    RequestShake(4f, 20);
                    break;
            }
        }

        /// <summary>钉笼：位置锁到双掌中点（玩家位移客户端权威，由本机施加）</summary>
        private void PinToCage(NPC head) {
            Vector2 cage = SkeletronPalmSnatchState.GetCageCenter(head);
            Player.Center = cage;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>运镜接管相机后普通震屏可能失效，走导演器震动</summary>
        private static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not SkeletronSnatchCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.88f, duration);
        }

        #endregion
    }
}
