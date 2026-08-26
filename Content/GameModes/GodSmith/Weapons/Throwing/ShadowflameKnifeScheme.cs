using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 暗影焰刀(总控裁定补入):非消耗投掷武器,走族连投窗口轴,不吃消耗经济。
    /// 命中回速叠层(3/6/9 层攻速 1.10/1.18/1.25,9 层再 +8% 初速,均为族标准轴);
    /// 9 层进入「焰锋」:每刀的暗影焰烧满 4s,且每第 4 刀分裂一把六成伤的影刀
    /// </summary>
    internal class GsShadowflameKnife : GsThrowScheme
    {
        public override int TargetItemID => ItemID.ShadowFlameKnife;
        protected override string GsDescFallback =>
            "Reforged: hits feed a combo that quickens your arm, up to 25% faster at 9 stacks\nAt full combo the blade enters Flame Edge: shadowflame burns a full 4s and every 4th knife splits off a shadow clone";

        //非消耗:回收经济全零,只走连投轴
        protected override float DamageMul => 1.06f;
        protected override Color ComboGlowColor => ShadowPurple;

        /// <summary>MarkData 焰锋出手码;MarkData2 影刀码</summary>
        private const float EdgeCode = 1f;
        private const float CloneCode = 1f;

        internal static readonly Color ShadowPurple = new(168, 92, 232);
        internal static readonly Color ShadowDeep = new(96, 44, 150);

        //焰锋分刀计数与影刀生成标(myPlayer 契约)
        private int edgeThrowCount;
        private bool pendingClone;

        protected override bool? GsThrowShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.GetModPlayer<GsThrowPlayer>().ComboFor(item.type) >= 9) {
                //焰锋:每第 4 刀分裂影刀
                if (++edgeThrowCount % 4 == 0) {
                    pendingClone = true;
                    Vector2 v = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextBool() ? 10f : -10f));
                    Projectile.NewProjectile(source, position, v, type,
                        (int)(damage * 0.6f), knockback * 0.5f, player.whoAmI);
                    pendingClone = false;
                }
            }
            else {
                edgeThrowCount = 0;
            }
            return null;
        }

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            if (pendingClone) {
                //影刀:不叠层不参与经济,远端按码画紫影
                st.IsPrimary = false;
                router.MarkData2 = CloneCode;
                return;
            }
            //焰锋态出手打码,各端按码渲染紫焰尾
            if (Main.player[proj.owner].GetModPlayer<GsThrowPlayer>().ComboFor(TargetItemID) >= 9) {
                router.MarkData = EdgeCode;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || (router.MarkData != EdgeCode && router.MarkData2 != CloneCode)) {
                return;
            }
            //焰锋/影刀:紫焰尾迹
            Lighting.AddLight(proj.Center, ShadowPurple.ToVector3() * 0.22f);
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.35f,
                    -proj.velocity * 0.05f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    Main.rand.NextBool() ? ShadowPurple : ShadowDeep,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, 12);
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || target.friendly) {
                return;
            }
            //焰锋:暗影焰烧满 4s(原版随机短时,AddBuff 取长者,自动同步)
            if (router.MarkData == EdgeCode || router.MarkData2 == CloneCode) {
                target.AddBuff(BuffID.ShadowFlame, 240);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (router.MarkData2 != CloneCode) {
                return null;
            }
            //影刀:半透明本体 + 紫色加色重影(identity 定相)
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            Vector2 pos = proj.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.77f);
            Color glow = ShadowPurple * (0.5f * pulse);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, glow, proj.rotation, origin, 1.2f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, lightColor * 0.55f, proj.rotation, origin, proj.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
