using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.CameraModifiers;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 村正次元斩主控弹幕，负责控制整个演出流程
    /// 与MuraExecutionCutOnSpan的伤害爆发时机同步
    /// </summary>
    internal class MuraDimensionSlash : ModProjectile
    {
        public override string Texture => MuraSlayAllAssets.TransparentImg;

        #region 属性
        /// <summary>
        /// RGB色差强度
        /// </summary>
        public float ColorSeparation {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        /// <summary>
        /// 屏幕灰暗度
        /// </summary>
        public float ScreenDarkness {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        /// <summary>
        /// 碎屏状态:0初始化 1绘制中 2缩小中 3不绘制
        /// </summary>
        public float BrokenScreenState {
            get => Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        /// <summary>
        /// 径向模糊强度
        /// </summary>
        public float RadialBlurStrength {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        /// <summary>
        /// 滤镜强度
        /// </summary>
        public float FilterIntensity {
            get => Projectile.localAI[1];
            set => Projectile.localAI[1] = value;
        }

        /// <summary>
        /// 内部计时器
        /// </summary>
        private int Timer {
            get => (int)Projectile.localAI[2];
            set => Projectile.localAI[2] = value;
        }

        /// <summary>
        /// 玩家引用
        /// </summary>
        public Player Owner => Main.player[Projectile.owner];
        #endregion

        //MuraExecutionCutOnSpan的timeLeft=500，在timeLeft==50时生成MuraExecutionCut
        //因此从生成到爆发需要约450帧（考虑MaxUpdates=5，实际游戏帧约90帧）
        //演出总时长设为120帧，与斩击爆发同步
        private const int TotalDuration = 120;
        private const int BurstTime = 90; //爆发时刻（对应MuraExecutionCut生成）

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void OnSpawn(IEntitySource source) {
            ScreenDarkness = 0;
            BrokenScreenState = 3;
            FilterIntensity = 0;
            RadialBlurStrength = 0;
            ColorSeparation = 0;
            Timer = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            int time = Timer;

            //持续时间冻结直到演出结束
            if (time < TotalDuration - 10) {
                CWRWorld.TimeFrozenTick = 2;
            }

            //===== 阶段1: 时停开始 (0-10帧) =====
            //立即生成弧形斩击表示进入时停
            if (time == 1) {
                //初始屏幕闪烁
                ScreenDarkness = 60;
                AddCameraShake(8f, 10f, 15f);

                //生成环绕弧形斩击
                for (int i = 0; i < 8; i++) {
                    float rotation = MathHelper.TwoPi / 8f * i + Main.rand.NextFloat(-0.3f, 0.3f);
                    Vector2 vec = new Vector2(0, Main.rand.NextFloat(60f, Main.screenHeight / 4f)).RotatedBy(rotation);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Main.LocalPlayer.Center + vec
                        , Vector2.Zero, ModContent.ProjectileType<MuraSlashArc>(), 0, 0, -1, ai0: 0);
                }

                //启动碎屏扭曲效果
                BrokenScreenState = 0;
            }

            //===== 阶段2: 时停中的斩击蓄力 (10-80帧) =====
            if (time >= 10 && time < 80) {
                //滤镜效果渐入（屏幕反色/暗红调）
                if (FilterIntensity < 2) {
                    FilterIntensity += 0.08f;
                    if (FilterIntensity > 2) FilterIntensity = 2;
                }

                //持续生成斩击线，营造空间被切割的感觉
                if (time % 2 == 0) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI()
                        , Main.LocalPlayer.Center + Main.rand.NextVector2Circular(Main.screenWidth / 8f, Main.screenHeight / 8f) * 3f
                        , Vector2.Zero, ModContent.ProjectileType<MuraSlashLine>(), 0, 0, -1, ai0: Main.rand.NextBool() ? 0 : 1);
                }

                //偶尔补充弧形斩击
                if (time % 12 == 0) {
                    float rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vec = new Vector2(0, Main.rand.NextFloat(80f, Main.screenHeight / 3f)).RotatedBy(rotation);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Main.LocalPlayer.Center + vec
                        , Vector2.Zero, ModContent.ProjectileType<MuraSlashArc>(), 0, 0, -1, ai0: Main.rand.NextBool() ? 0 : 1f);
                }

                //轻微屏幕抖动，营造紧张感
                if (time % 20 == 0) {
                    AddCameraShake(3f, 5f, 8f);
                }

                //屏幕闪烁节奏
                if (time == 30 || time == 55 || time == 75) {
                    ScreenDarkness += Main.rand.Next(40, 70);
                    AddCameraShake(5f, 8f, 12f);
                }
            }

            //===== 阶段3: 爆发前准备 (80-90帧) =====
            if (time >= 80 && time < BurstTime) {
                //径向模糊渐入，暗示能量汇聚
                RadialBlurStrength += 0.025f;
                if (RadialBlurStrength > 0.25f) RadialBlurStrength = 0.25f;

                //色差效果渐入
                ColorSeparation += 0.001f;
                if (ColorSeparation > 0.008f) ColorSeparation = 0.008f;

                //密集生成斩击线
                if (time % 2 == 0) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI()
                        , Main.LocalPlayer.Center + Main.rand.NextVector2Circular(Main.screenWidth / 6f, Main.screenHeight / 6f) * 2.5f
                        , Vector2.Zero, ModContent.ProjectileType<MuraSlashLine>(), 0, 0, -1, ai0: 1);
                }
            }

            //===== 阶段4: 爆发瞬间 (90帧) =====
            //此时MuraExecutionCut正在生成并造成伤害
            if (time == BurstTime) {
                //强烈屏幕闪白
                ScreenDarkness = 180;

                //强力相机震动
                AddCameraShake(15f, 20f, 25f);

                //径向模糊达到峰值
                RadialBlurStrength = 0.4f;
                ColorSeparation = 0.015f;

                //碎屏开始收缩
                BrokenScreenState = 2;

                //爆发粒子效果
                for (int i = 0; i < 12; i++) {
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: true, ParticleOrchestraType.StardustPunch
                        , new ParticleOrchestraSettings {
                            PositionInWorld = Main.LocalPlayer.Center,
                            MovementVector = new Vector2(Main.rand.NextFloat(-8, 8), Main.rand.NextFloat(-8, 8))
                        });
                }

                //密集斩击线爆发
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI()
                        , Main.LocalPlayer.Center + Main.rand.NextVector2Circular(Main.screenWidth / 5f, Main.screenHeight / 5f) * 2f
                        , Vector2.Zero, ModContent.ProjectileType<MuraSlashLine>(), 0, 0, -1, ai0: 1);
                }
            }

            //===== 阶段5: 收尾恢复 (90-120帧) =====
            if (time > BurstTime) {
                //滤镜效果渐出
                FilterIntensity -= 0.07f;
                if (FilterIntensity < 0) FilterIntensity = 0;

                //径向模糊渐出
                RadialBlurStrength -= 0.02f;
                if (RadialBlurStrength < 0) RadialBlurStrength = 0;

                //色差效果渐出
                ColorSeparation -= 0.001f;
                if (ColorSeparation < 0) ColorSeparation = 0;

                //碎屏效果结束
                if (time == BurstTime + 15) {
                    BrokenScreenState = 3;
                }
            }

            //屏幕暗度自然衰减
            ScreenDarkness = System.Math.Max(0, ScreenDarkness - 4);
        }

        private void AddCameraShake(float strength, float duration, float vibration) {
            var bump = new PunchCameraModifier(Main.LocalPlayer.Center, Main.rand.NextVector2Unit(), strength, duration, (int)vibration);
            Main.instance.CameraModifiers.Add(bump);
        }

        public override void OnKill(int timeLeft) {
            //确保所有效果清除
            BrokenScreenState = 3;
            FilterIntensity = 0;
            RadialBlurStrength = 0;
            ColorSeparation = 0;
            ScreenDarkness = 0;
        }
    }
}
