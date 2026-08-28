using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霖「细雨」（合成首符，SortOrder 0）：雨拍 x0.80、单滴 x0.94；
    /// 连雨不断（攻击态）约十秒蓄满「连绵」，入「霖成」：雨拍再 x0.90（合计约 x0.72）、
    /// 滴染青灰（打霖标，先到先得）；停雨渐散、收伞折半。
    /// 通道所有权：连绵计量 + 伞下雨丝帘（帘密度即连绵读数，演出与数值同源）。<br/>
    /// 会话仓语义：MeterA=连绵度 0..1（各端本地随攻击态推进，昼夜浸染同款近似，owner 端权威）、
    /// TimerA=霖成状态位（0/1，迟滞开阖：满蓄开、退过 0.6 关，各端本地）、
    /// CounterA=最近出手拍霖成快照（0/1，节拍窗滞留判据）、CounterB=最近出手帧锚
    /// </summary>
    internal sealed class FuLin : KikasaTalismanDefinition
    {
        /// <summary>连绵蓄满帧数（攻击态累计）＝10 秒</summary>
        private const int BuildFrames = 600;

        /// <summary>停雨消退帧数（满到空）＝2.5 秒</summary>
        private const int DecayFrames = 150;

        /// <summary>霖成雨拍倍率（叠在基线 0.80 上）</summary>
        private const float SteadyTempoMul = 0.90f;

        /// <summary>霖成迟滞关断阈：短暂停手不掉档</summary>
        private const float SteadyOffThreshold = 0.6f;

        public override int SortOrder => 0;

        public override Color InkAccent => new(96, 158, 204);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.RainTempoMul *= 0.80f;
            profile.DropDamageMul *= 0.94f;
        }

        //霖：雨盖下三缕错拍斜雨，雨脚三点渐远——连日不歇的节奏感
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.09f, -0.44f, -0.28f, -0.58f, 0.26f),
            L(0.09f, -0.04f, -0.20f, -0.16f, 0.42f),
            L(0.09f, 0.36f, -0.26f, 0.26f, 0.20f),
            L(0.07f, 0.58f, -0.02f, 0.50f, 0.34f),
            Dot(0.10f, -0.62f, 0.52f),
            Dot(0.09f, -0.22f, 0.66f),
            Dot(0.10f, 0.16f, 0.50f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            //连绵度存续（雨还连着），但拍锚随撑伞会话走：陈旧滞留锚必须清
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.CounterA = state.TimerA;
                state.CounterB = 0;
            }
        }

        internal override void OnRecall(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            //收伞雨断一口：连绵折半，霖成随迟滞阈自然掉档
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.MeterA *= 0.5f;
            }
        }

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            bool steady = state.TimerA == 1;
            //出手窗滞留（抄澍）：上一拍的错拍滴还没走完时仍按那一拍的解走，
            //防霖成恰在窗中途开阖把节拍解换挡、吞掉后续滴。纯读取无副作用
            int dwell = rhythm.DropCount * rhythm.Stagger + 2;
            if (state.CounterB > 0 && umbrella.ai[2] <= state.CounterB + dwell) {
                steady = state.CounterA == 1;
            }
            if (steady) {
                //霖成：雨拍再密一成（护栏由伞侧在挂钩后统一再钳）
                rhythm.Period = Math.Max((int)MathF.Round(rhythm.Period * SteadyTempoMul), 1);
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            //记下这一拍的出手帧与所用的解，供节拍窗滞留判定
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            state.CounterB = (int)umbrella.ai[2];
            state.CounterA = state.TimerA;
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //霖成滴打霖标（先到先得）：只为青灰染色，不动数值
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA != 1 || drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //霖成滴染青灰：冷雨暗体+灰蓝缘+雨青柔芯（久雨的冷调，区别于霎的银白）
            draw.Body = new Color(38, 52, 66);
            draw.Deep = new Color(84, 118, 150);
            draw.Core = new Color(184, 212, 232);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //连绵账各端本地推进（含服务器，节拍解要用）：攻击态缓涨、停手快退
            bool raining = KikasaRainUmbrella.OwnerIsRaining(ctx.Owner);
            state.MeterA = MathHelper.Clamp(
                state.MeterA + (raining ? 1f / BuildFrames : -1f / DecayFrames), 0f, 1f);
            //霖成迟滞开阖：满蓄开、退过阈关；开阖边沿各端本地起演
            if (state.TimerA == 0 && state.MeterA >= 1f) {
                state.TimerA = 1;
                FuLinFX.SteadyRainSet(ctx.Owner, InkAccent);
            }
            else if (state.TimerA == 1 && state.MeterA < SteadyOffThreshold) {
                state.TimerA = 0;
            }
            //伞下雨丝帘：密度即连绵读数，只在下雨时垂，纯表现各端本地
            if (!Main.dedServ && raining && state.MeterA > 0.04f) {
                FuLinFX.DrizzleVeil(ctx.Owner, state.MeterA, state.TimerA == 1, InkAccent);
            }
        }
    }

    /// <summary>霖符纸：合成首符（近水工作台），非礼物符</summary>
    internal sealed class KikasaTalismanLin : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuLin);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "细雨符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨雨节拍加快，连雨不断愈下愈密；单滴略轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "老符师说：求雨不必惊动龙王，一张细雨符足矣。雨点小些不打紧，要紧的是下个不停");
            this.GetLocalization("Power",
                () => "「细雨」墨雨节拍加快 20%，雨点前赴后继；连雨不断约十秒蓄成「霖」：伞下垂落雨丝帘，雨拍再加快 10%；停手雨势渐散，收伞立折一半");
            this.GetLocalization("Burden",
                () => "单滴伤害 -6%");
            base.SetDefaults();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.RainCloud, 4)
                .AddIngredient(ItemID.Silk, 2)
                .AddIngredient(ItemID.BlackInk, 1)
                .AddTile(TileID.WorkBenches)
                .AddCondition(Condition.NearWater)
                .Register();
        }
    }
}
