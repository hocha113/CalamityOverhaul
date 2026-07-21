using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 技能海域图鉴主场景
    /// 节点纵向四带排列、滚轮下潜；顶研究祭坛，底装备坞（至多十槽）
    /// </summary>
    internal class AtlasSkillSea
    {
        //布局常量
        private const float AltarLayoutY = 128f;
        private const float FirstBandY = 252f;
        private const float BandHeaderH = 46f;
        private const float BandBottomPad = 56f;
        private const float DockHeight = 74f;

        private readonly List<AtlasSkillNode> nodes = [];
        private readonly float[] bandStartY = new float[HalibutTheme.AtlasTierCount];
        private float totalHeight;

        //滚动
        private float scroll;
        private float scrollTarget;

        //交互状态
        private AtlasSkillNode hoveredNode;
        private AtlasSkillNode selectedNode;
        private FishSkill draggingSkill;
        private bool dragFromDock;
        //按下登记拖拽候选，过阈值即成形
        private FishSkill pressSkill;       //候选技能（已解锁节点或装备坞槽位才有值，锁定节点为 null）
        private AtlasSkillNode pressNode;   //按下命中的节点（未成形拖拽时松手=点击打开详情）
        private bool pressFromDock;         //候选来自装备坞
        private int pressDockIndex = -1;    //候选所在的装备坞槽位
        private Vector2 pressMouse;         //按下时的光标位置（位移阈值基准）
        private bool pressActive;           //本次按压是否已登记（含空白处，供松手判定）
        //光标自按下点位移超过此值即由"点击候选"转为"拖拽"
        private const float DragStartDist = 6f;
        private int dockHoverIndex = -1;
        private float loadoutFullFlash;

        //详情卡按钮命中区（Update计算，Draw使用）
        private Rectangle detailRect;
        private Rectangle equipBtnRect;
        private Rectangle selectBtnRect;
        private bool equipBtnHover;
        private bool selectBtnHover;

        public readonly AtlasStudyAltar Altar = new();
        private readonly HalibutUIParticlePool particles = new(140);
        private int ambientTimer;
        //最近一帧的内容区域，供异步回调换算屏幕坐标
        private Rectangle lastContentArea = new(0, 64, 1920, 1016);

        //滚动避让、下潜时顶底让位
        private float chromeHide;
        private int scrollIdleTimer = 60;

        /// <summary>
        /// 边缘挂件的隐藏程度 0显示-1隐藏（滚动时上升），页眉提示等共享此值
        /// </summary>
        public float ChromeHide => chromeHide;

        /// <summary>
        /// 当前下潜深度 0–1，驱动背景着色器
        /// </summary>
        public float Depth { get; private set; }

        /// <summary>
        /// 当前滚动像素值（驱动背景视差）
        /// </summary>
        public float ScrollPx => scroll;

        /// <summary>
        /// 图鉴打开时重建节点布局
        /// </summary>
        public void Rebuild(HalibutSave save) {
            nodes.Clear();
            //按（深度带，注册ID）稳定排序
            List<FishSkill> all = [.. FishSkill.Instances];
            all.Sort((x, y) => {
                int t = AtlasTierMap.GetTier(x).CompareTo(AtlasTierMap.GetTier(y));
                return t != 0 ? t : x.ID.CompareTo(y.ID);
            });

            float y = FirstBandY;
            int currentTier = -1;
            int columnIndex = 0;
            //X使用相对屏幕中线的偏移，绘制时实时换算，保证UI缩放中途变化也不偏移
            float startX = -(HalibutTheme.AtlasNodeColumns - 1) * HalibutTheme.AtlasNodeSpacingX * 0.5f;
            foreach (FishSkill skill in all) {
                int tier = AtlasTierMap.GetTier(skill);
                if (tier != currentTier) {
                    //进新带、累入上带行高到y
                    if (currentTier >= 0) {
                        y += RowsUsed(columnIndex) * HalibutTheme.AtlasNodeSpacingY + BandBottomPad;
                    }
                    for (int t = currentTier + 1; t <= tier; t++) {
                        bandStartY[t] = y;
                    }
                    y += BandHeaderH;
                    currentTier = tier;
                    columnIndex = 0;
                }
                var node = new AtlasSkillNode(skill);
                //奇数行半格偏移
                int row = columnIndex / HalibutTheme.AtlasNodeColumns;
                int col = columnIndex % HalibutTheme.AtlasNodeColumns;
                float xJitter = (row % 2 == 1) ? HalibutTheme.AtlasNodeSpacingX * 0.5f : 0f;
                float maxX = startX + (HalibutTheme.AtlasNodeColumns - 1) * HalibutTheme.AtlasNodeSpacingX;
                float nodeX = Math.Min(startX + col * HalibutTheme.AtlasNodeSpacingX + xJitter, maxX);
                node.LayoutPos = new Vector2(nodeX, y + row * HalibutTheme.AtlasNodeSpacingY);
                nodes.Add(node);
                columnIndex++;
            }
            y += RowsUsed(columnIndex) * HalibutTheme.AtlasNodeSpacingY;
            totalHeight = y + 140f;
            //未触及的更深带也给个底
            for (int t = currentTier + 1; t < HalibutTheme.AtlasTierCount; t++) {
                bandStartY[t] = y;
            }
            selectedNode = null;
            draggingSkill = null;
            ClearPress();
        }

        /// <summary>
        /// 给定本带已放入的节点数，返回占用的行数
        /// </summary>
        private static int RowsUsed(int columnIndex) {
            return columnIndex <= 0 ? 0 : (columnIndex - 1) / HalibutTheme.AtlasNodeColumns + 1;
        }

        /// <summary>
        /// 平滑滚动到指定深度带
        /// </summary>
        public void JumpToTier(int tier) {
            tier = Math.Clamp(tier, 0, HalibutTheme.AtlasTierCount - 1);
            scrollTarget = bandStartY[tier] - 110f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.1f + tier * 0.08f });
        }

        /// <summary>
        /// 研究完成回调、飞图标+点亮+下潜+复苏反馈
        /// </summary>
        public void OnStudyCompleted(FishSkill skill, bool atlasVisible) {
            AtlasSkillNode node = nodes.Find(n => n.Skill == skill);
            int flyCount = (int)Math.Clamp(HalibutSave.ResurrectionMaxIncreasePerFish / 3f, 4f, 18f);
            if (node == null || !atlasVisible) {
                node?.TriggerIgnite();
                HalibutHud.Instance?.TriggerGaugeImprove(HalibutHud.Anchor + new Vector2(0f, -50f), flyCount);
                return;
            }
            //镜头下潜到新节点
            scrollTarget = node.LayoutPos.Y - HalibutTheme.UIScreenH * 0.42f;
            Vector2 targetScreen = new(HalibutTheme.UIScreenW * 0.5f + node.LayoutPos.X,
                node.LayoutPos.Y - scrollTarget + lastContentArea.Y);
            particles.SpawnFlyingIcon(skill.Icon, Altar.ScreenCenter, targetScreen, () => {
                node.TriggerIgnite();
                particles.SpawnRingPulse(targetScreen, HalibutTheme.Caustic, 60f, 3.5f);
                particles.SpawnBurst(targetScreen, HalibutTheme.TierColor(node.Tier), 16, 3.6f);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f });
                HalibutHud.Instance?.TriggerGaugeImprove(targetScreen, flyCount);
            });
        }

        public void Update(Rectangle contentArea, HalibutSave save, float alpha, bool inputAvailable) {
            lastContentArea = contentArea;
            particles.Update();

            //滚动
            float maxScroll = MathF.Max(0f, totalHeight - contentArea.Height + DockHeight);
            if (inputAvailable && !Altar.PanelOpen && contentArea.Contains(Main.MouseScreen.ToPoint())) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    scrollTarget -= MathF.Sign(delta) * 96f;
                    UIInputGuard.SuppressWeaponSwitch(5);
                    scrollIdleTimer = 0;
                }
            }
            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, maxScroll);
            scroll = MathHelper.Lerp(scroll, scrollTarget, 0.16f);
            Depth = maxScroll > 1f ? MathHelper.Clamp(scroll / maxScroll, 0f, 1f) : 0f;

            //滚动避让、下潜收挂件，停稳/交互归位
            if (MathF.Abs(scroll - scrollTarget) > 2f) {
                scrollIdleTimer = 0;
            }
            else if (scrollIdleTimer < 600) {
                scrollIdleTimer++;
            }
            bool wantHide = scrollIdleTimer < 26;
            if (draggingSkill != null || Main.MouseScreen.Y > contentArea.Bottom - 150f) {
                wantHide = false;//拖拽中或光标靠近底部 = 玩家需要装备坞
            }
            chromeHide = MathHelper.Lerp(chromeHide, wantHide ? 1f : 0f, 0.10f);

            //祭坛（含次级选鱼面板）
            Altar.ScreenCenter = new Vector2(HalibutTheme.UIScreenW * 0.5f, contentArea.Y + AltarLayoutY - scroll);
            bool altarVisible = Altar.ScreenCenter.Y > contentArea.Y - 60f;
            Altar.Update(contentArea, save, inputAvailable && (altarVisible || Altar.PanelOpen));
            //面板打开时清掉海域选中/拖拽，避免详情卡与拖拽幽灵透出到面板之下
            if (Altar.PanelOpen) {
                selectedNode = null;
                draggingSkill = null;
            }

            //环境粒子（深度驱动）
            ambientTimer++;
            if (ambientTimer % 7 == 0) {
                float x = Main.rand.NextFloat(contentArea.X + 40f, contentArea.Right - 40f);
                if (Depth < 0.45f && Main.rand.NextFloat() > Depth) {
                    particles.SpawnBubble(new Vector2(x, contentArea.Bottom - 20f));
                }
                else {
                    particles.SpawnSnow(new Vector2(x, contentArea.Y + 10f));
                }
            }

            float time = Main.GlobalTimeWrappedHourly;
            Vector2 mouse = Main.MouseScreen;
            bool mouseFree = inputAvailable && !Altar.Hovered && !Altar.PanelOpen;

            //详情卡区域（先于节点计算，避免点击穿透）
            bool detailOpen = selectedNode != null;
            if (detailOpen) {
                int detailW = 264;
                int detailH = 350;
                detailRect = new Rectangle(contentArea.Right - detailW - 18,
                    contentArea.Center.Y - detailH / 2, detailW, detailH);
                if (detailRect.Contains(mouse.ToPoint())) {
                    mouseFree = false;
                }
            }

            //节点命中
            hoveredNode = null;
            if (mouseFree && draggingSkill == null) {
                float best = AtlasSkillNode.HitRadius;
                foreach (var node in nodes) {
                    Vector2 pos = node.ScreenPos(scroll - contentArea.Y, time);
                    if (pos.Y < contentArea.Y - 40f || pos.Y > contentArea.Bottom + 40f) {
                        continue;
                    }
                    float dist = Vector2.Distance(mouse, pos);
                    if (dist < best) {
                        best = dist;
                        hoveredNode = node;
                    }
                }
            }
            foreach (var node in nodes) {
                node.UpdateState(node == hoveredNode);
            }

            //装备坞命中（收起时不可交互）
            dockHoverIndex = -1;
            if (chromeHide < 0.5f && !Altar.PanelOpen) {
                for (int i = 0; i < HalibutTheme.DockSlotCount; i++) {
                    if (Vector2.Distance(mouse, DockSlotPos(contentArea, i)) < HalibutTheme.DockSlotR + 4f) {
                        dockHoverIndex = i;
                    }
                }
            }

            HandleMouseInput(contentArea, save, mouse, inputAvailable && !Altar.PanelOpen);

            if (loadoutFullFlash > 0f) {
                loadoutFullFlash = MathF.Max(loadoutFullFlash - 0.02f, 0f);
            }
        }

        private void HandleMouseInput(Rectangle contentArea, HalibutSave save, Vector2 mouse, bool inputAvailable) {
            if (!inputAvailable) {
                //输入不可用、弃未成形候选，进行中拖拽保留
                if (draggingSkill == null) {
                    ClearPress();
                }
                return;
            }

            //详情卡按钮即时响应，右缘不与节点重叠
            if (selectedNode != null && Main.mouseLeft && Main.mouseLeftRelease
                && detailRect.Contains(mouse.ToPoint())) {
                Main.mouseLeftRelease = false;
                HandleDetailButtons(save, mouse);
                return;
            }

            //按下只登记拖拽候选，不即时开详情
            if (Main.mouseLeft && Main.mouseLeftRelease && draggingSkill == null) {
                pressActive = true;
                pressMouse = mouse;
                pressNode = hoveredNode;
                if (dockHoverIndex >= 0 && dockHoverIndex < save.loadout.Count) {
                    pressFromDock = true;
                    pressDockIndex = dockHoverIndex;
                    pressSkill = save.loadout[dockHoverIndex];
                    Main.mouseLeftRelease = false;
                }
                else {
                    pressFromDock = false;
                    pressDockIndex = -1;
                    //仅已解锁节点可拖拽；锁定节点松手时仍可点击查看详情
                    pressSkill = hoveredNode != null && save.IsUnlocked(hoveredNode.Skill) ? hoveredNode.Skill : null;
                    if (hoveredNode != null) {
                        Main.mouseLeftRelease = false;
                    }
                }
            }

            //按住光标移动越过阈值 = 开始拖拽（用按下时捕获的目标，光标移离原节点也不丢失）
            if (pressActive && draggingSkill == null && pressSkill != null && Main.mouseLeft
                && Vector2.Distance(mouse, pressMouse) > DragStartDist) {
                draggingSkill = pressSkill;
                dragFromDock = pressFromDock;
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.25f });
            }

            //松手、拖拽落点入坞/卸下，否则点击
            if (!Main.mouseLeft) {
                if (draggingSkill != null) {
                    ReleaseDrag(contentArea, save, mouse);
                }
                else if (pressActive) {
                    HandleClick(save, mouse);
                }
                ClearPress();
            }
        }

        /// <summary>
        /// 拖拽落点、入坞装备/移，拖出卸下
        /// </summary>
        private void ReleaseDrag(Rectangle contentArea, HalibutSave save, Vector2 mouse) {
            bool overDockArea = mouse.Y > contentArea.Bottom - DockHeight - 44f;
            if (dockHoverIndex >= 0 || overDockArea) {
                int targetSlot = dockHoverIndex >= 0 ? dockHoverIndex : save.loadout.Count;
                if (save.loadout.Contains(draggingSkill)) {
                    save.MoveLoadout(save.loadout.IndexOf(draggingSkill), Math.Min(targetSlot, save.loadout.Count - 1));
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.4f });
                }
                else if (save.EquipSkill(draggingSkill, targetSlot)) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.1f });
                    particles.SpawnRingPulse(DockSlotPos(contentArea,
                        Math.Min(targetSlot, save.loadout.Count - 1)), HalibutTheme.GlowHi, 38f, 3f);
                }
                else {
                    //装备失败（已满）
                    loadoutFullFlash = 1f;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                }
            }
            else if (dragFromDock) {
                //从装备坞拖出 = 卸下
                save.UnequipSkill(draggingSkill);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.3f });
            }
            draggingSkill = null;
        }

        /// <summary>
        /// 轻点、坞槽选用/节点开详情/空白关
        /// </summary>
        private void HandleClick(HalibutSave save, Vector2 mouse) {
            //装备坞轻点 = 选用该技能
            if (pressFromDock && pressDockIndex >= 0 && pressDockIndex < save.loadout.Count) {
                SkillWheel.HalibutWheelController.LocalInstance?.SelectSkill(save.loadout[pressDockIndex]);
                return;
            }
            //节点轻点 = 打开详情
            if (pressNode != null) {
                selectedNode = pressNode;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
                return;
            }
            //轻点空白 = 关闭详情
            if (selectedNode != null && !detailRect.Contains(mouse.ToPoint())) {
                selectedNode = null;
            }
        }

        /// <summary>
        /// 详情卡按钮命中（装备/卸下、选用）
        /// </summary>
        private void HandleDetailButtons(HalibutSave save, Vector2 mouse) {
            if (selectedNode == null || !save.IsUnlocked(selectedNode.Skill)) {
                return;
            }
            if (equipBtnRect.Contains(mouse.ToPoint())) {
                if (save.loadout.Contains(selectedNode.Skill)) {
                    save.UnequipSkill(selectedNode.Skill);
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.3f });
                }
                else if (save.EquipSkill(selectedNode.Skill)) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.1f });
                }
                else {
                    loadoutFullFlash = 1f;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                }
            }
            else if (selectBtnRect.Contains(mouse.ToPoint())) {
                SkillWheel.HalibutWheelController.LocalInstance?.SelectSkill(selectedNode.Skill);
            }
        }

        /// <summary>
        /// 清空本次按压登记的拖拽候选
        /// </summary>
        private void ClearPress() {
            pressActive = false;
            pressSkill = null;
            pressNode = null;
            pressFromDock = false;
            pressDockIndex = -1;
        }

        /// <summary>
        /// 滚动避让导致的装备坞下沉偏移
        /// </summary>
        private float DockHideOffset => VaultUtils.EaseInCubic(chromeHide) * 92f;

        /// <summary>
        /// 装备坞的大致外接矩形（引导高亮用），随收起偏移一并移动
        /// </summary>
        public Rectangle GetDockBounds() {
            Vector2 left = DockSlotPos(lastContentArea, 0);
            Vector2 right = DockSlotPos(lastContentArea, HalibutTheme.DockSlotCount - 1);
            int r = (int)HalibutTheme.DockSlotR;
            int x = (int)left.X - r - 8;
            int top = (int)MathF.Min(left.Y, right.Y) - r - 6;
            int w = (int)(right.X - left.X) + (r + 8) * 2;
            int h = r * 2 + 16;
            return new Rectangle(x, top, w, h);
        }

        private Vector2 DockSlotPos(Rectangle contentArea, int index) {
            float spacing = HalibutTheme.DockSlotR * 2f + 9f;
            float startX = contentArea.Center.X - (HalibutTheme.DockSlotCount - 1) * spacing * 0.5f;
            //弧形下垂偏移
            float t = index / (float)(HalibutTheme.DockSlotCount - 1);
            float sag = MathF.Sin(t * MathHelper.Pi) * 7f;
            return new Vector2(startX + index * spacing,
                contentArea.Bottom - DockHeight * 0.5f - 16f + sag + DockHideOffset);
        }

        public void Draw(SpriteBatch sb, Rectangle contentArea, HalibutSave save, float alpha) {
            float time = Main.GlobalTimeWrappedHourly;
            float offY = scroll - contentArea.Y;

            //深度带分界线与标题
            for (int t = 0; t < HalibutTheme.AtlasTierCount; t++) {
                float bandScreenY = bandStartY[t] - offY;
                if (bandScreenY < contentArea.Y - 60f || bandScreenY > contentArea.Bottom + 30f) {
                    continue;
                }
                Color tierCol = HalibutTheme.TierColor(t);
                Vector2 lineL = new(contentArea.X + 70f, bandScreenY);
                Vector2 lineR = new(contentArea.Right - 70f, bandScreenY);
                HalibutRenderer.DrawGradientLine(sb, lineL, contentArea.Center.ToVector2() with { Y = bandScreenY },
                    tierCol * (0.05f * alpha), tierCol * (0.5f * alpha), 1.2f);
                HalibutRenderer.DrawGradientLine(sb, contentArea.Center.ToVector2() with { Y = bandScreenY }, lineR,
                    tierCol * (0.5f * alpha), tierCol * (0.05f * alpha), 1.2f);
                HalibutRenderer.DrawGlowTextCentered(sb, HalibutAtlas.TierName(t),
                    new Vector2(contentArea.Center.X, bandScreenY + 19f),
                    tierCol * alpha, tierCol * (0.3f * alpha), 0.92f);
            }

            //节点
            foreach (var node in nodes) {
                Vector2 pos = node.ScreenPos(offY, time);
                if (pos.Y < contentArea.Y - 50f || pos.Y > contentArea.Bottom + 50f) {
                    continue;
                }
                bool unlocked = save.IsUnlocked(node.Skill);
                bool equipped = save.loadout.Contains(node.Skill);
                bool selected = save.FishSkill == node.Skill;
                float nodeAlpha = alpha;
                //装备坞区域附近淡出，避免与坞重叠（坞收起时淡出带跟着下移）
                float dockTop = contentArea.Bottom - DockHeight - 38f + DockHideOffset;
                if (pos.Y > dockTop) {
                    nodeAlpha *= MathHelper.Clamp(1f - (pos.Y - dockTop) / 50f, 0f, 1f);
                }
                node.Draw(sb, pos, unlocked, equipped, selected, nodeAlpha, time);
            }

            //祭坛（海面）
            if (Altar.ScreenCenter.Y > contentArea.Y - 120f) {
                Altar.Draw(sb, save, alpha * MathHelper.Clamp(1f - Depth * 2.2f + 0.35f, 0f, 1f), time);
            }

            particles.Draw(sb, alpha);

            //右缘深度刻度
            DrawDepthRuler(sb, contentArea, alpha);

            //装备坞
            DrawDock(sb, contentArea, save, alpha, time);

            //详情卡
            if (selectedNode != null) {
                DrawDetailCard(sb, save, alpha, time);
            }

            //拖拽幽灵
            if (draggingSkill?.Icon != null) {
                Texture2D icon = draggingSkill.Icon;
                float scale = 36f / MathF.Max(icon.Width, icon.Height);
                Vector2 mouse = Main.MouseScreen;
                sb.Draw(icon, mouse, null, HalibutTheme.Glow with { A = 0 } * (0.55f * alpha),
                    0f, icon.Size() * 0.5f, scale * 1.3f, SpriteEffects.None, 0f);
                sb.Draw(icon, mouse, null, Color.White * alpha, 0f, icon.Size() * 0.5f,
                    scale * 1.1f, SpriteEffects.None, 0f);
            }

            //节点悬浮提示（锁定节点显示解锁来源）
            if (hoveredNode != null && draggingSkill == null && !save.IsUnlocked(hoveredNode.Skill)) {
                string fishName = Lang.GetItemNameValue(hoveredNode.Skill.UnlockFishID);
                HalibutRenderer.DrawCursorPanel(sb, Main.MouseScreen, HalibutAtlas.LockedNodeName.Value,
                    HalibutTheme.TextDim, string.Format(HalibutAtlas.LockedNodeHint.Value, fishName), alpha);
            }

            //次级选鱼面板（最上层，含全屏遮罩）
            Altar.DrawPanel(sb, contentArea, save, alpha, time);
        }

        private void DrawDepthRuler(SpriteBatch sb, Rectangle contentArea, float alpha) {
            float x = contentArea.Right - 26f;
            Vector2 top = new(x, contentArea.Y + 30f);
            Vector2 bottom = new(x, contentArea.Bottom - DockHeight - 30f);
            HalibutRenderer.DrawLine(sb, top, bottom, 1f, HalibutTheme.Teal * (0.5f * alpha));
            //当前位置标记
            float h = bottom.Y - top.Y;
            Vector2 marker = top + new Vector2(0f, h * Depth);
            HalibutRenderer.DrawDisc(sb, marker, 3f, 2f, HalibutTheme.GlowHi * (0.9f * alpha));
            HalibutRenderer.DrawLine(sb, marker + new Vector2(-5f, 0f), marker + new Vector2(5f, 0f),
                1.2f, HalibutTheme.GlowHi * (0.7f * alpha));
        }

        private void DrawDock(SpriteBatch sb, Rectangle contentArea, HalibutSave save, float alpha, float time) {
            //滚动避让、收起下沉淡出
            alpha *= 1f - chromeHide;
            if (alpha < 0.02f) {
                return;
            }
            //槽位连线弧
            Vector2 prev = Vector2.Zero;
            for (int i = 0; i < HalibutTheme.DockSlotCount; i++) {
                Vector2 pos = DockSlotPos(contentArea, i);
                if (i > 0) {
                    HalibutRenderer.DrawLine(sb, prev, pos, 1f, HalibutTheme.Teal * (0.45f * alpha));
                }
                prev = pos;
            }

            //标签
            float labelY = contentArea.Bottom - DockHeight - 36f + DockHideOffset;
            Color labelCol = loadoutFullFlash > 0.01f
                ? Color.Lerp(HalibutTheme.TextDim, HalibutTheme.Danger, MathF.Sin(loadoutFullFlash * MathHelper.Pi))
                : HalibutTheme.TextDim;
            string label = string.Format(HalibutAtlas.DockLabel.Value, save.loadout.Count, HalibutSave.LoadoutCap);
            if (loadoutFullFlash > 0.01f) {
                label = HalibutAtlas.LoadoutFullHint.Value;
            }
            HalibutRenderer.DrawGlowTextCentered(sb, label,
                new Vector2(contentArea.Center.X, labelY),
                labelCol * alpha, HalibutTheme.Deep * (0.4f * alpha), 0.74f);

            //槽位
            for (int i = 0; i < HalibutTheme.DockSlotCount; i++) {
                Vector2 pos = DockSlotPos(contentArea, i);
                bool occupied = i < save.loadout.Count;
                bool hovered = dockHoverIndex == i;
                FishSkill skill = occupied ? save.loadout[i] : null;
                bool isCurrent = skill != null && skill == save.FishSkill;

                //槽底
                HalibutRenderer.DrawDisc(sb, pos, HalibutTheme.DockSlotR - 3f, 3f,
                    HalibutTheme.Deep * ((occupied ? 0.92f : 0.6f) * alpha));
                Color ringCol = isCurrent ? HalibutTheme.Accent
                    : hovered ? HalibutTheme.GlowHi
                    : occupied ? HalibutTheme.Glow : HalibutTheme.Disabled;
                float ringA = occupied ? 0.8f : 0.4f;
                HalibutRenderer.DrawRing(sb, pos, HalibutTheme.DockSlotR, 1.3f, ringCol * (ringA * alpha));

                if (skill?.Icon != null && skill != draggingSkill) {
                    float scale = (HalibutTheme.DockSlotR * 2f - 10f) / MathF.Max(skill.Icon.Width, skill.Icon.Height);
                    scale *= 1f + (hovered ? 0.12f : 0f);
                    sb.Draw(skill.Icon, pos, null, Color.White * alpha, 0f,
                        skill.Icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                    if (skill.CooldownRatio > 0.01f) {
                        HalibutRenderer.DrawCooldownSweep(sb, pos, HalibutTheme.DockSlotR - 4f,
                            skill.CooldownRatio, alpha);
                    }
                    if (isCurrent) {
                        float pulse = HalibutTheme.Breath(time, i, 3f);
                        HalibutRenderer.DrawRing(sb, pos, HalibutTheme.DockSlotR + 3f + pulse * 1.6f, 1f,
                            HalibutTheme.Accent * ((0.5f + pulse * 0.3f) * alpha));
                    }
                }
                else if (!occupied && draggingSkill != null && hovered) {
                    //拖拽悬停的空槽位高亮
                    HalibutRenderer.DrawDisc(sb, pos, HalibutTheme.DockSlotR - 6f, 3f,
                        HalibutTheme.Glow * (0.3f * alpha));
                }
            }
        }

        private void DrawDetailCard(SpriteBatch sb, HalibutSave save, float alpha, float time) {
            FishSkill skill = selectedNode.Skill;
            bool unlocked = save.IsUnlocked(skill);
            Color tierCol = HalibutTheme.TierColor(selectedNode.Tier);

            HalibutRenderer.DrawSeaPanel(sb, detailRect, alpha, 0.45f + selectedNode.Tier * 0.15f, 0f, 0.55f);
            HalibutRenderer.DrawOrnateFrame(sb, detailRect,
                Color.Lerp(tierCol, HalibutTheme.Glow, 0.35f), alpha * 0.9f, time, 13f);

            float pad = 16f;
            float x = detailRect.X + pad;
            float y = detailRect.Y + pad;

            //图标 + 名称
            Texture2D icon = skill.Icon;
            if (icon != null) {
                Vector2 iconCenter = new(x + 25f, y + 25f);
                if (unlocked) {
                    HalibutRenderer.DrawSoftGlow(sb, iconCenter, 30f, tierCol * (0.4f * alpha));
                    float scale = 44f / MathF.Max(icon.Width, icon.Height);
                    sb.Draw(icon, iconCenter, null, Color.White * alpha, 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                }
                else {
                    float scale = 44f / MathF.Max(icon.Width, icon.Height);
                    sb.Draw(icon, iconCenter, null, HalibutTheme.Void * (0.9f * alpha), 0f,
                        icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                    HalibutRenderer.DrawGlowTextCentered(sb, "?", iconCenter,
                        HalibutTheme.TextDim * alpha, HalibutTheme.Deep * (0.4f * alpha), 1f);
                }
            }
            string name = unlocked ? skill.DisplayName?.Value ?? skill.Name : HalibutAtlas.LockedNodeName.Value;
            HalibutRenderer.DrawGlowText(sb, name, new Vector2(x + 58f, y + 6f),
                (unlocked ? HalibutTheme.GlowHi : HalibutTheme.TextDim) * alpha,
                tierCol * (0.4f * alpha), 0.92f);
            //深度带标签
            HalibutRenderer.DrawGlowText(sb, HalibutAtlas.TierName(selectedNode.Tier),
                new Vector2(x + 58f, y + 28f), tierCol * (0.85f * alpha), tierCol * (0.25f * alpha), 0.7f);

            y += 62f;
            HalibutRenderer.DrawPearl(sb, new Vector2(x + 1.5f, y), 1.8f, HalibutTheme.Caustic, 0.85f * alpha);
            HalibutRenderer.DrawGradientLine(sb, new Vector2(x + 6f, y), new Vector2(detailRect.Right - pad, y),
                tierCol * (0.7f * alpha), tierCol * (0.06f * alpha), 1.2f);
            y += 10f;

            //描述正文
            string body = unlocked
                ? skill.Tooltip?.Value ?? string.Empty
                : string.Format(HalibutAtlas.LockedNodeHint.Value, Lang.GetItemNameValue(skill.UnlockFishID));
            const float bodyScale = 0.74f;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            //WordwrapString 内部数组按 maxLines 分配，超出会越界；可见行数由下方 bodyBottom 裁剪
            int wrapPx = Math.Max(32, (int)((detailRect.Width - pad * 2f) / bodyScale) + 30);
            string[] lines = string.IsNullOrEmpty(body)
                ? []
                : VaultUtils.WrapTextArray(body, font, wrapPx, 99, out _);
            float bodyBottom = detailRect.Bottom - pad - 46f;
            foreach (string raw in lines) {
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }
                if (y + 17f > bodyBottom) {
                    break;
                }
                string line = raw.TrimEnd('-', ' ');
                Utils.DrawBorderString(sb, line, new Vector2(x + 1f, y + 1f), Color.Black * (alpha * 0.5f), bodyScale);
                Utils.DrawBorderString(sb, line, new Vector2(x, y), HalibutTheme.Text * alpha, bodyScale);
                y += 17.5f;
            }

            //解锁来源（已解锁也展示来源鱼）
            if (unlocked && skill.UnlockFishID > ItemID.None) {
                string src = string.Format(HalibutAtlas.UnlockFishLine.Value, Lang.GetItemNameValue(skill.UnlockFishID));
                Utils.DrawBorderString(sb, src, new Vector2(x, bodyBottom + 2f), HalibutTheme.TextDim * alpha, 0.66f);
            }

            //按钮（仅已解锁可用）
            if (unlocked) {
                int btnW = (detailRect.Width - (int)pad * 2 - 10) / 2;
                int btnH = 26;
                equipBtnRect = new Rectangle(detailRect.X + (int)pad, detailRect.Bottom - btnH - (int)pad + 4, btnW, btnH);
                selectBtnRect = new Rectangle(equipBtnRect.Right + 10, equipBtnRect.Y, btnW, btnH);
                Vector2 mouse = Main.MouseScreen;
                equipBtnHover = equipBtnRect.Contains(mouse.ToPoint());
                selectBtnHover = selectBtnRect.Contains(mouse.ToPoint());

                bool equipped = save.loadout.Contains(skill);
                HalibutRenderer.DrawCapsuleButton(sb, equipBtnRect,
                    equipped ? HalibutAtlas.UnequipBtn.Value : HalibutAtlas.EquipBtn.Value,
                    equipped ? HalibutTheme.TextDim : HalibutTheme.Glow, equipBtnHover, equipped, alpha, time);
                bool isCurrent = save.FishSkill == skill;
                HalibutRenderer.DrawCapsuleButton(sb, selectBtnRect,
                    isCurrent ? HalibutAtlas.SelectedTag.Value : HalibutAtlas.SelectBtn.Value,
                    isCurrent ? HalibutTheme.Accent : HalibutTheme.GlowHi, selectBtnHover && !isCurrent, isCurrent, alpha, time);
            }
            else {
                equipBtnRect = selectBtnRect = Rectangle.Empty;
            }
        }
    }
}
