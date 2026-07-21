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
    /// 刻心者色板，动脉暗红+心肌粉白+黑；粉白仅瞬时小面积，取色经由此处
    /// </summary>
    internal static class HeartcarverPalette
    {
        /// <summary>动脉暗红</summary>
        public static readonly Color Arterial = new(164, 10, 22);
        /// <summary>干涸深红</summary>
        public static readonly Color ArterialDeep = new(70, 3, 10);
        /// <summary>心肌粉白（瞬时小面积）</summary>
        public static readonly Color Myocard = new(255, 214, 218);
        /// <summary>近黑</summary>
        public static readonly Color Night = new(14, 2, 5);

        /// <summary>暗红族随机（粒子）</summary>
        public static Color Blood(float t) => Color.Lerp(ArterialDeep, Arterial, t);
        /// <summary>暗红→粉白热度</summary>
        public static Color Heat(float t) => Color.Lerp(Arterial, Myocard, t);
    }

    /// <summary>
    /// 刻心者域内资源加载器（shader 注册不动 <see cref="EffectLoader"/>）
    /// </summary>
    internal class HeartcarverAssets
    {
        /// <summary>刺击白热刺线</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverLance { get; set; }
        /// <summary>冲刺血刃条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverRibbon { get; set; }
        /// <summary>心脏 SDF 本体</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverOrgan { get; set; }
        /// <summary>剜心 impact frame</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Asset<Effect> HeartcarverImpact { get; set; }
    }

    /// <summary>
    /// 刻心者玩家态，心跳/冲刺冷却/狂热攻速窗/剜心击信号；心跳仅手持时行进，本地视觉广播节拍
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
        /// <summary>窗口开启白热闪包络</summary>
        public float WindowFlash { get; private set; }
        /// <summary>剜心窗口开启态</summary>
        public bool InCarveWindow => HoldingHeartcarver && BeatPhase >= WindowOpen && BeatPhase < WindowClose;

        //==== 冲刺冷却（原 HeartcarverAlt 隐形弹幕的替代）====
        public int DashCooldown;

        //==== 剜心狂热攻速窗 ====
        public const int FrenzyDuration = 300;
        public const float FrenzyAttackSpeed = 0.25f;
        public int FrenzyTimer;

        //==== 剜心击信号（血刃齐射）====
        /// <summary>信号自增戳，血刃比对该值感知新剜心击</summary>
        public int CarveSignalStamp { get; private set; }
        /// <summary>最近一次剜心击命中的 NPC 索引</summary>
        public int CarveSignalNpc { get; private set; } = -1;

        private bool dashReadyCueArmed;

        public bool HoldingHeartcarver => !Player.dead && Player.HeldItem != null
            && Player.HeldItem.type == ModContent.ItemType<Heartcarver>();

        /// <summary>剜心判定，间隙窗口（含边缘宽容）</summary>
        public bool JudgeCarve() {
            if (!HoldingHeartcarver) {
                return false;
            }
            int phase = BeatPhase;
            return phase >= WindowOpen - JudgeGrace && phase < WindowClose + JudgeGrace;
        }

        /// <summary>剜心击命中时调用（拥有者端），驱动血刃齐射</summary>
        public void NotifyCarveStrike(int npcWhoAmI) {
            CarveSignalStamp++;
            CarveSignalNpc = npcWhoAmI;
        }

        /// <summary>吸收心脏，开狂热攻速窗</summary>
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

                //心跳血光，搏动瞬间点光
                if (BeatEnvelope > 0.05f && !VaultUtils.isServer) {
                    Lighting.AddLight(Player.MountedCenter,
                        HeartcarverPalette.Arterial.ToVector3() * (BeatEnvelope * 0.55f));
                }

                //冷却就绪提示音
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

        /// <summary>lub-dub 双音，鼓组压低拼合</summary>
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

        /// <summary>搏动心口微光环</summary>
        private void SpawnBeatCue(bool isLub) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 chest = Player.MountedCenter + new Vector2(Player.direction * 4f, -2f);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(chest, Player.velocity * 0.4f,
                HeartcarverPalette.Arterial * (isLub ? 0.85f : 0.55f), 1f)
                ?.Configure(0.03f, isLub ? 0.15f : 0.10f, 10);
        }

        /// <summary>窗口开启瞬间血珠上浮</summary>
        private void SpawnWindowCue() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Player.MountedCenter + new Vector2(Player.direction * 12f, 0f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(hand, new Vector2(0f, -1.1f),
                HeartcarverPalette.Myocard * 0.85f, 0.6f)?.Configure(10, 0.06f);
        }
    }

    /// <summary>
    /// 剜心 impact frame，~10 帧全屏后效，限频；纯本地客户端
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
