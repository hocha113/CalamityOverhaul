using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Demo
{
    /// <summary>
    /// 绯红裂空斩：完整连段演出编排器（跟随玩家，一条时间轴调度多道子刀光）<br/>
    /// 节拍（60fps）：<br/>
    /// T0-6   环月回旋 —— 椭圆全周回旋斩快速扫开（开场铺垫，薄、快）<br/>
    /// T6     回旋收势小型光斩（轻白闪确认，无顿帧）<br/>
    /// T10/14 交叉裂斩 —— 两道白热直线斩呈 X 交叉（明度对比层）<br/>
    /// T16    交叉点迸发（中等确认）<br/>
    /// T17-21 负空间停顿 —— 已有刀光侵蚀，无新元素（蓄势）<br/>
    /// T22-25 月牙终结 —— 厚重月牙扫开，T25 冲击帧：爆点全层 + 世界顿帧 + 白闪<br/>
    /// T27-33 负片收缩暗核<br/>
    /// T35-76 长尾：侵蚀燃边、烟化、余韵光球内爆消散<br/>
    /// 屏幕级只保留短白闪与 Bloom（<see cref="CrimsonImpactFX"/>）—— 不做震屏/压暗/变焦，防眩晕<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=挥动镜像(±1) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendSlash : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 子刀光定义 ====
        private struct SlashDef
        {
            public int Birth;            //时间轴出生帧
            public int SweepFrames;      //扫开帧数
            public int Life;             //总寿命（相对出生）
            public int ErodeStart;       //侵蚀起点（相对出生）
            public int ErodeFrames;
            public float ColorShiftDelay;
            public float ColorShiftFrames;
            public int DamageStart;      //伤害窗口（相对出生）
            public int DamageEnd;
            public float Mode;           //0=弧形 1=直线
            public float Rot;            //弧:quad 基准角 直:刃方向角
            public float Span;           //弧跨度（弧度）
            public float Thick;          //shader 厚度
            public float HalfX;          //quad 半尺寸（直线=半刃长）
            public float HalfY;          //quad 半尺寸（<HalfX 即椭圆压扁；直线=半幅宽）
            public float Flip;
            public float Opacity;
            public float FrontGlow;
            public float OffsetAlongAim; //中心沿瞄准方向偏移
            public float Seed;
            public float TailErode;      //彗星尾定向蒸发强度上限（0=不蒸发）
            public float FlashPower;     //全形白闪帧强度
        }

        /// <summary>子刀光单帧动画状态（几何动画包：过冲/外扩/厚度呼吸/惯性收势/尾蒸发/白闪/奔涌）</summary>
        private struct SlashAnim
        {
            public float ScaleMul;   //出生爆发+过冲+缓慢外扩
            public float RotOffset;  //扫掠后惯性收势旋转
            public float ThickMul;   //薄入→冲击帧最厚→衰减
            public float TailErode;  //彗星尾蒸发进度
            public float Flash;      //全形白闪
            public float FlowPhase;  //能量沿刃奔涌相位
        }

        //==== 时间轴常量 ====
        private const int HitstopFrames = 4;
        private const int BurstFadeFrames = 16;
        private const int PingFrame = 6;        //回旋收势
        private const int CrossFrame = 16;      //交叉点迸发
        private const int FinisherIndex = 3;
        private const int TotalLifetime = 82;

        //==== 调色 ====
        private static readonly Vector3 ColHot = new(1.60f, 1.32f, 1.08f);
        private static readonly Vector3 ColBright = new(1.30f, 0.16f, 0.10f);
        private static readonly Vector3 ColDeep = new(0.62f, 0.05f, 0.07f);
        private static readonly Vector3 ColDark = new(0.16f, 0.015f, 0.035f);

        private SlashDef[] slashes;
        private int timer;
        private int hitstopHold;
        private bool impactFired;
        private int finisherImpactFrame;
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;

        private float AimAngle => Projectile.ai[0];
        private float Flip => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;
        private Vector2 AimDir => AimAngle.ToRotationVector2();

        private Vector2 ImpactWorldPos {
            get {
                float outer = slashes != null ? slashes[FinisherIndex].HalfX * 0.90f : 200f * SizeMul;
                return Projectile.Center + AimDir * outer * 0.92f;
            }
        }

        private Vector2 CrossWorldPos => Projectile.Center + AimDir * 120f * SizeMul;

        /// <summary>
        /// 触发接口：在持有者客户端调用（例如 testItem 的 Shoot/UseItem 内 <c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；整套连段跟随玩家移动
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="origin">起手锚点（生成后每帧跟随玩家中心）</param>
        /// <param name="aim">瞄准方向（无需归一化，终结月牙冲击端落在该方向）</param>
        /// <param name="damage">单段伤害（连段可多次命中）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="flip">挥动镜像 ±1</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, int flip = 1, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            return Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: flip, ai2: scale);
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime + HitstopFrames + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;   //连段各节拍可分别结算
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>依 aim/flip/scale 确定性生成连段编排，各端一致</summary>
        private void BuildSchedule() {
            float s = SizeMul;
            float a = AimAngle;
            float f = Flip;

            slashes = new SlashDef[4];

            //0 环月回旋：椭圆全周，薄且快，尾部高蒸发 → 读作"旋转的运动拖尾"
            slashes[0] = new SlashDef {
                Birth = 0, SweepFrames = 6, Life = 26, ErodeStart = 8, ErodeFrames = 16,
                ColorShiftDelay = 6, ColorShiftFrames = 12, DamageStart = 2, DamageEnd = 8,
                Mode = 0f, Rot = a - f * 2.95f, Span = 5.90f, Thick = 0.20f,
                HalfX = 170f * s, HalfY = 78f * s, Flip = f,
                Opacity = 0.80f, FrontGlow = 1.7f, OffsetAlongAim = 0f, Seed = 0.13f,
                TailErode = 0.85f, FlashPower = 0.45f,
            };

            //1/2 交叉裂斩：两道白热直线斩，前移交叉于打击区
            slashes[1] = new SlashDef {
                Birth = 10, SweepFrames = 3, Life = 24, ErodeStart = 7, ErodeFrames = 15,
                ColorShiftDelay = 9, ColorShiftFrames = 11, DamageStart = 0, DamageEnd = 8,
                Mode = 1f, Rot = a - 0.52f * f, Span = 0f, Thick = 0.34f,
                HalfX = 235f * s, HalfY = 128f * s, Flip = f,
                Opacity = 0.95f, FrontGlow = 2.7f, OffsetAlongAim = 120f * s, Seed = 0.47f,
                TailErode = 0.55f, FlashPower = 1f,
            };
            slashes[2] = slashes[1] with {
                Birth = 14, Rot = a + 0.52f * f, Flip = -f, Seed = 0.71f,
            };

            //3 月牙终结：厚重主月牙（力量核心）
            slashes[3] = new SlashDef {
                Birth = 22, SweepFrames = 3, Life = 54, ErodeStart = 10, ErodeFrames = 30,
                ColorShiftDelay = 6, ColorShiftFrames = 18, DamageStart = 1, DamageEnd = 12,
                Mode = 0f, Rot = a - f * 1.775f, Span = 3.55f, Thick = 0.36f,
                HalfX = 245f * s, HalfY = 245f * s, Flip = f,
                Opacity = 1f, FrontGlow = 2.6f, OffsetAlongAim = 0f, Seed = 0.88f,
                TailErode = 0.42f, FlashPower = 1f,
            };

            finisherImpactFrame = slashes[FinisherIndex].Birth + slashes[FinisherIndex].SweepFrames;
        }

        //==== 子刀光生命周期采样 ====

        private static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        /// <summary>带过冲的缓出（尺寸爆发"弹"出的关键曲线，峰值 ~1.05 后回落 1）</summary>
        private static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private float SlashSweep(in SlashDef d, int lt) => EaseOutCubic(lt / (float)d.SweepFrames);

        private float SlashErode(in SlashDef d, int lt) => SmoothStep01((lt - d.ErodeStart) / (float)d.ErodeFrames);

        private float SlashColorShift(in SlashDef d, int lt) => MathHelper.Clamp((lt - d.ColorShiftDelay) / d.ColorShiftFrames, 0f, 1f);

        private float SlashOpacity(in SlashDef d, int lt) => d.Opacity * (1f - MathHelper.Clamp((lt - (d.Life - 6)) / 6f, 0f, 1f));

        private float SlashFrontGlow(in SlashDef d, int lt) => lt <= d.SweepFrames + 1
            ? d.FrontGlow
            : d.FrontGlow * MathF.Max(0f, 1f - (lt - d.SweepFrames - 1) / 5f);

        /// <summary>几何动画包：静态贴纸 → 运动实体的核心。所有形变随生命期逐帧演进</summary>
        private SlashAnim GetSlashAnim(in SlashDef d, int lt) {
            float lifeT = MathHelper.Clamp(lt / (float)d.Life, 0f, 1f);

            //出生爆发：62% 尺寸起步，easeOutBack 过冲到 ~104% 回落，随后全程缓慢外扩（波向外传播）
            float burstT = MathHelper.Clamp(lt / (d.SweepFrames + 2f), 0f, 1f);
            float scale = MathHelper.Lerp(0.62f, 1f, EaseOutBack(burstT)) + 0.07f * lifeT;

            //惯性收势：扫掠结束后沿挥动方向继续减速旋转（follow-through）
            float followT = MathHelper.Clamp((lt - d.SweepFrames) / 14f, 0f, 1f);
            float rotOff = d.Flip * 0.13f * (1f - (1f - followT) * (1f - followT));

            //厚度呼吸：薄入 → 冲击帧最厚 → 消散期变薄
            float thickIn = EaseOutCubic(lt / (d.SweepFrames + 2f));
            float thickMul = MathHelper.Lerp(0.68f, 1.12f, thickIn)
                * (1f - 0.42f * SmoothStep01((lifeT - 0.45f) / 0.55f));

            //彗星尾：扫掠完成即从起笔端向前蒸发
            float tail = d.TailErode * SmoothStep01((lt - d.SweepFrames) / (d.Life * 0.72f));

            //全形白闪帧：完全张开瞬间过曝 1~2 帧，速落
            float ft = lt - d.SweepFrames;
            float flash = ft < 0f ? 0f : ft <= 1f ? 1f : MathF.Pow(0.52f, ft - 1f);
            if (flash < 0.02f) {
                flash = 0f;
            }
            flash *= d.FlashPower;

            //能量沿刃奔涌：前段快速冲出、减速停驻
            float flowPhase = 0.62f * EaseOutCubic(lt / 15f);

            return new SlashAnim {
                ScaleMul = scale, RotOffset = rotOff, ThickMul = thickMul,
                TailErode = tail, Flash = flash, FlowPhase = flowPhase,
            };
        }

        private Vector2 SlashCenter(in SlashDef d) => Projectile.Center + AimDir * d.OffsetAlongAim;

        /// <summary>刀光带中线上一点：uc=0..1 沿刃，含几何动画（缩放/收势旋转/厚度）</summary>
        private Vector2 PointAt(in SlashDef d, float uc, int lt) {
            SlashAnim anim = GetSlashAnim(in d, lt);
            Vector2 ax = (d.Rot + anim.RotOffset).ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            Vector2 c = SlashCenter(d);
            float hx = d.HalfX * anim.ScaleMul;
            float hy = d.HalfY * anim.ScaleMul;
            if (d.Mode > 0.5f) {
                //直线：沿刃长 -HalfX..+HalfX
                return c + ax * (uc * 2f - 1f) * hx * 0.90f;
            }
            float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
            float w = d.Thick * anim.ThickMul * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
            float rFrac = 0.90f - w * 0.5f;
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            return c + ax * MathF.Cos(phi) * rFrac * hx + ay * MathF.Sin(phi) * rFrac * hy;
        }

        //==================== 时间轴推进 ====================

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                BuildSchedule();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
            }

            //整套连段跟随玩家
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                Projectile.Center = owner.Center;
            }

            //顿帧保持：终结冲击后世界冻结期间时间轴挂起，姿态定格
            if (impactFired && hitstopHold > 0 && CWRWorld.TimeFrozenTick > 0) {
                hitstopHold--;
                Projectile.timeLeft++;
                PushScreenState();
                return;
            }

            timer++;
            DispatchBeats();

            if (!Main.dedServ) {
                SpawnSweepSparks();
                SpawnEdgeSmoke();
            }

            Lighting.AddLight(ImpactWorldPos, new Vector3(1.0f, 0.25f, 0.18f));
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.10f));

            PushScreenState();
        }

        /// <summary>时间轴节拍分发：起手/收势/交叉/终结各一击</summary>
        private void DispatchBeats() {
            switch (timer) {
                case PingFrame:
                    //回旋收势：轻确认
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.65f, Volume = 0.35f }, Projectile.Center);
                    CrimsonImpactFX.PushImpact(CrossWorldPos, 0.16f);
                    SpawnBeatSparks(CrossWorldPos, 6, 0.7f);
                    break;

                case 10:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.45f, Volume = 0.5f }, Projectile.Center);
                    break;

                case 14:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.6f, Volume = 0.5f }, Projectile.Center);
                    break;

                case CrossFrame:
                    //交叉点迸发：中等确认
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.7f, Volume = 0.45f }, CrossWorldPos);
                    CrimsonImpactFX.PushImpact(CrossWorldPos, 0.28f);
                    SpawnBeatSparks(CrossWorldPos, 12, 1f);
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_CrimsonHitFlash>(CrossWorldPos, Vector2.Zero
                            , new Color(255, 200, 180), 1.1f * SizeMul);
                    }
                    break;

                case 22:
                    //终结起挥
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.95f }, Projectile.Center);
                    break;
            }

            if (!impactFired && timer >= finisherImpactFrame && slashes != null) {
                DoFinisherImpact();
            }
        }

        /// <summary>终结冲击帧：世界顿帧 + 白闪 + 爆点全层（无震屏/压暗/变焦）</summary>
        private void DoFinisherImpact() {
            impactFired = true;
            hitstopHold = HitstopFrames;
            CWRWorld.TimeFrozenTick = HitstopFrames;

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.9f }, ImpactWorldPos);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.55f, Volume = 0.45f }, ImpactWorldPos);

            CrimsonImpactFX.PushImpact(ImpactWorldPos, 0.5f);

            if (Main.dedServ) {
                return;
            }

            Vector2 impact = ImpactWorldPos;
            Vector2 aimDir = AimDir;

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact, Vector2.Zero
                , new Color(255, 225, 205), 1.5f * SizeMul);
            for (int i = 0; i < 2; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.55f, 0.8f) * SizeMul);
            }

            for (int i = 0; i < 20; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.78) * Main.rand.NextFloat(6f, 21f) * SizeMul;
                Color c = Main.rand.NextBool(3) ? new Color(255, 236, 210) : new Color(255, 92, 58);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(impact, vel, c
                    , Main.rand.NextFloat(0.5f, 1.05f) * SizeMul)
                    ?.Configure(Main.rand.Next(22, 40), affectedByGravity: true);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 8f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(impact, vel, new Color(255, 70, 46)
                    , Main.rand.NextFloat(0.35f, 0.6f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
            }
        }

        /// <summary>小节拍火花：轻量确认（收势/交叉点）</summary>
        private void SpawnBeatSparks(Vector2 pos, int count, float power) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 10f) * power * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 130, 90)
                    , Main.rand.NextFloat(0.35f, 0.7f) * power * SizeMul)
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: false);
            }
        }

        /// <summary>屏幕级演出包络：仅 Bloom + 终结脉冲（白闪由节拍触发）</summary>
        private void PushScreenState() {
            float bloom = 0.28f;
            if (impactFired) {
                float bp = MathHelper.Clamp((timer - finisherImpactFrame) / (float)BurstFadeFrames, 0f, 1f);
                bloom += 0.38f * (1f - bp) * (1f - bp);
            }
            if (timer > TotalLifetime - 14) {
                bloom *= (TotalLifetime - timer) / 14f;
            }
            CrimsonImpactFX.PushAmbience(ImpactWorldPos, MathF.Max(bloom, 0f));
        }

        /// <summary>各扫开中的刀光前缘火花</summary>
        private void SpawnSweepSparks() {
            if (slashes == null) {
                return;
            }
            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                int lt = timer - d.Birth;
                if (lt < 0 || lt > d.SweepFrames + 1) {
                    continue;
                }
                float edgeU = MathHelper.Clamp(SlashSweep(in d, lt) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = PointAt(in d, edgeU, lt);
                Vector2 tangent = (PointAt(in d, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), lt) - pos).SafeNormalize(AimDir);

                for (int k = 0; k < 2; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 11f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                        , Main.rand.NextFloat(0.3f, 0.6f) * SizeMul)
                        ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>终结月牙侵蚀期沿外缘生成细碎烟屑，后期停喷</summary>
        private void SpawnEdgeSmoke() {
            if (slashes == null || timer % 2 != 0) {
                return;
            }
            ref readonly SlashDef fin = ref slashes[FinisherIndex];
            int lt = timer - fin.Birth;
            if (lt <= fin.ErodeStart) {
                return;
            }
            float erode = SlashErode(in fin, lt);
            if (erode > 0.78f) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                float uc = Main.rand.NextFloat(0.12f, 0.96f);
                Vector2 mid = PointAt(in fin, uc, lt);
                Vector2 dir = (mid - Projectile.Center).SafeNormalize(AimDir);
                Vector2 pos = mid + dir * fin.HalfX * 0.06f;
                Vector2 vel = dir * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.35f, 0.35f);

                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel
                    , Color.White, Main.rand.NextFloat(0.055f, 0.105f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26)
                        , new Color(150, 26, 34), new Color(46, 16, 24)
                        , Main.rand.NextFloat(0.01f, 0.024f));
            }
        }

        //==================== 判定 ====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (slashes == null) {
                return false;
            }

            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                int lt = timer - d.Birth;
                if (lt < d.DamageStart || lt > d.DamageEnd) {
                    continue;
                }
                float sweepU = MathHelper.Clamp(SlashSweep(in d, lt) * 1.05f, 0f, 1f);

                if (d.Mode > 0.5f) {
                    //直线：单线段判定
                    Vector2 head = PointAt(in d, 0.05f, lt);
                    Vector2 tail = PointAt(in d, MathF.Min(0.95f, sweepU), lt);
                    float cp = 0f;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                        , head, tail, d.HalfY * 0.62f, ref cp)) {
                        return true;
                    }
                    continue;
                }

                //弧/椭圆：折线采样
                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float thickWorld = d.Thick * d.HalfX;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    if (uc > sweepU) {
                        break;
                    }
                    Vector2 mid = PointAt(in d, uc, lt);
                    if (hasPrev) {
                        float cp = 0f;
                        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                            , prev, mid, MathF.Max(28f, thickWorld * 0.8f), ref cp)) {
                            return true;
                        }
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //受击目标单独短暂顿帧（局部卡肉，不动镜头）
            target.CWR().TimeFrozenTick = HitstopFrames + 2;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.75f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = AimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 12f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center, vel, new Color(255, 96, 60)
                    , Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 28), affectedByGravity: true);
            }
        }

        //==================== 绘制（EndEntityDraw 弹幕扩展层） ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || slashes == null) {
                return;
            }

            DrawSlashes();
            DrawAdditiveLayers();
            DrawCollapseCore();
        }

        /// <summary>全部子刀光：三层异步结构（软辉光垫底滞后2帧 → 主体 → 白热核心薄条超前1帧），
        /// 层间时序差与几何差本身构成层次感</summary>
        private void DrawSlashes() {
            Effect fx = EffectLoader.DemoCrimsonSlash?.Value;
            Texture2D brush = DemoAssets.SlashBrush01?.Value;
            Texture2D noise = DemoAssets.NoiseSoft01?.Value;
            if (fx == null || brush == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBright"]?.SetValue(ColBright);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColDark"]?.SetValue(ColDark);
            fx.Parameters["uBrushTex"]?.SetValue(brush);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                int lt = timer - d.Birth;
                if (lt < 0 || lt >= d.Life) {
                    continue;
                }

                //软辉光垫底：滞后 2 帧出现，更宽更淡，余韵最后消
                DrawSlashLayer(device, fx, in d, lt - 2
                    , opacityMul: 0.30f, thickMul: 1.85f, scaleMul: 1.08f
                    , erodeBias: 0.06f, frontMul: 0.35f, flashMul: 0.5f, forceHot: false);

                //主体色带
                DrawSlashLayer(device, fx, in d, lt
                    , opacityMul: 1f, thickMul: 1f, scaleMul: 1f
                    , erodeBias: 0f, frontMul: 1f, flashMul: 1f, forceHot: false);

                //白热核心薄条：超前 1 帧领跑，贴锋利侧，不随生命期压暗
                DrawSlashLayer(device, fx, in d, Math.Min(lt + 1, d.Life - 1)
                    , opacityMul: 0.92f, thickMul: 0.42f, scaleMul: 1f
                    , erodeBias: 0f, frontMul: 1.25f, flashMul: 1f, forceHot: true);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>单层绘制：以层内时间 lt 采样生命周期与几何动画后提交 quad</summary>
        private void DrawSlashLayer(GraphicsDevice device, Effect fx, in SlashDef d, int lt
            , float opacityMul, float thickMul, float scaleMul, float erodeBias
            , float frontMul, float flashMul, bool forceHot) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            float opacity = SlashOpacity(in d, lt) * opacityMul;
            if (opacity <= 0.012f) {
                return;
            }

            SlashAnim anim = GetSlashAnim(in d, lt);

            fx.Parameters["uMode"]?.SetValue(d.Mode);
            fx.Parameters["uSweep"]?.SetValue(SlashSweep(in d, lt));
            fx.Parameters["uErode"]?.SetValue(MathHelper.Clamp(SlashErode(in d, lt) + erodeBias, 0f, 1f));
            fx.Parameters["uTailErode"]?.SetValue(anim.TailErode);
            fx.Parameters["uFlash"]?.SetValue(anim.Flash * flashMul);
            fx.Parameters["uFlowPhase"]?.SetValue(anim.FlowPhase);
            fx.Parameters["uColorShift"]?.SetValue(forceHot ? 0f : SlashColorShift(in d, lt));
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uFlip"]?.SetValue(d.Flip);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uArcSpan"]?.SetValue(d.Span > 0f ? d.Span : 1f);
            fx.Parameters["uThick"]?.SetValue(d.Thick * anim.ThickMul * thickMul);
            fx.Parameters["uFrontGlow"]?.SetValue(SlashFrontGlow(in d, lt) * frontMul);

            Vector2 center = SlashCenter(in d);
            Vector2 axisX = (d.Rot + anim.RotOffset).ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * anim.ScaleMul * scaleMul;
            float hy = d.HalfY * anim.ScaleMul * scaleMul;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>终结爆点 + 余韵光球，自管加色批次</summary>
        private void DrawAdditiveLayers() {
            bool burstActive = impactFired && timer - finisherImpactFrame < BurstFadeFrames;
            bool afterglowActive = impactFired && timer - finisherImpactFrame is >= 26 and < 46;
            if (!burstActive && !afterglowActive) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (burstActive) {
                DrawImpactBurst(sb);
            }

            //余韵：暗紫红光球内爆收束（参考序列尾帧）
            if (afterglowActive && DemoAssets.StarFlare01?.Value is Texture2D orb) {
                float t = (timer - finisherImpactFrame - 26) / 20f;
                float oA = MathF.Sin(t * MathF.PI) * 0.42f;
                float oS = MathHelper.Lerp(0.9f, 0.18f, EaseOutCubic(t)) * SizeMul;
                Color oc = Color.Lerp(new Color(210, 70, 130), new Color(70, 24, 66), t);
                sb.Draw(orb, ImpactWorldPos - Main.screenPosition, null, oc * oA
                    , t * 2.4f, orb.Size() * 0.5f, oS, SpriteEffects.None, 0);
            }

            sb.End();
        }

        /// <summary>终结爆点全 layer：星爆核心/放射尖刺/十字闪/扩散环/撕裂形/速度线</summary>
        private void DrawImpactBurst(SpriteBatch sb) {
            float bt = MathHelper.Clamp(timer - finisherImpactFrame, 0f, BurstFadeFrames);
            float bp = bt / BurstFadeFrames;
            if (bp >= 1f) {
                return;
            }

            Vector2 impact = ImpactWorldPos - Main.screenPosition;
            Vector2 aimDir = AimDir;
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            float seedRot = Projectile.whoAmI * 1.37f;

            //白热星爆核心：前3帧过曝，随后急剧收缩
            if (DemoAssets.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f);
                float coreS = (1.0f + easeOut * 0.75f) * SizeMul;
                sb.Draw(flare, impact, null, Color.White * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, new Color(255, 120, 80) * (coreA * 0.55f), -seedRot * 0.6f
                    , flare.Size() * 0.5f, coreS * 1.3f, SpriteEffects.None, 0);
            }

            //放射尖刺
            if (DemoAssets.RayBurst01?.Value is Texture2D rays) {
                float rayA = MathF.Pow(inv, 1.8f);
                float rayS = (1.25f + easeOut * 1.2f) * SizeMul;
                sb.Draw(rays, impact, null, new Color(255, 190, 160) * rayA, seedRot * 0.4f
                    , rays.Size() * 0.5f, rayS, SpriteEffects.None, 0);
            }

            //十字长闪沿瞄准方向
            if (DemoAssets.RayCross01?.Value is Texture2D cross) {
                float cA = MathF.Pow(inv, 2.4f);
                sb.Draw(cross, impact, null, new Color(255, 230, 215) * cA, AimAngle
                    , cross.Size() * 0.5f, new Vector2(2.5f, 1.15f) * easeOut * SizeMul, SpriteEffects.None, 0);
            }

            //扩散环
            if (DemoAssets.Ring01?.Value is Texture2D ring) {
                float ringS = (0.4f + easeOut * 2.2f) * SizeMul;
                float ringA = MathF.Pow(inv, 2.5f) * 0.6f;
                sb.Draw(ring, impact, null, new Color(255, 90, 60) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            //手绘撕裂形：沿瞄准方向一大一小，短命
            if (bt < 9f && DemoAssets.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 9f, 1.8f) * 0.85f;
                sb.Draw(tear, impact, null, new Color(255, 150, 120) * tA, AimAngle
                    , tear.Size() * 0.5f, (1.5f + easeOut * 0.55f) * SizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, new Color(255, 60, 40) * (tA * 0.75f), AimAngle + 0.35f * Flip
                    , tear.Size() * 0.5f, (1.0f + easeOut * 0.4f) * SizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

            //锯齿冲击形垫底
            if (bt < 7f && DemoAssets.HitJagged01?.Value is Texture2D jag) {
                float jA = MathF.Pow(1f - bt / 7f, 2f) * 0.5f;
                sb.Draw(jag, impact, null, new Color(255, 80, 55) * jA, AimAngle + MathHelper.Pi
                    , jag.Size() * 0.5f, (1.8f + easeOut * 0.6f) * SizeMul, SpriteEffects.None, 0);
            }

            //速度线：随机截条从冲击点向后扫出
            if (DemoAssets.SpeedLines01?.Value is Texture2D lines) {
                EnsureSpeedLineRects();
                float lA = MathF.Pow(inv, 1.6f) * 0.5f;
                for (int i = 0; i < speedLineRects.Length; i++) {
                    Rectangle src = speedLineRects[i];
                    float off = speedLineOffsets[i];
                    Vector2 pos = impact - aimDir * (40f + off * 70f + easeOut * 40f) * SizeMul
                        + aimDir.RotatedBy(MathHelper.PiOver2) * (off - 0.5f) * 110f * SizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 170, 140) * lA, AimAngle
                        , src.Size() * 0.5f, new Vector2(0.40f + easeOut * 0.30f, 0.42f) * SizeMul
                        , SpriteEffects.None, 0);
                }
            }
        }

        private void EnsureSpeedLineRects() {
            if (speedLineRects != null) {
                return;
            }
            speedLineRects = new Rectangle[3];
            speedLineOffsets = new float[3];
            for (int i = 0; i < 3; i++) {
                speedLineRects[i] = new Rectangle(0, Main.rand.Next(0, 1024 - 96), 1024, 96);
                speedLineOffsets[i] = Main.rand.NextFloat();
            }
        }

        /// <summary>负片收缩：爆闪第2~8帧，暗核压在加色星爆之上，只留红边<br/>
        /// 注意：AlphaBlend 压暗必须用 alpha 通道承载形状的贴图（SmokeSheet01），
        /// 黑底不透明的亮度型贴图会把整个 quad 糊成暗色方框</summary>
        private void DrawCollapseCore() {
            float bt = timer - finisherImpactFrame;
            if (!impactFired || bt < 2f || bt > 8f) {
                return;
            }
            Texture2D cloud = DemoAssets.SmokeSheet01?.Value;
            if (cloud == null) {
                return;
            }

            float t = (bt - 2f) / 6f;   //0..1
            //512px 帧：峰值 ~0.36 倍 ≈ 185px 暗核，收缩至 ~60px
            float coreS = MathHelper.Lerp(0.36f, 0.12f, t * t) * SizeMul;
            float coreA = MathF.Sin(t * MathF.PI) * 0.78f;
            Rectangle frame = new((Projectile.whoAmI % 2) * 512, (Projectile.whoAmI / 2 % 2) * 512, 512, 512);

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(cloud, ImpactWorldPos - Main.screenPosition, frame
                , new Color(16, 4, 9) * coreA, Projectile.whoAmI * 1.37f
                , frame.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
