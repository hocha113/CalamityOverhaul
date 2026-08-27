using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 裹布卷:木乃伊缠掷的实弹。由 <see cref="MummyWrapOmen"/> 在提交帧掷出,
    /// 沿锁定直线飞行,自旋读作滚卷,身后铺展同材质布条拖尾;命中挂原版缓速(2/2.5/3 秒)。
    /// 弹体走 M5 双层配方:亚麻暗外壳(A&gt;0)承担遮挡+亮芯加色+风味点睛芯。
    /// ai[0]=风味 ai[1]=出生档位
    /// </summary>
    internal class MummyWrapProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>飞行帧数(射程=速度×此值,预告标记线长与之对齐)</summary>
        internal const int FlightFrames = 52;
        /// <summary>淡入帧数,淡入期无判定(伤害窗口=可见窗口)</summary>
        private const int FadeInFrames = 10;
        /// <summary>缓速时长:档位 1/2/3 → 2/2.5/3 秒(只调强度)</summary>
        private const int SlowBaseTicks = 120;
        private const int SlowTierStep = 30;
        /// <summary>亚麻裹布双层配色(暗层 A&gt;0 承担实体感,亮层加色敷料)</summary>
        internal static readonly Color LinenDeep = new(88, 76, 54);
        internal static readonly Color LinenBright = new(226, 208, 164);

        /// <summary>裹布速度按档位(只调强度,机制形状不变)</summary>
        internal static float SpeedFor(int tier) => 8.2f + 0.8f * (Math.Clamp(tier, 1, 3) - 1);

        private int Flavor => (int)Projectile.ai[0];
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = FlightFrames;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            //出膛淡入(可见度与判定同一时间轴)
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));
            //自旋读作滚卷展布
            Projectile.rotation += 0.32f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                    -Projectile.velocity * 0.1f, 150, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.15f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //受击端本机结算:原版缓速(接触汲取另有 OnHitPlayer 路径,互不重复挂)
            target.AddBuff(BuffID.Slow, SlowBaseTicks + SlowTierStep * (Tier - 1));
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //布屑四散
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 130, default, 1f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;
            float travelRot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //展开的布条拖尾:同材质暗层沿路径铺条(最新段横轴 ≥0.5 倍体宽),近段叠亮芯
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 stripScale = new Vector2(0.24f, 0.4f) * (0.6f + 0.4f * t);
                Main.EntitySpriteDraw(tex, oldDrawPos, null, LinenDeep * (0.4f * t * opacity),
                    travelRot, origin, stripScale, SpriteEffects.None, 0);
                if (i <= 3) {
                    Main.EntitySpriteDraw(tex, oldDrawPos, null, LinenBright with { A = 0 } * (0.25f * t * opacity),
                        travelRot, origin, stripScale * 0.65f, SpriteEffects.None, 0);
                }
            }

            //弹体:暗外壳(A>0,承担遮挡)+亮芯(加色)+风味点睛芯
            Vector2 bodyScale = new(0.36f, 0.3f);
            Main.EntitySpriteDraw(tex, pos, null, LinenDeep * (0.95f * opacity),
                Projectile.rotation, origin, bodyScale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, LinenBright with { A = 0 } * (0.8f * opacity),
                Projectile.rotation, origin, bodyScale * 0.75f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, EvilBiomeFX.Bright(Flavor) with { A = 0 } * (0.4f * opacity),
                Projectile.rotation, origin, bodyScale * 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
