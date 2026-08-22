using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>
    /// 结縁演出与告知（owner 客户端）。<br/>
    /// 累计一笔只给一记轻反馈；结縁那一刻在身前依笔序凿出该铭字形，
    /// 拓本落在脚下，读作"这一铭自己找上门"
    /// </summary>
    internal static class OniMeiDeedRite
    {
        /// <summary>累计反馈的最短间隔（帧），防连杀刷屏</summary>
        private const int TickCueCooldown = 24;
        private static ulong lastTickCue;

        /// <summary>推进一笔的轻反馈：刃侧一粒纸白凿屑 + 极轻凿音</summary>
        internal static void PlayTick(Player player, OniMeiDeed deed, int value, int need) {
            if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            if (now - lastTickCue < TickCueCooldown) {
                return;
            }
            lastTickCue = now;
            //越接近结縁，凿声越紧、屑越亮
            float t = need <= 1 ? 1f : MathHelper.Clamp((value - 1f) / (need - 1f), 0f, 1f);
            SoundEngine.PlaySound(SoundID.Tink with {
                Pitch = -0.30f + t * 0.45f,
                Volume = 0.18f + t * 0.14f,
            }, player.Center);
            Vector2 anchor = player.Center - Vector2.UnitY * 8f
                + Vector2.UnitX * player.direction * 16f;
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(anchor + Main.rand.NextVector2Circular(6f, 8f)
                    , Main.rand.NextVector2Circular(1.6f, 1.2f) - Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f)
                    , Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, t)
                    , Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        /// <summary>结縁：字形凿现 + 重凿一记 + 屏震 + 告知行</summary>
        internal static void PlaySettle(Player player, OniMeiDeed deed) {
            if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            bool gold = OniMeiRegistry.TryGet(deed.MeiKey, out OniMeiDefinition definition)
                && definition.IsGoldTier;
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.15f, Volume = 0.55f }, player.Center);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.55f, Volume = 0.70f }, player.Center);
            player.CWR()?.GetScreenShake(3.4f);

            //字形浮在身前一臂处，依笔序凿现后冷却淡出（复用世界字形闪现）
            Vector2 offset = -Vector2.UnitY * 52f;
            PRTLoader.NewParticle<PRT_OniMeiGlyph>(player.Center + offset, Vector2.Zero, Color.White, 1f)
                ?.Configure(deed.MeiKey, 64, 58f,
                    gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright,
                    maxReveal: 1f, followPlayer: player.whoAmI, followOffset: offset);

            //凿屑向外崩一圈，落墨两三滴：刻痕是砸出来的，不是亮出来的
            Vector2 anchor = player.Center + offset;
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(-0.16f, 0.16f);
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(anchor + ang.ToRotationVector2() * 14f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2.4f, 5.2f)
                    , gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.HotWhite
                    , Main.rand.NextFloat(0.22f, 0.38f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(anchor + Main.rand.NextVector2Circular(18f, 14f)
                    , Main.rand.NextVector2Circular(1.8f, 0.8f) + Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.4f)
                    , new Color(30, 14, 16), Main.rand.NextFloat(0.18f, 0.32f))
                    ?.Configure(Main.rand.Next(18, 28));
            }

            AnnounceSettle(deed);
        }

        /// <summary>结縁即落拓本；背包满则掉在脚下</summary>
        internal static void GrantRubbing(Player player, string meiKey) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            int type = OniMeiRubbingItem.ItemTypeForKey(meiKey);
            if (type <= 0) {
                return;
            }
            player.GiveItem(player.GetSource_Misc("CWR_OniMeiDeed"), type);
        }

        private static void AnnounceSettle(OniMeiDeed deed) {
            if (!OniMeiRegistry.TryGet(deed.MeiKey, out OniMeiDefinition definition)
                || definition.DisplayName == null || OniMeiDeedText.Settle == null) {
                return;
            }
            Main.NewText(OniMeiDeedText.Settle.Format(definition.DisplayName.Value),
                OnikiriUITheme.Seal.R, OnikiriUITheme.Seal.G, OnikiriUITheme.Seal.B);
        }
    }

    /// <summary>刀縁的公用文案入口（未凿位木牌与结縁告知共用）</summary>
    internal sealed class OniMeiDeedText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";

        /// <summary>未凿位的类目签</summary>
        public static LocalizedText LockedKind { get; private set; }
        /// <summary>Count 型进度：{0}=已得 {1}=需求</summary>
        public static LocalizedText LockedCount { get; private set; }
        /// <summary>Feat 型未成</summary>
        public static LocalizedText LockedFeat { get; private set; }
        /// <summary>无縁可循的未凿位（本不该出现，兜底）</summary>
        public static LocalizedText LockedUnknown { get; private set; }
        /// <summary>结縁告知：{0}=铭名</summary>
        public static LocalizedText Settle { get; private set; }

        public override void SetStaticDefaults() {
            LockedKind = this.GetLocalization(nameof(LockedKind), () => "未凿");
            LockedCount = this.GetLocalization(nameof(LockedCount), () => "縁分 {0} / {1}");
            LockedFeat = this.GetLocalization(nameof(LockedFeat), () => "縁分未至");
            LockedUnknown = this.GetLocalization(nameof(LockedUnknown), () => "此铭无縁可循");
            Settle = this.GetLocalization(nameof(Settle), () => "刀縁已结，「{0}」的拓本落在脚下");
        }

        /// <summary>某縁在木牌上的进度读法</summary>
        internal static string DescribeProgress(OniMeiDeed deed, int value) {
            if (deed == null) {
                return LockedUnknown?.Value ?? "...";
            }
            if (deed.ProgressKind == OniMeiDeedProgressKind.Count) {
                return LockedCount.Format(Math.Clamp(value, 0, Math.Max(1, deed.NeedCount)),
                    Math.Max(1, deed.NeedCount));
            }
            return LockedFeat.Value;
        }
    }
}
