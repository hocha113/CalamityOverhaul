using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 族内通用一次性 AoE 判定弹幕（血蚀引爆、雨云魔棒雷雨落雷柱）。<br/>
    /// ai[0]=判定半径（px），ai[1]=样式：0 圆判定 + 原版贴图范围提示（默认）；
    /// 2 = 落雷柱（R2 保留件雨云魔棒专用）：柱形判定 + 冲击环电柱轮廓 + 出生迸溅。<br/>
    /// 出生随生成包定型，前 4 帧判定（每目标一次），其余帧只留范围提示
    /// </summary>
    internal class GsMorphBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        /// <summary>落雷柱样式索引</summary>
        internal const int StyleBoltColumn = 2;
        /// <summary>落雷柱半高（px）</summary>
        private const float BoltColumnHalfHeight = 90f;
        private static readonly Color BoltBright = new(200, 235, 255);
        private static readonly Color BoltMain = new(96, 160, 255);
        private static readonly Color BoltDeep = new(30, 48, 130);

        private const int LifeTicks = 14;
        private const int DamageWindowEnd = 10;

        private float Radius => Projectile.ai[0] <= 0f ? 60f : Projectile.ai[0];

        private bool IsBoltColumn => (int)Projectile.ai[1] == StyleBoltColumn;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Projectile.timeLeft > DamageWindowEnd ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 c = Projectile.Center;
            float nx = MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom);
            if (IsBoltColumn) {
                //柱形判定（落雷）：横向按半径、纵向按柱半高
                return MathHelper.Distance(nx, c.X) <= Radius && MathHelper.Distance(ny, c.Y) <= BoltColumnHalfHeight;
            }
            return c.DistanceSQ(new Vector2(nx, ny)) <= Radius * Radius;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
                    if (IsBoltColumn) {
                        SpawnBoltBirthVisual();
                    }
                }
            }
        }

        /// <summary>落雷柱出生迸溅（各端客户端；≤10 粒，纵向甩出）</summary>
        private void SpawnBoltBirthVisual() {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-6f, 6f));
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + vel * 2f, vel,
                    Main.rand.NextBool() ? BoltBright : BoltMain, Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, BoltMain.ToVector3() * 0.6f);
        }

        /// <summary>范围提示：默认原版贴图按判定半径缩放画一笔（lightColor 着色），随寿命渐隐；落雷柱改画冲击环电柱轮廓</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            if (IsBoltColumn) {
                //落雷柱：纵向拉伸的窄环两枚叠出电柱轮廓（timeLeft 确定函数，各端一致）
                float t = 1f - fade;
                float r = Radius * (0.35f + 0.65f * MathHelper.Clamp(t * 2.2f, 0f, 1f));
                float alpha = 0.85f * fade;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, BoltColumnHalfHeight * (0.6f + 0.4f * t), 6f,
                    BoltBright, BoltMain, BoltDeep, alpha, squish: 0.22f, timeSeed: Projectile.identity * 0.41f);
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, r, 5f,
                    BoltBright, BoltMain, BoltDeep, alpha * 0.7f, squish: 0.5f, timeSeed: Projectile.identity * 0.77f);
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>便捷生成：折算伤害并封顶，防终局武器把小爆叠出天文数字（仅本地玩家/攻击方路径调用）</summary>
        internal static void Spawn(Projectile source, Vector2 pos, int damage, float radius)
            => Spawn(source, pos, damage, radius, 0);

        /// <summary>带样式的便捷生成（styleIdx 见类注释；R2 保留件落雷柱传 <see cref="StyleBoltColumn"/>）</summary>
        internal static void Spawn(Projectile source, Vector2 pos, int damage, float radius, int styleIdx) {
            int dmg = (int)MathHelper.Clamp(damage, 1, 1200);
            Projectile.NewProjectile(source.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<GsMorphBurstProj>(), dmg, 2f, source.owner, radius, styleIdx);
        }
    }
}
