using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 沛「倾盆」（合成三符，SortOrder 2）：蓄墨 x1.35、泉伤 x1.20、雨拍 x1.08；
    /// 新增「盆满则倾」：倒撑蓄满时碗沿溢珠+低鸣（盆满可听可见），此刻开泼即「开闸」——
    /// 满蓄墨瀑打沛标（先到先得，经瀑 ai[1] 量化同步），瀑宽 x1.15（各端首帧写旋钮），
    /// 开闸拍在瀑源炸一记 0.8x 瀑伤冲击（仅所有者端生成，击退顺倾向）+白沫喷环+短震屏（各端）。
    /// 通道所有权：倒撑蓄墨演出 + 瀑源开闸瞬间（泉的雷化归霆、瀑的月化归霸，互不越界）。<br/>
    /// 会话仓语义：CounterA=盆满演出状态位（0/1，各端本地边沿；State/StateTimer 走原版
    /// ai 同步，旁观端读数近似同拍）、TimerA=低鸣节流（各端本地自减）
    /// </summary>
    internal sealed class FuPei : KikasaTalismanDefinition
    {
        /// <summary>满蓄判读阈（Fill 打包量化到 0.001，留一格余量）</summary>
        private const float FullFillThreshold = 0.999f;

        /// <summary>满蓄瀑宽倍率</summary>
        private const float GateWidthMul = 1.15f;

        /// <summary>开闸拍伤害 = 瀑伤 x 此倍率</summary>
        private const float GateBurstMul = 0.8f;

        /// <summary>开闸拍判定半径（px）</summary>
        private const float GateBurstRadius = 90f;

        /// <summary>低鸣节拍（帧）</summary>
        private const int HumCadenceFrames = 42;

        public override int SortOrder => 2;

        public override Color InkAccent => new(208, 122, 92);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.ChargeRateMul *= 1.35f;
            profile.GeyserDamageMul *= 1.20f;
            profile.RainTempoMul *= 1.08f;
        }

        //沛：雨盖下一道微斜粗注贯底，旁一缕细流，
        //落点双溅一长一陡（倾出来的水不对称），飞沫两点一大一小
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.13f, -0.44f, 0.50f),
            L(0.20f, 0.02f, -0.30f, -0.04f, 0.50f),
            L(0.06f, 0.16f, -0.16f, 0.20f, 0.18f),
            L(0.10f, -0.04f, 0.50f, -0.44f, 0.74f),
            L(0.09f, -0.04f, 0.50f, 0.30f, 0.64f),
            Dot(0.12f, -0.56f, 0.46f),
            Dot(0.09f, 0.46f, 0.34f),
        ];

        //====行为====

        internal override void ModifyPourSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaPourSpawnContext pour) {
            //满蓄瀑打沛标（先到先得，与霸的月标同规竞标）：开闸材质与冲击都认这枚标
            if (pour.Fill < FullFillThreshold || pour.TagId != 0) {
                return;
            }
            pour.TagId = KikasaTalismanHooks.TagIdFor(this);
        }

        internal override void OnPourStart(in KikasaTalismanRainContext ctx, Projectile pour) {
            if (pour.ModProjectile is not KikasaInkPour inkPour
                || inkPour.TalismanTag != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //开闸材质旋钮：各端（含服务器）首帧一次性写，瀑更宽
            inkPour.TalismanWidthMul = GateWidthMul;
            //开闸演出各端本地；冲击判定只在所有者端生成（生成包自然同步）
            FuPeiFX.GateOpenBurst(pour, InkAccent);
            if (ctx.IsOwnerClient) {
                int damage = System.Math.Max((int)(pour.damage * GateBurstMul), 1);
                Projectile.NewProjectile(pour.GetSource_FromThis(), pour.Center, Vector2.Zero,
                    ModContent.ProjectileType<FuPeiGateBurst>(), damage, pour.knockBack,
                    ctx.Owner.whoAmI, GateBurstRadius, pour.ai[0]);
            }
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            //盆满读数纯表现：服务器不参与
            if (Main.dedServ) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            if (state == null) {
                return;
            }
            if (state.TimerA > 0) {
                state.TimerA--;
            }
            KikasaRainUmbrella umbrella = KikasaRainUmbrella.FindFor(ctx.Owner.whoAmI);
            float fill = umbrella?.FlipChargeFill ?? 0f;
            if (fill < FullFillThreshold) {
                state.CounterA = 0;
                return;
            }
            Projectile proj = umbrella.Projectile;
            //盆满边沿：碗沿荡一圈溢珠+一记沉鸣，读作「满了，可以倒了」
            if (state.CounterA == 0) {
                state.CounterA = 1;
                FuPeiFX.BowlFullCue(proj, InkAccent);
            }
            //满蓄持续溢沿：碗口渗珠外滚+低鸣节拍
            FuPeiFX.BowlBrim(proj, InkAccent);
            if (state.TimerA <= 0) {
                state.TimerA = HumCadenceFrames;
                KikasaInk.Play(KikasaInk.InkSplash, proj.Center, 0.26f, -0.8f, 2);
            }
        }
    }

    /// <summary>沛符纸：合成三符（近水工作台），非礼物符</summary>
    internal sealed class KikasaTalismanPei : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuPei);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "倾盆符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "倒撑蓄墨更快，盆满则倾、开泼如开闸；常雨略缓");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "求雨最忌不痛不痒。这张符只应一件事：要下，就下个倾盆");
            this.GetLocalization("Power",
                () => "「倾盆」倒撑蓄墨速率 +35%，墨泉伤害 +20%；蓄满时碗沿溢珠低鸣，此刻开泼即为开闸：瀑源炸开一记冲击（80% 瀑伤），墨瀑更宽");
            this.GetLocalization("Burden",
                () => "墨雨节拍放缓 8%");
            base.SetDefaults();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.RainCloud, 8)
                .AddIngredient(ItemID.Silk, 2)
                .AddIngredient(ItemID.BlackInk, 1)
                .AddIngredient(ItemID.FallenStar, 1)
                .AddTile(TileID.WorkBenches)
                .AddCondition(Condition.NearWater)
                .Register();
        }
    }
}
