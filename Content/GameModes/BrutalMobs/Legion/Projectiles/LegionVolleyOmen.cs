using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 军团箭令：弓手齐射的预告实体。velocity = 锁定的瞄准方向（随生成包原生同步，预告即承诺），
    /// ai[0] = 战矢伤害。全程无判定，倒数结束由权威端放出 <see cref="LegionVolleyArrow"/>
    /// </summary>
    internal class LegionVolleyOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowHostile;

        /// <summary>瞄准线幽灵箭数量</summary>
        private const int GhostCount = 5;
        /// <summary>幽灵箭间距（像素）</summary>
        private const float GhostSpacing = 34f;

        private int ArrowDamage => (int)Projectile.ai[0];
        private int Elapsed => LegionNPC.VolleyTelegraphFrames - Projectile.timeLeft;
        private float Charge => MathHelper.Clamp(Elapsed / (float)LegionNPC.VolleyTelegraphFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LegionNPC.VolleyTelegraphFrames;
            Projectile.netImportant = true;
        }

        /// <summary>预告体锚定原地，velocity 只作方向载体</summary>
        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告，永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //蓄力火星沿瞄准线爬行（≤2 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                float reach = Main.rand.NextFloat(16f, 30f + 100f * Charge);
                Dust ember = Dust.NewDustPerfect(Projectile.Center + Projectile.velocity * reach,
                    DustID.Torch, Projectile.velocity * 0.8f, 120, default, 0.9f);
                ember.noGravity = true;
            }

            if (Projectile.timeLeft == 1) {
                //倒数结束：权威端放箭，箭沿锁定方向出膛（不重瞄）。
                //释放音效锚定在战矢实体首帧，不挂本地倒计时——联机下客户端倒计时
                //与服务端击杀包同帧竞速会偶发吞音，晚加入者还会在错误时刻补播
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                        Projectile.velocity.SafeNormalize(Vector2.UnitX) * LegionNPC.VolleyArrowSpeed,
                        ModContent.ProjectileType<LegionVolleyArrow>(), ArrowDamage, 1f, Main.myPlayer);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float drawRot = Projectile.rotation + MathHelper.PiOver2;
            float charge = Charge;
            //末段收束闪烁，提示即将放箭
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity);
            float urgency = charge > 0.75f ? 1.25f : 1f;

            for (int i = 0; i < GhostCount; i++) {
                Vector2 pos = Projectile.Center + Projectile.velocity * (26f + i * GhostSpacing)
                    - Main.screenPosition;
                //由近及远渐次点亮：远端幽灵只在蓄力后段浮现
                float reveal = MathHelper.Clamp(charge * (GhostCount + 1) - i, 0f, 1f);
                if (reveal <= 0f) {
                    break;
                }
                float alpha = reveal * (0.30f + 0.45f * charge) * pulse * urgency;
                //真 alpha 本体层（有遮挡像素）+ 琥珀加色描辉
                Main.EntitySpriteDraw(tex, pos, null, Color.Lerp(lightColor, Color.White, 0.3f) * alpha,
                    drawRot, orig, 0.9f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos, null, new Color(255, 170, 70, 0) * (0.5f * alpha),
                    drawRot, orig, 1.05f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
