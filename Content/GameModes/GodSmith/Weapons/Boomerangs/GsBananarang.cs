using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 香蕉镖重铸。材质：熟透的弯月蕉。签名行为：①同场可掷十只，一掷之内每次命中让这只香蕉更熟，
    /// 每层加伤 6% 至多五层 ②越熟镖体越透出焦金斑纹 ③右键全体齐冲光标
    /// </summary>
    internal class GsBananarang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Bananarang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsBananarangProj>();

        internal override int MaxAirborne => 10;   //与原版十只上限对齐

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Throw up to ten; every hit ripens that banana, 6% bonus damage per stage, five stages max\n" +
            "Right click: every airborne banana dashes toward your cursor at once";
    }

    /// <summary>弯月蕉镖体：熟成连击</summary>
    internal class GsBananarangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Bananarang;

        protected override Color GlowColor => new(250, 220, 90);

        protected override Color TrailColor => new(240, 200, 80);

        protected override bool HoverOnFirstHit => false;   //连击镖，命中不滞空直接穿场

        /// <summary>本掷熟成层数（owner 判定端本地量）</summary>
        private int ripeness;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (ripeness > 0) {
                modifiers.FinalDamage *= 1f + (0.06f * ripeness);
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone)
            => ripeness = Math.Min(5, ripeness + 1);

        protected override void PostDrawLayers(SpriteBatch sb, Vector2 drawPos, Color lightColor) {
            //熟成读数：焦金斑纹辉光随层数增亮偏棕
            if (ripeness <= 0) {
                return;
            }
            Main.instance.LoadItem(SourceItemID);
            Texture2D tex = TextureAssets.Item[SourceItemID].Value;
            float k = ripeness / 5f;
            Color ripe = Color.Lerp(new Color(255, 200, 90), new Color(180, 110, 40), k) * (0.25f + (0.3f * k));
            ripe.A = 0;
            sb.Draw(tex, drawPos, null, ripe, Projectile.rotation, tex.Size() / 2f,
                Projectile.scale * 1.03f, SpriteEffects.None, 0);
        }
    }
}
