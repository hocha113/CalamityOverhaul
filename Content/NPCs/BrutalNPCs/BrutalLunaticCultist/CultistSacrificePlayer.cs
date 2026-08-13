using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>
    /// 献祭投技受害者侧：只在被抓玩家自己的客户端运行——
    /// 位置钉锁/清钩爪坐骑/锁输入/按本地时间轴结算连段伤害/掷出与免疫/运镜启停；
    /// 玩家位置是客户端权威，服务器绝不直写（读同步来的 NPC 抓取态驱动）
    /// </summary>
    internal class CultistSacrificePlayer : ModPlayer
    {
        /// <summary>当前帧是否处于锁身（SetControls 消费）</summary>
        private bool lockedNow;
        /// <summary>锁身经过帧数（吸附平滑用）</summary>
        private int lockTicks;
        /// <summary>已结算的连段拍位掩码（1拍/2拍/终结/掷出）</summary>
        private int firedBeatMask;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Observe();
        }

        /// <summary>死亡帧也要清理（死亡玩家不跑 PostUpdate）</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            ReleaseLocal();
        }

        /// <summary>观察本地是否被献祭锁身，驱动锁定与运镜的启停</summary>
        private void Observe() {
            (NPC boss, CultistBossAI bossOverride, CultistSacrificeGrabState grabState) = FindGrabBoss();

            bool grabbingMe = grabState != null
                && bossOverride.Context.GrabTargetIndex == Player.whoAmI
                && bossOverride.Context.GrabResult == 1
                && grabState.Timer > CultistSacrificeGrabState.SealCloseEnd
                && grabState.Timer <= CultistSacrificeGrabState.ReleaseEnd
                && !Player.dead;

            if (grabbingMe) {
                ApplyLock(boss, bossOverride.Context, grabState.Timer);
                //运镜只接管被抓者本机
                if (CutsceneDirector.CurrentClip is not CultistSacrificeCutscene) {
                    CutsceneDirector.Play<CultistSacrificeCutscene, NPC>(boss, restartSameClip: false);
                }
            }
            else {
                ReleaseLocal();
            }
        }

        /// <summary>找到正抓取中的邪教徒本体，无则全 null</summary>
        private static (NPC, CultistBossAI, CultistSacrificeGrabState) FindGrabBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.CultistBoss) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)CultistStateIndex.SacrificeGrab) {
                    continue;
                }
                //必须确认接管在场（原版 AI 的 ai[2] 可能撞值）
                if (!npc.TryGetOverride(out CultistBossAI bossOverride) || bossOverride?.Context == null) {
                    continue;
                }
                if (bossOverride.Machine?.CurrentState is not CultistSacrificeGrabState grabState) {
                    continue;
                }
                return (npc, bossOverride, grabState);
            }
            return (null, null, null);
        }

        /// <summary>逐帧锁定：吸附→钉死→按拍结算→掷出</summary>
        private void ApplyLock(NPC boss, CultistStateContext ctx, int t) {
            //掷出之后不再钉人（玩家带着掷出速度自由落体，控制权已归还）
            bool launched = (firedBeatMask & 8) != 0;
            if (launched) {
                lockedNow = false;
                return;
            }

            if (!lockedNow) {
                //锁身第一帧：斩断位移类挂点
                Player.StopVanityActions();
                if (Player.mount?.Active == true) {
                    Player.mount.Dismount(Player);
                }
                lockTicks = 0;
                firedBeatMask = 0;
            }
            lockedNow = true;
            lockTicks++;

            Vector2 anchor = CultistSacrificeGrabState.SealCenter(ctx, t);
            //前 8 帧平滑吸附，之后钉死在阵心
            Player.Center = lockTicks <= 8
                ? Vector2.Lerp(Player.Center, anchor, 0.4f)
                : anchor;
            Player.velocity = Vector2.Zero;
            Player.RemoveAllGrapplingHooks();
            Player.fallStart = (int)(Player.position.Y / 16f);
            //持续短免疫挡住场上残留杂伤（如仍在场的幻影龙接触），连段拍走直接 Hurt 不受其影响
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            //不闪烁：镜头贴脸，被缚者保持实体感
            Player.immuneNoBlink = true;
            Player.noItems = true;

            //连段拍（>= 判定 + 掩码：跳帧也不漏拍、不重拍）
            TryBeat(boss, t, CultistSacrificeGrabState.Beat1Hit, 1, 46f, 32f);
            TryBeat(boss, t, CultistSacrificeGrabState.Beat2Hit, 2, 46f, 32f);
            TryBeat(boss, t, CultistSacrificeGrabState.FinaleHit, 4, 60f, 42f);

            //终结后 2 帧掷出：残速清干净再赋掷出速度+足额免疫
            if (t >= CultistSacrificeGrabState.FinaleHit + 2 && (firedBeatMask & 8) == 0) {
                firedBeatMask |= 8;
                int side = Player.direction != 0 ? -Player.direction : 1;
                Player.velocity = new Vector2(side * 8.5f, -6f);
                Player.SetImmuneTimeForAllTypes(90);
                Player.fallStart = (int)(Player.position.Y / 16f);
                lockedNow = false;
            }
        }

        /// <summary>单拍结算：敌对弹幕×2口径+保命阀（永不致死，留 1 血）</summary>
        private void TryBeat(NPC boss, int t, int hitTick, int mask, float normal, float expert) {
            if (t < hitTick || (firedBeatMask & mask) != 0) {
                return;
            }
            firedBeatMask |= mask;
            //迟到超过 20 帧的拍作废（极端同步延迟下不叠帧爆发）
            if (t - hitTick > 20) {
                return;
            }

            if (Player.creativeGodMode) {
                return;
            }
            //与本 boss 其余弹幕同口径：难度感知数值 ×2（敌对弹幕对玩家的原版惯例）
            int raw = boss.GetAttackDamage_ForProjectiles(normal, expert) * 2;
            //保命阀：原始值不越过"当前生命-1"，过防御后必不致死
            raw = Math.Min(raw, Player.statLife - 1);
            if (raw <= 0) {
                return;
            }
            Player.Hurt(PlayerDeathReason.ByNPC(boss.whoAmI), raw, 0,
                pvp: false, quiet: false, cooldownCounter: -1, dodgeable: true, knockback: 0f);
        }

        /// <summary>解除本地锁定与运镜（异常断投/死亡/正常释放共用出口）</summary>
        private void ReleaseLocal() {
            if (lockedNow) {
                lockedNow = false;
                //异常断投（未走到掷出拍）：补足额免疫，清残速
                if ((firedBeatMask & 8) == 0) {
                    Player.velocity = Vector2.Zero;
                    Player.SetImmuneTimeForAllTypes(60);
                }
            }
            firedBeatMask = 0;
            lockTicks = 0;
            if (CutsceneDirector.CurrentClip is CultistSacrificeCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>锁身期间清空全部操作输入（运镜 InputLock 之外的双保险）</summary>
        public override void SetControls() {
            if (!lockedNow || Player.whoAmI != Main.myPlayer) {
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
    }
}
