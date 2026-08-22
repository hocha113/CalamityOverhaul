using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDeerclops
{
    /// <summary>
    /// 鬼奴鹿角怪的冰血雹：吼声掀落的一阵重力弹。冻成灰蓝的血珠自天而坠，
    /// 拖一条冷丝尾迹；砸物碎成冻珠与一缕暖血，落湖被水收走、荡出一圈涟漪
    /// 一阵雹子连着落水便是成串涟漪。spawn 参数完整（位置/初速/伤害皆在包里）
    /// </summary>
    internal class KikasaDeerclopsHail : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GravityDelay = 6;

        private ref float Life => ref Projectile.ai[0];

        //被湖收走：谢幕换涟漪，不走碎裂
        private bool lakeSwallowed;
        //钻出出生岩层后才武装手动撞地检测
        private bool collisionArmed;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //砸地碎裂改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Life++;

            //出生点在高空，可能嵌在洞顶岩层里：钻出实体后才武装撞地检测
            if (!collisionArmed && Life > 10
                && !Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                collisionArmed = true;
            }

            //重力雹：短暂滞空后被重量拽下
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 12.5f);
            }
            Projectile.velocity.X *= 0.999f;

            //冷丝尾迹：稀疏的小冻珠往后撒
            if (!Main.dedServ && Life % 4 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(3f, 3f),
                    Projectile.velocity * 0.15f,
                    KikasaDeerclopsServant.FrostBright * 0.35f,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(8, 14), 0f);
            }

            float glow = 0.3f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.12f * glow, 0.18f * glow, 0.2f * glow);

            //落湖：湖收回自己的水，一圈涟漪作数，成串的雹子就是成串的涟漪
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY - 2f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.55f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 2);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
                return;
            }

            //砸地碎裂（机制身份保留）：手动地形检测替代 tileCollide
            //湖线以下的真地形被湖面盖住，撞上去像凭空截停，交给上面的落湖收走
            if (collisionArmed
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            //砸碎：冻珠半球迸开 + 一缕暖血 + 一口寒气
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = normal.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(1.6f, 4.4f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.Center, vel,
                    Main.rand.NextBool(4)
                        ? KikasaDeerclopsServant.WoundBlood
                        : KikasaDeerclopsServant.FrostMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                normal * 0.4f, KikasaDeerclopsServant.FrostMist * 0.6f,
                Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(26, 40));
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                KikasaDeerclopsServant.FrostBright, 0.05f)
                ?.Configure(new Vector2(0.7f, 1f), normal.ToRotation(), 0.16f, 7);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit5 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 5 }, target.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.1f, 0.8f);
            float rot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //冷丝残影：短尾三段
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k -= 2) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(tex, oldCenter - Main.screenPosition, null,
                    KikasaDeerclopsServant.FrostMain * (0.18f * fall * fade), rot, origin,
                    new Vector2(0.16f, 0.3f + stretch * 0.5f), SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //暗缘压边→冻蓝主体→亮芯湿光，速度拉伸读出坠感
            sb.Draw(tex, pos, null, KikasaDeerclopsServant.FrostDark * (0.8f * fade), rot, origin,
                new Vector2(0.3f, 0.36f + stretch * 0.7f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, KikasaDeerclopsServant.FrostMain * fade, rot, origin,
                new Vector2(0.22f, 0.28f + stretch * 0.6f), SpriteEffects.None, 0f);
            Color core = KikasaDeerclopsServant.FrostBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.55f * fade), rot, origin,
                new Vector2(0.09f, 0.14f + stretch * 0.25f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
