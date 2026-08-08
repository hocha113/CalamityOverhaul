using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 稽古符纹样,无贴图,形体全部由 SVG 路径串描出<br/>
    /// 归一 [-1,1] 空间,任意尺寸锐利
    /// </summary>
    internal static class OniKeikoRuneSigil
    {
        //内环,四段三次贝塞尔逼近正圆(kappa 0.5523)
        private const string RingData =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z";

        //外圈八节短弧,整体缓转
        private const string DashData =
            "M 0.7657,-0.1488 L 0.7657,0.1488"
            + " M 0.6466,0.4362 L 0.4362,0.6466"
            + " M 0.1488,0.7657 L -0.1488,0.7657"
            + " M -0.4362,0.6466 L -0.6466,0.4362"
            + " M -0.7657,0.1488 L -0.7657,-0.1488"
            + " M -0.6466,-0.4362 L -0.4362,-0.6466"
            + " M -0.1488,-0.7657 L 0.1488,-0.7657"
            + " M 0.4362,-0.6466 L 0.6466,-0.4362";

        //鸟居,子路径序即笔序:笠木-额束-贯-两柱-两础
        private const string ToriiData =
            "M -0.70,-0.52 Q 0,-0.34 0.70,-0.52"
            + " M 0,-0.42 L 0,-0.12"
            + " M -0.48,-0.12 L 0.48,-0.12"
            + " M -0.30,-0.42 L -0.38,0.60"
            + " M 0.30,-0.42 L 0.38,0.60"
            + " M -0.50,0.60 L -0.26,0.60"
            + " M 0.26,0.60 L 0.50,0.60";

        /// <summary>巡回墨笔的最小可读半径,再小只剩闪烁,不如让形体安静</summary>
        private const float DetailRadius = 20f;

        /// <summary>画一枚符,radius = 归一半径对应的像素</summary>
        internal static void Draw(SpriteBatch sb, Vector2 center, float radius, float alpha, float time) {
            if (alpha <= 0.01f || radius < 2f) {
                return;
            }

            SvgPath ring = SvgPathPen.Path(RingData);
            SvgPath dashes = SvgPathPen.Path(DashData);
            SvgPath torii = SvgPathPen.Path(ToriiData);

            bool detailed = radius >= DetailRadius;
            float breath = 0.5f + 0.5f * MathF.Sin(time * 1.7f);
            Color accent = Color.Lerp(OnikiriUITheme.Bright, OnikiriUITheme.GhostFire, 0.18f + 0.22f * breath);
            float thin = MathF.Max(radius * 0.095f, 1.05f);

            //底衬:符体本身只有几十像素,允许小幅羽化压出暗芯
            OniBrush.DrawBacklight(sb, center, radius * 1.4f, accent, alpha * (0.14f + 0.13f * breath));
            OniBrush.DrawFeathered(sb, center, MathHelper.PiOver4,
                new Vector2(radius * 1.18f), OnikiriUITheme.Ink, alpha * 0.75f);

            SvgPathPen.Stroke(sb, dashes, center, radius, time * 0.20f,
                OnikiriUITheme.Deep, thin * 0.85f, alpha * 0.8f);
            //小图标上内环直接给亮色,巡行弧那点位移看不出来
            SvgPathPen.Stroke(sb, ring, center, radius * 0.63f, 0f,
                detailed ? OnikiriUITheme.Deep : Color.Lerp(OnikiriUITheme.Deep, accent, 0.45f),
                thin * 0.9f, alpha * 0.85f);
            SvgPathPen.Stroke(sb, torii, center, radius * 0.80f, 0f,
                OnikiriUITheme.Paper, thin * 1.05f, alpha * 0.92f);

            if (detailed) {
                SvgPathPen.StrokeRunner(sb, ring, center, radius * 0.63f, 0f,
                    accent, thin, alpha, time * 0.28f, 0.17f, OnikiriUITheme.HotWhite);
                //一段湿墨按鸟居笔序巡回,走完留一段停顿
                float wet = time * 0.22f % 1.45f;
                if (wet < 1f) {
                    SvgPathPen.StrokeRunner(sb, torii, center, radius * 0.80f, 0f,
                        accent, thin * 1.15f, alpha * 0.9f, wet, 0.20f, OnikiriUITheme.HotWhite);
                }
            }

            //刀痕:斜贯符面的一斩,扫入后淡出;尺度大,小图标上也读得出
            float cut = time * 0.30f % 1f;
            float sweep = MathHelper.Clamp(cut / 0.32f, 0f, 1f);
            float fade = 1f - MathHelper.Clamp((cut - 0.52f) / 0.48f, 0f, 1f);
            if (fade > 0.01f) {
                OniBrush.DrawTaperedSlash(sb,
                    center + new Vector2(-1.02f, 0.70f) * radius,
                    center + new Vector2(1.02f, -0.56f) * radius,
                    MathF.Max(radius * 0.12f, 1.2f), radius * 0.09f, alpha * 0.8f * fade, sweep);
            }

            //鬼火余烬,确定性相位,自符口上飘
            int embers = detailed ? 4 : 2;
            for (int i = 0; i < embers; i++) {
                float seed = OniBrush.Hash01(i * 37 + 11);
                float rise = (time * (0.22f + seed * 0.15f) + seed) % 1f;
                float x = MathF.Sin((seed + rise) * MathHelper.TwoPi) * radius * 0.66f;
                Vector2 pos = center + new Vector2(x, MathHelper.Lerp(radius * 0.9f, -radius * 1.15f, rise));
                OniBrush.DrawSoftDot(sb, pos, radius * (0.09f + 0.05f * seed),
                    OnikiriUITheme.GhostFire, alpha * 0.45f * MathF.Sin(rise * MathF.PI));
            }
        }
    }

    /// <summary>婉拒开场教习时补发的凭证,使用即重开鬼切教习</summary>
    internal sealed class OniKeikoRune : ModItem, ILocalizedModType
    {
        public override string LocalizationCategory => "Items";
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>行囊里没有鬼切时的回绝</summary>
        public static LocalizedText NeedBlade { get; private set; }
        /// <summary>进行中再用一次,收起教习</summary>
        public static LocalizedText Closed { get; private set; }
        /// <summary>对话或过场占场时的回绝</summary>
        public static LocalizedText NotNow { get; private set; }

        public override void SetStaticDefaults() {
            NeedBlade = this.GetLocalization(nameof(NeedBlade), () => "行囊里没有鬼切，符引不出教习");
            Closed = this.GetLocalization(nameof(Closed), () => "教习已收起，再用此符可重开");
            NotNow = this.GetLocalization(nameof(NotNow), () => "此刻另有要事，符压不住场");
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void Unload() {
            NeedBlade = null;
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
            Item.UseSound = SoundID.Item29 with { Pitch = -0.55f, Volume = 0.5f };
        }

        /// <summary>补一枚符进行囊,已有则不重发</summary>
        internal static void GrantTo(Player player) {
            if (Main.dedServ || player?.whoAmI != Main.myPlayer) {
                return;
            }
            int type = ModContent.ItemType<OniKeikoRune>();
            if (!player.HasItem(type)) {
                player.GiveItem(player.GetSource_Misc("CWR_OnikiriKeikoRune"), type);
            }
            VaultUtils.Text(OnikiriTutorialLead.DeclineNotice.Value, OnikiriUITheme.GoldInlay);
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return null;
            }
            //进行中再用一次当收起,给教习一条不必退世界的退出路
            if (OnikiriTutorialLead.StopFromRune(player)) {
                VaultUtils.Text(Closed.Value, OnikiriUITheme.TextDim);
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.7f, Volume = 0.32f }, player.Center);
                return true;
            }
            if (!player.HasItem(OnikiriOverride.ID)) {
                VaultUtils.Text(NeedBlade.Value, OnikiriUITheme.Seal);
                return true;
            }
            if (OnikiriTutorialLead.RuneStartBlocked) {
                VaultUtils.Text(NotNow.Value, OnikiriUITheme.TextDim);
                return true;
            }
            OnikiriTutorialLead.StartFromRune(player);

            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.3f, Volume = 0.5f }, player.Center);
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(player.Center + angle.ToRotationVector2() * 18f,
                    angle.ToRotationVector2() * Main.rand.NextFloat(1.4f, 2.8f),
                    new Color(255, 243, 226), Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
            }
            return true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position,
            Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            OniKeikoRuneSigil.Draw(spriteBatch, position, 16f * scale,
                MathF.Max(drawColor.A / 255f, 0.5f), Main.GlobalTimeWrappedHourly);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            //浮沉交给 ItemNoGravity 的原版位移,这里只按当前位置落笔
            OniKeikoRuneSigil.Draw(spriteBatch, Item.Center - Main.screenPosition, 18f * scale,
                MathF.Max(lightColor.A / 255f, 0.42f), Main.GlobalTimeWrappedHourly);
            return false;
        }
    }
}
