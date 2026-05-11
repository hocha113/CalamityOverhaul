using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// 义体技能雷达的可视层 —— 纯绘制，状态全部从 <see cref="CyberwareSkillRadialController"/> 读取
    /// <list type="bullet">
    ///   <item>使用 <see cref="SHPCRenderer"/> 的底层弧形绘制原语，与 SHPC 系列 UI 视觉语言保持一致</item>
    ///   <item>扇区图标复用各义体物品的原版纹理，无需额外美术资源</item>
    ///   <item>悬停扇区的信息面板复用 <see cref="SHPCRenderer.DrawInfoPanel"/></item>
    /// </list>
    /// </summary>
    internal class CyberwareSkillRadialUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static CyberwareSkillRadialUI Instance => UIHandleLoader.GetUIHandleOfType<CyberwareSkillRadialUI>();

        public static LocalizedText StatusOn { get; private set; }
        public static LocalizedText HintHoldToCharge { get; private set; }
        public static LocalizedText HintReleaseToFire { get; private set; }
        public static LocalizedText HintToggle { get; private set; }
        public static LocalizedText HintInstant { get; private set; }
        public static LocalizedText HintNotReady { get; private set; }

        public override void SetStaticDefaults() {
            //本地化全部集中在这里，避免与具体义体的本地化文件混淆
            StatusOn = this.GetLocalization(nameof(StatusOn), () => "ON");
            HintHoldToCharge = this.GetLocalization(nameof(HintHoldToCharge), () => "悬停蓄力，松开释放");
            HintReleaseToFire = this.GetLocalization(nameof(HintReleaseToFire), () => "松开按键释放");
            HintToggle = this.GetLocalization(nameof(HintToggle), () => "松开按键切换开关");
            HintInstant = this.GetLocalization(nameof(HintInstant), () => "松开按键立即触发");
            HintNotReady = this.GetLocalization(nameof(HintNotReady), () => "条件不满足");
        }

        //仅在玩家存活、有任意主动技能义体、且雷达进度大于阈值时显示
        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                    return false;
                }
                CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
                if (ctrl == null) {
                    return false;
                }
                //打开中 / 关闭过程中都需要继续绘制，等待 OpenProgress 自然归零
                return ctrl.IsOpen || ctrl.OpenProgress > 0.01f;
            }
        }

        public override void Update() {
            //本 UI 不做任何输入处理，所有状态由 Controller 提供
        }

        public override void Draw(SpriteBatch sb) {
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            if (ctrl == null) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            float a = MathHelper.Clamp(ctrl.OpenProgress, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            //每帧根据当前屏幕尺寸重算一次锚点：避开"PostUpdate / Draw 跨阶段窗口尺寸变化"
            //的潜在边界情况，且让 ScreenAnchor 永远与 sb 实际绘制坐标系对齐
            Vector2 center = new(
                Main.screenWidth * 0.5f,
                Main.screenHeight * CyberwareSkillRadialController.ScreenAnchorYRatio);
            ctrl.SetScreenAnchor(center);

            float time = ctrl.Time;

            //子弹时间的全屏滤镜：在雷达绘制之前先铺一层背景，让"世界凝固"的感官立刻成立
            //同时给雷达提供高对比的暗色底，避免雷达图形与花草背景颜色冲突而看不清
            DrawBulletTimeOverlay(sb, px, a);

            //雷达底盘：一个明显的圆形大底板，绝对不会与世界背景混色而被忽略
            //它的尺寸覆盖整个雷达活动范围（外缘 + 装饰环 + 安全边距）
            DrawRadialBackdrop(sb, px, center, a, time);

            //先尝试用专属着色器铺底（外圈刻度 / 中心虹膜 / 接口背板）
            //失败（未编译/未加载）时直接走纯 CPU 路径，雷达功能不受影响
            TryDrawShaderBackplate(sb, px, center, time, a);

            DrawCenterCore(sb, px, center, a, time);

            //每个扇区分别绘制
            int count = ctrl.Sectors.Count;
            for (int i = 0; i < count; i++) {
                ctrl.GetSectorAngles(i, out float aStart, out float aEnd);
                CyberwareSkillRadialSector sec = ctrl.Sectors[i];
                bool enabled = sec.Skill.IsReady;
                float fill = MathHelper.Clamp(sec.Skill.StatusFillRatio, 0f, 1f);

                //核心 → 扇区的辐射连接线
                float midA = (aStart + aEnd) * 0.5f;
                SHPCRenderer.DrawConnector(sb, px, center, midA, a, sec.HoverAmount, a);

                //扇区底板与状态弧填充
                SHPCRenderer.DrawSector(sb, px, center, aStart, aEnd, a,
                    sec.HoverAmount, 0f, enabled, fill,
                    glyph: string.Empty, time, a);

                //蓄力技能的额外亮带：在外缘叠加一道暖色高光以突出蓄力进度
                if (sec.Skill.Kind == CyberwareSkillKind.Charge && sec.Skill.RadialChargeRatio > 0.01f) {
                    DrawChargeOverlay(sb, px, center, aStart, aEnd, sec.Skill.RadialChargeRatio, a);
                }

                //Toggle 类已激活时叠一道金色环带，提示"开启中"
                if (sec.Skill.Kind == CyberwareSkillKind.Toggle && sec.Skill.IsActivated) {
                    DrawToggleActiveOverlay(sb, px, center, aStart, aEnd, time, a);
                }

                //物品图标，绘制在扇区中线上
                DrawItemIcon(sb, sec, center, midA, sec.HoverAmount, enabled, a);

                //右上角状态文字（"ON" / "12s" / "RAM 3" / 百分比等）
                DrawStatusText(sb, sec, center, midA, a);
            }

            //悬停信息面板：仅在悬停且雷达完全展开时显示，避开雷达扇区外侧
            DrawInfoPanelForHovered(sb, px, ctrl, center, a);

            //雷达边缘的视觉框架装饰
            DrawOuterRing(sb, px, center, count, a, time);
        }

        /// <summary>
        /// 子弹时间的全屏滤镜
        /// <list type="bullet">
        ///   <item>只在 <see cref="WorldFreezeSystem.IsActive"/> 时绘制（单人；多人无冻结故无滤镜）</item>
        ///   <item>整体压暗 + 顶/底两侧的暗角，让玩家视线被引导到中央偏下的雷达</item>
        ///   <item>叠一道极薄的水平扫描带，传递"系统介入"的赛博质感</item>
        ///   <item>淡入淡出严格跟随雷达 <paramref name="globalAlpha"/>，避免开/关瞬间硬切</item>
        /// </list>
        /// </summary>
        private static void DrawBulletTimeOverlay(SpriteBatch sb, Texture2D px, float globalAlpha) {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }

            int w = Main.screenWidth;
            int h = Main.screenHeight;

            //主体压暗层：均匀的深青色 + alpha，平摊整个屏幕
            //alpha 上限 0.42 既明显又不至于压死世界信息
            Color tint = new(8, 14, 22);
            sb.Draw(px, new Rectangle(0, 0, w, h), tint * (0.42f * globalAlpha));

            //顶/底各一道更深的暗角条带，按高度的 1/4 分布
            int bandH = h / 4;
            Color band = new(2, 6, 10);
            sb.Draw(px, new Rectangle(0, 0, w, bandH), band * (0.55f * globalAlpha));
            sb.Draw(px, new Rectangle(0, h - bandH, w, bandH), band * (0.55f * globalAlpha));

            //水平扫描带：极薄、低 alpha，随着 Main.GameUpdateCount 缓慢下移
            //冻结期间 GameUpdateCount 不会推进，因此这里改用本地控制器的 Time 推进
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            float t = ctrl != null ? ctrl.Time : 0f;
            float scanY = (t * 28f) % h;
            sb.Draw(px, new Rectangle(0, (int)scanY - 1, w, 2),
                SHPCTheme.CyanHi * (0.10f * globalAlpha));
            sb.Draw(px, new Rectangle(0, (int)scanY + 90, w, 1),
                SHPCTheme.Cyan * (0.06f * globalAlpha));
        }

        /// <summary>
        /// 雷达底盘：在雷达本体下方铺一块明显的圆形深色底板
        /// <br/>不依赖任何着色器，仅用 <see cref="SHPCRenderer.DrawArc"/> 系列原语，
        /// 即便着色器未加载也能让玩家一眼锁定雷达位置
        /// </summary>
        private static void DrawRadialBackdrop(SpriteBatch sb, Texture2D px,
            Vector2 center, float globalAlpha, float time) {
            //外边缘半径：覆盖雷达外缘 + 装饰环 + 一圈安全间距
            const float backdropR = SHPCTheme.ButtonOuterR + 28f;
            //中心半透明圆盘
            SHPCRenderer.DrawArc(sb, px, center, 0f, backdropR,
                0f, MathHelper.TwoPi, new Color(4, 12, 18) * (0.62f * globalAlpha));
            //稍小一圈的渐变中心，提高与世界的对比
            SHPCRenderer.DrawArc(sb, px, center, 0f, backdropR - 6f,
                0f, MathHelper.TwoPi, new Color(10, 26, 36) * (0.32f * globalAlpha));

            //边缘描边：双层渐变让底盘"立起来"，再叠一圈呼吸光晕
            float pulse = 0.65f + 0.35f * (MathF.Sin(time * 2.0f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center, backdropR, 0f, MathHelper.TwoPi,
                2.0f, SHPCTheme.Border * (0.85f * globalAlpha));
            SHPCRenderer.DrawArcStroke(sb, px, center, backdropR + 4f, 0f, MathHelper.TwoPi,
                1.0f, SHPCTheme.CyanHi * (0.45f * pulse * globalAlpha));

            //底部"基座"投影：让底盘像悬浮在 HUD 上
            SHPCRenderer.DrawArc(sb, px, center + new Vector2(0f, 4f),
                backdropR - 2f, backdropR + 6f, 0.10f, MathHelper.Pi - 0.10f,
                new Color(0, 4, 8) * (0.55f * globalAlpha));
        }

        /// <summary>
        /// 用义体专属着色器 <see cref="EffectLoader.CyberwareRadialPanel"/> 铺底
        /// <list type="bullet">
        ///   <item>渲染内容：中心虹膜 + 内/外弧描边 + 外圈拨码刻度 + 入场扩散动画</item>
        ///   <item>底纹 alpha 故意压低，让 CPU 扇区清晰叠加在上方</item>
        ///   <item>未加载（用户尚未编译 .fx 或资源加载失败）时直接 return，
        ///     雷达回退到纯 CPU 绘制；该方法应当在 CPU 扇区/图标绘制之前调用</item>
        ///   <item>SpriteBatch 状态恢复严格对齐 SHPCDialogueBox/HackRamRenderer 的范式</item>
        /// </list>
        /// </summary>
        private static bool TryDrawShaderBackplate(SpriteBatch sb, Texture2D px,
            Vector2 center, float time, float openProgress) {
            Effect effect = EffectLoader.CyberwareRadialPanel?.Value;
            if (effect == null) {
                return false;
            }

            //quad 范围：覆盖外圈刻度环（OuterR + 12）再加 20px 安全边距，
            //保证旋转刻度、外缘辉光、虹膜旋转辐条都不会被裁剪
            const float decoExtend = 12f;
            const float padding = 20f;
            float halfSize = SHPCTheme.ButtonOuterR + decoExtend + padding;

            float qLeft = center.X - halfSize;
            float qTop = center.Y - halfSize;
            int qSize = (int)MathF.Ceiling(halfSize * 2f);
            if (qSize <= 0) {
                return true;
            }

            Rectangle dest = new((int)qLeft, (int)qTop, qSize, qSize);
            Vector2 relCenter = new(center.X - qLeft, center.Y - qTop);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(openProgress);
            effect.Parameters["uOpenProgress"]?.SetValue(openProgress);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(qSize, qSize));
            effect.Parameters["uCenter"]?.SetValue(relCenter);
            effect.Parameters["uInnerR"]?.SetValue(SHPCTheme.ButtonInnerR);
            effect.Parameters["uOuterR"]?.SetValue(SHPCTheme.ButtonOuterR);
            effect.Parameters["uDeadZoneR"]?.SetValue(CyberwareSkillRadialController.DeadZoneRadius);
            effect.Parameters["uDecoOuterR"]?.SetValue(SHPCTheme.ButtonOuterR + decoExtend);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, dest, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            return true;
        }

        /// <summary>
        /// 中心小核心：单纯的视觉地标，告诉玩家"这是技能盘的圆心"
        /// </summary>
        private static void DrawCenterCore(SpriteBatch sb, Texture2D px, Vector2 center, float a, float time) {
            //柔和外晕
            SHPCRenderer.DrawDisc(sb, px, center,
                CyberwareSkillRadialController.DeadZoneRadius * 0.42f, 6f,
                SHPCTheme.Cyan * (0.18f * a));
            //核心圆点
            float breath = 0.85f + MathF.Sin(time * 4.2f) * 0.12f;
            SHPCRenderer.DrawDisc(sb, px, center,
                4.5f * breath, 2.5f,
                SHPCTheme.CyanHi * (0.85f * a));
            //十字微元素
            SHPCRenderer.DrawLine(sb, px,
                center - new Vector2(3.5f, 0f), center + new Vector2(3.5f, 0f),
                1.0f, SHPCTheme.SlotBg * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px,
                center - new Vector2(0f, 3.5f), center + new Vector2(0f, 3.5f),
                1.0f, SHPCTheme.SlotBg * (0.9f * a));
        }

        /// <summary>
        /// 蓄力进度高光带：沿扇区外缘按比例覆盖一道暖色弧线，模拟"蓄能上膛"
        /// </summary>
        private static void DrawChargeOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float ratio, float globalAlpha) {
            float fillEnd = MathHelper.Lerp(aStart, aEnd, MathHelper.Clamp(ratio, 0f, 1f));
            //外缘的金色高亮
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 1.5f, aStart, fillEnd,
                2.2f, SHPCTheme.Accent * (0.85f * globalAlpha));
            //内部柔光，强化蓄力的能量感
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 4f, aStart, fillEnd,
                3.5f, SHPCTheme.Accent * (0.32f * globalAlpha));
            //蓄满后增加一圈呼吸闪烁
            if (ratio >= 0.999f) {
                float pulse = (MathF.Sin(Main.GameUpdateCount * 0.32f) + 1f) * 0.5f;
                SHPCRenderer.DrawArcStroke(sb, px, center,
                    SHPCTheme.ButtonOuterR + 1.2f, aStart, aEnd,
                    1.4f, SHPCTheme.Accent * (0.5f * pulse * globalAlpha));
            }
        }

        /// <summary>
        /// Toggle 类已激活的金色环带，强化"开启中"语义
        /// </summary>
        private static void DrawToggleActiveOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float time, float globalAlpha) {
            float pulse = 0.65f + 0.35f * (MathF.Sin(time * 4.2f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 2.4f, aStart, aEnd,
                2.6f, SHPCTheme.Accent * (pulse * 0.7f * globalAlpha));
        }

        /// <summary>
        /// 绘制扇区中线上的物品图标，自适应缩放到 32x32 以下
        /// </summary>
        private static void DrawItemIcon(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float hoverAmt, bool enabled, float globalAlpha) {
            int type = sec.Skill.IconItemType;
            if (type <= 0) {
                return;
            }
            //安全：访问 TextureAssets 之前确认主线程已加载完成
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            //把图标放在按钮中点半径，给图标一点点跳出动效
            float jump = 1f + hoverAmt * 0.12f;
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            Vector2 iconCenter = center + dir * SHPCTheme.ButtonMidR;
            float maxDim = Math.Max(tex.Width, tex.Height);
            float scale = maxDim > 28f ? 28f / maxDim : 1f;
            scale *= jump;
            Color tint = enabled ? Color.White : SHPCTheme.Disabled;
            sb.Draw(tex, iconCenter, null, tint * globalAlpha, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 扇区右上角的状态文字（冷却剩余、RAM 消耗、ON 等）
        /// </summary>
        private static void DrawStatusText(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float globalAlpha) {
            string text = sec.Skill.StatusText;
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            //略外侧 + 偏切向，避免与图标重叠
            Vector2 tangent = new(-dir.Y, dir.X);
            Vector2 pos = center + dir * (SHPCTheme.ButtonOuterR - 8f) + tangent * 10f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text, pos - size * 0.5f,
                SHPCTheme.CyanHi * globalAlpha, 0.55f);
        }

        /// <summary>
        /// 悬停扇区的信息面板：始终把面板布置在雷达外侧，与扇区不重叠
        /// <br/>策略：根据玩家所在屏幕的左右半，把面板放到剩余空间更多的一侧；
        /// 垂直方向居中对齐雷达圆心。面板坐标通过反推 <see cref="SHPCRenderer.DrawInfoPanel"/>
        /// 内部的 "cursor + (18, 14)" 默认偏移得到
        /// </summary>
        private static void DrawInfoPanelForHovered(SpriteBatch sb, Texture2D px,
            CyberwareSkillRadialController ctrl, Vector2 center, float globalAlpha) {
            int idx = ctrl.HoveredIndex;
            if (idx < 0 || idx >= ctrl.Sectors.Count) {
                return;
            }
            CyberwareSkillRadialSector sec = ctrl.Sectors[idx];

            //SHPCRenderer.DrawInfoPanel 内部估算的极限尺寸（保守值，避免边缘溢出导致 auto-flip 误判）
            const float estPanelW = 230f;
            const float estPanelH = 130f;
            //雷达外缘与面板之间的最小安全间隙
            const float clearance = 14f;
            float radialEdge = SHPCTheme.ButtonOuterR + 6f;

            //哪一侧空间更多就放在哪一侧
            float spaceRight = Main.screenWidth - (center.X + radialEdge + clearance) - estPanelW;
            float spaceLeft = (center.X - radialEdge - clearance) - estPanelW;
            bool placeLeft = spaceLeft > spaceRight && spaceLeft > 0f;

            //目标面板左上角的屏幕坐标
            float panelX = placeLeft
                ? center.X - radialEdge - clearance - estPanelW
                : center.X + radialEdge + clearance;
            float panelY = center.Y - estPanelH * 0.5f;
            //如果展开还没到 100%，slide 偏移会让默认右下放置多偏移 (1-alpha)*8 像素，提前减掉
            float slide = (1f - globalAlpha) * 8f;

            //从目标 panelPos 反推应当传入的 cursor，让 DrawInfoPanel 内部的默认 (18+slide, 14) 自然命中
            Vector2 anchor = new(panelX - 18f - slide, panelY - 14f);
            //再做一次屏幕边界保护，避免 anchor 让面板被 clamp 后跨过雷达
            anchor.X = MathHelper.Clamp(anchor.X, 4f, Main.screenWidth - 4f);
            anchor.Y = MathHelper.Clamp(anchor.Y, 4f, Main.screenHeight - 4f);

            string subtitle = BuildSubtitle(sec.Skill);
            SHPCRenderer.DrawInfoPanel(sb, px, anchor, globalAlpha, globalAlpha,
                title: sec.Skill.DisplayName,
                subtitle: subtitle,
                description: sec.Skill.Description,
                statusText: sec.Skill.StatusText);
        }

        /// <summary>
        /// 根据技能类型构造副标题，例如"按住蓄力" / "切换开关" / "立即触发" / "条件不满足"
        /// </summary>
        private static string BuildSubtitle(CyberwareSkillBase skill) {
            if (!skill.IsReady) {
                return HintNotReady.Value;
            }
            return skill.Kind switch {
                CyberwareSkillKind.Charge => HintHoldToCharge.Value,
                CyberwareSkillKind.Toggle => HintToggle.Value,
                CyberwareSkillKind.Instant => HintInstant.Value,
                _ => string.Empty,
            };
        }

        /// <summary>
        /// 雷达外圈的整体描边装饰，仅在多扇区时绘制
        /// </summary>
        private static void DrawOuterRing(SpriteBatch sb, Texture2D px,
            Vector2 center, int sectorCount, float globalAlpha, float time) {
            if (sectorCount <= 1) {
                return;
            }
            //最外圈细线，配旋转刻度
            float ringR = SHPCTheme.ButtonOuterR + 5f;
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR,
                0f, MathHelper.TwoPi, 1.2f,
                SHPCTheme.Border * (0.6f * globalAlpha));
            //旋转刻度
            const int markCount = 8;
            float markSpan = 0.18f;
            float markGap = MathHelper.TwoPi / markCount;
            float markRot = time * 0.25f;
            for (int i = 0; i < markCount; i++) {
                float a0 = markRot + i * markGap - markSpan * 0.5f;
                SHPCRenderer.DrawArcStroke(sb, px, center, ringR + 4f,
                    a0, a0 + markSpan, 1.0f,
                    SHPCTheme.BorderHi * (0.5f * globalAlpha));
            }
        }
    }
}
