using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 荆棘鞭处决「荆棘爆裂」主爆：目标身上荆棘倒刺炸开（0.6x），
    /// 棘刺由方案在同帧另行生成，本弹幕只管爆点与毒雾视觉
    /// </summary>
    internal class GsWhipThornBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal static readonly Color ThornBright = new(198, 255, 128);
        internal static readonly Color ThornMain = new(110, 196, 64);
        internal static readonly Color ThornDeep = new(48, 104, 36);

        private const int GatherFrames = 4;
        private const int BurstFrames = 4;
        private const int LifeFrames = 22;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= GatherFrames && Elapsed < GatherFrames + BurstFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(140f)));

        public override void AI() {
            if (Elapsed == GatherFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Circular(5.5f, 5.5f),
                        i % 2 == 0 ? ThornBright : ThornMain,
                        Main.rand.NextFloat(0.28f, 0.5f))?.Configure(true, Main.rand.Next(14, 22));
                }
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                        DustID.JungleGrass, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.2f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D jag = CWRUtils.GetT2DAsset(CWRConstant.Masking + "HitJagged01")?.Value;
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare01")?.Value;
            if (jag == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.identity * 0.61f;
            if (Elapsed < GatherFrames) {
                //聚拢：绿芒收束
                float g = 1f - Elapsed / (float)GatherFrames;
                Main.EntitySpriteDraw(flare, pos, null, ThornMain with { A = 0 } * 0.75f,
                    seed, flare.Size() * 0.5f, 0.34f * g + 0.1f, SpriteEffects.None, 0);
                return false;
            }
            //爆裂：三片锯齿倒刺按 identity 定角展开后渐隐
            float t = MathF.Min(1f, (Elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames));
            float fade = 1f - t;
            float grow = 0.4f + 0.5f * (1f - fade * fade);
            for (int i = 0; i < 3; i++) {
                float rot = seed + i * MathHelper.TwoPi / 3f;
                Main.EntitySpriteDraw(jag, pos, null, ThornMain with { A = 0 } * (0.7f * fade), rot,
                    jag.Size() * 0.5f, grow, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(flare, pos, null, ThornBright with { A = 0 } * (0.8f * fade),
                -seed, flare.Size() * 0.5f, 0.3f * fade + 0.1f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 荆棘鞭处决棘刺：四散后转向追踪（各 0.4x），命中挂原版中毒。
    /// 贴图复用原版毒刺弹幕，绿染
    /// </summary>
    internal class GsWhipThornDartProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        private const int ScatterFrames = 10;   //四散段
        private const int LifeFrames = 100;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            int elapsed = LifeFrames - Projectile.timeLeft;
            if (elapsed < ScatterFrames) {
                Projectile.velocity *= 0.96f;
            }
            else {
                //追踪最近可追目标：逐帧限速转向，保留弧线感
                NPC target = FindNearest(560f);
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
                }
                else if (Projectile.velocity.Length() < 9f) {
                    Projectile.velocity *= 1.04f;
                }
            }
            if (!VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.05f, GsWhipThornBurstProj.ThornMain,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy()) {
                    continue;
                }
                float d = Vector2.Distance(npc.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 180);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleGrass,
                        Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.1f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版毒刺绿染 + 加色描心
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color body = Color.Lerp(lightColor, GsWhipThornBurstProj.ThornMain, 0.6f);
            Main.EntitySpriteDraw(tex, pos, null, body, Projectile.rotation,
                tex.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, GsWhipThornBurstProj.ThornBright with { A = 0 } * 0.5f,
                Projectile.rotation, tex.Size() * 0.5f, 1.15f, SpriteEffects.None, 0);
            return false;
        }
    }
}
