using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 掌中风铃：鬼伞常驻 HUD（左下，持伞或领域激活时浮现）。
    /// 檐钩垂一只玻璃风铃，铃身盛着一小汪血湖——液面=涨水进度、晃荡=事件涌浪、
    /// 液中烬点=湖藏填充、整铃随形态浸染；常态无水时铃也不空：吹制玻璃质感
    /// （冠结/双壁/旋纹/封存气泡/暮色反射带）加烬萤（=湖藏）、凝露与潮痕内景。
    /// 短册纸条压一道墨字水印与朱印，其上挂三道冷却墨线
    /// （沉溺手=主色居左、梦中唤犬=烬红居右、血湖态中列=湖力金线、鬼雨态中列=重启冷青），
    /// 册底三点驻影小印是沉影盘编成的缩影（亮=驻席，暗=空席）。
    /// 点铃展开「湖畔村图」全画（任何域状态都响应）。
    /// </summary>
    internal class KikasaHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaHud Instance => UIHandleLoader.GetUIHandleOfType<KikasaHud>();

        public static LocalizedText ScrollName { get; private set; }
        public static LocalizedText OpenTag { get; private set; }

        public override void SetStaticDefaults() {
            ScrollName = this.GetLocalization(nameof(ScrollName), () => "湖畔村图");
            OpenTag = this.GetLocalization(nameof(OpenTag), () => "点击展开画卷");
        }

        //==================== 可见性 ====================

        private float appear;

        private static bool HoldingUmbrella(Player p) {
            Item item = p.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        private static bool WantVisible(Player p)
            => HoldingUmbrella(p) || p.GetModPlayer<KikasaDomainPlayer>().AnyActive;

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead || Main.dedServ) {
                    return false;
                }
                return WantVisible(p) || appear > 0.01f;
            }
        }

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上覆悬停名牌，下到短册尾
        float IBottomLeftHud.HudStackTopExtent => KikasaHudTheme.ChimeH * 0.5f + 34f;
        float IBottomLeftHud.HudStackBottomExtent => KikasaHudTheme.ChimeH * 0.5f + 8f;
        #endregion

        //====== 风铃内部布局（相对锚点=风铃中心，静止位） ======

        //檐钩（摆锤支点）
        private const float HookY = -51f;
        //铃身中心
        private const float BellY = -16f;
        //铃舌珠
        private const float ClapperY = 7f;
        //短册顶与尺寸
        private const float TanzakuY = 11f;
        private const float TanzakuW = 14f;
        private const float TanzakuH = 35f;

        //====== SVG 路径（归一 [-1,1]，A 弧不可用） ======

        //檐钩短枝：一段斜出的枝子，末端下弯成钩，钩尖收在 (0, 0.5)
        private const string BranchPath =
            "M -1 -0.5 Q -0.45 -0.72 0.1 -0.45 Q 0.55 -0.28 0.62 0.0 "
            + "M 0.62 0.0 Q 0.66 0.3 0.35 0.42 Q 0.12 0.5 0 0.5";

        //铃身轮廓（闭环，供巡行亮笔与缺编回退）：球肩 + 波口唇线
        private const string BellRimPath =
            "M -0.78 0.55 Q -1.02 -0.08 -0.56 -0.60 Q 0 -0.96 0.56 -0.60 "
            + "Q 1.02 -0.08 0.78 0.55 Q 0.4 0.63 0 0.60 Q -0.4 0.57 -0.78 0.55";

        //铃顶冠结：小玻璃冠盖 + 一道系绳箍带，接住吊绳
        private const string CrownPath =
            "M -1 0.6 Q -0.9 -0.25 0 -0.45 Q 0.9 -0.25 1 0.6 "
            + "M -0.55 0.05 Q 0 -0.18 0.55 0.05";

        //短册墨字：竖排草书一线的抽象水印（非可读字），冷却墨线仍覆其上
        private const string TanzakuInkPath =
            "M 0.04 -1 Q -0.20 -0.72 0.06 -0.48 Q 0.26 -0.30 -0.06 -0.10 "
            + "Q -0.26 0.04 0.08 0.22 M -0.04 0.42 Q 0.16 0.55 -0.02 0.72 "
            + "Q -0.14 0.84 0.06 0.98";

        /// <summary>自然锚点（风铃中心），未参与左下队列避让时的原始位置</summary>
        public static Vector2 NaturalAnchor => new(KikasaHudTheme.AnchorOffset.X,
            KikasaHudTheme.UIScreenH + KikasaHudTheme.AnchorOffset.Y);

        /// <summary>风铃中心锚点，经左下队列避让后的最终位</summary>
        public static Vector2 Anchor {
            get {
                KikasaHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        /// <summary>风铃整体命中矩形</summary>
        public static Rectangle ChimeRect {
            get {
                Vector2 anchor = Anchor;
                return new Rectangle(
                    (int)(anchor.X - KikasaHudTheme.ChimeW * 0.5f),
                    (int)(anchor.Y - KikasaHudTheme.ChimeH * 0.5f),
                    KikasaHudTheme.ChimeW, KikasaHudTheme.ChimeH);
            }
        }

        /// <summary>铃身静止中心（引导指环也认它）</summary>
        public static Vector2 BellAnchor => Anchor + new Vector2(0f, BellY);

        /// <summary>铃身矩形；「湖畔村图」自这里放大铺开</summary>
        public static Rectangle BellRect {
            get {
                Vector2 c = BellAnchor;
                int s = KikasaHudTheme.BellSize;
                return new Rectangle((int)(c.X - s * 0.5f), (int)(c.Y - s * 0.5f), s, s);
            }
        }

        //==================== 状态 ====================

        //事件搅一记涌浪（stir），涌浪推摆幅；读数交给画
        private float stir;
        private float swingT;
        private int lastVaultCount;
        private int lastMemoryType;
        private bool lastLakeReady;

        private bool hoverChime;
        private float hoverLerp;

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaDreamPlayer Dream => player.GetModPlayer<KikasaDreamPlayer>();

        //==================== 更新 ====================

        public override void Update() {
            Player p = player;
            bool want = WantVisible(p);
            appear = MathHelper.Clamp(appear + (want ? 0.06f : -0.06f), 0f, 1f);

            Vector2 anchor = Anchor;
            Size = new Vector2(KikasaHudTheme.ChimeW + 16f, KikasaHudTheme.ChimeH + 16f);
            DrawPosition = anchor - Size * 0.5f;
            UIHitBox = DrawPosition.GetRectangle(Size);

            //事件只在铃上搅一记涌浪，细节反馈都在大画里
            KikasaVaultPlayer vault = Vault;
            int vaultCount = vault.Stored.Count;
            int memoryType = p.GetModPlayer<KikasaServants.KikasaServantPlayer>().LastDrownedType;
            bool lakeReady = vault.LakeReady;
            if ((vaultCount != lastVaultCount || memoryType != lastMemoryType
                || (lakeReady && !lastLakeReady)) && appear > 0.1f) {
                stir = MathF.Max(stir, 0.6f);
            }
            lastVaultCount = vaultCount;
            lastMemoryType = memoryType;
            lastLakeReady = lakeReady;
            stir = MathHelper.Lerp(stir,
                Domain.Phase == KikasaDomainPhase.Opening
                || Domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.12f, 0.06f);

            //摆锤相位：水一搅，铃就荡
            swingT += 0.030f + MathHelper.Clamp(stir, 0f, 1f) * 0.055f;

            //悬停占鼠标；点击展开/收起画卷——任何域状态都响应
            Rectangle chime = ChimeRect;
            hoverChime = appear > 0.5f && chime.Contains(KikasaHudTheme.UIMouse.ToPoint());
            hoverLerp = MathHelper.Lerp(hoverLerp, hoverChime ? 1f : 0f, 0.15f);
            if (hoverChime) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    KikasaSceneUI scene = KikasaSceneUI.Instance;
                    if (scene != null) {
                        if (scene.IsOpen) {
                            scene.Close();
                        }
                        else {
                            scene.Open();
                        }
                    }
                }
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = appear;
            if (a < 0.01f) {
                return;
            }
            //画卷展开后风铃让位，免得铃与画同屏抢戏
            float sceneOpen = KikasaSceneUI.Instance?.OpenProgress ?? 0f;
            a *= 1f - MathHelper.Clamp(sceneOpen * 1.4f, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            KikasaDomainPlayer domain = Domain;
            float rain = KikasaSceneUI.EffectiveRain(domain);
            float rise = domain.AnyActive ? domain.RiseProgress : 0f;
            float lightGate = Vault.Stored.Count / (float)KikasaVaultPlayer.Capacity;
            float time = Main.GlobalTimeWrappedHourly;
            float stir01 = MathHelper.Clamp(stir, 0f, 1f);

            //浮现自下轻托
            Vector2 anchor = Anchor + new Vector2(0f, (1f - a) * 10f);
            Vector2 hook = anchor + new Vector2(0f, HookY);

            //摆角：铃身主摆，铃舌与短册滞后跟摆
            float amp = 0.045f + stir01 * 0.24f;
            float ang = MathF.Sin(swingT) * amp;
            float angC = MathF.Sin(swingT - 0.85f) * amp * 1.12f;
            float angT = MathF.Sin(swingT - 1.45f) * amp * 1.05f
                + MathF.Sin(time * 5.3f) * 0.012f;

            //支点旋转：静止位 y 偏移 → 摆后位置
            Vector2 Swing(float restY, float theta)
                => hook + new Vector2(0f, restY - HookY).RotatedBy(theta);
            Vector2 bellC = Swing(BellY, ang);
            Vector2 bellTop = Swing(BellY - 23f, ang);
            Vector2 clapper = Swing(ClapperY, angC);
            Vector2 tzTop = Swing(TanzakuY, angT);

            Color barCol = KikasaHudTheme.Void(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            Color glow = KikasaHudTheme.Glow(rain);
            Color dim = KikasaHudTheme.TextDim(rain);
            Texture2D px = VaultAsset.placeholder2.Value;

            //1 檐钩短枝（静，不随摆）：粗笔枝身 + 一线受光
            SvgPath branch = SvgPathPen.Path(BranchPath);
            Vector2 branchC = hook + new Vector2(0f, -8f);
            SvgPathPen.Stroke(spriteBatch, branch, branchC, 16f, 0f, barCol, 2.4f, a * 0.95f);
            SvgPathPen.Stroke(spriteBatch, branch, branchC, 16f, 0f, accent, 0.8f, a * 0.35f);

            //2 吊绳与铃舌（先画，玻璃罩在上面）：钩→铃顶→舌珠
            KikasaVaultRenderer.DrawLine(spriteBatch, hook, bellTop, 1.1f, dim * (0.55f * a));
            KikasaVaultRenderer.DrawLine(spriteBatch, bellTop, clapper, 1f, dim * (0.4f * a));
            spriteBatch.Draw(px, clapper, null, barCol * a, MathHelper.PiOver4,
                px.Size() * 0.5f, new Vector2(4.5f / px.Width, 4.5f / px.Height),
                SpriteEffects.None, 0f);

            //2.5 铃顶冠结：冠盖骑在铃肩上收住吊绳，冠顶一粒系结（缘光稍后压住冠脚）
            SvgPath crown = SvgPathPen.Path(CrownPath);
            Vector2 crownC = Swing(BellY - 21.5f, ang);
            SvgPathPen.Stroke(spriteBatch, crown, crownC, 4.6f, ang, barCol, 1.8f, a * 0.9f);
            SvgPathPen.Stroke(spriteBatch, crown, crownC, 4.6f, ang, accent, 0.7f, a * 0.35f);
            spriteBatch.Draw(px, Swing(BellY - 23.8f, ang), null, barCol * (0.9f * a),
                MathHelper.PiOver4, px.Size() * 0.5f,
                new Vector2(2.6f / px.Width, 2.6f / px.Height), SpriteEffects.None, 0f);

            //3 玻璃铃身（TechChime / 缺编回退）：先垫一枚随呼吸的衬光，
            //把暗玻璃从暗背景里托出来（under-layer，不作铃的本体）
            float glowBreath = KikasaHudTheme.Breath(time, 0.31f, 0.9f);
            SvgPathPen.SoftDot(spriteBatch, bellC, 26f, glow, (0.05f + glowBreath * 0.03f) * a);
            DrawBell(spriteBatch, bellC, ang, a, rain, rise, stir01, lightGate, domain, time,
                hoverLerp);

            //4 铃缘巡行亮笔：悬停/涌浪时一段亮笔沿铃缘走
            float runA = (0.10f + hoverLerp * 0.30f + MathF.Max(stir01 - 0.2f, 0f) * 0.3f) * a;
            if (runA > 0.03f) {
                SvgPath rim = SvgPathPen.Path(BellRimPath);
                SvgPathPen.StrokeRunner(spriteBatch, rim, bellC,
                    KikasaHudTheme.BellSize * 0.36f, ang, glow, 1.1f, runA,
                    time * 0.16f, 0.14f);
            }

            //5 短册纸条：纸底 + 边线 + 两道冷却墨线
            DrawTanzaku(spriteBatch, tzTop, angT, a, rain, domain, dim);

            //6 摆到头一记铃缘微光
            float peak = MathF.Abs(MathF.Sin(swingT));
            float glint = MathHelper.Clamp((peak - 0.94f) / 0.06f, 0f, 1f)
                * MathHelper.Clamp(stir01 * 2f - 0.3f, 0f, 1f);
            if (glint > 0.05f) {
                SvgPathPen.SoftDot(spriteBatch, Swing(BellY + 16f, ang), 7f, glow,
                    glint * 0.5f * a);
            }

            //7 悬停名牌：画名 + 展开提示
            if (hoverLerp > 0.05f) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                string name = ScrollName.Value;
                Vector2 nameSize = font.MeasureString(name) * 0.78f;
                float nameY = anchor.Y - KikasaHudTheme.ChimeH * 0.5f - 26f;
                Utils.DrawBorderString(spriteBatch, name,
                    new Vector2(anchor.X - nameSize.X * 0.5f, nameY),
                    KikasaHudTheme.Text(rain) * (hoverLerp * a), 0.78f);
                string tag = OpenTag.Value;
                Vector2 tagSize = font.MeasureString(tag) * 0.62f;
                Utils.DrawBorderString(spriteBatch, tag,
                    new Vector2(anchor.X - tagSize.X * 0.5f, nameY + nameSize.Y + 1f),
                    KikasaHudTheme.TextDim(rain) * (hoverLerp * a * 0.9f), 0.62f);
            }
        }

        /// <summary>铃身：TechChime 吹制玻璃质感 + 常驻内景（烬萤/凝露/潮痕）；
        /// 缺编回退 SVG 轮廓 + 液面一线</summary>
        private static void DrawBell(SpriteBatch sb, Vector2 center, float ang, float a,
            float rain, float fill, float stir01, float lightGate,
            KikasaDomainPlayer domain, float time, float hover) {
            Effect effect = EffectLoader.KikasaScene?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D px = VaultAsset.placeholder2.Value;
            int size = KikasaHudTheme.BellSize;

            if (effect != null && noise != null && effect.Techniques["TechChime"] != null) {
                effect.CurrentTechnique = effect.Techniques["TechChime"];
                effect.Parameters["uTime"]?.SetValue(time);
                effect.Parameters["uAlpha"]?.SetValue(a);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(size, size));
                effect.Parameters["uRain"]?.SetValue(rain);
                effect.Parameters["uStir"]?.SetValue(stir01);
                effect.Parameters["uBoil"]?.SetValue(domain.FlipBoil);
                effect.Parameters["uFlash"]?.SetValue(domain.FlipFlash);
                effect.Parameters["uLightGate"]?.SetValue(lightGate);
                effect.Parameters["uWaterY"]?.SetValue(MathHelper.Clamp(fill, 0f, 1f));
                effect.Parameters["uSwing"]?.SetValue(ang);
                effect.Parameters["uHover"]?.SetValue(hover);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                sb.Draw(px, center, null, Color.White, ang, px.Size() * 0.5f,
                    new Vector2(size / (float)px.Width, size / (float)px.Height),
                    SpriteEffects.None, 0f);
                KikasaVaultRenderer.RestoreUIBatch(sb);
                return;
            }

            //缺编回退：铃形轮廓两笔 + 世界水平的液面一线
            SvgPath rim = SvgPathPen.Path(BellRimPath);
            float scale = size * 0.36f;
            SvgPathPen.Stroke(sb, rim, center, scale, ang, KikasaHudTheme.Void(rain), 3.2f, a * 0.95f);
            SvgPathPen.Stroke(sb, rim, center, scale, ang, KikasaHudTheme.Accent(rain), 1f, a * 0.5f);
            if (fill > 0.03f) {
                float lv = MathHelper.Lerp(scale * 0.62f, scale * -0.32f, fill);
                Vector2 lp = center + new Vector2(0f, lv);
                float half = scale * 0.6f;
                KikasaVaultRenderer.DrawLine(sb, lp - new Vector2(half, 0f),
                    lp + new Vector2(half, 0f), 1.4f, KikasaHudTheme.Glow(rain) * (0.55f * a));
            }
        }

        /// <summary>短册：湿暗纸底 + 边线 + 下缘水痕；冷却读数化作纸上三道墨线</summary>
        private void DrawTanzaku(SpriteBatch sb, Vector2 top, float ang, float a,
            float rain, KikasaDomainPlayer domain, Color dim) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 dir = new Vector2(0f, 1f).RotatedBy(ang);
            Vector2 side = new Vector2(1f, 0f).RotatedBy(ang);
            Vector2 center = top + dir * (TanzakuH * 0.5f);

            //纸底（材料纯色底）与下缘浸润
            Color paper = Color.Lerp(new Color(56, 38, 32), new Color(34, 40, 44), rain);
            sb.Draw(px, center, null, paper * (0.92f * a), ang, px.Size() * 0.5f,
                new Vector2(TanzakuW / px.Width, TanzakuH / px.Height), SpriteEffects.None, 0f);
            sb.Draw(px, top + dir * (TanzakuH - 3.5f), null,
                KikasaHudTheme.Accent(rain) * (0.22f * a), ang, px.Size() * 0.5f,
                new Vector2(TanzakuW / px.Width, 7f / px.Height), SpriteEffects.None, 0f);

            //边线与顶端系结
            Color edge = dim * (0.35f * a);
            Vector2 halfW = side * (TanzakuW * 0.5f);
            KikasaVaultRenderer.DrawLine(sb, top - halfW, top - halfW + dir * TanzakuH, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top + halfW, top + halfW + dir * TanzakuH, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top - halfW, top + halfW, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top - halfW + dir * TanzakuH,
                top + halfW + dir * TanzakuH, 1f, edge * 0.8f);
            sb.Draw(px, top, null, dim * (0.6f * a), MathHelper.PiOver4,
                px.Size() * 0.5f, new Vector2(3f / px.Width, 3f / px.Height),
                SpriteEffects.None, 0f);

            //墨字水印与朱印：竖排草书一线沉在纸底，印落条尾略歪（手押的章不会正）；
            //冷却墨线更亮，仍覆其上作功能层
            SvgPath inkGlyph = SvgPathPen.Path(TanzakuInkPath);
            SvgPathPen.Stroke(sb, inkGlyph, top + dir * (TanzakuH * 0.46f), 12.5f, ang,
                dim, 1.1f, a * 0.20f);
            sb.Draw(px, top + dir * (TanzakuH - 6f), null,
                KikasaHudTheme.Accent(rain) * (0.40f * a), ang + 0.3f,
                px.Size() * 0.5f, new Vector2(3f / px.Width, 3f / px.Height),
                SpriteEffects.None, 0f);

            //冷却墨线：满=刚用完，退尽=可再用
            float run = TanzakuH - 8f;
            Vector2 inkTop = top + dir * 4f;
            float drownCd = KikasaDrown.LocalCooldown01;
            if (drownCd > 0.005f) {
                Vector2 off = -side * 2.6f;
                KikasaVaultRenderer.DrawLine(sb, inkTop + off,
                    inkTop + off + dir * (run * drownCd), 1.6f,
                    KikasaHudTheme.Glow(rain) * (0.6f * a));
            }
            if (domain.Phase == KikasaDomainPhase.Dreaming) {
                float houndCd = Dream.HoundCooldown01;
                if (houndCd > 0.005f) {
                    Vector2 off = side * 2.6f;
                    KikasaVaultRenderer.DrawLine(sb, inkTop + off,
                        inkTop + off + dir * (run * houndCd), 1.4f,
                        new Color(230, 96, 40) * (0.65f * a));
                }
            }
            //中列：鬼雨态给大范围重启冷青，血湖态给湖力金线（欠着多少画多少，
            //烧着/入梦后读得出「这汪水还差几口」）——两形态各占各的相位，不挤
            if (domain.IsRainForm) {
                float resetCd = KikasaReset.LocalCooldown01;
                if (resetCd > 0.005f) {
                    KikasaVaultRenderer.DrawLine(sb, inkTop,
                        inkTop + dir * (run * resetCd), 1.4f,
                        new Color(108, 190, 198) * (0.6f * a));
                }
            }
            else if (domain.AnyActive) {
                float vigorGap = 1f - MathHelper.Clamp(domain.LakeVigor, 0f, 1f);
                if (vigorGap > 0.005f) {
                    KikasaVaultRenderer.DrawLine(sb, inkTop,
                        inkTop + dir * (run * vigorGap), 1.4f,
                        KikasaWisps.KikasaWisp.GoldBody * (0.55f * a));
                }
            }

            //驻影小印：册底缘三点，沉影盘编成的缩影（亮=驻席、暗=空席）
            var servant = player.GetModPlayer<KikasaServants.KikasaServantPlayer>();
            Vector2 dotRow = top + dir * (TanzakuH - 10.5f);
            for (int i = 0; i < KikasaServants.KikasaServantPlayer.SlotCount; i++) {
                Vector2 pos = dotRow + side * ((i - 1) * 4.2f);
                bool filled = servant.SlotKeyAt(i) != 0;
                if (filled) {
                    float breath = KikasaHudTheme.Breath(Main.GlobalTimeWrappedHourly,
                        i * 2.7f, 1.6f);
                    SvgPathPen.SoftDot(sb, pos, 3.4f, KikasaHudTheme.Glow(rain),
                        (0.45f + breath * 0.25f) * a);
                }
                else {
                    SvgPathPen.SoftDot(sb, pos, 2.4f, dim, 0.20f * a);
                }
            }
        }
    }
}
