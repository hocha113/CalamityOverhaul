using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>一条合鬼边本帧的表现量,由 <see cref="OniSigilUI"/> 汇总</summary>
    internal struct OniSigilEdgeView
    {
        /// <summary>成立的合鬼名(null=未成立)</summary>
        public string Name;
        /// <summary>墨线流通进度 0~1(成边时亮笔跑线,退位时墨退)</summary>
        public float Flow;
        /// <summary>流通起笔端(0=EdgeSlots.A / 1=EdgeSlots.B)</summary>
        public int FlowOrigin;
        /// <summary>预演配对名(持印悬停空位时"这条边会通")</summary>
        public string PreviewName;
        /// <summary>预演强度 0~1</summary>
        public float Preview;
        /// <summary>三印崩边闪 0~1</summary>
        public float Flash;
    }

    /// <summary>一个结印位本帧的表现量</summary>
    internal struct OniSigilSlotView
    {
        public OniGhostEntry Entry;
        public float Hover;
        /// <summary>候令中(请求在途)</summary>
        public bool Pending;
        /// <summary>压印量 0~1(印被按在槽上等回执)</summary>
        public float Press;
        /// <summary>持印邀请 0~1(空位鬼火环呼吸)</summary>
        public float Invite;
        /// <summary>持印悬停此位的预放印(空位=落印预览,占位=换印预览)</summary>
        public OniGhostEntry PreviewEntry;
        /// <summary>拒绝震颤位移</summary>
        public Vector2 Shake;
        /// <summary>拒绝红闪 1→0</summary>
        public float DenyFlash;
        /// <summary>落印定妆闪 1→0(朱墨涟漪)</summary>
        public float StampFlash;
    }

    /// <summary>
    /// 结印盘绘制:shader 漆盘盘体(缺编退 CPU 环层)+六芒墨骨+外环鬼位
    /// (鬼影/变体朱印/线香燃弧)+内三角结印位+合鬼边墨线+三印崩心。<br/>
    /// 锐利前景全 CPU 笔触;暗部一律贴身投影,禁同心放大伪造羽化
    /// </summary>
    internal static class OniSigilRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>盘座外圈的一段蚀刻缺口(CPU 回退用,shader 自带噪蚀边)</summary>
        private const string RingNickD = "M -0.42 -0.06 C -0.2 -0.16 0.2 -0.16 0.42 -0.06";

        /// <summary>线香燃尽的灰烬色</summary>
        private static readonly Color AshCol = new(66, 54, 50);

        //====================== 盘座 ======================

        /// <summary>
        /// 漆盘盘体:贴身实心盘影 + shader 漆盘(轆轤纹/漆光/蒔絵/金压线/眠焰),
        /// 缺编退回实心漆底 + 环层简笔
        /// </summary>
        public static void DrawBoard(SpriteBatch sb, in OniSigilWheel wheel, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 c = wheel.Center;
            float r = wheel.Radius;
            float discR = r * 1.02f;

            //贴身投影:实心盘影,偏移不放大(放大同心=方块黑层)
            DrawFilledCircle(sb, c + new Vector2(3f, 4.5f), discR, new Color(8, 2, 5) * (alpha * 0.45f));

            if (OniSigilBoardDraw.Available) {
                (Vector3 lit, Vector3 danger, float complete) = OniSigilBoardDraw.ReadSlotState();
                float slotR = r * OniSigilWheel.SlotRadiusRatio;
                OniSigilBoardDraw.DrawDisc(sb, c, discR, r, slotR, lit, danger, complete, alpha, time);
            }
            else {
                //CPU 回退:实心漆底(粗边由厚环遮住) + 既有环层
                DrawFilledCircle(sb, c, discR,
                    Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.28f) * (alpha * 0.96f));
                DrawRing(sb, c, discR, 10f, Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.35f) * (alpha * 0.97f), 96);
                DrawRing(sb, c, r, 4.5f, Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, 0.6f) * (alpha * 0.97f), 96);
                DrawRing(sb, c, r * 0.985f, 1.2f, OnikiriUITheme.GoldDeep * (alpha * 0.5f), 96);
                DrawRing(sb, c, r * 0.968f, 1f, OnikiriUITheme.Deep * (alpha * 0.34f), 96);
                //漆理:几道随机长度的淡纹,绕环走向
                for (int i = 0; i < 10; i++) {
                    float u = OniBrush.Hash01(i * 47 + 13);
                    float ang = u * MathHelper.TwoPi;
                    float span = 0.10f + OniBrush.Hash01(i * 71 + 5) * 0.22f;
                    DrawArc(sb, c, r * (0.99f + OniBrush.Hash01(i * 29 + 3) * 0.02f), 1f,
                        Color.Black * (alpha * 0.20f), ang, ang + span, 12);
                }
                //蚀刻缺口:手工件不该是标准圆
                SvgPath nick = SvgPathPen.Path(RingNickD);
                if (nick != null) {
                    SvgPathPen.Stroke(sb, nick, c - new Vector2(0f, r * 0.99f), r * 0.34f, 0f,
                        Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.5f), 6f, alpha * 0.8f);
                }
            }

            //盘面呼吸背光:极缓,静物不死
            float breath = 0.5f + 0.5f * MathF.Sin(time * 0.6f);
            OniBrush.DrawBacklight(sb, c, r * 0.9f, OnikiriUITheme.Deep,
                alpha * (0.05f + breath * 0.03f));
        }

        /// <summary>六芒星:两枚交叠正三角的墨线骨架(shader 蒔絵暗纹之上的一层湿墨)</summary>
        public static void DrawHexagram(SpriteBatch sb, in OniSigilWheel wheel, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            for (int t = 0; t < 2; t++) {
                float a = alpha * (t == 0 ? 0.60f : 0.46f);
                for (int i = 0; i < 3; i++) {
                    Vector2 p0 = wheel.StarPos(t + i * 2);
                    Vector2 p1 = wheel.StarPos((t + (i + 1) * 2) % OniSigilWheel.NodeCount);
                    //起笔重收笔轻,避免六条等重直线读成矢量图
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.Deep * a, OnikiriUITheme.Dark * (a * 0.4f), 1.5f);
                }
            }
            //尖端朱点,随位相位错开呼吸
            for (int i = 0; i < OniSigilWheel.NodeCount; i++) {
                float breath = OnikiriUITheme.Breath(time, i * 1.7f, 1.1f);
                OniBrush.DrawSoftDot(sb, wheel.StarPos(i), 2.4f + breath * 0.8f,
                    OnikiriUITheme.Seal, alpha * (0.35f + breath * 0.2f));
            }
        }

        //====================== 外环鬼位 ======================

        /// <summary>
        /// 一枚外环鬼位:鬼影(悬停睁眼凝视光标)+变体朱印+线香燃弧读复苏+名讳。<br/>
        /// boundSlot &gt;= 0 表示它正结印在盘上;carried=印已被拾在手,原位只剩拓空痕
        /// </summary>
        public static void DrawNode(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            int index, OniGhostEntry entry, int boundSlot, bool carried, float hover,
            Vector2 mouse, float alpha, float time) {
            if (alpha <= 0.01f || entry == null) {
                return;
            }
            Vector2 p = wheel.NodePos(index);
            float size = wheel.NodeHit * 0.78f;
            bool onBoard = boundSlot >= 0;
            int seed = OniBrush.SealSeedFromKey(entry.Key);

            //鬼影:印后立影,悬停睁眼凝视光标;拾走后影仍守在环位等印归
            if (OniGhostShadowDraw.Available) {
                Rectangle shadowRect = new((int)(p.X - size * 1.45f), (int)(p.Y - size * 2.55f),
                    (int)(size * 2.9f), (int)(size * 3.5f));
                float eyeOpen = MathHelper.Clamp(hover * 1.1f, 0f, 1f);
                if (onBoard) {
                    eyeOpen = MathF.Max(eyeOpen, 0.16f);
                }
                if (entry.InDanger) {
                    eyeOpen = MathF.Max(eyeOpen, 0.48f + 0.2f * MathF.Sin(time * 2.6f + index));
                }
                Vector2 toMouse = mouse - p;
                float dist = toMouse.Length();
                Vector2 glance = dist < 1f ? Vector2.Zero
                    : toMouse / dist * 0.03f * MathHelper.Clamp(dist / 240f, 0.25f, 1f);
                OniGhostShadowDraw.Draw(sb, shadowRect, new OniGhostShadowParams {
                    Writhe = 0.30f + hover * 0.35f + (entry.InDanger ? 0.25f : 0f),
                    Break = carried ? 0.55f : onBoard ? 0.10f : 0.30f,
                    EyeOpen = eyeOpen,
                    Glance = glance,
                    Seed = OniGhostShadowDraw.SeedFromKey(entry.Key),
                    Alpha = alpha * (carried ? 0.30f
                        : 0.40f + hover * 0.30f + (onBoard ? 0.10f : 0f)),
                    Time = time,
                });
            }

            //在盘上的鬼:印外一圈朱环 + 暖底
            if (onBoard && !carried) {
                OniBrush.DrawBacklight(sb, p, size * 2.0f, OnikiriUITheme.Deep, alpha * 0.20f);
                DrawRing(sb, p, size * 1.42f, 1.5f, OnikiriUITheme.Seal * (alpha * 0.85f), 40);
            }
            //将醒:印下垂一笔血墨
            if (entry.InDanger) {
                float bleed = 0.5f + 0.5f * MathF.Sin(time * 2.4f + index);
                OniBrush.DrawGradientLine(sb, p + new Vector2(0f, size * 0.9f),
                    p + new Vector2(-1.4f, size * (1.9f + bleed * 0.5f)),
                    OnikiriUITheme.Bright * (alpha * 0.7f), OnikiriUITheme.Deep * 0f, 1.8f);
            }

            if (carried) {
                //拓空痕:印被拾走,原位一记浅框与残印
                DrawRing(sb, p, size * 0.74f, 1.1f, OnikiriUITheme.TextDim * (alpha * 0.38f), 26);
                OniBrush.DrawSealGlyphSeeded(sb, p, size * 0.94f, alpha * 0.18f, seed, 0f, 0.3f);
            }
            else {
                float lift = 1f + hover * 0.10f;
                float integrity = onBoard ? 1f : 0.78f + hover * 0.2f;
                OniBrush.DrawSealGlyphSeeded(sb, p, size * lift,
                    alpha * (onBoard ? 1f : 0.72f + hover * 0.25f), seed, 0f, integrity);
            }

            //线香燃弧:自顶顺时针,燃去比即复苏;燃前沿一粒余烬,将醒转高温
            float burnFrom = -MathHelper.PiOver2;
            float burnTo = burnFrom + MathHelper.Clamp(entry.Revival, 0f, 1f) * MathHelper.TwoPi;
            float arcR = size * 1.18f;
            DrawArc(sb, p, arcR, 1.1f, OnikiriUITheme.Seal * (alpha * 0.32f),
                burnTo, burnFrom + MathHelper.TwoPi, 40);
            if (entry.Revival > 0.01f) {
                DrawArc(sb, p, arcR, 1.5f, AshCol * (alpha * 0.6f), burnFrom, burnTo, 40);
                Vector2 emberPos = p + burnTo.ToRotationVector2() * arcR;
                float flick = 0.6f + 0.4f * MathF.Sin(time * (4.6f + index * 0.7f));
                Color emberCol = entry.InDanger ? OnikiriUITheme.BurnHot : OnikiriUITheme.BurnDim;
                OniBrush.DrawSoftDot(sb, emberPos, entry.InDanger ? 3.2f : 2.1f,
                    emberCol, alpha * (0.45f + 0.4f * flick));
            }

            //名讳横书印下;复苏读数只在悬停时浮出
            string name = entry.Name?.Invoke() ?? string.Empty;
            Color nameCol = onBoard ? OnikiriUITheme.Paper
                : Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, hover);
            DrawCentered(sb, font, name, p + new Vector2(0f, size * 1.55f + 4f),
                nameCol * (alpha * (0.80f + hover * 0.20f)), 0.60f);
            if (hover > 0.25f) {
                string read = $"{(int)MathF.Round(entry.Revival * 100f)}%";
                Color readCol = entry.InDanger ? OnikiriUITheme.Bright : OnikiriUITheme.TextDim;
                DrawCentered(sb, font, read, p + new Vector2(0f, size * 1.55f + 19f),
                    readCol * (alpha * 0.85f * hover), 0.55f);
            }
        }

        //====================== 内三角结印位与合鬼边 ======================

        /// <summary>三角三边:成边墨线流通+巡行亮笔,预演虚线鬼火,未成只留干笔</summary>
        public static void DrawEdges(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            OniSigilEdgeView[] edges, float alpha, float time) {
            if (alpha <= 0.01f || edges == null) {
                return;
            }
            for (int e = 0; e < 3 && e < edges.Length; e++) {
                (int a, int b) = OniSigilWheel.EdgeSlots(e);
                Vector2 p0 = wheel.SlotPos(a);
                Vector2 p1 = wheel.SlotPos(b);
                Vector2 mid = Vector2.Lerp(p0, p1, 0.5f);
                OniSigilEdgeView view = edges[e];
                bool live = !string.IsNullOrEmpty(view.Name) && view.Flow > 0.01f;

                if (live) {
                    //成立的组合:墨自起笔端流通;未到位时笔锋在前沿,到位后一段亮笔巡行
                    float flow = 1f - (1f - view.Flow) * (1f - view.Flow);
                    Vector2 from = view.FlowOrigin == 1 ? p1 : p0;
                    Vector2 to = view.FlowOrigin == 1 ? p0 : p1;
                    Vector2 tip = Vector2.Lerp(from, to, flow);
                    OniBrush.DrawGradientLine(sb, from, tip,
                        OnikiriUITheme.Bright * (alpha * 0.75f),
                        OnikiriUITheme.Deep * (alpha * 0.75f), 2.2f);
                    if (view.Flow < 0.999f) {
                        OniBrush.DrawSoftDot(sb, tip, 4.4f, OnikiriUITheme.HotWhite, alpha * 0.8f);
                    }
                    else {
                        float run = (time * 0.22f + e * 0.33f) % 1f;
                        OniBrush.DrawSoftDot(sb, Vector2.Lerp(p0, p1, run), 3.2f,
                            OnikiriUITheme.HotWhite, alpha * 0.45f);
                    }
                    float nameA = MathHelper.Clamp((view.Flow - 0.68f) / 0.32f, 0f, 1f);
                    if (nameA > 0.01f) {
                        DrawCentered(sb, font, view.Name, mid + new Vector2(0f, -9f),
                            OnikiriUITheme.Bright * (alpha * 0.9f * nameA), 0.58f);
                    }
                }
                else if (view.Preview > 0.02f && !string.IsNullOrEmpty(view.PreviewName)) {
                    //预演:鬼火虚线缓移——落印之前就读得懂"这条边会通、通成什么"
                    DrawDashedLine(sb, p0, p1, OnikiriUITheme.GhostDim, OnikiriUITheme.GhostFire,
                        alpha * view.Preview * 0.8f, time, e * 1.7f);
                    DrawCentered(sb, font, view.PreviewName, mid + new Vector2(0f, -9f),
                        OnikiriUITheme.GhostFire * (alpha * 0.8f * view.Preview), 0.55f);
                }
                else {
                    //未成立:只留一道断续的干笔,读得出"这里本该通"
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.Dark * (alpha * 0.5f),
                        OnikiriUITheme.Dark * (alpha * 0.18f), 1.1f);
                }

                //三印崩边闪:白热压一拍
                if (view.Flash > 0.01f) {
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.HotWhite * (alpha * view.Flash * 0.9f),
                        OnikiriUITheme.Bright * (alpha * view.Flash * 0.6f), 3f);
                }
            }
        }

        /// <summary>一个结印位:占位画印,空位画凿槽;邀请/预放/压印/拒绝/定妆各有其形</summary>
        public static void DrawSlot(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            int slot, in OniSigilSlotView v, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 p = wheel.SlotPos(slot) + v.Shake;
            float size = wheel.SlotHit * 0.72f;

            //槽底凿痕:一圈内暗上缘 + 受光下唇,取代描边矩形
            DrawRing(sb, p, size * 1.25f, 1.4f, Color.Black * (alpha * 0.55f), 32);
            DrawRing(sb, p + new Vector2(0f, 1.2f), size * 1.25f, 1f,
                OnikiriUITheme.Paper * (alpha * 0.10f), 32);
            //拒绝:凿圈染绯一闪
            if (v.DenyFlash > 0.01f) {
                DrawRing(sb, p, size * 1.25f, 1.8f,
                    OnikiriUITheme.Bright * (alpha * v.DenyFlash * 0.8f), 32);
            }

            if (v.Entry == null) {
                //空槽:凿槽与一点余烬
                OniBrush.DrawSoftDot(sb, p, size * 0.5f, OnikiriUITheme.Dark, alpha * 0.5f);
                //持印邀请:鬼火环呼吸,受印的位自己亮起来
                if (v.Invite > 0.02f) {
                    float br = 0.5f + 0.5f * MathF.Sin(time * 2.2f + slot * 2.1f);
                    DrawRing(sb, p, size * (1.30f + br * 0.10f), 1.3f,
                        OnikiriUITheme.GhostFire * (alpha * v.Invite * (0.30f + 0.40f * br)), 36);
                    OniBrush.DrawSoftDot(sb, p, size * 0.8f, OnikiriUITheme.GhostDim,
                        alpha * v.Invite * 0.22f * br);
                }
                if (v.Hover > 0.03f) {
                    DrawRing(sb, p, size * (1.25f + v.Hover * 0.15f), 1.2f,
                        OnikiriUITheme.Seal * (alpha * v.Hover * 0.8f), 32);
                }
                //预放:印影浮在槽上,读得出"落下去就是这样"
                if (v.PreviewEntry != null && v.Hover > 0.08f) {
                    OniBrush.DrawSealGlyphSeeded(sb, p, size * 0.96f,
                        alpha * 0.38f * v.Hover,
                        OniBrush.SealSeedFromKey(v.PreviewEntry.Key), 0f, 0.85f);
                }
                //落印定妆可能落在刚清空的槽上(卸印涟漪)
                DrawStampRipple(sb, p, size, v.StampFlash, alpha);
                return;
            }

            //占用槽
            OniBrush.DrawBacklight(sb, p, size * 2.4f, OnikiriUITheme.Deep,
                alpha * (0.24f + v.Hover * 0.12f));
            float lift = 1f + v.Hover * 0.12f;
            float sink = v.Press * 0.10f;
            OniBrush.DrawSealGlyphSeeded(sb, p + new Vector2(0f, v.Press * 1.6f),
                size * (lift - sink), alpha * (v.Pending ? 0.55f : 1f),
                OniBrush.SealSeedFromKey(v.Entry.Key));
            //候令期:印上压一道慢转的干笔,读得出"在等回执"
            if (v.Pending) {
                DrawArc(sb, p, size * 1.5f, 1.6f, OnikiriUITheme.TextDim * (alpha * 0.7f),
                    time * 2.2f, time * 2.2f + 1.6f, 14);
            }
            //换印预览:新印浮在旧印上一线
            if (v.PreviewEntry != null && v.PreviewEntry != v.Entry && v.Hover > 0.08f) {
                OniBrush.DrawSealGlyphSeeded(sb, p + new Vector2(0f, -3f), size * 0.92f,
                    alpha * 0.42f * v.Hover,
                    OniBrush.SealSeedFromKey(v.PreviewEntry.Key), 0f, 0.9f);
            }
            DrawStampRipple(sb, p, size, v.StampFlash, alpha);

            string name = v.Entry.Name?.Invoke() ?? string.Empty;
            DrawCentered(sb, font, name, p + new Vector2(0f, size * 1.7f),
                OnikiriUITheme.Paper * (alpha * 0.9f), 0.58f);
        }

        /// <summary>落印定妆:朱墨涟漪扩环 + 一拍白热</summary>
        private static void DrawStampRipple(SpriteBatch sb, Vector2 p, float size,
            float flash, float alpha) {
            if (flash <= 0.01f) {
                return;
            }
            float spread = 1f - flash;
            DrawRing(sb, p, size * (1.05f + spread * 1.7f), 0.6f + flash * 2.2f,
                OnikiriUITheme.Seal * (alpha * flash * 0.9f), 36);
            DrawRing(sb, p, size * (1.05f + spread * 1.15f), 0.5f + flash * 1.4f,
                OnikiriUITheme.Deep * (alpha * flash * 0.6f), 32);
            OniBrush.DrawSoftDot(sb, p, size * 1.15f, OnikiriUITheme.HotWhite,
                alpha * flash * flash * 0.55f);
        }

        /// <summary>三角中心:三槽齐了是合鬼心(慢心跳),否则一枚空座;burstT 三印崩收束脉冲</summary>
        public static void DrawCore(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            bool complete, string label, float burstT, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 c = wheel.Center;
            float size = wheel.SlotHit * 0.5f;

            if (!complete && burstT < 0f) {
                DrawRing(sb, c, size, 1.1f, OnikiriUITheme.Dark * (alpha * 0.6f), 28);
                return;
            }

            if (complete) {
                //慢心跳:两拍收缩,收缩期短舒张期长
                float beat = HeartBeat(time);
                OniBrush.DrawBacklight(sb, c, size * (4.0f + beat * 0.8f), OnikiriUITheme.Bright,
                    alpha * (0.13f + beat * 0.14f));
                //三印崩的座:三枚小印围心巡行,心跳时略提速
                for (int i = 0; i < 3; i++) {
                    float ang = OniSigilWheel.SlotAngle(i) + time * (0.25f + beat * 0.10f);
                    OniBrush.DrawSoftDot(sb, c + ang.ToRotationVector2() * (size * 0.9f),
                        2.6f + beat * 0.8f, OnikiriUITheme.Seal, alpha * (0.7f + beat * 0.25f));
                }
                DrawRing(sb, c, size * (1.5f + beat * 0.06f), 1.6f,
                    OnikiriUITheme.Bright * (alpha * (0.55f + beat * 0.30f)), 32);
                OniBrush.DrawSealGlyph(sb, c, size * (1.08f + beat * 0.06f), alpha, time * 0.12f);

                if (!string.IsNullOrEmpty(label)) {
                    DrawCentered(sb, font, label, c + new Vector2(0f, size * 2.4f),
                        OnikiriUITheme.HotWhite * (alpha * 0.9f), 0.6f);
                }
            }

            //三印崩收束脉冲:一记白热扩环
            if (burstT >= 0f) {
                float pulse = MathHelper.Clamp((burstT - 0.42f) / 0.58f, 0f, 1f);
                if (pulse > 0.001f && pulse < 0.999f) {
                    float grow = 1f - (1f - pulse) * (1f - pulse);
                    DrawRing(sb, c, size * (1.2f + grow * 4.6f), 2.4f * (1f - pulse) + 0.4f,
                        OnikiriUITheme.HotWhite * (alpha * (1f - pulse) * 0.85f), 44);
                    OniBrush.DrawSoftDot(sb, c, size * (2.2f + grow * 1.4f),
                        OnikiriUITheme.Bright, alpha * (1f - pulse) * 0.5f);
                }
            }
        }

        /// <summary>三印崩收束墨线:三槽各一道墨向心奔涌</summary>
        public static void DrawBurstThreads(SpriteBatch sb, in OniSigilWheel wheel,
            float burstT, float alpha, float time) {
            if (burstT < 0f || alpha <= 0.01f) {
                return;
            }
            Vector2 c = wheel.Center;
            for (int i = 0; i < OniSigilWheel.SlotCount; i++) {
                float t = MathHelper.Clamp((burstT - 0.12f - i * 0.07f) / 0.36f, 0f, 1f);
                if (t <= 0.001f || t >= 0.999f) {
                    continue;
                }
                Vector2 from = wheel.SlotPos(i);
                float ease = t * t * (3f - 2f * t);
                Vector2 tip = Vector2.Lerp(from, c, ease);
                OniBrush.DrawGradientLine(sb, from, tip,
                    OnikiriUITheme.Deep * (alpha * 0.4f),
                    OnikiriUITheme.Bright * (alpha * 0.9f), 2.6f);
                OniBrush.DrawSoftDot(sb, tip, 4.6f, OnikiriUITheme.HotWhite, alpha * 0.8f);
            }
        }

        /// <summary>双拍心跳包络 0~1,收缩期短舒张期长</summary>
        private static float HeartBeat(float time) {
            float f = time * 0.55f;
            f -= MathF.Floor(f);
            float b1 = MathF.Exp(-(f - 0.10f) * (f - 0.10f) * 340f);
            float b2 = MathF.Exp(-(f - 0.26f) * (f - 0.26f) * 300f) * 0.62f;
            return MathHelper.Clamp(b1 + b2, 0f, 1f);
        }

        //====================== 持印在手 ======================

        /// <summary>
        /// 拾在手上的役鬼印:随光标带惯性,身后两帧残影,鬼影贴印同行;
        /// press&gt;0 时印被按去 pressPos(候令压印)
        /// </summary>
        public static void DrawCarriedSeal(SpriteBatch sb, Vector2 pos, Vector2 vel,
            OniGhostEntry entry, float size, float ease, float press, Vector2 pressPos,
            float alpha, float time) {
            if (entry == null || ease <= 0.01f || alpha <= 0.01f) {
                return;
            }
            int seed = OniBrush.SealSeedFromKey(entry.Key);
            Vector2 drawPos = press > 0.01f ? Vector2.Lerp(pos, pressPos, press) : pos;
            float scale = size * (0.82f + 0.18f * ease) * (1f - press * 0.12f);
            float rot = MathF.Sin(time * 2.6f) * 0.05f
                + MathHelper.Clamp(vel.X * 0.006f, -0.16f, 0.16f);

            //残影:身后两枚渐淡
            if (press < 0.5f) {
                for (int i = 1; i <= 2; i++) {
                    Vector2 ghostPos = drawPos - vel * (i * 2.4f);
                    OniBrush.DrawSealGlyphSeeded(sb, ghostPos, scale * (1f - i * 0.06f),
                        alpha * ease * 0.16f / i, seed, rot, 0.5f);
                }
            }
            //鬼影贴着印走:一缕小影浮在印上方
            if (OniGhostShadowDraw.Available && press < 0.7f) {
                Rectangle shadowRect = new((int)(drawPos.X - size * 1.1f),
                    (int)(drawPos.Y - size * 2.6f), (int)(size * 2.2f), (int)(size * 2.6f));
                OniGhostShadowDraw.Draw(sb, shadowRect, new OniGhostShadowParams {
                    Writhe = 0.7f,
                    Break = 0.45f,
                    EyeOpen = 0.55f,
                    Glance = Vector2.Zero,
                    Seed = OniGhostShadowDraw.SeedFromKey(entry.Key),
                    Alpha = alpha * ease * 0.34f * (1f - press),
                    Time = time,
                });
            }
            //印影 + 印体
            OniBrush.DrawSealGlyphSeeded(sb, drawPos + new Vector2(2.2f, 3.2f), scale,
                alpha * ease * 0.30f, seed, rot, 0.2f);
            OniBrush.DrawSealGlyphSeeded(sb, drawPos, scale, alpha * ease, seed, rot);
        }

        /// <summary>飞回环位的印(卸下/被换下),一道抛物淡出</summary>
        public static void DrawFlyBackSeal(SpriteBatch sb, string key, Vector2 from, Vector2 to,
            float t, float size, float alpha) {
            if (string.IsNullOrEmpty(key) || t <= 0f || t >= 1f || alpha <= 0.01f) {
                return;
            }
            float ease = 1f - (1f - t) * (1f - t);
            Vector2 pos = Vector2.Lerp(from, to, ease);
            //一点上抛弧度,像被手掷回
            pos.Y -= MathF.Sin(t * MathHelper.Pi) * 26f;
            int seed = OniBrush.SealSeedFromKey(key);
            OniBrush.DrawSealGlyphSeeded(sb, pos, size * (1f - t * 0.14f),
                alpha * (1f - t * 0.35f), seed, (t - 0.5f) * 0.5f, 0.9f);
        }

        //====================== 墨线与批注 ======================

        /// <summary>活的虚线墨/鬼火线:缓移虚段+微摆,预演与邀请共用</summary>
        public static void DrawDashedLine(SpriteBatch sb, Vector2 p0, Vector2 p1,
            Color colFrom, Color colTo, float alpha, float time, float phase) {
            Vector2 edge = p1 - p0;
            float len = edge.Length();
            if (len < 6f || alpha <= 0.01f) {
                return;
            }
            Vector2 dir = edge / len;
            Vector2 perp = new(dir.Y, -dir.X);
            int seg = Math.Max(6, (int)(len / 15f));
            float drift = time * 0.55f + phase;
            drift -= MathF.Floor(drift);
            float cell = 1f / seg;
            for (int i = 0; i < seg; i++) {
                float uA = (i + drift) * cell;
                if (uA >= 1f) {
                    uA -= 1f;
                }
                float uB = MathF.Min(uA + cell * 0.55f, 1f);
                if (uB - uA < 0.004f) {
                    continue;
                }
                float wob = MathF.Sin(time * 2.4f + i * 1.7f + phase) * 1.3f;
                Vector2 a = p0 + edge * uA + perp * wob;
                Vector2 b = p0 + edge * uB + perp * wob;
                Color col = Color.Lerp(colFrom, colTo, (uA + uB) * 0.5f);
                OniBrush.DrawGradientLine(sb, a, b, col * alpha, col * (alpha * 0.55f), 1.4f);
            }
        }

        /// <summary>
        /// 盘内批注:一句渐显渐隐的墨字,字下一道两端渐没的朱线——
        /// 结印回执写在盘上,不进聊天栏
        /// </summary>
        public static void DrawNote(SpriteBatch sb, DynamicSpriteFont font, Vector2 center,
            string text, float t01, Color col, float alpha) {
            if (string.IsNullOrEmpty(text) || t01 <= 0f || t01 >= 1f || alpha <= 0.01f) {
                return;
            }
            float inE = MathHelper.Clamp(t01 / 0.10f, 0f, 1f);
            inE = 1f - (1f - inE) * (1f - inE);
            float outE = 1f - MathHelper.Clamp((t01 - 0.72f) / 0.28f, 0f, 1f);
            float a = alpha * inE * outE;
            if (a <= 0.01f) {
                return;
            }
            const float Scale = 0.72f;
            Vector2 sizePx = font.MeasureString(text) * Scale;
            Vector2 pos = center - sizePx * 0.5f + new Vector2(0f, (1f - inE) * 6f);
            Utils.DrawBorderString(sb, text, pos, col * a, Scale);
            //字下一道朱线,两端渐没
            Vector2 mid = new(center.X, pos.Y + sizePx.Y + 2f);
            float half = sizePx.X * 0.5f + 9f;
            OniBrush.DrawGradientLine(sb, mid, mid - new Vector2(half, 0.6f),
                OnikiriUITheme.Deep * (a * 0.75f), OnikiriUITheme.Deep * 0f, 1.3f);
            OniBrush.DrawGradientLine(sb, mid, mid + new Vector2(half, -0.6f),
                OnikiriUITheme.Deep * (a * 0.75f), OnikiriUITheme.Deep * 0f, 1.3f);
        }

        //====================== 卷槽（去点鬼簿的门） ======================

        /// <summary>卷轴端面的纸涡(自己的 [-1,1] 小空间)</summary>
        private const string NicheSpiralD =
            "M 0.6 0.05 C 0.6 -0.52 -0.58 -0.52 -0.58 0.05 C -0.58 0.46 0.28 0.46 0.28 0.08";

        /// <summary>
        /// 盘座下缘凿出的卷槽,点鬼簿插在里面。<br/>
        /// 悬停语言是「抽卷」——卷自槽里升起一截、绳札松一分,不是图标变亮。<br/>
        /// 卷身按圆筒排明暗(烛在屏下:下缘受暖光,上缘沉),两端天地轴朱帽带受光点
        /// </summary>
        public static void DrawScrollNiche(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            float hover, float alpha, float time, string label) {
            if (alpha <= 0.01f) {
                return;
            }
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            Vector2 half = new(0.5f);
            float a = alpha * (0.9f + hover * 0.1f);
            //抽书行程:卷升起一截,槽口漏出的暖光跟着涨
            float pull = hover * OnikiriUITheme.CodexBookPull;

            //====槽体:凿进盘座的暗腔——内暗上缘/两壁沉影/受光下唇====
            Vector2 slotC = new(rect.Center.X, rect.Center.Y + 6f);
            Vector2 slotSize = new(rect.Width - 8f, rect.Height - 14f);
            sb.Draw(pixel, slotC, PixelSrc, Color.Black * (a * 0.84f), 0f, half,
                slotSize, SpriteEffects.None, 0f);
            sb.Draw(pixel, slotC - new Vector2(0f, slotSize.Y * 0.5f), PixelSrc,
                Color.Black * (a * 0.65f), 0f, half, new Vector2(slotSize.X, 2.4f),
                SpriteEffects.None, 0f);
            foreach (float wx in new[] { -slotSize.X * 0.5f + 1f, slotSize.X * 0.5f - 1f }) {
                sb.Draw(pixel, slotC + new Vector2(wx, 0f), PixelSrc,
                    Color.Black * (a * 0.5f), 0f, half, new Vector2(2f, slotSize.Y - 2f),
                    SpriteEffects.None, 0f);
            }
            sb.Draw(pixel, slotC + new Vector2(0f, slotSize.Y * 0.5f), PixelSrc,
                OnikiriUITheme.CandleWarm * (a * 0.14f), 0f, half, new Vector2(slotSize.X, 1f),
                SpriteEffects.None, 0f);

            //====卷身:和纸圆筒,按筒面排明暗(烛在屏下,下亮上沉),随抽书上移====
            Vector2 rollC = new(rect.Center.X, rect.Y + 20f - pull);
            float rollW = rect.Width - 26f;
            const float RollH = 22f;
            sb.Draw(pixel, rollC + new Vector2(1.2f, 1.8f), PixelSrc,
                new Color(8, 2, 5) * (a * 0.5f), 0f, half, new Vector2(rollW, RollH),
                SpriteEffects.None, 0f);
            //四段筒面:上棱沉→上腹→高光腹→下缘暖
            Span<(float f0, float f1, float lit, bool warm)> bands = [
                (0f, 0.22f, 0.62f, false),
                (0.22f, 0.52f, 0.82f, false),
                (0.52f, 0.80f, 1.0f, false),
                (0.80f, 1f, 0.90f, true),
            ];
            foreach ((float f0, float f1, float lit, bool warm) in bands) {
                Vector2 bandC = rollC + new Vector2(0f, RollH * (f0 + f1 - 1f) * 0.5f);
                Vector2 bandS = new(rollW, RollH * (f1 - f0) + 0.6f);
                Color bandCol = OnikiriUITheme.Paper * (a * 0.88f * lit);
                if (warm) {
                    bandCol = Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.CandleWarm, 0.4f)
                        * (a * 0.88f * lit);
                }
                sb.Draw(pixel, bandC, PixelSrc, bandCol, 0f, half, bandS, SpriteEffects.None, 0f);
            }
            //纸层两线,读得出这是卷起来的
            for (int i = -1; i <= 0; i++) {
                sb.Draw(pixel, rollC + new Vector2(0f, 2f + i * 7f), PixelSrc,
                    OnikiriUITheme.TextDim * (a * 0.26f), 0f, half, new Vector2(rollW - 6f, 1f),
                    SpriteEffects.None, 0f);
            }
            //两端天地轴:朱帽内芯沉一线,底缘受光点
            foreach (float capX in new[] { -rollW * 0.5f, rollW * 0.5f }) {
                Vector2 capC = rollC + new Vector2(capX, 0f);
                sb.Draw(pixel, capC, PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), 0f, half,
                    new Vector2(6f, RollH + 4f), SpriteEffects.None, 0f);
                sb.Draw(pixel, capC, PixelSrc, OnikiriUITheme.Dark * (a * 0.7f), 0f, half,
                    new Vector2(2.4f, RollH + 4f), SpriteEffects.None, 0f);
                sb.Draw(pixel, capC + new Vector2(0f, RollH * 0.5f - 1f), PixelSrc,
                    OnikiriUITheme.CandleWarm * (a * 0.35f), 0f, half, new Vector2(4.4f, 1.2f),
                    SpriteEffects.None, 0f);
            }
            //端面纸涡:只在抽出来一截时才看得见
            if (hover > 0.05f) {
                SvgPath spiral = SvgPathPen.Path(NicheSpiralD);
                if (spiral != null) {
                    SvgPathPen.Stroke(sb, spiral, rollC + new Vector2(rollW * 0.5f, 0f), 11f, 0f,
                        OnikiriUITheme.TextDim, 1.2f, a * hover * 0.8f);
                }
            }

            //====束带一匝 + 垂下的绳札,抽书时松一分====
            sb.Draw(pixel, rollC, PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), 0f, half,
                new Vector2(rollW * 0.34f, RollH + 2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, rollC + new Vector2(0f, -RollH * 0.5f + 1.2f), PixelSrc,
                Color.Black * (a * 0.30f), 0f, half, new Vector2(rollW * 0.34f, 1.2f),
                SpriteEffects.None, 0f);
            float sway = MathF.Sin(time * 1.1f) * (0.06f + hover * 0.06f);
            OniBrush.DrawGradientLine(sb, rollC + new Vector2(0f, 12f),
                rollC + new Vector2(sway * 16f, 12f + 14f + hover * 4f),
                OnikiriUITheme.Deep * (a * 0.85f), OnikiriUITheme.Dark * (a * 0.2f), 1.4f);

            //槽口暖光:抽出来一截时槽里漏出灯色
            if (hover > 0.03f) {
                OniBrush.DrawBacklight(sb, slotC - new Vector2(0f, 4f), rect.Width * 0.55f,
                    OnikiriUITheme.CandleWarm, a * hover * 0.16f);
            }

            //槽下荷札:卷的名字
            if (!string.IsNullOrEmpty(label)) {
                const float Scale = 0.56f;
                Vector2 size = font.MeasureString(label) * Scale;
                Utils.DrawBorderString(sb, label,
                    new Vector2(rect.Center.X - size.X * 0.5f, rect.Bottom + 2f),
                    Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, hover)
                        * (a * (0.7f + hover * 0.3f)), Scale);
            }
        }

        //====================== 基元 ======================

        /// <summary>折线圆环:1px 笔按段铺,避免同心 quad</summary>
        public static void DrawRing(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, int segments) {
            DrawArc(sb, center, radius, thickness, color, 0f, MathHelper.TwoPi, segments);
        }

        public static void DrawArc(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, float from, float to, int segments) {
            if (color.A == 0 && color == Color.Transparent || segments < 2 || radius <= 0.5f
                || to - from < 0.001f) {
                return;
            }
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            float step = (to - from) / segments;
            Vector2 prev = center + from.ToRotationVector2() * radius;
            for (int i = 1; i <= segments; i++) {
                Vector2 next = center + (from + step * i).ToRotationVector2() * radius;
                Vector2 seg = next - prev;
                float len = seg.Length();
                if (len > 0.01f) {
                    sb.Draw(pixel, prev, PixelSrc, color, seg.ToRotation(),
                        new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness),
                        SpriteEffects.None, 0f);
                }
                prev = next;
            }
        }

        /// <summary>扫线实心圆:2px 行填充(盘影与 CPU 回退盘底;粗边由厚环遮住)</summary>
        public static void DrawFilledCircle(SpriteBatch sb, Vector2 center, float radius, Color color) {
            Texture2D pixel = Pixel;
            if (pixel == null || radius < 2f) {
                return;
            }
            const float Step = 2f;
            for (float y = -radius; y <= radius; y += Step) {
                float halfW = MathF.Sqrt(MathF.Max(radius * radius - y * y, 0f));
                if (halfW < 0.6f) {
                    continue;
                }
                sb.Draw(pixel, new Vector2(center.X - halfW, center.Y + y), PixelSrc, color, 0f,
                    new Vector2(0f, 0.5f), new Vector2(halfW * 2f, Step + 0.4f),
                    SpriteEffects.None, 0f);
            }
        }

        private static void DrawCentered(SpriteBatch sb, DynamicSpriteFont font, string text,
            Vector2 center, Color color, float scale) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 size = font.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text, center - size * 0.5f, color, scale);
        }
    }
}
