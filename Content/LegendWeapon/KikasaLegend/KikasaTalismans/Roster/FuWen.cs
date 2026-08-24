using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 雯「云篆」（礼物序 16，肉山后）：伞缘环绕三枚云篆符星（Glyph 笔画实描的
    /// 常驻 PRT 表现件），墨滴命中充能，满充时轮值符星自掷为追踪墨箭
    /// （1.2x 滴伤，owner 端生成，符文尾迹）。代价 RainTempoMul 1.08。<br/>
    /// 会话仓语义：MeterA=符星充能 0..1（仅所有者端命中累积，满充清回）、
    /// CounterA=已掷符星序号（owner，定轮值星位并驱动再凝动画）、
    /// TimerB=符星 PRT 活性信标（各端本地，PRT 逐帧写、UpdateWhileHeld 失联即补生）
    /// </summary>
    internal sealed class FuWen : KikasaTalismanDefinition
    {
        /// <summary>每滴命中充能量（八滴一箭）</summary>
        private const float ChargePerHit = 0.125f;

        /// <summary>墨箭伤害倍率（对满充那滴的滴伤）</summary>
        private const float ArrowDamageMul = 1.2f;

        public override int SortOrder => 116;

        /// <summary>云篆金青：铜绿里透金的旧云纹</summary>
        public override Color InkAccent => new(172, 204, 150);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //分神驭星，雨拍放缓 8%
            profile.RainTempoMul *= 1.08f;
        }

        //雯：雨盖下一大一小两道反向回环云纹，云尾外扬一挑，朱点坠左下
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.10f, -0.10f, 0.10f, 0.30f, -1.20f, 2.60f, 12),
            Arc(0.08f, 0.16f, 0.24f, 0.16f, 1.20f, 4.80f, 10),
            L(0.07f, 0.30f, 0.36f, 0.52f, 0.48f),
            Dot(0.10f, -0.46f, 0.52f),
        ];

        //====行为====

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //滴命中充能（仅所有者端）；满充即轮值符星自掷追踪墨箭
            if (kind != KikasaRainSourceKind.Drop) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            state.MeterA += ChargePerHit;
            if (state.MeterA < 1f) {
                return;
            }
            state.MeterA -= 1f;
            int starIndex = state.CounterA % 3;
            state.CounterA++;

            //出箭点取轮值符星的当前轨道位；伞不在（理论不至）退到头顶
            Projectile umbrella = FuWenFX.FindUmbrella(ctx.Owner);
            Vector2 launchPos = umbrella != null
                ? FuWenFX.StarPos(umbrella, starIndex)
                : ctx.Owner.Top - Vector2.UnitY * 40f;
            Vector2 vel = (npc.Center - launchPos).SafeNormalize(-Vector2.UnitY) * 9f;
            int damage = (int)(source.damage * ArrowDamageMul);
            if (damage > 0) {
                Projectile.NewProjectile(source.GetSource_FromThis(), launchPos, vel,
                    ModContent.ProjectileType<FuWenArrowProj>(), damage, source.knockBack,
                    ctx.Owner.whoAmI, npc.whoAmI, starIndex);
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //符星常驻件：PRT 逐帧写活性信标，失联即补生（撑伞中丢失/旁观中途入场）
            if (Main.dedServ) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || (int)Main.GameUpdateCount - state.TimerB <= 4) {
                return;
            }
            Projectile umbrella = FuWenFX.FindUmbrella(ctx.Owner);
            if (umbrella == null) {
                return;
            }
            state.TimerB = (int)Main.GameUpdateCount;
            InnoVault.PRT.PRTLoader.NewParticle<PRT_FuWenStars>(
                umbrella.Center, Vector2.Zero, InkAccent, 1f)
                ?.Configure(ctx.Owner.whoAmI, InkAccent, nameof(FuWen));
        }
    }

    /// <summary>雯符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanWen : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuWen);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "云篆符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "伞缘三枚符星随命中充能，蓄满自掷追踪墨箭；雨拍稍缓");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "云篆是道门写在云上的字，凡人临不来。符师临了个大概，字没临像，倒是学会了自己飞");
            this.GetLocalization("Power",
                () => "「云篆」伞缘环绕三枚云篆符星，墨滴每次命中为其充能；蓄满时轮值符星自动掷出追踪墨箭（120% 滴伤）");
            this.GetLocalization("Burden",
                () => "雨拍间隔 +8%");
            base.SetDefaults();
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }
    }
}
