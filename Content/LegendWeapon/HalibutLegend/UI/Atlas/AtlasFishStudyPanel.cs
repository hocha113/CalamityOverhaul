using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 研究祭坛选鱼次级面板
    /// 图鉴近全屏且冻结背包交互，点击祭坛改为弹出本面板列出可研究鱼
    /// 点选扣除一只投入祭坛，底部「取回」退回研究中物品
    /// 本地读写 <see cref="HalibutSave"/>，无网络同步
    /// </summary>
    internal class AtlasFishStudyPanel
    {
        private sealed class FishEntry
        {
            public int ItemType;
            public int Count;
            public FishSkill Skill;
            public Rectangle Rect;
        }

        //布局常量
        private const int GridCols = 6;
        private const int MaxVisibleRows = 4;
        private const float CellSize = 58f;
        private const float CellGap = 12f;
        private const float Pad = 22f;
        private const float HeaderH = 58f;
        private const float FooterH = 64f;

        /// <summary>
        /// 面板是否处于打开状态（关闭后仍会播放收起动画）
        /// </summary>
        public bool IsOpen { get; private set; }
        private float openAnim;

        //背包扫描结果（按物品类型聚合）
        private readonly List<FishEntry> entries = [];
        //逐格悬停动画，键为物品类型，与 entries 重建解耦以免每帧丢失过渡
        private readonly Dictionary<int, float> cellHover = [];
        private readonly HalibutUIParticlePool particles = new(60);

        //网格滚动（鱼种类多于可视行数时）
        private float gridScroll;
        private float gridScrollMax;

        //命中区缓存：Update 计算，Draw 复用
        private Rectangle panelRect;
        private Rectangle gridClip;
        private Rectangle closeBtnRect;
        private Rectangle reclaimBtnRect;
        private bool closeHover;
        private bool reclaimHover;
        private FishEntry hoveredEntry;

        /// <summary>
        /// 打开面板并立即扫描背包
        /// </summary>
        public void Open(HalibutSave save) {
            RefreshEntries(save);
            cellHover.Clear();
            gridScroll = 0f;
            IsOpen = true;
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.2f, Volume = 0.5f });
        }

        /// <summary>
        /// 关闭面板（研究状态不受影响，计时在数据层继续）
        /// </summary>
        public void Close() {
            if (!IsOpen) {
                return;
            }
            IsOpen = false;
            hoveredEntry = null;
            SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = 0.1f, Volume = 0.4f });
        }

        /// <summary>
        /// 静默重置（图鉴重新打开 / 切换视图时调用，不播放音效、不保留淡出动画）
        /// </summary>
        public void Reset() {
            IsOpen = false;
            openAnim = 0f;
            hoveredEntry = null;
            entries.Clear();
            cellHover.Clear();
            particles.Clear();
        }

        /// <summary>
        /// 扫描主背包（0-49），按物品类型聚合所有可研究的鱼
        /// </summary>
        private void RefreshEntries(HalibutSave save) {
            entries.Clear();
            Player player = Main.LocalPlayer;
            for (int i = 0; i < 50; i++) {
                Item item = player.inventory[i];
                if (item == null || !save.CanStudy(item)) {
                    continue;
                }
                FishEntry exist = entries.Find(e => e.ItemType == item.type);
                if (exist != null) {
                    exist.Count += item.stack;
                    continue;
                }
                FishSkill.UnlockFishs.TryGetValue(item.type, out FishSkill skill);
                entries.Add(new FishEntry {
                    ItemType = item.type,
                    Count = item.stack,
                    Skill = skill,
                });
            }
        }

        public void Update(Rectangle contentArea, HalibutSave save, bool inputAvailable) {
            particles.Update();
            openAnim = MathHelper.Lerp(openAnim, IsOpen ? 1f : 0f, 0.2f);
            if (!IsOpen && openAnim < 0.01f) {
                return;
            }

            //背包可能因研究完成（解锁后该鱼不再可研究）而变化，保持列表实时
            if (IsOpen) {
                RefreshEntries(save);
            }
            LayoutCompute(contentArea);

            bool interact = IsOpen && inputAvailable && openAnim > 0.9f;
            Vector2 mouse = Main.MouseScreen;
            hoveredEntry = null;

            //网格滚动
            if (interact && gridScrollMax > 0f && gridClip.Contains(mouse.ToPoint())) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    gridScroll = MathHelper.Clamp(gridScroll - MathF.Sign(delta) * (CellSize + CellGap),
                        0f, gridScrollMax);
                    Main.LocalPlayer.CWR().DontSwitchWeaponTime = 5;
                    LayoutCompute(contentArea);
                }
            }

            //逐格悬停命中与动画
            foreach (FishEntry e in entries) {
                bool hit = interact && gridClip.Contains(e.Rect) && e.Rect.Contains(mouse.ToPoint());
                if (hit) {
                    hoveredEntry = e;
                }
                float cur = cellHover.GetValueOrDefault(e.ItemType, 0f);
                cellHover[e.ItemType] = MathHelper.Lerp(cur, hit ? 1f : 0f, 0.3f);
            }

            bool studying = save.StudyItem.Alives() && save.StudyItem.type > ItemID.None;
            closeHover = interact && closeBtnRect.Contains(mouse.ToPoint());
            reclaimHover = interact && studying && reclaimBtnRect.Contains(mouse.ToPoint());

            if (!interact) {
                return;
            }
            Main.LocalPlayer.mouseInterface = true;

            if (!(Main.mouseLeft && Main.mouseLeftRelease)) {
                return;
            }

            if (closeHover) {
                Main.mouseLeftRelease = false;
                Close();
                return;
            }
            if (reclaimHover) {
                Main.mouseLeftRelease = false;
                ReturnCurrentStudy(save);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.2f });
                RefreshEntries(save);
                return;
            }
            if (hoveredEntry != null) {
                Main.mouseLeftRelease = false;
                PlaceIntoStudy(save, hoveredEntry);
                return;
            }
            //点击面板之外的任意处 = 关闭面板
            if (!panelRect.Contains(mouse.ToPoint())) {
                Main.mouseLeftRelease = false;
                Close();
            }
        }

        /// <summary>
        /// 计算面板与各命中区，居中于内容区
        /// </summary>
        private void LayoutCompute(Rectangle contentArea) {
            int rowsTotal = Math.Max(1, (entries.Count + GridCols - 1) / GridCols);
            int visRows = Math.Clamp(rowsTotal, 1, MaxVisibleRows);
            float gridW = GridCols * CellSize + (GridCols - 1) * CellGap;
            float gridH = visRows * CellSize + (visRows - 1) * CellGap;
            float panelW = gridW + Pad * 2f;
            float panelH = HeaderH + gridH + FooterH + Pad;
            Vector2 center = contentArea.Center.ToVector2();
            panelRect = new Rectangle((int)(center.X - panelW * 0.5f), (int)(center.Y - panelH * 0.5f),
                (int)panelW, (int)panelH);

            float gridX = panelRect.X + Pad;
            float gridY = panelRect.Y + HeaderH;
            gridClip = new Rectangle((int)gridX - 4, (int)gridY - 4, (int)gridW + 8, (int)gridH + 8);

            gridScrollMax = MathF.Max(0f, (rowsTotal - visRows) * (CellSize + CellGap));
            gridScroll = MathHelper.Clamp(gridScroll, 0f, gridScrollMax);

            for (int i = 0; i < entries.Count; i++) {
                int r = i / GridCols;
                int c = i % GridCols;
                float ex = gridX + c * (CellSize + CellGap);
                float ey = gridY + r * (CellSize + CellGap) - gridScroll;
                entries[i].Rect = new Rectangle((int)ex, (int)ey, (int)CellSize, (int)CellSize);
            }

            closeBtnRect = new Rectangle(panelRect.Right - 32, panelRect.Y + 12, 20, 20);
            reclaimBtnRect = new Rectangle(panelRect.Right - (int)Pad - 112,
                panelRect.Bottom - (int)FooterH + 22, 112, 26);
        }

        /// <summary>
        /// 把一条鱼从背包投入祭坛研究；若祭坛已有研究对象先退回背包
        /// </summary>
        private void PlaceIntoStudy(HalibutSave save, FishEntry entry) {
            ReturnCurrentStudy(save);
            if (!ConsumeOne(entry.ItemType)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero);
                RefreshEntries(save);
                return;
            }
            Item fish = new();
            fish.SetDefaults(entry.ItemType);
            fish.stack = 1;
            save.StudyItem = fish;
            save.IsStudying = true;
            save.StudyTimer = 0;
            SoundEngine.PlaySound(SoundID.Grab);
            Vector2 burstPos = entry.Rect.Center.ToVector2();
            particles.SpawnRingPulse(burstPos, HalibutTheme.Glow, 46f, 3f);
            particles.SpawnBurst(burstPos, HalibutTheme.GlowHi, 10, 3f);
            RefreshEntries(save);
        }

        /// <summary>
        /// 把祭坛上正在研究的鱼退回玩家背包（背包满则掉落到地面），并中断研究
        /// </summary>
        private static void ReturnCurrentStudy(HalibutSave save) {
            if (!save.StudyItem.Alives() || save.StudyItem.type <= ItemID.None) {
                return;
            }
            GiveToInventory(save.StudyItem.Clone());
            save.StudyItem.TurnToAir();
            save.IsStudying = false;
            save.StudyTimer = 0;
        }

        /// <summary>
        /// 从主背包扣除一只指定类型的物品
        /// </summary>
        private static bool ConsumeOne(int itemType) {
            Player player = Main.LocalPlayer;
            for (int i = 0; i < 50; i++) {
                Item item = player.inventory[i];
                if (item != null && item.Alives() && item.type == itemType && item.stack > 0) {
                    item.stack--;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 优先放回背包，放不下则在玩家位置掉落
        /// </summary>
        private static void GiveToInventory(Item item) {
            if (item == null || !item.Alives()) {
                return;
            }
            Player player = Main.LocalPlayer;
            Item leftover = player.GetItem(Main.myPlayer, item,
                GetItemSettings.InventoryEntityToPlayerInventorySettings);
            if (leftover != null && leftover.Alives() && leftover.stack > 0) {
                player.QuickSpawnItem(new EntitySource_OverfullInventory(player), leftover.type, leftover.stack);
            }
        }

        public void Draw(SpriteBatch sb, Rectangle contentArea, HalibutSave save, float alpha, float time) {
            if (openAnim < 0.01f) {
                return;
            }
            float a = alpha * VaultUtils.EaseOutCubic(MathHelper.Clamp(openAnim, 0f, 1f));

            //全屏压暗遮罩，聚焦面板
            sb.Draw(HalibutRenderer.Pixel,
                new Rectangle(0, 0, (int)HalibutTheme.UIScreenW, (int)HalibutTheme.UIScreenH),
                new Rectangle(0, 0, 1, 1), HalibutTheme.Void * (0.5f * a));

            //背板 + 饰框
            HalibutRenderer.DrawSeaPanel(sb, panelRect, a, 0.5f, 0f, 0.62f);
            HalibutRenderer.DrawOrnateFrame(sb, panelRect, HalibutTheme.Glow, a * 0.95f, time, 14f);

            //标题
            float titleX = panelRect.X + Pad;
            HalibutRenderer.DrawPearl(sb, new Vector2(titleX, panelRect.Y + 23f), 2.4f, HalibutTheme.Glow, 0.8f * a);
            HalibutRenderer.DrawGlowText(sb, HalibutAtlas.StudyPickerTitle.Value,
                new Vector2(titleX + 13f, panelRect.Y + 13f),
                HalibutTheme.Text * a, HalibutTheme.Glow * (0.4f * a), 0.95f);
            HalibutRenderer.DrawGlowText(sb, string.Format(HalibutAtlas.StudyPickerCount.Value, entries.Count),
                new Vector2(titleX + 13f, panelRect.Y + 36f),
                HalibutTheme.TextDim * a, HalibutTheme.Deep * (0.3f * a), 0.64f);

            //标题分隔线
            float lineY = panelRect.Y + HeaderH - 9f;
            HalibutRenderer.DrawGradientLine(sb, new Vector2(panelRect.X + Pad, lineY),
                new Vector2(panelRect.Right - Pad, lineY),
                HalibutTheme.Glow * (0.5f * a), HalibutTheme.Glow * (0.04f * a), 1.2f);

            DrawCloseButton(sb, a);

            //网格内容
            if (entries.Count == 0) {
                HalibutRenderer.DrawGlowTextCentered(sb, HalibutAtlas.StudyPickerEmpty.Value,
                    new Vector2(panelRect.Center.X, gridClip.Center.Y),
                    HalibutTheme.TextDim * a, HalibutTheme.Deep * (0.3f * a), 0.78f);
            }
            else {
                foreach (FishEntry e in entries) {
                    if (!gridClip.Contains(e.Rect)) {
                        continue;
                    }
                    DrawFishCell(sb, e, a);
                }
                if (gridScrollMax > 0f) {
                    DrawScrollBar(sb, a);
                }
            }

            DrawFooter(sb, save, a, time);
            particles.Draw(sb, a);

            //悬停鱼的光标信息
            if (hoveredEntry != null) {
                HalibutRenderer.DrawCursorPanel(sb, Main.MouseScreen,
                    Lang.GetItemNameValue(hoveredEntry.ItemType), HalibutTheme.GlowHi,
                    HalibutAtlas.StudyPlaceHint.Value, a);
            }
        }

        private void DrawFishCell(SpriteBatch sb, FishEntry e, float alpha) {
            Vector2 center = e.Rect.Center.ToVector2();
            float hov = cellHover.GetValueOrDefault(e.ItemType, 0f);
            float r = CellSize * 0.46f;

            HalibutRenderer.DrawDisc(sb, center, r, 3f, HalibutTheme.Deep * ((0.68f + hov * 0.22f) * alpha));
            if (hov > 0.04f) {
                HalibutRenderer.DrawSoftGlow(sb, center, r * 1.1f, HalibutTheme.Glow * (hov * 0.3f * alpha));
            }
            HalibutRenderer.DrawRing(sb, center, r, 1.2f,
                Color.Lerp(HalibutTheme.Glow, HalibutTheme.GlowHi, hov) * ((0.5f + hov * 0.4f) * alpha));

            float scale = 1f + hov * 0.1f;
            VaultUtils.SimpleDrawItem(sb, e.ItemType, center, (int)(CellSize * 0.7f), scale, 0f, Color.White * alpha);

            if (e.Count > 1) {
                Vector2 cntPos = new(e.Rect.Right - 5f, e.Rect.Bottom - 14f);
                string cnt = e.Count.ToString();
                Utils.DrawBorderString(sb, cnt, cntPos + new Vector2(1f, 1f), Color.Black * (alpha * 0.6f), 0.72f, 1f, 0.5f);
                Utils.DrawBorderString(sb, cnt, cntPos, HalibutTheme.Text * alpha, 0.72f, 1f, 0.5f);
            }
        }

        private void DrawCloseButton(SpriteBatch sb, float alpha) {
            Vector2 c = closeBtnRect.Center.ToVector2();
            float hi = closeHover ? 1f : 0f;
            HalibutRenderer.DrawRing(sb, c, 10f, 1.1f,
                Color.Lerp(HalibutTheme.Teal, HalibutTheme.Danger, hi) * ((0.6f + hi * 0.4f) * alpha));
            Color x = Color.Lerp(HalibutTheme.TextDim, HalibutTheme.Danger, hi) * alpha;
            HalibutRenderer.DrawLine(sb, c + new Vector2(-4f, -4f), c + new Vector2(4f, 4f), 1.5f, x);
            HalibutRenderer.DrawLine(sb, c + new Vector2(4f, -4f), c + new Vector2(-4f, 4f), 1.5f, x);
        }

        private void DrawScrollBar(SpriteBatch sb, float alpha) {
            float trackX = panelRect.Right - 11f;
            float top = gridClip.Top + 2f;
            float bottom = gridClip.Bottom - 2f;
            HalibutRenderer.DrawLine(sb, new Vector2(trackX, top), new Vector2(trackX, bottom),
                1f, HalibutTheme.Teal * (0.4f * alpha));
            float t = gridScrollMax > 0f ? gridScroll / gridScrollMax : 0f;
            const float thumbH = 26f;
            float thumbY = MathHelper.Lerp(top, bottom - thumbH, t);
            HalibutRenderer.DrawLine(sb, new Vector2(trackX, thumbY), new Vector2(trackX, thumbY + thumbH),
                2f, HalibutTheme.Glow * (0.75f * alpha));
        }

        private void DrawFooter(SpriteBatch sb, HalibutSave save, float alpha, float time) {
            float footerTop = panelRect.Bottom - FooterH;
            HalibutRenderer.DrawGradientLine(sb, new Vector2(panelRect.X + Pad, footerTop + 6f),
                new Vector2(panelRect.Right - Pad, footerTop + 6f),
                HalibutTheme.Glow * (0.04f * alpha), HalibutTheme.Glow * (0.4f * alpha), 1f);

            bool studying = save.StudyItem.Alives() && save.StudyItem.type > ItemID.None;
            if (!studying) {
                HalibutRenderer.DrawGlowTextCentered(sb, HalibutAtlas.StudyPickerHint.Value,
                    new Vector2(panelRect.Center.X, footerTop + 34f),
                    HalibutTheme.TextDim * alpha, HalibutTheme.Deep * (0.3f * alpha), 0.7f);
                return;
            }

            //研究中：左侧鱼图标 + 名称 + 进度，右侧取回按钮
            Vector2 iconPos = new(panelRect.X + Pad + 14f, footerTop + 32f);
            HalibutRenderer.DrawDisc(sb, iconPos, 15f, 2f, HalibutTheme.Deep * (0.7f * alpha));
            VaultUtils.SimpleDrawItem(sb, save.StudyItem.type, iconPos, 24, 1f, 0f, Color.White * alpha);

            float progress = save.IsStudying
                ? MathHelper.Clamp(save.StudyTimer / (float)save.StudyDuration, 0f, 1f)
                : 0f;
            HalibutRenderer.DrawGlowText(sb,
                string.Format(HalibutAtlas.StudyingFormat.Value, Lang.GetItemNameValue(save.StudyItem.type)),
                new Vector2(panelRect.X + Pad + 34f, footerTop + 16f),
                HalibutTheme.Text * alpha, HalibutTheme.Glow * (0.3f * alpha), 0.72f);
            HalibutRenderer.DrawGlowText(sb, $"{(int)(progress * 100)}%",
                new Vector2(panelRect.X + Pad + 34f, footerTop + 35f),
                HalibutTheme.Accent * alpha, HalibutTheme.Deep * (0.3f * alpha), 0.7f);

            HalibutRenderer.DrawCapsuleButton(sb, reclaimBtnRect, HalibutAtlas.StudyReclaim.Value,
                HalibutTheme.Accent, reclaimHover, false, alpha, time);
        }
    }
}
