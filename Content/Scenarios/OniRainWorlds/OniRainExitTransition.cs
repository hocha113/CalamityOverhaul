using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 送出演出的相位状态机（沈幽初遇落幕后被送出鬼雨世界）：
    /// 骤雨合幕 → 满幕遮蔽下深度归零并真正交付鬼伞 → 排水揭回真实世界 → 落定。<br/>
    /// 与深潜同一套冲刷语汇，渲染复用 <see cref="OniRainDescentRender"/> 的合成管线
    /// （两条演出互斥，渲染层按活动源取数）；节拍常量是唯一时钟，纯本地演出量。
    /// </summary>
    internal static class OniRainExitTransition
    {
        //节拍表（60fps）：合幕起势0-40 → 冲刷合幕40-92 → 遮蔽结算100 → 排水110-152 → 落定168
        public const int SurgeEnd = 40;
        public const int CoverEnd = 92;
        /// <summary>送出结算帧：满幕遮蔽下深度归零+发伞，跳变全被水幕盖住</summary>
        public const int CommitFrame = 100;
        public const int DrainStart = 110;
        public const int DrainEnd = 152;
        public const int TotalFrames = 168;

        public static bool Active { get; private set; }
        public static int Timer { get; private set; }

        /// <summary>运镜焦点：起演时的玩家立点，这次被送走的门是人自己</summary>
        public static Vector2 FocusWorld { get; private set; }

        //渲染包络，Update 逐帧推进
        /// <summary>骤雨增压：送别的雨最后压一阵，结算后真实世界无雨</summary>
        public static float RainSurge { get; private set; }
        /// <summary>湿墨冲刷强度 0-1：深层的颜色被冲得向下流淌</summary>
        public static float InkRun { get; private set; }
        /// <summary>雨帘遮蔽 0-1：满幕水幕合拢进度</summary>
        public static float CurtainCover { get; private set; }
        /// <summary>排水进度 0-1：水幕排走，露出真实世界</summary>
        public static float Drain { get; private set; }
        /// <summary>结算雷闪</summary>
        public static float Flash { get; private set; }

        /// <summary>渲染合成是否需要介入：排尽且无闪光后输出等于输入</summary>
        public static bool RenderActive => Active
            && (InkRun > 0.0005f || Flash > 0.0005f
            || (CurtainCover > 0.0005f && Drain < 0.999f));

        /// <summary>开始送出演出，仅本地玩家且身处雨世界时生效；重复调用无效</summary>
        public static void Begin(Player player) {
            if (Active || OniRainWorldTransition.Active || OniRainDescentTransition.Active
                || Main.dedServ || player == null || player.whoAmI != Main.myPlayer
                || !player.Alives()) {
                return;
            }
            if (!OniRainWorldState.LocalIn) {
                return;
            }

            Active = true;
            Timer = 0;
            FocusWorld = player.Center;
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

            //骤雨增压：起势段涨满压着送行，结算后真实世界自然无雨
            RainSurge = t >= CommitFrame ? 0f : 0.6f * Smooth01(t / (float)SurgeEnd);

            //湿墨冲刷：起势中段起手，合幕时拉满；结算后真实世界不再流墨
            float inkStart = SurgeEnd * 0.6f;
            InkRun = t >= CommitFrame ? 0f
                : Smooth01((t - inkStart) / (CoverEnd - inkStart));

            //雨帘遮蔽：略晚于流墨合拢，满幕后保持，揭开交给排水
            CurtainCover = Smooth01((t - 26f) / (CoverEnd - 26f));

            //排水：先慢后快再慢，水幕整体排走
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
        }

        /// <summary>相位粒子：合幕段满屏流水、排水段沿前沿泼水、落定溅圈——与深潜同语汇</summary>
        private static void SpawnStageFx(Player player) {
            //湿墨色板，与鬼雨体系一致
            Color pale = new(170, 185, 190);
            Color damp = new(58, 66, 70);

            //合幕段：整幅屏幕都在流水
            if (Timer > 26 && Timer < CoverEnd && Timer % 2 == 0) {
                Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);
                int burst = 2 + (int)(CurtainCover * 3f);
                for (int i = 0; i < burst; i++) {
                    Vector2 screenPx = new(
                        Main.rand.NextFloat(0.02f, 0.98f) * Main.screenWidth,
                        Main.rand.NextFloat(-0.05f, 0.6f) * Main.screenHeight);
                    Vector2 world = Vector2.Transform(screenPx, inv) + Main.screenPosition;
                    Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f),
                        Main.rand.NextFloat(14f, 20f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(world, vel,
                        pale * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.8f, 1.25f))
                        ?.Configure(Main.rand.Next(26, 44), vel.X);
                }
            }

            //排水段：前沿向下泼水
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
                            Main.rand.NextFloat(9f, 13f));
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(world, vel,
                            pale * Main.rand.NextFloat(0.35f, 0.55f),
                            Main.rand.NextFloat(0.5f, 0.75f))
                            ?.Configure(Main.rand.Next(18, 30), vel.X);
                    }
                }
            }

            //落定确认拍：真实世界的地面上溅开最后一圈水，潮气散尽
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
                player.CWR()?.GetScreenShake(3f);
            }
        }

        private static void PlayBeats(Player player) {
            switch (Timer) {
                case 8:
                    //送别的雨压上来
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.8f,
                        Volume = 0.55f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case SurgeEnd:
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.55f,
                        Volume = 0.6f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case 70:
                    //颜色被冲走：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                        Pitch = -0.9f,
                        Volume = 0.42f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case CoverEnd - 4:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                        Pitch = -0.75f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case DrainStart + 6:
                    //水向下抽走，真实世界露出来
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.25f,
                        Volume = 0.6f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case DrainEnd:
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.9f,
                        Volume = 0.4f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
            }
        }

        /// <summary>结算：满幕遮蔽下深度归零、真正交付鬼伞，跳变全被水幕盖住</summary>
        private static void Commit(Player player) {
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.7f,
                Volume = 0.8f,
                MaxInstances = 3,
            }, player.Center);
            //交付拍：雨声里一记清响
            SoundEngine.PlaySound(SoundID.Item4 with {
                Pitch = -0.2f,
                Volume = 0.6f,
                MaxInstances = 3,
            }, player.Center);
            player.CWR()?.GetScreenShake(7f);

            OniRainWorldState.ExitToSurfaceLocal(player);
            GrantKikasa(player);
        }

        /// <summary>真正交付鬼伞，防重复；背包满时 GiveItem 自动落地</summary>
        internal static void GrantKikasa(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer
                || ShenyoStorySync.KikasaGranted) {
                return;
            }
            ShenyoStorySync.KikasaGranted = true;
            player.GiveItem(player.GetSource_Misc("OniRainWorld"),
                ModContent.ItemType<KikasaItem>());
        }

        private static void Finish() {
            //排尽、无闪光时输出等于输入，直接停用无跳变
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>玩家中途失效：结算前取消（进度自愈会再送一次），结算后直接收尾</summary>
        private static void Abort() {
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>世界卸载/回主菜单的硬复位，不回滚已结算的状态</summary>
        internal static void HardReset() {
            Active = false;
            Timer = 0;
            ZeroEnvelopes();
        }

        private static void ZeroEnvelopes() {
            RainSurge = InkRun = CurtainCover = Drain = Flash = 0f;
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
