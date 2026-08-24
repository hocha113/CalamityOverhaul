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
    /// 雩「大雩」（礼物序 23，终章符）：墨雨命中蓄积雩祭，满值自动开坛入『大雩』十秒——
    /// 雨拍减半、窗内滴携虹墨 +15% 且尽数积洼（洼档以挂钩等效强开）、墨泉档强开；
    /// 祭毕于伞位三墨泉齐发（所有者端）。非大雩期间墨系全伤 -10%。<br/>
    /// 演出：开坛伞影升空自旋（表现层，不动真伞）、雨幕加密、地面雩坛符环大阵逐笔展开、
    /// 窗终三泉齐发，配编钟雅乐。<br/>
    /// 会话仓语义：MeterA=雩祭蓄量（仅 owner 端累积，权威）、
    /// TimerA=大雩窗剩余帧（owner 端权威；旁观端按"虹墨标滴在场"近似刷新，演出允许近似）、
    /// TimerB=最近出手拍锚（节拍滞留）、CounterA=出手拍时窗内快照（0/1，节拍滞留判据）、
    /// CounterB=窗内演出状态位（0=窗外，1=已开坛；各端本地）
    /// </summary>
    internal sealed class FuYu : KikasaTalismanDefinition
    {
        /// <summary>雩祭满值（命中次数）</summary>
        private const int MeterCap = 40;

        /// <summary>大雩窗时长（帧）＝10 秒</summary>
        internal const int WindowFrames = 600;

        /// <summary>窗内雨拍周期倍率</summary>
        private const float WindowTempoMul = 0.5f;

        /// <summary>窗内滴伤害倍率（虹墨）</summary>
        private const float WindowDropMul = 1.15f;

        /// <summary>非窗墨系全伤倍率</summary>
        private const float OffWindowMul = 0.90f;

        public override int SortOrder => 123;

        /// <summary>祭朱金：雩坛丹漆与礼金的颜色</summary>
        public override Color InkAccent => new(232, 128, 64);

        //雩：雨盖下一座"于"形祭台——上短横、下长横、一竖带钩落坛，坛侧一点祭朱
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.13f),
            L(0.10f, -0.24f, -0.18f, 0.24f, -0.15f),
            L(0.10f, -0.38f, 0.08f, 0.38f, 0.11f),
            L(0.12f, 0.03f, -0.15f, 0.01f, 0.54f, -0.20f, 0.66f),
            Dot(0.11f, 0.36f, 0.46f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //节拍滞留锚随撑伞会话走：跨会话的旧锚作废（MeterA/TimerA 存续，大雩不因收伞中断）
            state.TimerB = 0;
            state.CounterA = state.TimerA > 0 ? 1 : 0;
        }

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            bool inWindow = state.TimerA > 0;
            //出手窗滞留（抄霎）：本拍的错拍滴还没走完时，仍按出手拍时点的窗态解走，
            //防大雩起讫瞬间节拍解换挡吞掉窗中滴。纯读取无副作用
            int span = rhythm.DropCount * rhythm.Stagger + 2;
            if (state.TimerB > 0 && umbrella.ai[2] <= state.TimerB + span) {
                inWindow = state.CounterA == 1;
            }
            if (inWindow) {
                //大雩：雨拍减半（护栏由伞侧在挂钩后统一再钳）
                rhythm.Period = Math.Max((int)MathF.Round(rhythm.Period * WindowTempoMul), 1);
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //出手拍锚+窗态快照，供节拍解的窗滞留判定
            state.TimerB = (int)umbrella.ai[2];
            state.CounterA = state.TimerA > 0 ? 1 : 0;
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0) {
                return;
            }
            //大雩滴：虹墨 +15%，洼档挂钩等效强开（Profile 层面做不到窗内动态）
            drop.DamageMul *= WindowDropMul;
            drop.Puddle = true;
            //虹墨标（先到先得）：染虹与旁观端窗近似都认这枚标
            if (drop.TagId == 0) {
                drop.TagId = KikasaTalismanHooks.TagIdFor(this);
            }
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //虹墨：滴身沿七彩缓转，芯色错半相——墨为体、虹为芯
            float hue = (Main.GlobalTimeWrappedHourly * 0.35f + drop.identity * 0.13f) % 1f;
            Color rainbow = Main.hslToRgb(hue, 0.75f, 0.62f);
            draw.Body = Color.Lerp(new Color(30, 16, 14), rainbow, 0.3f);
            draw.Deep = Color.Lerp(new Color(70, 36, 28), rainbow, 0.5f);
            draw.Core = Main.hslToRgb((hue + 0.5f) % 1f, 0.8f, 0.78f);
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //大典之外，天也要歇息：非窗 -10%
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0) {
                modifiers.FinalDamage *= OffWindowMul;
            }
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA > 0) {
                return;
            }
            //蓄雩祭（仅所有者端，权威）：满值自动入大雩，开坛演出走 UpdateWhileHeld 的边沿
            state.MeterA += 1f;
            if (state.MeterA >= MeterCap) {
                state.MeterA = 0f;
                state.TimerA = WindowFrames;
            }
        }

        internal override void ModifyGeyserVolley(in KikasaTalismanRainContext ctx,
            Projectile pour, ref KikasaGeyserVolleyContext geysers) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0) {
                return;
            }
            //大雩：墨泉档强开——不满蓄的墨瀑也唤泉
            geysers.Fire = true;
            if (geysers.TagId == 0) {
                geysers.TagId = KikasaTalismanHooks.TagIdFor(this);
                geysers.TagPayload = 1;
            }
        }

        internal override void OnGeyserErupt(in KikasaTalismanRainContext ctx, Projectile geyser) {
            //标签派发（勿重复查标）：雩泉喷发拍的朱金礼花，各端本地
            FuYuFX.GeyserEruptFlourish(geyser, InkAccent);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //服务器不参与：蓄量权威在 owner 客户端，演出在各客户端
            if (Main.dedServ) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //旁观端窗近似：虹墨标滴在场即视作窗内，TimerA 兜底续上（演出允许近似）
            if (!ctx.IsOwnerClient && FuYuFX.AnyTaggedDrop(ctx.Owner, this)) {
                state.TimerA = Math.Max(state.TimerA, 45);
            }
            if (state.TimerA <= 0) {
                return;
            }
            state.TimerA--;

            //开坛边沿：伞影升空+雩坛大阵+首钟，各端各自起演
            if (state.CounterB == 0) {
                state.CounterB = 1;
                FuYuFX.GrandRainOpening(ctx.Owner, InkAccent);
            }
            //编钟列拍：开坛后连奏两记渐高的钟（旁观端近似窗赶不上列拍，只闻首钟）
            if (state.TimerA == WindowFrames - 40 || state.TimerA == WindowFrames - 80) {
                FuYuFX.RitualChime(ctx.Owner.Center, (WindowFrames - (int)state.TimerA) / 40);
            }
            FuYuFX.WindowAmbient(ctx.Owner, InkAccent);

            //窗终：三墨泉齐发（仅 owner 生成，自然同步）+终鼓，演出状态复位
            if (state.TimerA == 0 && state.CounterB == 1) {
                state.CounterB = 0;
                if (ctx.IsOwnerClient) {
                    FuYuFX.FireFinaleGeysers(ctx.Owner, this);
                }
                FuYuFX.GrandRainClosing(ctx.Owner.Center);
            }
        }
    }

    /// <summary>雩符纸：礼物符不配合成配方，随终章礼物戏发放（获取期四之末）</summary>
    internal sealed class KikasaTalismanYu : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuYu);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·雩");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "命中蓄雩祭，满则开坛入大雩：雨密滴烈、洼泉全开、祭毕三泉齐发；大典之外略钝");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "大旱之年，巫者舞雩于坛，八音齐鸣，雨应声而至。二十四符至此写毕，最后一笔落在坛心");
            this.GetLocalization("Power",
                () => "「大雩」墨雨命中蓄积雩祭，满值自动开坛入『大雩』十秒：雨拍减半，滴携虹墨 +15% 且尽数积洼，墨泉档全开；祭毕，伞下三泉齐发");
            this.GetLocalization("Burden",
                () => "大雩之外墨系全伤 -10%。大典与大典之间，天也要歇息");
            base.SetDefaults();
            //终章符：灾厄在场再上一档绿松石，否则紫档封顶
            Item.rare = CWRID.Rarity_Turquoise > 0 ? CWRID.Rarity_Turquoise : ItemRarityID.Purple;
        }
    }
}
