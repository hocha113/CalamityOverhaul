using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaWhips
{
    /// <summary>
    /// 鞭奴的湖水鞭体：原版鞭 AI_165 契约的锚点移植，控制点曲线算法逐行对齐
    /// Projectile.FillWhipControlPoints（甩出/回收包络、段长公式、三链混合、
    /// 回收回旋 3π/2），只把锚点从玩家手臂位换成鞭柄驻位、计时从 itemAnimationMax
    /// 换成档案 LashTime 自持（extraUpdates=0 下实际时长与原版等价）。
    /// 判定同原版：自身判定框逐控制点盖章，任一相交即命中；命中把
    /// MinionAttackTargetNPC 指向目标（鞭子的集火本质），原版鞭照挂各自的标签 buff。
    /// 鞭响 Item153 固定在半程、播在鞭尖控制点（与原版同拍同位）。
    /// 绘制沿原版 DrawWhip 双层法：鞭绳线（FishingLine 换血色）+ 鞭段贴图
    /// Frame(1,5) 帧布局（0=柄、末段=4 鞭尖、中段 1+i%3 轮换），水化只做染色
    /// 与撕珠，鞭体是快演出，扫描水线留给盘鞭常态。
    /// ai[0]=原型武器物品类型（档案之源）；velocity=甩向×原武器弹速，
    /// 全程保真不清零（方向/射程/击退/迟到端重建都吃它），锚定靠逐帧位移预补偿
    /// </summary>
    internal class KikasaWhipLash : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧：曲线进度与判定/绘制的时间轴（对位原版 ai[0]）</summary>
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>复制的原型武器物品类型（生成包自带）</summary>
        private int ArmsItemType => (int)Projectile.ai[0];

        private KikasaWhipProfile? profileCache;

        private KikasaWhipProfile Profile => profileCache ??= KikasaArmsProfiler.WhipProfileOf(ArmsItemType);

        /// <summary>控制点缓存：判定/绘制/演出共用，逐次清空重填（对位原版 WhipPointsForCollision）</summary>
        private readonly List<Vector2> lashPoints = [];

        /// <summary>甩向的横向符号（对位原版 spriteDirection）</summary>
        private int LashDir => Projectile.velocity.X >= 0f ? 1 : -1;

        /// <summary>曲线基准旋转（对位原版 rotation = 弹速角 + π/2）</summary>
        private float LashRot => Projectile.velocity.ToRotation() + MathHelper.PiOver2;

        public override void SetDefaults() {
            //18×18 判定框沿控制点逐点盖章，原版 DefaultToWhip 同款规格
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            //一记鞭笞对每个敌人只算一次（原版鞭同款免疫语义）
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //锚定：velocity 保真承载甩向与射程因子，位移逐帧预补偿抵掉引擎推进
            //中途 netUpdate/迟到端收到的永远是真实甩向，不会拿到清零残包
            Projectile.position -= Projectile.velocity;

            Life++;
            int lashTime = Profile.LashTime;
            if (Life >= lashTime) {
                Projectile.Kill();
                return;
            }

            //鞭响：半程整帧、播在鞭尖控制点，与原版 AI_165 同拍同位，各端都响（按距离衰减）
            if ((int)Life == lashTime / 2) {
                FillLashPoints(lashPoints);
                Vector2 tip = lashPoints[^1];
                SoundEngine.PlaySound(SoundID.Item153, tip);
                CrackBurst(tip);
            }

            //甩出段撕珠：包络与原版逐鞭尘一致（0.1→0.7 升、0.9→0.7 收的帐篷形）
            float t = Life / lashTime;
            float envelope = Utils.GetLerpValue(0.1f, 0.7f, t, clamped: true)
                * Utils.GetLerpValue(0.9f, 0.7f, t, clamped: true);
            if (!Main.dedServ && envelope > 0.1f && Main.rand.NextFloat() < envelope * 0.6f) {
                FillLashPoints(lashPoints);
                int at = Main.rand.Next(lashPoints.Count * 2 / 3, lashPoints.Count);
                Vector2 along = at > 0
                    ? (lashPoints[at] - lashPoints[at - 1]).SafeNormalize(Vector2.UnitX)
                    : Vector2.UnitX;
                PRTLoader.NewParticle<PRT_GhostRainDrop>(lashPoints[at],
                    along.RotatedBy(LashDir * MathHelper.PiOver2) * Main.rand.NextFloat(1.5f, 3.5f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18), 0f);
            }

            float glow = 0.4f * MathF.Min(Life / 4f, 1f);
            Lighting.AddLight(Projectile.Center, 0.45f * glow, 0.1f * glow, 0.09f * glow);
        }

        /// <summary>鞭尖炸响：水花锥 + 细环，鞭速崩碎了鞭梢的水</summary>
        private void CrackBurst(Vector2 tip) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = lashPoints.Count >= 2
                ? (lashPoints[^1] - lashPoints[^2]).SafeNormalize(Vector2.UnitX)
                : Vector2.UnitX;
            for (int k = 0; k < 7; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    tip + Main.rand.NextVector2Circular(6f, 6f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.52f))?.Configure(Main.rand.Next(12, 22));
            }
            PRTLoader.NewParticle<PRT_DWave>(tip, Vector2.Zero, BloodBright, 0.05f)
                ?.Configure(new Vector2(0.55f, 1f), dir.ToRotation(), 0.16f, 7);
        }

        //==================== 曲线：FillWhipControlPoints 的锚点移植 ====================

        /// <summary>
        /// 控制点填充：算法与原版逐行对齐（甩出量 num5=min(1.5t,1)、回收段二次方回旋、
        /// 段长=弹速×(useAnimation×2×t)×甩出量×射程倍率/段数、三链混合 + Y 轴 1.5 压扁），
        /// 锚点=Projectile.Center（鞭柄驻位），whipRangeMultiplier 按 1（复制体不吃玩家鞭长加成）
        /// </summary>
        private void FillLashPoints(List<Vector2> points) {
            points.Clear();
            KikasaWhipProfile profile = Profile;
            int segments = profile.Segments;
            float progress = Life / profile.LashTime;

            const float overshoot = 0.5f;
            const float squash = 1f + overshoot;
            float coilStep = MathF.PI * 10f * (1f - progress * squash) * -LashDir / segments;
            float extend = progress * squash;
            float retract = 0f;
            if (extend > 1f) {
                retract = (extend - 1f) / overshoot;
                extend = MathHelper.Lerp(1f, 0f, retract);
            }
            float reach = profile.UseAnimation * 2 * progress;
            float segLen = Projectile.velocity.Length() * reach * extend * profile.RangeMul / segments;

            Vector2 anchor = Projectile.Center;
            Vector2 chainA = anchor;
            float angA = -MathHelper.PiOver2;
            Vector2 chainB = anchor;
            float angB = MathHelper.PiOver2 + MathHelper.PiOver2 * LashDir;
            Vector2 chainC = anchor;
            float angC = MathHelper.PiOver2;
            float baseRot = LashRot;

            points.Add(anchor);
            for (int i = 0; i < segments; i++) {
                float frac = i / (float)segments;
                float step = coilStep * frac;
                Vector2 nextA = chainA + angA.ToRotationVector2() * segLen;
                Vector2 nextC = chainC + angC.ToRotationVector2() * (segLen * 2f);
                Vector2 nextB = chainB + angB.ToRotationVector2() * (segLen * 2f);
                float slack = 1f - extend;
                float blend = 1f - slack * slack;
                Vector2 mixAC = Vector2.Lerp(nextC, nextA, blend * 0.9f + 0.1f);
                Vector2 mixed = Vector2.Lerp(nextB, mixAC, blend * 0.7f + 0.3f);
                Vector2 squashed = anchor + (mixed - anchor) * new Vector2(1f, squash);
                float flip = retract * retract;
                points.Add(squashed.RotatedBy(baseRot + 4.712389f * flip * LashDir, anchor));
                angA += step;
                angC += step;
                angB += step;
                chainA = nextA;
                chainC = nextC;
                chainB = nextB;
            }
        }

        //==================== 判定：控制点逐点盖章（原版 Colliding 同款）====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            FillLashPoints(lashPoints);
            for (int i = 0; i < lashPoints.Count; i++) {
                Point point = lashPoints[i].ToPoint();
                projHitbox.Location = new Point(point.X - projHitbox.Width / 2, point.Y - projHitbox.Height / 2);
                if (projHitbox.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //鞭子的机制身份：鞭中即集火，全部役鬼当场认这个目标（原版 aiStyle 165 同款）
            if (target.active && target.CanBeChasedBy(Projectile)) {
                Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
            }
            //原版鞭的标签 buff 照挂（240 帧与原版一致）；荆棘鞭另带 1/5 概率中毒
            int[] tags = KikasaArmsProfiler.WhipTagBuffsOf(Profile.WhipProjType);
            for (int i = 0; i < tags.Length; i++) {
                target.AddBuff(tags[i], 240);
            }
            if (Profile.WhipProjType == ProjectileID.ThornWhip && Main.rand.Next(5) == 0) {
                target.AddBuff(BuffID.Poisoned, 180);
            }

            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(1.8f, 4.2f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：鞭绳线 + 段贴图帧布局，血湖染色 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (Life < 1f) {
                return false;
            }
            Main.instance.LoadProjectile(Profile.WhipProjType);
            Texture2D segTex = TextureAssets.Projectile[Profile.WhipProjType]?.Value;
            if (segTex == null) {
                return false;
            }
            FillLashPoints(lashPoints);
            if (lashPoints.Count < 2) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //鞭绳线：FishingLine 逐段拉伸（原版 DrawWhip 第一层），换血色半透
            Texture2D lineTex = TextureAssets.FishingLine?.Value;
            if (lineTex != null) {
                Rectangle lineFrame = lineTex.Frame();
                Vector2 lineOrigin = new(lineFrame.Width / 2, 2f);
                Vector2 walk = lashPoints[0];
                for (int i = 0; i < lashPoints.Count - 1; i++) {
                    Vector2 seg = lashPoints[i + 1] - lashPoints[i];
                    float rot = seg.ToRotation() - MathHelper.PiOver2;
                    Color lit = Lighting.GetColor(lashPoints[i].ToTileCoordinates(), BloodDeep);
                    Vector2 scale = new(1f, (seg.Length() + 2f) / lineFrame.Height);
                    sb.Draw(lineTex, walk - Main.screenPosition, lineFrame, lit * 0.7f,
                        rot, lineOrigin, scale, SpriteEffects.None, 0f);
                    walk += seg;
                }
            }

            //鞭段贴图：Frame(1,5) 帧布局，0=柄、末段=4 鞭尖、中段 1+i%3 轮换（原版通用式）
            Rectangle segFrame = segTex.Frame(1, 5);
            int frameHeight = segFrame.Height;
            segFrame.Height -= 2;
            Vector2 segOrigin = segFrame.Size() / 2f;
            for (int i = 0; i < lashPoints.Count - 1; i++) {
                if (i == 0) {
                    segFrame.Y = 0;
                }
                else if (i == lashPoints.Count - 2) {
                    segFrame.Y = frameHeight * 4;
                }
                else {
                    segFrame.Y = frameHeight * (1 + i % 3);
                }
                Vector2 seg = lashPoints[i + 1] - lashPoints[i];
                float rot = seg.ToRotation() - MathHelper.PiOver2;
                //血湖染色：亮度取环境光，向血色收拢，是"湖水凝成的鞭"，不是原物
                Color lit = Lighting.GetColor(lashPoints[i].ToTileCoordinates());
                Color color = Color.Lerp(lit, BloodMain, 0.45f);
                sb.Draw(segTex, lashPoints[i] - Main.screenPosition, segFrame, color,
                    rot, segOrigin, 1f, SpriteEffects.None, 0f);
            }

            //鞭尖水光：半程炸响窗内一点加色亮意
            float t = Life / Profile.LashTime;
            float envelope = Utils.GetLerpValue(0.3f, 0.5f, t, clamped: true)
                * Utils.GetLerpValue(0.72f, 0.5f, t, clamped: true);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && envelope > 0.05f) {
                sb.Draw(glow, lashPoints[^1] - Main.screenPosition, null,
                    (BloodBright with { A = 0 }) * (0.55f * envelope), 0f,
                    glow.Size() * 0.5f, new Vector2(22f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
