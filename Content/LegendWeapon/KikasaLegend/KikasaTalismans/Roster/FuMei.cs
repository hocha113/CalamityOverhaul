using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霉「霉雨」（礼物序 19）：墨滴命中挂『霉蚀』叠层（上限 10，按层持续蚀伤），
    /// 带霉者死亡喷孢子雾感染周围——传一半层数并附小伤（服务端/单机权威，
    /// 纯 AddStacks+SimpleStrikeNPC 实现，不生成投射物，避开 owner 语义）；
    /// 代价是墨滴直击 -10%。<br/>
    /// 会话仓：本符不占任何会话字段（叠层全在 <see cref="KikasaTalismanStackNPC"/>）
    /// </summary>
    internal sealed class FuMei : KikasaTalismanDefinition
    {
        /// <summary>霉蚀层上限</summary>
        internal const int MoldCap = 10;

        /// <summary>霉蚀单次刷新时长（帧）</summary>
        private const int MoldTimerFrames = 360;

        /// <summary>孢子雾感染半径（px）</summary>
        private const float SporeRadius = 250f;

        public override int SortOrder => 119;

        /// <summary>霉黄绿：梅雨季长在符匣上的那种颜色</summary>
        public override Color InkAccent => new(172, 190, 96);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //代价：霉雨不打人，直击 -10%
            profile.DropDamageMul *= 0.90f;
        }

        //霉：雨盖下一团受潮之物，物上物旁生出三簇绒点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.11f, -0.30f, 0.06f, -0.24f, 0.44f, 0.26f, 0.48f),
            Dot(0.10f, -0.40f, -0.06f),
            Dot(0.08f, 0.10f, -0.12f),
            Dot(0.11f, 0.44f, 0.16f),
        ];

        //====行为====

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //滴击挂霉：只认墨滴（含墨瀑散射滴），洼/瀑/泉不长霉
            if (kind != KikasaRainSourceKind.Drop) {
                return;
            }
            KikasaTalismanStackNPC.AddStacks(npc, this, 1, MoldCap, MoldTimerFrames);
        }

        internal override void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) {
            //按层持续蚀伤：每层 3/s（lifeRegen 半血单位），跳字随层数走
            npc.lifeRegen -= 6 * stacks;
            damage = Math.Max(damage, stacks);
        }

        internal override void OnStackNPCKill(NPC npc, int stacks) {
            //死亡传播（服务端/单机权威）：周围敌人吃一半层数+一口孢子小伤。
            //写入端=服务端，SetStacks 广播给各端表现；伤害走 SimpleStrikeNPC 原版同步，
            //不生成投射物——服务端造 friendly 弹幕的 owner 语义说不清，这里不碰
            int transfer = Math.Max(1, stacks / 2);
            int burstDamage = 12 + stacks * 8;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other?.active != true || other.whoAmI == npc.whoAmI
                    || other.friendly || other.dontTakeDamage || other.lifeMax <= 5) {
                    continue;
                }
                if (Vector2.Distance(other.Center, npc.Center) > SporeRadius) {
                    continue;
                }
                KikasaTalismanStackNPC.AddStacks(other, this, transfer, MoldCap, MoldTimerFrames);
                other.SimpleStrikeNPC(burstDamage, other.direction);
            }
        }

        internal override void DrawNPCStack(SpriteBatch spriteBatch, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            FuMeiFX.DrawMoldSpots(spriteBatch, npc, stacks, screenPos, drawColor, InkAccent);
        }
    }

    /// <summary>霉符纸：礼物符不配合成配方，随礼物戏发放（获取期四）</summary>
    internal sealed class KikasaTalismanMei : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuMei);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "霉雨符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨滴挂霉蚀掉血，带霉者死后孢子感染周围；直击略轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "梅雨连下四十天，符匣里长了霉。符师没舍得擦。都是雨养出来的东西，凑合也算同门");
            this.GetLocalization("Power",
                () => "「霉雨」墨滴命中挂\"霉蚀\"（至多十层，按层持续掉血）；带霉的敌人死亡时喷出孢子雾，把一半霉层传给周围敌人并造成小伤");
            this.GetLocalization("Burden",
                () => "墨滴直击伤害 -10%");
            base.SetDefaults();
            Item.rare = ItemRarityID.Red;
        }
    }
}
