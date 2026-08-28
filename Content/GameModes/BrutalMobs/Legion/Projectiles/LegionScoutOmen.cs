using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 斥候瞄准线：拉距点射的直线预告，镜像 <see cref="LegionVolleyOmen"/> 的写法但独立小型化
    /// （更短的幽灵线 + 冷钢青色调 + 单发）。velocity=锁定瞄向（生成即承诺，不重瞄），
    /// ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，斥候死亡或槽位复用即取消发射） ai[1]=短矢伤害。
    /// 倒数结束由权威端放出 <see cref="LegionScoutBolt"/>，全程无判定
    /// </summary>
    internal class LegionScoutOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowHostile;

        /// <summary>预告帧数（公平底线 ≥30，档位一律不缩短）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>瞄准线幽灵矢数量（比弓手齐射的 5 枚短，读作点射而非箭幕）</summary>
        private const int GhostCount = 3;
        /// <summary>幽灵矢间距（像素）</summary>
        private const float GhostSpacing = 30f;

        private int BoltDamage => (int)Projectile.ai[1];
        private int Elapsed => TelegraphFrames - Projectile.timeLeft;
        private float Charge => MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames;
            Projectile.netImportant = true;
        }

        /// <summary>预告体锚定原地，velocity 只作方向载体</summary>
        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告，永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //来源校验：斥候死亡则取消发射（击杀施法者=有效反制）；类型比对防槽位复用
            if (!Cancelled) {
                int packed = (int)Projectile.ai[0];
                int src = (packed & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != packed >> 8) {
                    Cancelled = true;
                }
            }

            //瞄准冷光沿线爬行（≤1 粒/帧）
            if (!Cancelled && !Main.dedServ && Main.rand.NextBool(2)) {
                float reach = Main.rand.NextFloat(14f, 24f + 70f * Charge);
                Dust glint = Dust.NewDustPerfect(Projectile.Center + Projectile.velocity * reach,
                    DustID.SilverCoin, Projectile.velocity * 0.6f, 130, default, 0.7f);
                glint.noGravity = true;
            }

            if (Projectile.timeLeft == 1 && !Cancelled && !VaultUtils.isClient) {
                //倒数结束：权威端放矢，沿锁定方向出膛（不重瞄）；
                //出弦声锚定在短矢实体首帧，不挂本地倒计时（镜像弓手齐射的吞音教训）
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                    Projectile.velocity.SafeNormalize(Vector2.UnitX) * LegionScoutBolt.BoltSpeed,
                    ModContent.ProjectileType<LegionScoutBolt>(), BoltDamage, 1f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Cancelled) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float drawRot = Projectile.rotation + MathHelper.PiOver2;
            float charge = Charge;
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity);
            //末段收束闪烁，提示即将放矢
            float urgency = charge > 0.75f ? 1.3f : 1f;

            for (int i = 0; i < GhostCount; i++) {
                Vector2 pos = Projectile.Center + Projectile.velocity * (22f + i * GhostSpacing)
                    - Main.screenPosition;
                //由近及远渐次点亮
                float reveal = MathHelper.Clamp(charge * (GhostCount + 1) - i, 0f, 1f);
                if (reveal <= 0f) {
                    break;
                }
                float alpha = reveal * (0.32f + 0.45f * charge) * pulse * urgency;
                //真 alpha 本体层（有遮挡像素）+ 冷钢青描辉（与弓手齐射的琥珀线可辨区分）
                Main.EntitySpriteDraw(tex, pos, null, Color.Lerp(lightColor, Color.White, 0.3f) * alpha,
                    drawRot, orig, 0.85f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos, null, new Color(120, 210, 255, 0) * (0.5f * alpha),
                    drawRot, orig, 1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
