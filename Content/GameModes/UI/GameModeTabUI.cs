using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 背包右缘的游戏模式标签行：残酷与神匠常驻，修罗模式在残酷开启后从两者之间滑出，
    /// 神匠随之让位到第三席（神匠是独立开关，任何时候都能点）。
    /// 标签动画由旗标真值差分点火（本地点击与联机回执走同一条表现路径）；
    /// 交互回馈四件套：悬停抬升+旗面微放大、按压下沉+uPress 即时回响（联机等回执期不哑）、
    /// 拒绝横震+uDeny 红脉冲+闷响、悬浮说明面板（<see cref="GameModeTipOverlay"/> 顶层绘制）。
    /// 切换演出大字也由本 UI 绘制，背包合上时靠 <see cref="GameModeCeremony.LineActive"/> 维持活跃。
    /// 首见引导：光标从未造访过标签时，残酷标签挂信标光效相邀，
    /// 悬停一次即收束熄灭并落档（客户端全局，跨世界只引导一次）
    /// </summary>
    internal class GameModeTabUI : UIHandle
    {
        public static GameModeTabUI Instance => UIHandleLoader.GetUIHandleOfType<GameModeTabUI>();

        /// <summary>切换爆发动画时长（帧）</summary>
        private const float BurstStep = 1f / 42f;
        /// <summary>拒绝横震时长（帧）</summary>
        private const int ShakeDuration = 14;
        /// <summary>活跃间隔超过该帧数视为新会话，旗标差分静默重播（进档同步不该放爆发）</summary>
        private const uint SessionGapTicks = 30;
        /// <summary>入场滑入推进步长（帧）</summary>
        private const float EntranceStep = 1f / 16f;
        /// <summary>入场滑入距离（px）</summary>
        private const float EntranceSlide = 30f;
        /// <summary>首见信标脉冲周期（帧）</summary>
        private const int GuidePulsePeriod = 120;
        /// <summary>首见信标扩散环时长（帧），须小于脉冲周期</summary>
        private const int GuideRingFrames = 52;

        private float brutalLit;
        private float asuraLit;
        private float godSmithLit;
        private float asuraReveal;
        private float brutalHover;
        private float asuraHover;
        private float godSmithHover;
        private float disabledDim;
        private float brutalBurst = 1f;
        private float asuraBurst = 1f;
        private float godSmithBurst = 1f;
        private bool brutalBurstOn;
        private bool asuraBurstOn;
        private bool godSmithBurstOn;
        private int shakeTimer;
        private int shakeTab;
        private bool prevBrutal;
        private bool prevAsura;
        private bool prevGodSmith;
        private int hoverKind = -1;
        //最后悬停过的标签：悬停缓动衰减期间悬浮说明照着它淡出
        private int tipKind = -1;
        private uint lastUpdateTick;

        //入场滑入进度
        private float entranceBrutal = 1f;
        private float entranceAsura = 1f;
        private float entranceGodSmith = 1f;
        private bool prevTabsVisible;
        //受理点击的按压下沉
        private float pressDip;
        private int pressTab;
        //首见引导
        private bool discovered;
        private int guideTimer;
        private float guideAck;

        //本帧动画后的标签矩形：命中与绘制共用一份
        private Rectangle brutalRect;
        private Rectangle asuraRect;
        private Rectangle godSmithRect;

        private static bool TabsVisible => Main.playerInventory && !Main.gameMenu;

        public override bool Active =>
            TabsVisible
            || GameModeCeremony.LineActive
            || brutalBurst < 1f || asuraBurst < 1f || godSmithBurst < 1f
            || asuraReveal is > 0.01f and < 0.99f;

        public override void SaveUIData(TagCompound tag) => tag[Name + ":discovered"] = discovered;

        public override void LoadUIData(TagCompound tag)
            => discovered = tag.TryGet(Name + ":discovered", out bool value) && value;

        public override void Update() {
            GameModeCeremony.UpdateLine();

            uint tick = Main.GameUpdateCount;
            bool freshSession = tick - lastUpdateTick > SessionGapTicks;
            lastUpdateTick = tick;
            if (freshSession) {
                //长间隔后的第一帧：静默对齐旗标（进档/换世界不该播切换爆发）
                prevBrutal = GameModeSystem.BrutalActive;
                prevAsura = GameModeSystem.AsuraActive;
                prevGodSmith = GameModeSystem.GodSmithActive;
                shakeTimer = 0;
                pressDip = 0f;
                GameModeRenderer.ClearMotes();
            }

            //入场滑入：背包打开沿重置（Active 断档期 prevTabsVisible 会失真，freshSession 兜底）
            if (TabsVisible && (!prevTabsVisible || freshSession)) {
                entranceBrutal = 0f;
                entranceAsura = 0f;
                entranceGodSmith = 0f;
            }
            prevTabsVisible = TabsVisible;
            if (TabsVisible) {
                entranceBrutal = Math.Min(1f, entranceBrutal + EntranceStep);
                if (entranceBrutal > 0.30f) {
                    //修罗错帧随后落座
                    entranceAsura = Math.Min(1f, entranceAsura + EntranceStep);
                }
                if (entranceAsura > 0.30f) {
                    //神匠再错一帧组
                    entranceGodSmith = Math.Min(1f, entranceGodSmith + EntranceStep);
                }
            }

            //真值差分点火切换爆发（含扩张环与余烬喷洒）；
            //喷洒锚点用 ComputeRects 产出的动画矩形（上一帧值，至多 1 帧陈旧），入场滑入期不再错位
            if (GameModeSystem.BrutalActive != prevBrutal) {
                prevBrutal = GameModeSystem.BrutalActive;
                brutalBurst = 0f;
                brutalBurstOn = prevBrutal;
                if (TabsVisible) {
                    GameModeRenderer.EmitBurst(brutalRect, GameModeFace.Brutal, brutalBurstOn);
                }
            }
            if (GameModeSystem.AsuraActive != prevAsura) {
                prevAsura = GameModeSystem.AsuraActive;
                asuraBurst = 0f;
                asuraBurstOn = prevAsura;
                if (TabsVisible) {
                    GameModeRenderer.EmitBurst(asuraRect,
                        GameModeSystem.FaceOf(GameModeKind.Asura), asuraBurstOn);
                }
            }
            if (GameModeSystem.GodSmithActive != prevGodSmith) {
                prevGodSmith = GameModeSystem.GodSmithActive;
                godSmithBurst = 0f;
                godSmithBurstOn = prevGodSmith;
                if (TabsVisible) {
                    GameModeRenderer.EmitBurst(godSmithRect, GameModeFace.GodSmith, godSmithBurstOn);
                }
            }

            brutalBurst = Math.Min(1f, brutalBurst + BurstStep);
            asuraBurst = Math.Min(1f, asuraBurst + BurstStep);
            godSmithBurst = Math.Min(1f, godSmithBurst + BurstStep);

            brutalLit = Ease(brutalLit, GameModeSystem.BrutalActive ? 1f : 0f, 0.12f);
            asuraLit = Ease(asuraLit, GameModeSystem.AsuraActive ? 1f : 0f, 0.12f);
            godSmithLit = Ease(godSmithLit, GameModeSystem.GodSmithActive ? 1f : 0f, 0.12f);
            asuraReveal = Ease(asuraReveal, GameModeSystem.BrutalActive ? 1f : 0f, 0.13f);
            disabledDim = Ease(disabledDim, GameModeSystem.CanToggleNow() ? 0f : 1f, 0.15f);

            if (shakeTimer > 0) {
                shakeTimer--;
            }
            if (pressDip > 0f) {
                pressDip = Math.Max(0f, pressDip - 0.11f);
            }

            ComputeRects();
            UpdateInteraction();
            UpdateGuide();
            UpdateMotes();
        }

        /// <summary>
        /// 把入场滑入/悬停抬升/按压下沉/拒绝横震一次性折进标签矩形，命中与绘制不再各算一遍。
        /// 悬停抬升读的是上一帧的缓动值（本方法先于 UpdateInteraction 跑），至多陈旧 1 帧
        /// </summary>
        private void ComputeRects() {
            Rectangle brutal = GameModeTheme.BrutalTab;
            brutal.X += SlideOffset(entranceBrutal);
            brutal.Y -= (int)MathF.Round(brutalHover * 2f);
            if (pressDip > 0f && pressTab == 0) {
                brutal.Y += (int)MathF.Round(pressDip * 2.5f);
            }
            if (shakeTimer > 0 && shakeTab == 0) {
                brutal.X += ShakeOffset();
            }
            brutalRect = brutal;

            Rectangle asura = GameModeTheme.AsuraTab(EasedReveal());
            asura.X += SlideOffset(entranceAsura);
            asura.Y -= (int)MathF.Round(asuraHover * 2f);
            if (pressDip > 0f && pressTab == 1) {
                asura.Y += (int)MathF.Round(pressDip * 2.5f);
            }
            if (shakeTimer > 0 && shakeTab == 1) {
                asura.X += ShakeOffset();
            }
            asuraRect = asura;

            Rectangle godSmith = GameModeTheme.GodSmithTab(EasedReveal());
            godSmith.X += SlideOffset(entranceGodSmith);
            godSmith.Y -= (int)MathF.Round(godSmithHover * 2f);
            if (pressDip > 0f && pressTab == 2) {
                godSmith.Y += (int)MathF.Round(pressDip * 2.5f);
            }
            if (shakeTimer > 0 && shakeTab == 2) {
                godSmith.X += ShakeOffset();
            }
            godSmithRect = godSmith;
        }

        private void UpdateInteraction() {
            //先算后比，悬停 tick 只在进入新目标时响一次
            int next = -1;
            if (TabsVisible) {
                Point mouse = Main.MouseScreen.ToPoint();
                if (brutalRect.Contains(mouse)) {
                    next = 0;
                }
                else if (asuraReveal > 0.6f && asuraRect.Contains(mouse)) {
                    next = 1;
                }
                else if (godSmithRect.Contains(mouse)) {
                    next = 2;
                }
            }
            if (next != hoverKind && next != -1) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f });
            }
            hoverKind = next;
            brutalHover = Ease(brutalHover, hoverKind == 0 ? 1f : 0f, 0.2f);
            asuraHover = Ease(asuraHover, hoverKind == 1 ? 1f : 0f, 0.2f);
            godSmithHover = Ease(godSmithHover, hoverKind == 2 ? 1f : 0f, 0.2f);

            if (hoverKind < 0) {
                return;
            }

            player.mouseInterface = true;
            tipKind = hoverKind;
            GameModeKind kind = hoverKind switch {
                0 => GameModeKind.Brutal,
                1 => GameModeKind.Asura,
                _ => GameModeKind.GodSmith,
            };

            if (keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (GameModeSystem.RequestToggle(kind)) {
                //单人当帧生效由差分点火接管；联机等服务端回执，这里只给受理音与按压下沉
                pressDip = 1f;
                pressTab = hoverKind;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else {
                //拒绝必有回应：横震 + 闷响（Boss 在场或修罗依赖未满足）
                shakeTimer = ShakeDuration;
                shakeTab = hoverKind;
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 0.7f });
            }
        }

        /// <summary>首见引导推进：未发现时脉冲计时，悬停任一标签即收束熄灭并落档</summary>
        private void UpdateGuide() {
            if (discovered) {
                if (guideAck > 0f) {
                    guideAck = Math.Max(0f, guideAck - 1f / 26f);
                }
                return;
            }
            if (!TabsVisible) {
                return;
            }
            guideTimer++;
            if (hoverKind != -1) {
                discovered = true;
                guideAck = 1f;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.75f, Pitch = 0.15f });
                GameModeRenderer.EmitAckMotes(brutalRect);
            }
        }

        /// <summary>传给 shader 的引导增辉包络：未发现=慢呼吸脉冲，发现后=收束余辉</summary>
        private float GuideLevel() {
            if (!discovered) {
                float breath = 0.5f + 0.5f * MathF.Sin(
                    guideTimer * (MathHelper.TwoPi / GuidePulsePeriod) - MathHelper.PiOver2);
                return 0.4f + 0.55f * breath;
            }
            return guideAck * 0.8f;
        }

        /// <summary>点亮标签顶缘偶发余烬上浮；微粒池推进随之</summary>
        private void UpdateMotes() {
            if (TabsVisible) {
                if (GameModeSystem.BrutalActive && brutalLit > 0.8f && Main.rand.NextBool(24)) {
                    GameModeRenderer.EmitIdleMote(brutalRect, GameModeFace.Brutal);
                }
                if (GameModeSystem.AsuraActive && asuraLit > 0.8f && asuraReveal > 0.9f && Main.rand.NextBool(24)) {
                    GameModeRenderer.EmitIdleMote(asuraRect, GameModeSystem.FaceOf(GameModeKind.Asura));
                }
                if (GameModeSystem.GodSmithActive && godSmithLit > 0.8f && Main.rand.NextBool(24)) {
                    GameModeRenderer.EmitIdleMote(godSmithRect, GameModeFace.GodSmith);
                }
            }
            GameModeRenderer.UpdateMotes(TabsVisible);
        }

        private float EasedReveal() => MathHelper.SmoothStep(0f, 1f, asuraReveal);

        /// <summary>标签的按压回响强度（受理请求后随 pressDip 衰减）</summary>
        private float PressOf(int tab) => pressDip > 0f && pressTab == tab ? pressDip : 0f;

        /// <summary>标签的拒绝红脉冲强度（与横震同拍衰减）</summary>
        private float DenyOf(int tab) => shakeTimer > 0 && shakeTab == tab
            ? shakeTimer / (float)ShakeDuration : 0f;

        /// <summary>悬浮说明是否在场（含淡出尾），由顶层 overlay 查询</summary>
        internal bool TipActive => TabsVisible && tipKind >= 0 && TipEase(tipKind) > 0.03f;

        private float TipEase(int tab) => tab switch {
            0 => brutalHover,
            1 => asuraHover,
            _ => godSmithHover,
        };

        /// <summary>顶层悬浮说明绘制入口（由 <see cref="GameModeTipOverlay"/> 调用，压在同层其他 UI 之上）</summary>
        internal void DrawTipOverlay(SpriteBatch spriteBatch) {
            if (!TipActive) {
                return;
            }
            GameModeKind kind = tipKind switch {
                0 => GameModeKind.Brutal,
                1 => GameModeKind.Asura,
                _ => GameModeKind.GodSmith,
            };
            Rectangle rect = tipKind switch {
                0 => brutalRect,
                1 => asuraRect,
                _ => godSmithRect,
            };
            GameModeRenderer.DrawHoverPanel(spriteBatch, rect, kind, TipEase(tipKind));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (TabsVisible) {
                //神匠最先画（常驻末席），修罗次之，残酷压在上层；天顶世界里修罗恒以毁灭脸示人
                float reveal = EasedReveal();
                GameModeFace asuraFace = GameModeSystem.FaceOf(GameModeKind.Asura);
                GameModeRenderer.DrawTab(spriteBatch, godSmithRect, GameModeFace.GodSmith,
                    godSmithLit, godSmithHover, godSmithBurst, godSmithBurstOn, disabledDim,
                    0f, EntranceAlpha(entranceGodSmith), PressOf(2), DenyOf(2));
                if (reveal > 0.01f) {
                    GameModeRenderer.DrawTab(spriteBatch, asuraRect, asuraFace,
                        asuraLit, asuraHover, asuraBurst, asuraBurstOn, disabledDim,
                        0f, reveal * EntranceAlpha(entranceAsura), PressOf(1), DenyOf(1));
                }

                GameModeRenderer.DrawTab(spriteBatch, brutalRect, GameModeFace.Brutal,
                    brutalLit, brutalHover, brutalBurst, brutalBurstOn, disabledDim,
                    GuideLevel(), EntranceAlpha(entranceBrutal), PressOf(0), DenyOf(0));

                //切换爆发的越身扩张环压在旗身之上
                if (godSmithBurst < 1f) {
                    GameModeRenderer.DrawBurstRing(spriteBatch, godSmithRect, GameModeFace.GodSmith, godSmithBurst, godSmithBurstOn);
                }
                if (asuraBurst < 1f && reveal > 0.01f) {
                    GameModeRenderer.DrawBurstRing(spriteBatch, asuraRect, asuraFace, asuraBurst, asuraBurstOn);
                }
                if (brutalBurst < 1f) {
                    GameModeRenderer.DrawBurstRing(spriteBatch, brutalRect, GameModeFace.Brutal, brutalBurst, brutalBurstOn);
                }

                //首见信标 / 悬停确认收束
                if (!discovered) {
                    float ringT = guideTimer % GuidePulsePeriod / (float)GuideRingFrames;
                    GameModeRenderer.DrawGuideBeacon(spriteBatch, brutalRect,
                        ringT <= 1f ? ringT : -1f, GuideLevel());
                }
                else if (guideAck > 0f) {
                    GameModeRenderer.DrawGuideAck(spriteBatch, brutalRect, guideAck);
                }

                GameModeRenderer.DrawMotes(spriteBatch);
            }

            GameModeRenderer.DrawCeremonyLine(spriteBatch);
        }

        private int ShakeOffset()
            => (int)(MathF.Sin(shakeTimer * 1.35f) * (shakeTimer / (float)ShakeDuration) * 4f);

        /// <summary>入场滑入的落座缓动：轻微过冲再归位</summary>
        private static int SlideOffset(float t) {
            if (t >= 1f) {
                return 0;
            }
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            float eased = 1f + c3 * u * u * u + c1 * u * u;
            return (int)MathF.Round((1f - eased) * EntranceSlide);
        }

        /// <summary>入场期快速淡入</summary>
        private static float EntranceAlpha(float t) => Math.Min(1f, t * 3f);

        private static float Ease(float cur, float target, float rate) {
            float next = MathHelper.Lerp(cur, target, rate);
            return Math.Abs(next - target) < 0.004f ? target : next;
        }
    }

    /// <summary>
    /// 模式标签悬浮说明的顶层绘制壳：说明面板压在同层其余 UIHandle（任务书图标等）之上，
    /// 不与旗身同批（口径同 KikasaHudTipOverlay）
    /// </summary>
    internal sealed class GameModeTipOverlay : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Mouse_Text;

        public override float RenderPriority => 10f;

        public override bool Active => GameModeTabUI.Instance?.TipActive ?? false;

        public override void Draw(SpriteBatch spriteBatch)
            => GameModeTabUI.Instance?.DrawTipOverlay(spriteBatch);
    }
}
