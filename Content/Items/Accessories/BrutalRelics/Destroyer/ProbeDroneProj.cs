using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Destroyer
{
    /// <summary>
    /// 探针无人机：饰品自带常驻体，不占召唤栏。
    /// ai[0]槽位 ai[1]标定目标(-1无，所有者写入) ai[2]队长携带的量化标定进度。
    /// 编队维护与开火裁决在所有者端，远端按同步的 ai 与本地节拍走表现
    /// </summary>
    internal class ProbeDroneProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.NPC + "BTD/Probe";

        [VaultLoaden(CWRConstant.NPC + "BTD/Probe_Glow")]
        internal static Asset<Texture2D> GlowTex = null;

        //系列主题色，僚机三件共用
        internal static readonly Color ThemeBlood = new(255, 60, 38);
        internal static readonly Color ThemeAmber = new(255, 150, 70);
        internal static readonly Color ThemeCore = new(255, 225, 185);

        /// <summary>开火周期(5机合计约5.6发/秒)</summary>
        internal const int FireInterval = 54;
        /// <summary>周期末蓄力帧数</summary>
        internal const int ChargeFrames = 12;
        /// <summary>出生免击落帧数</summary>
        private const int SpawnGrace = 45;

        private int Slot => (int)Projectile.ai[0];
        private ref float FireTimer => ref Projectile.localAI[0];
        private ref float Age => ref Projectile.localAI[1];

        //本机侧倾与姿态平滑，仅表现
        private float lean;
        private float prevRot;
        private float chargeT;
        private float spawnScale;
        //队长广播的标定进度镜像
        private float squadProgress;
        private bool isLead;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }

            ProbeMatrixPlayer mp = owner.GetModPlayer<ProbeMatrixPlayer>();
            //存亡由所有者端裁决，远端等同步
            if (Projectile.owner == Main.myPlayer && (!mp.MatrixActive || owner.dead)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 90;

            if (Age == 0f) {
                FireTimer = Slot * 8f;
                prevRot = Projectile.rotation;
                SpawnDeployEffect();
            }
            Age++;
            spawnScale = 0.25f + 0.75f * VaultUtils.EaseOutCubic(Math.Min(Age / 14f, 1f));

            ReadSquadBroadcast(mp);
            bool hasTarget = TargetAlive(out NPC target);

            UpdateMovement(owner, hasTarget ? target : null);
            UpdatePosture(owner, hasTarget ? target : null);
            UpdateFire(owner, hasTarget ? target : null);
            CheckShotDown(mp);

            float heat = Math.Max(squadProgress, chargeT);
            Lighting.AddLight(Projectile.Center, ThemeBlood.ToVector3() * (0.28f + 0.3f * heat));
        }

        #region 运动与姿态

        private void UpdateMovement(Player owner, NPC target) {
            Vector2 anchor = ComputeAnchor(owner, target);
            Vector2 toAnchor = anchor - Projectile.Center;

            //远离过甚直接收束回位
            if (toAnchor.Length() > 2200f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Vector2 desired = toAnchor * 0.085f;
            float maxSpeed = 17f + owner.velocity.Length() * 0.6f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.13f);
        }

        /// <summary>逐槽独立锚点：面敌扇形/头顶弧线，外加双频呼吸漂移</summary>
        private Vector2 ComputeAnchor(Player owner, NPC target) {
            int slot = Slot;
            float t = Main.GlobalTimeWrappedHourly;
            float p1 = t * (1.05f + slot * 0.14f) + slot * 2.39f;
            float p2 = t * (0.63f + slot * 0.09f) + slot * 4.71f;
            Vector2 drift = new((float)Math.Sin(p2) * 9f, (float)Math.Sin(p1) * 11f);
            float fan = (slot - (ProbeMatrixCore.ProbeCount - 1) * 0.5f);

            if (target != null) {
                Vector2 dir = (target.Center - owner.Center).SafeNormalize(-Vector2.UnitY);
                float radius = 118f + (slot % 2) * 26f;
                return owner.Center + dir.RotatedBy(fan * 0.42f) * radius + new Vector2(0f, -14f) + drift;
            }

            float arc = -MathHelper.PiOver2 + fan * 0.5f;
            float r = 92f + (slot % 2) * 14f;
            return owner.Center + arc.ToRotationVector2() * r + drift;
        }

        private void UpdatePosture(Player owner, NPC target) {
            float targetRot;
            if (target != null) {
                targetRot = Projectile.AngleTo(target.Center);
            }
            else if (Projectile.velocity.Length() > 4f) {
                targetRot = Projectile.velocity.ToRotation();
            }
            else {
                targetRot = owner.direction > 0 ? 0f : MathHelper.Pi;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(targetRot, 0.14f);

            //转向倾斜：角速度驱动机体侧倾，稳定跟踪时自然归零
            float dRot = MathHelper.WrapAngle(Projectile.rotation - prevRot);
            prevRot = Projectile.rotation;
            lean = MathHelper.Lerp(lean, MathHelper.Clamp(dRot * 6f, -0.45f, 0.45f), 0.18f);
        }

        #endregion

        #region 开火

        private void UpdateFire(Player owner, NPC target) {
            if (target == null) {
                FireTimer = Math.Min(FireTimer, FireInterval - ChargeFrames);
                chargeT = 0f;
                return;
            }

            FireTimer++;
            chargeT = MathHelper.Clamp((FireTimer - (FireInterval - ChargeFrames)) / ChargeFrames, 0f, 1f);

            //蓄力抖动，纯表现
            if (chargeT > 0f && !VaultUtils.isServer) {
                Projectile.Center += Main.rand.NextVector2Circular(0.8f, 0.8f) * chargeT;
            }

            if (FireTimer < FireInterval) {
                return;
            }

            if (Projectile.owner != Main.myPlayer) {
                //远端只走表现节拍
                FireTimer = 0f;
                return;
            }

            //射界被地形挡住则持弹待机，每帧重试
            if (!Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1)) {
                FireTimer = FireInterval;
                return;
            }

            FireBolt(owner, target);
            FireTimer = 0f;
        }

        private void FireBolt(Player owner, NPC target) {
            Vector2 aimDir = (target.Center + target.velocity * 6f - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int damage = (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(ProbeMatrixCore.BoltDamage);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                Projectile.Center + aimDir * 12f, aimDir * 15f,
                ModContent.ProjectileType<ProbeLockBolt>(), damage, 2f, owner.whoAmI,
                target.whoAmI);
            //后坐
            Projectile.velocity -= aimDir * 4.2f;
        }

        #endregion

        #region 击落与殉爆

        /// <summary>
        /// 敌怪撞击或敌对弹幕命中即击落，仅所有者端裁决。
        /// 候选来自 <see cref="ProbeMatrixPlayer"/> 单趟粗筛的短清单，不再逐探针扫全表
        /// </summary>
        private void CheckShotDown(ProbeMatrixPlayer mp) {
            if (Projectile.owner != Main.myPlayer || Age < SpawnGrace) {
                return;
            }
            //隔帧错开检查，摊薄开销
            if (((int)Age + Slot) % 2 != 0) {
                return;
            }
            //粗筛缓存非本帧(所有者 PostUpdate 未跑到)则跳过本拍
            if (mp.ThreatCacheFrame != Main.GameUpdateCount) {
                return;
            }

            Rectangle box = Projectile.Hitbox;
            foreach (int idx in mp.ThreatNpcs) {
                NPC npc = Main.npc[idx];
                if (npc.active && npc.Hitbox.Intersects(box)) {
                    Projectile.Kill();
                    return;
                }
            }
            foreach (int idx in mp.ThreatProjs) {
                Projectile proj = Main.projectile[idx];
                if (proj.active && proj.hostile && proj.Hitbox.Intersects(box)) {
                    Projectile.Kill();
                    return;
                }
            }
        }

        private void SpawnDeployEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 90, 110), 0.05f)?.Configure(0.05f, 0.5f, 18);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    new Color(255, 120, 130), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
            SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 5 }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //小型殉爆，任何死因统一演出
            Color warm = Color.Lerp(ThemeAmber, ThemeBlood, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero, warm, 0.6f)?.Configure(22, warm);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f),
                    Color.Lerp(ThemeAmber, ThemeCore, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.3f))?.Configure(true, Main.rand.Next(14, 24));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY * 1.5f,
                    Color.White, Main.rand.NextFloat(0.5f, 0.9f))?.SetLifetime(14, 26);
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, -Vector2.UnitY * 1.2f,
                new Color(60, 56, 54), Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(28, 40), 0.5f, Main.rand.NextFloat(-0.04f, 0.04f));
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 4 }, Projectile.Center);
        }

        #endregion

        #region 队内广播

        /// <summary>
        /// 读队长 ai[2] 的量化标定进度并判定自己是否队长，各端一致。
        /// 走 <see cref="ProbeMatrixPlayer"/> 帧戳缓存：owner 端由编队维护顺手填好，
        /// 其余端每帧首个探针填一次、其余直读——五探针不再各自扫全表
        /// </summary>
        private void ReadSquadBroadcast(ProbeMatrixPlayer mp) {
            mp.EnsureSquadCache();
            squadProgress = Math.Max(Projectile.ai[2], mp.SquadProgressCache);
            isLead = Slot == mp.SquadLeadSlotCache;
        }

        private bool TargetAlive(out NPC target) {
            target = null;
            int idx = (int)Projectile.ai[1];
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[idx];
            if (!npc.active || npc.friendly) {
                return false;
            }
            target = npc;
            return true;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = TextureAssets.Projectile[Type].Value;
            Texture2D glowTex = GlowTex?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = body.Size() / 2f;
            //本体贴图反向朝向，沿用Boss探针的绘制约定
            float drawRot = Projectile.rotation + MathHelper.Pi + lean;
            float scl = Projectile.scale * spawnScale;

            //高速残影
            if (Projectile.velocity.Length() > 7f && glowTex != null) {
                for (int i = 2; i < Projectile.oldPos.Length; i += 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float a = 0.35f * (1f - i / (float)Projectile.oldPos.Length);
                    Main.spriteBatch.Draw(glowTex, ghostPos, null, new Color(255, 60, 40, 0) * a,
                        drawRot, glowTex.Size() / 2f, scl, SpriteEffects.None, 0f);
                }
            }

            //标定推进/蓄力时机体升温
            float heat = Math.Max(squadProgress, chargeT * 0.85f);
            MechBossVisualMode mode = heat > 0.03f ? MechBossVisualMode.Warning : MechBossVisualMode.Idle;
            float intensity = 0.5f + 0.5f * heat;

            MechBossThermalRenderer.DrawOutlineHalo(Main.spriteBatch, body, drawPos, null,
                drawRot, origin, scl, SpriteEffects.None, mode, intensity, heat);

            bool shaderOn = MechBossThermalRenderer.BeginThermalShader(Main.spriteBatch, body, body.Bounds,
                mode, intensity, heat, Slot * 0.137f);
            Main.spriteBatch.Draw(body, drawPos, null, lightColor, drawRot, origin, scl, SpriteEffects.None, 0f);
            if (shaderOn) {
                MechBossThermalRenderer.EndThermalShader(Main.spriteBatch);
            }

            if (glowTex != null) {
                Main.spriteBatch.Draw(glowTex, drawPos, null, Color.White, drawRot, glowTex.Size() / 2f, scl, SpriteEffects.None, 0f);
            }
            return false;
        }

        /// <summary>加色层：细红扫描线+镜头蓄能辉光+队长标定光标。真加色批，颜色A随强度走</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch sb) {
            if (!TargetAlive(out NPC target) || Age < 6f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 lensPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 11f;
            Vector2 toTarget = target.Center - lensPos;
            float len = toTarget.Length();
            if (len < 30f) {
                return;
            }
            float rot = toTarget.ToRotation();
            Vector2 screenLens = lensPos - Main.screenPosition;

            //细红扫描基线，随标定加深，带微闪
            float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f + Slot * 1.9f);
            Color lineCol = ThemeBlood * ((0.2f + 0.16f * squadProgress) * flicker);
            sb.Draw(pixel, screenLens, null, lineCol, rot, new Vector2(0f, pixel.Height / 2f),
                new Vector2(len / pixel.Width, 1.4f / pixel.Height), SpriteEffects.None, 0f);

            //沿线行进的扫描亮段
            float dashT = (Main.GlobalTimeWrappedHourly * 1.7f + Slot * 0.31f) % 1f;
            Vector2 dashPos = screenLens + rot.ToRotationVector2() * (len * dashT);
            sb.Draw(pixel, dashPos, null, ThemeAmber * 0.5f, rot, new Vector2(pixel.Width * 0.5f, pixel.Height / 2f),
                new Vector2(18f / pixel.Width, 2.2f / pixel.Height), SpriteEffects.None, 0f);

            //镜头蓄能辉光
            if (chargeT > 0.02f) {
                Texture2D glowDot = CWRAsset.SoftGlow.Value;
                sb.Draw(glowDot, screenLens, null, ThemeBlood * (0.85f * chargeT), 0f,
                    glowDot.Size() / 2f, 0.34f * chargeT, SpriteEffects.None, 0f);
                sb.Draw(glowDot, screenLens, null, ThemeCore * (0.6f * chargeT * chargeT), 0f,
                    glowDot.Size() / 2f, 0.16f * chargeT, SpriteEffects.None, 0f);
            }

            //标定光标由队长绘制，全队只此一份
            if (isLead && squadProgress > 0.01f) {
                DrawReticle(sb, target, pixel);
            }
        }

        /// <summary>目标上的机械标定光标：收拢四角括号+12格进度刻度环</summary>
        private void DrawReticle(SpriteBatch sb, NPC target, Texture2D pixel) {
            float p = squadProgress;
            Vector2 center = target.Center - Main.screenPosition;
            float baseR = Math.Max(target.width, target.height) * 0.5f + 14f;
            float r = baseR + 34f * (1f - p);
            float spin = Main.GlobalTimeWrappedHourly * (0.7f + p * 1.5f);
            Color col = Color.Lerp(ThemeBlood, ThemeAmber, p) * (0.5f + 0.5f * p);

            //四角括号，随进度收拢加速旋转
            for (int i = 0; i < 4; i++) {
                float ang = spin + MathHelper.PiOver2 * i + MathHelper.PiOver4;
                Vector2 corner = center + ang.ToRotationVector2() * r;
                DrawSeg(sb, pixel, corner, ang + MathHelper.Pi * 0.75f, 13f, 2f, col);
                DrawSeg(sb, pixel, corner, ang - MathHelper.Pi * 0.75f, 13f, 2f, col);
            }

            //12格进度刻度环
            int lit = (int)(p * 12f + 0.5f);
            for (int i = 0; i < 12; i++) {
                float ang = -MathHelper.PiOver2 + MathHelper.TwoPi / 12f * i;
                bool on = i < lit;
                Vector2 segStart = center + ang.ToRotationVector2() * (baseR + 4f)
                    - (ang + MathHelper.PiOver2).ToRotationVector2() * 3f;
                DrawSeg(sb, pixel, segStart, ang + MathHelper.PiOver2, 6f,
                    on ? 2.4f : 1.2f, on ? col : ThemeBlood * 0.22f);
            }
        }

        private static void DrawSeg(SpriteBatch sb, Texture2D pixel, Vector2 start, float rot,
            float length, float thick, Color col) {
            sb.Draw(pixel, start, null, col, rot, new Vector2(0f, pixel.Height / 2f),
                new Vector2(length / pixel.Width, thick / pixel.Height), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
