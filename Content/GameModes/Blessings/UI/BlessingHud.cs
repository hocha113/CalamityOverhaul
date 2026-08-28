using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs;
using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.UI
{
    /// <summary>
    /// 引魂灯 HUD：修罗（含死神永生态）开启时常驻左下角堆叠。
    /// 灯焰映射当前燃焰数，新讨伐时灯焰腾起一记；有未看过的祝福时灯侧一缕新焰苗呼吸。
    /// 点击或按 <see cref="CWRKeySystem.Blessing_Key"/> 打开往生轮；异域全屏开启时淡出让位
    /// </summary>
    internal class BlessingHud : UIHandle, IBottomLeftHud
    {
        public static BlessingHud Instance => UIHandleLoader.GetUIHandleOfType<BlessingHud>();

        /// <summary>解锁灯焰腾起演出时长（帧）</summary>
        private const int UnlockPulseFrames = 100;

        private float hover;
        private Rectangle lanternRect;
        private readonly List<FlameCell> flameScratch = [];

#if DEBUG
        /// <summary>VisLab 视觉联排：真值时无视模式门并伪装燃焰数（仅影响显示）</summary>
        public bool mockActive;
#endif

        public override bool Active {
            get {
                bool gate = BlessingPlayer.SystemActive;
#if DEBUG
                gate |= mockActive;
#endif
                return gate && !Main.gameMenu;
            }
        }

        //——左下角堆叠契约——

        public bool HudStackActive => Active;
        public int HudStackOrder => 40;
        public Vector2 HudStackAnchor => BlessingTheme.LanternAnchor;
        public float HudStackTopExtent => BlessingTheme.LanternSize.Y + 8f;
        public float HudStackBottomExtent => 0f;

        /// <summary>让位遮蔽：异域全屏展开度与自家往生轮展开度取大</summary>
        private static float Occlusion {
            get {
                float foreign = FullScreenUIHub.ForeignOcclusion01(FullScreenUIDomain.Asura);
                float own = BlessingWheelUI.Instance?.OpenProgress.Current ?? 0f;
                return Math.Max(foreign, own);
            }
        }

        /// <summary>解锁腾起包络 1→0</summary>
        private static float UnlockPulse {
            get {
                if (BlessingWorld.RecentUnlock == null) {
                    return 0f;
                }
                uint since = Main.GameUpdateCount - BlessingWorld.RecentUnlockTick;
                return since < UnlockPulseFrames ? 1f - since / (float)UnlockPulseFrames : 0f;
            }
        }

        public override void Update() {
            Vector2 anchor = BottomLeftHudStack.ResolveAnchor(this);
            lanternRect = BlessingTheme.LanternRect(anchor);

            bool interactive = Occlusion < 0.1f;
            bool hovering = interactive && lanternRect.Contains(Main.MouseScreen.ToPoint());
            hover = MathHelper.Lerp(hover, hovering ? 1f : 0f, 0.2f);
            if (!hovering) {
                return;
            }

            player.mouseInterface = true;
            BlessingPlayer bp = Main.LocalPlayer.GetModPlayer<BlessingPlayer>();
            Main.hoverItemName = BlessingSystemText.HudName.Value + " · "
                + BlessingSystemText.BurningCounter.Format(bp.BurningCount, BlessingPlayer.SlotCap)
                + "\n" + BlessingSystemText.HudOpenHint.Value;

            if (keyLeftPressState == KeyPressState.Pressed) {
                BlessingWheelUI.Instance?.Toggle();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float alpha = 1f - Occlusion;
            if (alpha <= 0.01f) {
                return;
            }

            BlessingPlayer bp = Main.LocalPlayer.GetModPlayer<BlessingPlayer>();
            int burning = bp.BurningCount;
            int cap = BlessingPlayer.SlotCap;
            bool hasNew = bp.HasUnwitnessed;
            float pulse = UnlockPulse;
#if DEBUG
            if (mockActive) {
                burning = 2;
                cap = 3;
                hasNew = true;
            }
#endif

            //灯身线稿：燃焰越多灯骨越染 accent，悬停提亮
            float litness = Math.Min(1f, burning / 3f);
            Color frame = Color.Lerp(BlessingTheme.BoneDim, BlessingTheme.Accent,
                0.25f + litness * 0.45f + hover * 0.30f) * alpha;
            SvgPath lantern = SvgPathPen.Path(BlessingRenderer.LanternPath);
            BlessingRenderer.StrokePath100(spriteBatch, lantern, lanternRect.Center.ToVector2(),
                lanternRect.Height * 0.5f, frame, 1.7f, alpha,
                hover > 0.4f ? BlessingTheme.Ember * (hover * 0.6f) : null);

            //灯焰：零燃焰也留一缕长明微焰（灯要能被一眼认出来），亮度随燃焰占比，解锁腾起时焰室扩张
            float litRatio = cap > 0 ? burning / (float)cap : 0f;
            float lit = burning > 0 ? 0.5f + 0.5f * litRatio : 0.18f;
            lit = Math.Min(1f, lit + pulse * 0.8f);
            if (lit > 0.01f) {
                Rectangle flameRect = BlessingRenderer.LanternFlameRect(lanternRect);
                if (pulse > 0f) {
                    int grow = (int)(pulse * 10f);
                    flameRect.Inflate(grow, grow + 4);
                    flameRect.Y -= grow;
                }
                flameScratch.Clear();
                flameScratch.Add(new FlameCell {
                    Rect = flameRect,
                    Seed = 17.3f,
                    Lit = lit,
                    Alpha = alpha,
                });
                BlessingRenderer.DrawFlames(spriteBatch, flameScratch);
            }

            //解锁腾起：自灯心冲出的扩散环 + 一记辉闪
            if (pulse > 0f) {
                Vector2 c = lanternRect.Center.ToVector2();
                float ease = 1f - pulse;
                BlessingRenderer.DrawRingPasses(spriteBatch, c,
                    MathHelper.Lerp(lanternRect.Width * 0.5f, lanternRect.Width * 2.6f, ease * ease),
                    Color.Lerp(BlessingTheme.Accent, BlessingTheme.Ember, 0.5f), pulse * 0.8f * alpha);
                BlessingRenderer.DrawGlow(spriteBatch, c, lanternRect.Width * 2.6f,
                    BlessingTheme.Ember, pulse * 0.5f * alpha);
            }

            //新焰苗：灯顶右侧一粒呼吸余烬
            if (hasNew) {
                float breath = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.06f);
                Vector2 sprout = new(lanternRect.Right - 4f, lanternRect.Y + 6f);
                BlessingRenderer.DrawGlow(spriteBatch, sprout, 16f + breath * 8f,
                    BlessingTheme.Ember, (0.35f + 0.4f * breath) * alpha);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, sprout, new Rectangle(0, 0, 1, 1),
                    BlessingTheme.Ember * ((0.6f + 0.4f * breath) * alpha), 0f,
                    new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>祝福键：修罗开启时开合往生轮</summary>
    internal class BlessingKeySystem : ModSystem
    {
        public override void UpdateUI(GameTime gameTime) {
            if (!BlessingPlayer.SystemActive || Main.gameMenu) {
                return;
            }
            if (CWRKeySystem.Blessing_Key != null && CWRKeySystem.Blessing_Key.JustReleased) {
                BlessingWheelUI.Instance?.Toggle();
            }
        }
    }
}
