using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霰「霰珠」（礼物序 04）：大滴（鬼滴/墨瀑散射滴）落地碎成 5 粒弹跳霰珠
    /// （各 0.30x，可再弹 2 次）；代价是墨洼半径 -15%。<br/>
    /// FromPourScatter 只存在于生成上下文，故在 ModifyDropSpawn 给大滴打霰标，
    /// 落地各端凭标识滴；本符不占会话仓
    /// </summary>
    internal sealed class FuXian : KikasaTalismanDefinition
    {
        /// <summary>碎珠粒数</summary>
        private const int PelletCount = 5;

        /// <summary>单粒霰珠伤害 = 源滴伤害 × 此倍率</summary>
        private const float PelletDamageMul = 0.30f;

        /// <summary>霰珠可再弹次数</summary>
        internal const int PelletBounces = 2;

        /// <summary>墨洼半径代价</summary>
        private const float PuddleRadiusPenalty = 0.85f;

        public override int SortOrder => 104;

        /// <summary>霜白：冻在半空的那种白</summary>
        public override Color InkAccent => FuXianFX.Accent;

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.PuddleRadiusMul *= PuddleRadiusPenalty;
        }

        //霰：雨盖下一滴贯落，落中裂作三叉，旁蹦一星碎珠
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.11f, 0.00f, -0.30f, 0.00f, 0.06f, 0.03f, 0.46f),
            L(0.085f, 0.00f, 0.06f, -0.24f, 0.40f),
            L(0.085f, 0.00f, 0.06f, 0.26f, 0.38f),
            Dot(0.09f, 0.34f, 0.14f),
        ];

        //====行为====

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //只认大滴：鬼滴与墨瀑散射滴。打霰标供落地各端识滴（先到先得，不夺人标）
            if ((!drop.Ghost && !drop.FromPourScatter) || drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
        }

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //只有落地才碎：命中敌人的大滴已经付清直击，凌空消散无从着力
            if (!onTile) {
                return;
            }
            FuXianFX.ShatterFlash(drop.Center);
            if (!ctx.IsOwnerClient) {
                return;
            }
            //扇形上抛五粒：中间高两侧斜，落地还能再弹（生成物随生成包同步）
            for (int i = 0; i < PelletCount; i++) {
                float ang = -MathHelper.PiOver2
                    + MathHelper.Lerp(-0.95f, 0.95f, i / (float)(PelletCount - 1))
                    + Main.rand.NextFloat(-0.12f, 0.12f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(4.6f, 7.2f);
                Projectile.NewProjectile(drop.GetSource_FromThis(),
                    drop.Center - Vector2.UnitY * 6f, vel,
                    ModContent.ProjectileType<FuXianHailPellet>(),
                    (int)(drop.damage * PelletDamageMul), 0.5f, ctx.Owner.whoAmI);
            }
        }
    }

    /// <summary>霰符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanXian : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuXian);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "溅珠符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "大滴落地碎成五粒弹跳墨珠；墨洼略小");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "大雨砸在石板上，水珠能蹦起半人高，溅人一身。符师把这股蹦劲写进符里，让水珠也学会咬人");
            this.GetLocalization("Power",
                () => "「溅珠」大滴落地碎成五粒墨珠（各 30% 伤害），每粒还能再弹跳两次");
            this.GetLocalization("Burden",
                () => "墨洼半径 -15%");
            base.SetDefaults();
        }
    }
}
