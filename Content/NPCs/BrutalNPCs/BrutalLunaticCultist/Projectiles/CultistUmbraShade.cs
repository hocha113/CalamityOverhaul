using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 蚀祭主控:暗影盘滑过主星(食相自身即预告),全食后先给宽限期,再放冕矛,本影楔=唯一安全走廊<br/>
    /// ai[0]=宿主npc ai[1]=本影基角(出生锁定,朝向当时的目标=先给玩家安全区) ai[2]=漂移率(符号即方向)<br/>
    /// 节奏:滑入 62f(楔形随食相渐显)→宽限 108f(无冕矛,楔慢漂 0.4x)→齐射期 512f(楔加速到 1.9x,12槽/20f 起步收紧到 16槽/14f)→复圆,全程 760f<br/>
    /// 分相变体:星旋楔宽 ×0.8;星云本影漂至中点拍反向折返(折返窗匀减速过零);日耀本影旋速 ×0.7(出手端定率)+齐射窗天降散点火焰流星;<br/>
    /// 月明全食段 +MoonExtend 帧(齐射窗同步拉长,蚀祭态另在首尾各压一轮追星矢);星尘由蚀祭态召幻影龙(主星公转冻结见星球)<br/>
    /// 公平阀:GapHalf 声明角缺口(分相同参),冕矛发射循环与本影楔绘制同读;本影在漂,故跳槽按冕矛伤害窗前瞻扫过区间,<br/>
    /// 星云折返拍落在窗内时区间并入折返极值;命中端再对楔内玩家豁免(<see cref="PointInUmbra"/>)=黑区对冕矛绝对安全<br/>
    /// (幻影龙与火焰流星是独立威胁层,各带自身预告与侧移可避声明);宽限期步行可跟,加速期贴近星球弧速更慢
    /// </summary>
    internal class CultistUmbraShade : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Lifetime = 760;
        private const int SlideInEnd = 62;
        /// <summary>全食后宽限帧数:不放冕矛,给玩家看清并走进安全区</summary>
        private const int GraceFrames = 108;
        private const int TotalityEnd = 694;
        private const int SlideOutEnd = 738;
        /// <summary>月明相全食延长帧数:滑入/宽限/复圆不变,只拉长齐射窗(220→24:用户令月明全程砍 20%,980→784)</summary>
        internal const int MoonExtend = 24;
        /// <summary>月明相末轮追星矢齐射龄(蚀祭态读,"快结束时"的第二轮连射拍)</summary>
        internal const int MoonLateVolleyAge = TotalityEnd + MoonExtend - 90;
        /// <summary>声明缺口半角基准(rad):本影楔可见宽度与冕矛跳角同源;分相实值走 <see cref="GapHalf"/></summary>
        internal const float GapHalfAngle = 0.34f;
        /// <summary>星旋相楔宽系数:宽度减 20%(判定/绘制/跳槽同参收窄;
        /// 0.7 时首相楔仅 ±13.6°,最窄的楔压在玩家还没学会跟楔的第一阶段,判过窄勿回调)</summary>
        private const float VortexGapMul = 0.8f;
        /// <summary>冕矛节奏:起步 12 槽/20 帧,中段起收紧 16 槽/14 帧(浪形升级)</summary>
        private const int VolleyIntervalEarly = 20;
        private const int VolleyIntervalLate = 14;
        private const int VolleySlotsEarly = 12;
        private const int VolleySlotsLate = 16;
        /// <summary>日耀相流星拍距:齐射窗内每拍一颗,锚玩家横向散点(2颗/14f→1颗/12f,密度 -42%≈用户令 -40%)</summary>
        private const int MeteorGap = 12;
        /// <summary>日耀相流星横向散布半宽(px)</summary>
        private const float MeteorSpreadX = 820f;
        /// <summary>宽限结束帧(冕矛自此解禁)</summary>
        private const float GraceEnd = SlideInEnd + GraceFrames;

        private int OwnerWho => (int)Projectile.ai[0];
        private float UmbraBase => Projectile.ai[1];
        private float DriftRate => Projectile.ai[2];

        /// <summary>宿主阶段(镜像 ai[0]);首帧快照后恒定——转阶段 Phase++ 先于清场 8 帧,快照防分相参数在此窗内漂移</summary>
        private int OwnerPhase => phaseCache >= 0 ? phaseCache : LiveOwnerPhase;

        private int LiveOwnerPhase {
            get {
                NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
                return owner != null && owner.active ? (int)owner.ai[0] : 0;
            }
        }

        /// <summary>相位快照(-1=未锁),各端首帧写入</summary>
        private int phaseCache = -1;
        /// <summary>月明相全食延长量(其余相 0)</summary>
        private int Ext => OwnerPhase >= 4 ? MoonExtend : 0;
        /// <summary>本次施放全程帧数(月明相延长)</summary>
        private float TotalLife => Lifetime + Ext;
        private float TotalityEndF => TotalityEnd + Ext;
        private float SlideOutEndF => SlideOutEnd + Ext;
        /// <summary>齐射末帧</summary>
        private float VolleyEndF => TotalityEndF - 12f;
        /// <summary>节奏换挡帧:齐射窗中点收紧;星云相同拍本影折返</summary>
        private float EscalateAgeF => (GraceEnd + VolleyEndF) * 0.5f;
        /// <summary>本相缺口半角:星旋相收窄 30%,判定/绘制/跳槽同参</summary>
        internal float GapHalf => OwnerPhase == 0 ? GapHalfAngle * VortexGapMul : GapHalfAngle;
        private float Age => TotalLife - Projectile.timeLeft;

        /// <summary>月明延时已落账(各端首帧按相位拉长 timeLeft,Age 以延长后全程起算)</summary>
        private bool lifeExtended;

        /// <summary>暗影盘滑入方向(屏幕系固定)</summary>
        private static readonly Vector2 SlideDir = new Vector2(1f, 0.28f).SafeNormalize(Vector2.UnitX);

        private int planetCache = -1;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
        }

        /// <summary>找主星(常驻非幻象),缓存失效重扫</summary>
        private Projectile FindPlanet() {
            if (planetCache >= 0 && planetCache < Main.maxProjectiles) {
                Projectile cached = Main.projectile[planetCache];
                if (cached.active && cached.type == ModContent.ProjectileType<CultistPlanetProj>()
                    && (int)cached.ai[1] == OwnerWho) {
                    return cached;
                }
            }
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == OwnerWho
                    && (int)proj.ai[2] % 10 == 1 && (int)proj.ai[2] / 10 == 0) {
                    planetCache = proj.whoAmI;
                    return proj;
                }
            }
            return null;
        }

        /// <summary>食相进度 0~1(1=全食);滑出段回落</summary>
        internal float Coverage {
            get {
                float age = Age;
                if (age < SlideInEnd) {
                    float t = age / SlideInEnd;
                    return t * t;
                }
                if (age < TotalityEndF) {
                    return 1f;
                }
                if (age < SlideOutEndF) {
                    float t = (age - TotalityEndF) / (SlideOutEndF - TotalityEndF);
                    return 1f - t * t;
                }
                return 0f;
            }
        }

        /// <summary>本影角:宽限期 0.4x 慢漂,冕矛解禁后 90 帧线性加速到 1.9x;解析积分保持 Age 纯函数(各端零同步)</summary>
        internal float UmbraAngle => UmbraAngleAt(Age);

        /// <summary>漂移速率档:宽限慢漂/满速巡航/加速斜坡长(漂移时间的每帧增量,乘 DriftRate 才是角速度);<br/>
        /// 满速追楔所需切速=档位×0.0045rad/f×半径,贴星半径 600 处≈5px/f 带翅可跟;
        /// 2.55 档在 700~900px 半径要求 8~10px/f 持续圆周飞行,判无解(2026-08-31),勿回调</summary>
        private const float DriftSlow = 0.4f;
        private const float DriftFast = 1.9f;
        private const float DriftRampLen = 90f;
        /// <summary>星云折返缓冲半窗(帧):折返点两侧匀减速过零再反向,不再瞬间调头;窗必须落在满速巡航段内(见 UmbraAngleAt)</summary>
        private const float FlipEaseHalf = 45f;

        /// <summary>漂移时间积分(Age 纯函数):宽限慢漂→加速斜坡→满速巡航的闭式解</summary>
        private static float DriftTimeAt(float age) {
            if (age <= GraceEnd) {
                return age * DriftSlow;
            }
            float t = age - GraceEnd;
            return GraceEnd * DriftSlow + (t <= DriftRampLen
                ? DriftSlow * t + (DriftFast - DriftSlow) * t * t / (2f * DriftRampLen)
                : (DriftSlow + DriftFast) * DriftRampLen * 0.5f + DriftFast * (t - DriftRampLen));
        }

        /// <summary>
        /// 任意时刻的本影角:纯函数可前瞻,冕矛跳槽按伤害窗查未来扫过区间;<br/>
        /// 星云相漂至中点拍反向,折返窗内匀减速过零(抛物线顶,顶点恰在 flip=极值采样点不变);<br/>
        /// 缓冲窗恒落在满速巡航段(flip≥Grace+Ramp+窗),窗内 D 为线性段,故窗外两支与瞬间折返的镜像积分逐点一致
        /// </summary>
        internal float UmbraAngleAt(float age) {
            float driftTime;
            if (OwnerPhase == 1) {
                float flip = EscalateAgeF;
                if (age <= flip - FlipEaseHalf) {
                    driftTime = DriftTimeAt(age);
                }
                else if (age < flip + FlipEaseHalf) {
                    //折返缓冲:以 -DriftFast/半窗 匀减速,过零点恰在 flip
                    float tau = age - (flip - FlipEaseHalf);
                    driftTime = DriftTimeAt(flip - FlipEaseHalf)
                        + DriftFast * tau - DriftFast / (2f * FlipEaseHalf) * tau * tau;
                }
                else {
                    driftTime = DriftTimeAt(flip - FlipEaseHalf) + DriftTimeAt(flip + FlipEaseHalf)
                        - DriftTimeAt(age);
                }
            }
            else {
                driftTime = DriftTimeAt(age);
            }
            return UmbraBase + DriftRate * driftTime;
        }

        /// <summary>
        /// 点是否处于本影安全楔内:与冕矛跳槽同读 GapHalf(缺口即所见,星旋相同参收窄);<br/>
        /// 供冕矛命中豁免——本影持续漂移,任何出生时刻的静态跳槽都盖不住全部时序,黑区安全由命中端兜底
        /// </summary>
        internal static bool PointInUmbra(int planetWho, Vector2 point) {
            int type = ModContent.ProjectileType<CultistUmbraShade>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type || proj.ModProjectile is not CultistUmbraShade shade) {
                    continue;
                }
                Projectile planet = shade.FindPlanet();
                if (planet == null || planet.whoAmI != planetWho) {
                    continue;
                }
                float pointAngle = (point - planet.Center).ToRotation();
                if (Math.Abs(MathHelper.WrapAngle(pointAngle - shade.UmbraAngle)) < shade.GapHalf) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>该宿主是否有蚀祭本影在场:星尘主星借此冻结公转(本影楔锚星心,星不动楔才立得住)</summary>
        internal static bool ShadeActiveFor(int ownerWho) {
            int type = ModContent.ProjectileType<CultistUmbraShade>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            Projectile planet = FindPlanet();
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss || planet == null) {
                Projectile.Kill();
                return;
            }
            //首帧落账:锁相位快照,再按相位拉长 timeLeft(Age 以延长后全程起算,各端本地一致)
            if (!lifeExtended) {
                lifeExtended = true;
                phaseCache = LiveOwnerPhase;
                Projectile.timeLeft += Ext;
            }
            float age = Age;
            Projectile.Center = planet.Center;
            float coverage = Coverage;

            //全食起拍(各端本地)
            if ((int)age == SlideInEnd) {
                CultistScreenFX.PushFlash(0.35f);
                CultistMotion.Shake(planet.Center, 6f, 14);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 1.1f, Pitch = -0.7f }, planet.Center);
                }
            }
            //宽限结束拍:冕矛解禁的宣告(缘线脉冲此前 40 帧已开始爬升)
            if ((int)age == (int)GraceEnd) {
                CultistScreenFX.PushFlash(0.30f);
                CultistMotion.Shake(planet.Center, 5f, 12);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 1f, Pitch = -0.45f }, planet.Center);
                }
            }
            //星云相折返拍:本影调头的宣告(与冕矛节奏换挡同拍)
            if (OwnerPhase == 1 && (int)age == (int)EscalateAgeF) {
                CultistScreenFX.PushFlash(0.22f);
                CultistMotion.Shake(planet.Center, 4f, 10);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.75f, Pitch = 0.1f }, planet.Center);
                }
            }
            //复圆拍:钻石环闪
            if ((int)age == (int)TotalityEndF) {
                CultistScreenFX.PushFlash(0.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.4f }, planet.Center);
                }
            }

            //全食压场(本地):天黑+去饱和
            if (coverage > 0.6f && !VaultUtils.isServer) {
                CultistScreenFX.SetVeil(0.45f * coverage, planet.Center, new Color(30, 40, 46), 1500f);
                CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.22f * coverage);
            }

            //冕矛志愿(权威端):宽限期不出手;起步 12 槽/20f,中段收紧 16 槽/14f
            //跳槽按伤害窗前瞻:本影在漂,矛又有 18f 预警延迟,静态查出生角会让"合法出界"的矛
            //在开火时落进已漂来的安全楔——缺口区间=[开火起~开火止]本影扫过角±GapHalf(略放 2f 余量);
            //星云折返拍落在伤害窗内时,扫过角在折返点取极值,区间并入它再取中
            if (!VaultUtils.isClient && age > GraceEnd && age < VolleyEndF) {
                bool late = age >= EscalateAgeF;
                int interval = late ? VolleyIntervalLate : VolleyIntervalEarly;
                if ((int)age % interval == 0) {
                    int slots = late ? VolleySlotsLate : VolleySlotsEarly;
                    int volley = (int)age / interval;
                    float baseRot = volley * 0.26f;
                    int palette = (int)planet.ai[0];
                    float fireStartAge = age + CultistCoronaLance.WarnFrames - 2f;
                    float fireEndAge = age + CultistCoronaLance.WarnFrames + CultistCoronaLance.FireFrames + 2f;
                    float lo = Math.Min(UmbraAngleAt(fireStartAge), UmbraAngleAt(fireEndAge));
                    float hi = Math.Max(UmbraAngleAt(fireStartAge), UmbraAngleAt(fireEndAge));
                    float flipAge = EscalateAgeF;
                    if (OwnerPhase == 1 && flipAge > fireStartAge && flipAge < fireEndAge) {
                        float atFlip = UmbraAngleAt(flipAge);
                        lo = Math.Min(lo, atFlip);
                        hi = Math.Max(hi, atFlip);
                    }
                    float sweepMid = (lo + hi) * 0.5f;
                    float sweepHalf = (hi - lo) * 0.5f + GapHalf;
                    for (int i = 0; i < slots; i++) {
                        float angle = baseRot + i * MathHelper.TwoPi / slots;
                        float delta = MathHelper.WrapAngle(angle - sweepMid);
                        if (Math.Abs(delta) < sweepHalf) {
                            continue;
                        }
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), planet.Center,
                            angle.ToRotationVector2() * 0.01f, ModContent.ProjectileType<CultistCoronaLance>(),
                            42, 0f, Main.myPlayer, angle, planet.whoAmI, palette);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.5f, Pitch = -0.2f }, planet.Center);
                    }
                }
            }

            //日耀相天火(权威端):齐射窗内锚玩家横向散点,持续降下快速火焰流星
            //公平阀:每星自带 26f+ 预告柱与恒窄判定(半宽 20),散点单落不成排,横移一步即避;流星不追踪
            if (!VaultUtils.isClient && OwnerPhase == 3 && age > GraceEnd && age < VolleyEndF
                && (int)age % MeteorGap == 0) {
                Player target = owner.target >= 0 && owner.target < 255 ? Main.player[owner.target] : null;
                if (target.Alives()) {
                    Vector2 pos = new(target.Center.X + Main.rand.NextFloat(-MeteorSpreadX, MeteorSpreadX),
                        target.Center.Y - 780f + Main.rand.NextFloat(-64f, 64f));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<CultistFallingStar>(), 40, 0f, Main.myPlayer,
                        OwnerWho, CultistPlanetProj.KindSolar, Main.rand.NextFloat());
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Projectile planet = FindPlanet();
            if (planet == null) {
                return false;
            }
            float coverage = Coverage;
            if (coverage <= 0.01f) {
                return false;
            }
            float age = Age;

            float visR = planet.Hitbox.Width * 0.5f;
            if (planet.ModProjectile is CultistPlanetProj planetProj) {
                visR = planetProj.VisRadius * planet.scale;
            }
            int palette = (int)planet.ai[0];
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.4f);

            SpriteBatch sb = Main.spriteBatch;

            //本影楔:缓动渐显(骤然满强被判生硬),全食落拍前后浮现完毕仍远早于冕矛解禁;复圆段随食相回落;略窄于判定缺口=对玩家宽容
            float wedgeT = MathHelper.Clamp(age / 78f, 0f, 1f);
            float wedgeStrength = wedgeT * wedgeT * (3f - 2f * wedgeT);
            if (age >= TotalityEndF) {
                wedgeStrength *= MathHelper.Clamp(coverage / 0.72f, 0f, 1f);
            }
            if (wedgeStrength > 0.01f) {
                //缘线脉冲:宽限最后 40 帧爬升,齐射期保持=冕矛在场的危险宣告
                float lancePulse = age >= VolleyEndF ? 0f
                    : MathHelper.Clamp((age - (GraceEnd - 40f)) / 40f, 0f, 1f);
                float umbra = UmbraAngle;
                Vector2 dir = umbra.ToRotationVector2();
                //采样点须密:梯形条带的仿射 UV 插值会把缘线等值线在段缝处折出台阶,10 点时肉眼可见锯齿
                const int WedgePts = 40;
                const float WedgeLen = 1750f;
                float tanHalf = (float)Math.Tan(GapHalf * 0.94f);
                Vector2[] pts = new Vector2[WedgePts];
                float[] widths = new float[WedgePts];
                float[] alphas = new float[WedgePts];
                for (int i = 0; i < WedgePts; i++) {
                    float t = i / (float)(WedgePts - 1);
                    float dist = visR * 0.8f + t * WedgeLen;
                    pts[i] = planet.Center + dir * dist - Main.screenPosition;
                    widths[i] = dist * tanHalf;
                    alphas[i] = 1f;
                }
                sb.End();
                CultistOrreryRenderer.DrawTechniqueStrip("TechUmbra", pts, widths, alphas,
                    new Color(6, 10, 18), mid, bright, wedgeStrength, 0f, lancePulse, 0.51f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //暗影盘:滑过主星,盘径契约 0.42 与行星同
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return false;
            }
            float slideOffset;
            if (age < SlideInEnd) {
                float t = age / SlideInEnd;
                slideOffset = MathHelper.Lerp(visR * 2.6f, 0f, 1f - (1f - t) * (1f - t));
            }
            else if (age < TotalityEndF) {
                slideOffset = 0f;
            }
            else {
                float t = MathHelper.Clamp((age - TotalityEndF) / (SlideOutEndF - TotalityEndF), 0f, 1f);
                slideOffset = -visR * 2.6f * t * t;
            }
            Vector2 shadePos = planet.Center + SlideDir * slideOffset;
            float shadeR = visR * 0.94f;

            fx.CurrentTechnique = fx.Techniques["TechShade"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.10f, 0.10f, 0.13f));
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.96f, 0.85f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp((coverage - 0.85f) / 0.15f, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(0.41f);
            fx.Parameters["uProgress"]?.SetValue(1f);
            fx.Parameters["uDash"]?.SetValue(0f);
            fx.Parameters["uArm"]?.SetValue(0f);
            fx.Parameters["uEnv"]?.SetValue(0f);

            float quadSize = shadeR / 0.42f * 2f;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, shadePos - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
