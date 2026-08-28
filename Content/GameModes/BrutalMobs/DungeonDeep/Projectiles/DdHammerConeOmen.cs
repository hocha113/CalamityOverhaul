using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// Paladin 锤震地锥形预告体：抡锤 36 帧地面锥幕，提交帧放射震地骨刺。
    /// ai[0]=锁定弧度（生成帧锁死，预告即承诺） ai[1]=打包（档位密度|缺口侧） ai[2]=来源打包（槽位+1|类型&lt;&lt;8）。
    /// 骨刺虚影与发射循环共用 <see cref="EmitOffset"/>，缺口是循环真正跳过的角度带（所见即所射）；
    /// 镜像 Wastes 锥幕的来源校验与取消语义，独立实现不跨包引用。
    /// 预告期 Paladin 死亡/槽位复用则取消发射（击杀=有效反制）
    /// </summary>
    internal class DdHammerConeOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        /// <summary>抡锤预告帧数（契约 ≥34，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 36;
        private const int FadeFrames = 8;

        //==== 公平阀门：具名缺口（发射循环真正读取） ====
        /// <summary>缺口半角（弧度），发射与虚影共用同一判定</summary>
        internal const float HammerGapHalfAngle = 0.14f;
        /// <summary>缺口中心偏离锥轴的量（弧度），偏向侧由打包位决定</summary>
        internal const float HammerGapOffset = 0.22f;

        /// <summary>锥半张角</summary>
        private const float HammerHalfArc = 0.46f;
        /// <summary>基础骨刺数（档位只加密度，缺口测试不变）</summary>
        private const int SpikeBase = 9;
        private const float SpikeSpeed = 8.6f;

        private static readonly Color BoneWarn = new Color(226, 214, 180);

        /// <summary>ai[1] 打包：密度加成 | 缺口侧</summary>
        internal static int Pack(int bonus, bool gapSideNegative)
            => Math.Clamp(bonus, 0, 3) | (gapSideNegative ? 16 : 0);

        private float LockedAim => Projectile.ai[0];
        private int Packed => (int)Projectile.ai[1];
        private int Bonus => Packed & 15;
        private float GapSide => (Packed & 16) != 0 ? -1f : 1f;
        private int SourcePacked => (int)Projectile.ai[2];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>第 i 枚骨刺相对锥轴的偏角；落在缺口内返回 null（逃生巷由此保证）</summary>
        internal static float? EmitOffset(int i, int count, float gapSide) {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float ang = MathHelper.Lerp(-HammerHalfArc, HammerHalfArc, t);
            if (Math.Abs(MathHelper.WrapAngle(ang - HammerGapOffset * gapSide)) < HammerGapHalfAngle) {
                return null;
            }
            return ang;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由骨刺
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    //架锤铿锵：前摇开始的可听信号
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                }
            }
            int elapsed = Elapsed;

            //来源校验：施法者死亡/槽位复用则取消提交（玩家击杀=有效反制）
            if (!Cancelled && elapsed < TelegraphFrames) {
                if (AnchorIndex < 0 || AnchorIndex >= Main.maxNPCs || !Main.npc[AnchorIndex].active
                    || Main.npc[AnchorIndex].type != SourcePacked >> 8) {
                    Cancelled = true;
                }
            }

            //预告期凝骨尘（≤2 粒/帧）
            if (!Cancelled && elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = (LockedAim + Main.rand.NextFloat(-HammerHalfArc, HammerHalfArc)).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(20f, 44f),
                    DustID.Bone, -dir * Main.rand.NextFloat(0.8f, 2f), 110, default, 1.0f);
                dust.noGravity = true;
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
                }
            }
        }

        /// <summary>提交帧发射：与虚影同一 EmitOffset，缺口是循环真正跳过的角度带</summary>
        private void Emit() {
            int count = SpikeBase + Bonus;//档位只加密度，缺口测试不变
            int boltType = ModContent.ProjectileType<DdBoltProj>();
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, GapSide);
                if (offset == null) {
                    continue;//具名缺口：逃生巷
                }
                Vector2 vel = (LockedAim + offset.Value).ToRotationVector2() * SpikeSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    boltType, Projectile.damage, 1f, Main.myPlayer, DdBoltProj.ModeSpike);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TelegraphFrames, 0f, 1f);
            }
            else if (elapsed >= TelegraphFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.02f) {
                return false;
            }

            int count = SpikeBase + Bonus;
            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float ghostDist = 22f + 46f * progress;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Texture2D dark = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //锥座暗楔（真透暗底=有遮挡像素）
            Main.EntitySpriteDraw(dark, center + LockedAim.ToRotationVector2() * 30f, null,
                new Color(44, 36, 26, 220) * (0.7f * fade), LockedAim, dark.Size() / 2f,
                new Vector2(0.5f, 0.22f), SpriteEffects.None, 0);

            //骨刺虚影：与发射同一 EmitOffset，虚影即承诺
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, GapSide);
                if (offset == null) {
                    continue;
                }
                float ang = LockedAim + offset.Value;
                Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                Main.EntitySpriteDraw(tex, pos, null, BoneWarn with { A = 160 } * (0.55f * fade * pulse),
                    ang, orig, 0.85f, SpriteEffects.None, 0);
            }

            //缺口亮巷（加色光，指示安全方向）
            float gapAng = LockedAim + HammerGapOffset * GapSide;
            Main.EntitySpriteDraw(glow, center + gapAng.ToRotationVector2() * (ghostDist + 28f), null,
                new Color(255, 244, 200, 0) * (0.5f * fade), gapAng, glow.Size() / 2f,
                new Vector2(2.4f, 0.42f), SpriteEffects.None, 0);
            return false;
        }
    }
}
