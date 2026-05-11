using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    //左侧骇入队列渲染器
    //显示已选择的骇入协议及其上传进度，模仿赛博朋克2077的左侧待执行队列
    internal class HackQueueRenderer
    {
        #region 布局常量

        private const float LeftMargin = 36f;
        private const float ItemWidth = 280f;
        private const float ItemHeight = 52f;
        private const float ItemGap = 4f;
        private const float SlashWidth = 14f;
        private const float BarHeight = 4f;
        private const float FontName = 0.62f;
        private const float FontPct = 0.38f;
        private const float FontStatus = 0.30f;
        //完成后闪烁持续时间（秒）
        private const float CompletedDuration = 1.2f;

        #endregion

        //队列数据
        private readonly List<HackQueueEntry> queue = new();
        //队列只读访问
        public IReadOnlyList<HackQueueEntry> Entries => queue;
        private float timer;

        #region 公共接口

        /// <summary>
        /// 向队列添加一个骇入协议
        /// <br/>不允许同一目标上的同一slot重复入队，但不同目标可以并行持有同一slot的骇入状态
        /// </summary>
        public bool Enqueue(QuickHackDef hack, int slotIndex, IHackTarget target) {
            if (target == null) return false;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (queue[i].Target != null && queue[i].Target.TargetEquals(target)) return false;
            }
            queue.Add(new HackQueueEntry(hack, slotIndex, target));
            return true;
        }

        //取消队列中指定slot的协议（任意目标，仅在显式取消场景使用）
        public void Cancel(int slotIndex) {
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (queue[i].SlotIndex == slotIndex) {
                    queue.RemoveAt(i);
                    break;
                }
            }
        }

        //取消队列中指定slot且作用于指定目标的协议
        public void Cancel(int slotIndex, IHackTarget target) {
            if (target == null) return;
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (queue[i].Target == null || !queue[i].Target.TargetEquals(target)) continue;
                queue.RemoveAt(i);
                break;
            }
        }

        //清空队列
        public void Clear() {
            queue.Clear();
        }

        //全局消费：处理所有已完成的条目，施加效果并移除
        //由全局更新调用，不依赖HackTimeUI的Active状态
        public void ConsumeAndApplyAll() {
            for (int i = queue.Count - 1; i >= 0; i--) {
                var entry = queue[i];
                //目标失效则移除
                if (!entry.IsTargetValid) {
                    queue.RemoveAt(i);
                    continue;
                }
                //上传完成且闪烁结束，由目标自身分派协议生效
                if (entry.State == HackQueueState.Completed && entry.CompletedTimer <= 0f) {
                    entry.Target.ApplyHack(entry.Hack, Main.LocalPlayer);
                    //多人模式下广播给其它客户端做视觉复刻；单人模式下方法内部会直接返回
                    HackTimeNetSync.SendApplyPacket(entry.Hack, entry.Target, Main.myPlayer);
                    queue.RemoveAt(i);
                }
            }
        }

        //查询某个hack slot在队列中的状态（重复时优先级：Uploading > Queued > Completed）
        public QueueSlotState GetSlotState(int slotIndex) {
            QueueSlotState best = QueueSlotState.None;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                var s = queue[i].State;
                if (s == HackQueueState.Uploading) return QueueSlotState.Uploading;
                if (s == HackQueueState.Waiting && best != QueueSlotState.Queued)
                    best = QueueSlotState.Queued;
                else if (s == HackQueueState.Completed && best == QueueSlotState.None)
                    best = QueueSlotState.Completed;
            }
            return best;
        }

        //查询指定slot针对指定目标的状态，target为空时退化为忽略目标的版本
        public QueueSlotState GetSlotState(int slotIndex, IHackTarget target) {
            if (target == null) return GetSlotState(slotIndex);
            QueueSlotState best = QueueSlotState.None;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (queue[i].Target == null || !queue[i].Target.TargetEquals(target)) continue;
                var s = queue[i].State;
                if (s == HackQueueState.Uploading) return QueueSlotState.Uploading;
                if (s == HackQueueState.Waiting && best != QueueSlotState.Queued)
                    best = QueueSlotState.Queued;
                else if (s == HackQueueState.Completed && best == QueueSlotState.None)
                    best = QueueSlotState.Completed;
            }
            return best;
        }

        //获取某个hack slot的上传进度（重复时取最活跃的那个）
        public float GetSlotProgress(int slotIndex) {
            float best = 0f;
            bool found = false;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                //优先返回Uploading的进度
                if (queue[i].State == HackQueueState.Uploading)
                    return queue[i].UploadProgress;
                if (!found || queue[i].UploadProgress > best) {
                    best = queue[i].UploadProgress;
                    found = true;
                }
            }
            return best;
        }

        //获取指定slot针对指定目标的上传进度
        public float GetSlotProgress(int slotIndex, IHackTarget target) {
            if (target == null) return GetSlotProgress(slotIndex);
            float best = 0f;
            bool found = false;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (queue[i].Target == null || !queue[i].Target.TargetEquals(target)) continue;
                if (queue[i].State == HackQueueState.Uploading)
                    return queue[i].UploadProgress;
                if (!found || queue[i].UploadProgress > best) {
                    best = queue[i].UploadProgress;
                    found = true;
                }
            }
            return best;
        }

        //消费已完成的队列头部（返回hack定义，调用方施加效果）
        public QuickHackDef ConsumeCompleted() {
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].State == HackQueueState.Completed && queue[i].CompletedTimer <= 0f) {
                    var hack = queue[i].Hack;
                    queue.RemoveAt(i);
                    return hack;
                }
            }
            return null;
        }

        //队列是否为空
        public bool IsEmpty => queue.Count == 0;

        //当前队列中是否有已完成待消费的协议
        public bool HasCompleted {
            get {
                for (int i = 0; i < queue.Count; i++) {
                    if (queue[i].State == HackQueueState.Completed && queue[i].CompletedTimer <= 0f)
                        return true;
                }
                return false;
            }
        }

        //获取指定NPC身上所有正在上传的队列条目
        public void GetEntriesForNPC(int npcIndex, List<HackQueueEntry> result) {
            result.Clear();
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].TargetIndex == npcIndex)
                    result.Add(queue[i]);
            }
        }

        //获取指定物块坐标上所有正在上传的队列条目
        public void GetEntriesForTile(int tileX, int tileY, List<HackQueueEntry> result) {
            result.Clear();
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].TargetKind == HackTargetKind.Tile
                    && queue[i].TileX == tileX && queue[i].TileY == tileY)
                    result.Add(queue[i]);
            }
        }

        //获取指定炮台上所有正在上传的队列条目
        public void GetEntriesForTurret(IHackableTurret turret, List<HackQueueEntry> result) {
            result.Clear();
            if (turret == null) return;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].TargetKind == HackTargetKind.Turret
                    && ReferenceEquals(queue[i].TurretTarget, turret))
                    result.Add(queue[i]);
            }
        }

        //获取指定信号塔上所有正在上传的队列条目
        public void GetEntriesForSignalTower(IHackableSignalTower tower, List<HackQueueEntry> result) {
            result.Clear();
            if (tower == null) return;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].TargetKind == HackTargetKind.SignalTower
                    && ReferenceEquals(queue[i].SignalTowerTarget, tower))
                    result.Add(queue[i]);
            }
        }

        //获取队列头部（正在上传或已完成的）的进度和状态，用于NPC头顶进度环
        public bool TryGetActiveEntry(out float progress, out bool completed) {
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].State == HackQueueState.Uploading) {
                    progress = queue[i].UploadProgress;
                    completed = false;
                    return true;
                }
                if (queue[i].State == HackQueueState.Completed) {
                    progress = 1f;
                    completed = true;
                    return true;
                }
            }
            progress = 0f;
            completed = false;
            return false;
        }

        //获取作用于指定目标的活跃条目（Uploading 优先，其次 Completed）的进度
        //仅返回与目标匹配的条目，避免多目标场景下把别的目标的进度画到当前选中头顶
        public bool TryGetActiveEntry(IHackTarget target, out float progress, out bool completed) {
            if (target != null) {
                bool foundCompleted = false;
                for (int i = 0; i < queue.Count; i++) {
                    var entry = queue[i];
                    if (entry.Target == null || !entry.Target.TargetEquals(target)) continue;
                    if (entry.State == HackQueueState.Uploading) {
                        progress = entry.UploadProgress;
                        completed = false;
                        return true;
                    }
                    if (entry.State == HackQueueState.Completed) {
                        foundCompleted = true;
                    }
                }
                if (foundCompleted) {
                    progress = 1f;
                    completed = true;
                    return true;
                }
            }
            progress = 0f;
            completed = false;
            return false;
        }

        #endregion

        #region 逻辑更新

        public void Update() {
            timer += 0.016f;

            //确保只有队列头部处于Uploading状态
            bool hasUploading = false;
            for (int i = 0; i < queue.Count; i++) {
                var entry = queue[i];

                //飞入动画
                if (entry.FlyIn < 1f) {
                    entry.FlyIn = MathHelper.Lerp(entry.FlyIn, 1f, 0.08f);
                    if (entry.FlyIn > 0.99f) entry.FlyIn = 1f;
                }

                //状态机
                switch (entry.State) {
                    case HackQueueState.Waiting:
                        //骇客时间中允许状态流转到Uploading（显示排队），但不推进进度
                        if (!hasUploading) {
                            entry.State = HackQueueState.Uploading;
                            hasUploading = true;
                        }
                        break;

                    case HackQueueState.Uploading:
                        hasUploading = true;
                        //仅在世界真正冻结时暂停上传进度
                        //单人模式下进入骇客时间会触发 WorldFreezeSystem，对应进度暂停由 IsActive 决定
                        //多人模式下不再冻结世界，上传与世界一同实时推进
                        if (!TimeFreezes.WorldFreezeSystem.IsActive) {
                            if (entry.Hack.UploadTime > 0)
                                entry.UploadProgress += 1f / entry.Hack.UploadTime;
                            if (entry.UploadProgress >= 1f) {
                                entry.UploadProgress = 1f;
                                entry.State = HackQueueState.Completed;
                                entry.CompletedTimer = CompletedDuration;
                            }
                        }
                        break;

                    case HackQueueState.Completed:
                        entry.CompletedTimer -= 0.016f;
                        break;
                }
            }
        }

        #endregion

        #region 绘制

        public void Draw(SpriteBatch sb) {
            if (queue.Count == 0) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;

            //标题
            DrawHeader(sb, alpha);

            //队列条目
            for (int i = 0; i < queue.Count; i++) {
                DrawQueueItem(sb, px, alpha, i, queue[i]);
            }
        }

        private void DrawHeader(SpriteBatch sb, float alpha) {
            float headerY = GetStartY() - 26f;
            string header = HackTime.UploadQueue.Value;
            Color headerColor = HackTheme.Accent * (alpha * 0.55f);
            Utils.DrawBorderString(sb, header, new Vector2(LeftMargin, headerY), headerColor, 0.34f);

            //队列计数
            string countStr = $"[{queue.Count}]";
            Vector2 countSize = FontAssets.MouseText.Value.MeasureString(countStr) * 0.28f;
            Utils.DrawBorderString(sb, countStr,
                new Vector2(LeftMargin + FontAssets.MouseText.Value.MeasureString(header).X * 0.34f + 8, headerY + 2),
                HackTheme.TextDim * (alpha * 0.4f), 0.28f);
        }

        private void DrawQueueItem(SpriteBatch sb, Texture2D px, float alpha, int index, HackQueueEntry entry) {
            float fly = entry.FlyIn;
            if (fly < 0.01f) return;

            float y = GetStartY() + index * (ItemHeight + ItemGap);
            //飞入偏移（从左侧滑入）
            float flyOffset = (1f - EaseOutCubic(fly)) * -300f;
            //完成态淡出
            float fadeAlpha = 1f;
            if (entry.State == HackQueueState.Completed) {
                fadeAlpha = Math.Clamp(entry.CompletedTimer / CompletedDuration, 0f, 1f);
            }

            float itemAlpha = alpha * Math.Min(fly * 2.5f, 1f) * fadeAlpha;
            float x = LeftMargin + flyOffset;
            Rectangle rect = new((int)x, (int)y, (int)ItemWidth, (int)ItemHeight);

            //=== 背景 ===
            Color bgColor = entry.State switch {
                HackQueueState.Uploading => Color.Lerp(HackTheme.BgSlot, HackTheme.Uploading, 0.06f),
                HackQueueState.Completed => Color.Lerp(HackTheme.BgSlot, HackTheme.Accent, 0.08f * fadeAlpha),
                _ => HackTheme.BgSlot,
            };
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor * (itemAlpha * 0.88f));

            //=== CRT扫描线 ===
            DrawCRTOverlay(sb, px, rect, itemAlpha * 0.04f);

            //=== 右侧斜切遮罩（镜像于右面板的左斜切） ===
            DrawSlashCutRight(sb, px, rect, itemAlpha);

            //=== 类别色条（左侧） ===
            Color catColor = HackTheme.CategoryColor(entry.Hack.Category);
            float breathe = MathF.Sin(timer * 2.5f + index * 1.1f) * 0.1f + 0.9f;
            float barGlow = entry.State == HackQueueState.Uploading ? 1f : 0.5f;
            barGlow *= breathe;
            sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 3, 3, rect.Height - 6),
                new Rectangle(0, 0, 1, 1), catColor * (itemAlpha * barGlow));
            //色条渐变扩散
            sb.Draw(px, new Rectangle(rect.X + 5, rect.Y + 3, 10, rect.Height - 6),
                new Rectangle(0, 0, 1, 1), catColor * (itemAlpha * barGlow * 0.06f));

            //=== 类别符号 ===
            string catSymbol = HackTheme.CategorySymbol(entry.Hack.Category);
            Utils.DrawBorderString(sb, catSymbol, new Vector2(rect.X + 10, rect.Y + 6),
                catColor * (itemAlpha * 0.5f), 0.24f);

            //=== 协议名称 ===
            float nameX = rect.X + 28;
            float nameY = rect.Y + 6;
            Color nameColor = entry.State switch {
                HackQueueState.Uploading => Color.Lerp(HackTheme.TextBright, HackTheme.Uploading, 0.3f),
                HackQueueState.Completed => HackTheme.Accent,
                _ => HackTheme.TextNormal,
            };
            Utils.DrawBorderString(sb, entry.Hack.DisplayName.Value, new Vector2(nameX, nameY),
                nameColor * itemAlpha, FontName);

            //=== 状态文字（右上角） ===
            string statusText;
            Color statusColor;
            switch (entry.State) {
                case HackQueueState.Uploading:
                    statusText = $"{(int)(entry.UploadProgress * 100)}%";
                    statusColor = HackTheme.Uploading;
                    break;
                case HackQueueState.Completed:
                    float flash = MathF.Sin(entry.CompletedTimer * 12f) * 0.3f + 0.7f;
                    statusText = HackTime.Done.Value;
                    statusColor = HackTheme.Accent * flash;
                    break;
                default:
                    statusText = HackTime.Queued.Value;
                    statusColor = HackTheme.TextDim;
                    break;
            }
            Vector2 statusSize = FontAssets.MouseText.Value.MeasureString(statusText) * FontPct;
            Utils.DrawBorderString(sb, statusText,
                new Vector2(rect.Right - (int)SlashWidth - statusSize.X - 8, rect.Y + 8),
                statusColor * itemAlpha, FontPct);

            //=== 上传时间（等待态右下角） ===
            if (entry.State == HackQueueState.Waiting) {
                float sec = entry.Hack.UploadTime / 60f;
                string timeStr = $"{sec:F1}s";
                Vector2 ts = FontAssets.MouseText.Value.MeasureString(timeStr) * FontStatus;
                Utils.DrawBorderString(sb, timeStr,
                    new Vector2(rect.Right - (int)SlashWidth - ts.X - 8, rect.Bottom - ts.Y - 6),
                    HackTheme.TextDim * (itemAlpha * 0.5f), FontStatus);
            }

            //=== 进度条 ===
            int barY = rect.Bottom - (int)BarHeight - 4;
            int barX = rect.X + 28;
            int barW = rect.Width - 28 - (int)SlashWidth - 8;
            //背景槽
            sb.Draw(px, new Rectangle(barX, barY, barW, (int)BarHeight),
                new Rectangle(0, 0, 1, 1), HackTheme.ProgressBg * (itemAlpha * 0.6f));
            //填充
            float progress = entry.UploadProgress;
            int fillW = (int)(barW * progress);
            if (fillW > 0) {
                Color fillColor = entry.State == HackQueueState.Completed
                    ? HackTheme.Accent
                    : HackTheme.ProgressFill;
                sb.Draw(px, new Rectangle(barX, barY, fillW, (int)BarHeight),
                    new Rectangle(0, 0, 1, 1), fillColor * (itemAlpha * 0.85f));
                //进度前端辉光
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null && entry.State == HackQueueState.Uploading && fillW > 2) {
                    Color tipGlow = HackTheme.ProgressGlow * (itemAlpha * 0.35f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(barX + fillW, barY + BarHeight * 0.5f), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.1f, 0.03f), SpriteEffects.None, 0);
                }
                //高光线
                sb.Draw(px, new Rectangle(barX, barY, fillW, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.TextBright * (itemAlpha * 0.12f));
            }

            //=== 上传时扫描线 ===
            if (entry.State == HackQueueState.Uploading) {
                float scanPos = (timer * 2f + index * 0.3f) % 1.4f - 0.2f;
                DrawScanLine(sb, px, rect, scanPos, itemAlpha * 0.2f);
            }

            //=== 边框 ===
            Color borderCol = entry.State switch {
                HackQueueState.Uploading => Color.Lerp(HackTheme.Border, HackTheme.Uploading, 0.4f),
                HackQueueState.Completed => Color.Lerp(HackTheme.Border, HackTheme.Accent, 0.3f * fadeAlpha),
                _ => HackTheme.Border,
            };
            //顶边
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width - (int)SlashWidth, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (itemAlpha * 0.5f));
            //底边
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (itemAlpha * 0.35f));
            //左边
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), borderCol * (itemAlpha * 0.35f));
            //右斜切边线
            DrawSlashEdgeRight(sb, px, rect, borderCol * (itemAlpha * 0.45f));

            //=== 完成时底部光带 ===
            if (entry.State == HackQueueState.Completed) {
                float completedPulse = MathF.Sin(entry.CompletedTimer * 8f) * 0.3f + 0.7f;
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                    new Rectangle(0, 0, 1, 1), HackTheme.Accent * (itemAlpha * 0.4f * completedPulse));
            }
        }

        #endregion

        #region 布局计算

        private float GetStartY() {
            float totalH = queue.Count * (ItemHeight + ItemGap) - ItemGap;
            return (Main.screenHeight - totalH) * 0.5f;
        }

        #endregion

        #region 视觉辅助

        private static void DrawCRTOverlay(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            Color line = HackTheme.BgDarkest * alpha;
            for (int dy = 0; dy < rect.Height; dy += 3) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, rect.Width, 1),
                    new Rectangle(0, 0, 1, 1), line);
            }
        }

        //右侧斜切遮罩（镜像）
        private static void DrawSlashCutRight(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            Color mask = HackTheme.BgDarkest * (alpha * 0.95f);
            int slashW = (int)SlashWidth;
            for (int dy = 0; dy < rect.Height; dy++) {
                float t = (float)dy / rect.Height;
                int cutW = (int)(slashW * t);
                if (cutW > 0)
                    sb.Draw(px, new Rectangle(rect.Right - cutW, rect.Y + dy, cutW, 1),
                        new Rectangle(0, 0, 1, 1), mask);
            }
        }

        //右侧斜切边线
        private static void DrawSlashEdgeRight(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            Vector2 top = new(rect.Right, rect.Y);
            Vector2 bottom = new(rect.Right - SlashWidth, rect.Bottom);
            DrawLine(sb, px, top, bottom, 1f, color);
        }

        private static void DrawScanLine(SpriteBatch sb, Texture2D px, Rectangle rect, float pos, float alpha) {
            int lineX = rect.X + (int)(rect.Width * pos);
            if (lineX < rect.X || lineX > rect.Right - 2) return;
            sb.Draw(px, new Rectangle(lineX, rect.Y + 1, 2, rect.Height - 2),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * alpha);
        }

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

        #endregion
    }
}
