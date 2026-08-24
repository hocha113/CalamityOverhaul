using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霅「霅霅」（礼物序 14，肉山后）：伞顶悬七点节拍环，连续 7 拍每拍至少
    /// 1 滴命中，第 8 拍全滴 x3「重霅」；漏拍清零，重霅后停雨 1 拍
    /// （节拍解整拍置零 DropCount，帧锚窗滞留防吞重霅滴）。<br/>
    /// 会话仓语义（连击账全在所有者端，旁观端节拍环为近似演出）：
    /// CounterA=连击拍数 0..7，负值为重霅整理相位（-2=重霅拍进行、-1=停雨拍进行）、
    /// CounterB=本拍命中滴数、TimerA=重霅出手帧锚（0=无，节拍解窗滞留用）、
    /// MeterA=重霅拍进行标记（滴生成 x3 窗）、TimerB=节拍环 PRT 活性信标（各端本地）
    /// </summary>
    internal sealed class FuZha : KikasaTalismanDefinition
    {
        /// <summary>连击所需拍数</summary>
        internal const int ComboBeats = 7;

        /// <summary>重霅全滴伤害倍率</summary>
        private const float HeavyDamageMul = 3f;

        public override int SortOrder => 114;

        /// <summary>鼓棕金：绷紧的鼓皮被雨点敲亮的那种棕金</summary>
        public override Color InkAccent => new(208, 164, 96);

        //霅：雨盖下三短横作鼓点阶梯，一记重竖收槌，朱点落鼓心
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.08f, -0.50f, -0.12f, -0.22f, -0.12f, -0.44f, 0.12f, -0.16f, 0.12f, -0.38f, 0.36f, -0.10f, 0.36f),
            L(0.16f, 0.24f, -0.18f, 0.28f, 0.52f),
            Dot(0.10f, 0.42f, 0.62f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            //新雨新鼓：连击与重霅相位全部清零（PRT 活性信标不动，归环自己管）
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.CounterA = 0;
                state.CounterB = 0;
                state.TimerA = 0;
                state.MeterA = 0f;
            }
        }

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            //停雨一拍：以重霅出手帧为锚，重霅出手窗过完之后的一个整周期内
            //DropCount=0、齐掷拍关断（防齐掷拍在停拍窗全鬼齐掷）。
            //窗滞留保证重霅拍自己的滴全部出手后才停，纯函数只读状态
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0) {
                return;
            }
            int span = rhythm.DropCount * rhythm.Stagger + 4;
            int delta = (int)umbrella.ai[2] - state.TimerA;
            if (delta > span && delta <= span + rhythm.Period) {
                rhythm.DropCount = 0;
                rhythm.GhostVolley = false;
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            //连击账只有所有者端可知（命中只在 owner 端结算）
            if (!ctx.IsOwnerClient) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            if (state.CounterA < 0) {
                //重霅后的整理拍：-2→-1 停雨拍开始（重霅窗随之关闭）、-1→0 回到蓄拍
                state.CounterA++;
                state.MeterA = 0f;
                state.CounterB = 0;
                if (state.CounterA == 0) {
                    state.TimerA = 0;
                }
                return;
            }
            //结算上一拍：有命中续连击，漏拍清零重数
            if (state.CounterB > 0) {
                state.CounterA++;
            }
            else {
                state.CounterA = 0;
            }
            state.CounterB = 0;
            if (state.CounterA >= ComboBeats) {
                //第 8 拍触发重霅：本拍全滴 x3，帧锚落定供停拍窗
                state.MeterA = 1f;
                state.TimerA = (int)umbrella.ai[2];
                state.CounterA = -2;
                FuZhaFX.HeavyBeatBurst(umbrella, InkAccent);
            }
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //节拍属于伞的甩雨，墨瀑散射滴不入拍
            if (drop.FromPourScatter) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //停雨拍防御兜底：根治已落（伞侧帮衬鬼滴出手条件已加 dropCount>0 门），
            //保留哑滴压制，防未来新增节拍外出手点再漏
            if (state.CounterA == -1) {
                drop.DamageMul *= 0f;
                drop.Scale *= 0.6f;
                return;
            }
            if (state.MeterA < 0.5f) {
                return;
            }
            //重霅拍：全滴 x3，打鼓标走绘制与落点鼓纹分支
            drop.DamageMul *= HeavyDamageMul;
            if (drop.TagId == 0) {
                drop.TagId = KikasaTalismanHooks.TagIdFor(this);
                drop.TagPayload = 0;
            }
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //重霅滴换鼓棕金：暗鼓皮体色+金芯，滴身放大一档
            draw.Body = new Color(88, 64, 34);
            draw.Deep = new Color(44, 30, 16);
            draw.Core = new Color(255, 212, 128);
            draw.SizeMul = 1.2f;
        }

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            //只认自己的重霅滴：鼓面波纹+鼓点声在各客户端本地落拍
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            FuZhaFX.DrumRipple(drop, InkAccent);
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //本拍命中记账：任何一滴命中即算拍达成
            if (kind != KikasaRainSourceKind.Drop) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.CounterB++;
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //节拍环常驻件：PRT 逐帧写活性信标，失联（撑伞中环丢失/旁观中途入场）即补生
            if (Main.dedServ) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || (int)Main.GameUpdateCount - state.TimerB <= 4) {
                return;
            }
            Projectile umbrella = FuZhaFX.FindUmbrella(ctx.Owner);
            if (umbrella == null) {
                return;
            }
            state.TimerB = (int)Main.GameUpdateCount;
            InnoVault.PRT.PRTLoader.NewParticle<PRT_FuZhaBeatRing>(
                umbrella.Center - Vector2.UnitY * 46f, Vector2.Zero, InkAccent, 1f)
                ?.Configure(ctx.Owner.whoAmI, InkAccent,
                    KikasaTalismanHooks.TagIdFor(this), nameof(FuZha));
        }
    }

    /// <summary>霅符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanZha : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuZha);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霅");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "七拍连击不漏则第八拍全滴 x3 重霅；重霅后停雨一拍");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "霅霅，是雨点敲在鼓皮上的声音。写符的人数着拍子落笔，七拍写完，一拍未漏");
            this.GetLocalization("Power",
                () => "「七拍」伞顶悬七点节拍环：连续七拍每拍至少一滴命中，第八拍全滴伤害 x3，重霅齐落如擂鼓");
            this.GetLocalization("Burden",
                () => "重霅之后停雨一拍；漏了拍，环碎重数。鼓点容不得虚一声");
            base.SetDefaults();
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 1);
        }
    }
}
