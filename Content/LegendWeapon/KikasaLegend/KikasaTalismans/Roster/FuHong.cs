using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 虹「霁虹」（礼物序 08）：墨瀑收势后在落点上空拱起虹桥 5 秒——
    /// 虹下玩家移速 +10%、召唤伤 +10%（各端本地给自家玩家），
    /// 虹身对穿过之敌每 0.5s 一次 0.4x 瀑伤判定。代价墨瀑宽度 x0.90。<br/>
    /// 会话仓语义：不使用（虹桥是自含投射物，状态全在弹幕上）
    /// </summary>
    internal sealed class FuHong : KikasaTalismanDefinition
    {
        /// <summary>虹身判定伤害倍率（取墨瀑伤）</summary>
        private const float BridgeDamageMul = 0.4f;

        public override int SortOrder => 108;

        /// <summary>虹粉：七场雨各还一笔的颜色</summary>
        public override Color InkAccent => new(238, 152, 178);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //分了七色出去，瀑就细了一分
            profile.PourWidthMul *= 0.90f;
        }

        //虹：雨盖下满弧双层拱，右脚一点朱点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.11f, 0.00f, 0.52f, 0.50f, 3.32f, 6.10f, 14),
            Arc(0.08f, 0.00f, 0.54f, 0.32f, 3.44f, 5.98f, 12),
            Dot(0.10f, 0.52f, 0.42f),
        ];

        //====行为====

        internal override void OnPourEnd(in KikasaTalismanRainContext ctx, Projectile pour) {
            //谢幕挂钩在非服务器各端派发；生成只归所有者端。
            //空泼（射线没找到落点）不起虹——虹要有雨停的地方
            if (!ctx.IsOwnerClient
                || pour.ModProjectile is not KikasaInkPour inkPour || !inkPour.HitGroundNow) {
                return;
            }
            Vector2 basePos = inkPour.FallEndPoint - new Vector2(0f, 36f);
            Projectile.NewProjectile(pour.GetSource_FromThis(), basePos, Vector2.Zero,
                ModContent.ProjectileType<FuHongRainbowBridge>(),
                System.Math.Max((int)(pour.damage * BridgeDamageMul), 1),
                1f, ctx.Owner.whoAmI);
        }
    }

    /// <summary>虹符纸：礼物符不配合成配方，随礼物戏发放（礼物序 08）</summary>
    internal sealed class KikasaTalismanHong : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuHong);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "虹桥符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨瀑收势后架起虹桥：桥下增益、桥身灼敌；墨瀑略窄");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "雨后见虹是吉兆。符师索性把吉兆画进符里：雨一停就架桥，站在桥下的人人沾光");
            this.GetLocalization("Power",
                () => "「虹桥」墨瀑收势后，落点上空架起虹桥五秒：桥下移速 +10%、召唤伤害 +10%；穿过桥身的敌人每半秒受 40% 瀑伤");
            this.GetLocalization("Burden",
                () => "墨瀑宽度 -10%");
            base.SetDefaults();
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
