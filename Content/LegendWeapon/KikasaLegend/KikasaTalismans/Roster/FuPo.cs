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
    /// 霸「月魄」（礼物序 17，肉山后）：夜间限定（Main.dayTime 各端确定一致，
    /// 白昼全部挂钩早退）。伞顶悬墨月随命中渐盈、闲置缓亏；满月时下一次墨瀑
    /// 打月瀑标（标签经瀑 ai[1] 同步，OnPourStart 各端首帧写 TalismanWidthMul x1.5），
    /// 月瀑命中附月痕爆（0.35x，owner 端）；演出=月相渐变、月瀑银粒逆升。
    /// 代价：白昼符力尽敛。<br/>
    /// 会话仓语义：MeterA=月相盈量 0..1（仅所有者端命中累积，满月泼瀑即清）、
    /// CounterA=月瀑序号（owner）、TimerB=墨月 PRT 活性信标（各端本地）
    /// </summary>
    internal sealed class FuPo : KikasaTalismanDefinition
    {
        /// <summary>月瀑瀑宽倍率</summary>
        private const float MoonWidthMul = 1.5f;

        /// <summary>月痕爆伤害倍率（对瀑击）</summary>
        private const float MoonburstMul = 0.35f;

        /// <summary>滴命中的盈量（泉同档；瀑减半、洼酌减）</summary>
        private const float WaxPerDropHit = 0.04f;

        /// <summary>闲置每帧亏量：不下雨，月就慢慢瘦回去</summary>
        private const float WanePerFrame = 0.0004f;

        public override int SortOrder => 117;

        /// <summary>月银：月始生时那一线偏暖的银</summary>
        public override Color InkAccent => new(224, 220, 202);

        //霸：雨盖下带缺圆月，怀中一弯内影，朱点恰补月缺
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.11f, 0.00f, 0.22f, 0.34f, -0.90f, 4.60f, 16),
            Arc(0.07f, 0.08f, 0.22f, 0.20f, 2.20f, 4.40f, 10),
            Dot(0.11f, 0.10f, -0.10f),
        ];

        //====行为====

        internal override void ModifyPourSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaPourSpawnContext pour) {
            //满月转月瀑：打标（随 ai[1] 量化同步）并耗尽月相；先到先得
            if (Main.dayTime || pour.TagId != 0) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            int tagId = KikasaTalismanHooks.TagIdFor(this);
            if (state == null || tagId == 0 || state.MeterA < 1f) {
                return;
            }
            pour.TagId = tagId;
            state.MeterA = 0f;
            state.CounterA++;
        }

        internal override void OnPourStart(in KikasaTalismanRainContext ctx, Projectile pour) {
            //月瀑材质旋钮：各端按同步标签首帧一次性写宽度倍率
            if (Main.dayTime || pour.ModProjectile is not KikasaInkPour inkPour
                || inkPour.TalismanTag != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            inkPour.TalismanWidthMul = MoonWidthMul;
            FuPoFX.MoonPourCue(pour, InkAccent);
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            if (Main.dayTime) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //命中渐盈：滴/泉满档，瀑半档，洼细流
            float wax = kind switch {
                KikasaRainSourceKind.Drop => WaxPerDropHit,
                KikasaRainSourceKind.Geyser => WaxPerDropHit,
                KikasaRainSourceKind.Pour => WaxPerDropHit * 0.5f,
                _ => WaxPerDropHit * 0.2f,
            };
            state.MeterA = MathF.Min(state.MeterA + wax, 1f);

            //月瀑命中附月痕爆（仅所有者端；伤害与半径随生成包自含）
            if (kind == KikasaRainSourceKind.Pour
                && source.ModProjectile is KikasaInkPour inkPour
                && inkPour.TalismanTag == KikasaTalismanHooks.TagIdFor(this)) {
                int damage = (int)(source.damage * MoonburstMul);
                if (damage > 0) {
                    Projectile.NewProjectile(source.GetSource_FromThis(), npc.Center,
                        Vector2.Zero, ModContent.ProjectileType<FuPoMoonburstProj>(),
                        damage, 0f, ctx.Owner.whoAmI, 62f);
                }
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //白昼全部早退：无月、不盈不亏、不做演出
            if (Main.dayTime) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //闲置缓亏：只有权威读数端（所有者）走盈亏账
            if (ctx.IsOwnerClient) {
                state.MeterA = MathF.Max(state.MeterA - WanePerFrame, 0f);
            }
            if (Main.dedServ) {
                return;
            }
            //月瀑银辉：给带月瀑标的自家墨瀑撒银粒逆升（各客户端本地，锚定瀑体几何）
            int pourType = ModContent.ProjectileType<KikasaInkPour>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == pourType && proj.owner == ctx.Owner.whoAmI
                    && proj.ModProjectile is KikasaInkPour inkPour
                    && inkPour.TalismanTag == myTag) {
                    FuPoFX.MoonPourMotes(proj, inkPour, InkAccent);
                }
            }
            //墨月常驻件：PRT 逐帧写活性信标，失联即补生
            if ((int)Main.GameUpdateCount - state.TimerB <= 4) {
                return;
            }
            Projectile umbrella = FuPoFX.FindUmbrella(ctx.Owner);
            if (umbrella == null) {
                return;
            }
            state.TimerB = (int)Main.GameUpdateCount;
            InnoVault.PRT.PRTLoader.NewParticle<PRT_FuPoMoon>(
                umbrella.Center - Vector2.UnitY * 58f, Vector2.Zero, InkAccent, 1f)
                ?.Configure(ctx.Owner.whoAmI, InkAccent, nameof(FuPo));
        }
    }

    /// <summary>霸符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanPo : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuPo);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霸");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "夜间命中蓄月，满月转月瀑（瀑宽 x1.5+月痕爆）；白昼无效");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "霸者，月始生之白。写符的人在无月的夜里补了一轮，自新月一笔笔养到满月");
            this.GetLocalization("Power",
                () => "「月魄」夜间伞顶悬墨月，随命中渐盈；满月时下一次墨瀑化月瀑：瀑宽 x1.5，瀑击附月痕爆（35% 溅伤）");
            this.GetLocalization("Burden",
                () => "白昼符力尽敛，此符唯夜有效。月不与日争辉");
            base.SetDefaults();
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }
    }
}
