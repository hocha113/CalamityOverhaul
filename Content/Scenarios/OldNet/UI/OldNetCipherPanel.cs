using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.HackTimes.PvP;
using CalamityOverhaul.Content.HackTimes.PvP.Protocols;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>
    /// 主控破译面板（ModSystem 相位机，形态镜像 OldNetDebriefPanel）：
    /// 环形时序锁小游戏 + 每关升级的弃留梯子。游戏不暂停，ICE 照常调度，
    /// 开台期每秒 +2 噪音（走 AddNoise，时停 ×0.25 自然生效）。<br/>
    /// 判定规则：指针转动中受击或横向离台超 60px = 脱靶爆仓；
    /// 弃留菜单期离台 = 视同收手结算。爆仓 S3 起吃反制骇入。<br/>
    /// 全部状态本机语义，入口在 <see cref="OldNetCipherVaultTile"/> 处单人硬门禁。
    /// TODO MP: MP 化时锁盘判定与结算移服务器、结果走包，面板只做表现
    /// </summary>
    internal class OldNetCipherPanel : ModSystem
    {
        //════════ 相位机 ════════

        private enum Phase { Hidden, FadeIn, Spin, StageDone, FadeOut }

        private const int PanelW = 430;
        private const int PanelH = 440;
        private const int StageCount = 5;
        private const float RingRadius = 92f;

        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color EmberRed = new(235, 64, 44);
        private static readonly Color Amber = new(255, 180, 80);
        private static readonly Color Mint = new(110, 240, 170);
        private static readonly Color TextDim = new(150, 160, 175);
        private static readonly Color PanelBg = new(8, 12, 16);

        private static Phase phase = Phase.Hidden;
        private static float alpha;
        private static float idleTimer;
        /// <summary>台体 tile 坐标</summary>
        private static Point console = new(-1, -1);
        /// <summary>当前关 0..4（显示为 S1..S5）</summary>
        private static int stage;
        /// <summary>指针角（度）与本关旋向</summary>
        private static float cursorDeg;
        private static int spinDir = 1;
        /// <summary>本关密钥闸弧：中心角与半宽（度）</summary>
        private static float arcCenterDeg;
        private static float arcHalfDeg;
        //彩池：碎片 / 模块件数 / RAM 芯片
        private static int potShards;
        private static int potModules;
        private static bool potChip;
        //受击检测（不动 OldNetPlayer 的 OnHurt，面板自监血量）
        private static int prevLife;
        //输入沿
        private static bool prevMouseLeft;
        private static bool prevJump;
        private static bool clickEdge;
        private static bool jumpEdge;
        //爆仓撕裂线（FadeOut 期）
        private static bool bustFlash;

        private static Rectangle panelRect = Rectangle.Empty;
        private static Rectangle cashOutRect = Rectangle.Empty;
        private static Rectangle continueRect = Rectangle.Empty;

#if DEBUG
        //调参口（仅 DEBUG）：面板开启期 上/下箭头调指针速度，左/右箭头调闸弧宽度
        private static float debugSpeedMul = 1f;
        private static float debugArcMul = 1f;
        private static bool prevKeyUp, prevKeyDown, prevKeyLeft, prevKeyRight;
#endif

        public static bool Visible => phase != Phase.Hidden;

        //════════ 开台 / 收台 ════════

        /// <summary>开台（台体右键调用，单人门禁已在入口验过）。RAM 已扣</summary>
        internal static void Open(int i, int j) {
            console = new Point(i, j);
            stage = 0;
            potShards = 0;
            potModules = 0;
            potChip = false;
            RollStage();
            phase = Phase.FadeIn;
            alpha = 0f;
            idleTimer = 0f;
            bustFlash = false;
            prevLife = Main.LocalPlayer.statLife;
            //吞掉开台那一次点击/按键，防止 FadeIn 后立刻误判
            prevMouseLeft = true;
            prevJump = true;
        }

        internal static void Hide() {
            phase = Phase.Hidden;
            alpha = 0f;
            console = new Point(-1, -1);
            panelRect = Rectangle.Empty;
            cashOutRect = Rectangle.Empty;
            continueRect = Rectangle.Empty;
        }

        public override void OnWorldUnload() => Hide();

        //重掷本关锁盘：闸弧随机落位、逐关反向、指针从闸弧对侧起转（±180° 起步的读弧时间）
        private static void RollStage() {
            arcHalfDeg = OldNetMetrics.VaultArcDeg[stage] * 0.5f;
#if DEBUG
            arcHalfDeg *= debugArcMul;
#endif
            arcCenterDeg = Main.rand.NextFloat(0f, 360f);
            spinDir = stage % 2 == 0 ? 1 : -1;
            cursorDeg = Wrap360(arcCenterDeg + 180f);
        }

        private static float Wrap360(float deg) {
            deg %= 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        //带符号最短角差（度）
        private static float AngleDelta(float a, float b) {
            return ((a - b + 540f) % 360f) - 180f;
        }

        //UI 空间口径：布局与命中共用（DebriefPanel 同款）
        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        private static Point UIMouse => new((int)(PlayerInput.MouseX / Main.UIScale),
            (int)(PlayerInput.MouseY / Main.UIScale));

        //════════ 更新 ════════

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu || phase == Phase.Hidden) {
                return;
            }
            //暂停帧整体冻结（tML 的 UpdateUI 在 gamePaused 判定前照跑）：
            //autopause 开物品栏期间锁盘不转、判定不收、噪音不涨，堵"暂停白嫖判定窗"
            if (Main.gamePaused) {
                return;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            idleTimer += dt;

            //死亡或链路烧断弹出演出中：面板直接合上，不结算不反制。
            //彩池随链路一起烧（与死亡路径同语义），堵烧断 15 tick 窗口内的结算/拾取绕过
            if (player.dead || OldNetEjectFlash.Active) {
                Hide();
                return;
            }
            //台体没了（正常只有本面板自己消它）：兜底合上
            if (phase != Phase.FadeOut) {
                Tile tile = Framing.GetTileSafely(console.X, console.Y);
                if (!tile.HasTile || tile.TileType != ModContent.TileType<OldNetCipherVaultTile>()) {
                    Hide();
                    return;
                }
            }

            //输入沿（本帧统一取样）
            clickEdge = Main.mouseLeft && !prevMouseLeft;
            jumpEdge = player.controlJump && !prevJump;
            prevMouseLeft = Main.mouseLeft;
            prevJump = player.controlJump;

#if DEBUG
            TickDebugTune();
#endif

            //开台期噪音：上行链路激活，走 AddNoise 吃时停系数
            if (phase is Phase.FadeIn or Phase.Spin or Phase.StageDone) {
                session.AddNoise(OldNetMetrics.VaultNoisePerSecond / 60f);
            }

            //离台检查（只量横向距离：跳跃确认不会把人跳出圈）
            if (phase is Phase.Spin or Phase.StageDone) {
                float dx = MathF.Abs(player.Center.X - (console.X * 16 + 8));
                if (dx > OldNetMetrics.EncryptChannelRadius) {
                    if (phase == Phase.Spin) {
                        Bust(player, session);
                    }
                    else {
                        CashOut(player, session);
                    }
                }
            }

            //受击打断：指针转动中掉血 = 脱靶（弃留菜单期免疫，加密引导 OnHurt 先例的面板版）
            if (phase == Phase.Spin && player.statLife < prevLife) {
                Bust(player, session);
            }
            prevLife = player.statLife;

            switch (phase) {
                case Phase.FadeIn:
                    alpha = MathHelper.Lerp(alpha, 1f, 0.16f);
                    if (alpha > 0.985f) {
                        alpha = 1f;
                        phase = Phase.Spin;
                    }
                    break;
                case Phase.Spin:
                    TickSpin(player, session, dt);
                    break;
                case Phase.StageDone:
                    TickStageDone(player, session);
                    break;
                case Phase.FadeOut:
                    alpha = MathHelper.Lerp(alpha, 0f, 0.14f);
                    if (alpha < 0.02f) {
                        Hide();
                    }
                    break;
            }

            if (phase != Phase.Hidden && panelRect != Rectangle.Empty) {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

#if DEBUG
        //DEBUG 调参：上/下 = 指针速度 ×0.1 步进，左/右 = 闸弧宽度 ×0.1 步进（即时生效）
        private static void TickDebugTune() {
            KeyboardState ks = Main.keyState;
            bool up = ks.IsKeyDown(Keys.Up);
            bool down = ks.IsKeyDown(Keys.Down);
            bool left = ks.IsKeyDown(Keys.Left);
            bool right = ks.IsKeyDown(Keys.Right);
            if (up && !prevKeyUp) {
                debugSpeedMul = MathF.Min(3f, debugSpeedMul + 0.1f);
            }
            if (down && !prevKeyDown) {
                debugSpeedMul = MathF.Max(0.2f, debugSpeedMul - 0.1f);
            }
            if (right && !prevKeyRight) {
                debugArcMul = MathF.Min(3f, debugArcMul + 0.1f);
            }
            if (left && !prevKeyLeft) {
                debugArcMul = MathF.Max(0.3f, debugArcMul - 0.1f);
            }
            prevKeyUp = up;
            prevKeyDown = down;
            prevKeyLeft = left;
            prevKeyRight = right;
        }
#endif

        //════════ 小游戏判定 ════════

        private static void TickSpin(Player player, OldNetPlayer session, float dt) {
            float speed = OldNetMetrics.VaultCursorDegPerSec;
#if DEBUG
            speed *= debugSpeedMul;
#endif
            cursorDeg = Wrap360(cursorDeg + spinDir * speed * dt);

            if (!clickEdge && !jumpEdge) {
                return;
            }
            //指针进弧 = 过关，脱靶 = 爆仓
            if (MathF.Abs(AngleDelta(cursorDeg, arcCenterDeg)) <= arcHalfDeg) {
                StageHit(player, session);
            }
            else {
                Bust(player, session);
            }
        }

        private static void StageHit(Player player, OldNetPlayer session) {
            potShards += OldNetMetrics.VaultPotShards[stage];
            if (stage == 3) {
                potModules++;
            }
            if (stage == 4) {
                potModules++;
                potChip = true;
            }
            //每关机械音：过关的代价挂在声音上
            session.AddNoise(OldNetMetrics.VaultStageNoise);
            SoundEngine.PlaySound(SoundID.Unlock with {
                Volume = 0.7f,
                Pitch = -0.3f + stage * 0.15f
            }, player.Center);

            if (stage >= StageCount - 1) {
                //S5 通关：没有更高一级，直接结算
                CashOut(player, session);
                return;
            }
            phase = Phase.StageDone;
        }

        private static void TickStageDone(Player player, OldNetPlayer session) {
            if (!clickEdge) {
                return;
            }
            Point mouse = UIMouse;
            if (cashOutRect.Contains(mouse)) {
                Main.mouseLeft = false;
                CashOut(player, session);
            }
            else if (continueRect.Contains(mouse)) {
                Main.mouseLeft = false;
                stage++;
                RollStage();
                phase = Phase.Spin;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = 0.2f });
            }
        }

        //════════ 结算 / 爆仓 ════════

        private static void CashOut(Player player, OldNetPlayer session) {
            Vector2 worldPos = new(console.X * 16 + 8, console.Y * 16 + 8);
            var dropRect = new Rectangle(console.X * 16, console.Y * 16 - 8, 16, 16);
            var source = new EntitySource_TileInteraction(player, console.X, console.Y);

            //碎片经 TryAddHarvest 入账（未铭刻）：先量余位，装不下的弃置
            if (potShards > 0) {
                int room = Math.Max(0, session.LedgerCapacity - session.PendingTotal);
                int banked = Math.Min(potShards, room);
                int discard = potShards - banked;
                if (banked > 0) {
                    //按类别随机分桶，逐桶入账（余位已预检，必收满）
                    int[] bucket = new int[SHPCData.SlotCount];
                    for (int k = 0; k < banked; k++) {
                        bucket[Main.rand.Next(SHPCData.SlotCount)]++;
                    }
                    for (int c = 0; c < bucket.Length; c++) {
                        if (bucket[c] > 0) {
                            session.TryAddHarvest(c, bucket[c]);
                        }
                    }
                    CombatText.NewText(player.getRect(), Mint,
                        OldNetTexts.OldNetVaultPayout.Format(banked), dramatic: true);
                    OldNetAbsorbFX.Emit(worldPos, Amber, banked);
                }
                if (discard > 0) {
                    CombatText.NewText(new Rectangle((int)worldPos.X - 8, (int)worldPos.Y - 8, 16, 16),
                        new Color(255, 120, 60), OldNetTexts.VaultDiscard.Format(discard));
                }
            }
            //模块实体与 RAM 芯片绕过账本直接掉落（缓存同款）
            for (int m = 0; m < potModules; m++) {
                int itemType = RollModuleItem();
                if (itemType > 0) {
                    Item.NewItem(source, dropRect, itemType);
                }
            }
            if (potChip) {
                Item.NewItem(source, dropRect, ModContent.ItemType<RamCapacityUpgradeChip>());
            }

            SoundEngine.PlaySound(SoundID.ResearchComplete with { Volume = 0.8f }, player.Center);
            ConsumeConsole();
            bustFlash = false;
            phase = Phase.FadeOut;
        }

        private static void Bust(Player player, OldNetPlayer session) {
            potShards = 0;
            potModules = 0;
            potChip = false;
            session.AddNoise(OldNetMetrics.VaultBustNoise);

            //S3 起爆仓追加反制骇入（伪读数），S4/S5 换冷却注入。stage 0 基：S3=2
            PlayerHackDef def = stage >= 3 ? QuickHackDef.Get<CooldownInject>()
                : stage >= 2 ? QuickHackDef.Get<GaugePollution>() : null;
            if (def != null && OldNetHostileHack.TryCast(player, def, OldNetTexts.VaultTitle.Value)) {
                CombatText.NewText(player.getRect(), EmberRed, OldNetTexts.VaultCounterHack.Value);
            }

            CombatText.NewText(player.getRect(), EmberRed, OldNetTexts.VaultBust.Value, dramatic: true);
            SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.8f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = -0.6f }, player.Center);
            ConsumeConsole();
            bustFlash = true;
            phase = Phase.FadeOut;
        }

        //控制台上锁消散（一次性：收手与爆仓同归）
        private static void ConsumeConsole() {
            if (console.X < 0) {
                return;
            }
            WorldGen.KillTile(console.X, console.Y, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, console.X, console.Y, 1);
            }
        }

        //模块池：深潜保留池（CanGenerateInLabChest=false）优先，空则全池兜底（缓存同款）
        private static int RollModuleItem() {
            int[] pool = [.. ModContent.GetContent<SHPCModuleItem>()
                .Where(m => !m.CanGenerateInLabChest)
                .Select(m => m.Type)
                .OrderBy(t => t)];
            if (pool.Length == 0) {
                pool = [.. ModContent.GetContent<SHPCModuleItem>()
                    .Select(m => m.Type)
                    .OrderBy(t => t)];
            }
            if (pool.Length == 0) {
                CWRMod.Instance.Logger.Warn("[OldNet] 破译矩阵结算：SHPC 模块池为空");
                return 0;
            }
            return pool[Main.rand.Next(pool.Length)];
        }

        //════════ 绘制 ════════

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (phase == Phase.Hidden || alpha < 0.01f) {
                return;
            }
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: OldNet Cipher Panel",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }

            //面板放中偏右：游戏不暂停，角色与来袭的 ICE 要留在视野里
            float slideY = (1f - alpha) * 22f;
            int x0 = (int)MathF.Min(UIScreenW * 0.5f + 110f, UIScreenW - PanelW - 8f);
            int y0 = (int)((UIScreenH - PanelH) * 0.5f + slideY);
            panelRect = new Rectangle(x0, y0, PanelW, PanelH);

            DrawPanelBase(sb, px);

            //慢速横向扫描线：面板活着
            float scanPhase = idleTimer * 0.22f % 1f;
            int scanY = y0 + (int)(scanPhase * (PanelH - 2)) + 1;
            sb.Draw(px, new Rectangle(x0 + 1, scanY, PanelW - 2, 1), ColdCyan * (0.10f * alpha));

            DrawContent(sb, px);

            //爆仓撕裂线：FadeOut 期三条黑墙红横裂（EjectFlash 缩微版）
            if (bustFlash && phase == Phase.FadeOut) {
                for (int k = 0; k < 3; k++) {
                    float hash = MathF.Sin(k * 12.9898f + 78.233f) * 43758.5453f;
                    int ty = y0 + 30 + (int)((hash - MathF.Floor(hash)) * (PanelH - 60));
                    sb.Draw(px, new Rectangle(x0, ty + k * 3, PanelW, 2), EmberRed * (0.7f * alpha));
                }
            }
        }

        //底板：OldNetHud.fx TechPanel 暗钢切角（域内同一张皮），缺编 CPU 实底 + 1px 边线
        private static void DrawPanelBase(SpriteBatch sb, Texture2D px) {
            Effect fx = EffectLoader.OldNetHud?.Value;
            if (fx != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                //共享参数化 shader：每次调用全参数重设
                fx.CurrentTechnique = fx.Techniques["TechPanel"];
                fx.Parameters["uTime"]?.SetValue(idleTimer);
                fx.Parameters["uPanelSize"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
                fx.Parameters["uFrac"]?.SetValue(0f);
                fx.Parameters["uTier"]?.SetValue(stage);
                fx.Parameters["uAlpha"]?.SetValue(alpha);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, panelRect, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }
            else {
                sb.Draw(px, panelRect, PanelBg * (0.92f * alpha));
            }
            DrawBorder(sb, px, panelRect, ColdCyan * (0.5f * alpha));
            sb.Draw(px, new Rectangle(panelRect.X + 1, panelRect.Y + 1, PanelW - 2, 1),
                ColdCyan * (0.75f * alpha));
        }

        private static void DrawContent(SpriteBatch sb, Texture2D px) {
            DynamicSpriteFont title = FontAssets.DeathText.Value;
            DynamicSpriteFont body = FontAssets.MouseText.Value;
            int x0 = panelRect.X;
            int y0 = panelRect.Y;
            OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);

            //标题：诊断字样走机器英文（域语汇），副题走本地化
            string titleText = "CIPHER MATRIX";
            Vector2 titleSz = title.MeasureString(titleText) * 0.5f;
            Vector2 titlePos = new(x0 + (PanelW - titleSz.X) * 0.5f, y0 + 14f);
            sb.DrawString(title, titleText, titlePos + new Vector2(2f, 2f),
                Color.Black * (0.6f * alpha), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            sb.DrawString(title, titleText, titlePos, Amber * alpha,
                0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            string sub = OldNetTexts.VaultTitle.Value;
            Vector2 subSz = body.MeasureString(sub) * 0.75f;
            Utils.DrawBorderString(sb, sub,
                new Vector2(x0 + (PanelW - subSz.X) * 0.5f, titlePos.Y + titleSz.Y + 4f),
                TextDim * alpha, 0.75f);

            //关数行 + 五枚关卡刻点
            string stageText = OldNetTexts.VaultStage.Format(stage + 1);
            Vector2 stageSz = body.MeasureString(stageText) * 0.8f;
            float stageY = titlePos.Y + titleSz.Y + 30f;
            Utils.DrawBorderString(sb, stageText,
                new Vector2(x0 + (PanelW - stageSz.X) * 0.5f, stageY), ColdCyan * alpha, 0.8f);
            for (int s = 0; s < StageCount; s++) {
                var pip = new Rectangle(x0 + PanelW / 2 - StageCount * 9 + s * 18, (int)(stageY + 26f), 10, 4);
                Color c = s < stage ? Mint : s == stage ? Amber : TextDim * 0.4f;
                sb.Draw(px, pip, c * alpha);
            }

            //环形锁盘
            Vector2 ringCenter = new(x0 + PanelW * 0.5f, y0 + 218f);
            DrawRing(sb, px, ringCenter);

            //彩池与账本余位
            float infoY = y0 + PanelH - 118f;
            string pot = OldNetTexts.VaultPot.Format(potShards);
            if (potModules > 0) {
                pot += "  " + OldNetTexts.VaultPotModule.Format(potModules);
            }
            if (potChip) {
                pot += "  " + OldNetTexts.VaultPotChip.Value;
            }
            Vector2 potSz = body.MeasureString(pot) * 0.78f;
            Utils.DrawBorderString(sb, pot, new Vector2(x0 + (PanelW - potSz.X) * 0.5f, infoY),
                Amber * alpha, 0.78f);

            int room = Math.Max(0, session.LedgerCapacity - session.PendingTotal);
            string roomText = OldNetTexts.VaultLedgerRoom.Format(room);
            Vector2 roomSz = body.MeasureString(roomText) * 0.66f;
            //余位装不下彩池 = 提前亮红（弃置的前置警告）
            Color roomCol = room < potShards ? EmberRed : TextDim;
            Utils.DrawBorderString(sb, roomText,
                new Vector2(x0 + (PanelW - roomSz.X) * 0.5f, infoY + 22f), roomCol * alpha, 0.66f);

            if (phase == Phase.StageDone) {
                DrawChoiceButtons(sb, px, body);
            }
            else {
                cashOutRect = Rectangle.Empty;
                continueRect = Rectangle.Empty;
                //操作引导（键位写明：左键或跳跃键）
                string guide = OldNetTexts.VaultGuide.Value;
                Vector2 guideSz = body.MeasureString(guide) * 0.56f;
                Utils.DrawBorderString(sb, guide,
                    new Vector2(x0 + (PanelW - guideSz.X) * 0.5f, panelRect.Bottom - 34f),
                    TextDim * (0.7f * alpha), 0.56f);
            }

#if DEBUG
            string dbg = $"DBG speed x{debugSpeedMul:0.0} (Up/Dn)  arc x{debugArcMul:0.0} (L/R)";
            Utils.DrawBorderString(sb, dbg, new Vector2(x0 + 10f, y0 + 6f),
                new Color(120, 130, 140) * (0.8f * alpha), 0.55f);
#endif
        }

        //12 段基环 + 琥珀密钥闸弧 + 扫描指针（全 CPU：placeholder2 旋转细条拼绘）
        private static void DrawRing(SpriteBatch sb, Texture2D px, Vector2 center) {
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            bool spinning = phase == Phase.Spin;
            float ringAlpha = (spinning ? 1f : 0.45f) * alpha;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);
            //角度约定：0° 指向正上，顺时针增长
            Vector2 Dir(float deg) {
                float rad = MathHelper.ToRadians(deg - 90f);
                return new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            }

            //基环：12 段短弧（冷青，段间留缝）
            const int segs = 12;
            for (int s = 0; s < segs; s++) {
                float deg = s * (360f / segs);
                Vector2 p = center + Dir(deg) * RingRadius;
                float rad = MathHelper.ToRadians(deg);
                sb.Draw(px, p, null, ColdCyan * (0.35f * ringAlpha), rad,
                    origin, Size(30f, 2f), SpriteEffects.None, 0f);
            }
            //刻度内环
            for (int s = 0; s < segs; s++) {
                float deg = s * (360f / segs) + 15f;
                Vector2 p = center + Dir(deg) * (RingRadius - 14f);
                float rad = MathHelper.ToRadians(deg);
                sb.Draw(px, p, null, TextDim * (0.30f * ringAlpha), rad,
                    origin, Size(6f, 1.2f), SpriteEffects.None, 0f);
            }

            //密钥闸弧：琥珀亮带，3° 一枚 tick 铺满弧宽（进度弧同法）
            float step = 3f;
            for (float d = -arcHalfDeg; d <= arcHalfDeg; d += step) {
                float deg = arcCenterDeg + d;
                Vector2 p = center + Dir(deg) * RingRadius;
                float rad = MathHelper.ToRadians(deg);
                //弧缘渐隐：中心最亮
                float edgeFade = 1f - MathF.Abs(d) / MathF.Max(arcHalfDeg, 0.01f) * 0.55f;
                sb.Draw(px, p, null, Amber * (0.85f * edgeFade * ringAlpha), rad,
                    origin, Size(RingRadius * MathF.PI * 2f / 360f * step + 1.5f, 5f),
                    SpriteEffects.None, 0f);
            }

            //指针：从心到环外的细杆 + 尖端辉光（SoftGlow 黑底加色，A=0 染色纪律）
            Vector2 dir = Dir(cursorDeg);
            Vector2 tip = center + dir * (RingRadius + 9f);
            Vector2 mid = center + dir * (RingRadius * 0.55f + 4.5f);
            float needleRad = MathHelper.ToRadians(cursorDeg - 90f);
            sb.Draw(px, mid, null, Color.White * (0.9f * ringAlpha), needleRad,
                origin, Size(RingRadius * 0.9f + 9f, 1.6f), SpriteEffects.None, 0f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && !glow.IsDisposed) {
                Color glowCol = spinning ? Amber : ColdCyan;
                glowCol.A = 0;
                sb.Draw(glow, tip, null, glowCol * (0.9f * ringAlpha), 0f,
                    new Vector2(glow.Width * 0.5f, glow.Height * 0.5f),
                    26f / glow.Width, SpriteEffects.None, 0f);
            }

            //盘芯：反向慢转的斜方核
            float t = idleTimer;
            sb.Draw(px, center, null, ColdCyan * (0.6f * ringAlpha),
                -t * 0.8f, origin, Size(9f, 9f), SpriteEffects.None, 0f);
            sb.Draw(px, center, null, Color.White * (0.75f * ringAlpha),
                t * 1.2f, origin, Size(4f, 4f), SpriteEffects.None, 0f);
        }

        //弃留梯子：收手结算（薄荷）/ 下一关（琥珀）双键
        private static void DrawChoiceButtons(SpriteBatch sb, Texture2D px, DynamicSpriteFont body) {
            const int btnW = 170;
            const int btnH = 34;
            int gap = 22;
            int bx = panelRect.X + (PanelW - btnW * 2 - gap) / 2;
            int by = panelRect.Bottom - 52;
            cashOutRect = new Rectangle(bx, by, btnW, btnH);
            continueRect = new Rectangle(bx + btnW + gap, by, btnW, btnH);
            Point mouse = UIMouse;

            DrawButton(sb, px, body, cashOutRect, OldNetTexts.VaultCashOut.Value,
                Mint, cashOutRect.Contains(mouse));
            DrawButton(sb, px, body, continueRect, OldNetTexts.VaultContinue.Value,
                Amber, continueRect.Contains(mouse));
        }

        private static void DrawButton(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle rect, string label, Color accent, bool hover) {
            sb.Draw(px, rect, PanelBg * (0.9f * alpha));
            DrawBorder(sb, px, rect, (hover ? accent : accent * 0.55f) * alpha);
            Vector2 sz = font.MeasureString(label) * 0.8f;
            Utils.DrawBorderString(sb, label,
                new Vector2(rect.X + (rect.Width - sz.X) * 0.5f, rect.Y + (rect.Height - sz.Y) * 0.5f + 2f),
                (hover ? Color.White : accent) * alpha, 0.8f);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle rect, Color color) {
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }
    }
}
