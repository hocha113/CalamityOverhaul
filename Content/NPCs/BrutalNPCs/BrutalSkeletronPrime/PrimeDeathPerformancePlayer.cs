using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 机械骷髅王死亡演出的运镜与玩家控制层。
    /// <list type="bullet">
    /// <item>所有玩家本地通过 <see cref="CutsceneCamera"/> 将镜头锁定到演出（围观这场处决）。</item>
    /// <item>仅被抓的目标玩家在拖拽/举起阶段被强制锁定到头部正前方，终爆瞬间被掀飞释放。</item>
    /// <item>屏幕震动经 <see cref="RequestShake"/> 由头部演出逻辑请求，统一交由运镜叠加。</item>
    /// </list>
    /// </summary>
    internal class PrimeDeathPerformancePlayer : ModPlayer
    {
        private readonly CutsceneCamera camera = new();

        //拖拽起点缓存（被抓玩家本地）
        private bool dragStarted;
        private Vector2 dragStartPos;

        //震动请求（本地，由 ModifyScreenPosition 消费）
        private static float pendingShakeIntensity;
        private static int pendingShakeDuration;

        /// <summary>由头部演出逻辑请求一次屏幕震动（本地，受屏幕震动设置约束）</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            //保留更强的请求，避免弱震动覆盖强震动
            if (intensity > pendingShakeIntensity) {
                pendingShakeIntensity = intensity;
                pendingShakeDuration = duration;
            }
        }

        public override void ModifyScreenPosition() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            HeadPrimeAI headAI = FindPerformanceHead(out NPC head);
            if (headAI != null && head != null) {
                if (!camera.Active) {
                    camera.Start(head.Center, 0.06f, 1.4f, 0.04f);
                }
                ConfigureCamera(headAI, head);

                if (pendingShakeIntensity > 0.5f) {
                    camera.Shake(Vector2.Zero, pendingShakeIntensity, 0.9f, pendingShakeDuration);
                    pendingShakeIntensity = 0f;
                    pendingShakeDuration = 0;
                }
                camera.Apply();
            }
            else {
                if (camera.Active) {
                    camera.Stop();
                }
                camera.Apply();
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            HeadPrimeAI headAI = FindPerformanceHead(out NPC head);
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
                    float p = (headAI.DeathTimer - HeadPrimeAI.PhaseLungeEnd)
                        / (float)(HeadPrimeAI.PhaseDragEnd - HeadPrimeAI.PhaseLungeEnd);
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
                    if (headAI.DeathTimer == HeadPrimeAI.PhaseRoarEnd) {
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

        private void ConfigureCamera(HeadPrimeAI headAI, NPC head) {
            Player target = headAI.DeathTargetPlayer;
            Vector2 targetCenter = (target != null && target.active) ? target.Center : head.Center;
            Vector2 lift = headAI.DeathLiftPoint;
            camera.LockPlayerControls = true;

            switch (headAI.CurrentDeathPhase) {
                case PrimeDeathPhase.FakeDeath:
                    camera.FocusTarget = head.Center;
                    camera.TargetZoom = 1.35f;
                    camera.PositionLerpSpeed = 0.045f;
                    camera.ZoomLerpSpeed = 0.03f;
                    break;
                case PrimeDeathPhase.Summon:
                    camera.FocusTarget = head.Center;
                    camera.TargetZoom = 1.6f;
                    camera.PositionLerpSpeed = 0.05f;
                    camera.ZoomLerpSpeed = 0.04f;
                    break;
                case PrimeDeathPhase.Lunge:
                    camera.FocusTarget = (head.Center + targetCenter) * 0.5f;
                    camera.TargetZoom = 1.4f;
                    camera.PositionLerpSpeed = 0.06f;
                    camera.ZoomLerpSpeed = 0.04f;
                    break;
                case PrimeDeathPhase.Drag:
                    camera.FocusTarget = (head.Center + lift) * 0.5f;
                    camera.TargetZoom = 1.7f;
                    camera.PositionLerpSpeed = 0.07f;
                    camera.ZoomLerpSpeed = 0.05f;
                    break;
                case PrimeDeathPhase.Roar:
                    camera.FocusTarget = head.Center + new Vector2(0f, HeadPrimeAI.DeathLiftDistance * 0.45f);
                    camera.TargetZoom = 2f;
                    camera.PositionLerpSpeed = 0.09f;
                    camera.ZoomLerpSpeed = 0.06f;
                    break;
                case PrimeDeathPhase.Finale:
                    camera.FocusTarget = head.Center + new Vector2(0f, HeadPrimeAI.DeathLiftDistance * 0.3f);
                    camera.TargetZoom = 1.45f;
                    camera.PositionLerpSpeed = 0.12f;
                    camera.ZoomLerpSpeed = 0.08f;
                    break;
            }
        }

        /// <summary>查询当前正在进行死亡演出的机械骷髅王头部，无则返回 null</summary>
        private static HeadPrimeAI FindPerformanceHead(out NPC head) {
            head = null;
            int h = HeadPrimeAI.ActivePerformanceHead;
            if (h < 0 || h >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[h];
            if (!npc.active || npc.type != NPCID.SkeletronPrime) {
                return null;
            }
            HeadPrimeAI ai = npc.GetOverride<HeadPrimeAI>();
            if (ai == null || !ai.InDeathPerformance) {
                return null;
            }
            head = npc;
            return ai;
        }
    }
}
