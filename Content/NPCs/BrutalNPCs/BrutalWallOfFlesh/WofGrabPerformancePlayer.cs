using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh
{
    /// <summary>
    /// 舌卷回吞受害者侧：只在被抓玩家自己的客户端生效。
    /// 借用原版TheTongue的运动包(输入禁用/钩爪清除/无碰撞位移)，
    /// 每帧覆写速度实现高速回卷与口内保持；全程免疫杂伤(节拍伤害穿透结算)；
    /// 运镜启停由观察墙的同步状态驱动，绝不接管旁观者
    /// </summary>
    internal class WofGrabPerformancePlayer : ModPlayer
    {
        /// <summary>回卷绷紧顿帧长度</summary>
        private const int TautFrames = 8;

        /// <summary>本帧运动接管中(异常断投的残余清理判据)</summary>
        private bool motionWasActive;
        /// <summary>抓取开场清理(钩爪/坐骑)已执行</summary>
        private bool startHandled;
        /// <summary>正常吐出已施加击飞</summary>
        private bool spitApplied;

        /// <summary>投技运镜期间的震屏；运镜未在播时退化为普通相机冲击</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is WofGrabCutscene) {
                CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
            }
            else if (Main.LocalPlayer.Alives()) {
                Rendering.WofMotionFX.CameraPunch(Main.LocalPlayer.Center, intensity * 0.7f, duration, "WofGrabSelf");
            }
        }

        /// <summary>本地玩家正被这面墙抓住，输出本端演出时钟</summary>
        private bool TryGetMyGrab(out NPC wall, out WofTongueGrabState state) {
            wall = null;
            state = null;
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return false;
            }
            if (!WofTongueGrabState.TryGetActiveGrab(out wall, out state)) {
                return false;
            }
            return WofTongueGrabState.VictimIndex(wall) == Player.whoAmI;
        }

        /// <summary>处于位移接管窗口(回卷起点到吐出帧前)</summary>
        private bool HoldActive(out NPC wall, out WofTongueGrabState state) {
            if (!TryGetMyGrab(out wall, out state)) {
                return false;
            }
            int t = state.GrabTimer;
            return t >= WofTongueGrabState.ReelStartTick && t < WofTongueGrabState.SpitTick;
        }

        /// <summary>处于吐出窗口(窗口而非等值判定：客户端时钟跨刻快进也不丢击飞)</summary>
        private bool SpitPending(out NPC wall) {
            if (!TryGetMyGrab(out wall, out WofTongueGrabState state)) {
                return false;
            }
            int t = state.GrabTimer;
            return t >= WofTongueGrabState.SpitTick && t < WofTongueGrabState.RecoverStartTick;
        }

        /// <summary>借用原版TheTongue运动包：输入禁用+钩爪清除+无碰撞位移都由原版代劳</summary>
        public override void PostUpdateBuffs() {
            if (HoldActive(out _, out _)) {
                Player.tongued = true;
            }
        }

        /// <summary>取消原版舌头的专家吸血(投技伤害全部由节拍表结算，保底不处死)</summary>
        public override void UpdateBadLifeRegen() {
            if (Main.expertMode && Player.tongued && HoldActive(out _, out _)) {
                Player.lifeRegen += 100;
            }
        }

        /// <summary>每帧覆写速度：绷紧顿帧→高速回卷→口内咬合保持→吐出击飞</summary>
        public override void PreUpdateMovement() {
            //吐出窗口：向推进方向掷出一次，给足无敌帧后交还控制(碰撞恢复正常)
            if (SpitPending(out NPC spitWall)) {
                if (!spitApplied) {
                    spitApplied = true;
                    Player.velocity = new Vector2(spitWall.direction * 17f, -8.5f);
                    Player.SetImmuneTimeForAllTypes(90);
                    Player.immuneNoBlink = false;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                }
                return;
            }

            if (!HoldActive(out NPC wall, out WofTongueGrabState state)) {
                return;
            }
            int timer = state.GrabTimer;

            //开场清理：钩爪/坐骑/合成动作
            if (!startHandled) {
                startHandled = true;
                Player.RemoveAllGrapplingHooks();
                if (Player.mount != null && Player.mount.Active) {
                    Player.mount.Dismount(Player);
                }
                Player.StopVanityActions();
            }

            //全程免疫杂伤：投技期间只有节拍表能造成伤害(节拍走DD2槽穿透)
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
            Player.lavaImmune = true;

            Vector2 hold = WofTongueGrabState.MouthHold(wall);

            int reelLocal = timer - WofTongueGrabState.ReelStartTick;
            Vector2 target;
            float maxStep;

            if (timer < WofTongueGrabState.ChewStartTick) {
                //回卷段
                if (reelLocal < TautFrames) {
                    //绷紧顿帧：几乎不动，只有细微牵引颤动
                    target = Player.Center + (hold - Player.Center).SafeNormalize(Vector2.Zero) * 0.6f;
                    maxStep = 0.8f;
                }
                else {
                    //高速回卷：起步即高速，末段不减(撞进嘴里才停)
                    float ramp = MathHelper.Clamp((reelLocal - TautFrames) / 7f, 0f, 1f);
                    target = hold;
                    maxStep = MathHelper.Lerp(7f, WofDirector.GrabReelSpeed, ramp * ramp);
                }
            }
            else {
                //咀嚼保持：钉在口内，咬合节拍时向喉内挤压
                int chewLocal = timer - WofTongueGrabState.ChewStartTick;
                float squeeze = (float)Math.Sin(chewLocal * 0.55f) * 3f;
                target = hold + new Vector2(wall.direction * squeeze,
                    (float)Math.Sin(chewLocal * 0.9f) * 4f);
                maxStep = 30f;
            }

            Vector2 delta = target - Player.Center;
            Player.velocity = delta.Length() <= maxStep
                ? delta
                : delta.SafeNormalize(Vector2.Zero) * maxStep;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }

            //运镜窗口：抓取确认(回卷)起播，吐出回吞后停(平滑归位)
            bool cinematicActive = false;
            NPC wall = null;
            if (TryGetMyGrab(out wall, out WofTongueGrabState state)) {
                int t = state.GrabTimer;
                cinematicActive = t >= WofTongueGrabState.ReelStartTick && t < WofTongueGrabState.RecoverStartTick;
            }
            bool playing = CutsceneDirector.CurrentClip is WofGrabCutscene;
            if (cinematicActive && !playing) {
                CutsceneDirector.Play<WofGrabCutscene, NPC>(wall, restartSameClip: false);
            }
            else if (!cinematicActive && playing) {
                CutsceneDirector.Stop();
            }

            //运动接管收尾：正常吐出走击飞；异常断投(墙死亡/断线拉回等)清残余速度并给缓冲无敌
            bool engagedNow = HoldActive(out _, out _) || SpitPending(out _);
            if (motionWasActive && !engagedNow && !spitApplied) {
                Player.velocity *= 0.35f;
                Player.SetImmuneTimeForAllTypes(60);
                Player.immuneNoBlink = false;
            }
            motionWasActive = engagedNow;
            if (!engagedNow && !cinematicActive) {
                startHandled = false;
                spitApplied = false;
            }
        }

        /// <summary>死亡时演出立即散场(死亡玩家不再走PostUpdate的主路径不可靠，双保险)</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            if (CutsceneDirector.CurrentClip is WofGrabCutscene) {
                CutsceneDirector.Stop();
            }
            motionWasActive = false;
            startHandled = false;
            spitApplied = false;
        }
    }
}
