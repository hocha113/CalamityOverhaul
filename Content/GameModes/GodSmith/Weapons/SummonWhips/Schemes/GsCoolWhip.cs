using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 酷鞭「冰上圆舞」：14f 基准窗；四层转印。<br/>
    /// 深化 = 踩拍命中生成的追击雪花升格「冰晶使者」：体积 +30%、穿透 +1
    /// （雪花是鞭命中的子弹幕，承签自动继承节拍快照，穿透在生成端出生窗改，
    /// 体积各端按同步的 MarkData2 首帧重演）；<br/>
    /// 处决 = 冰锁绽放：1.5x 冰爆 + 0.5x 冰晶迸裂二段 + 原版霜焚。强度目标 120%
    /// </summary>
    internal class GsCoolWhip : GsWhipScheme
    {
        public override int TargetItemID => ItemID.CoolWhip;

        public override int WhipProjType => ProjectileID.CoolWhip;

        public override int BaseWindowFrames => 14;

        public override int MarkCap => 4;

        public override float DamageTweak => 1.06f;

        /// <summary>冰蓝</summary>
        public override Color MarkColor => new(120, 210, 255);

        protected override string GsDescFallback =>
            "Reforged: snowflakes born from on-beat lashes grow larger and pierce one more foe; " +
            "4 frost scars seal the mark, and the next on-beat hit blooms it " +
            "into a frost burst that inflicts Frostbite";

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 1.5f));
            int crackDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.5f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipFrostBloomProj>(), dmg, 3f, player.whoAmI, crackDmg);
        }

        /// <summary>承签出生窗（生成端）：升格雪花只动权威量（穿透带 >0 守卫）</summary>
        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (proj.type == ProjectileID.CoolWhipProj && router.MarkData2 >= 1f && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        /// <summary>雪花的每弹幕闩：体积升格只做一次</summary>
        private sealed class FlakeLocal
        {
            public bool Applied;
        }

        protected override void OnWhipProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.CoolWhipProj || router.MarkData2 < 1f) {
                return;
            }
            FlakeLocal local = router.GetOrCreateState<FlakeLocal>();
            if (!local.Applied) {
                //体积 +30% 是视觉与判定的同源缩放：各端按同步快照首帧重演，无需过线
                local.Applied = true;
                proj.scale *= 1.3f;
            }
            //冰晶使者描边：低频白晶闪
            if (!VaultUtils.isServer && Main.GameUpdateCount % 5 == 0) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -proj.velocity * 0.08f, new Color(200, 240, 255),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }
    }
}
