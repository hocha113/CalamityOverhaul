using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>自然元素枪管共享 VFX，屏震/晕/owned 计数与近距节流</summary>
    internal static class SHPCNaturalFx
    {
        /// <summary>多层柔光，inner→outer</summary>
        public static void GlowLayered(SpriteBatch sb, Texture2D tex, Vector2 screenPos,
            Color inner, Color outer, float scale, float rotation, int layers = 3) {
            if (tex == null) return;
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < layers; i++) {
                float t = layers <= 1 ? 0f : i / (float)(layers - 1);
                Color c = Color.Lerp(inner, outer, t);
                float layerScale = scale * (1f + t * 0.6f);
                sb.Draw(tex, screenPos, null, c, rotation, origin, layerScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>屏震，仅本地</summary>
        public static void Shake(float amount) {
            if (amount <= 0f || Main.netMode == NetmodeID.Server) return;
            Player p = Main.LocalPlayer;
            if (p == null) return;
            if (p.TryGetModPlayer(out CWRPlayer cp)) cp.GetScreenShake(amount);
        }

        /// <summary>同主同类型活跃数</summary>
        public static int CountOwned(int owner, int type) {
            int n = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == type) n++;
            }
            return n;
        }

        /// <summary>半径内是否已有同主同型</summary>
        public static bool HasOwnedNear(int owner, int type, Vector2 center, float radius) {
            float r2 = radius * radius;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner || p.type != type) continue;
                if (Vector2.DistanceSquared(p.Center, center) <= r2) return true;
            }
            return false;
        }
    }

    /// <summary>黑曜石枪管，命中叠裂纹，满层碎为寻敌碎片</summary>
    internal sealed class ObsidianBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(95, 55, 135);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.15f;
            ctx.DamageMul += -0.1f;
            ctx.ManaCostMul += 0.35f;
            ctx.BeamExtraPierce += 1;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyObsidianCrack(target, 300, beam.Projectile.owner, Math.Max(damageDone / 5, 1));
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > 520f * 520f) continue;
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                if (eff.ObsidianCrackTime <= 0 || eff.ObsidianCrackOwner != orb.Projectile.owner) continue;
                //联机下 BurstObsidian 在客户端是 no-op，直调会让爆发永远不发生；走请求通道
                eff.RequestObsidianBurst(npc, orb.Projectile.owner,
                    Math.Max(orb.Projectile.damage / 3, 1));
            }
        }
    }

    /// <summary>黑曜石碎片，Trail+Additive，命中小脉冲</summary>
    internal sealed class SHPCObsidianShardProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TrailLen = 10;
        private static readonly Color CoreColor = new(255, 110, 50);
        private static readonly Color EdgeColor = new(80, 35, 110);
        private static readonly Vector3 CoreVec = new Color(255, 200, 110).ToVector3();
        private static readonly Vector3 GlowVec = new Color(255, 90, 40).ToVector3();
        private static readonly Vector3 AuraVec = new Color(70, 25, 90).ToVector3();

        private Vector2[] trailPoints;
        private Trail trail;
        private float fadeAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            //碎片由服务端代生成（BurstObsidian），SyncProjectile 伤害是 short，带全量兜底
            writer.Write(Projectile.damage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int fullDamage = reader.ReadInt32();
            if (fullDamage > 0) {
                Projectile.damage = fullDamage;
            }
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            //首帧玻璃喷出点缀
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f), CoreColor, Main.rand.NextFloat(0.3f, 0.6f)).Configure(EdgeColor, Main.rand.Next(8, 16), Main.rand.NextFloat(-0.2f, 0.2f), 0.7f);
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.32f, 0.18f) * fadeAlpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.45f, Pitch = 0.4f }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = -0.2f }, target.Center);
            //小爆，CyberDetonation 50px
            if (Projectile.owner == Main.myPlayer) {
                int dmg = Math.Max(Projectile.damage / 8, 1);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, Projectile.owner, ai0: 0.3f, ai1: 0f, ai2: 50f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].usesLocalNPCImmunity = true;
                    Main.projectile[idx].localNPCHitCooldown = 30;
                }
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4.2f, 4.2f);
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, vel, CoreColor, Main.rand.NextFloat(0.5f, 1.0f)).Configure(EdgeColor, Main.rand.Next(14, 26), Main.rand.NextFloat(-0.3f, 0.3f), 0.9f);
            }
            SHPCNaturalFx.Shake(2.5f);
        }

        private float WidthFunction(float progress) {
            float taper = MathHelper.Lerp(8f, 0f, progress);
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.5f + progress * 6f);
            return taper * pulse;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2 || fadeAlpha < 0.05f) return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //oldPos→trail，零向量首帧用 head
            trailPoints ??= new Vector2[TrailLen];
            Vector2 head = Projectile.Center;
            for (int i = 0; i < TrailLen; i++) {
                Vector2 raw = i < Projectile.oldPos.Length ? Projectile.oldPos[i] : Vector2.Zero;
                trailPoints[i] = raw == Vector2.Zero ? head : raw + Projectile.Size * 0.5f;
            }

            trail ??= new Trail(trailPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = trailPoints;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["auraColor"]?.SetValue(AuraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(0f);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(CoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(GlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(AuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.05f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screenPos,
                new Color(255, 200, 110) * fadeAlpha,
                new Color(120, 40, 90) * fadeAlpha * 0.3f,
                0.7f, Projectile.rotation, 3);
            //LightShot 方向感
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, screenPos, null,
                    new Color(255, 130, 60, 0) * fadeAlpha * 0.6f,
                    Projectile.rotation, origin, new Vector2(0.5f, 0.4f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Trail+Additive 接管，PreDraw 空
            return false;
        }
    }
}
