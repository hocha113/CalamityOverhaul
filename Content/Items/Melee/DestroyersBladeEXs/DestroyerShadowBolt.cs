using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 黑色影子弹幕:吸光暗体拖着红缘游动,蛇形游走缓慢咬向目标。
    /// ai[0]=初始化闩 ai[1]=歼灭协议(1 时追踪与速度强化) ai[2]=游走相位
    /// </summary>
    internal class DestroyerShadowBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> ShadowTex = null;

        private bool Empowered => Projectile.ai[1] > 0f;
        private ref float Init => ref Projectile.ai[0];
        private ref float WavePhase => ref Projectile.ai[2];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 240;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Init == 0) {
                //出膛不响,声音留给挥砍拍
                Init = 1;
                WavePhase = Projectile.identity * 1.37f % MathHelper.TwoPi;
                if (Empowered) {
                    Projectile.penetrate = 2;
                    Projectile.scale = 1.15f;
                }
            }

            //追踪:常态缓咬,歼灭协议死咬,速度随飞行缓慢爬升
            float steer = Empowered ? 0.055f : 0.02f;
            float range = Empowered ? 800f : 520f;
            int target = FindTarget(range);
            if (target >= 0) {
                float speed = Projectile.velocity.Length();
                speed = MathF.Min(speed + (Empowered ? 0.06f : 0.025f), Empowered ? 16f : 12.5f);
                Vector2 want = (Main.npc[target].Center - Projectile.Center)
                    .SafeNormalize(Vector2.Zero) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, steer);
            }

            //蛇形游走:侧向加速度摆动航向后归一回原速,身体真转向而不是贴图横移
            WavePhase += 0.23f;
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
            float keepSpeed = Projectile.velocity.Length();
            Projectile.velocity += side * (MathF.Cos(WavePhase) * 0.45f);
            Projectile.velocity = Projectile.velocity.SafeNormalize(forward) * keepSpeed;

            Projectile.rotation = Projectile.velocity.ToRotation();

            //尾迹排烟:小口黑烟撒在本帧掠过的路径上,出生带弹体冲量、几帧内泄劲悬停
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 at = Projectile.Center - Projectile.velocity * Main.rand.NextFloat(1.2f)
                    + side * Main.rand.NextFloat(-4f, 4f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(at,
                    Projectile.velocity * 0.16f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    new Color(14, 3, 6) * 0.9f,
                    Main.rand.NextFloat(0.13f, 0.22f))?.Configure(Main.rand.Next(16, 26), 0.005f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.25f, 0.03f, 0.03f));
        }

        private int FindTarget(float range) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (!VaultUtils.isServer) {
                //同帧多发齐灭只留一声闷响
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.26f, Pitch = -0.5f, MaxInstances = 2, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew }, Projectile.Center);
            }
            //暗爆:小口黑烟带径向冲量崩出,强阻力下泄劲悬停再散
            for (int i = 0; i < 6; i++) {
                Vector2 burst = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(Projectile.Center + burst * 2f, burst,
                    new Color(16, 4, 6) * 0.9f, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(18, 30), 0.008f);
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f),
                    new Color(255, 45, 30), Main.rand.NextFloat(0.8f, 1.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ShadowTex.Value;
            Vector2 origin = tex.Size() / 2f;
            float speed = Projectile.velocity.Length();
            //速度拉伸:越快越长越扁,读作高速掠影
            Vector2 stretch = new(1f + speed * 0.05f, 0.62f);

            //拖影:沿旧航向的暗色纺锤逐级缩小收细,是尾迹不是烟堆
            if (Projectile.oldPos != null) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, pos, null, new Color(8, 2, 4) * (0.55f * t * t),
                        Projectile.oldRot[i], origin, Projectile.scale * (0.16f + 0.28f * t) * stretch, SpriteEffects.None, 0);
                }
            }

            //本体:红缘勾边 + 吸光暗核,紧贴30px判定体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color rim = new Color(255, 35, 25) * 0.6f;
            rim.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, rim, Projectile.rotation, origin,
                Projectile.scale * 0.6f * stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(6, 1, 3) * 0.96f, Projectile.rotation, origin,
                Projectile.scale * 0.5f * stretch, SpriteEffects.None, 0);
            //核心里一点红灯,读作影里的机械眼
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color eye = new Color(255, 40, 30) * 0.8f;
            eye.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, eye, 0f, glow.Size() / 2f,
                0.15f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
