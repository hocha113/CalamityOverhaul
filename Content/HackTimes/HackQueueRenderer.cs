using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    //左下角上行链路线程条
    internal class HackQueueRenderer
    {
        #region 布局常量

        private const float LeftMargin = 36f;
        private const float ItemWidth = 250f;
        private const float ItemHeight = 25f;
        private const float ItemGap = 6f;
        private const float BarHeight = 2f;
        private const float DiamondZone = 16f;
        //距屏底基线
        private const float BottomMargin = 150f;
        private const float FontName = 0.64f;
        private const float FontPct = 0.56f;
        //完成闪烁秒数
        private static float CompletedDuration => 0.2f;

        #endregion

        private readonly List<HackQueueEntry> queue = new();
        public IReadOnlyList<HackQueueEntry> Entries => queue;
        private float timer;

        #region 公共接口

        /// <summary>入队，同目标同 slot 不重复</summary>
        public bool Enqueue(QuickHackDef hack, int slotIndex, IHackTarget target, int computedRamCost) {
            return Enqueue(hack, slotIndex, target, computedRamCost, 0, 0);
        }

        public bool Enqueue(QuickHackDef hack, int slotIndex, IHackTarget target,
            int computedRamCost, uint sessionId, uint requestId) {
            if (target == null) return false;
            for (int i = 0; i < queue.Count; i++) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (requestId != 0 && queue[i].RequestId == requestId
                    && queue[i].SessionId == sessionId) return false;
                if (queue[i].Target != null && queue[i].Target.TargetEquals(target)) return false;
            }
            queue.Add(new HackQueueEntry(hack, slotIndex, target, computedRamCost,
                sessionId, requestId));
            return true;
        }

        //取消指定 slot，任意目标
        public void Cancel(int slotIndex) {
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (queue[i].SlotIndex == slotIndex) {
                    queue.RemoveAt(i);
                    break;
                }
            }
        }

        //取消指定 slot + 目标
        public void Cancel(int slotIndex, IHackTarget target) {
            if (target == null) return;
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (queue[i].SlotIndex != slotIndex) continue;
                if (queue[i].Target == null || !queue[i].Target.TargetEquals(target)) continue;
                queue.RemoveAt(i);
                break;
            }
        }

        public void Clear() {
            queue.Clear();
        }

        //清理本地预测完成条，不施加权威逻辑
        public void ConsumeAndApplyAll() {
            for (int i = queue.Count - 1; i >= 0; i--) {
                var entry = queue[i];
                if (!entry.IsTargetValid) {
                    queue.RemoveAt(i);
                    continue;
                }
                //完成条只用于表现，效果由服务端包驱动
                if (entry.State == HackQueueState.Completed && entry.CompletedTimer <= 0f) {
                    queue.RemoveAt(i);
                }
            }
        }

        public void RemoveRequest(uint sessionId, uint requestId) {
            if (requestId == 0) return;
            for (int i = queue.Count - 1; i >= 0; i--) {
                if (queue[i].SessionId == sessionId && queue[i].RequestId == requestId) {
                    queue.RemoveAt(i);
                }
            }
        }

        public void ApplyAuthorityState(uint sessionId, uint requestId, int slotIndex,
            HackQueueState state, float progress, long activationId, bool accepted) {
            if (requestId == 0) return;
            progress = float.IsFinite(progress) ? MathHelper.Clamp(progress, 0f, 1f) : 0f;
            for (int i = 0; i < queue.Count; i++) {
                HackQueueEntry entry = queue[i];
                if (entry.SessionId != sessionId || entry.RequestId != requestId) continue;
                if (!accepted) {
                    queue.RemoveAt(i);
                    return;
                }
                entry.AuthorityAccepted = true;
                entry.State = state;
                entry.UploadProgress = progress;
                entry.ActivationId = activationId;
                if (state == HackQueueState.Completed && entry.CompletedTimer <= 0f)
                    entry.CompletedTimer = CompletedDuration;
                return;
            }
        }

        //slot 状态，Uploading 优先
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

        //查询 slot 针对目标的状态
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

        //获取 slot 上传进度
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

        //获取 slot 针对目标的进度
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

        //队列头进度，NPC 头顶环
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

        //获取目标活跃条目进度，避免多目标串进度
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

            //仅队列头部 Uploading
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
                        //骇客时间内可排队但不推进
                        if (!hasUploading) {
                            entry.State = HackQueueState.Uploading;
                            hasUploading = true;
                        }
                        break;

                    case HackQueueState.Uploading:
                        hasUploading = true;
                        if (entry.Hack.UploadTime > 0)
                            entry.UploadProgress += 1f / entry.Hack.UploadTime;
                        if (entry.UploadProgress >= 1f) {
                            entry.UploadProgress = 1f;
                            entry.State = HackQueueState.Completed;
                            entry.CompletedTimer = CompletedDuration;
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

            Texture2D px = HackTheme.Pixel;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;

            float startY = GetStartY();

            //左侧细竖轨，把线程串成一组
            float railTop = startY - 18f;
            float railBottom = startY + queue.Count * (ItemHeight + ItemGap) - ItemGap + 2f;
            sb.Draw(px, new Rectangle((int)(LeftMargin - 8), (int)railTop, 1, (int)(railBottom - railTop)),
                HackTheme.SrcPixel, HackTheme.Border * (alpha * 0.6f));

            //标题
            DrawHeader(sb, alpha, startY);

            //线程条目
            for (int i = 0; i < queue.Count; i++) {
                DrawThread(sb, px, alpha, i, startY, queue[i]);
            }
        }

        private void DrawHeader(SpriteBatch sb, float alpha, float startY) {
            float headerY = startY - 26f;
            string header = $"{HackTime.UplinkHeader.Value} // {queue.Count}";
            Utils.DrawBorderString(sb, header, new Vector2((int)LeftMargin, (int)headerY),
                Color.Lerp(HackTheme.Accent, Color.White, 0.15f) * (alpha * 0.9f), 0.56f);
            //标题下引出短线
            float headerW = FontAssets.MouseText.Value.MeasureString(header).X * 0.56f;
            HackTheme.DrawLine(sb, new Vector2(LeftMargin, headerY + 18),
                new Vector2(LeftMargin + headerW + 20, headerY + 18),
                1f, HackTheme.Accent * (alpha * 0.3f));
        }

        private void DrawThread(SpriteBatch sb, Texture2D px, float alpha, int index, float startY, HackQueueEntry entry) {
            float fly = entry.FlyIn;
            if (fly < 0.01f) return;

            float y = startY + index * (ItemHeight + ItemGap);
            //飞入偏移（从左侧滑入）
            float flyOffset = (1f - HackTheme.EaseOutCubic(fly)) * -260f;
            //完成态淡出
            float fadeAlpha = 1f;
            if (entry.State == HackQueueState.Completed) {
                fadeAlpha = Math.Clamp(entry.CompletedTimer / CompletedDuration, 0f, 1f);
            }

            float itemAlpha = alpha * Math.Min(fly * 2.5f, 1f) * fadeAlpha;
            float x = LeftMargin + flyOffset;
            Rectangle rect = new((int)x, (int)y, (int)ItemWidth, (int)ItemHeight);

            Color catColor = HackTheme.CategoryColor(entry.Hack.Category);
            Color threadColor = entry.State switch {
                HackQueueState.Uploading => HackTheme.Uploading,
                HackQueueState.Completed => HackTheme.Accent,
                _ => HackTheme.TextDim,
            };

            //菱形，上传转/等待呼吸/完成闪
            Vector2 diamondC = new(rect.X + DiamondZone * 0.5f, rect.Y + 9f);
            switch (entry.State) {
                case HackQueueState.Uploading: {
                    float rot = timer * 3f + index;
                    sb.Draw(px, diamondC, HackTheme.SrcPixel, threadColor * (itemAlpha * 0.9f),
                        rot, new Vector2(0.5f), 8f, SpriteEffects.None, 0f);
                    sb.Draw(px, diamondC, HackTheme.SrcPixel, HackTheme.BgDarkest * itemAlpha,
                        rot, new Vector2(0.5f), 4f, SpriteEffects.None, 0f);
                    break;
                }
                case HackQueueState.Completed: {
                    float flash = MathF.Sin(entry.CompletedTimer * 12f) * 0.3f + 0.7f;
                    HackTheme.DrawDiamond(sb, diamondC, 8f, threadColor * (itemAlpha * flash));
                    break;
                }
                default: {
                    float breathe = MathF.Sin(timer * 2.5f + index * 1.1f) * 0.25f + 0.75f;
                    HackTheme.DrawDiamondOutline(sb, diamondC, 4f, 1f, threadColor * (itemAlpha * breathe));
                    break;
                }
            }

            //协议名
            float nameX = rect.X + DiamondZone + 6f;
            Color nameColor = entry.State switch {
                HackQueueState.Uploading => Color.Lerp(HackTheme.TextBright, HackTheme.Uploading, 0.3f),
                HackQueueState.Completed => HackTheme.Accent,
                _ => HackTheme.TextNormal,
            };
            Utils.DrawBorderString(sb, entry.Hack.DisplayName.Value, new Vector2((int)nameX, rect.Y + 1),
                nameColor * itemAlpha, FontName);

            //右侧读数
            string statusText;
            Color statusColor;
            switch (entry.State) {
                case HackQueueState.Uploading:
                    statusText = $"{(int)(entry.UploadProgress * 100)}%";
                    statusColor = HackTheme.Uploading;
                    break;
                case HackQueueState.Completed:
                    statusText = HackTime.Done.Value;
                    statusColor = HackTheme.Accent;
                    break;
                default:
                    statusText = $"{entry.Hack.UploadTime / 60f:F1}s";
                    statusColor = HackTheme.TextDim;
                    break;
            }
            Vector2 statusSize = FontAssets.MouseText.Value.MeasureString(statusText) * FontPct;
            Utils.DrawBorderString(sb, statusText,
                new Vector2((int)(rect.Right - statusSize.X - 2), rect.Y + 2),
                Color.Lerp(statusColor, Color.White, 0.15f) * itemAlpha, FontPct);

            //细进度轨（线程底部通宽）
            int barY = rect.Bottom - (int)BarHeight - 1;
            int barX = (int)nameX;
            int barW = rect.Width - (int)DiamondZone - 8;
            sb.Draw(px, new Rectangle(barX, barY, barW, (int)BarHeight),
                HackTheme.SrcPixel, HackTheme.ProgressBg * (itemAlpha * 0.8f));
            float progress = entry.UploadProgress;
            int fillW = (int)(barW * progress);
            if (fillW > 0) {
                Color fillColor = entry.State == HackQueueState.Completed
                    ? HackTheme.Accent : HackTheme.ProgressFill;
                sb.Draw(px, new Rectangle(barX, barY, fillW, (int)BarHeight),
                    HackTheme.SrcPixel, fillColor * (itemAlpha * 0.9f));
                //进度前端辉光
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null && entry.State == HackQueueState.Uploading && fillW > 2) {
                    Color tipGlow = HackTheme.ProgressGlow * (itemAlpha * 0.35f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(barX + fillW, barY + BarHeight * 0.5f), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.08f, 0.02f), SpriteEffects.None, 0);
                }
            }

            //线程与竖轨的连接刻点
            sb.Draw(px, new Rectangle((int)(LeftMargin - 8), (int)(rect.Y + 6), 5, 1),
                HackTheme.SrcPixel, threadColor * (itemAlpha * 0.5f));

            //类别微刻（名称左上小色点）
            sb.Draw(px, new Rectangle((int)nameX - 2, rect.Y + 1, 2, 2),
                HackTheme.SrcPixel, catColor * (itemAlpha * 0.8f));
        }

        #endregion

        #region 布局计算

        private float GetStartY() {
            float totalH = queue.Count * (ItemHeight + ItemGap) - ItemGap;
            return Main.screenHeight - BottomMargin - totalH;
        }

        #endregion
    }
}
