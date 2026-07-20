using CalamityOverhaul.Content.HackTimes.Scannables;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    //NPC 头顶骇入状态卡片
    internal static class HackStatusDisplay
    {
        //卡片尺寸
        private const float CardWidth = 140f;
        private const float CardHeight = 22f;
        private const float CardGap = 3f;
        private const float BarHeight = 3f;
        private const float BarMargin = 4f;
        private const float FontScale = 0.32f;
        //距NPC头顶的距离
        private const float TopOffset = 18f;

        //复用缓冲
        private static readonly List<ActiveHackEffect> effectBuf = [];
        private static readonly List<HackQueueEntry> queueBuf = [];
        private static readonly List<ActiveHackEffect> tileEffectBuf = [];
        private static readonly List<HackQueueEntry> tileQueueBuf = [];
        private static readonly List<HackQueueEntry> turretQueueBuf = [];
        private static readonly List<HackQueueEntry> signalTowerQueueBuf = [];
        //收集需要绘制的NPC索引集合（避免重复）
        private static readonly HashSet<int> npcSet = [];
        //收集需要绘制的物块坐标集合（避免重复，用long编码x|y）
        private static readonly HashSet<long> tileSet = [];
        //收集需要绘制的炮台集合（避免重复）
        private static readonly HashSet<IHackableTurret> turretSet = [];
        //收集需要绘制的信号塔集合（避免重复）
        private static readonly HashSet<IHackableSignalTower> signalTowerSet = [];

        public static void Draw(SpriteBatch sb) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            var queue = HackTimeUI.Instance?.Queue;

            //收集所有需要绘制的NPC
            npcSet.Clear();
            var allEffects = HackEffectTracker.AllActiveEffects;
            for (int i = 0; i < allEffects.Count; i++) {
                if (allEffects[i].Active)
                    npcSet.Add(allEffects[i].TargetIndex);
            }
            if (queue != null) {
                var entries = queue.Entries;
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].TargetKind == HackTargetKind.Npc)
                        npcSet.Add(entries[i].TargetIndex);
                }
            }

            //逐NPC绘制
            foreach (int npcIndex in npcSet) {
                if (npcIndex < 0 || npcIndex >= Main.maxNPCs) continue;
                NPC npc = Main.npc[npcIndex];
                if (!npc.active) continue;
                DrawNPCStatus(sb, px, npc, npcIndex, queue);
            }

            //收集所有需要绘制的物块
            tileSet.Clear();
            var allTileEffects = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < allTileEffects.Count; i++) {
                if (allTileEffects[i].Active)
                    tileSet.Add(PackTileCoord(allTileEffects[i].TileX, allTileEffects[i].TileY));
            }
            if (queue != null) {
                var entries = queue.Entries;
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].TargetKind == HackTargetKind.Tile)
                        tileSet.Add(PackTileCoord(entries[i].TileX, entries[i].TileY));
                }
            }

            //逐物块绘制
            foreach (long packed in tileSet) {
                UnpackTileCoord(packed, out int tx, out int ty);
                if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY) continue;
                if (!Main.tile[tx, ty].HasTile) continue;
                DrawTileStatus(sb, px, tx, ty, queue);
            }

            //收集所有需要绘制的炮台
            turretSet.Clear();
            if (queue != null) {
                var entries = queue.Entries;
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].TargetKind == HackTargetKind.Turret && entries[i].TurretTarget != null)
                        turretSet.Add(entries[i].TurretTarget);
                }
            }

            //逐炮台绘制
            foreach (IHackableTurret turret in turretSet) {
                Actor actor = turret?.AsActor;
                if (actor == null || !actor.Active) continue;
                DrawTurretStatus(sb, px, turret, actor, queue);
            }

            //收集所有需要绘制的信号塔
            signalTowerSet.Clear();
            if (queue != null) {
                var entries = queue.Entries;
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].TargetKind == HackTargetKind.SignalTower && entries[i].SignalTowerTarget != null)
                        signalTowerSet.Add(entries[i].SignalTowerTarget);
                }
            }

            //逐信号塔绘制
            foreach (IHackableSignalTower tower in signalTowerSet) {
                Actor actor = tower?.AsActor;
                if (actor == null || !actor.Active) continue;
                DrawSignalTowerStatus(sb, px, tower, actor, queue);
            }
        }

        private static long PackTileCoord(int x, int y) => (long)x << 32 | (uint)y;
        private static void UnpackTileCoord(long packed, out int x, out int y) {
            x = (int)(packed >> 32);
            y = (int)(packed & 0xFFFFFFFF);
        }

        private static void DrawNPCStatus(SpriteBatch sb, Texture2D px, NPC npc, int npcIndex, HackQueueRenderer queue) {
            //收集该NPC的活跃效果和上传条目
            HackEffectTracker.GetEffects(npcIndex, effectBuf);
            if (queue != null)
                queue.GetEntriesForNPC(npcIndex, queueBuf);
            else
                queueBuf.Clear();

            int totalCards = effectBuf.Count + queueBuf.Count;
            if (totalCards == 0) return;

            //计算NPC屏幕坐标，卡片从头顶向上排列
            Vector2 npcScreen = npc.Top - Main.screenPosition;
            float totalHeight = totalCards * (CardHeight + CardGap) - CardGap;
            float startY = npcScreen.Y - TopOffset - totalHeight;
            float baseX = npcScreen.X - CardWidth * 0.5f;

            int cardIndex = 0;

            //先绘制上传中的条目（最上方）
            for (int i = 0; i < queueBuf.Count; i++) {
                var entry = queueBuf[i];
                float y = startY + cardIndex * (CardHeight + CardGap);
                DrawUploadCard(sb, px, baseX, y, entry);
                cardIndex++;
            }

            //再绘制已生效的协议
            for (int i = 0; i < effectBuf.Count; i++) {
                var eff = effectBuf[i];
                float y = startY + cardIndex * (CardHeight + CardGap);
                DrawActiveCard(sb, px, baseX, y, eff);
                cardIndex++;
            }
        }

        private static void DrawTurretStatus(SpriteBatch sb, Texture2D px, IHackableTurret turret, Actor actor, HackQueueRenderer queue) {
            if (queue == null) return;
            queue.GetEntriesForTurret(turret, turretQueueBuf);
            if (turretQueueBuf.Count == 0) return;

            //以炮台顶部中心为锚，卡片从头顶向上排列
            Vector2 topCenter = new(
                actor.Position.X + actor.Width * 0.5f - Main.screenPosition.X,
                actor.Position.Y - Main.screenPosition.Y);
            float totalHeight = turretQueueBuf.Count * (CardHeight + CardGap) - CardGap;
            float startY = topCenter.Y - TopOffset - totalHeight;
            float baseX = topCenter.X - CardWidth * 0.5f;

            for (int i = 0; i < turretQueueBuf.Count; i++) {
                float y = startY + i * (CardHeight + CardGap);
                DrawUploadCard(sb, px, baseX, y, turretQueueBuf[i]);
            }
        }

        private static void DrawSignalTowerStatus(SpriteBatch sb, Texture2D px, IHackableSignalTower tower, Actor actor, HackQueueRenderer queue) {
            if (queue == null) return;
            queue.GetEntriesForSignalTower(tower, signalTowerQueueBuf);
            if (signalTowerQueueBuf.Count == 0) return;

            //信号塔锚点上移
            Vector2 scanCenter = tower.WorldCenter;
            Vector2 topCenter = new(
                scanCenter.X - Main.screenPosition.X,
                scanCenter.Y - Main.screenPosition.Y - actor.Height * 0.25f);
            float totalHeight = signalTowerQueueBuf.Count * (CardHeight + CardGap) - CardGap;
            float startY = topCenter.Y - TopOffset - totalHeight;
            float baseX = topCenter.X - CardWidth * 0.5f;

            for (int i = 0; i < signalTowerQueueBuf.Count; i++) {
                float y = startY + i * (CardHeight + CardGap);
                DrawUploadCard(sb, px, baseX, y, signalTowerQueueBuf[i]);
            }
        }

        private static void DrawTileStatus(SpriteBatch sb, Texture2D px, int tileX, int tileY, HackQueueRenderer queue) {
            //收集该物块的活跃效果和上传条目
            HackEffectTracker.GetTileEffects(tileX, tileY, tileEffectBuf);
            if (queue != null)
                queue.GetEntriesForTile(tileX, tileY, tileQueueBuf);
            else
                tileQueueBuf.Clear();

            int totalCards = tileEffectBuf.Count + tileQueueBuf.Count;
            if (totalCards == 0) return;

            //获取物块包围盒，卡片从顶部向上排列
            Rectangle bounds = TileScannable.GetTileWorldBounds(tileX, tileY);
            Vector2 topCenter = new(bounds.Center.X - Main.screenPosition.X,
                bounds.Top - Main.screenPosition.Y);
            float totalHeight = totalCards * (CardHeight + CardGap) - CardGap;
            float startY = topCenter.Y - TopOffset - totalHeight;
            float baseX = topCenter.X - CardWidth * 0.5f;

            int cardIndex = 0;

            //先绘制上传中的条目
            for (int i = 0; i < tileQueueBuf.Count; i++) {
                float y = startY + cardIndex * (CardHeight + CardGap);
                DrawUploadCard(sb, px, baseX, y, tileQueueBuf[i]);
                cardIndex++;
            }

            //再绘制已生效的协议
            for (int i = 0; i < tileEffectBuf.Count; i++) {
                float y = startY + cardIndex * (CardHeight + CardGap);
                DrawActiveTileCard(sb, px, baseX, y, tileEffectBuf[i]);
                cardIndex++;
            }
        }

        //上传中协议卡片
        private static void DrawUploadCard(SpriteBatch sb, Texture2D px, float x, float y, HackQueueEntry entry) {
            //卡片暗底
            Color bgColor = HackTheme.BgPanel * 0.85f;
            sb.Draw(px, new Rectangle((int)x, (int)y, (int)CardWidth, (int)CardHeight), bgColor);

            //左侧竖条（琥珀色表示上传中）
            Color barColor = entry.State == HackQueueState.Uploading
                ? HackTheme.Uploading
                : HackTheme.TextDim;
            sb.Draw(px, new Rectangle((int)x, (int)y, 2, (int)CardHeight), barColor);

            //协议名称
            string name = entry.Hack.DisplayName.Value;
            float nameX = x + 6f;
            float nameY = y + 2f;
            Utils.DrawBorderString(sb, name, new Vector2(nameX, nameY), HackTheme.Uploading, FontScale);

            //状态文字
            string status = entry.State switch {
                HackQueueState.Waiting => HackTime.Queued.Value,
                HackQueueState.Uploading => HackTime.UploadingPct.Format((int)(entry.UploadProgress * 100)),
                HackQueueState.Completed => HackTime.Complete.Value,
                _ => ""
            };
            float statusWidth = FontAssets.MouseText.Value.MeasureString(status).X * (FontScale * 0.85f);
            Utils.DrawBorderString(sb, status, new Vector2(x + CardWidth - statusWidth - 4f, nameY),
                HackTheme.TextDim, FontScale * 0.85f);

            //上传进度条
            float barY = y + CardHeight - BarHeight - 2f;
            float barW = CardWidth - BarMargin * 2;
            //进度条背景
            sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY, (int)barW, (int)BarHeight),
                HackTheme.ProgressBg * 0.9f);
            //进度条填充
            float fill = Math.Clamp(entry.UploadProgress, 0f, 1f);
            if (fill > 0) {
                Color fillColor = entry.State == HackQueueState.Completed
                    ? HackTheme.Accent
                    : HackTheme.Uploading;
                sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY, (int)(barW * fill), (int)BarHeight),
                    fillColor);
            }

            //边框
            DrawCardBorder(sb, px, x, y, HackTheme.Border * 0.6f);
        }

        //生效中协议卡片
        private static void DrawActiveCard(SpriteBatch sb, Texture2D px, float x, float y, ActiveHackEffect eff) {
            //卡片暗底
            Color bgColor = HackTheme.BgSection * 0.85f;
            sb.Draw(px, new Rectangle((int)x, (int)y, (int)CardWidth, (int)CardHeight), bgColor);

            //左侧竖条（青色表示生效中）
            sb.Draw(px, new Rectangle((int)x, (int)y, 2, (int)CardHeight), HackTheme.Accent);

            //协议名称
            string name = eff.Hack.DisplayName.Value;
            float nameX = x + 6f;
            float nameY = y + 2f;
            Utils.DrawBorderString(sb, name, new Vector2(nameX, nameY), HackTheme.Accent, FontScale);

            //剩余时间进度条
            int duration = (int)(eff.Hack.GetDuration() * eff.EffectMult);
            float progress = duration > 0 ? Math.Clamp(1f - (float)eff.Elapsed / duration, 0f, 1f) : 0f;

            //状态文字
            string status = duration > 0 ? HackTime.ActivePct.Format((int)(progress * 100)) : HackTime.ActiveText.Value;
            float statusWidth = FontAssets.MouseText.Value.MeasureString(status).X * (FontScale * 0.85f);
            Utils.DrawBorderString(sb, status, new Vector2(x + CardWidth - statusWidth - 4f, nameY),
                HackTheme.TextDim, FontScale * 0.85f);

            if (duration > 0) {
                float barY = y + CardHeight - BarHeight - 2f;
                float barW = CardWidth - BarMargin * 2;
                //进度条背景
                sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY, (int)barW, (int)BarHeight),
                    HackTheme.ProgressBg * 0.9f);
                //剩余时间填充（青色，随时间递减）
                if (progress > 0) {
                    sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY,
                        (int)(barW * progress), (int)BarHeight), HackTheme.ProgressFill);
                }
            }

            //边框
            DrawCardBorder(sb, px, x, y, HackTheme.Border * 0.4f);
        }

        //物块生效协议卡片
        private static void DrawActiveTileCard(SpriteBatch sb, Texture2D px, float x, float y, ActiveHackEffect eff) {
            Color bgColor = HackTheme.BgSection * 0.85f;
            sb.Draw(px, new Rectangle((int)x, (int)y, (int)CardWidth, (int)CardHeight), bgColor);

            sb.Draw(px, new Rectangle((int)x, (int)y, 2, (int)CardHeight), HackTheme.Accent);

            string name = eff.Hack.DisplayName.Value;
            float nameX = x + 6f;
            float nameY = y + 2f;
            Utils.DrawBorderString(sb, name, new Vector2(nameX, nameY), HackTheme.Accent, FontScale);

            int duration = eff.Hack.GetDuration();
            float progress = duration > 0 ? Math.Clamp(1f - (float)eff.Elapsed / duration, 0f, 1f) : 0f;

            string status = duration > 0 ? HackTime.ActivePct.Format((int)(progress * 100)) : HackTime.ActiveText.Value;
            float statusWidth = FontAssets.MouseText.Value.MeasureString(status).X * (FontScale * 0.85f);
            Utils.DrawBorderString(sb, status, new Vector2(x + CardWidth - statusWidth - 4f, nameY),
                HackTheme.TextDim, FontScale * 0.85f);

            if (duration > 0) {
                float barY = y + CardHeight - BarHeight - 2f;
                float barW = CardWidth - BarMargin * 2;
                sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY, (int)barW, (int)BarHeight),
                    HackTheme.ProgressBg * 0.9f);
                if (progress > 0) {
                    sb.Draw(px, new Rectangle((int)(x + BarMargin), (int)barY,
                        (int)(barW * progress), (int)BarHeight), HackTheme.ProgressFill);
                }
            }

            DrawCardBorder(sb, px, x, y, HackTheme.Border * 0.4f);
        }

        //绘制卡片细边框
        private static void DrawCardBorder(SpriteBatch sb, Texture2D px, float x, float y, Color color) {
            int w = (int)CardWidth;
            int h = (int)CardHeight;
            sb.Draw(px, new Rectangle((int)x, (int)y, w, 1), color);          //上
            sb.Draw(px, new Rectangle((int)x, (int)y + h - 1, w, 1), color);  //下
            sb.Draw(px, new Rectangle((int)x, (int)y, 1, h), color);          //左
            sb.Draw(px, new Rectangle((int)x + w - 1, (int)y, 1, h), color);  //右
        }
    }
}
