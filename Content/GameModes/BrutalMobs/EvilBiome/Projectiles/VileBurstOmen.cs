using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 邪液凝核:死亡定向溅射的无害预告体。怪物死亡瞬间凝聚,存续期间以三根渐亮的
    /// 指向刻线亮明溅射走向(出生即锁定,不再重瞄),期满才放出 <see cref="VileLanceProj"/> 三连。
    /// ai[0]=锁定射向 ai[1]=风味 ai[2]=出生档位;damage 携带溅矛伤害(本体永不敌对)
    /// </summary>
    internal class VileBurstOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>预告帧数(契约要求 ≥30)</summary>
        public const int HeraldFrames = 34;
        /// <summary>
        /// 三连的相邻槽位夹角:发射循环与预告刻线共用此常量,只在锁定角 ±FanSlotSpacing 三个定角出弹,
        /// 不追踪不重瞄,槽位之间的空隙即逃生走廊
        /// </summary>
        public const float FanSlotSpacing = 0.62f;
        /// <summary>溅矛基础射速;档位每 +1 加一档(只调强度)</summary>
        private const float LanceSpeedBase = 7.5f;
        private const float LanceSpeedTierStep = 1f;
        private const int LanceCount = 3;

        private float LockAngle => Projectile.ai[0];
        private int Flavor => (int)Projectile.ai[1];
        private int Tier => Math.Clamp((int)Projectile.ai[2], 1, 3);
        private int Elapsed => HeraldFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = HeraldFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体,永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            //凝聚感:外围粉尘向核心收拢(预算 ≤3 粒/帧)
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(30f, 30f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, EvilBiomeFX.DustFor(Flavor),
                    -offset * 0.06f, 130, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.35f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                        Main.rand.NextVector2Circular(3.5f, 3.5f), 110, default, 1.3f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.45f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            //定向三连:只在锁定角 ±FanSlotSpacing 三个定角出弹,槽位间走廊即缺口
            float speed = LanceSpeedBase + LanceSpeedTierStep * (Tier - 1);
            for (int slot = -(LanceCount - 1) / 2; slot <= (LanceCount - 1) / 2; slot++) {
                Vector2 vel = (LockAngle + slot * FanSlotSpacing).ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<VileLanceProj>(), Projectile.damage, 0f, Main.myPlayer,
                    Flavor, 0f, Tier);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float progress = MathHelper.Clamp(Elapsed / (float)HeraldFrames, 0f, 1f);
            //脉动随临近发射而加急
            float pulse = 1f + 0.22f * MathF.Sin(Elapsed * (0.3f + progress * 0.55f));
            Color deep = EvilBiomeFX.Deep(Flavor);
            Color bright = EvilBiomeFX.Bright(Flavor);

            //凝核本体:暗层(A>0)+亮芯(加色)
            float coreScale = (0.3f + 0.25f * progress) * pulse;
            Main.EntitySpriteDraw(tex, center, null, deep * 0.95f, 0f, origin, coreScale * 1.2f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, center, null, bright with { A = 0 } * 0.85f, 0f, origin, coreScale * 0.75f, SpriteEffects.None, 0);

            //三根指向刻线:预告即承诺,亮明每一根溅矛的走向
            for (int slot = -(LanceCount - 1) / 2; slot <= (LanceCount - 1) / 2; slot++) {
                float ang = LockAngle + slot * FanSlotSpacing;
                Vector2 dir = ang.ToRotationVector2();
                float len = 0.45f + 0.5f * progress;
                Main.EntitySpriteDraw(tex, center + dir * (30f + 26f * progress), null,
                    bright with { A = 0 } * (0.2f + 0.55f * progress),
                    ang + MathHelper.PiOver2, origin, new Vector2(0.1f, len), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
