using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 星轨连珠:一道倾斜的 3D 轨道椭圆,八槽星珠沿轨巡行<br/>
    /// ai[0]=宿主npc ai[1]=种子(轨道姿态/缺口槽全由此确定,各端一致) ai[2]=变体(1=镜像副轨)<br/>
    /// 公平阀:GapSlots=2 声明缺口(发射环与判定同读);只在近平面(投影 scale&gt;0.92)咬人,远平面可穿;<br/>
    /// 轨道定形即锁死(预告即承诺),描绘期无伤
    /// </summary>
    internal class CultistOrbitPath : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Lifetime = 288;
        private const int DrawOnEnd = 44;
        private const int CommitFrame = 52;
        private const int ReleaseFrame = 250;
        private const int Slots = 8;
        /// <summary>声明缺口:连续两槽恒空(判定与绘制同读)</summary>
        private const int GapSlots = 2;
        /// <summary>轨道专用长焦(半径 560 级结构,短焦近平面会除法爆炸)</summary>
        private const float OrbitFocal = 2600f;
        /// <summary>近平面阈:投影比例超过此值才咬人(与视觉近大远小同源;长焦下 s∈0.84~1.24)</summary>
        private const float NearPlaneScale = 1.06f;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Seed => (int)Projectile.ai[1];
        private bool Mirrored => (int)Projectile.ai[2] == 1;
        private float Age => Lifetime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>确定性哈希 0~1</summary>
        private static float Hash01(int seed, int salt) {
            uint h = (uint)(seed * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) % 10000 / 10000f;
        }

        private void GetBasis(out Vector3 e1, out Vector3 e2, out float radius, out int gapStart, out float spinDir) {
            float yaw = Hash01(Seed, 1) * MathHelper.TwoPi;
            float pitch = 0.92f + Hash01(Seed, 2) * 0.38f;
            if (Mirrored) {
                yaw += MathHelper.PiOver2;
                pitch = -pitch;
            }
            CultistOrreryRig.BuildBasis(yaw, pitch, out e1, out e2);
            radius = 560f + Hash01(Seed, 3) * 90f;
            gapStart = (int)(Hash01(Seed, 4) * Slots);
            spinDir = Hash01(Seed, 5) > 0.5f ? 1f : -1f;
            if (Mirrored) {
                spinDir = -spinDir;
            }
        }

        /// <summary>巡行角积分(闭式,各端由 Age 一致推导):定形前静止,后 70 帧内匀加速到 0.03 rad/f</summary>
        private static float OrbitPhase(float age, float spinDir) {
            float t = age - CommitFrame;
            if (t <= 0f) {
                return 0f;
            }
            const float RampFrames = 70f;
            const float MaxOmega = 0.03f;
            float ramp = MathHelper.Min(t, RampFrames);
            float phase = MaxOmega * (ramp * ramp / (2f * RampFrames));
            if (t > RampFrames) {
                phase += MaxOmega * (t - RampFrames);
            }
            return phase * spinDir;
        }

        /// <summary>槽位是否在声明缺口内</summary>
        private static bool InGap(int slot, int gapStart) {
            int d = ((slot - gapStart) % Slots + Slots) % Slots;
            return d < GapSlots;
        }

        /// <summary>槽珠世界位置与投影比例</summary>
        private Vector2 BeadWorldPos(int slot, float age, out float scale) {
            GetBasis(out Vector3 e1, out Vector3 e2, out float radius, out _, out float spinDir);
            float theta = slot * MathHelper.TwoPi / Slots + OrbitPhase(age, spinDir);
            Vector3 p = (e1 * (float)Math.Cos(theta) + e2 * (float)Math.Sin(theta)) * radius;
            Vector2 offset = CultistOrreryRig.Project(p, OrbitFocal, out scale);
            return Projectile.Center + offset;
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            float age = Age;

            //定形拍(各端本地)
            if ((int)age == CommitFrame) {
                CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)), 1.2f);
                CultistMotion.Shake(Projectile.Center, 3.5f, 9);
            }

            //释放拍:每颗近轨珠切向甩出(权威端),轨道随后熄灭
            if (!VaultUtils.isClient && (int)age == ReleaseFrame) {
                GetBasis(out _, out _, out _, out int gapStart, out float spinDir);
                for (int slot = 0; slot < Slots; slot++) {
                    if (InGap(slot, gapStart)) {
                        continue;
                    }
                    Vector2 now = BeadWorldPos(slot, age, out float s);
                    if (s <= 0.95f) {
                        continue;
                    }
                    Vector2 next = BeadWorldPos(slot, age + 1f, out _);
                    Vector2 tangent = (next - now).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), now, tangent * 6.5f,
                        ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, PaletteOf(owner));
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)).ToVector3() * 0.3f);
        }

        private static int PaletteOf(NPC owner) => owner != null && owner.active ? (int)owner.ai[0] : 0;

        /// <summary>伤害窗=定形后、释放前;判定=近平面槽珠圆域(与视觉同源)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float age = Age;
            if (age < CommitFrame + 8 || age > ReleaseFrame) {
                return false;
            }
            GetBasis(out _, out _, out _, out int gapStart, out _);
            for (int slot = 0; slot < Slots; slot++) {
                if (InGap(slot, gapStart)) {
                    continue;
                }
                Vector2 pos = BeadWorldPos(slot, age, out float scale);
                if (scale < NearPlaneScale) {
                    continue;
                }
                float radius = 26f * scale;
                Vector2 closest = new(
                    MathHelper.Clamp(pos.X, targetHitbox.Left, targetHitbox.Right),
                    MathHelper.Clamp(pos.Y, targetHitbox.Top, targetHitbox.Bottom));
                if (Vector2.DistanceSquared(pos, closest) < radius * radius) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = PaletteOf(owner);
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.45f);
            float age = Age;

            //生命周期透明度:入场淡入,释放后收灭
            float lifeAlpha = MathHelper.Clamp(age / 10f, 0f, 1f)
                * MathHelper.Clamp((Lifetime - age) / 34f, 0f, 1f);
            float progress = MathHelper.Clamp(age / DrawOnEnd, 0f, 1f);
            //定形闪:commit 后 20 帧内衰减
            float commitFlash = age >= CommitFrame
                ? MathHelper.Clamp(1f - (age - CommitFrame) / 20f, 0f, 1f) * 0.8f : 0f;

            GetBasis(out Vector3 e1, out Vector3 e2, out float radius, out int gapStart, out float spinDir);

            //轨道折线(闭环,首尾重复)
            const int PathSegs = 56;
            Vector2[] pts = new Vector2[PathSegs + 1];
            float[] widths = new float[PathSegs + 1];
            float[] alphas = new float[PathSegs + 1];
            for (int i = 0; i <= PathSegs; i++) {
                float theta = i / (float)PathSegs * MathHelper.TwoPi;
                Vector3 p = (e1 * (float)Math.Cos(theta) + e2 * (float)Math.Sin(theta)) * radius;
                Vector2 offset = CultistOrreryRig.Project(p, OrbitFocal, out float s);
                pts[i] = Projectile.Center + offset - Main.screenPosition;
                widths[i] = 15f * s * s * s;
                alphas[i] = MathHelper.Lerp(0.26f, 1f, MathHelper.Clamp((s - 0.86f) / 0.34f, 0f, 1f)) * lifeAlpha;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                new Color(10, 26, 46), mid, bright, progress, 22f, commitFlash, Seed % 100 * 0.07f);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //槽珠:近大远小,近平面全亮=危险即所见
            if (age >= CommitFrame - 8f && age <= ReleaseFrame) {
                Color edge = CultistMotion.PhaseEdge(palette);
                float appear = MathHelper.Clamp((age - (CommitFrame - 8f)) / 12f, 0f, 1f);
                for (int slot = 0; slot < Slots; slot++) {
                    if (InGap(slot, gapStart)) {
                        continue;
                    }
                    Vector2 pos = BeadWorldPos(slot, age, out float s);
                    //近大远小:s³ 拉开纵深反差,近平面(咬人窗)明显更大更亮
                    float hot = MathHelper.Clamp((s - 0.95f) / (NearPlaneScale - 0.95f), 0f, 1f);
                    CultistOrreryRenderer.DrawStarBead(sb, pos - Main.screenPosition, mid, edge,
                        0.30f * s * s * s * appear, (0.35f + 0.65f * hot) * lifeAlpha * appear,
                        Main.GlobalTimeWrappedHourly * 2f + slot);
                }
            }
            return false;
        }
    }
}
