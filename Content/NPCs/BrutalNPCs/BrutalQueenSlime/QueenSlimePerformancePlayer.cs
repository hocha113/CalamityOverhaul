using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime
{
    /// <summary>
    /// 水晶囚舞受害者端：仅 Main.myPlayer 生效。
    /// 钉位/清速/免摔由被抓玩家自己的客户端施加(玩家位置客户端权威，服务器绝不写)；
    /// 连段 Hurt 与终结掷飞也在本端按同步时钟结算；运镜只在本端启停。
    /// </summary>
    internal class QueenSlimePerformancePlayer : ModPlayer
    {
        /// <summary>演出节拍高水位(0..2=三踢)，防同步回卷重放</summary>
        private int lastKickFired = -1;
        /// <summary>最后一次被钉住时的囚舞时钟</summary>
        private int lastPinnedTick = -1;
        /// <summary>终结掷飞已结算</summary>
        private bool thrownApplied;
        /// <summary>上一帧处于被抓状态(SetControls 用)</summary>
        private bool wasGrabbed;
        /// <summary>释放后免摔窗口</summary>
        private int postGrabSafeTicks;
        /// <summary>掷飞方向缓存(茧相对皇后的水平侧)</summary>
        private int throwSignCache = 1;
        /// <summary>属主皇后缓存(释放帧仍需结算终结时使用)</summary>
        private int queenIndexCache = -1;
        /// <summary>坐骑/钩爪已卸(每次抓取只做一次)</summary>
        private bool dismountDone;

        /// <summary>囚舞运镜期间的镜头震动(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not QueenWaltzGrabCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //释放后免摔窗口
            if (postGrabSafeTicks > 0) {
                postGrabSafeTicks--;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            NPC queen = FindGrabbingQueen(Player.whoAmI);
            bool playing = CutsceneDirector.CurrentClip is QueenWaltzGrabCutscene;

            if (queen != null) {
                HandleGrabbed(queen);
                if (!playing) {
                    CutsceneDirector.Play<QueenWaltzGrabCutscene, NPC>(queen, restartSameClip: false);
                }
            }
            else {
                if (wasGrabbed) {
                    OnReleased();
                }
                if (playing) {
                    CutsceneDirector.Stop();
                }
            }
        }

        /// <summary>被抓帧：钉位、强制无敌(挡杂弹)、按时钟结算踢击与终结</summary>
        private void HandleGrabbed(NPC queen) {
            int t = QueenCrystalPrisonWaltzState.GrabTick(queen);
            Projectile prison = QueenCrystalPrisonWaltzState.FindPrison(queen);
            wasGrabbed = true;
            lastPinnedTick = t;
            queenIndexCache = queen.whoAmI;

            //掷飞水平侧缓存：茧相对皇后的方位(终结帧皇后穿心后不再更新)
            if (prison != null && t < QueenCrystalPrisonWaltzState.FinisherTick - 2) {
                float dx = prison.Center.X - queen.Center.X;
                if (Math.Abs(dx) > 8f) {
                    throwSignCache = dx >= 0f ? 1 : -1;
                }
            }

            //本端时钟先到终结拍(单机/客户端领先)：直接掷飞
            if (!thrownApplied && t >= QueenCrystalPrisonWaltzState.FinisherTick) {
                ApplyThrow(queen);
                return;
            }
            if (thrownApplied) {
                return;
            }

            //位移类挂点立即斩断(仅一次)
            if (!dismountDone) {
                dismountDone = true;
                if (Player.mount?.Active == true) {
                    Player.mount.Dismount(Player);
                }
                Player.RemoveAllGrapplingHooks();
                Player.StopVanityActions();
            }

            //钉位：成茧初段硬插值吸附吸收压点误差，之后硬贴茧心
            if (prison != null) {
                if (t < QueenCrystalPrisonWaltzState.CocoonTime + 8) {
                    Player.Center = Vector2.Lerp(Player.Center, prison.Center, 0.45f);
                }
                else {
                    Player.Center = prison.Center;
                }
            }
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);

            //囚茧内强制无敌：屏蔽随从杂弹误伤，剧本踢击直调 Hurt 不受其阻
            Player.immune = true;
            Player.immuneTime = 2;
            Player.immuneNoBlink = true;

            //三踢结算(高水位防重放；时钟跳变跨拍时逐帧补拍，每帧至多一踢防伤害脉冲叠加)
            int[] kicks = QueenCrystalPrisonWaltzState.KickTicks;
            for (int i = 0; i < kicks.Length; i++) {
                if (t >= kicks[i] && lastKickFired < i) {
                    lastKickFired = i;
                    ApplyKick(queen);
                    break;
                }
            }
        }

        /// <summary>单踢结算：难度缩放伤害+免死阀(不足则免伤照演)</summary>
        private void ApplyKick(NPC queen) {
            int raw = queen.GetAttackDamage_ScaledByStrength(QueenCrystalPrisonWaltzState.KickDamageBase);
            if (Player.statLife > raw + 20) {
                ScriptedHurt(PlayerDeathReason.ByNPC(queen.whoAmI), raw, 0);
            }
            RequestShake(5f, 14);
        }

        /// <summary>
        /// 剧本结算：Hurt 默认被无敌帧吞掉(上游 flag=!immune)，而茧内每帧强制 immune 挡杂弹，
        /// 故结算前显式清无敌、结算后立刻恢复(OniPlayerDismember 同款先例)。
        /// </summary>
        private void ScriptedHurt(PlayerDeathReason reason, int damage, int hitDirection) {
            Player.immune = false;
            Player.immuneTime = 0;
            Player.Hurt(reason, damage, hitDirection, knockback: 0f);
            Player.immune = true;
        }

        /// <summary>终结掷飞：伤害(免死阀)+抛出速度+足额无敌+免摔</summary>
        private void ApplyThrow(NPC queen) {
            thrownApplied = true;
            if (queen != null) {
                int raw = queen.GetAttackDamage_ScaledByStrength(QueenCrystalPrisonWaltzState.FinisherDamageBase);
                if (Player.statLife > raw + 20) {
                    ScriptedHurt(PlayerDeathReason.ByNPC(queen.whoAmI), raw, throwSignCache);
                }
            }
            Player.velocity = new Vector2(throwSignCache * 14.5f, -11.5f);
            Player.immune = true;
            Player.immuneTime = 90;
            Player.immuneNoBlink = false;
            Player.fallStart = (int)(Player.position.Y / 16f);
            postGrabSafeTicks = 90;
            RequestShake(9f, 22);
        }

        /// <summary>释放收尾：贴近终结窗释放视作终结(服务端先行清目标的时序)，否则软释放</summary>
        private void OnReleased() {
            if (!thrownApplied && lastPinnedTick >= QueenCrystalPrisonWaltzState.FinisherTick - 24) {
                NPC queen = queenIndexCache >= 0 && queenIndexCache < Main.maxNPCs
                    && Main.npc[queenIndexCache].active && Main.npc[queenIndexCache].type == NPCID.QueenSlimeBoss
                    ? Main.npc[queenIndexCache] : null;
                ApplyThrow(queen);
            }
            else if (!thrownApplied) {
                //异常提前释放：不结算伤害，给短无敌与免摔防冷枪
                Player.immune = true;
                Player.immuneTime = 60;
                postGrabSafeTicks = 60;
            }
            ResetGrabLocals();
        }

        private void ResetGrabLocals() {
            lastKickFired = -1;
            lastPinnedTick = -1;
            thrownApplied = false;
            wasGrabbed = false;
            dismountDone = false;
            queenIndexCache = -1;
        }

        /// <summary>被抓期间清操控输入并禁持物(回忆药水等传送物品一并封死)</summary>
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || !wasGrabbed || thrownApplied) {
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

        /// <summary>死亡帧不再走 PostUpdate，运镜与本地状态在此兜底清理</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (CutsceneDirector.CurrentClip is QueenWaltzGrabCutscene) {
                CutsceneDirector.Stop();
            }
            if (wasGrabbed) {
                ResetGrabLocals();
            }
            postGrabSafeTicks = 0;
        }

        /// <summary>正抓着指定玩家、且确认被本模组接管的皇后，无则null</summary>
        private static NPC FindGrabbingQueen(int playerIndex) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.QueenSlimeBoss) {
                    continue;
                }
                if (!QueenCrystalPrisonWaltzState.IsGrabbing(npc, playerIndex)) {
                    continue;
                }
                //确认接管在场，防裸原版皇后 ai 撞值
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(QueenSlimeAI), out NPCOverride queenOverride)
                    || queenOverride is not QueenSlimeAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }
    }
}
