using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 沉影盘：湖畔村图点血湖后自湖面铺开的编成面板（画境的一部分，不是独立 UIHandle）。
    /// 上半是收集册沉影弧——沉溺过的 boss 拓在泥岸上；下半没在水里的是三席影位。
    /// 交互走「拾影在手」：点弧上沉影拾起（影随光标），点影位落座；
    /// 空手点驻影拾走，再点空处卸回湖底（右键影位也可卸）。点回原席才是再驻上。
    /// 落座/腾席即时生效——驻影自动出水随行。
    /// 席间成边（梦火/沸雨/雨魇）与三影镇湖在盘上明示；回执写成盘底批注，拒绝有声有震——
    /// 点击无反馈是硬禁忌。数据只写本机 <see cref="KikasaServantPlayer"/>（储钱罐语义）
    /// </summary>
    internal class KikasaBoardPanel
    {
        //==================== 状态 ====================

        internal bool IsOpen { get; private set; }

        private float openLerp;

        //拾在手里的记忆键（0=空手）；自影位拾起时记来处，放回原位=收手
        private int pickedKey;
        private int pickedFromSlot = -1;
        private Vector2 pickedPos;
        private Vector2 pickedTrail;

        //悬停对象（互斥）：弧上条目序 / 影位序
        private int hoverEntry = -1;
        private int hoverSlot = -1;

        //落座定妆与拒绝横震（逐席）
        private readonly float[] slotPulse = new float[KikasaServantPlayer.SlotCount];
        private readonly float[] slotShake = new float[KikasaServantPlayer.SlotCount];

        //盘底批注：一句回执，写后自灭
        private string noteText;
        private int noteTimer;
        private const int NoteFrames = 150;

        //组合边差分：新成边时批注点一句
        private bool prevDreamFire;
        private bool prevBoilRain;
        private bool prevRainNightmare;
        private bool prevTriSeal;

        //收集弧条目（每帧自玩家册重建；小列表，无分配压力可忽略）
        private readonly List<int> entryKeys = [];
        private readonly List<Vector2> entryPos = [];
        private readonly List<float> entryHover = [];

        private Rectangle boardRect;
        private float waterPixY;
        private readonly Vector2[] slotCenter = new Vector2[KikasaServantPlayer.SlotCount];

        /// <summary>可见（含开合动画余量）</summary>
        internal bool Visible => IsOpen || openLerp > 0.01f;

        internal void Reset() {
            IsOpen = false;
            openLerp = 0f;
            pickedKey = 0;
            pickedFromSlot = -1;
            hoverEntry = -1;
            hoverSlot = -1;
            noteText = null;
            noteTimer = 0;
            Array.Clear(slotPulse, 0, slotPulse.Length);
            Array.Clear(slotShake, 0, slotShake.Length);
        }

        internal void Open() {
            if (IsOpen) {
                return;
            }
            IsOpen = true;
            pickedKey = 0;
            pickedFromSlot = -1;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.45f, MaxInstances = 2 });
            //开盘时以当前编成为差分基线，别把既有边当新边报
            Player player = Main.LocalPlayer;
            prevDreamFire = KikasaEffigyBoard.HasDreamFireEdge(player);
            prevBoilRain = KikasaEffigyBoard.HasBoilRainEdge(player);
            prevRainNightmare = KikasaEffigyBoard.HasRainNightmareEdge(player);
            prevTriSeal = KikasaEffigyBoard.HasTriSeal(player);
        }

        internal void Close() {
            if (!IsOpen) {
                return;
            }
            IsOpen = false;
            pickedKey = 0;
            pickedFromSlot = -1;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.75f, MaxInstances = 2 });
        }

        private void SetNote(string text) {
            noteText = text;
            noteTimer = NoteFrames;
        }

        private static void Refuse() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 });
        }

        //==================== 布局 ====================

        /// <summary>盘身自血湖热区铺开到近乎整幅画心：空间连续，盘是湖面的特写</summary>
        private void Layout(Rectangle canvas) {
            Rectangle lake = KikasaSceneTheme.UvToScreen(canvas, KikasaSceneTheme.LakeHotspot);
            Rectangle full = canvas;
            full.Inflate(-14, -10);
            float ease = 1f - MathF.Pow(1f - openLerp, 3f);
            boardRect = new Rectangle(
                (int)MathHelper.Lerp(lake.X, full.X, ease),
                (int)MathHelper.Lerp(lake.Y, full.Y, ease),
                (int)MathHelper.Lerp(lake.Width, full.Width, ease),
                (int)MathHelper.Lerp(lake.Height, full.Height, ease));
            waterPixY = boardRect.Y + boardRect.Height * 0.60f;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                float shake = slotShake[i] > 0f
                    ? MathF.Sin(slotShake[i] * 1.7f) * slotShake[i] * 0.45f : 0f;
                slotCenter[i] = new Vector2(
                    boardRect.Center.X + (i - 1) * boardRect.Width * 0.22f + shake,
                    boardRect.Y + boardRect.Height * 0.78f);
            }
        }

        /// <summary>
        /// 收集弧排布：条目沿上拱的弧摆开，间距随枚数收放，超过 9 枚拆内外两行
        /// （几何与命中同一份）；逐键散列微抖——泥上的拓影不该排成印刷体
        /// </summary>
        private void LayoutEntries(KikasaServantPlayer servant) {
            entryKeys.Clear();
            entryPos.Clear();
            entryKeys.AddRange(servant.BuildCodexKeys());
            while (entryHover.Count < entryKeys.Count) {
                entryHover.Add(0f);
            }
            if (entryHover.Count > entryKeys.Count) {
                entryHover.RemoveRange(entryKeys.Count, entryHover.Count - entryKeys.Count);
            }

            int count = entryKeys.Count;
            if (count == 0) {
                return;
            }
            Vector2 arcCenter = new(boardRect.Center.X, boardRect.Y + boardRect.Height * 1.16f);
            float radiusOuter = boardRect.Height * 0.92f;
            bool twoRows = count > 9;
            int outerCount = twoRows ? (count + 1) / 2 : count;
            int innerCount = count - outerCount;

            void PlaceRow(int rowCount, int startIndexStep, float radius) {
                if (rowCount <= 0) {
                    return;
                }
                float spread = MathHelper.ToRadians(MathF.Min(104f, 15f * (rowCount - 1) + 8f));
                for (int k = 0; k < rowCount; k++) {
                    int index = startIndexStep == 2 ? k * 2 : k * 2 + 1;
                    if (!twoRows) {
                        index = k;
                    }
                    if (index >= count) {
                        continue;
                    }
                    float theta = MathHelper.PiOver2
                        + (rowCount <= 1 ? 0f : (k - (rowCount - 1) * 0.5f) * (spread / MathF.Max(rowCount - 1, 1)));
                    int key = entryKeys[index];
                    float hx = Hash01(key * 0.731f) - 0.5f;
                    float hy = Hash01(key * 1.377f) - 0.5f;
                    Vector2 pos = arcCenter
                        + new Vector2(MathF.Cos(theta), -MathF.Sin(theta)) * radius
                        + new Vector2(hx * 7f, hy * 5f);
                    while (entryPos.Count <= index) {
                        entryPos.Add(Vector2.Zero);
                    }
                    entryPos[index] = pos;
                }
            }

            PlaceRow(outerCount, 2, radiusOuter);
            PlaceRow(innerCount, 1, radiusOuter - boardRect.Height * 0.155f);
        }

        private static float Hash01(float seed)
            => MathF.Abs(MathF.Sin(seed * 12.9898f) * 43758.5453f) % 1f;

        //==================== 更新 ====================

        internal void Update(Player player, Rectangle canvas, bool inputAvailable,
            Vector2 mouse, KeyPressState left, KeyPressState right) {
            openLerp = MathHelper.Clamp(openLerp + (IsOpen ? 0.09f : -0.11f), 0f, 1f);
            if (openLerp < 0.01f) {
                return;
            }
            Layout(canvas);
            KikasaServantPlayer servant = player.GetModPlayer<KikasaServantPlayer>();
            LayoutEntries(servant);

            for (int i = 0; i < slotPulse.Length; i++) {
                slotPulse[i] *= 0.9f;
                if (slotShake[i] > 0f) {
                    slotShake[i] -= 1f;
                }
            }
            if (noteTimer > 0) {
                noteTimer--;
            }

            //拾影跟手：弹簧趋近 + 一点拖尾
            if (pickedKey != 0) {
                pickedPos = Vector2.Lerp(pickedPos, mouse, 0.35f);
                pickedTrail = Vector2.Lerp(pickedTrail, pickedPos, 0.22f);
            }

            //====== 悬停 ======
            hoverEntry = -1;
            hoverSlot = -1;
            bool interactive = IsOpen && openLerp > 0.85f && inputAvailable;
            if (interactive) {
                for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                    if (Vector2.Distance(mouse, slotCenter[i]) < 32f) {
                        hoverSlot = i;
                        break;
                    }
                }
                if (hoverSlot < 0) {
                    for (int i = 0; i < entryKeys.Count && i < entryPos.Count; i++) {
                        if (Vector2.Distance(mouse, entryPos[i]) < 23f) {
                            hoverEntry = i;
                            break;
                        }
                    }
                }
            }
            for (int i = 0; i < entryHover.Count; i++) {
                entryHover[i] = MathHelper.Lerp(entryHover[i], i == hoverEntry ? 1f : 0f, 0.18f);
            }

            //====== 点击 ======
            if (interactive && left == KeyPressState.Pressed) {
                HandleLeftClick(player, servant, mouse);
            }
            //右键：手里有影=放下（席上拾来的不回席，即卸下）；空手点驻影=直接卸
            if (interactive && right == KeyPressState.Pressed) {
                if (pickedKey != 0) {
                    DropPicked();
                }
                else if (hoverSlot >= 0 && servant.SlotKeyAt(hoverSlot) != 0) {
                    int key = servant.SlotKeyAt(hoverSlot);
                    if (servant.ClearSlot(hoverSlot)) {
                        SetNote(string.Format(KikasaSceneUI.BoardUnslottedFormat.Value,
                            KikasaServantPlayer.KeyDisplayName(key)));
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.45f, Pitch = -0.7f, MaxInstances = 2
                        });
                    }
                }
            }

            //====== 组合边差分：新成边点一句批注 ======
            DiffEdges(player);
        }

        private void HandleLeftClick(Player player, KikasaServantPlayer servant, Vector2 mouse) {
            //点影位
            if (hoverSlot >= 0) {
                int slotKey = servant.SlotKeyAt(hoverSlot);
                if (pickedKey != 0) {
                    //放回来处=收手，不算变更
                    if (hoverSlot == pickedFromSlot && slotKey == 0) {
                        servant.TrySetSlot(hoverSlot, pickedKey);
                        DropQuiet();
                        return;
                    }
                    if (servant.TrySetSlot(hoverSlot, pickedKey)) {
                        SetNote(string.Format(KikasaSceneUI.BoardPlacedFormat.Value,
                            KikasaServantPlayer.KeyDisplayName(pickedKey)));
                        slotPulse[hoverSlot] = 1f;
                        pickedKey = 0;
                        pickedFromSlot = -1;
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 });
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 });
                    }
                    else {
                        //同影同席之类：横震轻拒
                        slotShake[hoverSlot] = 14f;
                        Refuse();
                    }
                    return;
                }
                if (slotKey != 0) {
                    //空手拾走驻影：席空了，点空处才卸得成；点回原席是反悔再驻
                    pickedKey = slotKey;
                    pickedFromSlot = hoverSlot;
                    servant.ClearSlot(hoverSlot);
                    pickedPos = pickedTrail = mouse;
                    SetNote(KikasaSceneUI.BoardUnslotHint.Value);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 2 });
                }
                else {
                    //空手点空席：批注说下一步，别让人白点
                    SetNote(KikasaSceneUI.BoardPickHint.Value);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 2 });
                }
                return;
            }

            //点收集弧条目
            if (hoverEntry >= 0 && hoverEntry < entryKeys.Count) {
                int key = entryKeys[hoverEntry];
                if (pickedKey == key) {
                    //再点原影=收手
                    DropPicked();
                    return;
                }
                int slotted = servant.SlotIndexOf(key);
                if (slotted >= 0) {
                    //已驻席的影：从席上拾走。点空处卸下，点回原席再驻
                    pickedKey = key;
                    pickedFromSlot = slotted;
                    servant.ClearSlot(slotted);
                    SetNote(KikasaSceneUI.BoardUnslotHint.Value);
                }
                else {
                    pickedKey = key;
                    pickedFromSlot = -1;
                }
                pickedPos = pickedTrail = KikasaHudTheme.UIMouse;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 2 });
                return;
            }

            //点盘上空处：拾着影=收手；空手=合盘（盘外点击由画境层分发）
            if (boardRect.Contains(mouse.ToPoint())) {
                if (pickedKey != 0) {
                    DropPicked();
                }
                else {
                    Close();
                }
            }
            else {
                if (pickedKey != 0) {
                    DropPicked();
                }
                else {
                    Close();
                }
            }
        }

        /// <summary>
        /// 收手。岸上拾来的只是放下；席上拾来的不回席——影回湖底，这才是卸下。
        /// 要再驻，点回空着的原席
        /// </summary>
        private void DropPicked() {
            if (pickedFromSlot >= 0 && pickedKey != 0) {
                SetNote(string.Format(KikasaSceneUI.BoardUnslottedFormat.Value,
                    KikasaServantPlayer.KeyDisplayName(pickedKey)));
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.45f, Pitch = -0.7f, MaxInstances = 2
                });
            }
            DropQuiet();
        }

        private void DropQuiet() {
            pickedKey = 0;
            pickedFromSlot = -1;
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 });
        }

        private void DiffEdges(Player player) {
            bool dreamFire = KikasaEffigyBoard.HasDreamFireEdge(player);
            bool boilRain = KikasaEffigyBoard.HasBoilRainEdge(player);
            bool rainNightmare = KikasaEffigyBoard.HasRainNightmareEdge(player);
            bool triSeal = KikasaEffigyBoard.HasTriSeal(player);
            //三影镇湖压过两两边——齐印那一下报大的
            if (triSeal && !prevTriSeal) {
                SetNote(KikasaSceneUI.EdgeTriSealNote.Value);
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 });
            }
            else if (dreamFire && !prevDreamFire) {
                SetNote(KikasaSceneUI.EdgeDreamFireNote.Value);
            }
            else if (boilRain && !prevBoilRain) {
                SetNote(KikasaSceneUI.EdgeBoilRainNote.Value);
            }
            else if (rainNightmare && !prevRainNightmare) {
                SetNote(KikasaSceneUI.EdgeRainNightmareNote.Value);
            }
            prevDreamFire = dreamFire;
            prevBoilRain = boilRain;
            prevRainNightmare = rainNightmare;
            prevTriSeal = triSeal;
        }

        //==================== 绘制 ====================

        internal void Draw(SpriteBatch sb, Player player, Rectangle canvas,
            float sceneAlpha, float rain, float time) {
            float a = openLerp * sceneAlpha;
            if (a < 0.02f) {
                return;
            }
            Layout(canvas);
            KikasaServantPlayer servant = player.GetModPlayer<KikasaServantPlayer>();
            KikasaVaultPlayer vault = player.GetModPlayer<KikasaVaultPlayer>();
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            //1 盘底：湿纸卡 + 下半没水
            KikasaSceneUI.DrawCardBg(sb, boardRect, a * 0.98f, rain);
            Rectangle water = new(boardRect.X + 3, (int)waterPixY,
                boardRect.Width - 6, boardRect.Bottom - (int)waterPixY - 3);
            sb.Draw(px, water, KikasaHudTheme.Deep(rain) * (0.55f * a));

            //2 题头与收集计数
            float chromeA = MathHelper.Clamp((openLerp - 0.5f) / 0.5f, 0f, 1f) * sceneAlpha;
            if (chromeA > 0.02f) {
                Utils.DrawBorderString(sb, KikasaSceneUI.BoardTitle.Value,
                    new Vector2(boardRect.X + 18f, boardRect.Y + 12f),
                    KikasaHudTheme.Text(rain) * chromeA, 0.92f);
                string count = string.Format(KikasaSceneUI.BoardCountFormat.Value,
                    servant.CollectedServantCount, KikasaServantPlayer.ServantCodexTotal);
                Vector2 countSize = font.MeasureString(count) * 0.72f;
                Utils.DrawBorderString(sb, count,
                    new Vector2(boardRect.Right - 16f - countSize.X, boardRect.Y + 16f),
                    KikasaHudTheme.TextDim(rain) * chromeA, 0.72f);
            }

            float detailA = MathHelper.Clamp((openLerp - 0.62f) / 0.38f, 0f, 1f) * sceneAlpha;
            if (detailA < 0.02f) {
                return;
            }

            //3 组合边与水线（加色亮件）
            KikasaVaultRenderer.BeginAdditive(sb);
            KikasaVaultRenderer.DrawLine(sb,
                new Vector2(boardRect.X + 10f, waterPixY),
                new Vector2(boardRect.Right - 10f, waterPixY), 1.2f,
                KikasaHudTheme.Glow(rain) * (0.30f * detailA));
            DrawEdgeLines(sb, player, servant, detailA, time);
            //空席相邀：拾着影时空位鬼火环呼吸
            if (pickedKey != 0) {
                for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                    if (servant.SlotKeyAt(i) != 0) {
                        continue;
                    }
                    float breath = KikasaSceneTheme.Breath(time, i * 2.3f, 2.6f);
                    KikasaVaultRenderer.DrawRing(sb, slotCenter[i],
                        24f + breath * 4f, 6f,
                        KikasaWisps.KikasaWisp.GoldBody * ((0.22f + breath * 0.14f) * detailA));
                }
            }
            //落座定妆脉冲
            for (int i = 0; i < slotPulse.Length; i++) {
                if (slotPulse[i] > 0.03f) {
                    float p = 1f - slotPulse[i];
                    KikasaVaultRenderer.DrawRing(sb, slotCenter[i],
                        18f + p * 26f, 7f * slotPulse[i],
                        KikasaHudTheme.Glow(rain) * (0.5f * slotPulse[i] * detailA));
                }
            }
            KikasaVaultRenderer.RestoreUIBatch(sb);

            //4 影位（座圈 + 驻影/空席）
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                DrawSlot(sb, servant, vault, i, detailA, rain, time);
            }

            //5 收集弧（沉影拓在泥岸上）
            if (entryKeys.Count == 0) {
                string empty = string.Format(KikasaSceneUI.BoardEmptyHint.Value,
                    CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value));
                Vector2 size = font.MeasureString(empty) * 0.78f;
                float breathe = 0.6f + 0.3f * KikasaSceneTheme.Breath(time, 1.1f, 1.6f);
                Utils.DrawBorderString(sb, empty,
                    new Vector2(boardRect.Center.X - size.X * 0.5f,
                        boardRect.Y + boardRect.Height * 0.34f),
                    KikasaHudTheme.TextDim(rain) * (breathe * detailA), 0.78f);
            }
            else {
                for (int i = 0; i < entryKeys.Count && i < entryPos.Count; i++) {
                    DrawEntry(sb, servant, vault, i, detailA, rain, time);
                }
            }

            //6 拾在手里的影：拖尾残影 + 本体微放大
            if (pickedKey != 0) {
                DrawEffigyByKey(sb, pickedKey, pickedTrail, 34f, detailA * 0.35f,
                    submerge: 0.6f, tamed: true, absent: false, rain, 0.5f);
                DrawEffigyByKey(sb, pickedKey, pickedPos, 40f, detailA * 0.95f,
                    submerge: 0.55f, tamed: true, absent: false, rain, 0.65f);
            }

            //7 底行：亲和计数（左）· 批注（中）· 湖力（右）
            DrawFooter(sb, player, domain, font, detailA, rain, time);

            //8 悬停名牌
            DrawHoverTip(sb, servant, vault, font, detailA, rain);
        }

        /// <summary>席间成边的连线与边名；三影齐时三线汇心一枚脉环</summary>
        private void DrawEdgeLines(SpriteBatch sb, Player player, KikasaServantPlayer servant,
            float a, float time) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                for (int j = i + 1; j < KikasaServantPlayer.SlotCount; j++) {
                    string edge = EdgeNameOf(servant.SlotAffinity(i), servant.SlotAffinity(j));
                    if (edge == null) {
                        continue;
                    }
                    Vector2 from = slotCenter[i];
                    Vector2 to = slotCenter[j];
                    KikasaVaultRenderer.DrawLine(sb, from, to, 1.1f,
                        KikasaHudTheme.Accent(0f) * (0.35f * a));
                    //一段亮笔沿边巡行：边是通着的
                    float run = (time * 0.5f + i * 0.37f) % 1f;
                    Vector2 spark = Vector2.Lerp(from, to, run);
                    KikasaVaultRenderer.DrawGlowDot(sb, spark, 3.2f,
                        KikasaHudTheme.Glow(0f) * (0.4f * a));
                    Vector2 mid = (from + to) * 0.5f + new Vector2(0f, -12f);
                    Vector2 size = font.MeasureString(edge) * 0.62f;
                    Utils.DrawBorderString(sb, edge, mid - size * 0.5f,
                        KikasaHudTheme.Glow(0f) * (0.85f * a), 0.62f);
                }
            }
            if (KikasaEffigyBoard.HasTriSeal(player)) {
                Vector2 centroid = (slotCenter[0] + slotCenter[1] + slotCenter[2]) / 3f
                    + new Vector2(0f, -26f);
                float pulse = KikasaSceneTheme.Breath(time, 0.7f, 1.8f);
                KikasaVaultRenderer.DrawRing(sb, centroid, 10f + pulse * 5f, 3.5f,
                    KikasaHudTheme.Glow(0f) * ((0.30f + pulse * 0.2f) * a));
                string seal = KikasaSceneUI.EdgeTriSeal.Value;
                Vector2 size = font.MeasureString(seal) * 0.66f;
                Utils.DrawBorderString(sb, seal,
                    centroid + new Vector2(-size.X * 0.5f, -26f),
                    KikasaHudTheme.Text(0f) * (0.9f * a), 0.66f);
            }
        }

        /// <summary>两席亲和能否成边及边名；同系不成边（只叠强度），百搭顶任意一系</summary>
        private static string EdgeNameOf(KikasaAffinity a, KikasaAffinity b) {
            if (a == KikasaAffinity.None || b == KikasaAffinity.None) {
                return null;
            }
            if (a == b) {
                return null;
            }
            bool Covers(KikasaAffinity x)
                => a == x || b == x || a == KikasaAffinity.Wild || b == KikasaAffinity.Wild;
            if (Covers(KikasaAffinity.Flame) && Covers(KikasaAffinity.Nightmare)) {
                return KikasaSceneUI.EdgeDreamFire.Value;
            }
            if (Covers(KikasaAffinity.Flame) && Covers(KikasaAffinity.Rain)) {
                return KikasaSceneUI.EdgeBoilRain.Value;
            }
            if (Covers(KikasaAffinity.Nightmare) && Covers(KikasaAffinity.Rain)) {
                return KikasaSceneUI.EdgeRainNightmare.Value;
            }
            return null;
        }

        private void DrawSlot(SpriteBatch sb, KikasaServantPlayer servant, KikasaVaultPlayer vault,
            int index, float a, float rain, float time) {
            Vector2 center = slotCenter[index];
            int key = servant.SlotKeyAt(index);
            //座圈：湖床上的影座
            KikasaVaultRenderer.DrawRing(sb, center + new Vector2(0f, 14f), 17f, 4.5f,
                KikasaHudTheme.Void(rain) * (0.55f * a));
            if (key == 0) {
                //空席暗一点的水痕
                KikasaVaultRenderer.DrawRing(sb, center, 21f, 1.6f,
                    KikasaHudTheme.TextDim(rain) * (0.22f * a));
                return;
            }
            bool servantOut = servant.FindServantOf(key) != null;
            float bob = MathF.Sin(time * 1.2f + index * 2.1f) * 2.2f;
            DrawEffigyByKey(sb, key, center + new Vector2(0f, bob), 42f, a,
                submerge: 1f, tamed: true, absent: servantOut, rain,
                0.4f + slotPulse[index] * 0.5f);
            //亲和烬点：席右下一粒身份色
            KikasaAffinity affinity = servant.SlotAffinity(index);
            if (affinity != KikasaAffinity.None) {
                float breath = KikasaSceneTheme.Breath(time, index * 3.1f, 2.2f);
                SvgPathPen.SoftDot(sb, center + new Vector2(16f, 15f), 5f,
                    KikasaEffigyBoard.AffinityColor(affinity), (0.5f + breath * 0.3f) * a);
            }
            //驻影在外：席上一圈慢旋涡（沿用画境「鬼奴在外」的语言）
            if (servantOut) {
                KikasaVaultRenderer.BeginAdditive(sb);
                for (int ring = 0; ring < 2; ring++) {
                    float rp = (time * 0.35f + ring * 0.5f) % 1f;
                    float r = MathHelper.Lerp(14f, 4f, rp);
                    KikasaVaultRenderer.DrawRing(sb, center, r, r * 0.4f,
                        KikasaHudTheme.Glow(rain) * (0.20f * (1f - rp) * a));
                }
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
        }

        private void DrawEntry(SpriteBatch sb, KikasaServantPlayer servant, KikasaVaultPlayer vault,
            int index, float a, float rain, float time) {
            int key = entryKeys[index];
            Vector2 pos = entryPos[index];
            float hover = index < entryHover.Count ? entryHover[index] : 0f;
            bool slotted = servant.SlotIndexOf(key) >= 0;
            bool held = pickedKey == key;
            //拾在手里的影原位留一圈拓空痕
            if (held) {
                KikasaVaultRenderer.DrawRing(sb, pos, 15f, 1.8f,
                    KikasaHudTheme.TextDim(rain) * (0.35f * a));
                return;
            }
            float fit = 34f + hover * 5f;
            //弧上是泥岸拓影（干形）；已驻席的沉进席里，弧上留个暗些的底档
            float entryAlpha = slotted ? 0.38f : 0.95f;
            //械奴断档（湖里没原件）暗着提醒
            if (key < 0 && KikasaServantPlayer.CountStoredArms(vault, -key) <= 0) {
                entryAlpha *= 0.5f;
            }
            DrawEffigyByKey(sb, key, pos + new Vector2(0f, -hover * 4f), fit,
                a * entryAlpha, submerge: 0.12f, tamed: true, absent: false, rain,
                0.25f + hover * 0.4f);
            //亲和小点缀在影脚
            KikasaAffinity affinity = KikasaServantPlayer.AffinityOfKey(key);
            if (affinity != KikasaAffinity.None) {
                SvgPathPen.SoftDot(sb, pos + new Vector2(0f, 15f), 3.4f,
                    KikasaEffigyBoard.AffinityColor(affinity),
                    (slotted ? 0.25f : 0.45f + hover * 0.3f) * a);
            }
            //已驻席的打一记席位小勾（暗色下沉线）
            if (slotted) {
                KikasaVaultRenderer.DrawLine(sb, pos + new Vector2(-5f, 18f),
                    pos + new Vector2(5f, 21f), 1.4f, KikasaHudTheme.Accent(rain) * (0.5f * a));
            }
        }

        /// <summary>记忆键通用沉影：正键走 NPC 贴图重载，负键取物品贴图走纹理重载</summary>
        private static void DrawEffigyByKey(SpriteBatch sb, int key, Vector2 center, float fit,
            float alpha, float submerge, bool tamed, bool absent, float rain, float stir) {
            if (key > 0) {
                KikasaVaultRenderer.DrawSunkEffigy(sb, key, center, fit, alpha,
                    submerge, 0.35f, tamed, absent, rain, stir, KikasaHudTheme.Accent(rain));
                return;
            }
            int itemType = -key;
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return;
            }
            KikasaVaultRenderer.DrawSunkEffigy(sb, tex, new Rectangle(0, 0, tex.Width, tex.Height),
                center, fit, alpha, submerge, 0.35f, tamed, absent, rain, stir,
                itemType * 0.173f, KikasaHudTheme.Accent(rain), SpriteEffects.None);
        }

        private void DrawFooter(SpriteBatch sb, Player player, KikasaDomainPlayer domain,
            DynamicSpriteFont font, float a, float rain, float time) {
            float y = boardRect.Bottom - 26f;
            //左：三系亲和计数（只报有的）
            float x = boardRect.X + 16f;
            void AffinityCount(KikasaAffinity affinity, string label) {
                int count = KikasaEffigyBoard.CountAffinity(player, affinity);
                if (count <= 0) {
                    return;
                }
                string text = $"{label}×{count}";
                Utils.DrawBorderString(sb, text, new Vector2(x, y),
                    KikasaEffigyBoard.AffinityColor(affinity) * (0.85f * a), 0.68f);
                x += font.MeasureString(text).X * 0.68f + 12f;
            }
            AffinityCount(KikasaAffinity.Flame, KikasaSceneUI.AffinityFlame.Value);
            AffinityCount(KikasaAffinity.Nightmare, KikasaSceneUI.AffinityNightmare.Value);
            AffinityCount(KikasaAffinity.Rain, KikasaSceneUI.AffinityRain.Value);

            //中：批注回执
            if (noteTimer > 0 && !string.IsNullOrEmpty(noteText)) {
                float noteA = MathHelper.Clamp(noteTimer / 24f, 0f, 1f) * a;
                Vector2 size = font.MeasureString(noteText) * 0.7f;
                Vector2 pos = new(boardRect.Center.X - size.X * 0.5f, y - 1f);
                Utils.DrawBorderString(sb, noteText, pos,
                    KikasaHudTheme.Text(rain) * noteA, 0.7f);
                //字下压一道朱线
                KikasaVaultRenderer.DrawLine(sb, pos + new Vector2(0f, size.Y + 1f),
                    pos + new Vector2(size.X, size.Y + 2f), 1f,
                    KikasaHudTheme.Accent(rain) * (0.5f * noteA));
            }

            //右：湖力细读（鬼火与鬼梦共饮的一汪水）——域开着才有湖可读
            if (domain.AnyActive) {
                const float barW = 64f;
                float bx = boardRect.Right - 16f - barW;
                string label = KikasaSceneUI.LakeVigorLabel.Value;
                Vector2 labelSize = font.MeasureString(label) * 0.62f;
                Utils.DrawBorderString(sb, label,
                    new Vector2(bx - labelSize.X - 6f, y + 1f),
                    KikasaHudTheme.TextDim(rain) * (0.8f * a), 0.62f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(bx, y + 8f),
                    new Vector2(bx + barW, y + 8f), 2f,
                    KikasaHudTheme.TextDim(rain) * (0.2f * a));
                float vigor = MathHelper.Clamp(domain.LakeVigor, 0f, 1f);
                if (vigor > 0.01f) {
                    KikasaVaultRenderer.DrawLine(sb, new Vector2(bx, y + 8f),
                        new Vector2(bx + barW * vigor, y + 8f), 2f,
                        KikasaWisps.KikasaWisp.GoldBody * (0.7f * a));
                }
            }
        }

        private void DrawHoverTip(SpriteBatch sb, KikasaServantPlayer servant,
            KikasaVaultPlayer vault, DynamicSpriteFont font, float a, float rain) {
            int key = 0;
            bool servantOut = false;
            if (hoverSlot >= 0) {
                key = servant.SlotKeyAt(hoverSlot);
                if (key == 0) {
                    return;
                }
                servantOut = servant.FindServantOf(key) != null;
            }
            else if (hoverEntry >= 0 && hoverEntry < entryKeys.Count) {
                key = entryKeys[hoverEntry];
            }
            if (key == 0) {
                return;
            }

            List<(string line, Color col, float scale)> lines = [];
            lines.Add((KikasaServantPlayer.KeyDisplayName(key), KikasaHudTheme.Text(rain), 0.78f));
            KikasaAffinity affinity = KikasaServantPlayer.AffinityOfKey(key);
            if (affinity != KikasaAffinity.None) {
                string affinityName = affinity switch {
                    KikasaAffinity.Flame => KikasaSceneUI.AffinityFlame.Value,
                    KikasaAffinity.Nightmare => KikasaSceneUI.AffinityNightmare.Value,
                    KikasaAffinity.Rain => KikasaSceneUI.AffinityRain.Value,
                    _ => KikasaSceneUI.AffinityWild.Value,
                };
                lines.Add((string.Format(KikasaSceneUI.BoardAffinityFormat.Value, affinityName),
                    KikasaEffigyBoard.AffinityColor(affinity), 0.66f));
            }
            if (key < 0) {
                int stock = KikasaServantPlayer.CountStoredArms(vault, -key);
                lines.Add((stock > 0
                    ? string.Format(KikasaSceneUI.BoardArmsStockFormat.Value, stock)
                    : KikasaSceneUI.BoardArmsNoStock.Value,
                    stock > 0 ? KikasaHudTheme.TextDim(rain) : KikasaHudTheme.Accent(rain), 0.66f));
            }
            if (servantOut) {
                lines.Add((KikasaSceneUI.ServantOutTag.Value, KikasaHudTheme.TextDim(rain), 0.66f));
            }
            if (hoverSlot >= 0) {
                lines.Add((KikasaSceneUI.BoardSlotRemoveHint.Value,
                    KikasaHudTheme.TextDim(rain), 0.66f));
            }
            int slotted = servant.SlotIndexOf(key);
            if (slotted >= 0 && hoverSlot < 0) {
                lines.Add((KikasaSceneUI.BoardAlreadySlotted.Value,
                    KikasaHudTheme.TextDim(rain), 0.66f));
            }

            Vector2 mouse = KikasaHudTheme.UIMouse;
            float y = mouse.Y + 20f;
            foreach ((string line, Color col, float scale) in lines) {
                Vector2 size = font.MeasureString(line) * scale;
                float x = MathHelper.Clamp(mouse.X + 16f, boardRect.X + 6f,
                    MathF.Max(boardRect.X + 6f, boardRect.Right - 6f - size.X));
                Utils.DrawBorderString(sb, line, new Vector2(x, y), col * a, scale);
                y += size.Y + 2f;
            }
        }
    }
}
