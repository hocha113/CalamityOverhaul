using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Overlay
{
    /// <summary>
    /// 深潜演出的相位状态机（雨世界内再撑一层）：骤雨起势→湿墨冲刷合幕→满幕遮蔽下结算→
    /// 排墨揭深层→落定，纯本地演出量。<br/>
    /// 与入雨的镜面翻转不同语法：世界是画在纸上的，雨把它冲掉，颜色先被冲得向下流淌，
    /// 雨帘随后合拢成整幅水幕，幕后切层，幕再向下排尽。节拍常量是唯一时钟，
    /// <see cref="OniRainDescentCutscene"/> 与 <see cref="OniRainDescentRender"/> 都从这里取数；
    /// 运镜失败不致命，演出照走。
    /// </summary>
    internal static class OniRainDescentTransition
    {
        //节拍表（60fps）：骤雨起势0-46 → 冲刷合幕46-118 → 遮蔽结算126 → 排墨136-186 → 落定204
        public const int SurgeEnd = 46;
        public const int CoverEnd = 118;
        /// <summary>深潜结算帧：满幕遮蔽下切换深度，跳变全被水幕盖住</summary>
        public const int CommitFrame = 126;
        public const int DrainStart = 136;
        public const int DrainEnd = 186;
        public const int TotalFrames = 204;

        public static bool Active { get; private set; }
        public static int Timer { get; private set; }

        /// <summary>运镜焦点：伞盖上方，深潜的门就是这把伞</summary>
        public static Vector2 FocusWorld { get; private set; }
        /// <summary>伞的世界坐标，排墨撕口从伞顶先裂开</summary>
        public static Vector2 UmbrellaWorld { get; private set; }

        //渲染包络，Update 逐帧推进
        /// <summary>骤雨增压：加进常驻雨帘密度的额外量，结算后由深层密度接管</summary>
        public static float RainSurge { get; private set; }
        /// <summary>湿墨冲刷强度 0-1：旧世界颜色向下流淌溶解</summary>
        public static float InkRun { get; private set; }
        /// <summary>雨帘遮蔽 0-1：满幕水幕的合拢进度，1=全遮蔽</summary>
        public static float CurtainCover { get; private set; }
        /// <summary>排墨进度 0-1：水幕自上而下排走，露出深层</summary>
        public static float Drain { get; private set; }
        /// <summary>结算雷闪：隔着水幕亮起的惨白</summary>
        public static float Flash { get; private set; }
        /// <summary>伞的躁动包络，起势段起颤、合幕段拉满、结算后回落</summary>
        public static float UmbrellaAgitation { get; private set; }
        /// <summary>溺亡拖入模式：被鬼奴杀死时的下潜，起手带一记被拽走的重拍</summary>
        public static bool DrownMode { get; private set; }

        /// <summary>渲染合成是否需要介入：排尽且无闪光后输出等于输入，直接让位</summary>
        public static bool RenderActive => Active
            && (InkRun > 0.0005f || Flash > 0.0005f
            || (CurtainCover > 0.0005f && Drain < 0.999f));

        /// <summary>开始深潜演出，仅本地玩家且身处雨世界未达最深层时生效；重复调用无效</summary>
        public static void Begin(Player player, Vector2 umbrellaGround)
            => BeginCore(player, umbrellaGround, drown: false);

        /// <summary>
        /// 溺亡拖入：被鬼奴杀死后的下潜，共用整条冲刷时间轴，
        /// 只在起手补一记被拽走的重拍
        /// </summary>
        public static void BeginFromDrown(Player player, Vector2 ground)
            => BeginCore(player, ground, drown: true);

        private static void BeginCore(Player player, Vector2 umbrellaGround, bool drown) {
            if (Active || OniRainWorldTransition.Active || Main.dedServ
                || player == null || player.whoAmI != Main.myPlayer || !player.Alives()) {
                return;
            }
            if (!OniRainWorldState.LocalIn
                || OniRainWorldState.LocalDepth >= OniRainWorldState.MaxDepth) {
                return;
            }

            Active = true;
            Timer = 0;
            UmbrellaWorld = umbrellaGround;
            FocusWorld = umbrellaGround + new Vector2(0f, -40f);
            ZeroEnvelopes();
            DrownMode = drown;

            if (drown) {
                //被拽走的一记重锤：致死帧直接砸下，雨随后灌满
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = -0.5f,
                    Volume = 0.8f,
                    MaxInstances = 3,
                }, player.Center);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Pitch = -0.6f,
                    Volume = 0.65f,
                    MaxInstances = 3,
                }, player.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.95f,
                    Volume = 0.7f,
                    MaxInstances = 3,
                }, player.Bottom);
                player.CWR()?.GetScreenShake(10f);
            }
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
            SpawnStageFx(player);
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

            //骤雨增压：起势段涨满，结算前一直压着；结算后深层自身密度接管
            RainSurge = t >= CommitFrame ? 0f : 0.7f * Smooth01(t / (float)SurgeEnd);

            //湿墨冲刷：起势中段起手，合幕时拉满；结算后新世界不再流墨（切断藏在满幕后）
            float inkStart = SurgeEnd * 0.6f;
            InkRun = t >= CommitFrame ? 0f
                : Smooth01((t - inkStart) / (CoverEnd - inkStart));

            //雨帘遮蔽：略晚于流墨合拢，满幕后保持，揭开交给排墨
            CurtainCover = Smooth01((t - 30f) / (CoverEnd - 30f));

            //排墨：先慢后快再慢，水幕整体向下排走
            Drain = t <= DrainStart ? 0f
                : CubicInOut((t - DrainStart) / (float)(DrainEnd - DrainStart));

            //结算雷闪：短促起势，长尾退潮
            if (t >= CommitFrame - 2 && t < CommitFrame) {
                Flash = (t - (CommitFrame - 2)) / 2f;
            }
            else if (t >= CommitFrame) {
                Flash = MathHelper.Clamp(1f - (t - CommitFrame) / 18f, 0f, 1f);
            }
            else {
                Flash = 0f;
            }

            //伞的躁动：起势起颤、合幕拉满、结算后松劲
            UmbrellaAgitation = t <= SurgeEnd
                ? Smooth01(t / (float)SurgeEnd)
                : t < CommitFrame ? 1f
                : 1f - Smooth01((t - CommitFrame) / 40f);
        }

        /// <summary>相位粒子：合幕段满屏流水、排墨段沿撕口线泼水、落定溅圈</summary>
        private static void SpawnStageFx(Player player) {
            //湿墨色板，与鬼雨体系一致
            Color pale = new(170, 185, 190);
            Color damp = new(58, 66, 70);

            //合幕段：整幅屏幕都在流水，快速竖直水线随遮蔽加密
            if (Timer > 30 && Timer < CoverEnd && Timer % 2 == 0) {
                Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);
                int burst = 2 + (int)(CurtainCover * 3f);
                for (int i = 0; i < burst; i++) {
                    Vector2 screenPx = new(
                        Main.rand.NextFloat(0.02f, 0.98f) * Main.screenWidth,
                        Main.rand.NextFloat(-0.05f, 0.6f) * Main.screenHeight);
                    Vector2 world = Vector2.Transform(screenPx, inv) + Main.screenPosition;
                    Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f),
                        Main.rand.NextFloat(15f, 21f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(world, vel,
                        pale * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.8f, 1.3f))
                        ?.Configure(Main.rand.Next(26, 44), vel.X);
                }
            }

            //排墨段：撕口前沿向下泼水，水是整片排走的
            if (Timer >= DrainStart && Timer < DrainEnd && Timer % 3 == 0) {
                float frontUv = MathHelper.Clamp(Drain * 1.25f, 0f, 1f);
                if (frontUv > 0.02f && frontUv < 0.98f) {
                    Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);
                    for (int i = 0; i < 3; i++) {
                        Vector2 screenPx = new(
                            Main.rand.NextFloat(0.04f, 0.96f) * Main.screenWidth,
                            frontUv * Main.screenHeight);
                        Vector2 world = Vector2.Transform(screenPx, inv) + Main.screenPosition;
                        Vector2 vel = new(Main.rand.NextFloat(-0.5f, 0.5f),
                            Main.rand.NextFloat(9f, 14f));
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(world, vel,
                            pale * Main.rand.NextFloat(0.35f, 0.55f),
                            Main.rand.NextFloat(0.5f, 0.8f))
                            ?.Configure(Main.rand.Next(18, 30), vel.X);
                    }
                }
            }

            //落定确认拍：排尽的水在脚下溅开一圈，潮气腾起
            if (Timer == DrainEnd) {
                for (int i = 0; i < 12; i++) {
                    float angle = -MathHelper.Pi * (0.15f + 0.7f * i / 11f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        player.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), -2f),
                        vel, pale * Main.rand.NextFloat(0.45f, 0.6f),
                        Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(20, 32), vel.X);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        player.Bottom + new Vector2(Main.rand.NextFloat(-28f, 28f), -4f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.08f),
                        damp * Main.rand.NextFloat(0.6f, 0.9f),
                        Main.rand.NextFloat(0.7f, 1.05f))
                        ?.Configure(Main.rand.Next(80, 120));
                }
                player.CWR()?.GetScreenShake(3.5f);
            }
        }

        private static void PlayBeats(Player player) {
            switch (Timer) {
                case 6:
                    //远处一声闷雷，雨要变天
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.85f,
                        Volume = 0.4f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case 24:
                    //雨声骤密第一拍
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.8f,
                        Volume = 0.55f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case SurgeEnd:
                    //雨压上来，已经不是下雨是灌
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.55f,
                        Volume = 0.65f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 78:
                    //颜色开始被冲走：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                        Pitch = -0.9f,
                        Volume = 0.45f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case CoverEnd - 4:
                    //水幕合拢，世界被整幅盖住
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                        Pitch = -0.8f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case DrainStart + 6:
                    //排水：整片水向下抽走
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.3f,
                        Volume = 0.6f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case DrainEnd:
                    //落定一记压低的闷锣，深层的雨声接管
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.9f,
                        Volume = 0.4f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
            }
        }

        /// <summary>结算：满幕遮蔽下潜入更深一层，天空与调色的跳变全被水幕盖住</summary>
        private static void Commit(Player player) {
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.7f,
                Volume = 0.85f,
                MaxInstances = 3,
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.75f,
                Volume = 0.55f,
                MaxInstances = 3,
            }, player.Center);
            player.CWR()?.GetScreenShake(8f);
            OniRainWorldState.DescendLocal(player);
        }

        private static void Finish() {
            //排尽、无闪光时输出等于输入，直接停用无跳变
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>玩家中途失效：结算前取消不下潜，结算后直接收尾（深度已切换）</summary>
        private static void Abort() {
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>世界卸载/回主菜单的硬复位，不回滚已结算的深度</summary>
        internal static void HardReset() {
            Active = false;
            Timer = 0;
            ZeroEnvelopes();
        }

        private static void ZeroEnvelopes() {
            RainSurge = InkRun = CurtainCover = Drain = Flash = 0f;
            UmbrellaAgitation = 0f;
            DrownMode = false;
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
