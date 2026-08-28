using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 三重回旋镖重铸。材质：三合板复合镖。签名行为：①同场至多三镖，每多一镖在空中全体加伤 8%
    /// ②右键一声令下全队齐冲光标 ③编队就绪时镖体上亮起一到三枚编队标记
    /// </summary>
    internal class GsTrimarang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Trimarang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsTrimarangProj>();

        internal override int MaxAirborne => 3;   //与原版三镖上限对齐

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Up to three boomerangs airborne; each extra one in flight grants all of them 8% damage\n" +
            "Right click: the whole squad dashes toward your cursor at once";
    }

    /// <summary>复合镖体：三相编队</summary>
    internal class GsTrimarangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Trimarang;

        protected override Color GlowColor => new(215, 185, 115);

        protected override Color TrailColor => new(190, 160, 100);

        /// <summary>同场镖数（含自身），判定端与绘制端各自直读 ownedProjectileCounts</summary>
        private int SquadCount => Math.Clamp(Owner.ownedProjectileCounts[Type], 1, 3);

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int extra = SquadCount - 1;
            if (extra > 0) {
                modifiers.FinalDamage *= 1f + (0.08f * extra);
            }
        }

        protected override void OnCommandFX(Player owner) {
            //齐冲下令：短促编队光爆
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GlowColor, 0.35f)?.Configure(8, 0.85f);
            }
        }

        protected override void PostDrawLayers(SpriteBatch sb, Vector2 drawPos, Color lightColor) {
            //编队标记：镖体上方按同场镖数亮起一到三枚小星（whoAmI 种子微闪，不掷 Main.rand）
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return;
            }
            int count = SquadCount;
            float tw = 0.8f + (0.2f * MathF.Sin((Main.GlobalTimeWrappedHourly * 6f) + Projectile.whoAmI));
            for (int i = 0; i < count; i++) {
                Vector2 off = new((i - ((count - 1) * 0.5f)) * 10f, -24f);
                Color c = GlowColor * (0.55f * tw);
                c.A = 0;
                sb.Draw(star, drawPos + off, null, c, 0f, star.Size() / 2f, 0.035f, SpriteEffects.None, 0);
            }
        }
    }
}
