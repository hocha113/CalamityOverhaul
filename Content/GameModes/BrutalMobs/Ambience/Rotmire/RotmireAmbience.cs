using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire
{
    /// <summary>
    /// 「腐澜」常态氛围强度控制器：残酷模式下本地玩家身处腐化之地时缓升缓降的在场强度。
    /// 纯本地演出量（视觉与听觉共用），联机各端各自计算，不进网络
    /// </summary>
    internal static class RotmireAmbience
    {
        /// <summary>本地在场强度 0~1（进出群系约 1~2s 淡入淡出，不硬切）</summary>
        public static float Presence { get; private set; }

        /// <summary>地下深度变奏 0~1（地下深谷瘴气更沉、腹鸣更响）</summary>
        public static float DepthGrade { get; private set; }

        /// <summary>Boss 在场时的氛围减弱系数（纯视觉保留但收敛，让位战斗可读性）</summary>
        public static float BossDim => CWRWorld.HasBoss ? 0.55f : 1f;

        /// <summary>仍需在场（含渐出尾巴）</summary>
        public static bool Visible => Presence > 0.01f;

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Presence = 0f;
                DepthGrade = 0f;
                return;
            }
            Player player = Main.LocalPlayer;
            bool inZone = GameModeSystem.BrutalActive && player.active && !player.dead && player.ZoneCorrupt;
            float target = inZone ? 1f : 0f;
            Presence = Math.Abs(target - Presence) < 0.008f
                ? target : MathHelper.Lerp(Presence, target, 0.035f);

            float depthTarget = player.Center.Y > Main.worldSurface * 16.0 ? 1f : 0f;
            DepthGrade = MathHelper.Lerp(DepthGrade, depthTarget, 0.03f);
        }

        internal static void Reset() {
            Presence = 0f;
            DepthGrade = 0f;
        }
    }

    /// <summary>
    /// 「腐澜」氛围驱动：上升腐孢粒子、低频腹鸣与低语环境声、紫瘴压光；
    /// 附带第四特色「吞噬回响」：久待时远处传来世界吞噬者穿行的闷响与极轻屏震（纯氛围压迫，无判定）。
    /// 全部只在客户端执行，声音只用原版音源（循环槽管理镜像 OldNetAmbience）
    /// </summary>
    internal class RotmireAmbienceSystem : ModSystem
    {
        //==== 环境声循环槽 ====
        private static SlotId droneSlot;
        private static SlotId breathSlot;
        /// <summary>低频腹鸣：群系深处传来的消化般低鸣</summary>
        private static readonly SoundStyle DroneStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>腐息风噪：衬在腹鸣下的干哑气流声</summary>
        private static readonly SoundStyle BreathStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 低语一次性音 ====
        private static int whisperTimer;
        private static bool whisperAlt;

        //==== 吞噬回响（本地玩家的停留演出，纯客户端量）====
        /// <summary>首次回响前需持续停留的帧数（约 30s 的"久待"门）</summary>
        private const int EchoStayGate = 1800;
        private static int stayTicks;
        private static int echoTimer;
        /// <summary>回响第二声（穿行远去的余音）的延迟计数</summary>
        private static int echoFollow;
        private static Vector2 echoPos;

        //==== 腐孢粒子 ====
        private static int sporeTimer;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            RotmireAmbience.Update();
            float presence = RotmireAmbience.Presence;
            if (presence <= 0.01f) {
                stayTicks = 0;
                return;
            }
            UpdateLoops();
            UpdateWhisper(presence);
            UpdateSpores(presence);
            UpdateEcho(presence);
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(droneSlot, out _)) {
                droneSlot = SoundEngine.PlaySound(DroneStyle, null, UpdateDrone);
            }
            if (!SoundEngine.TryGetActiveSound(breathSlot, out _)) {
                breathSlot = SoundEngine.PlaySound(BreathStyle, null, UpdateBreath);
            }
        }

        //低频腹鸣：地下更沉更响
        private static bool UpdateDrone(ActiveSound sound) {
            float presence = RotmireAmbience.Presence;
            if (presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = presence * (0.28f + 0.16f * RotmireAmbience.DepthGrade) * RotmireAmbience.BossDim;
            sound.Pitch = -0.72f;
            sound.Position = null;
            return true;
        }

        //腐息风噪：表层极轻，地下随深度变奏微涨
        private static bool UpdateBreath(ActiveSound sound) {
            float presence = RotmireAmbience.Presence;
            if (presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = presence * (0.07f + 0.08f * RotmireAmbience.DepthGrade) * RotmireAmbience.BossDim;
            sound.Pitch = -0.35f;
            sound.Position = null;
            return true;
        }

        //低语：远处飘来的呜咽与腐鸣，方向感由声源位置自然给出
        private static void UpdateWhisper(float presence) {
            if (presence < 0.55f) {
                return;
            }
            if (--whisperTimer > 0) {
                return;
            }
            whisperTimer = 300 + Main.rand.Next(420);
            Vector2 pos = Main.LocalPlayer.Center
                + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(520f, 880f);
            whisperAlt = !whisperAlt;
            SoundStyle style = whisperAlt
                ? SoundID.Zombie3 with { Volume = 0.24f, Pitch = -0.62f, MaxInstances = 2 }
                : SoundID.Zombie103 with { Volume = 0.20f, Pitch = -0.48f, MaxInstances = 2 };
            SoundEngine.PlaySound(style, pos);
            //地下深处偶尔追一记远处孢囊湿响。禁 Zombie104，那是死光起手
            if (RotmireAmbience.DepthGrade > 0.6f && Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.22f, Pitch = -0.35f, MaxInstances = 2 }, pos);
            }
        }

        //上升腐孢：常态预算约 20 粒/s，随强度收缩；配色引用邪地风味表保持家族一致
        private static void UpdateSpores(float presence) {
            if (Main.gamePaused || presence < 0.2f) {
                return;
            }
            if (--sporeTimer > 0) {
                return;
            }
            sporeTimer = presence > 0.7f ? 3 : 6;
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                Main.rand.NextFloat(Main.screenHeight * 0.15f, Main.screenHeight + 40f));
            Vector2 vel = new(Main.windSpeedCurrent * 1.4f + Main.rand.NextFloat(-0.2f, 0.2f),
                -Main.rand.NextFloat(0.5f, 1.3f));
            Dust dust = Dust.NewDustPerfect(pos,
                EvilBiome.EvilBiomeFX.DustFor(EvilBiome.EvilBiomeFX.FlavorCorrupt), vel,
                150, default, Main.rand.NextFloat(0.7f, 1.15f));
            dust.noGravity = true;
            dust.fadeIn = 0.9f;
            //稀疏的暗影焰腐屑压层次
            if (Main.rand.NextBool(7)) {
                Dust dark = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel * 0.8f, 170, default, 0.8f);
                dark.noGravity = true;
            }
        }

        //「吞噬回响」：久待后远处传来蠕虫穿行的闷响与极轻屏震；Boss 战与低强度期不响
        private static void UpdateEcho(float presence) {
            if (echoFollow > 0 && --echoFollow == 0) {
                //穿行远去的余音
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.34f, Pitch = -0.78f, MaxInstances = 2 },
                    echoPos + new Vector2(0f, 140f));
                Main.LocalPlayer.CWR().GetScreenShake(0.9f);
            }
            if (presence < 0.7f) {
                stayTicks = 0;
                return;
            }
            if (CWRWorld.HasBoss) {
                return;
            }
            if (stayTicks < EchoStayGate) {
                stayTicks++;
                return;
            }
            if (--echoTimer > 0) {
                return;
            }
            echoTimer = 1500 + Main.rand.Next(1200);
            echoPos = Main.LocalPlayer.Center
                + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(900f, 1300f);
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.24f, Pitch = -0.9f, MaxInstances = 2 }, echoPos);
            SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.5f, Pitch = -0.82f, MaxInstances = 2 }, echoPos);
            Main.LocalPlayer.CWR().GetScreenShake(1.8f);
            echoFollow = 26;
        }

        //紫瘴压光：氛围级轻染，远弱于领域类接管（服务端 Presence 恒 0，天然无效）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float veil = RotmireAmbience.Presence;
            if (veil <= 0.01f) {
                return;
            }
            veil *= RotmireAmbience.BossDim;
            Color tile = new(66, 52, 84);
            Color bg = new(46, 34, 66);
            tileColor = Color.Lerp(tileColor, tile, veil * 0.22f);
            backgroundColor = Color.Lerp(backgroundColor, bg, veil * 0.30f);
        }

        public override void ClearWorld() {
            RotmireAmbience.Reset();
            whisperTimer = 0;
            stayTicks = 0;
            echoTimer = 0;
            echoFollow = 0;
            sporeTimer = 0;
        }
    }
}
