using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霹「霹雳」（礼物序 20）：每泼完一次墨瀑开 10 秒引雷窗，
    /// 窗内墨滴命中有 20% 自屏顶唤天雷直劈命中点（1.8x 雷径判定，所有者端生成），
    /// 劈点环形墨波+骤亮；代价是墨泉伤害 -20%。<br/>
    /// 会话仓语义：TimerA=引雷窗剩余帧（OnPourEnd 各端同拍写入，owner 端为权威读数）、
    /// TimerB=雷击间隔冷却（仅 owner 端消耗，防同拍连劈）
    /// </summary>
    internal sealed class FuPi : KikasaTalismanDefinition
    {
        /// <summary>引雷窗时长（帧）＝10 秒</summary>
        private const int WindowFrames = 600;

        /// <summary>窗内滴命中召雷概率＝1/5</summary>
        private const int StrikeChanceDenom = 5;

        /// <summary>两记天雷之间的最短间隔（帧）</summary>
        private const int StrikeCooldownFrames = 30;

        /// <summary>天雷伤害倍率（相对触发滴）</summary>
        private const float StrikeDamageMul = 1.8f;

        public override int SortOrder => 120;

        /// <summary>霹紫白：雷光劈开夜幕那一瞬的紫</summary>
        public override Color InkAccent => new(198, 168, 252);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //代价：雷声抢了泉涌的风头，墨泉 -20%
            profile.GeyserDamageMul *= 0.80f;
        }

        //霹：雨盖下一道贯底巨折雷，两侧各迸一粒裂点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.14f, 0.08f, -0.30f, -0.18f, 0.08f, 0.12f, 0.14f, -0.08f, 0.60f),
            Dot(0.10f, -0.38f, 0.24f),
            Dot(0.09f, 0.40f, 0.32f),
        ];

        //====行为====

        internal override void OnPourEnd(in KikasaTalismanRainContext ctx, Projectile pour) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            //开窗：各端同拍写入（本挂钩只在非服务器端派发），owner 端读数即权威
            state.TimerA = WindowFrames;
            FuPiFX.WindowOpenCue(pour.Center, InkAccent);
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //窗内滴击引雷：本挂钩仅所有者端派发，天雷弹幕生成即自然同步
            if (kind != KikasaRainSourceKind.Drop) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null || state.TimerA <= 0 || state.TimerB > 0
                || !Main.rand.NextBool(StrikeChanceDenom)) {
                return;
            }
            state.TimerB = StrikeCooldownFrames;
            Projectile.NewProjectile(source.GetSource_FromThis(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<KikasaFuPiThunderStrike>(),
                (int)(source.damage * StrikeDamageMul), source.knockBack * 2f, source.owner);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            if (state.TimerB > 0) {
                state.TimerB--;
            }
            if (state.TimerA <= 0) {
                return;
            }
            state.TimerA--;

            //窗内伞缘紫电噼啪：引雷窗可读化，纯表现各端本地（旁观端 TimerA 同拍就位）
            if (Main.dedServ || !Main.rand.NextBool(6)) {
                return;
            }
            int umbrellaType = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != umbrellaType || proj.owner != ctx.Owner.whoAmI) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_Sparkle>(
                    proj.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-6f, 8f)),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)),
                    Color.Lerp(InkAccent, Color.White, 0.4f), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(InkAccent * 0.6f, Main.rand.Next(10, 16), 0.12f, 0.7f);
                break;
            }
        }
    }

    /// <summary>霹符纸：礼物符不配合成配方，随礼物戏发放（获取期四）</summary>
    internal sealed class KikasaTalismanPi : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuPi);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霹");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "泼完墨瀑后十秒内，滴击两成引天雷直劈；墨泉略轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "那一夜雷把天劈成两半，写符的人在白光里看清了每一滴雨。符成之时，纸缘是焦的");
            this.GetLocalization("Power",
                () => "「霹雳」每泼完一次墨瀑，开启十秒引雷窗：窗内墨滴命中有 20% 唤天雷直劈命中点（180% 伤害，贯穿雷径）");
            this.GetLocalization("Burden",
                () => "墨泉伤害 -20%。雷声一响，泉涌也要让路");
            base.SetDefaults();
            Item.rare = ItemRarityID.Red;
        }
    }
}
