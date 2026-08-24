using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 洇「洇痕」（礼物序 01，教程期）：墨滴命中留洇痕（至多 5 层，逐层微蚀），
    /// 满层在命中处洇开墨花爆伤并清层；代价是直击 -8%。<br/>
    /// 叠层记在 <see cref="KikasaTalismanStackNPC"/>（Kind 自动取符网络 id），本符不占会话仓
    /// </summary>
    internal sealed class FuYin : KikasaTalismanDefinition
    {
        /// <summary>洇痕层数上限</summary>
        private const int StackCap = 5;

        /// <summary>洇痕留存帧数（每次叠层刷新）</summary>
        internal const int StackLifeFrames = 240;

        /// <summary>每层微蚀强度（lifeRegen 计 4 = 每秒 2 生命）</summary>
        private const int RegenLossPerStack = 4;

        /// <summary>墨花爆伤 = 引爆滴伤害 × 此倍率</summary>
        private const float BurstDamageMul = 1.6f;

        /// <summary>直击代价</summary>
        private const float DropDamagePenalty = 0.92f;

        public override int SortOrder => 101;

        /// <summary>墨紫：宣纸上洇开的紫墨</summary>
        public override Color InkAccent => FuYinFX.Accent;

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.DropDamageMul *= DropDamagePenalty;
        }

        //洇：雨盖下一点重墨，晕成一圈环痕，缘外再溅一星——收不回来的那种洇
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.09f, 0.00f, 0.12f, 0.30f, -1.10f, 4.60f, 14),
            Dot(0.16f, 0.00f, 0.12f),
            Dot(0.07f, 0.42f, 0.34f),
        ];

        //====行为====

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //滴击留洇痕：瀑/泉/洼不沾墨，痕是雨点一滴一滴打上去的
            if (kind != KikasaRainSourceKind.Drop || npc?.active != true || npc.friendly) {
                return;
            }
            int stacks = KikasaTalismanStackNPC.AddStacks(npc, this, 1, StackCap, StackLifeFrames);
            if (stacks < StackCap) {
                return;
            }
            //满层洇开：清层并在命中处起墨花。命中挂钩只在归属端派发，
            //爆炸判定随生成包自然同步，旁观端由墨花弹幕自己演
            KikasaTalismanStackNPC.ClearStacks(npc, this);
            Projectile.NewProjectile(source.GetSource_FromThis(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FuYinInkBurst>(),
                (int)(source.damage * BurstDamageMul), 3f, ctx.Owner.whoAmI);
        }

        internal override void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) {
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            npc.lifeRegen -= stacks * RegenLossPerStack;
            int tick = stacks * RegenLossPerStack / 2;
            if (damage < tick) {
                damage = tick;
            }
        }

        internal override void DrawNPCStack(SpriteBatch spriteBatch, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            FuYinFX.DrawStains(spriteBatch, npc, stacks, timerFrames, screenPos, drawColor);
        }
    }

    /// <summary>洇符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanYin : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuYin);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "墨花符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨滴命中叠墨痕，叠满五层绽开墨花爆伤；直击略轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "墨滴落在湿纸上，不去擦它，自己会晕成一朵花。符师说敌人也一样：多打几滴，就开花");
            this.GetLocalization("Power",
                () => "「墨花」墨滴命中留下墨痕（至多五层，每层持续掉血）；叠满五层绽开墨花，造成 160% 滴伤并清空层数");
            this.GetLocalization("Burden",
                () => "墨滴直击伤害 -8%");
            base.SetDefaults();
        }
    }
}
