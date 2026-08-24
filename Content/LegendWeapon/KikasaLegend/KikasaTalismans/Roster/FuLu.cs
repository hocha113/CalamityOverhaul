using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 露「朝露」（礼物序 02）：墨滴命中有 12% 在命中点凝出露珠，
    /// 拾取回复 2 点生命（场上至多 3 颗）；代价是雨拍 +6%。<br/>
    /// 露珠生成与治疗都只在归属端做，本符不占会话仓
    /// </summary>
    internal sealed class FuLu : KikasaTalismanDefinition
    {
        /// <summary>凝露概率</summary>
        private const float DewChance = 0.12f;

        /// <summary>场上同存露珠上限</summary>
        private const int DewFieldCap = 3;

        /// <summary>拾取回复量</summary>
        internal const int DewHealHp = 2;

        /// <summary>雨拍代价</summary>
        private const float TempoPenalty = 1.06f;

        public override int SortOrder => 102;

        /// <summary>晨青白：天将亮时草叶上的那点凉</summary>
        public override Color InkAccent => FuLuFX.Accent;

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.RainTempoMul *= TempoPenalty;
        }

        //露：雨盖下垂一条弯茎，茎梢托一颗圆露，露心一点晨光
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.09f, -0.06f, -0.30f, -0.02f, 0.02f, 0.12f, 0.22f),
            Arc(0.08f, 0.18f, 0.42f, 0.16f, 0f, MathHelper.TwoPi, 12),
            Dot(0.08f, 0.14f, 0.38f),
        ];

        //====行为====

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //只认滴击：露是雨点凝的。命中挂钩只在归属端派发，掷骰即权威
            if (kind != KikasaRainSourceKind.Drop || Main.rand.NextFloat() >= DewChance) {
                return;
            }
            //场上限自查：同型同主计数，超编不再凝
            int dewType = ModContent.ProjectileType<FuLuDewDrop>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == dewType && proj.owner == ctx.Owner.whoAmI) {
                    count++;
                }
            }
            if (count >= DewFieldCap) {
                return;
            }
            //命中点抛物弹出，落地静候拾取
            Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(4.2f, 6.2f));
            Projectile.NewProjectile(source.GetSource_FromThis(), source.Center, vel,
                dewType, 0, 0f, ctx.Owner.whoAmI);
        }
    }

    /// <summary>露符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanLu : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuLu);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·露");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨滴命中偶凝露珠，拾取回复少量生命；雨拍稍缓");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "天将亮未亮，草叶把整夜的雨攒成一颗露。拾起它的人，掌心会先凉一下，再暖起来");
            this.GetLocalization("Power",
                () => "「朝露」墨滴命中有 12% 凝出露珠，拾取回复 2 点生命；场上至多三颗");
            this.GetLocalization("Burden",
                () => "墨雨节拍放缓 6%。凝露急不得，雨也只好慢些");
            base.SetDefaults();
        }
    }
}
