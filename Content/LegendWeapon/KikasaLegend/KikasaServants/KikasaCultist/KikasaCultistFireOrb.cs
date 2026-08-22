using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的血火追踪球。材质身份：沸血火种，一团被点燃的湖血，烧的是血本身。
    /// 签名行为：主体表面沸泡鼓起又瘪回；行进反向撕出端头破碎的火舌（根部压在身下）；
    /// 沿途甩落仍在燃烧的血滴（液态火舌粒子）。弹体 = Extra_98 真 alpha 多层
    /// （焦壳暗缘/血火主体/沸泡子团/炽白芯 + 撕焰尾舌），非光斑叠层。
    /// 成对出膛、绕同一条追踪轴反相缠绕（DNA 双螺旋），
    /// 轴线各端确定性积分（同 spawn 同参数同轨迹），球间画淡光横档读出螺旋结构。
    /// 熄灭前分裂一次：寿命到点或贴近目标时炸成两颗直飞小火弹（只在 owner 端生成）。
    /// ai[0]=代数(0 母球/1 子弹)，ai[1]=母球相位(0/1)，ai[2]=目标 whoAmI+1
    /// </summary>
    internal class KikasaCultistFireOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>母球分裂帧（确定性拍点，与贴近判据取先到者）</summary>
        private const int SplitFrame = 56;
        private const float SplitNearDist = 100f;
        private const int ChildLife = 36;
        private const float AxisSpeed = 11f;
        private const float AxisTurnRate = 0.05f;
        private const float HelixRadius = 22f;

        private int Generation => (int)Projectile.ai[0];
        private float HelixPhase => (int)Projectile.ai[1] * MathHelper.Pi;
        private int TargetIndex => (int)Projectile.ai[2] - 1;

        private ref float Life => ref Projectile.localAI[0];

        //螺旋轴：各端从相同 spawn 参数确定性积分，不入同步
        private Vector2 axisPos;
        private float axisAngle;
        private bool axisInit;
        private Vector2 moveDelta;
        private bool splitDone;

        private float Seed => Projectile.identity * 0.7391f % 3.9f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            //母球与子焰都穿墙：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //子焰到寿自灭（ChildLife），不依赖撞地收尾
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            if (Generation == 1) {
                UpdateChild();
                return;
            }

            if (!axisInit) {
                //轴从出膛点起步，初向即出膛向；母球位置驱动，穿墙缠飞不吃物块
                axisInit = true;
                axisPos = Projectile.Center;
                axisAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
                Projectile.velocity = Vector2.Zero;
            }

            //轴线追踪：转率有顶，火球是缠着打不是贴脸炸
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            bool chase = target?.active == true && target.CanBeChasedBy(Projectile);
            if (chase) {
                float wantAngle = (target.Center - axisPos).ToRotation();
                axisAngle = axisAngle.AngleTowards(wantAngle, AxisTurnRate);
            }
            axisPos += axisAngle.ToRotationVector2() * AxisSpeed;

            //绕轴反相缠绕：球位 = 轴位 + 法向正弦摆
            Vector2 axisDir = axisAngle.ToRotationVector2();
            Vector2 perp = axisDir.RotatedBy(MathHelper.PiOver2);
            Vector2 want = axisPos + perp * MathF.Sin(Life * 0.24f + HelixPhase) * HelixRadius;
            moveDelta = want - Projectile.Center;
            Projectile.Center = want;
            Projectile.rotation = moveDelta.ToRotation();

            //尾焰：沿行进反向甩出的暖芒 + 燃烧的血滴 + 偶发暗烟
            if (!Main.dedServ) {
                if (Life % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center - moveDelta * 0.6f + Main.rand.NextVector2Circular(3f, 3f),
                        -moveDelta * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.BloodMain, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(8, 14));
                }
                //沸溅掉队的血滴仍在燃烧：液态火舌短寿飘落
                if (Life % 5 == 1) {
                    PRTLoader.NewParticle<PRT_KikasaTwinsFlame>(
                        Projectile.Center - moveDelta * 0.8f + Main.rand.NextVector2Circular(4f, 4f),
                        -moveDelta * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.BloodMain, Main.rand.NextFloat(0.3f, 0.7f)),
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 22), 0.04f);
                }
                if (Life % 9 == 4) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center - moveDelta * 1.2f,
                        new Vector2(0f, -0.3f), KikasaCultistServant.MistBlood * 0.55f,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(20, 34));
                }
            }
            Lighting.AddLight(Projectile.Center, 0.5f * VisualFade, 0.22f * VisualFade, 0.08f * VisualFade);

            //分裂裁决：确定性拍点或贴近目标（NPC 位置各端一致）；子弹只在 owner 端生成
            bool near = chase && Vector2.Distance(Projectile.Center, target.Center) < SplitNearDist;
            if (!splitDone && (Life >= SplitFrame || near)) {
                splitDone = true;
                SplitBurst(chase ? target.Center : axisPos + axisDir * 200f);
                Projectile.Kill();
            }
        }

        /// <summary>子弹：直飞微追踪的小火舌，30 余帧后自行熄灭</summary>
        private void UpdateChild() {
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            if (target?.active == true && target.CanBeChasedBy(Projectile)) {
                float wantAngle = (target.Center - Projectile.Center).ToRotation();
                float newAngle = Projectile.velocity.ToRotation().AngleTowards(wantAngle, 0.035f);
                Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
            }
            Projectile.velocity *= 1.005f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            moveDelta = Projectile.velocity;

            if (!Main.dedServ && Life % 2 == 1) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(2f, 2f),
                    -Projectile.velocity * 0.1f,
                    Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.RuneCore, Main.rand.NextFloat(0.35f)),
                    Main.rand.NextFloat(0.55f, 0.9f))?.Configure(false, Main.rand.Next(6, 11));
            }
            //子焰也在滴燃血，量减半
            if (!Main.dedServ && Life % 7 == 3) {
                PRTLoader.NewParticle<PRT_KikasaTwinsFlame>(
                    Projectile.Center - Projectile.velocity * 0.6f,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.BloodMain, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.28f, 0.42f))?.Configure(Main.rand.Next(10, 16), 0.03f);
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.15f, 0.05f);

            //熄灭：焰苗到寿即灭，不留爆点
            if (Life >= ChildLife) {
                Projectile.Kill();
            }
        }

        /// <summary>分裂拍：母球化作两颗小火弹沿轴向劈开（owner 端生成，spawn 自带全部初值）</summary>
        private void SplitBurst(Vector2 aimPos) {
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(axisAngle.ToRotationVector2());
            for (int s = -1; s <= 1; s += 2) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    aim.RotatedBy(s * 0.3f) * 13f,
                    ModContent.ProjectileType<KikasaCultistFireOrb>(),
                    (int)(Projectile.damage * 0.55f), 2f, Projectile.owner,
                    1f, 0f, TargetIndex + 1);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //火熄灭的一蓬溅焰：母球大、子弹小
            if (Main.dedServ) {
                return;
            }
            bool child = Generation == 1;
            int count = child ? 5 : 10;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2.8f, 2.8f) + moveDelta * 0.15f,
                    Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.BloodMain, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(false, Main.rand.Next(10, 18));
            }
            for (int i = 0; i < (child ? 2 : 4); i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(1.6f, 1.6f) + new Vector2(0f, -1f),
                    KikasaCultistServant.BloodMain * 0.55f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20), 0.25f);
            }
            if (!child) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center, new Vector2(0f, -0.4f),
                    KikasaCultistServant.MistBlood * 0.7f, Main.rand.NextFloat(0.45f, 0.65f))
                    ?.Configure(Main.rand.Next(30, 50));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            bool child = Generation == 1;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 dir = moveDelta.SafeNormalize(Vector2.UnitX);
            float rot = Projectile.rotation;
            float stretch = MathHelper.Clamp(moveDelta.Length() * 0.06f, 0.2f, 1f);
            float r = child ? 0.6f : 1f;

            //母球：与镜像位之间的淡光横档，把双螺旋的"梯子"读出来（黑底 SoftGlow 走 A=0 加色）
            if (!child && axisInit && glow != null) {
                Vector2 mirror = axisPos * 2f - Projectile.Center;
                Vector2 mid = (Projectile.Center + mirror) * 0.5f - Main.screenPosition;
                Vector2 span = mirror - Projectile.Center;
                if (span.Length() > 6f) {
                    sb.Draw(glow, mid, null, (KikasaCultistServant.FireTint with { A = 0 }) * (0.16f * fade),
                        span.ToRotation(), glow.Size() * 0.5f,
                        new Vector2(span.Length() * 1.05f / glow.Width * 2f, 2.2f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //撕焰尾舌：三条错相位火舌拖在行进反向，端头长度被相位撕碎；
            //先画舌再画身，舌根压进身体（三明治，火从血里长出来）
            int tq = (int)(Life * 0.5f);
            for (int i = 0; i < 3; i++) {
                float flick = KikasaCultistRunes.Hash01(tq * 3.1f + i * 7.7f + Seed);
                float swing = (KikasaCultistRunes.Hash01(tq * 5.3f + i * 11.3f + Seed * 1.7f) - 0.5f)
                    * (0.5f + i * 0.35f);
                Vector2 tongueDir = (-dir).RotatedBy(swing);
                float len = (0.26f + 0.26f * flick) * (1f + stretch * 0.4f) * r;
                Vector2 tPos = pos + tongueDir * (8f + 26f * len) * r;
                Color tCol = Color.Lerp(KikasaCultistServant.FireTint, KikasaCultistServant.BloodMain,
                    0.3f + 0.45f * flick);
                sb.Draw(tex, tPos, null, tCol * ((0.6f - i * 0.13f) * fade),
                    tongueDir.ToRotation() + MathHelper.PiOver2, origin,
                    new Vector2(0.12f - i * 0.02f, len), SpriteEffects.None, 0f);
            }

            //沸腾主体：焦壳暗缘→血火主体（张力抖动）→沸泡子团→炽白芯
            float wob = MathF.Sin(Life * 0.5f + Seed * 4f) * 0.1f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);
            sb.Draw(tex, pos, null, KikasaCultistServant.BloodDeep * (0.85f * fade), rot, origin,
                new Vector2(0.3f * (1f + stretch * 0.45f), 0.27f) * r * jiggle, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null,
                Color.Lerp(KikasaCultistServant.BloodMain, KikasaCultistServant.FireTint, 0.55f) * fade,
                rot, origin,
                new Vector2(0.24f * (1f + stretch * 0.4f), 0.21f) * r * jiggle, SpriteEffects.None, 0f);
            //沸泡：两粒错相子团鼓起又瘪回，表面在沸腾
            for (int i = 0; i < 2; i++) {
                float ph = Life * 0.37f + i * 2.6f + Seed * 3f;
                float swell = MathF.Max(0f, MathF.Sin(ph));
                swell *= swell;
                if (swell < 0.15f) {
                    continue;
                }
                Vector2 off = (Seed * 5f + i * MathHelper.Pi + Life * 0.11f).ToRotationVector2()
                    * 4.5f * r * jiggle.X;
                sb.Draw(tex, pos + off, null, KikasaCultistServant.FireTint * (0.6f * swell * fade), rot, origin,
                    new Vector2(0.08f, 0.07f) * r * (0.7f + swell * 0.6f), SpriteEffects.None, 0f);
            }
            //炽白芯（A=0 预乘加色）
            Color core = Color.Lerp(KikasaCultistServant.RuneCore, KikasaCultistServant.FireTint, 0.3f) with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.7f * fade), rot, origin,
                new Vector2(0.11f * (1f + stretch * 0.3f), 0.09f) * r * jiggle, SpriteEffects.None, 0f);
            return false;
        }
    }
}
