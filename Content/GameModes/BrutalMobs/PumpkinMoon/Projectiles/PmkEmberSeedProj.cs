using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 火种抛体（投火组稻草人掷出）：ai[0]=滞空帧 ai[1]=落地燃烧存续帧 ai[2]=引燃延长帧。
    /// 发射端定帧弹道解算（重力与本类严格同源），寿命尽即在承诺落点亲手放置地面祭火种
    /// （火种自带 ≥30 帧无害引燃期=踩灭窗，公平性由火种承载）；本体飞行全程无害（CanDamage=false）。
    /// 高抛过顶语义故穿地形；伤害值随生成包同步、由落地火种继承
    /// </summary>
    internal class PmkEmberSeedProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>弹道重力（每帧），与发射端解算严格同源</summary>
        internal const float SeedGravity = 0.24f;

        private static readonly Color SeedWarm = new Color(255, 150, 48);

        private int Flight => Math.Max((int)Projectile.ai[0], 10);
        private int LitFrames => (int)Projectile.ai[1];
        private int KindleExtra => Math.Max(0, (int)Projectile.ai[2]);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        /// <summary>飞行全程无害，伤害全部由落地火种的燃烧期承载</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //寿命由已同步的 ai[0] 各端确定性展开
                Projectile.timeLeft = Flight;
            }

            //定帧弹道：每帧先加速后位移，与发射端解算严格同构
            Projectile.velocity.Y += SeedGravity;
            Projectile.rotation += 0.18f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (Main.dedServ) {
                return;
            }
            if (Main.rand.NextBool(3)) {
                Dust flame = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.4f, 0.4f), 110, default, 0.95f);
                flame.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, SeedWarm.ToVector3() * 0.3f);
        }

        public override void OnKill(int timeLeft) {
            //寿命尽=抵达承诺落点：权威端放置地面祭火种（全局限额与寻地由公共入口把关）
            if (!VaultUtils.isClient) {
                PumpkinMoonNPC.SpawnEmberAt(Projectile.GetSource_FromAI(), Projectile.Center,
                    Projectile.damage, LitFrames, KindleExtra * 10);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(1.6f, 1.2f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //本体与拖尾同用原版南瓜贴图（实体层，有遮挡像素）
            Main.instance.LoadItem(ItemID.Pumpkin);
            Texture2D tex = TextureAssets.Item[ItemID.Pumpkin].Value;
            Vector2 origin = tex.Size() / 2f;

            //同材质拖尾：旧位重画（横轴 ≥ 弹体 0.75）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trail = Color.Lerp(lightColor, SeedWarm, 0.5f) * (0.4f * t);
                Main.EntitySpriteDraw(tex, oldDrawPos, null, trail, Projectile.rotation - i * 0.18f,
                    origin, 0.46f + 0.14f * t, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color body = Color.Lerp(lightColor, SeedWarm, 0.3f);
            Main.EntitySpriteDraw(tex, drawPos, null, body, Projectile.rotation, origin, 0.6f, SpriteEffects.None, 0);
            //火光敷料（加色只做辉光）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, (SeedWarm with { A = 0 }) * 0.4f, 0f,
                glow.Size() / 2f, 0.32f, SpriteEffects.None, 0);
            return false;
        }
    }
}
