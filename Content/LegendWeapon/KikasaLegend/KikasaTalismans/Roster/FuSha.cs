using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霎「霎那」（礼物序 00，史莱姆王）：起伞 16 帧缩到 4 帧，
    /// 开伞首拍必落"急雨三连"（三滴 1 帧错拍、各 0.75x、落点轮转异点），
    /// 代价是三连后的下一拍间隔 +40%。<br/>
    /// 会话仓语义：CounterA=三连剩余配额（仅所有者端消耗）、
    /// CounterB=本次撑伞已出手拍数（各端随 OnVolley 同拍自增）、TimerA=最近出手帧锚点
    /// </summary>
    internal sealed class FuSha : KikasaTalismanDefinition
    {
        /// <summary>三连滴伤害折减</summary>
        private const float TripleDamageMul = 0.75f;

        /// <summary>三连后的下一拍间隔倍率</summary>
        private const float PenaltyPeriodMul = 1.4f;

        public override int SortOrder => 100;

        /// <summary>银白青：骤雨初落那一瞬的冷银</summary>
        public override Color InkAccent => new(198, 222, 228);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //起伞 16f → 4f：霎那即至
            profile.RiseFramesMul *= 0.25f;
        }

        //霎：雨盖下一道短促斜闪贯落，旁一点朱点——那一瞬的亮
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.10f, 0.22f, -0.30f, -0.10f, 0.12f, 0.08f, 0.18f, -0.14f, 0.56f),
            Dot(0.11f, 0.34f, 0.40f),
        ];

        //====行为====

        internal override void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state != null) {
                state.CounterA = 3;
                state.CounterB = 0;
                state.TimerA = 0;
            }
            FuShaFX.RainStartBurst(umbrella, InkAccent);
        }

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            int tripleCount = Math.Max(rhythm.DropCount, 3);
            //出手窗滞留：拍序在出手拍自增，但该拍的错拍滴/帮衬拍还没走完，
            //窗内仍按上一拍的解走，防节拍解在窗中途换挡吞掉后续滴
            int fired = state.CounterB;
            if (fired > 0 && umbrella.ai[2] <= state.TimerA + tripleCount + 2) {
                fired--;
            }
            if (fired == 0) {
                //「急霎」首拍三连：1 帧错拍近似同帧，落点随目标轮转天然异点
                rhythm.DropCount = tripleCount;
                rhythm.Stagger = 1;
            }
            else if (fired == 1) {
                //三连的代价：下一拍间隔 +40%。节拍网格锚在悬停计时零点，
                //本拍拉伸后、再下一拍会先补一记约 0.6 倍的短拍才回到基准格（取舍见交付报告）
                rhythm.Period = Math.Max((int)MathF.Round(rhythm.Period * PenaltyPeriodMul),
                    rhythm.Period + 1);
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            state.CounterB++;
            //出手帧锚点，供节拍解的窗滞留判定
            state.TimerA = (int)umbrella.ai[2];
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //三连只属于开伞首拍的伞缘滴，墨瀑散射滴不占配额
            if (drop.FromPourScatter) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.CounterA <= 0) {
                return;
            }
            state.CounterA--;
            drop.DamageMul *= TripleDamageMul;
            //打霎标（先到先得），载荷记三连序 0..2；弹道与绘制按标分支
            if (drop.TagId == 0) {
                drop.TagId = KikasaTalismanHooks.TagIdFor(this);
                drop.TagPayload = 2 - state.CounterA;
            }
        }

        internal override void ModifyDropCurve(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropCurve curve) {
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //急霎直坠：弧段减半抢先入坠，坠得更急更快（叠乘既有值，各端同参确定性）
            curve.ArcDur *= 0.5f;
            curve.PlungeGravity *= 1.7f;
            curve.PlungeMaxSpeed *= 1.3f;
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //三连滴换银白青：冷银亮体+白芯（冷水材质允许白芯），速度线在 UpdateWhileHeld 侧喷发
            draw.Body = new Color(168, 198, 208);
            draw.Deep = new Color(84, 116, 130);
            draw.Core = Color.White;
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            if (Main.dedServ) {
                return;
            }
            //带霎标的坠滴拖白色速度线：纯表现，各端本地跑；标随生成包同步，旁观端同样看得到
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                    continue;
                }
                FuShaFX.DropSpeedLine(proj, InkAccent);
            }
        }
    }

    /// <summary>霎符纸：礼物符不配合成配方，随史莱姆王礼物戏发放</summary>
    internal sealed class KikasaTalismanSha : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuSha);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "疾雨符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "起伞极快，开伞第一拍连落三滴急雨；三连后一拍稍缓");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "符师赶夜路遇上劫道的，撑伞、落雨、收伞，前后不过一眨眼。事后他把这一手\"快\"字写成了符");
            this.GetLocalization("Power",
                () => "「疾雨」起伞耗时 -75%；开伞第一拍必连落三滴急雨（各 75% 伤害）");
            this.GetLocalization("Burden",
                () => "三连之后，下一拍间隔 +40%");
            base.SetDefaults();
        }
    }
}
