using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 澍「澍雨」（礼物序 11，及时雨）：受击开 3 秒急救窗——窗内雨拍间隔 x0.5、
    /// 滴染金线、每滴命中回 1 HP（所有者端）；冷却 12 秒。代价平时雨拍间隔 x1.05。<br/>
    /// 会话仓语义：TimerA=急救窗剩余帧、TimerB=冷却剩余帧（触发即 720，含窗期；
    /// 二者各端本地自减，旁观端缺 OnHurt 时其节拍仅近似、无碍权威）、
    /// CounterA=最近出手拍是否窗内（0/1）、CounterB=最近出手帧锚点（供节拍解窗滞留）
    /// </summary>
    internal sealed class FuShu : KikasaTalismanDefinition
    {
        /// <summary>急救窗时长与冷却（自触发起算）</summary>
        private const int RescueWindowFrames = 180;
        private const int RescueCooldownFrames = 720;

        /// <summary>窗内雨拍间隔倍率</summary>
        private const float WindowPeriodMul = 0.5f;

        /// <summary>每滴回复量</summary>
        private const int HealPerDrop = 1;

        public override int SortOrder => 111;

        /// <summary>澍金：旱地上第一场雨的颜色</summary>
        public override Color InkAccent => new(232, 192, 112);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //急雨救人，缓雨养伞：平时雨拍稍缓
            profile.RainTempoMul *= 1.05f;
        }

        //澍：雨盖下两线自侧汇入一点，中线贯点复散向下，另一侧一滴朱点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.09f, -0.34f, -0.14f, 0.00f, 0.18f),
            L(0.09f, 0.34f, -0.14f, 0.00f, 0.18f),
            L(0.10f, 0.00f, -0.26f, 0.00f, 0.18f, -0.16f, 0.52f),
            Dot(0.11f, 0.16f, 0.48f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            //拍锚随伞的状态计时走：新一次撑伞计时归零，滞留锚必须同步清掉，
            //否则上一把伞留下的陈旧锚会把窗滞留判定卡死一整段（急救窗计时不清，跨伞持续）
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.CounterA = 0;
                state.CounterB = 0;
            }
        }

        internal override void OnOwnerHurt(in KikasaTalismanRainContext ctx, in Player.HurtInfo info) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerB > 0) {
                return;
            }
            state.TimerA = RescueWindowFrames;
            state.TimerB = RescueCooldownFrames;
            //触发演出：伞面金边涟漪+身周水环，各持雨端本地
            FuShuFX.RescueBurst(ctx.Owner, InkAccent);
        }

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            bool useWindow = state.TimerA > 0;
            //出手窗滞留：上一拍的错拍滴/帮衬拍还没走完时仍按那一拍的解走，
            //防急救窗恰在窗中途开合把节拍解换挡、吞掉后续滴
            int dwell = rhythm.DropCount * rhythm.Stagger + 2;
            if (state.CounterB > 0 && umbrella.ai[2] <= state.CounterB + dwell) {
                useWindow = state.CounterA == 1;
            }
            if (useWindow) {
                //窗内雨拍 x0.5：整拍操作，出手窗护栏由调用方在挂钩后再钳
                rhythm.Period = Math.Max((int)MathF.Round(rhythm.Period * WindowPeriodMul), 1);
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //记下这一拍的出手帧与所用的解，供节拍窗滞留判定
            state.CounterB = (int)umbrella.ai[2];
            state.CounterA = state.TimerA > 0 ? 1 : 0;
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //窗内滴打澍标（先到先得）：金线绘制与速度线按标分支，标随生成包同步
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0 || drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //雨丝转金线：暖金亮体+金白芯
            draw.Body = new Color(208, 170, 92);
            draw.Deep = new Color(122, 96, 50);
            draw.Core = new Color(255, 236, 170);
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //窗内每滴命中回 1 HP：命中钩只在所有者端跑，Heal 正落在生命权威端
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (kind != KikasaRainSourceKind.Drop || state == null || state.TimerA <= 0) {
                return;
            }
            if (ctx.Owner.statLife < ctx.Owner.statLifeMax2) {
                ctx.Owner.Heal(HealPerDrop);
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            if (state.TimerA > 0) {
                state.TimerA--;
            }
            if (state.TimerB > 0) {
                state.TimerB--;
            }
            if (Main.dedServ) {
                return;
            }
            if (state.TimerA > 0) {
                //窗内伞面金滴泵：伞沿渗金，各端本地
                FuShuFX.WindowPump(ctx.Owner, InkAccent);
                //澍标坠滴拖金色速度线（标随生成包同步，旁观端同样看得到）
                int dropType = ModContent.ProjectileType<KikasaInkDrop>();
                int myTag = KikasaTalismanHooks.TagIdFor(this);
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                        || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                        continue;
                    }
                    FuShuFX.GoldDropLine(proj, InkAccent);
                }
            }
        }
    }

    /// <summary>澍符纸：礼物符不配合成配方，随礼物戏发放（礼物序 11）</summary>
    internal sealed class KikasaTalismanShu : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuShu);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·澍");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "受击后三秒雨拍减半、滴滴回血；平时雨拍稍缓");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "「澍」是及时雨的旧名。旱了太久的人写它，笔画里全是催");
            this.GetLocalization("Power",
                () => "「澍雨」受击后三秒内雨拍间隔 x0.5，雨丝转金，每滴命中回复 1 点生命；冷却十二秒");
            this.GetLocalization("Burden",
                () => "平时雨拍间隔 +5%。急雨救人，缓雨养伞");
            base.SetDefaults();
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
