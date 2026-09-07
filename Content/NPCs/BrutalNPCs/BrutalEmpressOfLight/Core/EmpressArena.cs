using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>
    /// 竞技场（昼）：跟随她的软边界圆。服务端算圆心与半径写入 NPCOverride.ai[0..2] 随 NPC 同步，
    /// 玩家端只读。越界被拉回，拉满 60f 即被捕：定身、扣血、上灼痕、被拖向她
    /// </summary>
    internal static class EmpressArena
    {
        /// <summary>NPCOverride.ai 槽位</summary>
        internal const int SlotRadius = 0;
        internal const int SlotCenterX = 1;
        internal const int SlotCenterY = 2;
        /// <summary>半径外多远拉力封顶</summary>
        internal const float PullRange = 250f;
        /// <summary>被捕进度满值（帧）</summary>
        internal const int CaptureFrames = 60;
        /// <summary>被捕后释放冷却（帧）</summary>
        internal const int ReleaseCooldown = 300;

        /// <summary>服务端每帧：圆心以 cbrt(d/r) 插值速度追随她，半径向请求值收敛（快扩慢缩）</summary>
        internal static void ServerUpdate(EmpressOfLightAI ai, EmpressStateContext ctx) {
            float request = ctx.ArenaRadiusRequest;
            float radius = ai.ai[SlotRadius];
            Vector2 center = new(ai.ai[SlotCenterX], ai.ai[SlotCenterY]);
            NPC npc = ctx.Npc;

            if (request <= 0f) {
                //关闭：半径快速收到 0，圆心跟死她
                ai.ai[SlotRadius] = Math.Max(0f, radius - 40f);
                ai.ai[SlotCenterX] = npc.Center.X;
                ai.ai[SlotCenterY] = npc.Center.Y;
                return;
            }

            if (radius <= 0f) {
                center = npc.Center;
                radius = request * 0.5f;
            }

            float dist = Vector2.Distance(center, npc.Center);
            if (dist > 0.001f) {
                float amount = MathF.Cbrt(Math.Min(dist / Math.Max(radius, 1f), 1f));
                center += center.DirectionTo(npc.Center) * MathHelper.Lerp(0f, ctx.ArenaFollowSpeed, amount);
            }
            radius += MathHelper.Clamp(request - radius, -7f, 12f);

            ai.ai[SlotRadius] = radius;
            ai.ai[SlotCenterX] = center.X;
            ai.ai[SlotCenterY] = center.Y;
        }

        /// <summary>各端：找到在场且开着竞技场的女皇（读同步数据）</summary>
        internal static bool TryGet(out NPC boss, out Vector2 center, out float radius) {
            boss = null;
            center = Vector2.Zero;
            radius = 0f;
            if (!CWRWorld.HasBoss) {
                return false;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.HallowBoss) {
                    continue;
                }
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(EmpressOfLightAI), out NPCOverride raw)
                    || raw is not EmpressOfLightAI ai) {
                    continue;
                }
                if (ai.ai[SlotRadius] <= 1f) {
                    continue;
                }
                boss = npc;
                center = new Vector2(ai.ai[SlotCenterX], ai.ai[SlotCenterY]);
                radius = ai.ai[SlotRadius];
                return true;
            }
            return false;
        }

        /// <summary>边界环（实体批内调用）：昼金白，越界的人越多环越亮</summary>
        internal static void DrawBoundary(SpriteBatch sb, Vector2 center, float radius, float presence) {
            if (presence <= 0.02f || radius < 100f) {
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            float breathe = 1f + 0.03f * MathF.Sin(t * 1.7f);
            ShockRingDraw.Draw(sb, center, radius * breathe, 26f,
                new Color(255, 246, 224), new Color(255, 200, 120), new Color(140, 70, 20),
                0.55f * presence, 30f, 1f, 0.06f, 3.7f);
        }
    }

    /// <summary>
    /// 竞技场玩家端：拉力、捕获进度、释放冷却全在受影响者本端（位置客户端权威），
    /// 捕获扣血经原版受伤包同步
    /// </summary>
    internal class EmpressArenaPlayer : ModPlayer
    {
        /// <summary>捕获进度 0~60</summary>
        public float CaptureProgress;
        /// <summary>释放冷却</summary>
        public int Cooldown;
        /// <summary>本帧越界（HUD/屏幕暗化读）</summary>
        public float OutsideDepth;
        public bool Captured => CaptureProgress >= EmpressArena.CaptureFrames;

        public override void PreUpdateMovement() {
            if (Player.whoAmI != Main.myPlayer || Player.dead || Player.ghost) {
                CaptureProgress = 0f;
                OutsideDepth = 0f;
                return;
            }
            if (!EmpressArena.TryGet(out NPC boss, out Vector2 center, out float radius)) {
                Release();
                return;
            }

            float dist = Vector2.Distance(center, Player.Center);
            float over = MathHelper.Clamp((dist - radius) / EmpressArena.PullRange, 0f, 5f);
            OutsideDepth = over / 5f;

            bool wasCaptured = Captured;
            if (over > 0f) {
                CaptureProgress = Math.Min(CaptureProgress + 1f, EmpressArena.CaptureFrames);
                Cooldown = EmpressArena.ReleaseCooldown;
            }
            else if (Cooldown > 0) {
                Cooldown--;
            }
            else {
                CaptureProgress = Math.Max(CaptureProgress - 0.25f, 0f);
            }

            //被捕：跑速翅膀归零、被拖向她；捕获瞬间扣三成并上灼痕
            if (Captured) {
                over += 2.5f;
                Player.velocity *= 0.85f;
                Player.wingTime = 0f;
                if (!wasCaptured) {
                    OnCaptured(boss);
                }
                if (Player.Distance(boss.Center) < 220f) {
                    //拖到身边即松手，进入冷却释放期
                    CaptureProgress = EmpressArena.CaptureFrames - 1f;
                    Cooldown = EmpressArena.ReleaseCooldown / 2;
                }
            }

            if (over > 0f) {
                Vector2 pull = Player.DirectionTo(boss.Center) * over;
                Player.velocity += pull;
                if (Player.wings != -1 && Player.wingTime < Player.wingTimeMax) {
                    Player.wingTime = Math.Min(Player.wingTime + 2f, Player.wingTimeMax);
                }
                if (CaptureProgress > 45f) {
                    EmpressMotion.Shake(Player.Center, (CaptureProgress / 60f - 0.75f) * 6f, 6);
                }
                EmpressScreenFX.DeclareArenaPull(OutsideDepth);
            }
        }

        private void OnCaptured(NPC boss) {
            int dmg = (int)(Player.statLifeMax2 * 0.3f);
            PlayerDeathReason reason = PlayerDeathReason.ByNPC(boss.whoAmI);
            //Hurt 会经 EmpressScorchPlayer.OnHurt 走一遍命中链，这里只补最高档灼痕与被捕闪光
            Player.Hurt(reason, dmg, Player.direction, false, false, -1, false, 0f, 1f, 0f);
            if (NPC.ShouldEmpressBeEnraged()) {
                Player.AddBuff(ModContent.BuffType<EmpressScorchBuff>(), EmpressScorchPlayer.ScorchTicks(EmpressScorchTier.Beam));
            }
            EmpressScreenFX.PushArenaFlash();
        }

        private void Release() {
            OutsideDepth = 0f;
            if (Cooldown > 0) {
                Cooldown--;
            }
            else {
                CaptureProgress = Math.Max(CaptureProgress - 0.5f, 0f);
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (Player.whoAmI != Main.myPlayer || !Captured) {
                return;
            }
            Player.maxRunSpeed = 0f;
            Player.runAcceleration = 0f;
            Player.wingAccRunSpeed = 0f;
        }
    }
}
