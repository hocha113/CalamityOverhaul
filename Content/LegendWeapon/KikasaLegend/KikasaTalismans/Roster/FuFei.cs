using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霏「雨雪霏霏」（礼物序 03）：每第 3 滴墨化作霏雾滴（本体 -30%），
    /// 消散处滞留漂雾团 2 秒——雾中之敌缓伤、缓行一成。<br/>
    /// 会话仓语义：CounterA=滴序计数 0..2（仅所有者端 ModifyDropSpawn 自增，逢三归零）
    /// </summary>
    internal sealed class FuFei : KikasaTalismanDefinition
    {
        /// <summary>每第几滴化雾</summary>
        private const int MistEveryNth = 3;

        /// <summary>霏雾滴本体伤害折减</summary>
        private const float MistDropDamageMul = 0.70f;

        /// <summary>漂雾团单口伤害 = 雾滴伤害 × 此倍率（约 30 帧一口）</summary>
        private const float CloudTickDamageMul = 0.40f;

        /// <summary>雾中缓行比例：每帧回拉一成位移</summary>
        internal const float CloudSlowFraction = 0.10f;

        public override int SortOrder => 103;

        /// <summary>灰青：看不清远山的那种雨雾色</summary>
        public override Color InkAccent => FuFeiFX.Accent;

        //霏：雨盖下斜排四点，一点淡过一点——雪一样斜着下的雨
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Dot(0.13f, -0.34f, -0.10f),
            Dot(0.11f, -0.10f, 0.12f),
            Dot(0.095f, 0.14f, 0.34f),
            Dot(0.08f, 0.38f, 0.56f),
        ];

        //====行为====

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //瀑散滴不占雨拍滴序（霏的节拍属于伞缘雨，也把大滴标签让给霰）
            if (drop.FromPourScatter) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            if (++state.CounterA < MistEveryNth) {
                return;
            }
            state.CounterA = 0;
            //标签先到先得：被先手符占用则本滴不化雾，滴序照常轮转
            if (drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
            drop.DamageMul *= MistDropDamageMul;
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //霏雾滴：灰青柔调，体胖芯淡，先在飞行里就读出"这滴是雾"
            draw.Body = new Color(112, 132, 138);
            draw.Deep = new Color(58, 74, 80);
            draw.Core = new Color(196, 214, 214);
            draw.SizeMul = 1.18f;
        }

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //雾团判定只在归属端起（随生成包同步）；各端再补一口柔雾，
            //把常规溅裂的读感往"化开"上盖——滴系共享谢幕无关断通道，只能叠不能删
            if (ctx.IsOwnerClient) {
                Projectile.NewProjectile(drop.GetSource_FromThis(), drop.Center, Vector2.Zero,
                    ModContent.ProjectileType<FuFeiMistCloud>(),
                    (int)(drop.damage * CloudTickDamageMul), 0f, ctx.Owner.whoAmI);
            }
            FuFeiFX.SoftenDeath(drop.Center);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            if (Main.dedServ) {
                return;
            }
            //霏雾滴飞行拖薄雾：纯表现各端本地；标签随生成包同步，旁观端同样看得到
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                    continue;
                }
                FuFeiFX.DropHaze(proj);
            }
        }
    }

    /// <summary>霏符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanFei : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuFei);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "凝雾符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "每第三滴化作雾滴，落点滞留缓伤减速的墨雾；雾滴本体较轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "山雨下到一半，常化成雾缠着人不散。符师觉得这股缠劲比雨点还难缠，索性收进符里替自己办事");
            this.GetLocalization("Power",
                () => "「凝雾」每第三滴墨化作雾滴：命中处滞留墨雾两秒，雾中之敌持续受伤、移速 -10%");
            this.GetLocalization("Burden",
                () => "雾滴本体伤害 -30%");
            base.SetDefaults();
        }
    }
}
