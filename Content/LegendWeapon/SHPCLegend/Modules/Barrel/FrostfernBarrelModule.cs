using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 霜蕨枪管：命中后沿弹道背面抽出冰晶脉络，穿线目标共享寒霜。
    /// </summary>
    internal sealed class FrostfernBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(150, 240, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.12f;
            ctx.SpreadMul += -0.24f;
            ctx.BeamSpeedMul += 0.10f;
            ctx.CritAdd += 4;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, dir,
                ModContent.ProjectileType<SHPCFrostfernLineProj>(),
                Math.Max(damageDone / 3, 1), 0f, beam.Projectile.owner);
        }
    }

    /// <summary>
    /// 霜蕨脉络：L-system 派生折线，每条分叉用 Trail + CyberDataArc shader 绘制
    /// </summary>
    internal sealed class SHPCFrostfernLineProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Vector3 CoreVec = new Color(220, 245, 255).ToVector3();
        private static readonly Vector3 GlowVec = new Color(170, 220, 255).ToVector3();

        //每条分叉为一段连续顶点序列；首段是主干，其余为派生子分叉
        private List<Vector2[]> branches;
        private List<Trail> trails;
        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 24;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (branches == null) {
                BuildLSystem();
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
                }
            }
            //快进快出：前 4 帧强势出现，剩余渐隐
            int t = 24 - Projectile.timeLeft;
            if (t < 4) fadeAlpha = MathHelper.SmoothStep(0f, 1f, t / 4f);
            else fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 4) / 20f);

            //节点处随机霜花闪烁
            if (Main.netMode != NetmodeID.Server && branches != null && Main.GameUpdateCount % 2 == 0) {
                var branch = branches[Main.rand.Next(branches.Count)];
                Vector2 pt = branch[Main.rand.Next(branch.Length)];
                PRTLoader.AddParticle(new PRT_Sparkle(
                    pt + Main.rand.NextVector2Circular(4f, 4f), Main.rand.NextVector2Circular(0.6f, 0.6f),
                    new Color(220, 245, 255), new Color(120, 180, 220),
                    Main.rand.NextFloat(0.3f, 0.65f), Main.rand.Next(8, 16),
                    Main.rand.NextFloat(-0.2f, 0.2f), 0.7f));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.55f, 0.7f) * fadeAlpha);
        }

        private void BuildLSystem() {
            branches = new List<Vector2[]>();
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //主干 4 段，每段 110px
            int trunkSegs = Main.rand.Next(3, 5);
            float trunkSegLen = 110f;
            Vector2[] trunk = new Vector2[trunkSegs + 1];
            trunk[0] = Projectile.Center;
            for (int i = 1; i <= trunkSegs; i++) {
                Vector2 wobble = dir.RotatedBy(Main.rand.NextFloat(-0.18f, 0.18f));
                trunk[i] = trunk[i - 1] + wobble * trunkSegLen;
            }
            branches.Add(trunk);

            //子分叉：从主干第 i 段中点向 ±35° 派生 1~2 段
            for (int i = 1; i < trunk.Length - 1; i++) {
                int childCount = Main.rand.Next(1, 3);
                for (int c = 0; c < childCount; c++) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    float angle = MathHelper.ToRadians(35f) * side + Main.rand.NextFloat(-0.25f, 0.25f);
                    Vector2 childDir = (trunk[i] - trunk[i - 1]).SafeNormalize(dir).RotatedBy(angle);
                    int subSegs = Main.rand.Next(2, 4);
                    Vector2[] sub = new Vector2[subSegs + 1];
                    sub[0] = Vector2.Lerp(trunk[i - 1], trunk[i], Main.rand.NextFloat(0.4f, 0.85f));
                    for (int s = 1; s <= subSegs; s++) {
                        Vector2 wobble = childDir.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));
                        sub[s] = sub[s - 1] + wobble * trunkSegLen * 0.55f;
                        //深度2：再分叉一次
                        if (s == subSegs - 1 && Main.rand.NextBool(2)) {
                            float side2 = Main.rand.NextBool() ? 1f : -1f;
                            Vector2 grandDir = childDir.RotatedBy(MathHelper.ToRadians(28f) * side2);
                            int gSegs = 2;
                            Vector2[] grand = new Vector2[gSegs + 1];
                            grand[0] = sub[s];
                            for (int g = 1; g <= gSegs; g++) {
                                grand[g] = grand[g - 1] + grandDir.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * trunkSegLen * 0.35f;
                            }
                            branches.Add(grand);
                        }
                    }
                    branches.Add(sub);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (branches == null) return false;
            float point = 0f;
            Vector2 tl = targetHitbox.TopLeft();
            Vector2 sz = targetHitbox.Size();
            foreach (var branch in branches) {
                for (int i = 0; i < branch.Length - 1; i++) {
                    if (Collision.CheckAABBvLineCollision(tl, sz, branch[i], branch[i + 1], 18f, ref point)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 240);
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item120 with { Volume = 0.35f, Pitch = 0.5f }, target.Center);
            //霜星脉冲环 + 火花
            PRTLoader.AddParticle(new PRT_StarPulseRing(
                target.Center, Vector2.Zero,
                new Color(180, 230, 255, 0), 0.05f, 0.4f, 18));
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.AddParticle(new PRT_Sparkle(
                    target.Center, vel,
                    new Color(220, 245, 255), new Color(120, 200, 230),
                    Main.rand.NextFloat(0.45f, 0.95f), Main.rand.Next(12, 22),
                    Main.rand.NextFloat(-0.3f, 0.3f), 0.9f));
            }
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return MathHelper.Lerp(2f, 14f, taper) * fadeAlpha;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (branches == null || fadeAlpha < 0.05f) return;

            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //每条分叉一个 Trail 实例
            if (trails == null) {
                trails = new List<Trail>(branches.Count);
                foreach (var branch in branches) {
                    trails.Add(new Trail(branch, WidthFunction, ColorFunction));
                }
            }
            else if (trails.Count != branches.Count) {
                trails.Clear();
                foreach (var branch in branches) {
                    trails.Add(new Trail(branch, WidthFunction, ColorFunction));
                }
            }

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            for (int i = 0; i < trails.Count; i++) {
                trails[i].TrailPositions = branches[i];
                trails[i].DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (branches == null || fadeAlpha < 0.05f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            //在主干末端节点贴一张 SoftGlow，强化"霜花结点"
            Vector2 origin = glow.Size() * 0.5f;
            foreach (var branch in branches) {
                Vector2 tip = branch[branch.Length - 1];
                Vector2 screen = tip - Main.screenPosition;
                Color inner = new Color(220, 245, 255, 0) * fadeAlpha;
                Color outer = new Color(110, 170, 220, 0) * fadeAlpha * 0.5f;
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, inner, outer, 0.4f, 0f, 2);
                _ = origin;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
