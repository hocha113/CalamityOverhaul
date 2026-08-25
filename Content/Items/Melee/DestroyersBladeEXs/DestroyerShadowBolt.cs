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
                Init = 1;
                WavePhase = Projectile.identity * 1.37f % MathHelper.TwoPi;
                if (Empowered) {
                    Projectile.penetrate = 2;
                    Projectile.scale = 1.15f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 5 }, Projectile.position);
                }
            }

            //蛇形游走:速度方向上叠加正弦侧摆,影子不走直线
            WavePhase += 0.23f;
            Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.position += side * MathF.Sin(WavePhase) * 1.7f;

            //追踪:常态缓咬,歼灭协议死咬
            float steer = Empowered ? 0.055f : 0.02f;
            float range = Empowered ? 800f : 520f;
            int target = FindTarget(range);
            if (target >= 0) {
                float speed = Projectile.velocity.Length();
                if (Empowered) {
                    speed = MathF.Min(speed + 0.06f, 16f);
                }
                Vector2 want = (Main.npc[target].Center - Projectile.Center)
                    .SafeNormalize(Vector2.Zero) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, steer);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //暗雾脱落:影子的行迹比本体活得久
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.06f, new Color(14, 3, 6) * 0.75f,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(20, 34));
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
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
            }
            //暗爆:雾向内收拢再散,红火星点缀
            for (int i = 0; i < 5; i++) {
                Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(18f, 18f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(at, (Projectile.Center - at) * 0.05f,
                    new Color(16, 4, 6) * 0.85f, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f),
                    new Color(255, 45, 30), Main.rand.NextFloat(0.8f, 1.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ShadowTex.Value;
            Vector2 origin = tex.Size() / 2f;

            //拖影:旧位置的暗渣逐级缩小
            if (Projectile.oldPos != null) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, pos, null, new Color(8, 2, 4) * (0.5f * t),
                        Projectile.rotation, origin, Projectile.scale * (0.28f + 0.22f * t), SpriteEffects.None, 0);
                }
            }

            //本体:吸光暗核(速度向拉伸) + 红缘一圈
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 stretch = new(1.5f, 0.9f);
            Color rim = new Color(255, 35, 25) * 0.55f;
            rim.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, rim, Projectile.rotation, origin,
                Projectile.scale * 0.72f * stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(6, 1, 3) * 0.95f, Projectile.rotation, origin,
                Projectile.scale * 0.6f * stretch, SpriteEffects.None, 0);
            //核心里一点红灯,读作影里的机械眼
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color eye = new Color(255, 40, 30) * 0.8f;
            eye.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, eye, 0f, glow.Size() / 2f,
                0.16f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
