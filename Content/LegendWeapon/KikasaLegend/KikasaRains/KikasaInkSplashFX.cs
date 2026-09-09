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
        /// <summary>贴地形(地/墙/顶):沿表面法线展开半扇,有溅裙</summary>
        Tile,
        /// <summary>沾敌:法线取来势反向,根随宿主走</summary>
        Npc,
        /// <summary>空中散尽:全向小爆,无溅裙</summary>
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
    /// 墨水花:墨滴命中一瞬爆发、14 帧内崩解干净的溅射演出(2026-09-09 取代 220f 落地渍斑与 150f 沾敌附着渍)。
    /// 配方对齐血柱:着色器(<see cref="EffectLoader.KikasaInkSplash"/> TechSplash)只画连续的冠状液指,
    /// 离散的那一半交给有物理的 <see cref="PRT_KikasaInkSpray"/>——爆发帧一蓬扇形飞沫,
    /// 之后几帧从着色器的指尖上按同一组指几何续甩珠(珠从指上断下来,不是两套东西);
    /// 贴面溅裙一撮低角度小珠、一口墨雾;音效帧预算、屏震只给手动高动能滴。
    /// 纯客户端列表(环形上限),无网络;各端在自己的 OnKill 里各起一朵,近似一致即可。
    /// 由 <see cref="KikasaRainSystem"/> 推进、<see cref="KikasaRainRender"/> 在墨/血体之后绘制
    /// </summary>
    internal static class KikasaInkSplashFX
    {
        /// <summary>一朵水花的寿命(帧):快,不许拖</summary>
        public const int LifeFrames = 14;
        private const int Cap = 24;
        private const int FingerSlots = 7;

        /// <summary>指根沿切向的摊开量(H 单位),与 KikasaInkSplash.fx 的 RootSpread 同值</summary>
        private const float RootSpread = 0.18f;

        /// <summary>画布护栏:横向 |xc|≤0.88、纵向 v∈[0.04,0.96] 可见,几何折算按此让位</summary>
        private const float GuardX = 0.88f;
        private const float GuardY = 0.96f;

        /// <summary>H(指长基准,px)=(HBase+HKe·ke)·scale</summary>
        private const float HBase = 40f;
        private const float HKe = 36f;

        private class Splash
        {
            public Vector2 Pos;
            /// <summary>撞击法线(单位,离面)</summary>
            public Vector2 Normal;
            /// <summary>quad 空间 +x 在世界里的方向</summary>
            public Vector2 Tangent;
            public float Skew;
            public float Ke;
            public float HPx;
            public float ScaleMul;
            /// <summary>quad 半宽/根上/根下延伸(H 单位,含护栏让位)</summary>
            public float HalfWH;
            public float TopH;
            public float BottomH;
            public float Seed;
            public int Age;
            public SplashKind Kind;
            public SplashPalette Palette;
            public Vector4[] Fingers = new Vector4[FingerSlots];
            public int FingerN;
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
                HPx = (HBase + HKe * ke) * scale * (kind == SplashKind.Air ? 0.7f : 1f),
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
        /// 解指几何:5~7 指(空中 4)按扇等分再 hash 抖动,整扇随切向偏斜转、偏斜侧指更长;
        /// 长随离法线角度收(中间高两翼低),根半宽 hash;槽 0 恒为主指(居中、略粗)。
        /// 顺手算 quad 让位:半宽/根上/根下按最长伸展(长包络峰值×指尖尾串)取最大再除护栏
        /// </summary>
        private static void SolveFingers(Splash s) {
            bool air = s.Kind == SplashKind.Air;
            int n = air ? 4 : 5 + (int)MathF.Round(2f * s.Ke);
            n = Math.Clamp(n, 2, FingerSlots);
            float halfFan = air ? 2.6f : 0.95f + 0.35f * s.Ke;
            int seed = (int)(s.Seed * 977f);
            int mainJ = n / 2;

            float halfW = 0.6f;
            float top = 0.4f;
            float bottom = 0.15f;
            int slot = 1;
            for (int j = 0; j < n; j++) {
                float baseAng = -halfFan + 2f * halfFan * j / (n - 1);
                float ang = baseAng + (KikasaInk.Hash(seed, j) - 0.5f) * 0.24f + s.Skew * 0.45f;
                if (!air) {
                    //贴面/沾敌的指不许扎回面里:偏斜转过头时钳在离面 83° 内
                    ang = MathHelper.Clamp(ang, -1.45f, 1.45f);
                }
                float len;
                if (air) {
                    len = (0.7f + 0.3f * KikasaInk.Hash(seed, j + 7)) * 0.8f;
                }
                else {
                    len = (0.75f + 0.25f * KikasaInk.Hash(seed, j + 7))
                        * (0.6f + 0.4f * MathF.Cos(baseAng))
                        * (1f + 0.35f * s.Ke)
                        * MathHelper.Clamp(1f + 0.4f * s.Skew * MathF.Sin(ang), 0.7f, 1.5f);
                }
                float hw = 0.07f + 0.05f * KikasaInk.Hash(seed, j + 14);
                float phase = KikasaInk.Hash(seed, j + 21) * MathHelper.TwoPi;
                int target;
                if (j == mainJ) {
                    target = 0;
                    hw += 0.015f;
                }
                else {
                    target = slot++;
                }
                s.Fingers[target] = new Vector4(ang, len, hw, phase);

                //最长伸展:长包络峰值 1.22(过冲 1.08 与缓增 0.22 不同时到顶,取和保守)× 指尖尾串 1.3
                float reach = len * 1.3f * 1.25f;
                halfW = MathF.Max(halfW, MathF.Abs(MathF.Sin(ang)) * reach + hw + RootSpread + 0.1f);
                top = MathF.Max(top, MathF.Cos(ang) * reach + hw + 0.08f);
                bottom = MathF.Max(bottom, -MathF.Cos(ang) * reach + hw + 0.08f);
            }
            for (int i = n; i < FingerSlots; i++) {
                s.Fingers[i] = Vector4.Zero;
            }
            s.FingerN = n;
            s.HalfWH = halfW / GuardX;
            s.TopH = top / GuardY;
            s.BottomH = bottom / GuardY;
        }

        //==================== 与着色器同源的指几何 ====================

        /// <summary>起手 EaseOutBack 过冲 + 之后开方缓增,与 KikasaInkSplash.fx 同式</summary>
        private static float LenEnvelope(float age) {
            float tr = MathHelper.Clamp(age / 0.25f, 0f, 1f);
            float e = tr - 1f;
            float rise = 1f + 2.3f * e * e * e + 1.3f * e * e;
            float grow = 0.22f * MathF.Sqrt(MathHelper.Clamp((age - 0.25f) / 0.75f, 0f, 1f));
            return rise + grow;
        }

        /// <summary>第 i 指此刻的指尖世界坐标与指向</summary>
        private static void FingerTip(Splash s, int i, out Vector2 tip, out Vector2 dir) {
            Vector4 f = s.Fingers[i];
            float sa = MathF.Sin(f.X);
            float ca = MathF.Cos(f.X);
            dir = s.Tangent * sa + s.Normal * ca;
            float len = f.Y * LenEnvelope(s.Age / (float)LifeFrames);
            tip = s.Pos + s.Tangent * (sa * RootSpread * s.HPx) + dir * (len * s.HPx);
        }

        //==================== 粒子编舞 ====================

        private static Color PickColor(in SplashPalette p) => Main.rand.NextBool(3) ? p.Deep : p.Body;

        /// <summary>
        /// 爆发帧:沿各指角散一蓬快飞沫(带一点切向来势的前带),
        /// 贴面一撮低角度小珠当溅裙,一口墨雾;空中散尽只有半量飞沫、无溅裙
        /// </summary>
        private static void SpawnBurstParticles(Splash s, Vector2 impactVel) {
            bool air = s.Kind == SplashKind.Air;
            float ke = s.Ke;
            float sizeMul = MathHelper.Clamp(s.ScaleMul, 0.8f, 1.4f);
            Vector2 carry = s.Tangent * (Vector2.Dot(impactVel, s.Tangent) * 0.15f);

            int sprays = 5 + (int)(7f * ke);
            if (air) {
                sprays /= 2;
            }
            for (int i = 0; i < sprays; i++) {
                Vector4 f = s.Fingers[i % s.FingerN];
                float ang = f.X + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 dir = s.Tangent * MathF.Sin(ang) + s.Normal * MathF.Cos(ang);
                float speed = Main.rand.NextFloat(5f, 10f) * (0.7f + 0.5f * ke) * sizeMul;
                Vector2 at = s.Pos + s.Normal * Main.rand.NextFloat(2f, 6f)
                    + s.Tangent * Main.rand.NextFloat(-0.15f, 0.15f) * s.HPx;
                float scale = Main.rand.NextFloat(0.34f, 0.62f) * sizeMul;
                PRTLoader.NewParticle<PRT_KikasaInkSpray>(at, dir * speed + carry, PickColor(s.Palette), scale)
                    ?.Configure(Main.rand.Next(16, 30), s.Palette.Deep, s.Palette.Sheen, s.LakeY, scale > 0.45f, true);
            }

            if (!air) {
                //溅裙:贴面低角度弹出的小珠,两侧各半
                int skirt = 3 + (int)(2f * ke);
                for (int i = 0; i < skirt; i++) {
                    float side = i % 2 == 0 ? 1f : -1f;
                    float lift = Main.rand.NextFloat(0.1f, 0.45f);
                    Vector2 dir = s.Tangent * (side * MathF.Cos(lift)) + s.Normal * MathF.Sin(lift);
                    Vector2 at = s.Pos + s.Normal * 2f + s.Tangent * (side * Main.rand.NextFloat(0.05f, 0.3f) * s.HPx);
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

        /// <summary>持续段指尖甩珠:第 2~7 帧每帧挑一两根指,从它此刻的指尖沿指向甩出不再分裂的小珠</summary>
        private static void ShedTips(Splash s) {
            if (s.Age < 2 || s.Age > 7 || s.FingerN <= 0) {
                return;
            }
            int count = Main.rand.NextBool(3) ? 2 : 1;
            float sizeMul = MathHelper.Clamp(s.ScaleMul, 0.8f, 1.4f);
            for (int k = 0; k < count; k++) {
                int i = Main.rand.Next(s.FingerN);
                FingerTip(s, i, out Vector2 tip, out Vector2 dir);
                Vector2 vel = dir * Main.rand.NextFloat(2.5f, 4.5f) * sizeMul
                    + s.Tangent * Main.rand.NextFloat(-0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_KikasaInkSpray>(tip + Main.rand.NextVector2Circular(2f, 2f), vel,
                    PickColor(s.Palette), Main.rand.NextFloat(0.28f, 0.4f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 24), s.Palette.Deep, s.Palette.Sheen, s.LakeY, false, true);
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
        /// 一朵一个 quad:根钉在撞击点(origin=(半宽, uRootV·quadH)),quad "上"对准法线
        /// (rotation=法线角+PiOver2,同墨滴 quad 朝向法),尺寸由 SolveFingers 的让位量折算
        /// </summary>
        private static void DrawQuad(SpriteBatch sb, Effect fx, Texture2D canvas, Splash s) {
            float quadW = 2f * s.HalfWH * s.HPx;
            float quadH = (s.TopH + s.BottomH) * s.HPx;
            float rootV = s.TopH / (s.TopH + s.BottomH);
            //世界重力在 quad 空间的方向:x=切向分量,y=法向分量
            Vector2 gravQ = new(s.Tangent.Y, s.Normal.Y);

            fx.Parameters["uAge"]?.SetValue(s.Age / (float)LifeFrames);
            fx.Parameters["uKe"]?.SetValue(s.Ke);
            fx.Parameters["uSkew"]?.SetValue(s.Skew);
            fx.Parameters["uFingerN"]?.SetValue((float)s.FingerN);
            fx.Parameters["uFinger"]?.SetValue(s.Fingers);
            fx.Parameters["uGravQ"]?.SetValue(gravQ);
            fx.Parameters["uHScale"]?.SetValue(s.HPx / quadH);
            fx.Parameters["uRootV"]?.SetValue(rootV);
            fx.Parameters["uHalfWH"]?.SetValue(s.HalfWH);
            fx.Parameters["uSeed"]?.SetValue(s.Seed);
            fx.Parameters["uColBody"]?.SetValue(s.Palette.Body.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(s.Palette.Deep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(s.Palette.Core.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(s.Palette.Sheen.ToVector3());
            fx.CurrentTechnique.Passes[0].Apply();

            float rotation = s.Normal.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = new(canvas.Width * 0.5f, canvas.Height * rootV);
            sb.Draw(canvas, s.Pos - Main.screenPosition, null, Color.White, rotation, origin,
                new Vector2(quadW / canvas.Width, quadH / canvas.Height), SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:沿各指角画速度拉伸的纺锤(暗缘+体),长度走同一包络,后半寿命随根断供缩短</summary>
        private static void DrawFallback(SpriteBatch sb, Splash s) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float age = s.Age / (float)LifeFrames;
            float env = LenEnvelope(age);
            float rootCut = MathHelper.Clamp((age - 0.35f) / 0.45f, 0f, 1f) * 0.85f;
            float alive = 1f - MathHelper.Clamp((age - 0.9f) / 0.1f, 0f, 1f);
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < s.FingerN; i++) {
                Vector4 f = s.Fingers[i];
                float sa = MathF.Sin(f.X);
                float ca = MathF.Cos(f.X);
                Vector2 dir = s.Tangent * sa + s.Normal * ca;
                float lenPx = f.Y * env * s.HPx;
                float startPx = rootCut * lenPx;
                float bodyLen = MathF.Max(lenPx - startPx, 2f);
                Vector2 mid = s.Pos + s.Tangent * (sa * RootSpread * s.HPx) + dir * (startPx + bodyLen * 0.5f)
                    - Main.screenPosition;
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                Vector2 scale = new(f.Z * s.HPx * 2f / tex.Width * 1.6f, bodyLen / tex.Height * 1.4f);
                sb.Draw(tex, mid, null, s.Palette.Deep * (0.9f * alive), rot, origin,
                    scale * new Vector2(1.3f, 1.04f), SpriteEffects.None, 0f);
                sb.Draw(tex, mid, null, s.Palette.Body * alive, rot, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
