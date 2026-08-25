using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者头颅弹幕:歼灭协议的终结斩额外吐出的小型头颅,
    /// 起步迟缓、随后咆哮加速死咬目标,通体缠绕白红电流。
    /// ai[0]=初始化闩 ai[1]=飞行计时
    /// </summary>
    internal class DestroyerHeadMissile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DestroyerHeadShot";

        private ref float Init => ref Projectile.ai[0];
        private ref float FlightTime => ref Projectile.ai[1];

        /// <summary>点火前的滞空帧:先醒后咬</summary>
        private const int ArmFrames = 12;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 240;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (Init == 0) {
                Init = 1;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            FlightTime++;

            if (FlightTime <= ArmFrames) {
                //点火前:减速悬滞,颚间攒电
                Projectile.velocity *= 0.94f;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(
                        Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                        Main.rand.NextVector2Circular(1.5f, 1.5f),
                        Main.rand.NextBool(3) ? Color.White : new Color(255, 90, 70),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, 8);
                }
            }
            else {
                //点火:加速爬升 + 强追踪
                float speed = MathF.Min(Projectile.velocity.Length() + 0.85f, 27f);
                int target = FindTarget(950f);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                if (target >= 0) {
                    NPC npc = Main.npc[target];
                    Vector2 want = (npc.Center + npc.velocity * 3f - Projectile.Center).SafeNormalize(dir);
                    dir = Vector2.Lerp(dir, want, 0.10f).SafeNormalize(dir);
                }
                Projectile.velocity = dir * speed;

                if (FlightTime == ArmFrames + 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.55f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                }

                //白红电流:每几帧沿身甩一条短弧
                if (!VaultUtils.isServer && FlightTime % 4 == 0) {
                    Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * (Main.rand.NextBool() ? 1f : -1f);
                    Vector2[] path = new Vector2[4];
                    for (int k = 0; k < 4; k++) {
                        path[k] = Projectile.Center - dir * (k * 20f)
                            + perp * MathF.Sin(k * 1.3f) * Main.rand.NextFloat(8f, 16f);
                    }
                    PRTLoader.NewParticle<PRT_TeslaArc>(Projectile.Center, Vector2.Zero,
                        new Color(255, 130, 110), 1f)?.Configure(path, Main.rand.Next(8, 13), 8f, (0f, 7f), 4f);
                }
                if (!VaultUtils.isServer && FlightTime % 26 == 0) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.25f, 0.2f));
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
            //电爆:放射状白红电弧 + 冲击环 + 火花,余波比本体活得久
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(0.7f);
                Vector2 dir = ang.ToRotationVector2();
                Vector2[] path = new Vector2[4];
                for (int k = 0; k < 4; k++) {
                    path[k] = Projectile.Center + dir * (k * Main.rand.NextFloat(22f, 34f));
                }
                PRTLoader.NewParticle<PRT_TeslaArc>(Projectile.Center, Vector2.Zero,
                    new Color(255, 120, 100), 1f)?.Configure(path, Main.rand.Next(12, 18), 10f, (0f, 8f), 5f);
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 80, 60), 0f)?.Configure(0.08f, 0.9f, 14);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextBool(3) ? Color.White : new Color(255, 70, 50),
                    Main.rand.NextFloat(1f, 1.9f))?.Configure(false, Main.rand.Next(10, 18));
            }
            Color warm = new Color(255, 90, 60);
            PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero,
                warm, 0.7f)?.Configure(Main.rand.Next(16, 24), warm);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //残影
            if (Projectile.oldPos != null && FlightTime > ArmFrames) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color ghost = new Color(255, 50, 35) * (0.35f * t);
                    ghost.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, ghost, Projectile.rotation, origin,
                        Projectile.scale * (0.8f + 0.2f * t), SpriteEffects.None, 0);
                }
            }

            //背光红晕 + 本体
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color halo = new Color(255, 60, 40) * 0.65f;
            halo.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f,
                0.6f * Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, Color.Lerp(lightColor, Color.White, 0.4f),
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
