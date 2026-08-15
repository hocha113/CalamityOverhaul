using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 掌中缩影：鬼伞常驻 HUD（左下，持伞或领域激活时浮现）。
    /// 它就是「湖畔村图」的小样——同一个着色器场景低细节跑在一张小横片上，
    /// 水位、形态浸染、窗火与画同步；点它即展开全画（任何域状态都响应）。
    /// 画框下缘两条细线是仅存的读数：沉溺手冷却与梦中唤犬冷却。
    /// </summary>
    internal class KikasaHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaHud Instance => UIHandleLoader.GetUIHandleOfType<KikasaHud>();

        public static LocalizedText ScrollName { get; private set; }
        public static LocalizedText OpenTag { get; private set; }

        public override void SetStaticDefaults() {
            ScrollName = this.GetLocalization(nameof(ScrollName), () => "湖畔村图");
            OpenTag = this.GetLocalization(nameof(OpenTag), () => "点击展开画卷");
        }

        //==================== 可见性 ====================

        private float appear;

        private static bool HoldingUmbrella(Player p) {
            Item item = p.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        private static bool WantVisible(Player p)
            => HoldingUmbrella(p) || p.GetModPlayer<KikasaDomainPlayer>().AnyActive;

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead || Main.dedServ) {
                    return false;
                }
                return WantVisible(p) || appear > 0.01f;
            }
        }

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上覆悬停名牌，下覆冷却细线
        float IBottomLeftHud.HudStackTopExtent => 50f;
        float IBottomLeftHud.HudStackBottomExtent => 40f;
        #endregion

        /// <summary>自然锚点（缩影中心），未参与左下队列避让时的原始位置</summary>
        public static Vector2 NaturalAnchor => new(KikasaHudTheme.AnchorOffset.X,
            KikasaHudTheme.UIScreenH + KikasaHudTheme.AnchorOffset.Y);

        /// <summary>缩影中心锚点，经左下队列避让后的最终位</summary>
        public static Vector2 Anchor {
            get {
                KikasaHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        /// <summary>缩影画片矩形；「湖畔村图」自这里放大铺开</summary>
        public static Rectangle MiniRect {
            get {
                Vector2 anchor = Anchor;
                return new Rectangle(
                    (int)(anchor.X - KikasaHudTheme.MiniW * 0.5f),
                    (int)(anchor.Y - KikasaHudTheme.MiniH * 0.5f),
                    KikasaHudTheme.MiniW, KikasaHudTheme.MiniH);
            }
        }

        //==================== 状态 ====================

        //缩影只留最轻的水语：事件搅一记，读数交给画
        private float stir;
        private int lastVaultCount;
        private int lastMemoryType;
        private bool lastLakeReady;

        private bool hoverMini;
        private float hoverLerp;

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaDreamPlayer Dream => player.GetModPlayer<KikasaDreamPlayer>();

        //==================== 更新 ====================

        public override void Update() {
            Player p = player;
            bool want = WantVisible(p);
            appear = MathHelper.Clamp(appear + (want ? 0.06f : -0.06f), 0f, 1f);

            Vector2 anchor = Anchor;
            Size = new Vector2(KikasaHudTheme.MiniW + 16f, KikasaHudTheme.MiniH + 30f);
            DrawPosition = anchor - Size * 0.5f;
            UIHitBox = DrawPosition.GetRectangle(Size);

            //事件只在小样上搅一记水，细节反馈都在大画里
            KikasaVaultPlayer vault = Vault;
            int vaultCount = vault.Stored.Count;
            int memoryType = p.GetModPlayer<KikasaServants.KikasaServantPlayer>().LastDrownedType;
            bool lakeReady = vault.LakeReady;
            if ((vaultCount != lastVaultCount || memoryType != lastMemoryType
                || (lakeReady && !lastLakeReady)) && appear > 0.1f) {
                stir = MathF.Max(stir, 0.6f);
            }
            lastVaultCount = vaultCount;
            lastMemoryType = memoryType;
            lastLakeReady = lakeReady;
            stir = MathHelper.Lerp(stir,
                Domain.Phase == KikasaDomainPhase.Opening
                || Domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.12f, 0.06f);

            //悬停占鼠标；点击展开/收起画卷——任何域状态都响应
            Rectangle mini = MiniRect;
            hoverMini = appear > 0.5f && mini.Contains(KikasaHudTheme.UIMouse.ToPoint());
            hoverLerp = MathHelper.Lerp(hoverLerp, hoverMini ? 1f : 0f, 0.15f);
            if (hoverMini) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    KikasaSceneUI scene = KikasaSceneUI.Instance;
                    if (scene != null) {
                        if (scene.IsOpen) {
                            scene.Close();
                        }
                        else {
                            scene.Open();
                        }
                    }
                }
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = appear;
            if (a < 0.01f) {
                return;
            }
            //画卷展开后小样让位，免得同屏两幅画
            float sceneOpen = KikasaSceneUI.Instance?.OpenProgress ?? 0f;
            a *= 1f - MathHelper.Clamp(sceneOpen * 1.4f, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            KikasaDomainPlayer domain = Domain;
            float rain = KikasaSceneUI.EffectiveRain(domain);
            float rise = domain.AnyActive ? domain.RiseProgress : 0f;
            float waterUv = KikasaSceneTheme.WaterUv(rise);
            Rectangle mini = MiniRect;
            //浮现自下轻托
            mini.Y += (int)((1f - a) * 8f);

            //1 画心小样：同一场景低细节；灯火照湖藏
            float lightGate = Vault.Stored.Count / (float)KikasaVaultPlayer.Capacity;
            KikasaSceneUI.Instance?.DrawVistaFor(spriteBatch, mini, a, rain, waterUv,
                1f - rise, MathHelper.Clamp(stir, 0f, 1f), domain.FlipBoil,
                domain.FlipFlash, lightGate);

            //2 装裱：两侧细卷杆 + 底缘装裱线；悬停提亮
            Color barCol = KikasaHudTheme.Void(rain);
            Color coreCol = Color.Lerp(KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain),
                hoverLerp * 0.5f);
            float frameA = a * (0.7f + hoverLerp * 0.3f);
            foreach (float x in (Span<float>)[mini.Left - 3f, mini.Right + 3f]) {
                KikasaVaultRenderer.DrawLine(spriteBatch, new Vector2(x, mini.Top - 4f),
                    new Vector2(x, mini.Bottom + 4f), 2.6f, barCol * frameA);
                KikasaVaultRenderer.DrawLine(spriteBatch, new Vector2(x, mini.Top - 4f),
                    new Vector2(x, mini.Bottom + 4f), 0.9f, coreCol * (frameA * 0.6f));
            }
            KikasaVaultRenderer.DrawLine(spriteBatch, new Vector2(mini.Left, mini.Bottom + 1f),
                new Vector2(mini.Right, mini.Bottom + 1f), 1f, coreCol * (frameA * 0.45f));
            KikasaVaultRenderer.DrawLine(spriteBatch, new Vector2(mini.Left, mini.Top - 1f),
                new Vector2(mini.Right, mini.Top - 1f), 1f, coreCol * (frameA * 0.45f));

            //3 读数细线：沉溺手冷却（主色）；梦中另一条唤犬冷却（烬红）
            float drownCd = KikasaDrown.LocalCooldown01;
            if (drownCd > 0.005f) {
                KikasaVaultRenderer.DrawLine(spriteBatch,
                    new Vector2(mini.Left, mini.Bottom + 5f),
                    new Vector2(mini.Left + mini.Width * drownCd, mini.Bottom + 5f),
                    1.6f, KikasaHudTheme.Glow(rain) * (0.55f * a));
            }
            if (domain.Phase == KikasaDomainPhase.Dreaming) {
                float houndCd = Dream.HoundCooldown01;
                if (houndCd > 0.005f) {
                    KikasaVaultRenderer.DrawLine(spriteBatch,
                        new Vector2(mini.Left, mini.Bottom + 8f),
                        new Vector2(mini.Left + mini.Width * houndCd, mini.Bottom + 8f),
                        1.4f, new Color(230, 96, 40) * (0.6f * a));
                }
            }

            //4 悬停名牌：画名 + 展开提示
            if (hoverLerp > 0.05f) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                string name = ScrollName.Value;
                Vector2 nameSize = font.MeasureString(name) * 0.78f;
                float nameY = mini.Top - 22f;
                Utils.DrawBorderString(spriteBatch, name,
                    new Vector2(mini.Center.X - nameSize.X * 0.5f, nameY),
                    KikasaHudTheme.Text(rain) * (hoverLerp * a), 0.78f);
                string tag = OpenTag.Value;
                Vector2 tagSize = font.MeasureString(tag) * 0.62f;
                Utils.DrawBorderString(spriteBatch, tag,
                    new Vector2(mini.Center.X - tagSize.X * 0.5f, nameY + nameSize.Y + 1f),
                    KikasaHudTheme.TextDim(rain) * (hoverLerp * a * 0.9f), 0.62f);
            }
        }
    }
}
