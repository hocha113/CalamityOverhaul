using CalamityOverhaul.Content.RAMSystems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇入面板渲染器(赛博朋克2077风格)
    /// <br/>选中目标后协议条目从屏幕右侧依次飞入，无边框侧栏
    /// <br/>叠加多层动画：CRT扫描线、色散分离、电路连接树、背景噪波等动态效果
    /// <br/>基于UI锚点和屏幕坐标
    /// </summary>
    internal class HackPanelRenderer
    {
        //每个槽位的飞入进度(0到1)
        private float[] slotFlyIn;
        //每个槽位的悬停动画值
        private float[] slotHoverAnim;
        //每个槽位的故障动画随机种子
        private float[] slotGlitchSeed;
        //各槽位的实际绘制矩形（用于悬停检测）
        private Rectangle[] slotRects;
        //当前悬停的槽位索引
        private int hoveredSlot = -1;
        //当前是否有悬停槽位（供外部查询）
        public bool HasHoveredSlot => hoveredSlot >= 0;
        //全局计时器
        private float timer;
        //是否显示
        private bool visible;
        //上传队列渲染器引用
        internal HackQueueRenderer Queue;
        //列表展开计时(秒)，Show()时重置
        private float revealTime;
        //全局故障带Y坐标（自顶向下扫描的横条，带色偏效果）
        private float glitchBandY;
        //故障带冷却计时
        private float glitchBandCooldown;
        //当前面板显示的目标类型
        private HackTargetKind currentTargetKind;
        //过滤后的协议索引映射（面板槽位→QuickHackDef全局索引）
        private readonly List<int> filteredIndices = [];
        //面板显示的协议数量（过滤后）
        private int displayCount;

        //===== 条目排版常量 =====
        private const float ItemWidth = 420f;
        private const float ItemHeight = 78f;
        private const float ItemGap = 5f;
        private const float RightMargin = 36f;
        //左侧斜切宽度（平行四边形的斜角效果）
        private const float SlashWidth = 22f;
        //电路连接树距离条目左边缘的偏移
        private const float TrunkOffsetX = 56f;
        //首个条目出现前的基础延迟(秒)
        private const float BaseEntryDelay = 0.2f;
        //每个条目之间的飞入间隔(秒)
        private const float EntryStagger = 0.07f;
        //协议列表整体下移偏移，避免RAM弧形HUD遮挡
        private const float TopPadding = 60f;
        //===== 字体尺寸 =====
        private static float FontName => 0.92f;
        private static float FontDesc => 0.74f;
        private static float FontIndex => 0.52f;
        private static float FontTime => 0.52f;
        private static float FontStatus => 0.48f;

        public void Show(HackTargetKind targetKind = HackTargetKind.Npc) {
            currentTargetKind = targetKind;
            QuickHackDef.GetFilteredIndices(targetKind, filteredIndices);
            displayCount = filteredIndices.Count;
            if (displayCount == 0) {
                Hide();
                return;
            }

            if (slotFlyIn == null || slotFlyIn.Length != displayCount) {
                slotFlyIn = new float[displayCount];
                slotHoverAnim = new float[displayCount];
                slotGlitchSeed = new float[displayCount];
                slotRects = new Rectangle[displayCount];
            }
            visible = true;
            hoveredSlot = -1;
            revealTime = 0f;
            glitchBandY = -100f;
            glitchBandCooldown = 0.5f;
            Array.Clear(slotFlyIn);
            Array.Clear(slotHoverAnim);
            for (int i = 0; i < slotGlitchSeed.Length; i++)
                slotGlitchSeed[i] = Main.rand.NextFloat() * 100f;
        }

        public void Hide() {
            visible = false;
            hoveredSlot = -1;
            //队列生命周期已独立于面板，退出时不清空，由CWRWorld全局驱动上传和消费
        }

        public void CancelUpload() {
            Queue?.Clear();
        }

        public void Update() {
            timer += 0.016f;

            if (!visible) {
                if (slotFlyIn == null) return;
                for (int i = 0; i < slotFlyIn.Length; i++)
                    slotFlyIn[i] = MathHelper.Lerp(slotFlyIn[i], 0f, 0.15f);
                revealTime = Math.Max(revealTime - 0.032f, 0f);
                return;
            }

            revealTime += 0.016f;

            //各条目依次飞入
            for (int i = 0; i < slotFlyIn.Length; i++) {
                float delay = BaseEntryDelay + i * EntryStagger;
                float elapsed = revealTime - delay;
                if (elapsed <= 0f) continue;
                float speed = 0.1f + elapsed * 0.25f;
                slotFlyIn[i] = MathHelper.Lerp(slotFlyIn[i], 1f, Math.Min(speed, 0.22f));
                if (slotFlyIn[i] > 0.995f) slotFlyIn[i] = 1f;
            }

            //故障带更新（自顶向下的快速横扫，带色偏效果）
            glitchBandCooldown -= 0.016f;
            if (glitchBandCooldown <= 0f) {
                glitchBandY += 600f * 0.016f; //匀速下移
                float totalH = displayCount * (ItemHeight + ItemGap);
                float startY = (Main.screenHeight - totalH) * 0.5f + TopPadding;
                if (glitchBandY > startY + totalH + 50f) {
                    glitchBandY = startY - 50f;
                    glitchBandCooldown = 2f + Main.rand.NextFloat() * 3f; //随机间隔
                }
            }

            UpdateHover();
        }

        private void UpdateHover() {
            hoveredSlot = -1;
            int mx = Main.mouseX;
            int my = Main.mouseY;
            for (int i = 0; i < slotRects.Length; i++) {
                if (slotFlyIn[i] < 0.8f) continue;
                if (slotRects[i].Contains(mx, my)) {
                    //禁用状态的槽位不响应悬停
                    int globalIdx = GetGlobalIndex(i);
                    var hack = QuickHackDef.GetByIndex(globalIdx);
                    var qs = Queue?.GetSlotState(globalIdx, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                    bool disabled = hack != null && !RamSystem.CanAfford(hack.RamCost)
                        || qs != QueueSlotState.None;
                    if (!disabled) hoveredSlot = i;
                    break;
                }
            }

            for (int i = 0; i < slotHoverAnim.Length; i++) {
                float target = 0f;
                int gi = GetGlobalIndex(i);
                if (i == hoveredSlot) target = 1f;
                else if (Queue != null && Queue.GetSlotState(gi, HackTime.CurrentScanTarget) == QueueSlotState.Uploading) target = 0.5f;
                slotHoverAnim[i] = MathHelper.Lerp(slotHoverAnim[i], target, 0.2f);
            }
        }

        //面板槽位索引→QuickHackDef全局索引
        private int GetGlobalIndex(int displaySlot) {
            if (displaySlot >= 0 && displaySlot < filteredIndices.Count)
                return filteredIndices[displaySlot];
            return -1;
        }

        public void HandleClick() {
            if (!visible) return;
            if (hoveredSlot < 0 || Queue == null) return;

            int globalIdx = GetGlobalIndex(hoveredSlot);
            var hack = QuickHackDef.GetByIndex(globalIdx);
            if (hack == null) return;

            IHackTarget target = HackTime.CurrentScanTarget;
            if (target == null) return;
            if ((hack.SupportedTargets & target.TargetType.Kind) == 0) return;

            int actualCost = HackCostEvaluator.GetActualCost(hack, target);
            if (!RamSystem.CanAfford(actualCost)) return;

            bool enqueued = Queue.Enqueue(hack, globalIdx, target, actualCost);
            if (enqueued) {
                RamSystem.TryConsume(actualCost);
            }
        }

        public bool ContainsMouse() {
            if (!visible || slotRects == null || slotFlyIn == null) return false;
            int mx = Main.mouseX;
            int my = Main.mouseY;
            for (int i = 0; i < slotRects.Length; i++) {
                if (slotFlyIn[i] < 0.5f) continue;
                if (slotRects[i].Contains(mx, my)) return true;
            }
            return false;
        }

        #region 主绘制入口

        public void Draw(SpriteBatch sb) {
            HackTargetFrame.Draw(sb, timer);

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;
            if (slotFlyIn == null) return;

            DrawAmbientNoise(sb, px, alpha);
            DrawConnectorTree(sb, px, alpha);
            DrawItems(sb, px, alpha);
            DrawGlitchBand(sb, px, alpha);
            DrawStatusBar(sb, px, alpha);
        }

        #endregion

        #region 背景噪波纹理

        //在条目列表区域背后画微弱的水平噪波纹理，增加CRT质感
        private void DrawAmbientNoise(SpriteBatch sb, Texture2D px, float alpha) {
            bool anyVisible = false;
            for (int i = 0; i < slotFlyIn.Length; i++) {
                if (slotFlyIn[i] > 0.3f) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            float totalH = displayCount * (ItemHeight + ItemGap) - ItemGap;
            float startY = (Main.screenHeight - totalH) * 0.5f + TopPadding - 10f;
            float baseX = Main.screenWidth - RightMargin - ItemWidth - TrunkOffsetX - 20;
            float endX = Main.screenWidth - RightMargin + 10;
            float regionH = totalH + 20f;

            float noiseAlpha = alpha * 0.025f;
            //每隔几个像素画一条水平线，亮度随时间微变
            for (int dy = 0; dy < (int)regionH; dy += 3) {
                float seed = dy * 0.73f + timer * 8f;
                float brightness = MathF.Sin(seed) * 0.5f + 0.5f;
                if (brightness < 0.3f) continue;
                sb.Draw(px, new Rectangle((int)baseX, (int)(startY + dy), (int)(endX - baseX), 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.Accent * (noiseAlpha * brightness));
            }
        }

        #endregion

        #region 故障色偏带

        //自顶向下横扫的故障带，水平色偏+亮度异常
        private void DrawGlitchBand(SpriteBatch sb, Texture2D px, float alpha) {
            if (glitchBandCooldown > 0f) return;

            float bandH = 4f + MathF.Sin(timer * 30f) * 2f;
            float bandAlpha = alpha * 0.15f;
            float baseX = Main.screenWidth - RightMargin - ItemWidth - TrunkOffsetX - 10;
            float endX = Main.screenWidth - RightMargin + 5;

            //主色偏带（主色条）
            sb.Draw(px, new Rectangle((int)(baseX + 3), (int)glitchBandY, (int)(endX - baseX), (int)bandH),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * bandAlpha);
            //偏移红色伪影
            sb.Draw(px, new Rectangle((int)(baseX - 2), (int)(glitchBandY + 1), (int)(endX - baseX), (int)(bandH * 0.5f)),
                new Rectangle(0, 0, 1, 1), new Color(200, 30, 60) * (bandAlpha * 0.4f));
        }

        #endregion

        #region 电路连接树

        private void DrawConnectorTree(SpriteBatch sb, Texture2D px, float alpha) {
            if (HackTime.CurrentScanTarget == null) return;

            float totalH = displayCount * (ItemHeight + ItemGap) - ItemGap;
            float listStartY = (Main.screenHeight - totalH) * 0.5f + TopPadding;
            float baseX = Main.screenWidth - RightMargin - ItemWidth;
            float trunkX = baseX - TrunkOffsetX;
            Vector2 screenCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

            float wireProgress = Math.Clamp(revealTime * 3f, 0f, 1f);
            float wireAlpha = alpha * wireProgress * 0.45f;
            Color wireColor = HackTheme.Accent * wireAlpha;

            //主干水平线
            float hLineEnd = MathHelper.Lerp(screenCenter.X, trunkX, EaseOutCubic(wireProgress));
            DrawLine(sb, px, screenCenter, new Vector2(hLineEnd, screenCenter.Y), 1.5f, wireColor);

            //水平线节点
            float nodeSpacing = 36f;
            for (float nx = screenCenter.X + nodeSpacing; nx < hLineEnd; nx += nodeSpacing) {
                float nodePulse = MathF.Sin(timer * 4f + nx * 0.02f) * 0.3f + 0.7f;
                sb.Draw(px, new Vector2(nx - 1.5f, screenCenter.Y - 1.5f),
                    new Rectangle(0, 0, 1, 1), HackTheme.Accent * (wireAlpha * 0.5f * nodePulse),
                    0, Vector2.Zero, 3f, SpriteEffects.None, 0);
            }

            //垂直干线
            if (wireProgress > 0.3f) {
                float trunkProg = Math.Clamp((wireProgress - 0.3f) / 0.7f, 0f, 1f);
                float trunkTop = MathHelper.Lerp(screenCenter.Y, listStartY + ItemHeight * 0.5f, trunkProg);
                float trunkBot = MathHelper.Lerp(screenCenter.Y, listStartY + totalH - ItemHeight * 0.5f, trunkProg);
                DrawLine(sb, px, new Vector2(trunkX, trunkTop), new Vector2(trunkX, trunkBot), 1.5f, wireColor * 0.7f);

                //分支线 + 节点
                for (int i = 0; i < displayCount; i++) {
                    float fly = slotFlyIn[i];
                    if (fly < 0.05f) continue;
                    float itemCY = listStartY + i * (ItemHeight + ItemGap) + ItemHeight * 0.5f;
                    float branchEnd = MathHelper.Lerp(trunkX, baseX - 4, fly);
                    Color branchColor = wireColor * 0.5f;
                    if (i == hoveredSlot) branchColor = HackTheme.Accent * (wireAlpha * 1f);
                    int gi = GetGlobalIndex(i);
                    if (Queue != null) {
                        var qs = Queue.GetSlotState(gi, HackTime.CurrentScanTarget);
                        if (qs == QueueSlotState.Uploading) branchColor = HackTheme.Uploading * (wireAlpha * 0.8f);
                        else if (qs == QueueSlotState.Queued) branchColor = HackTheme.Uploading * (wireAlpha * 0.4f);
                    }
                    DrawLine(sb, px, new Vector2(trunkX, itemCY), new Vector2(branchEnd, itemCY), 1f, branchColor);

                    //分支节点（菱形感，两点）
                    sb.Draw(px, new Vector2(trunkX - 2, itemCY - 2),
                        new Rectangle(0, 0, 1, 1), branchColor * 2f,
                        0, Vector2.Zero, 4f, SpriteEffects.None, 0);
                    sb.Draw(px, new Vector2(trunkX - 1, itemCY - 1),
                        new Rectangle(0, 0, 1, 1), HackTheme.BgDarkest,
                        0, Vector2.Zero, 2f, SpriteEffects.None, 0);

                    //分支末端小箭头
                    sb.Draw(px, new Vector2(branchEnd - 1, itemCY - 2),
                        new Rectangle(0, 0, 1, 1), branchColor * 1.2f,
                        0, Vector2.Zero, new Vector2(2, 4), SpriteEffects.None, 0);
                }
            }

            //流动数据光点沿电路移动（三个光点循环）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                for (int d = 0; d < 3; d++) {
                    float flowT = (timer * 0.6f + d * 0.33f) % 1f;
                    Vector2 flowPos;
                    if (flowT < 0.5f) {
                        float t = flowT / 0.5f;
                        flowPos = Vector2.Lerp(screenCenter, new Vector2(trunkX, screenCenter.Y), t);
                    }
                    else {
                        float t = (flowT - 0.5f) / 0.5f;
                        float tTop = listStartY + ItemHeight * 0.5f;
                        float tBot = listStartY + totalH - ItemHeight * 0.5f;
                        flowPos = new Vector2(trunkX, MathHelper.Lerp(tTop, tBot, t));
                    }
                    float dotIntensity = 1f - d * 0.25f;
                    Color dotGlow = HackTheme.Accent * (alpha * 0.3f * dotIntensity);
                    dotGlow.A = 0;
                    sb.Draw(glow, flowPos, null, dotGlow, 0, glow.Size() / 2, 0.08f, SpriteEffects.None, 0);
                }
            }

            //上传时在对应分支上有动态脉冲
            if (Queue != null && glow != null && wireProgress > 0.3f) {
                for (int i = 0; i < displayCount; i++) {
                    int gi = GetGlobalIndex(i);
                    if (Queue.GetSlotState(gi, HackTime.CurrentScanTarget) != QueueSlotState.Uploading) continue;
                    float itemCY = listStartY + i * (ItemHeight + ItemGap) + ItemHeight * 0.5f;
                    float pulseT = timer * 2f % 1f;
                    float pulseX = MathHelper.Lerp(trunkX, baseX - 4, pulseT);
                    Color pulseCol = HackTheme.Uploading * (alpha * 0.4f * (1f - pulseT));
                    pulseCol.A = 0;
                    sb.Draw(glow, new Vector2(pulseX, itemCY), null, pulseCol, 0, glow.Size() / 2, 0.06f, SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 协议条目列表

        private void DrawItems(SpriteBatch sb, Texture2D px, float alpha) {
            float totalH = displayCount * (ItemHeight + ItemGap) - ItemGap;
            float startY = (Main.screenHeight - totalH) * 0.5f + TopPadding;
            float baseX = Main.screenWidth - RightMargin - ItemWidth;

            for (int i = 0; i < displayCount; i++) {
                float fly = slotFlyIn[i];
                if (fly < 0.01f) {
                    slotRects[i] = Rectangle.Empty;
                    continue;
                }

                int globalIdx = GetGlobalIndex(i);
                QuickHackDef hack = QuickHackDef.GetByIndex(globalIdx);
                float hover = slotHoverAnim[i];
                float y = startY + i * (ItemHeight + ItemGap);

                //禁用状态：RAM不足或该协议已在队列中，抑制悬停展开
                QueueSlotState queueState = Queue?.GetSlotState(globalIdx, HackTime.CurrentScanTarget) ?? QueueSlotState.None;
                bool slotDisabled = !RamSystem.CanAfford(hack.RamCost)
                    || queueState != QueueSlotState.None;
                if (slotDisabled) hover = 0f;

                //飞入偏移（弹性过冲）
                float flyOffset = (1f - EaseOutBack(fly)) * 400f;
                //故障动画
                float glitch = 0f;
                if (fly < 0.85f) {
                    float seed = slotGlitchSeed[i] + timer * 25f;
                    glitch = (MathF.Sin(seed) + MathF.Sin(seed * 2.7f) * 0.5f) * (1f - fly) * 16f;
                }

                float x = baseX + flyOffset + glitch;
                float hoverExpand = hover * 18f;
                float w = ItemWidth + hoverExpand;
                Rectangle rect = new((int)(x - hoverExpand), (int)y, (int)w, (int)ItemHeight);
                slotRects[i] = rect;

                float itemAlpha = alpha * Math.Min(fly * 2.5f, 1f);
                float queueProgress = Queue?.GetSlotProgress(globalIdx, HackTime.CurrentScanTarget) ?? 0f;

                DrawSingleItem(sb, px, itemAlpha, rect, hack, i, hover, queueState, queueProgress);
            }
        }

        private void DrawSingleItem(SpriteBatch sb, Texture2D px, float alpha, Rectangle rect,
            QuickHackDef hack, int index, float hover, QueueSlotState queueState, float queueProgress) {

            bool isUploading = queueState == QueueSlotState.Uploading;
            bool isQueued = queueState == QueueSlotState.Queued;
            bool isCompleted = queueState == QueueSlotState.Completed;
            //禁用状态：RAM不足 或 已在上传队列中
            bool isDisabled = !RamSystem.CanAfford(hack.RamCost)
                || isUploading || isQueued;

            //=== 背景（双层渐变） ===
            Color bgColor = Color.Lerp(HackTheme.BgSlot, HackTheme.BgSlotHover, hover * 0.6f);
            if (isDisabled) {
                //禁用时背景压暗并带红色偏移
                float redPulse = MathF.Sin(timer * 4f + index * 1.2f) * 0.15f + 0.85f;
                bgColor = Color.Lerp(HackTheme.BgDarkest, new Color(45, 8, 8), 0.35f * redPulse);
            }
            else if (isUploading) bgColor = Color.Lerp(bgColor, HackTheme.Uploading, 0.08f);
            else if (isQueued) bgColor = Color.Lerp(bgColor, HackTheme.Uploading, 0.03f);
            if (isCompleted) {
                float flash = MathF.Sin(timer * 10f) * 0.5f + 0.5f;
                bgColor = Color.Lerp(bgColor, HackTheme.Accent, flash * 0.15f);
            }
            //无限骇入：背景红色脉冲
            if (HackTime.InfiniteHack) {
                float rPulse = MathF.Sin(timer * 18f + slotGlitchSeed[index] * 5f) * 0.3f + 0.7f;
                bgColor = Color.Lerp(bgColor, HackTheme.Danger, 0.12f * rPulse);
            }
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.92f));
            //底部暗色渐变（条目底部1/3区域微微加暗）
            int gradH = rect.Height / 3;
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - gradH, rect.Width, gradH),
                new Rectangle(0, 0, 1, 1), HackTheme.BgSlotHover * (alpha * 0.25f));

            //斜角遮罩
            DrawSlashCut(sb, px, rect, alpha);

            //=== CRT扫描线叠加层 ===
            DrawCRTOverlay(sb, px, rect, alpha * 0.05f);

            //=== 内部顶部线高光（模拟光泽） ===
            Color highlightLine = Color.Lerp(HackTheme.Border, HackTheme.Accent, hover * 0.3f);
            sb.Draw(px, new Rectangle(rect.X + (int)SlashWidth + 4, rect.Y + 1, rect.Width - (int)SlashWidth - 8, 1),
                new Rectangle(0, 0, 1, 1), highlightLine * (alpha * 0.18f));

            //=== 左侧色条（加粗+呼吸脉动+辉光） ===
            Color catColor = HackTheme.CategoryColor(hack.Category);
            //禁用时色条变为暗红/灰
            if (isDisabled) {
                catColor = Color.Lerp(new Color(80, 25, 25), HackTheme.Danger, 0.3f);
            }
            //无限骇入：红色闪烁覆盖
            else if (HackTime.InfiniteHack) {
                float rFlicker = MathF.Sin(timer * 15f + slotGlitchSeed[index] * 3f) * 0.35f
                    + MathF.Sin(timer * 23f + slotGlitchSeed[index] * 7f) * 0.15f + 0.5f;
                catColor = Color.Lerp(HackTheme.Danger, new Color(255, 60, 30), rFlicker * 0.3f);
            }
            float breathe = MathF.Sin(timer * 2.5f + index * 0.8f) * 0.15f + 0.85f;
            float barGlow = isUploading ? 1f : 0.45f + hover * 0.55f;
            barGlow *= breathe;
            int barX = rect.X + (int)SlashWidth;
            //色条主体（加粗到4px）
            sb.Draw(px, new Rectangle(barX, rect.Y + 3, 4, rect.Height - 6),
                new Rectangle(0, 0, 1, 1), catColor * (alpha * barGlow));
            //色条右侧的渐变消散（低透明度扩展）
            sb.Draw(px, new Rectangle(barX + 4, rect.Y + 3, 16, rect.Height - 6),
                new Rectangle(0, 0, 1, 1), catColor * (alpha * barGlow * 0.08f));
            //色条辉光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && barGlow > 0.4f) {
                Color barGlowCol = catColor * (alpha * barGlow * 0.12f);
                barGlowCol.A = 0;
                sb.Draw(glow, new Vector2(barX + 2, rect.Center.Y), null, barGlowCol,
                    0, glow.Size() / 2, new Vector2(0.06f, rect.Height / 35f), SpriteEffects.None, 0);
            }

            //=== 分类符号（色条旁的小标识符） ===
            string catSymbol = HackTheme.CategorySymbol(hack.Category);
            Color symColor = catColor * (alpha * (0.35f + hover * 0.3f));
            Utils.DrawBorderString(sb, catSymbol, new Vector2(barX + 8, rect.Y + rect.Height * 0.5f - 6), symColor, 0.40f);

            //=== 扫描线（悬停/上传/排队中时横扫） ===
            if (hover > 0.1f || isUploading || isQueued || HackTime.InfiniteHack) {
                float scanSpeed = HackTime.InfiniteHack ? 3.5f : isUploading ? 2.5f : 1.8f;
                float scanAlpha = HackTime.InfiniteHack ? 0.35f : isUploading ? 0.3f : isQueued ? 0.15f : hover * 0.22f;
                float scanPos = (timer * scanSpeed + index * 0.4f) % 1.4f - 0.2f;
                DrawScanLine(sb, px, rect, scanPos, alpha * scanAlpha);
            }

            //=== 边框层 ===
            Color borderCol;
            if (isDisabled) {
                float rBrd = MathF.Sin(timer * 5f + index * 1.5f) * 0.2f + 0.8f;
                borderCol = HackTheme.Danger * (0.4f * rBrd);
            }
            else if (isUploading)
                borderCol = Color.Lerp(HackTheme.Border, HackTheme.Uploading, 0.5f);
            else if (isQueued)
                borderCol = Color.Lerp(HackTheme.Border, HackTheme.Uploading, 0.25f);
            else
                borderCol = Color.Lerp(HackTheme.Border, HackTheme.Accent, hover * 0.5f);
            //无限骇入：边框红色闪烁
            if (HackTime.InfiniteHack) {
                float rBorder = MathF.Sin(timer * 20f + slotGlitchSeed[index] * 6f) * 0.3f + 0.7f;
                borderCol = Color.Lerp(borderCol, HackTheme.Danger, 0.5f * rBorder);
            }
            //顶边
            sb.Draw(px, new Rectangle(rect.X + (int)SlashWidth, rect.Y, rect.Width - (int)SlashWidth, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.55f));
            //底边
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.4f));
            //右边
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.35f));
            //斜边边线
            DrawSlashEdge(sb, px, rect, borderCol * (alpha * 0.5f));

            //=== 悬停角标（四角L形加亮指示符） ===
            if (hover > 0.15f) {
                DrawCornerAccents(sb, px, rect, alpha * hover, catColor);
            }

            //=== 悬停外发光 ===
            if (hover > 0.1f && glow != null) {
                Color slotGlow = catColor * (alpha * hover * 0.06f);
                slotGlow.A = 0;
                sb.Draw(glow, rect.Center.ToVector2(), null, slotGlow, 0,
                    glow.Size() / 2, new Vector2(rect.Width / 30f, rect.Height / 30f),
                    SpriteEffects.None, 0);
            }

            //=== 编号 ===
            string idxStr = $"{index + 1:D2}";
            Color idxColor = isDisabled
                ? HackTheme.Danger * (alpha * 0.25f)
                : Color.Lerp(HackTheme.TextNormal, catColor, hover * 0.6f) * (alpha * 0.6f);
            Vector2 idxPos = new(rect.X + SlashWidth + 10, rect.Y + 10);
            Utils.DrawBorderString(sb, idxStr, idxPos, idxColor, FontIndex);

            //=== 协议名称（带色散效果） ===
            float nameX = rect.X + SlashWidth + 48;
            float nameY = rect.Y + 8;
            Color nameColor;
            if (isDisabled) {
                //禁用时名称变为暗红灰
                nameColor = Color.Lerp(HackTheme.TextNormal, HackTheme.Danger, 0.4f) * 0.5f;
            }
            else {
                nameColor = Color.Lerp(HackTheme.TextBright, Color.White, hover * 0.3f);
                if (isUploading) nameColor = Color.Lerp(nameColor, HackTheme.Uploading, 0.35f);
                if (isQueued) nameColor = Color.Lerp(nameColor, HackTheme.Uploading, 0.15f);
                if (isCompleted) nameColor = HackTheme.Accent;
            }
            //无限骇入：名称红色闪烁
            if (HackTime.InfiniteHack) {
                float rName = MathF.Sin(timer * 12f + slotGlitchSeed[index] * 4.3f) * 0.25f + 0.75f;
                nameColor = Color.Lerp(nameColor, HackTheme.Danger, 0.5f * rName);
            }
            //色散（悬停时红偏移+蓝色叠加）
            if (hover > 0.2f) {
                float aberration = hover * 1.8f;
                Color redGhost = new Color(220, 40, 40) * (alpha * hover * 0.2f);
                Color blueGhost = new Color(40, 80, 220) * (alpha * hover * 0.2f);
                Utils.DrawBorderString(sb, hack.DisplayName.Value, new Vector2(nameX - aberration, nameY), redGhost, FontName);
                Utils.DrawBorderString(sb, hack.DisplayName.Value, new Vector2(nameX + aberration, nameY + 0.5f), blueGhost, FontName);
            }
            Utils.DrawBorderString(sb, hack.DisplayName.Value, new Vector2(nameX, nameY), nameColor * alpha, FontName);

            //=== 名称下方分隔点线 ===
            float sepY = rect.Y + 38;
            Color sepColor = isDisabled ? HackTheme.Danger * (alpha * 0.1f)
                : HackTheme.Border * (alpha * (0.15f + hover * 0.15f));
            for (float dx = 0; dx < rect.Width - SlashWidth - 60; dx += 8) {
                sb.Draw(px, new Rectangle((int)(rect.X + SlashWidth + 48 + dx), (int)sepY, 4, 1),
                    new Rectangle(0, 0, 1, 1), sepColor);
            }

            //=== 效果描述（第二行渐淡文字，超宽自动换行） ===
            Vector2 descPos = new(rect.X + SlashWidth + 48, rect.Y + 44);
            Color descColor = isDisabled
                ? HackTheme.TextNormal * 0.3f
                : Color.Lerp(HackTheme.TextNormal, HackTheme.TextBright, 0.3f + hover * 0.4f);
            //右侧数值/状态区域占用约95px，从描述起点到rect右侧内边距需扣除这部分防止压叠
            //Utils.WordwrapString按已缩放前的像素宽度分行，所以传入maxWidth需要除以缩放
            var descFont = FontAssets.MouseText.Value;
            int descWrapPx = Math.Max(32, (int)((rect.Right - descPos.X - 30f) / FontDesc));
            string[] descLines = Utils.WordwrapString(hack.Description.Value, descFont, descWrapPx, 2, out _);
            float descLineH = descFont.MeasureString("汉").Y * FontDesc;
            for (int li = 0; li < descLines.Length; li++) {
                if (string.IsNullOrEmpty(descLines[li])) continue;
                Utils.DrawBorderString(sb, descLines[li].TrimEnd('-', ' '),
                    new Vector2(descPos.X, descPos.Y + li * descLineH),
                    descColor * (alpha * 0.85f), FontDesc);
            }

            //=== 右侧状态区 ===
            if (isDisabled && !isUploading && !isQueued) {
                //纯RAM不足的禁用——显示闪烁红色LOCKED和RAM消耗
                float lockPulse = MathF.Sin(timer * 5f + index) * 0.25f + 0.75f;
                string lockStr = HackTime.Locked.Value;
                Vector2 lockSize = FontAssets.MouseText.Value.MeasureString(lockStr) * 0.50f;
                Vector2 lockPos = new(rect.Right - lockSize.X - 14,
                    rect.Y + (rect.Height - lockSize.Y) * 0.5f - 8);
                Utils.DrawBorderString(sb, lockStr, lockPos,
                    HackTheme.Danger * (alpha * 0.7f * lockPulse), 0.50f);
                //RAM消耗（LOCKED下方）
                int lockedActualCost = HackCostEvaluator.GetActualCost(hack, HackTime.CurrentScanTarget);
                string ramStr2 = BuildRamLabel(hack, lockedActualCost);
                Vector2 ramSize2 = FontAssets.MouseText.Value.MeasureString(ramStr2) * FontTime;
                Vector2 ramPos2 = new(rect.Right - ramSize2.X - 14, lockPos.Y + lockSize.Y + 2);
                Utils.DrawBorderString(sb, ramStr2, ramPos2,
                    HackTheme.Danger * (alpha * 0.5f * lockPulse), FontTime);
            }
            else if (isUploading) {
                DrawUploadIndicator(sb, px, alpha, rect, queueProgress);
            }
            else if (isCompleted) {
                float flash = MathF.Sin(timer * 8f) * 0.3f + 0.7f;
                string doneStr = HackTime.Done.Value;
                Vector2 okSize = FontAssets.MouseText.Value.MeasureString(doneStr) * 0.54f;
                Vector2 okPos = new(rect.Right - okSize.X - 14, rect.Y + (rect.Height - okSize.Y) * 0.5f);
                Utils.DrawBorderString(sb, doneStr, okPos, HackTheme.Accent * (alpha * flash), 0.54f);
            }
            else if (isQueued) {
                string queuedStr = HackTime.Queued.Value;
                Vector2 qSize = FontAssets.MouseText.Value.MeasureString(queuedStr) * 0.50f;
                Vector2 qPos = new(rect.Right - qSize.X - 14, rect.Y + (rect.Height - qSize.Y) * 0.5f);
                float qPulse = MathF.Sin(timer * 3f) * 0.15f + 0.85f;
                Utils.DrawBorderString(sb, queuedStr, qPos, HackTheme.Uploading * (alpha * 0.6f * qPulse), 0.50f);
            }
            else {
                //上传耗时（右上角）
                float sec = hack.UploadTime / 60f;
                string timeStr = $"{sec:F1}s";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(timeStr) * FontTime;
                Vector2 tp = new(rect.Right - ts.X - 14, rect.Y + 10);
                Utils.DrawBorderString(sb, timeStr, tp, HackTheme.TextNormal * (alpha * 0.55f), FontTime);

                //RAM消耗（右侧，上传耗时左边）
                int displayActualCost = HackCostEvaluator.GetActualCost(hack, HackTime.CurrentScanTarget);
                bool canAfford = RamSystem.CanAfford(displayActualCost);
                string ramStr = BuildRamLabel(hack, displayActualCost);
                Color ramColor = canAfford ? HackTheme.Accent : HackTheme.Danger;
                if (!canAfford) {
                    float ramPulse = MathF.Sin(timer * 6f) * 0.3f + 0.7f;
                    ramColor *= ramPulse;
                }
                Vector2 ramSize = FontAssets.MouseText.Value.MeasureString(ramStr) * FontTime;
                Vector2 ramPos = new(tp.X - ramSize.X - 12, rect.Y + 10);
                Utils.DrawBorderString(sb, ramStr, ramPos, ramColor * (alpha * 0.7f), FontTime);

                //分类标签（右下角）
                string catLabel = HackTheme.CategoryLabel(hack.Category);
                Vector2 cls = FontAssets.MouseText.Value.MeasureString(catLabel) * 0.38f;
                Vector2 clp = new(rect.Right - cls.X - 14, rect.Bottom - cls.Y - 8);
                Utils.DrawBorderString(sb, catLabel, clp, catColor * (alpha * 0.35f), 0.38f);
            }

            //=== 上传时底部进度光条（会穿过条目底边） ===
            if (isUploading) {
                int fillW = (int)(rect.Width * queueProgress);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, fillW, 2),
                    new Rectangle(0, 0, 1, 1), HackTheme.Uploading * (alpha * 0.5f));
                if (glow != null && fillW > 4) {
                    Color tipGlow = HackTheme.Uploading * (alpha * 0.25f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(rect.X + fillW, rect.Bottom - 1), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.12f, 0.02f), SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 视觉效果

        //构建 RAM 成本显示文字，倍率超过 1.0x 时附加倍率标签
        private static string BuildRamLabel(QuickHackDef hack, int actualCost) {
            if (actualCost == hack.RamCost) return $"{actualCost} RAM";
            float mult = (float)actualCost / hack.RamCost;
            return $"{actualCost} RAM ×{mult:F1}";
        }

        //左侧斜角遮罩
        private static void DrawSlashCut(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            Color mask = HackTheme.BgDarkest * (alpha * 0.95f);
            int slashW = (int)SlashWidth;
            for (int dy = 0; dy < rect.Height; dy++) {
                float t = (float)dy / rect.Height;
                int cutW = (int)(slashW * (1f - t));
                if (cutW > 0)
                    sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, cutW, 1),
                        new Rectangle(0, 0, 1, 1), mask);
            }
        }

        //斜边边线
        private static void DrawSlashEdge(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            Vector2 top = new(rect.X + SlashWidth, rect.Y);
            Vector2 bottom = new(rect.X, rect.Bottom);
            DrawLine(sb, px, top, bottom, 1f, color);
        }

        //CRT水平暗线叠加（每三像素的暗纹）
        private static void DrawCRTOverlay(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            Color line = HackTheme.BgDarkest * alpha;
            for (int dy = 0; dy < rect.Height; dy += 3) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, rect.Width, 1),
                    new Rectangle(0, 0, 1, 1), line);
            }
        }

        //悬停角标：四角的小L形加亮
        private static void DrawCornerAccents(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha, Color color) {
            int arm = 8;
            Color c = color * (alpha * 0.6f);
            //左上
            sb.Draw(px, new Rectangle(rect.X + (int)SlashWidth, rect.Y, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X + (int)SlashWidth, rect.Y, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //右上
            sb.Draw(px, new Rectangle(rect.Right - arm, rect.Y, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //左下
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - arm, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //右下
            sb.Draw(px, new Rectangle(rect.Right - arm, rect.Bottom - 1, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - arm, 1, arm), new Rectangle(0, 0, 1, 1), c);
        }

        //扫描线：一条竖线从左到右横穿条目
        private static void DrawScanLine(SpriteBatch sb, Texture2D px, Rectangle rect, float pos, float alpha) {
            int lineX = rect.X + (int)(rect.Width * pos);
            if (lineX < rect.X || lineX > rect.Right - 2) return;
            sb.Draw(px, new Rectangle(lineX, rect.Y + 1, 2, rect.Height - 2),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * alpha);
            //扫描辉光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color gc = HackTheme.Accent * (alpha * 0.6f);
                gc.A = 0;
                sb.Draw(glow, new Vector2(lineX, rect.Center.Y), null, gc, 0,
                    glow.Size() / 2, new Vector2(0.1f, rect.Height / 40f), SpriteEffects.None, 0);
            }
        }

        //上传进度指示器（右上角百分比+底部进度条+光点尾迹）
        private void DrawUploadIndicator(SpriteBatch sb, Texture2D px, float alpha, Rectangle rect, float progress) {
            int barW = 80;
            int barH = 5;
            int barX = rect.Right - barW - 14;
            int barY = rect.Bottom - barH - 8;

            //背景槽
            sb.Draw(px, new Rectangle(barX, barY, barW, barH),
                new Rectangle(0, 0, 1, 1), HackTheme.ProgressBg * alpha);
            int fillW = (int)(barW * progress);
            if (fillW > 0) {
                //填充
                sb.Draw(px, new Rectangle(barX, barY, fillW, barH),
                    new Rectangle(0, 0, 1, 1), HackTheme.ProgressFill * (alpha * 0.9f));
                //进度条前端辉光
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null && fillW > 2) {
                    Color tipGlow = HackTheme.ProgressGlow * (alpha * 0.4f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(barX + fillW, barY + barH * 0.5f), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.12f, 0.04f), SpriteEffects.None, 0);
                }
                //进度条内高光线
                sb.Draw(px, new Rectangle(barX, barY, fillW, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.TextBright * (alpha * 0.15f));
            }

            //百分比文字
            string pct = $"{(int)(progress * 100)}%";
            Vector2 ps = FontAssets.MouseText.Value.MeasureString(pct) * FontTime;
            Vector2 pp = new(rect.Right - ps.X - 10, rect.Y + 8);
            Utils.DrawBorderString(sb, pct, pp, HackTheme.Uploading * alpha, FontTime);
        }

        #endregion

        #region 底部状态栏

        private void DrawStatusBar(SpriteBatch sb, Texture2D px, float alpha) {
            bool anyVisible = false;
            for (int i = 0; i < slotFlyIn.Length; i++) {
                if (slotFlyIn[i] > 0.5f) { anyVisible = true; break; }
            }
            if (!anyVisible) return;

            float totalH = displayCount * (ItemHeight + ItemGap) - ItemGap;
            float startY = (Main.screenHeight - totalH) * 0.5f + TopPadding;
            float bottomY = startY + totalH + 14f;
            float baseX = Main.screenWidth - RightMargin - ItemWidth;

            //分隔线
            sb.Draw(px, new Rectangle((int)baseX, (int)(bottomY - 4), (int)ItemWidth, 1),
                new Rectangle(0, 0, 1, 1), HackTheme.Border * (alpha * 0.3f));

            //状态指示灯
            float pulse = (MathF.Sin(timer * 3.5f) + 1f) * 0.5f;
            bool hasActive = Queue != null && !Queue.IsEmpty;
            Color dotColor = hasActive
                ? Color.Lerp(HackTheme.Uploading * 0.4f, HackTheme.Uploading, pulse) * alpha
                : Color.Lerp(new Color(20, 100, 50), new Color(40, 200, 100), pulse) * alpha;
            sb.Draw(px, new Vector2(baseX, bottomY + 3),
                new Rectangle(0, 0, 1, 1), dotColor, 0, Vector2.Zero, 4f, SpriteEffects.None, 0);

            //指示灯辉光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color dglow = dotColor * 0.15f;
                dglow.A = 0;
                sb.Draw(glow, new Vector2(baseX + 2, bottomY + 5), null, dglow,
                    0, glow.Size() / 2, 0.04f, SpriteEffects.None, 0);
            }

            //状态文本
            string status = hasActive ? HackTime.UploadingText.Value : HackTime.BreachReady.Value;
            if (Queue != null && Queue.HasCompleted) status = HackTime.UploadComplete.Value;
            Utils.DrawBorderString(sb, status, new Vector2(baseX + 14, bottomY),
                HackTheme.TextDim * alpha, FontStatus);

            //伪十六进制标签
            string tag = $"NET::0x{(int)(timer * 100) % 0xFFFF:X4}";
            Utils.DrawBorderString(sb, tag, new Vector2(baseX + ItemWidth - 110, bottomY),
                HackTheme.Accent * (alpha * 0.22f), 0.36f);

            //协议计数
            string countStr = HackTime.Protocols.Format(displayCount);
            Utils.DrawBorderString(sb, countStr, new Vector2(baseX + ItemWidth - 110, bottomY + 20),
                HackTheme.TextDim * (alpha * 0.25f), 0.34f);

            //右键取消提示
            if (HackTime.CurrentScanTarget != null) {
                string hint = HackTime.RightClickHint.Value;
                float hintPulse = MathF.Sin(timer * 1.8f) * 0.12f + 0.88f;
                Utils.DrawBorderString(sb, hint, new Vector2(baseX, bottomY + 22f),
                    HackTheme.TextNormal * hintPulse, 0.8f);
            }
        }

        #endregion

        #region 工具函数

        private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        #endregion
    }
}