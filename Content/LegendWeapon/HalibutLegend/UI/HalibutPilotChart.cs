using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 引航海图纹样,无贴图,罗经形体由 SVG 路径串描出<br/>
    /// 归一 [-1,1] 空间,任意尺寸锐利
    /// </summary>
    internal static class HalibutPilotChartSigil
    {
        //正圆,四段三次贝塞尔(kappa 0.5523);与稽古符共用同一 d 串,解析缓存命中
        private const string RingData =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z";

        //罗经四正点,菱形轮廓交于心
        private const string RoseData =
            "M 0,-0.80 L 0.12,-0.16 L 0,0 L -0.12,-0.16 Z"
            + " M 0,0.80 L 0.12,0.16 L 0,0 L -0.12,0.16 Z"
            + " M 0.80,0 L 0.16,0.12 L 0,0 L 0.16,-0.12 Z"
            + " M -0.80,0 L -0.16,0.12 L 0,0 L -0.16,-0.12 Z";

        //四隅短芒
        private const string RayData =
            "M 0.13,-0.13 L 0.44,-0.44"
            + " M -0.13,-0.13 L -0.44,-0.44"
            + " M 0.13,0.13 L 0.44,0.44"
            + " M -0.13,0.13 L -0.44,0.44";

        //磁针,北长南短
        private const string NeedleData = "M 0,-0.62 L 0.10,0 L 0,0.30 L -0.10,0 Z";

        /// <summary>声呐环与旋臂的最小可读半径,再小只剩闪烁</summary>
        private const float DetailRadius = 20f;

        /// <summary>画一枚海图,radius = 归一半径对应的像素</summary>
        internal static void Draw(SpriteBatch sb, Vector2 center, float radius, float alpha, float time) {
            if (alpha <= 0.01f || radius < 2f) {
                return;
            }

            SvgPath ring = SvgPathPen.Path(RingData);
            SvgPath rose = SvgPathPen.Path(RoseData);
            SvgPath rays = SvgPathPen.Path(RayData);
            SvgPath needle = SvgPathPen.Path(NeedleData);

            bool detailed = radius >= DetailRadius;
            float breath = HalibutTheme.Breath(time, 1.1f, 2f);
            float thin = MathF.Max(radius * 0.09f, 1.05f);

            //环走 SVG 笔而非 DrawRing:后者是每 2.5px 一段的三层径向填充,画一枚图标要几百次 draw
            SvgPathPen.SoftDot(sb, center, radius * 1.5f,
                HalibutTheme.Glow, (0.30f + 0.16f * breath) * alpha);
            //暗盘是唯一值得用径向填充的一层,冷光笔画要靠它在亮背景上立住
            HalibutRenderer.DrawDisc(sb, center, radius * 0.94f, radius * 0.14f,
                HalibutTheme.Deep * (0.88f * alpha));

            SvgPathPen.Stroke(sb, ring, center, radius * 0.96f, 0f,
                HalibutTheme.Glow, thin * 0.9f, alpha * 0.7f);
            if (detailed) {
                SvgPathPen.Stroke(sb, ring, center, radius * 0.24f, 0f,
                    HalibutTheme.GlowHi, thin * 0.8f, alpha * 0.55f);
            }

            //罗经盘面:金线阴刻,静止不动
            SvgPathPen.Stroke(sb, rose, center, radius * 0.86f, 0f,
                HalibutTheme.Accent, thin, alpha * 0.92f, 0f, 1f,
                detailed ? HalibutTheme.Caustic : null);
            SvgPathPen.Stroke(sb, rays, center, radius * 0.86f, 0f,
                HalibutTheme.Teal, thin * 0.8f, alpha * 0.8f);

            //磁针:慢摆 + 一点未定的抖,整根都在动,小尺寸也读得出
            float swing = MathF.Sin(time * 0.35f) * 0.55f + MathF.Sin(time * 1.7f) * 0.06f;
            SvgPathPen.Stroke(sb, needle, center, radius * 0.86f, swing,
                HalibutTheme.GlowHi, thin * 1.1f, alpha, 0f, 1f, HalibutTheme.Caustic);
            SvgPathPen.SoftDot(sb, center, radius * 0.16f, HalibutTheme.Caustic, alpha * 0.6f);

            if (detailed) {
                //转盘语汇的旋臂
                float rot = time * 0.42f;
                for (int i = 0; i < 6; i++) {
                    float a0 = rot + i * MathHelper.TwoPi / 6f;
                    HalibutRenderer.DrawArcStroke(sb, center, radius * 1.06f, a0, a0 + 0.34f,
                        thin * 0.85f, HalibutTheme.GlowHi * (0.45f * alpha));
                }
                //声呐:一环自心扩出后没入
                float ping = time * 0.5f % 1f;
                float pingFade = (1f - ping) * (1f - ping);
                SvgPathPen.Stroke(sb, ring, center,
                    MathHelper.Lerp(radius * 0.25f, radius * 1.3f, ping), 0f,
                    HalibutTheme.Glow, thin * 0.7f, 0.55f * pingFade * alpha);
            }

            //焦散气泡,确定性相位,自盘口上浮
            int pearls = detailed ? 3 : 2;
            for (int i = 0; i < pearls; i++) {
                float seed = (i * 0.37f + 0.13f) % 1f;
                float rise = (time * (0.24f + seed * 0.14f) + seed) % 1f;
                float x = MathF.Sin((seed + rise) * MathHelper.TwoPi) * radius * 0.62f;
                Vector2 pos = center + new Vector2(x, MathHelper.Lerp(radius * 0.9f, -radius * 1.1f, rise));
                SvgPathPen.SoftDot(sb, pos, radius * (0.14f + 0.06f * seed),
                    HalibutTheme.Caustic, alpha * 0.5f * MathF.Sin(rise * MathF.PI));
            }
        }
    }

    /// <summary>婉拒开场引导时补发的凭证,使用即重开比目鱼引导</summary>
    internal sealed class HalibutPilotChart : ModItem, ILocalizedModType
    {
        public override string LocalizationCategory => "Items";
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>行囊里没有大比目鱼时的回绝</summary>
        public static LocalizedText NeedFish { get; private set; }
        /// <summary>已展开,等玩家换手到鱼上才开讲</summary>
        public static LocalizedText HoldFish { get; private set; }
        /// <summary>进行中再用一次,收起引导</summary>
        public static LocalizedText Closed { get; private set; }
        /// <summary>对话或过场占场时的回绝</summary>
        public static LocalizedText NotNow { get; private set; }

        public override void SetStaticDefaults() {
            NeedFish = this.GetLocalization(nameof(NeedFish), () => "行囊里没有大比目鱼，海图无处可引");
            HoldFish = this.GetLocalization(nameof(HoldFish), () => "海图已展开，握起大比目鱼便开讲");
            Closed = this.GetLocalization(nameof(Closed), () => "引导已收起，再用此图可重开");
            NotNow = this.GetLocalization(nameof(NotNow), () => "此刻另有要事，海图铺不开");
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void Unload() {
            NeedFish = null;
            HoldFish = null;
            Closed = null;
            NotNow = null;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Quest;
            Item.value = 0;
            Item.consumable = false;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 28;
            Item.useTurn = true;
            Item.UseSound = SoundID.SplashWeak with { Pitch = -0.35f, Volume = 0.5f };
        }

        /// <summary>补一张海图进行囊,已有则不重发</summary>
        internal static void GrantTo(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) {
                return;
            }
            int type = ModContent.ItemType<HalibutPilotChart>();
            if (!player.HasItem(type)) {
                player.QuickSpawnItem(player.GetSource_Misc("CWR_HalibutPilotChart"), type);
            }
            VaultUtils.Text(HalibutHudLead.DeclineNotice.Value, HalibutTheme.Accent);
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return null;
            }
            //进行中再用一次当收起,给引导一条不必退世界的退出路
            if (HalibutHudLead.StopFromChart(player)) {
                VaultUtils.Text(Closed.Value, HalibutTheme.TextDim);
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.15f, Volume = 0.45f }, player.Center);
                return true;
            }
            if (!player.HasItem(HalibutOverride.ID)) {
                VaultUtils.Text(NeedFish.Value, HalibutTheme.Danger);
                return true;
            }
            if (HalibutHudLead.ChartStartBlocked) {
                VaultUtils.Text(NotNow.Value, HalibutTheme.TextDim);
                return true;
            }
            if (!HalibutHudLead.StartFromChart(player)) {
                return true;
            }
            //引导只在手持鱼时才画,而用图这一刻手上拿的是图,不说清就是"用了没反应"
            VaultUtils.Text(HoldFish.Value, HalibutTheme.GlowHi);

            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.2f, Volume = 0.5f }, player.Center);
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_Spark>(player.Center + angle.ToRotationVector2() * 18f,
                    angle.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f),
                    HalibutTheme.GlowHi * 0.6f, Main.rand.NextFloat(0.24f, 0.38f))
                    ?.Configure(affectedByGravity: false, lifetime: Main.rand.Next(16, 26));
            }
            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position,
            Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            HalibutPilotChartSigil.Draw(spriteBatch, position, 16f * scale,
                MathF.Max(drawColor.A / 255f, 0.5f), Main.GlobalTimeWrappedHourly);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            HalibutPilotChartSigil.Draw(spriteBatch, Item.Center - Main.screenPosition, 18f * scale,
                MathF.Max(lightColor.A / 255f, 0.42f), Main.GlobalTimeWrappedHourly);
            return false;
        }
    }
}
