using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>水花的命中面类别:决定扇形、溅裙与随行</summary>
    internal enum SplashKind : byte
    {
        /// <summary>贴地形(地/墙/顶):指根沿表面铺开、朝法线半空发射,落回表面即消失,有溅裙</summary>
        Tile,
        /// <summary>沾敌:法线取来势反向,根随宿主走,无落面</summary>
        Npc,
        /// <summary>空中散尽:全向小爆,无溅裙无落面</summary>
        Air
    }

    /// <summary>水花四色:体/缘/芯/湿光,墨、鬼青、浓血三族与符染色都走这一套</summary>
    internal readonly struct SplashPalette
    {
        public readonly Color Body;
        public readonly Color Deep;
        public readonly Color Core;
        public readonly Color Sheen;

        public SplashPalette(Color body, Color deep, Color core, Color sheen) {
            Body = body;
            Deep = deep;
            Core = core;
            Sheen = sheen;
        }

        public static SplashPalette Ink => new(KikasaInk.InkBody, KikasaInk.InkDeep, KikasaInk.BloodCore, KikasaInk.WetSheen);
        public static SplashPalette Ghost => new(KikasaInk.GhostBody, KikasaInk.GhostDeep, KikasaInk.GhostCore, KikasaInk.GhostCore);
        public static SplashPalette Blood => new(KikasaInk.BloodBody, KikasaInk.BloodDeep, KikasaInk.BloodBright, KikasaInk.BloodSheen);
    }

    /// <summary>
    /// 墨水花:墨滴命中一瞬爆发、十几帧内崩解干净的溅射演出(2026-09-09 取代 220f 落地渍斑与 150f 沾敌附着渍)。
    /// 二版改成拉格朗日弹道模型(一版直线液指+三段包络被判"像帧图动画"):每指是一根液体射流,
    /// 流体元自铺开薄片的边缘以初速沿方向喷出、只受重力,着色器(<see cref="EffectLoader.KikasaInkSplash"/> TechSplash)
    /// 按同一套公式逐像素反解流体元龄画出弯下去的抛物线珠链;本类持有指几何(方位角落位/发射方向/初速/根半宽/
    /// 喷出时长),用**同一公式**算指尖与珠的位置速度:爆发帧一蓬扇形飞沫沿指向飞、之后几帧从指尖甩珠(继承流体速度,
    /// 珠接着同一条抛物线飞)、寿命末四帧把整条珠链交接给 <see cref="PRT_KikasaInkSpray"/> 再让着色器清残——
    /// 着色器与粒子是同一股液体的两半,不是两套东西。贴面溅裙一撮低角度小珠、一口墨雾;音效帧预算、屏震只给手动高动能滴。
    /// 纯客户端列表(环形上限),无网络;各端在自己的 OnKill 里各起一朵,近似一致即可。
    /// 由 <see cref="KikasaRainSystem"/> 推进、<see cref="KikasaRainRender"/> 在墨/血体之后绘制
    /// </summary>
    internal static class KikasaInkSplashFX
    {
        /// <summary>一朵水花的寿命(帧):快,不许拖;末四帧交接粒子、末三帧着色器清残</summary>
        public const int LifeFrames = 16;
        private const int Cap = 24;
        private const int FingerSlots = 7;
        private const int HandoffFrame = LifeFrames - 4;

        /// <summary>
        /// 流体重力 px/f²:墨是重液体,取得比粒子默认(0.36)沉,十六帧内抛物线走完整段(升、悬、落);
        /// 所有自水花脱手的粒子都显式带这个值,珠接着同一条抛物线飞
        /// </summary>
        private const float Gravity = 0.55f;

        /// <summary>珠链波长(根半宽倍数),与 KikasaInkSplash.fx 的 kB.x=2π/6.4 同源</summary>
        private const float BeadWavelengthR0 = 6.4f;

        /// <summary>画布护栏:横向两侧各 5%、纵向 4% 归零,几何让位按此除</summary>
        private const float GuardX = 0.90f;
        private const float GuardY = 0.92f;

        private class Splash
        {
            public Vector2 Pos;
            /// <summary>撞击法线(单位,离面)</summary>
            public Vector2 Normal;
            /// <summary>quad 空间 +x 在世界里的方向</summary>
            public Vector2 Tangent;
            public float Skew;
            public float Ke;
            public float ScaleMul;
            public float Seed;
            public int Age;
            public SplashKind Kind;
            public SplashPalette Palette;
            /// <summary>x,y=发射方向(quad 空间) z=初速 px/f w=根半宽 px</summary>
            public Vector4[] FingerA = new Vector4[FingerSlots];
            /// <summary>x=相位 y=喷出时长 te(帧) z=甩尾幅 px w=指根落位系数 c</summary>
            public Vector4[] FingerB = new Vector4[FingerSlots];
            public int FingerN;
            /// <summary>薄片最大半宽 px,指根随它外扩</summary>
            public float RimMax;
            //quad 让位(px)与根在 quad 内的 uv
            public float QuadW;
            public float QuadH;
            public float RootU;
            public float RootV;
            //沾敌随行
            public int NpcWho = -1;
            public int NpcType;
            public Vector2 Offset;
            //湖线(0=无湖):飞沫落回水线即结束
            public float LakeY;
        }

        private static readonly List<Splash> list = [];

        /// <summary>在场水花数,渲染层据此决定要不要开批</summary>
        public static int Count => list.Count;

        //音效与屏震的帧预算:齐掷波七八滴同帧落地只响头几声、只震一下
        private static uint soundStamp;
        private static int soundLeft;
        private static uint lastShakeFrame;

        private static bool TakeSoundBudget(int perFrame = 3) {
            if (soundStamp != Main.GameUpdateCount) {
                soundStamp = Main.GameUpdateCount;
                soundLeft = perFrame;
            }
            if (soundLeft <= 0) {
                return false;
            }
            soundLeft--;
            return true;
        }

        //==================== 起花 ====================

        /// <summary>
        /// 起一朵水花。pos=撞击点(贴地形请先吸附到表面),normal=离面法线,impactVel=撞击速度,
        /// ke=动能 0~1,scale=滴体倍率,host=沾敌宿主(随行),manual=手动指挥的滴(归属端本机才给屏震),
        /// quiet=不出声(穿墙入口的小花)
        /// </summary>
        public static void Burst(Vector2 pos, Vector2 normal, Vector2 impactVel, float ke, float scale,
            SplashPalette palette, SplashKind kind, NPC host = null, bool manual = false, bool quiet = false) {
            if (Main.dedServ) {
                return;
            }
            normal = normal.SafeNormalize(-Vector2.UnitY);
            ke = MathHelper.Clamp(ke, 0f, 1f);
            scale = MathHelper.Clamp(scale, 0.5f, 1.6f);
            Vector2 tangent = new(-normal.Y, normal.X);
            float speed = impactVel.Length();
            float skew = speed > 0.1f ? MathHelper.Clamp(Vector2.Dot(impactVel, tangent) / speed, -1f, 1f) : 0f;

            if (list.Count >= Cap) {
                list.RemoveAt(0);
            }
            Splash s = new() {
                Pos = pos,
                Normal = normal,
                Tangent = tangent,
                Skew = skew,
                Ke = ke,
                ScaleMul = scale,
                Seed = Main.rand.NextFloat(8f),
                Kind = kind,
                Palette = palette,
            };
            if (host != null) {
                s.NpcWho = host.whoAmI;
                s.NpcType = host.type;
                s.Offset = pos - host.Center;
            }
            s.LakeY = ResolveLakeY(pos);
            SolveFingers(s);
            SolveQuad(s);
            list.Add(s);

            SpawnBurstParticles(s, impactVel);
            if (!quiet) {
                PlayBeat(s, manual);
            }
        }

        /// <summary>观看域的水线:花根在水面之上才给,水下起的花不认水线(飞沫会在第一帧误判落水)</summary>
        private static float ResolveLakeY(Vector2 pos) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.AnyActive) {
                return 0f;
            }
            return pos.Y < kdp.LakeWorldY - 4f ? kdp.LakeWorldY : 0f;
        }

        /// <summary>
        /// 解指几何:指根落在薄片边缘这个 3D 圆环上,按方位角 az 等分再 hash 抖动;侧视投影下根位 x=cos(az)·rimR,
        /// 发射方向=(cos(az)·cosE, sinE) 归一(E=离面仰角 31°~66°,hash),两翼指外倾、正前正后的指近直立;
        /// 表观初速按投影长度折算、偏斜侧更快;根半宽/喷出时长/甩尾幅各自 hash。空中散尽改全向。
        /// 与着色器共用 FingerA/FingerB 两组 float4,任何一端改公式另一端必须同改
        /// </summary>
        private static void SolveFingers(Splash s) {
            bool air = s.Kind == SplashKind.Air;
            int n = air ? 5 : 5 + (int)MathF.Round(2f * s.Ke);
            n = Math.Clamp(n, 2, FingerSlots);
            float sizeMul = MathHelper.Clamp(s.ScaleMul, 0.8f, 1.4f);
            float vBase = (7f + 5f * s.Ke) * sizeMul;
            s.RimMax = (9f + 8f * s.Ke) * sizeMul;
            int seed = (int)(s.Seed * 977f);

            for (int j = 0; j < n; j++) {
                Vector2 dir;
                float c;
                float vProj;
                if (air) {
                    float th = MathHelper.TwoPi * j / n + (KikasaInk.Hash(seed, j) - 0.5f) * 0.5f;
                    dir = th.ToRotationVector2();
                    c = dir.X * 0.5f;
                    vProj = vBase * (0.7f + 0.4f * KikasaInk.Hash(seed, j + 14)) * 0.8f;
                }
                else {
                    float az = MathHelper.TwoPi * j / n + (KikasaInk.Hash(seed, j) - 0.5f) * 0.7f;
                    c = MathF.Cos(az);
                    //离面仰角 43°~77°:要往上冲的力量感,不是贴地扫
                    float elev = 0.75f + 0.6f * KikasaInk.Hash(seed, j + 7);
                    Vector2 proj = new(c * MathF.Cos(elev) + s.Skew * 0.35f, MathF.Sin(elev));
                    float pl = MathF.Max(proj.Length(), 0.2f);
                    dir = proj / pl;
                    //初速散得开(0.65~1.3):一样高的一排头读成手掌,高低错落才是甩出来的
                    vProj = vBase * (0.65f + 0.65f * KikasaInk.Hash(seed, j + 14)) * pl
                        * (1f + 0.25f * s.Skew * dir.X);
                }
                float r0 = (2.8f + 1.8f * KikasaInk.Hash(seed, j + 21)) * sizeMul * (0.85f + 0.3f * s.Ke);
                float phase = KikasaInk.Hash(seed, j + 28) * MathHelper.TwoPi;
                float te = 4.5f + 2.5f * KikasaInk.Hash(seed, j + 35);
                float whip = (0.25f + 0.35f * KikasaInk.Hash(seed, j + 42)) * r0;
                s.FingerA[j] = new Vector4(dir.X, dir.Y, MathF.Max(vProj, 1f), r0);
                s.FingerB[j] = new Vector4(phase, te, whip, c);
            }
            for (int i = n; i < FingerSlots; i++) {
                s.FingerA[i] = Vector4.Zero;
                s.FingerB[i] = Vector4.Zero;
            }
            s.FingerN = n;
        }

        /// <summary>
        /// quad 让位:沿每指整段寿命的轨迹取包围盒(含液滴头、甩尾余量),并入薄片与撞击芯;
        /// 贴面时面下一律不画(落地即消失),下缘只留 4px。护栏让位后反推根在 quad 内的 uv
        /// </summary>
        private static void SolveQuad(Splash s) {
            float xMin = -(s.RimMax + 6f);
            float xMax = s.RimMax + 6f;
            float yMin = -4f;
            float yMax = 3f + 3f * s.Ke + 8f;
            for (int j = 0; j < s.FingerN; j++) {
                float pad = s.FingerA[j].W * 1.35f + s.FingerB[j].Z + 3f;
                for (int k = 0; k <= 4; k++) {
                    float T = LifeFrames * k / 4f;
                    ElementAt(s, j, T, T, out Vector2 e, out _);
                    xMin = MathF.Min(xMin, e.X - pad);
                    xMax = MathF.Max(xMax, e.X + pad);
                    yMin = MathF.Min(yMin, e.Y - pad);
                    yMax = MathF.Max(yMax, e.Y + pad);
                }
            }
            if (s.Kind == SplashKind.Tile) {
                yMin = -4f;
            }
            float contentW = xMax - xMin;
            float contentH = yMax - yMin;
            s.QuadW = contentW / GuardX;
            s.QuadH = contentH / GuardY;
            xMin -= (s.QuadW - contentW) * 0.5f;
            yMax += (s.QuadH - contentH) * 0.5f;
            s.RootU = -xMin / s.QuadW;
            s.RootV = yMax / s.QuadH;
        }

        //==================== 与着色器同源的流体元公式 ====================

        /// <summary>喷出元速度:晚喷出的慢,v(T)=v0·(0.65+0.35·sat(T/te))</summary>
        private static float ElemSpeed(float v0, float T, float te)
            => v0 * (0.65f + 0.35f * MathHelper.Clamp(T / MathF.Max(te, 1f), 0f, 1f));

        /// <summary>薄片此刻半宽:快起后停</summary>
        private static float RimR(Splash s, float t) => s.RimMax * (1f - MathF.Exp(-t / 2.2f));

        /// <summary>重力在 quad 空间(px/f²)</summary>
        private static Vector2 GravQ(Splash s) => new Vector2(s.Tangent.Y, s.Normal.Y) * Gravity;

        /// <summary>第 j 指龄为 T 的流体元在第 t 帧的位置与速度(quad 空间 px),不含甩尾</summary>
        private static void ElementAt(Splash s, int j, float t, float T, out Vector2 posQ, out Vector2 velQ) {
            Vector4 fa = s.FingerA[j];
            Vector4 fb = s.FingerB[j];
            Vector2 dir = new(fa.X, fa.Y);
            float vT = ElemSpeed(fa.Z, T, fb.Y);
            Vector2 g = GravQ(s);
            posQ = new Vector2(fb.W * RimR(s, t), 0f) + dir * (vT * T) + g * (0.5f * T * T);
            velQ = dir * vT + g * T;
        }

        private static Vector2 ToWorld(Splash s, Vector2 q) => s.Pos + s.Tangent * q.X + s.Normal * q.Y;
        private static Vector2 ToWorldDir(Splash s, Vector2 q) => s.Tangent * q.X + s.Normal * q.Y;

        //==================== 粒子编舞 ====================

        private static Color PickColor(in SplashPalette p) => Main.rand.NextBool(3) ? p.Deep : p.Body;

        /// <summary>粒子尺寸:按液体半径折算(Extra_98 泪滴可见半宽≈8.4·scale px)</summary>
        private static float SprayScale(float radiusPx) => MathHelper.Clamp(radiusPx * 0.12f, 0.24f, 0.7f);

        /// <summary>
        /// 爆发帧:沿各指发射方向散一蓬比韧带更快的细飞沫(细雾先于韧带冲出去),带一点切向来势;
        /// 贴面一撮低角度小珠当溅裙,一口墨雾;空中散尽只有半量飞沫、无溅裙
        /// </summary>
        private static void SpawnBurstParticles(Splash s, Vector2 impactVel) {
            bool air = s.Kind == SplashKind.Air;
            float ke = s.Ke;
            float sizeMul = MathHelper.Clamp(s.ScaleMul, 0.8f, 1.4f);
            Vector2 carry = s.Tangent * (Vector2.Dot(impactVel, s.Tangent) * 0.12f);

            int sprays = 4 + (int)(6f * ke);
            if (air) {
                sprays /= 2;
            }
            for (int i = 0; i < sprays; i++) {
                int j = i % s.FingerN;
                Vector4 fa = s.FingerA[j];
                Vector2 dirQ = new Vector2(fa.X, fa.Y).RotatedBy(Main.rand.NextFloat(-0.22f, 0.22f));
                float speed = fa.Z * Main.rand.NextFloat(1.05f, 1.5f);
                Vector2 at = ToWorld(s, new Vector2(s.FingerB[j].W * s.RimMax * 0.5f, 2f));
                float scale = SprayScale(fa.W * Main.rand.NextFloat(0.5f, 0.9f));
                PRTLoader.NewParticle<PRT_KikasaInkSpray>(at, ToWorldDir(s, dirQ) * speed + carry,
                    PickColor(s.Palette), scale)
                    ?.Configure(Main.rand.Next(16, 30), s.Palette.Deep, s.Palette.Sheen, s.LakeY, scale > 0.45f, true, Gravity);
            }

            if (!air) {
                //溅裙:贴面低角度弹出的小珠,两侧各半
                int skirt = 3 + (int)(2f * ke);
                for (int i = 0; i < skirt; i++) {
                    float side = i % 2 == 0 ? 1f : -1f;
                    float lift = Main.rand.NextFloat(0.1f, 0.45f);
                    Vector2 dir = s.Tangent * (side * MathF.Cos(lift)) + s.Normal * MathF.Sin(lift);
                    Vector2 at = s.Pos + s.Normal * 2f + s.Tangent * (side * Main.rand.NextFloat(0.3f, 0.9f) * s.RimMax);
                    PRTLoader.NewParticle<PRT_KikasaInkSpray>(at, dir * Main.rand.NextFloat(1.5f, 3.5f),
                        PickColor(s.Palette), Main.rand.NextFloat(0.22f, 0.34f) * sizeMul)
                        ?.Configure(Main.rand.Next(12, 22), s.Palette.Deep, s.Palette.Sheen, s.LakeY, false, false, 0.3f, 0.985f);
                }
            }

            //一口墨雾:比空气重、缓沉,寿命短,不是滞留的云
            PRTLoader.NewParticle<PRT_KikasaInkMist>(s.Pos + s.Normal * 6f,
                s.Normal * Main.rand.NextFloat(0.4f, 1f), s.Palette.Deep,
                Main.rand.NextFloat(0.6f, 0.9f) * sizeMul * (air ? 0.7f : 1f))
                ?.Configure(Main.rand.Next(20, 28));
        }

        /// <summary>
        /// 持续段指尖甩珠:第 2~9 帧每帧挑一两根指,从液滴头此刻的位置沿流体速度甩出不再分裂的小珠——
        /// 珠继承头的速度,接着同一条抛物线飞,着色器的头与粒子的珠是同一股液体
        /// </summary>
        private static void ShedTips(Splash s) {
            if (s.Age < 2 || s.Age > 9 || s.FingerN <= 0) {
                return;
            }
            int count = Main.rand.NextBool(3) ? 2 : 1;
            for (int k = 0; k < count; k++) {
                int j = Main.rand.Next(s.FingerN);
                ElementAt(s, j, s.Age, s.Age, out Vector2 posQ, out Vector2 velQ);
                Vector2 tip = ToWorld(s, posQ);
                Vector2 vel = ToWorldDir(s, velQ) * Main.rand.NextFloat(0.85f, 1.05f)
                    + s.Tangent * Main.rand.NextFloat(-0.5f, 0.5f);
                PRTLoader.NewParticle<PRT_KikasaInkSpray>(tip + Main.rand.NextVector2Circular(1.5f, 1.5f), vel,
                    PickColor(s.Palette), SprayScale(s.FingerA[j].W * Main.rand.NextFloat(0.55f, 0.85f)))
                    ?.Configure(Main.rand.Next(14, 24), s.Palette.Deep, s.Palette.Sheen, s.LakeY, false, true, Gravity);
            }
        }

        /// <summary>
        /// 寿命末四帧交接:每指从液滴头往后按珠链波长取两三颗珠的位置与速度,原地换成粒子继续飞,
        /// 着色器随后三帧清残——观者看到的是同一串珠一直在飞,不是花消失了又冒出粒子
        /// </summary>
        private static void Handoff(Splash s) {
            float t = s.Age;
            for (int j = 0; j < s.FingerN; j++) {
                Vector4 fa = s.FingerA[j];
                Vector4 fb = s.FingerB[j];
                float tMin = MathF.Max(0f, t - fb.Y);
                float spacing = BeadWavelengthR0 * fa.W / MathF.Max(fa.Z, 1f);
                for (int k = 0; k < 3; k++) {
                    float T = t - k * spacing;
                    if (T <= tMin + 0.3f) {
                        break;
                    }
                    ElementAt(s, j, t, T, out Vector2 posQ, out Vector2 velQ);
                    if (s.Kind == SplashKind.Tile && posQ.Y < 0f) {
                        continue;
                    }
                    //头最大,身后的珠按链上半径剖面缩
                    float u = MathHelper.Clamp(T / t, 0f, 1f);
                    float rProf = fa.W * (k == 0 ? 1.08f : 0.65f + 0.35f * u);
                    PRTLoader.NewParticle<PRT_KikasaInkSpray>(ToWorld(s, posQ), ToWorldDir(s, velQ),
                        PickColor(s.Palette), SprayScale(rProf))
                        ?.Configure(Main.rand.Next(14, 22), s.Palette.Deep, s.Palette.Sheen, s.LakeY, false, true, Gravity);
                }
            }
        }

        /// <summary>命中拍:溅水(同旧法)+ 高动能闷响(帧预算)+ 沾敌肉响;屏震只给手动高动能滴、六帧一次</summary>
        private static void PlayBeat(Splash s, bool manual) {
            float ke = s.Ke;
            KikasaInk.Play(KikasaInk.InkSplash, s.Pos, 0.42f + 0.22f * ke, -0.35f, 5);
            if (ke > 0.7f && s.Kind != SplashKind.Air && TakeSoundBudget()) {
                KikasaInk.Play(SoundID.DD2_MonkStaffGroundImpact, s.Pos, 0.16f + 0.12f * ke, -0.7f, 2);
            }
            if (s.Kind == SplashKind.Npc) {
                KikasaInk.Play(SoundID.NPCHit13, s.Pos, 0.32f + 0.12f * ke, -0.45f, 4);
            }
            if (manual && ke > 0.85f && Main.GameUpdateCount - lastShakeFrame >= 6) {
                lastShakeFrame = Main.GameUpdateCount;
                Main.LocalPlayer?.CWR()?.GetScreenShake(0.8f);
            }
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = list.Count - 1; i >= 0; i--) {
                Splash s = list[i];
                s.Age++;
                if (s.Age >= LifeFrames) {
                    list.RemoveAt(i);
                    continue;
                }
                if (s.NpcWho >= 0 && s.NpcWho < Main.maxNPCs) {
                    NPC npc = Main.npc[s.NpcWho];
                    if (npc.active && npc.type == s.NpcType) {
                        s.Pos = npc.Center + s.Offset;
                    }
                    else {
                        //宿主没了:花钉在最后位置把这十几帧走完
                        s.NpcWho = -1;
                    }
                }
                ShedTips(s);
                if (s.Age == HandoffFrame) {
                    Handoff(s);
                }
            }
        }

        public static void Clear() => list.Clear();

        //==================== 绘制(由 KikasaRainRender 调用) ====================

        private static Rectangle ViewRect() {
            return new Rectangle((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);
        }

        /// <summary>着色器批:Immediate 逐花全量上参(共享参数会被上一朵污染);着色器缺席走精灵回退</summary>
        public static void Draw(SpriteBatch sb) {
            if (list.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.KikasaInkSplash?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Rectangle view = ViewRect();

            if (fx != null && canvas != null && noise != null) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                fx.CurrentTechnique = fx.Techniques["TechSplash"];
                fx.Parameters["uLife"]?.SetValue((float)LifeFrames);
                foreach (Splash s in list) {
                    if (!view.Contains(s.Pos.ToPoint())) {
                        continue;
                    }
                    DrawQuad(sb, fx, canvas, s);
                }
                sb.End();
                return;
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            foreach (Splash s in list) {
                if (view.Contains(s.Pos.ToPoint())) {
                    DrawFallback(sb, s);
                }
            }
            sb.End();
        }

        /// <summary>
        /// 一朵一个 quad:根钉在撞击点(origin=(RootU·W, RootV·H)),quad "上"对准法线
        /// (rotation=法线角+PiOver2,同墨滴 quad 朝向法),尺寸与根 uv 由 SolveQuad 的轨迹包围盒折算
        /// </summary>
        private static void DrawQuad(SpriteBatch sb, Effect fx, Texture2D canvas, Splash s) {
            fx.Parameters["uT"]?.SetValue((float)s.Age);
            fx.Parameters["uQuad"]?.SetValue(new Vector4(s.QuadW, s.QuadH, s.RootU, s.RootV));
            fx.Parameters["uGrav"]?.SetValue(GravQ(s));
            fx.Parameters["uSurfaceClip"]?.SetValue(s.Kind == SplashKind.Tile ? 1f : 0f);
            fx.Parameters["uRimMax"]?.SetValue(s.RimMax);
            fx.Parameters["uKe"]?.SetValue(s.Ke);
            fx.Parameters["uSkew"]?.SetValue(s.Skew);
            fx.Parameters["uFingerN"]?.SetValue((float)s.FingerN);
            fx.Parameters["uFinger"]?.SetValue(s.FingerA);
            fx.Parameters["uFingerB"]?.SetValue(s.FingerB);
            fx.Parameters["uSeed"]?.SetValue(s.Seed);
            fx.Parameters["uColBody"]?.SetValue(s.Palette.Body.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(s.Palette.Deep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(s.Palette.Core.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(s.Palette.Sheen.ToVector3());
            fx.CurrentTechnique.Passes[0].Apply();

            float rotation = s.Normal.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = new(canvas.Width * s.RootU, canvas.Height * s.RootV);
            sb.Draw(canvas, s.Pos - Main.screenPosition, null, Color.White, rotation, origin,
                new Vector2(s.QuadW / canvas.Width, s.QuadH / canvas.Height), SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:每指沿抛物线取四段,用速度拉伸的纺锤连成链(暗缘+体),末三帧淡出</summary>
        private static void DrawFallback(SpriteBatch sb, Splash s) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float t = s.Age;
            float alive = 1f - MathHelper.Clamp((t - (LifeFrames - 3f)) / 3f, 0f, 1f);
            Vector2 origin = tex.Size() * 0.5f;
            for (int j = 0; j < s.FingerN; j++) {
                Vector4 fa = s.FingerA[j];
                float tMin = MathF.Max(0f, t - s.FingerB[j].Y);
                const int Segs = 4;
                for (int k = 0; k < Segs; k++) {
                    float T0 = MathHelper.Lerp(tMin, t, k / (float)Segs);
                    float T1 = MathHelper.Lerp(tMin, t, (k + 1) / (float)Segs);
                    ElementAt(s, j, t, T0, out Vector2 a, out _);
                    ElementAt(s, j, t, T1, out Vector2 b, out _);
                    if (s.Kind == SplashKind.Tile && a.Y < 0f && b.Y < 0f) {
                        continue;
                    }
                    Vector2 mid = ToWorld(s, (a + b) * 0.5f) - Main.screenPosition;
                    Vector2 seg = ToWorldDir(s, b - a);
                    float len = MathF.Max(seg.Length(), 2f);
                    float rot = seg.ToRotation() + MathHelper.PiOver2;
                    float radius = fa.W * (k == Segs - 1 ? 1.3f : 0.7f);
                    Vector2 scale = new(radius * 2f / tex.Width * 1.6f, len / tex.Height * 1.5f);
                    sb.Draw(tex, mid, null, s.Palette.Deep * (0.9f * alive), rot, origin,
                        scale * new Vector2(1.3f, 1.04f), SpriteEffects.None, 0f);
                    sb.Draw(tex, mid, null, s.Palette.Body * alive, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
