using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 刻心者专属色彩脚本：动脉暗红 + 心肌粉白高光 + 黑。<br/>
    /// 粉白只允许瞬时小面积高光，不得常驻；禁用青绿/金色/橙色——所有刻心者视觉取色必须经由此处
    /// </summary>
    internal static class HeartcarverPalette
    {
        /// <summary>动脉暗红：主体能量色</summary>
        public static readonly Color Arterial = new(164, 10, 22);
        /// <summary>干涸深红：暗部、拖尾末端</summary>
        public static readonly Color ArterialDeep = new(70, 3, 10);
        /// <summary>心肌粉白：白热高光、剜心击强调（仅瞬时小面积）</summary>
        public static readonly Color Myocard = new(255, 214, 218);
        /// <summary>近黑：轮廓与收势</summary>
        public static readonly Color Night = new(14, 2, 5);

        /// <summary>暗红族随机取色（粒子用）</summary>
        public static Color Blood(float t) => Color.Lerp(ArterialDeep, Arterial, t);
        /// <summary>暗红 → 粉白的热度取色</summary>
        public static Color Heat(float t) => Color.Lerp(Arterial, Myocard, t);
    }

    /// <summary>
    /// 刻心者域内资源加载器（shader 注册不动 <see cref="EffectLoader"/>）
    /// </summary>
    internal class HeartcarverAssets
    {
        /// <summary>刺击针状白热刺线（静态 quad 图元）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverLance { get; set; }
        /// <summary>冲刺血刃条带拖尾（TriangleStrip）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverRibbon { get; set; }
        /// <summary>剜出心脏的程序化本体（SDF 心形 + 嘴）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverOrgan { get; set; }
        /// <summary>剜心瞬间红黑高对比 impact frame（全屏后效）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverImpact { get; set; }
    }

    /// <summary>
    /// 刻心者专属玩家状态：心跳节拍计时 / 冲刺冷却 / 剜心狂热攻速窗 / 剜心击信号。<br/>
    /// 心跳只在手持刻心者时行进；节拍由武器本地视觉广播——刀身辉光随拍脉动、
    /// 窗口开启时刀刃泛白热一瞬、心口微弱血光——不做任何常驻屏幕空间效果
    /// </summary>
    internal class HeartcarverPlayer : ModPlayer
    {
        //==== 心跳节拍（~72bpm = 50 帧一循环）====
        /// <summary>一次心跳循环的帧长</summary>
        public const int BeatCycle = 50;
        /// <summary>第一心音 lub 相位</summary>
        public const int LubPhase = 0;
        /// <summary>第二心音 dub 相位</summary>
        public const int DubPhase = 14;
        /// <summary>剜心窗口开启相位（舒张间隙）</summary>
        public const int WindowOpen = 28;
        /// <summary>剜心窗口关闭相位</summary>
        public const int WindowClose = 46;
        /// <summary>判定用边缘宽容帧数</summary>
        private const int JudgeGrace = 2;

        /// <summary>心跳计时器，仅手持时行进</summary>
        public int BeatTimer { get; private set; }
        /// <summary>当前节拍相位 0..BeatCycle</summary>
        public int BeatPhase => BeatTimer % BeatCycle;
        /// <summary>心音包络（lub 拍满、dub 拍次满、随后指数衰减），供刀身辉光与血刃同步取用</summary>
        public float BeatEnvelope { get; private set; }
        /// <summary>窗口开启瞬间的白热闪包络：开启帧置满后快速衰减，供刀刃"泛白一瞬"取用</summary>
        public float WindowFlash { get; private set; }
        /// <summary>剜心窗口开启态</summary>
        public bool InCarveWindow => HoldingHeartcarver && BeatPhase >= WindowOpen && BeatPhase < WindowClose;

        //==== 冲刺冷却（原 HeartcarverAlt 隐形弹幕的替代）====
        public int DashCooldown;

        //==== 剜心狂热：吸收心脏后的攻速窗 ====
        public const int FrenzyDuration = 300;
        public const float FrenzyAttackSpeed = 0.25f;
        public int FrenzyTimer;

        //==== 剜心击信号：血刃齐射的触发因果 ====
        /// <summary>信号自增戳，血刃比对该值感知新剜心击</summary>
        public int CarveSignalStamp { get; private set; }
        /// <summary>最近一次剜心击命中的 NPC 索引</summary>
        public int CarveSignalNpc { get; private set; } = -1;

        private bool dashReadyCueArmed;

        public bool HoldingHeartcarver => !Player.dead && Player.HeldItem != null
            && Player.HeldItem.type == ModContent.ItemType<Heartcarver>();

        /// <summary>剜心判定：攻击是否落在两次心跳之间的间隙窗口（带边缘宽容）</summary>
        public bool JudgeCarve() {
            if (!HoldingHeartcarver) {
                return false;
            }
            int phase = BeatPhase;
            return phase >= WindowOpen - JudgeGrace && phase < WindowClose + JudgeGrace;
        }

        /// <summary>由持械/冲刺弹幕在剜心击命中时调用（拥有者端）：驱动血刃齐射</summary>
        public void NotifyCarveStrike(int npcWhoAmI) {
            CarveSignalStamp++;
            CarveSignalNpc = npcWhoAmI;
        }

        /// <summary>心脏被刀吸收：开启剜心狂热攻速窗（"刀听到了剜心声"）</summary>
        public void NotifyAbsorb() => FrenzyTimer = FrenzyDuration;

        public override void PostUpdateEquips() {
            if (FrenzyTimer > 0) {
                Player.GetAttackSpeed(DamageClass.Generic) += FrenzyAttackSpeed;
            }
        }

        public override void PostUpdate() {
            if (DashCooldown > 0) {
                DashCooldown--;
            }
            if (FrenzyTimer > 0) {
                FrenzyTimer--;
            }

            if (HoldingHeartcarver) {
                BeatTimer++;
                int phase = BeatPhase;

                //拔刀第一帧即起搏，不等满一个循环
                if (phase == LubPhase || BeatTimer == 1) {
                    BeatEnvelope = 1f;
                    PlayHeartTone(true);
                    SpawnBeatCue(true);
                }
                else if (phase == DubPhase) {
                    BeatEnvelope = MathHelper.Max(BeatEnvelope, 0.62f);
                    PlayHeartTone(false);
                    SpawnBeatCue(false);
                }
                else if (phase == WindowOpen) {
                    WindowFlash = 1f;
                    SpawnWindowCue();
                }

                //心跳血光：只在搏动瞬间随包络泛起的点光，拍间归零，非常驻
                if (BeatEnvelope > 0.05f && !VaultUtils.isServer) {
                    Lighting.AddLight(Player.MountedCenter,
                        HeartcarverPalette.Arterial.ToVector3() * (BeatEnvelope * 0.55f));
                }

                //冷却就绪提示：一次性轻响，广播"可以再冲了"
                if (DashCooldown <= 0 && dashReadyCueArmed) {
                    dashReadyCueArmed = false;
                    if (!VaultUtils.isServer && Player.whoAmI == Main.myPlayer) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.35f, Volume = 0.3f }, Player.Center);
                    }
                }
                if (DashCooldown > 10) {
                    dashReadyCueArmed = true;
                }
            }
            else {
                BeatTimer = 0;
            }

            BeatEnvelope *= 0.88f;
            WindowFlash *= 0.80f;
        }

        /// <summary>lub-dub 双音：原版鼓组压低音高分层拼合，压低存在感，随狂热微升</summary>
        private void PlayHeartTone(bool isLub) {
            if (VaultUtils.isServer) {
                return;
            }
            float excite = FrenzyTimer > 0 ? 0.08f : 0f;
            if (isLub) {
                SoundEngine.PlaySound(SoundID.DrumKick with { Pitch = -0.85f + excite, Volume = 0.32f }, Player.Center);
                SoundEngine.PlaySound(SoundID.DrumFloorTom with { Pitch = -0.9f + excite, Volume = 0.13f }, Player.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.DrumKick with { Pitch = -0.55f + excite, Volume = 0.22f }, Player.Center);
            }
        }

        /// <summary>搏动瞬间的心口微光环：小、暗红、即生即灭——替代已移除的屏幕红晕承担节拍广播</summary>
        private void SpawnBeatCue(bool isLub) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 chest = Player.MountedCenter + new Vector2(Player.direction * 4f, -2f);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(chest, Player.velocity * 0.4f,
                HeartcarverPalette.Arterial * (isLub ? 0.85f : 0.55f), 1f)
                ?.Configure(0.03f, isLub ? 0.15f : 0.10f, 10);
        }

        /// <summary>窗口开启瞬间：持手处一点粉白冷光，一瞬即灭</summary>
        private void SpawnWindowCue() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Player.MountedCenter + new Vector2(Player.direction * 12f, 0f);
            PRTLoader.NewParticle<PRT_Line>(hand, new Vector2(0f, -0.7f),
                HeartcarverPalette.Myocard * 0.8f, 0.85f)?.Configure(false, 8);
        }
    }

    /// <summary>
    /// 剜心瞬间红黑高对比 impact frame：~10 帧全屏后效，限频防贬值。<br/>
    /// 纯本地客户端演出，静态状态为每客户端一份的屏幕表现，非玩家逻辑态
    /// </summary>
    internal sealed class HeartcarverImpactRender : RenderHandle
    {
        private const int ImpactLife = 10;
        /// <summary>两次 impact frame 至少间隔 4 秒</summary>
        private const uint MinInterval = 240;

        private static int impactAge = ImpactLife;
        private static float impactStrength;
        private static uint lastTriggerTick;

        public override float Weight => 1.09f;

        /// <summary>触发一次剜心 impact frame（客户端调用；未过限频间隔时静默忽略）</summary>
        public static void Trigger(float strength) {
            if (Main.dedServ) {
                return;
            }
            if (Main.GameUpdateCount - lastTriggerTick < MinInterval) {
                return;
            }
            lastTriggerTick = Main.GameUpdateCount;
            impactAge = 0;
            impactStrength = strength;
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.gameMenu || impactAge >= ImpactLife) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                impactAge = ImpactLife;
                return;
            }
            Effect shader = HeartcarverAssets.HeartcarverImpact?.Value;
            if (shader == null) {
                impactAge = ImpactLife;
                return;
            }

            shader.Parameters["uIntensity"]?.SetValue(impactStrength);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(impactAge / (float)ImpactLife, 0f, 1f));

            //拷屏到 screenSwap 再带 shader 写回 screenTarget（ping-pong）
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();

            impactAge++;
        }
    }
}
