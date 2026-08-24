using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>死亡演出玩家侧</summary>
    internal class PrimeDeathPerformancePlayer : ModPlayer
    {
        //拖拽起点缓存（被抓玩家本地）
        private bool dragStarted;
        private Vector2 dragStartPos;

        /// <summary>请求死亡运镜震动，本地</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            //非死亡运镜期间忽略
            if (CutsceneDirector.CurrentClip is not PrimeDeathCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            HeadPrimeAI headAI = FindPerformanceHead(out NPC head);

            //演出开始时本地播放过场运镜，头部消失/演出结束时平滑收尾
            UpdateCutscene(headAI, head);

            if (headAI == null || head == null || Player.whoAmI != headAI.DeathTargetIndex) {
                dragStarted = false;
                return;
            }

            Vector2 lift = headAI.DeathLiftPoint;
            switch (headAI.CurrentDeathPhase) {
                case PrimeDeathPhase.Drag: {
                    if (!dragStarted) {
                        dragStarted = true;
                        dragStartPos = Player.Center;
                    }
                    float p = (headAI.DeathTimer - PrimeDeathState.PhaseLungeEnd)
                        / (float)(PrimeDeathState.PhaseDragEnd - PrimeDeathState.PhaseLungeEnd);
                    p = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(p, 0f, 1f));
                    LockPlayerAt(Vector2.Lerp(dragStartPos, lift, p));
                    break;
                }
                case PrimeDeathPhase.Roar: {
                    LockPlayerAt(lift);
                    break;
                }
                case PrimeDeathPhase.Finale: {
                    //终爆瞬间将玩家掀飞释放
                    if (headAI.DeathTimer == PrimeDeathState.PhaseRoarEnd) {
                        Vector2 knock = (Player.Center - head.Center).SafeNormalize(Vector2.UnitY) * 24f;
                        Player.velocity = knock;
                    }
                    dragStarted = false;
                    break;
                }
                default: {
                    dragStarted = false;
                    break;
                }
            }
        }

        /// <summary>本地启停 PrimeDeathCutscene 过场</summary>
        private static void UpdateCutscene(HeadPrimeAI headAI, NPC head) {
            bool playing = CutsceneDirector.CurrentClip is PrimeDeathCutscene;
            if (headAI != null && head != null) {
                //restartSameClip:false，已播则复用
                if (!playing) {
                    CutsceneDirector.Play<PrimeDeathCutscene, NPC>(head, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        private void LockPlayerAt(Vector2 center) {
            Player.Center = center;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.gravity = 0f;
            //演出无敌，避免被其它来源误伤打断
            Player.immune = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }
        }

        /// <summary>死亡演出中的头部，无则null</summary>
        private static HeadPrimeAI FindPerformanceHead(out NPC head) {
            head = null;
            int h = HeadPrimeAI.ActivePerformanceHead;
            if (h < 0 || h >= Main.maxNPCs) {
                HeadPrimeAI.ActivePerformanceHead = -1;
                return null;
            }
            NPC npc = Main.npc[h];
            if (!npc.active || npc.type != NPCID.SkeletronPrime) {
                HeadPrimeAI.ActivePerformanceHead = -1;
                return null;
            }
            HeadPrimeAI ai = npc.GetOverride<HeadPrimeAI>();
            if (ai == null || !ai.InDeathPerformance) {
                HeadPrimeAI.ActivePerformanceHead = -1;
                return null;
            }
            head = npc;
            return ai;
        }
    }
}
