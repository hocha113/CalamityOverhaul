using CalamityOverhaul.Common;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 自然元素枪管共享演出工具，集中管理屏幕震动入口与多层 Additive 晕绘制
    /// 落在第一个使用方文件中，避免再开零散 helper 文件
    /// </summary>
    internal static class SHPCNaturalFx
    {
        /// <summary>
        /// 在指定屏幕坐标多层叠加柔光纹理，从内层到外层在 inner→outer 之间按 t 渐变
        /// </summary>
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

        /// <summary>
        /// 触发一次屏幕震动，仅作用于本地玩家；自动走 CWRPlayer.GetScreenShake 与服务器配置
        /// </summary>
        public static void Shake(float amount) {
            if (amount <= 0f || Main.netMode == NetmodeID.Server) return;
            Player p = Main.LocalPlayer;
            if (p == null) return;
            if (p.TryGetModPlayer(out CWRPlayer cp)) cp.GetScreenShake(amount);
        }

        /// <summary>
        /// 统计指定主人当前活跃的某类型弹幕数量；自然枪管节流用
        /// </summary>
        public static int CountOwned(int owner, int type) {
            int n = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == type) n++;
            }
            return n;
        }

        /// <summary>
        /// 在指定半径内是否已有同主同类型弹幕；用于"聚簇节流"避免同点重复生成
        /// </summary>
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

    /// <summary>
    /// 黑曜石枪管：命中叠加裂纹，满层碎裂为自动寻敌的火山玻璃碎片。
    /// </summary>
    internal sealed class ObsidianBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(95, 55, 135);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.12f;
            ctx.DamageMul += -0.08f;
            ctx.ManaCostMul += 0.28f;
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
                SHPCNPCEffects.BurstObsidian(npc, orb.Projectile.owner, Math.Max(orb.Projectile.damage / 3, 1));
                eff.ObsidianCrackTime = 0;
                eff.ObsidianCrackStacks = 0;
            }
        }
    }

    /// <summary>
    /// 黑曜石碎片：带 Trail 拖尾与 Additive 头部辉光，命中触发小型脉冲爆破
    /// </summary>
    internal sealed class SHPCObsidianShardProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

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

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            //首帧前置粒子点缀，强调玻璃喷出
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.AddParticle(new PRT_Sparkle(
                            Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f),
                            CoreColor, EdgeColor,
                            Main.rand.NextFloat(0.3f, 0.6f), Main.rand.Next(8, 16),
                            Main.rand.NextFloat(-0.2f, 0.2f), 0.7f));
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.32f, 0.18f) * fadeAlpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.45f, Pitch = 0.4f }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = -0.2f }, target.Center);
            //小型碎片爆：复用 CyberDetonationProj 50px 半径
            if (Projectile.owner == Main.myPlayer) {
                int dmg = Math.Max(Projectile.damage / 8, 1);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, Projectile.owner, ai0: 0.3f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].localAI[2] = 50f;
                    Main.projectile[idx].usesLocalNPCImmunity = true;
                    Main.projectile[idx].localNPCHitCooldown = 30;
                }
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4.2f, 4.2f);
                PRTLoader.AddParticle(new PRT_Sparkle(
                    target.Center, vel, CoreColor, EdgeColor,
                    Main.rand.NextFloat(0.5f, 1.0f), Main.rand.Next(14, 26),
                    Main.rand.NextFloat(-0.3f, 0.3f), 0.9f));
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

            //汇总 oldPos 历史为 trail 顶点（屏蔽零向量首帧，避免拖尾跳变）
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
            //叠一层 LightShot 强化方向感
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, screenPos, null,
                    new Color(255, 130, 60, 0) * fadeAlpha * 0.6f,
                    Projectile.rotation, origin, new Vector2(0.5f, 0.4f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //本体由 Trail + Additive 接管，PreDraw 留空避免占位贴图
            return false;
        }
    }
}
