using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 入雨演出的相位状态机：镜面浮现→驻留→180°翻转→落定，纯本地演出量。<br/>
    /// 节拍常量是唯一时钟，<see cref="OniRainWorldCutscene"/> 与 <see cref="OniRainWorldRender"/> 都从这里取数；
    /// 运镜失败不致命，演出照走。
    /// </summary>
    internal static class OniRainWorldTransition
    {
        //节拍表（60fps）：压镜0-40 → 镜面浮现40-150 → 驻留150-190 → 翻转190-280 → 落定280-312
        public const int ApproachEnd = 40;
        public const int RevealEnd = 150;
        public const int DwellEnd = 190;
        public const int RollEnd = 280;
        public const int TotalFrames = 312;
        /// <summary>入雨结算帧，翻转过半（θ≈90°）白闪处，世界状态在此切换</summary>
        public const int CommitFrame = 235;

        private const int GlimpseStart = 156;
        private const int GlimpseFrames = 20;

        public static bool Active { get; private set; }
        public static int Timer { get; private set; }

        /// <summary>缝线焦点：伞的地面锚点略上抬，运镜聚焦于此使缝线落屏幕中线</summary>
        public static Vector2 FocusWorld { get; private set; }
        /// <summary>伞的世界坐标，镜面波前的扩散圆心</summary>
        public static Vector2 UmbrellaWorld { get; private set; }

        //渲染包络，Update 逐帧推进
        public static float Reveal { get; private set; }
        public static float RollAngle { get; private set; }
        public static float Swallow { get; private set; }
        public static float Grade { get; private set; }
        public static float Glimpse { get; private set; }
        public static float Flash { get; private set; }
        public static float SeamGlow { get; private set; }

        /// <summary>渲染合成是否需要介入</summary>
        public static bool RenderActive => Active
            && (Reveal > 0.0005f || RollAngle > 0.0005f || Flash > 0.0005f);

        /// <summary>结算前的预压顶：镜面浮现期给真实世界一点阴叠，结算后由世界状态接管</summary>
        public static float AmbientPreGloom => Active && Timer < CommitFrame ? Reveal * 0.25f : 0f;

        /// <summary>开始入雨演出，仅本地玩家生效；重复调用无效</summary>
        public static void Begin(Player player, Vector2 umbrellaGround) {
            if (Active || Main.dedServ || player == null
                || player.whoAmI != Main.myPlayer || !player.Alives()) {
                return;
            }

            Active = true;
            Timer = 0;
            UmbrellaWorld = umbrellaGround;
            FocusWorld = umbrellaGround + new Vector2(0f, -8f);
            ZeroEnvelopes();
        }

        internal static void Update() {
            if (!Active) {
                return;
            }
            if (Main.gameMenu) {
                HardReset();
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.Alives()) {
                Abort();
                return;
            }

            //演出全程短无敌
            player.GivePlayerImmuneState(4);

            Timer++;
            AdvanceEnvelopes();
            PlayBeats(player);

            if (Timer == CommitFrame) {
                Commit(player);
            }
            if (Timer >= TotalFrames) {
                Finish();
            }
        }

        private static void AdvanceEnvelopes() {
            int t = Timer;

            //镜面从伞下扩散
            Reveal = t <= ApproachEnd ? 0f
                : Smooth01((t - ApproachEnd) / (float)(RevealEnd - ApproachEnd));

            //翻转角 0→π，先慢后快再慢
            RollAngle = t <= DwellEnd ? 0f
                : MathHelper.Pi * CubicInOut((t - DwellEnd) / (float)(RollEnd - DwellEnd));

            //结算后镜面向上吞没旧世界半屏，θ=π 时全屏皆镜、画面自动对上真实渲染
            Swallow = t < CommitFrame ? 0f : Smooth01((t - CommitFrame) / 37f);

            //镜像侧调色增益：结算后让位给真实世界的鬼雨氛围，避免双重压暗
            Grade = t < CommitFrame ? 1f : 1f - Smooth01((t - CommitFrame) / 41f);

            //镜中异样一闪
            Glimpse = t >= GlimpseStart && t < GlimpseStart + GlimpseFrames
                ? MathF.Sin(MathHelper.Pi * (t - GlimpseStart) / GlimpseFrames) : 0f;

            //结算白闪：短促起势，长尾退潮
            if (t >= CommitFrame - 2 && t < CommitFrame) {
                Flash = (t - (CommitFrame - 2)) / 2f;
            }
            else if (t >= CommitFrame) {
                Flash = MathHelper.Clamp(1f - (t - CommitFrame) / 18f, 0f, 1f);
            }
            else {
                Flash = 0f;
            }

            //缝线水膜辉光，落定段消隐
            float settleFade = t <= RollEnd ? 1f
                : 1f - Smooth01((t - RollEnd) / (float)(TotalFrames - RollEnd));
            SeamGlow = Math.Min(Reveal, settleFade);
        }

        private static void PlayBeats(Player player) {
            switch (Timer) {
                case 6:
                    //远处一声闷雷，预兆
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.85f, Volume = 0.38f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case ApproachEnd:
                    //镜面起：水膜漫开
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.6f, Volume = 0.85f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 104:
                    //第二声更沉的闷雷
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.95f, Volume = 0.3f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case GlimpseStart + 4:
                    //镜中异样：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                        Pitch = -0.9f, Volume = 0.42f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case DwellEnd:
                    //翻转起势
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                        Pitch = -0.7f, Volume = 0.5f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case RollEnd:
                    //落定一记压低的闷锣，雨声由常驻雨幕接管
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.9f, Volume = 0.4f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case CommitFrame + 26:
                    OniRainWorldState.ShowEnterText(player);
                    break;
            }
        }

        /// <summary>结算：白闪掩护下切入鬼雨世界状态，真实渲染从此带鬼雨调色</summary>
        private static void Commit(Player player) {
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.6f, Volume = 0.85f, MaxInstances = 3,
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.7f, Volume = 0.6f, MaxInstances = 3,
            }, player.Center);
            player.CWR()?.GetScreenShake(9f);
            OniRainWorldState.EnterLocal(player);
        }

        private static void Finish() {
            //θ=π、全屏皆镜、调色归零时输出等于输入，直接停用无跳变
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>玩家中途失效：结算前取消不入雨，结算后直接收尾（世界已切换）</summary>
        private static void Abort() {
            bool committed = Timer >= CommitFrame;
            Active = false;
            ZeroEnvelopes();
            if (!committed) {
                StopOwnCutscene();
            }
        }

        /// <summary>世界卸载/回主菜单的硬复位，不回滚已结算的世界状态</summary>
        internal static void HardReset() {
            Active = false;
            Timer = 0;
            ZeroEnvelopes();
        }

        private static void ZeroEnvelopes() {
            Reveal = RollAngle = Swallow = Glimpse = Flash = SeamGlow = 0f;
            Grade = 1f;
        }

        private static void StopOwnCutscene() {
            //if (CutsceneDirector.CurrentClip is OniRainWorldCutscene) {
            //    CutsceneDirector.Stop();
            //}
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float CubicInOut(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}
