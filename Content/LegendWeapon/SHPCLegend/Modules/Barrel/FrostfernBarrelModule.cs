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
    /// <summary>霜蕨枪管，命中抽冰晶脉络，穿线共享寒霜</summary>
    internal sealed class FrostfernBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(150, 240, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.15f;
            ctx.SpreadMul += -0.3f;
            ctx.BeamSpeedMul += 0.08f;
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

    /// <summary>霜蕨脉络，L-system 折线+Trail，SHPCModFrostfern.fx 结晶生长</summary>
    internal sealed class SHPCFrostfernLineProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Vector3 CoreVec = new Color(220, 245, 255).ToVector3();
        private static readonly Vector3 GlowVec = new Color(170, 220, 255).ToVector3();

        //时间轴：0~12 逐枝结晶生长，14 起自梢消融
        private const int LifeFrames = 24;
        private const float GrowWindow = 6f;
        private const int DissolveStart = 14;

        //分叉顶点，首段主干
        private List<Vector2[]> branches;
        //逐枝生长延迟帧与根宽
        private List<float> branchDelays;
        private List<float> branchWidths;
        private List<Trail> trails;
        private float fadeAlpha;
        private float dissolve01;
        private int ageFrames;
        private float seed;

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

        //逐枝结晶进度，延迟错帧
        private float BranchGrow01(int index) {
            if (branchDelays == null || index >= branchDelays.Count) return 1f;
            return MathHelper.Clamp((ageFrames - branchDelays[index]) / GrowWindow, 0f, 1f);
        }

        public override void AI() {
            if (branches == null) {
                BuildLSystem();
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
                }
            }
            //生长期由 shader 结晶门承担淡入，末段消融+淡出
            ageFrames = LifeFrames - Projectile.timeLeft;
            dissolve01 = MathHelper.Clamp((ageFrames - DissolveStart) / (float)(LifeFrames - DissolveStart), 0f, 1f);
            fadeAlpha = ageFrames < 16 ? 1f : MathHelper.SmoothStep(1f, 0f, (ageFrames - 16) / 8f);

            //生长前沿霜花，只落在已结晶段
            if (Main.netMode != NetmodeID.Server && branches != null && Main.GameUpdateCount % 2 == 0) {
                int bi = Main.rand.Next(branches.Count);
                float grow = BranchGrow01(bi);
                if (grow > 0.05f) {
                    var branch = branches[bi];
                    int maxIdx = Math.Max((int)(grow * (branch.Length - 1)), 0);
                    Vector2 pt = branch[Main.rand.Next(maxIdx + 1)];
                    PRTLoader.NewParticle<PRT_Sparkle>(pt + Main.rand.NextVector2Circular(4f, 4f), Main.rand.NextVector2Circular(0.6f, 0.6f), new Color(220, 245, 255), Main.rand.NextFloat(0.3f, 0.65f)).Configure(new Color(120, 180, 220), Main.rand.Next(8, 16), Main.rand.NextFloat(-0.2f, 0.2f), 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.55f, 0.7f) * fadeAlpha * (1f - dissolve01 * 0.6f));
        }

        private void BuildLSystem() {
            branches = new List<Vector2[]>();
            branchDelays = new List<float>();
            branchWidths = new List<float>();
            seed = Main.rand.NextFloat(0f, 8f);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //主干3~4段×110px
            int trunkSegs = Main.rand.Next(3, 5);
            float trunkSegLen = 110f;
            Vector2[] trunk = new Vector2[trunkSegs + 1];
            trunk[0] = Projectile.Center;
            for (int i = 1; i <= trunkSegs; i++) {
                Vector2 wobble = dir.RotatedBy(Main.rand.NextFloat(-0.18f, 0.18f));
                trunk[i] = trunk[i - 1] + wobble * trunkSegLen;
            }
            branches.Add(trunk);
            branchDelays.Add(0f);
            branchWidths.Add(15f);

            //子分叉，中点±35°；结晶自根向梢错帧
            for (int i = 1; i < trunk.Length - 1; i++) {
                int childCount = Main.rand.Next(1, 3);
                float subDelay = 2f + (i - 1) * 0.9f + Main.rand.NextFloat(0.8f);
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
                        //深度2再分
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
                            branchDelays.Add(subDelay + 2f);
                            branchWidths.Add(6.5f);
                        }
                    }
                    branches.Add(sub);
                    branchDelays.Add(subDelay);
                    branchWidths.Add(9.5f);
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
            //霜星环+火花，加色 PRT 染色须带 A
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(180, 230, 255), 0.05f).Configure(0.05f, 0.4f, 18);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, vel, new Color(220, 245, 255), Main.rand.NextFloat(0.45f, 0.95f)).Configure(new Color(120, 200, 230), Main.rand.Next(12, 22), Main.rand.NextFloat(-0.3f, 0.3f), 0.9f);
            }
        }

        //根粗梢细，消融期物理收细
        private float BranchWidth(int index, float progress) {
            float baseW = branchWidths != null && index < branchWidths.Count ? branchWidths[index] : 10f;
            return MathHelper.Lerp(baseW, 2.2f, progress) * (1f - dissolve01 * 0.55f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (branches == null || fadeAlpha < 0.05f) return;

            Effect shader = EffectLoader.SHPCModFrostfern?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //每分叉一 Trail，闭包捕获枝序取宽
            if (trails == null || trails.Count != branches.Count) {
                trails?.Clear();
                trails ??= new List<Trail>(branches.Count);
                for (int i = 0; i < branches.Count; i++) {
                    int idx = i;
                    trails.Add(new Trail(branches[i], p => BranchWidth(idx, p), ColorFunction));
                }
            }

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["uDissolve"]?.SetValue(dissolve01);
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            //晶粒噪声走 s1，寄存器显式声明
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.BlendState = BlendState.Additive;
            for (int i = 0; i < trails.Count; i++) {
                trails[i].TrailPositions = branches[i];
                shader.Parameters["uGrow"]?.SetValue(BranchGrow01(i));
                trails[i].DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (branches == null || fadeAlpha < 0.05f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            //长成的枝梢结点辉光，真加色批染色带 A
            for (int i = 0; i < branches.Count; i++) {
                float grow = BranchGrow01(i);
                if (grow < 0.85f) continue;
                var branch = branches[i];
                Vector2 screen = branch[branch.Length - 1] - Main.screenPosition;
                float k = fadeAlpha * (1f - dissolve01) * grow;
                Color inner = new Color(220, 245, 255) * (k * 0.55f);
                Color outer = new Color(110, 170, 220) * (k * 0.25f);
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, inner, outer, 0.4f, 0f, 2);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
