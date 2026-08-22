using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>
    /// 摄心镜狱受害端：本人客户端读同步抓取态（npc.ai[2] 状态 + override.ai[4] 受害者标记），
    /// 施加输入锁、位置钉锚悬浮、按本地节拍结算穿刺/掷飞伤害、启停运镜
    /// 玩家位置客户端权威，服务端从不写玩家位置；旁观者不受任何锁定与镜头接管
    /// </summary>
    internal class BrainMindSeizePlayer : ModPlayer
    {
        /// <summary>本地持环时钟（捕获包上升沿起算，-1=未被摄持）</summary>
        private int heldTick = -1;
        /// <summary>掷飞已结算</summary>
        private bool flingApplied;
        /// <summary>已结算的穿刺拍数</summary>
        private int piercesDone;
        /// <summary>上一次脚本命中的持环时刻（-1=本连段尚未命中过），用于识别自产无敌帧</summary>
        private int lastScriptHitTick = -1;
        /// <summary>掷飞落地保护余帧：持续重置摔落起点直到触地，防投技强制摔伤</summary>
        private int flingFallGuard;
        /// <summary>脑心跳时钟缓存：时钟停摆（世界冻结）时本地节拍同步暂停</summary>
        private float lastBrainClock = float.MinValue;

        /// <summary>坏包兜底：本地持环硬超时后强制自解</summary>
        private const int LocalTimeout = 60 * 10;

        /// <summary>本人是否正被摄持（仅本人客户端有效）</summary>
        internal bool Held => heldTick >= 0;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Drive();
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            //持环期间死亡（外部持续伤害等）：立即本地自解，服务端会随即断投
            if (Held) {
                Release(grantImmune: false, stopCutscene: true);
            }
        }

        /// <summary>输入锁：摄持期间清空全部操作并禁持物（仅本人客户端）</summary>
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || !Held) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
            Player.noItems = true;
            Player.noBuilding = true;
        }

        /// <summary>主驱动：侦测抓取态、钉锚、结算、运镜、释放</summary>
        private void Drive() {
            //掷飞落地保护：抛物线全程重置摔落起点，触地即撤（投技不得附赠摔落伤害）
            if (flingFallGuard > 0) {
                flingFallGuard--;
                Player.fallStart = (int)(Player.position.Y / 16f);
                if (Player.velocity.Y == 0f) {
                    flingFallGuard = 0;
                }
            }

            NPC holder = FindSeizingBrain(Player.whoAmI, out BrainOfCthulhuAI master);

            if (holder == null) {
                //抓取态消失：正常掷飞已自解；异常断投（boss 死亡/切态/断线）在此兜底解锁并收镜
                //断投点可能悬空，同样给落地保护
                if (Held) {
                    flingFallGuard = 300;
                    Release(grantImmune: true, stopCutscene: true);
                }
                //标记彻底消失后才复位掷飞闩，下一次全新捕获方可进入
                flingApplied = false;
                return;
            }

            //掷飞后服务端受害者标记尚有数帧残留：闩住防同一次抓取被误判为新捕获
            if (flingApplied && !Held) {
                return;
            }

            //捕获上升沿：斩断位移挂点，进入摄持
            if (!Held) {
                heldTick = 0;
                flingApplied = false;
                piercesDone = 0;
                lastScriptHitTick = -1;
                lastBrainClock = float.MinValue;
                if (Player.mount?.Active == true) {
                    Player.mount.Dismount(Player);
                }
                Player.RemoveAllGrapplingHooks();
                Player.velocity = Vector2.Zero;
            }

            //运镜：仅本人客户端接管镜头（restartSameClip:false，重复调用无副作用）
            if (!Main.dedServ) {
                CutsceneDirector.Play<BrainMindSeizeCutscene, NPC>(holder, restartSameClip: false);
            }

            //念力钉锚+悬浮微摆（位置客户端权威，旁观者经原版位置同步看到定身）
            //前几帧做吸附插值，读作"被念力拽入环心"而非硬瞬移
            Vector2 anchor = new(master.ai[0], master.ai[1]);
            float sway = heldTick * 0.09f;
            Vector2 holdPos = anchor + new Vector2(0f, -12f + (float)Math.Sin(sway) * 5f);
            Player.Center = heldTick < 6 ? Vector2.Lerp(Player.Center, holdPos, 0.45f) : holdPos;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fullRotationOrigin = Player.Size * 0.5f;
            Player.fullRotation = (float)Math.Sin(heldTick * 0.05f) * 0.06f;

            //穿刺三拍：本地节拍结算
            while (piercesDone < BrainMindSeizeState.PierceHurtTicks.Length
                && heldTick >= BrainMindSeizeState.PierceHurtTicks[piercesDone]) {
                ApplyScriptHurt(BrainMindSeizeState.PierceHurtFraction, holder);
                piercesDone++;
            }

            //掷飞帧：终结结算+抛掷+解锁（服务端稍后清标记，本地先行保证手感）
            //运镜不在此停：让掷飞跟身尾段自然播完（时间轴到期自停）
            if (!flingApplied && heldTick >= BrainMindSeizeState.FlingTick) {
                flingApplied = true;
                ApplyScriptHurt(BrainMindSeizeState.FlingHurtFraction, holder);
                Vector2 flingDir = master.ai[BrainMindSeizeState.SlotFlingAngle].ToRotationVector2();
                Player.velocity = flingDir * BrainMindSeizeState.FlingSpeed + new Vector2(0f, -4f);
                flingFallGuard = 300;
                Release(grantImmune: true, stopCutscene: false);
                return;
            }

            //本地时钟推进：脑心跳时钟（ai[3]）停摆＝世界冻结，节拍同步暂停
            float clock = holder.ai[3];
            if (clock != lastBrainClock) {
                lastBrainClock = clock;
                heldTick++;
            }

            //坏包兜底
            if (heldTick > LocalTimeout) {
                flingFallGuard = 300;
                Release(grantImmune: true, stopCutscene: true);
            }
        }

        /// <summary>
        /// 脚本化摄持伤害：走常规 Hurt 路径（原版受伤包自动同步），
        /// 伤害为最大生命固定比例，钳制永不致死（至少留 1 HP）
        /// 无敌帧规则：仅清除"上一拍脚本命中自产"的无敌（防十字章免疫整套连段）；
        /// 进环前的既有无敌与闪避奖励无敌（命中失败不记录）全部尊重
        /// </summary>
        private void ApplyScriptHurt(float fraction, NPC source) {
            if (!Player.Alives() || Player.statLife <= 1) {
                return;
            }

            //自产无敌识别：上一拍确实命中过且间隔在常规受伤无敌上限内
            if (lastScriptHitTick >= 0 && heldTick - lastScriptHitTick <= 84 && Player.immune) {
                Player.immune = false;
                Player.immuneTime = 0;
            }

            int damage = (int)(Player.statLifeMax2 * fraction);
            damage = Math.Min(damage, Player.statLife - 1);
            if (damage <= 0) {
                return;
            }
            Player.HurtInfo hurtInfo = new() {
                DamageSource = PlayerDeathReason.ByNPC(source.whoAmI),
                SourceDamage = damage,
                Damage = damage,
                HitDirection = 0,
                Knockback = 0f,
                Dodgeable = true,
                PvP = false,
                CooldownCounter = -1,
            };
            int lifeBefore = Player.statLife;
            Player.Hurt(hurtInfo);
            //以血量实变判命中（闪避/魔免时不记录，其奖励无敌得以保留）
            if (Player.statLife < lifeBefore) {
                lastScriptHitTick = heldTick;
            }
        }

        /// <summary>解除摄持：复位姿态、按需给足释放无敌帧；正常掷飞让运镜尾段播完，异常断投立即收镜</summary>
        private void Release(bool grantImmune, bool stopCutscene) {
            heldTick = -1;
            Player.fullRotation = 0f;
            Player.fallStart = (int)(Player.position.Y / 16f);
            if (grantImmune) {
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 90);
            }
            if (stopCutscene && !Main.dedServ && Player.whoAmI == Main.myPlayer
                && CutsceneDirector.CurrentClip is BrainMindSeizeCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>找到正在摄持指定玩家的脑（ai[2] 状态粗筛 + 接管在场验证 + 受害者标记精配）</summary>
        internal static NPC FindSeizingBrain(int playerIndex, out BrainOfCthulhuAI master) {
            master = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.BrainofCthulhu) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)BrainStateIndex.MindSeize) {
                    continue;
                }
                //验证本模组接管在场：原版 AI 下 ai[2] 可能撞值
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(BrainOfCthulhuAI), out NPCOverride brainOverride)
                    || brainOverride is not BrainOfCthulhuAI brainMaster) {
                    continue;
                }
                if ((int)brainMaster.ai[BrainMindSeizeState.SlotVictim] - 1 != playerIndex) {
                    continue;
                }
                master = brainMaster;
                return npc;
            }
            return null;
        }

        public override void OnRespawn() => ResetLocal();

        public override void OnEnterWorld() => ResetLocal();

        public override void PlayerDisconnect() => ResetLocal();

        private void ResetLocal() {
            heldTick = -1;
            flingApplied = false;
            piercesDone = 0;
            lastScriptHitTick = -1;
            flingFallGuard = 0;
        }
    }
}
