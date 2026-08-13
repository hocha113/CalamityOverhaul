using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    /// <summary>钢环绞缠的被抓端：只在被抓玩家自己的客户端锁控/钉身/按节拍结算连段伤害/释放击飞；
    /// 服务器与旁观客户端不做任何玩家位移写入。节拍表须与 DestroyerCoilCrushState 的常量对齐</summary>
    internal class DestroyerGrabPlayer : ModPlayer
    {
        #region 节拍与伤害常量(占最大生命比例，Hurt(HurtInfo)按最终伤结算)
        private const int PullInTime = 10;
        private const int BeatSeize = 22;
        private const int BeatCross1 = 64;
        private const int BeatCross2 = 90;
        private const int BeatSqueeze1 = 118;
        private const int BeatSqueeze2 = 150;
        /// <summary>终结判定窗起点</summary>
        private const int FinisherWindowStart = 196;
        /// <summary>兜底击飞帧，窗内未命中也在此释放</summary>
        private const int FinisherFallback = 240;
        /// <summary>本地硬超时，异常时强制解锁</summary>
        private const int HardTimeout = 320;

        private const float SeizeFraction = 0.04f;
        private const float CrossFraction = 0.06f;
        private const float SqueezeFraction = 0.06f;
        private const float FinisherFraction = 0.13f;
        #endregion

        private bool lockActive;
        private int lockTicks;
        private int boundHead = -1;
        private Vector2 grabStartPos;
        private readonly bool[] beatDone = new bool[5];
        private bool finisherDone;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            NPC head = FindGrabbingHead();

            if (!lockActive) {
                if (head != null && !Player.dead && !Player.ghost) {
                    BeginLock(head);
                }
                return;
            }

            //异常出口：状态离场/头失效
            if (head == null || head.whoAmI != boundHead) {
                AbortRelease();
                return;
            }

            //boss被时停时冻结本地时间线(节拍/超时暂停)，只保持钉身，避免演出跑到冻结的boss前面
            bool hostFrozen = TimeFreezeSystem.IsFrozen(head);
            if (!hostFrozen && ++lockTicks > HardTimeout) {
                AbortRelease();
                return;
            }

            Vector2 center = new(head.ai[0], head.ai[1]);

            //外力传送(回忆等)赢过钉身→立刻断投，不往回拽
            if (Player.Distance(center) > 1200f && lockTicks > PullInTime) {
                AbortRelease();
                return;
            }

            //钉身：入环收拢后定在环心，带小幅挣扎抖动
            Vector2 pin;
            if (lockTicks <= PullInTime) {
                float t = lockTicks / (float)PullInTime;
                pin = Vector2.Lerp(grabStartPos, center, t * t);
            }
            else {
                float wob = lockTicks * 0.31f;
                pin = center + new Vector2((float)Math.Sin(wob) * 2.2f, (float)Math.Cos(wob * 1.7f) * 2.2f);
            }
            Player.Center = pin;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);

            //连段节拍(玩家已死则静默跳过，出口交给 UpdateDead；宿主时停时节拍同停)
            if (!Player.dead && !hostFrozen) {
                UpdateBeats(head, center);
            }
        }

        /// <summary>找到正抓着本地玩家的毁灭者头(须确认接管在场)，无则null</summary>
        private NPC FindGrabbingHead() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.TheDestroyer) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)DestroyerStateIndex.CoilCrush) {
                    continue;
                }
                if ((int)npc.ai[3] != Player.whoAmI) {
                    continue;
                }
                //确认接管在场，防原版AI的ai槽撞值
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(DestroyerHeadAI), out NPCOverride headOverride)
                    || headOverride is not DestroyerHeadAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        #region 锁定与释放

        private void BeginLock(NPC head) {
            lockActive = true;
            lockTicks = 0;
            boundHead = head.whoAmI;
            grabStartPos = Player.Center;
            finisherDone = false;
            Array.Clear(beatDone, 0, beatDone.Length);

            //斩断位移挂点
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();
            Player.velocity = Vector2.Zero;

            //运镜只在被抓者本端接管，restartSameClip:false 幂等
            CutsceneDirector.Play<DestroyerCoilCrushCutscene, NPC>(head, restartSameClip: false);
        }

        /// <summary>终结击飞释放：给足无敌帧+恢复翅膀；立刻停运镜，掷出瞬间就把操控还给玩家</summary>
        private void ReleaseWithFling(Vector2 flingDir) {
            lockActive = false;
            boundHead = -1;

            Player.velocity = flingDir.SafeNormalize(-Vector2.UnitY) * 21f + new Vector2(0f, -4f);
            Player.fallStart = (int)(Player.position.Y / 16f);
            GrantReleaseImmunity(90);
            Player.wingTime = Player.wingTimeMax;

            if (CutsceneDirector.CurrentClip is DestroyerCoilCrushCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>异常断投：解锁+停运镜+短无敌</summary>
        private void AbortRelease() {
            lockActive = false;
            boundHead = -1;

            Player.velocity = Vector2.Zero;
            GrantReleaseImmunity(60);
            Player.wingTime = Player.wingTimeMax;

            if (CutsceneDirector.CurrentClip is DestroyerCoilCrushCutscene) {
                CutsceneDirector.Stop();
            }
        }

        private void GrantReleaseImmunity(int frames) {
            Player.immune = true;
            Player.immuneTime = frames;
            Player.SetImmuneTimeForAllTypes(frames);
        }

        #endregion

        #region 连段节拍

        private void UpdateBeats(NPC head, Vector2 center) {
            TryBeat(0, BeatSeize, head, SeizeFraction);
            TryBeat(1, BeatCross1, head, CrossFraction);
            TryBeat(2, BeatCross2, head, CrossFraction);
            TryBeat(3, BeatSqueeze1, head, SqueezeFraction);
            TryBeat(4, BeatSqueeze2, head, SqueezeFraction);

            //终结：贯穿窗内机头逼近环心即命中；窗尾兜底释放
            if (!finisherDone && lockTicks >= FinisherWindowStart) {
                bool headClose = head.Distance(Player.Center) < 140f;
                if (headClose || lockTicks >= FinisherFallback) {
                    finisherDone = true;
                    Vector2 flingDir = head.velocity.Length() > 4f
                        ? head.velocity.SafeNormalize(-Vector2.UnitY)
                        : -Vector2.UnitY;
                    if (headClose) {
                        ApplyBeatHurt(head, FinisherFraction, Math.Sign(flingDir.X));
                    }
                    ReleaseWithFling(flingDir);
                }
            }
        }

        private void TryBeat(int slot, int tick, NPC head, float fraction) {
            if (beatDone[slot] || lockTicks < tick) {
                return;
            }
            beatDone[slot] = true;
            bool clamped = ApplyBeatHurt(head, fraction, 0);
            //中途打到留命线→提前击飞结束，不再空耗演出
            if (clamped) {
                finisherDone = true;
                ReleaseWithFling(-Vector2.UnitY);
            }
        }

        /// <summary>脚本化连段伤害：按最大生命比例最终结算，永不打死(致死则减免留命)；返回是否触发了留命减免</summary>
        private bool ApplyBeatHurt(NPC head, float fraction, int hitDirection) {
            if (Player.dead || Player.creativeGodMode) {
                return false;
            }

            int damage = Math.Max((int)(Player.statLifeMax2 * fraction), 1);
            bool lethalClamped = false;
            if (Player.statLife - damage <= 0) {
                damage = Math.Max(Player.statLife - 1, 0);
                lethalClamped = true;
            }
            if (damage <= 0) {
                return lethalClamped;
            }

            //节拍必落：清掉上一拍的受击无敌
            Player.immune = false;
            Player.immuneTime = 0;
            Player.HurtInfo info = new() {
                DamageSource = PlayerDeathReason.ByNPC(head.whoAmI),
                SourceDamage = damage,
                Damage = damage,
                HitDirection = hitDirection,
                Knockback = 0f,
                Dodgeable = true,
                PvP = false,
                CooldownCounter = -1,
            };
            Player.Hurt(info);
            return lethalClamped;
        }

        #endregion

        /// <summary>锁定期间清空全部操控位</summary>
        public override void SetControls() {
            if (!lockActive) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
        }

        /// <summary>死亡不再走 PostUpdate，出口挂这里</summary>
        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer && lockActive) {
                AbortRelease();
            }
        }
    }
}
