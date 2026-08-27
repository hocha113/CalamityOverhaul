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
    /// 星轨连珠:一道倾斜的 3D 轨道椭圆,十槽星珠沿轨巡行;巡行末段全轨刹停,<br/>
    /// 星珠向心收缩蓄势、预警线沿锁死外法向铺出,停顿一拍后全珠极速环爆(流星模式)<br/>
    /// ai[0]=宿主npc ai[1]=种子(轨道姿态/缺口槽全由此确定,各端一致) ai[2]=变体(1=镜像副轨)<br/>
    /// 公平阀:GapSlots=2 声明缺口(发射环与判定同读);巡行段只近平面(投影 scale&gt;1.06)咬人;<br/>
    /// 环爆沿凸曲线外法向=弹道几何上不入环内域,环内是可学习的安全区;冻结即锁向(预告即承诺)
    /// </summary>
    internal class CultistOrbitPath : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Lifetime = 254;
        private const int DrawOnEnd = 32;
        private const int CommitFrame = 36;
        /// <summary>冻结拍:自旋刹停,预警线铺出(停顿本身是齐射的吸气)</summary>
        private const int FreezeFrame = 166;
        /// <summary>齐射拍:冻结→齐射之间 40 帧读线窗</summary>
        private const int ReleaseFrame = 206;
        private const int Slots = 10;
        /// <summary>声明缺口:连续两槽恒空(判定与绘制同读)</summary>
        private const int GapSlots = 2;
        /// <summary>齐射弹速(每更新步;流星模式 extraUpdates=1,有效弹速为其两倍)</summary>
        private const float LaunchSpeed = 17f;
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

        /// <summary>
        /// 巡行角积分(闭式,各端由 Age 一致推导):定形后 52 帧匀加速到 0.03 rad/f,<br/>
        /// 巡航至冻结前 16 帧匀减速刹停,此后恒定——骤停即齐射前的吸气拍
        /// </summary>
        private static float OrbitPhase(float age, float spinDir) {
            float t = age - CommitFrame;
            if (t <= 0f) {
                return 0f;
            }
            const float RampFrames = 52f;
            const float MaxOmega = 0.03f;
            const float DecelFrames = 16f;
            const float FreezeT = FreezeFrame - CommitFrame;
            const float CruiseEnd = FreezeT - DecelFrames;

            float ramp = MathHelper.Min(t, RampFrames);
            float phase = MaxOmega * (ramp * ramp / (2f * RampFrames));
            if (t > RampFrames) {
                phase += MaxOmega * (MathHelper.Min(t, CruiseEnd) - RampFrames);
            }
            if (t > CruiseEnd) {
                float tau = MathHelper.Min(t, FreezeT) - CruiseEnd;
                phase += MaxOmega * (tau - tau * tau / (2f * DecelFrames));
            }
            return phase * spinDir;
        }

        /// <summary>槽位是否在声明缺口内</summary>
        private static bool InGap(int slot, int gapStart) {
            int d = ((slot - gapStart) % Slots + Slots) % Slots;
            return d < GapSlots;
        }

        /// <summary>指定巡行相位下的槽珠位置(闭式;预警线与齐射切向共用)</summary>
        private Vector2 BeadPosAtPhase(int slot, float phase, out float scale) {
            GetBasis(out Vector3 e1, out Vector3 e2, out float radius, out _, out _);
            float theta = slot * MathHelper.TwoPi / Slots + phase;
            Vector3 p = (e1 * (float)Math.Cos(theta) + e2 * (float)Math.Sin(theta)) * radius;
            Vector2 offset = CultistOrreryRig.Project(p, OrbitFocal, out scale);
            return Projectile.Center + offset;
        }

        /// <summary>
        /// 齐射方向:冻结位置处投影椭圆的外法向(环爆)——闭式推导,各端一致且冻结后恒定<br/>
        /// 投影椭圆恒宽扁(基向量 e1 无屏幕竖分量),切向弹道会近乎全水平、放空上下方玩家;<br/>
        /// 外法向在扁弧段恰指屏幕上下,垂直覆盖每次施放都有,且与向心收缩蓄势构成收→爆闭环
        /// </summary>
        private Vector2 LaunchDir(int slot) {
            GetBasis(out _, out _, out _, out _, out float spinDir);
            float phase = OrbitPhase(FreezeFrame, spinDir);
            Vector2 a = BeadPosAtPhase(slot, phase, out _);
            Vector2 b = BeadPosAtPhase(slot, phase + 0.02f * spinDir, out _);
            Vector2 tangent = (b - a).SafeNormalize(Vector2.UnitY);
            Vector2 normal = new(-tangent.Y, tangent.X);
            //定向朝外:环心在投影轮廓内(长焦畸变小),点积判外侧
            if (Vector2.Dot(normal, a - Projectile.Center) < 0f) {
                normal = -normal;
            }
            return normal;
        }

        /// <summary>槽珠世界位置与投影比例;冻结窗内沿齐射反向缓退(向心收缩蓄势),判定与绘制同源</summary>
        private Vector2 BeadWorldPos(int slot, float age, out float scale) {
            GetBasis(out _, out _, out _, out _, out float spinDir);
            Vector2 pos = BeadPosAtPhase(slot, OrbitPhase(age, spinDir), out scale);
            if (age > FreezeFrame) {
                float hold = MathHelper.Clamp((age - FreezeFrame) / (float)(ReleaseFrame - FreezeFrame), 0f, 1f);
                pos -= LaunchDir(slot) * (12f * hold * hold * scale);
            }
            return pos;
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            float age = Age;

            int palette = PaletteOf(owner);

            //定形拍(各端本地)
            if ((int)age == CommitFrame) {
                CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.PhaseCore(palette), 1.2f);
                CultistMotion.Shake(Projectile.Center, 3.5f, 9);
            }

            //冻结拍:全轨骤停,预警线随即铺出(停顿即吸气,预告即承诺)
            if ((int)age == FreezeFrame) {
                CultistMotion.Shake(Projectile.Center, 3f, 8);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = 0.55f }, Projectile.Center);
                }
            }

            //环爆拍:全部在位星珠沿锁死外法向极速射出(权威端发射,闪光各端本地),轨道随后熄灭
            if ((int)age == ReleaseFrame) {
                GetBasis(out _, out _, out _, out int gapStart, out _);
                for (int slot = 0; slot < Slots; slot++) {
                    if (InGap(slot, gapStart)) {
                        continue;
                    }
                    Vector2 muzzle = BeadWorldPos(slot, age, out float s);
                    CultistMotion.CastFlash(muzzle, CultistMotion.PhaseCore(palette), 0.75f * s);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle,
                            LaunchDir(slot) * LaunchSpeed,
                            ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, palette, 3f);
                    }
                }
                CultistMotion.Shake(Projectile.Center, 7f, 14);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item125 with { Volume = 1.05f, Pitch = -0.15f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(palette).ToVector3() * 0.3f);
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

            //生命周期透明度:入场淡入,齐射后收灭
            float lifeAlpha = MathHelper.Clamp(age / 10f, 0f, 1f)
                * MathHelper.Clamp((Lifetime - age) / (float)(Lifetime - ReleaseFrame), 0f, 1f);
            float progress = MathHelper.Clamp(age / DrawOnEnd, 0f, 1f);
            //定形闪:commit 后 20 帧内衰减
            float commitFlash = age >= CommitFrame
                ? MathHelper.Clamp(1f - (age - CommitFrame) / 20f, 0f, 1f) * 0.8f : 0f;
            //蓄势度:冻结→齐射窗内 0→1(线宽/星亮/环线让位共用)
            float urge = age > FreezeFrame
                ? MathHelper.Clamp((age - FreezeFrame) / (float)(ReleaseFrame - FreezeFrame), 0f, 1f) : 0f;

            GetBasis(out Vector3 e1, out Vector3 e2, out float radius, out int gapStart, out float spinDir);

            //轨道折线(闭环,首尾重复);冻结窗内环线微让位,把注意力交给星珠与预警线
            const int PathSegs = 56;
            float ringDim = 1f - 0.22f * urge;
            Vector2[] pts = new Vector2[PathSegs + 1];
            float[] widths = new float[PathSegs + 1];
            float[] alphas = new float[PathSegs + 1];
            for (int i = 0; i <= PathSegs; i++) {
                float theta = i / (float)PathSegs * MathHelper.TwoPi;
                Vector3 p = (e1 * (float)Math.Cos(theta) + e2 * (float)Math.Sin(theta)) * radius;
                Vector2 offset = CultistOrreryRig.Project(p, OrbitFocal, out float s);
                pts[i] = Projectile.Center + offset - Main.screenPosition;
                widths[i] = 15f * s * s * s;
                alphas[i] = MathHelper.Lerp(0.26f, 1f, MathHelper.Clamp((s - 0.86f) / 0.34f, 0f, 1f))
                    * lifeAlpha * ringDim;
            }

            Color deep = new(10, 26, 46);
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                deep, mid, bright, progress, 22f, commitFlash, Seed % 100 * 0.07f);

            //预警线:冻结窗内自每颗在位星珠沿锁死外法向铺出(线指哪,星飞哪);末 12 帧收实线白热=倒数
            if (age >= FreezeFrame && age <= ReleaseFrame) {
                float warnIn = MathHelper.Clamp((age - FreezeFrame) / 8f, 0f, 1f);
                bool locked = age >= ReleaseFrame - 12;
                for (int slot = 0; slot < Slots; slot++) {
                    if (InGap(slot, gapStart)) {
                        continue;
                    }
                    Vector2 root = BeadWorldPos(slot, age, out float s) - Main.screenPosition;
                    Vector2 dir = LaunchDir(slot);
                    Vector2[] warnPts = [root, root + dir * 1700f];
                    float[] warnWidths = [(5.5f + 3.5f * urge) * MathHelper.Clamp(s, 0.8f, 1.2f), 3.5f];
                    float[] warnAlphas = [0.55f * warnIn, 0.16f * warnIn];
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", warnPts, warnWidths, warnAlphas,
                        deep, mid, bright, 1f, locked ? 0f : 13f,
                        locked ? 0.9f : 0.25f + 0.4f * urge,
                        Seed % 100 * 0.07f + slot * 0.131f, warnIn * lifeAlpha);
                }
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //槽珠:近大远小,近平面全亮=危险即所见;冻结窗内蓄势胀亮(将射之星一律点亮)
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
                        0.30f * s * s * s * appear * (1f + 0.18f * urge),
                        (0.35f + 0.65f * MathHelper.Max(hot, urge)) * lifeAlpha * appear,
                        Main.GlobalTimeWrappedHourly * 2f + slot);
                }
            }
            return false;
        }
    }
}
