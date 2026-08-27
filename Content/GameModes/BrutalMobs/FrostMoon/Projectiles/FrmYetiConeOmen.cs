using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 雪怪冰拳震地的锥形幕预告体（霜月独立实现，语义与荒漠沙锥同构但不跨包引用）。
    /// ai[0]=锁定弧度 ai[1]=打包参数（密度加成/缺口侧） ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 原点与方向在生成帧锁死（预告即承诺）；预告期用冰锥虚影逐条标出弹道，
    /// 缺口亮巷指示逃生方向，虚影与发射走同一个 <see cref="EmitOffset"/>，看到什么就来什么。
    /// 预告期来源死亡则取消发射（击杀施法者是有效反制）
    /// </summary>
    internal class FrmYetiConeOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>预告帧数（抡臂前摇契约 ≥34，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 36;
        private const int FadeFrames = 10;

        //==== 公平阀门：具名缺口（发射循环真正读取） ====
        /// <summary>缺口半角（弧度），发射与虚影共用同一判定</summary>
        internal const float ConeGapHalfAngle = 0.15f;
        /// <summary>缺口中心偏离锥轴的量（弧度），偏向侧由打包位决定</summary>
        internal const float ConeGapOffset = 0.22f;

        /// <summary>锥半张角（弧度）</summary>
        internal const float ConeHalfArc = 0.46f;
        /// <summary>基础冰刺数（档位密度加成叠加其上，缺口测试不受影响）</summary>
        internal const int SpikeBaseCount = 4;
        /// <summary>冰刺出膛速度与每帧重力（震地弧）</summary>
        private const float SpikeSpeed = 8.2f;
        private const float SpikeGravity = 0.14f;

        private static readonly Color FrostWarn = new Color(168, 216, 252, 0);

        //==== ai[1] 位打包 ====
        internal static int Pack(int bonus, bool gapSideNegative)
            => Math.Clamp(bonus, 0, 3) | (gapSideNegative ? 16 : 0);

        private int Packed => (int)Projectile.ai[1];
        private int Bonus => Packed & 15;
        private float GapSide => (Packed & 16) != 0 ? -1f : 1f;

        private float LockedAim => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>第 i 根冰刺相对锥轴的偏角；落在缺口内返回 null（逃生巷由此保证）</summary>
        internal static float? EmitOffset(int i, int count, float gapSide) {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float ang = MathHelper.Lerp(-ConeHalfArc, ConeHalfArc, t);
            if (Math.Abs(MathHelper.WrapAngle(ang - ConeGapOffset * gapSide)) < ConeGapHalfAngle) {
                return null;
            }
            return ang;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，杀伤经由冰刺
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //来源检查：施法者死亡则取消提交（玩家击杀=有效反制）；各端读同步的 npc.active，结论一致。
            //类型比对防槽位复用：原怪死后同槽刷出新怪时不放行
            int srcPacked = (int)Projectile.ai[2];
            int src = (srcPacked & 255) - 1;
            if (!Cancelled && elapsed < TelegraphFrames) {
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != srcPacked >> 8) {
                    Cancelled = true;
                }
            }

            if (!Cancelled && elapsed < TelegraphFrames && !Main.dedServ) {
                //预告期凝霜（≤2 粒/帧）：地面锥口聚霜
                if (Main.rand.NextBool(2)) {
                    Vector2 dir = (LockedAim + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(20f, 44f),
                        DustID.Frost, -dir * Main.rand.NextFloat(1f, 2.2f), 120, default, 1f);
                    dust.noGravity = true;
                }
                //抡臂可见性：施法者身上升腾霜屑（实体承载=联机各端可见）
                if (Main.rand.NextBool(3) && src >= 0 && src < Main.maxNPCs && Main.npc[src].active) {
                    NPC caster = Main.npc[src];
                    Dust dust = Dust.NewDustPerfect(caster.Top + new Vector2(Main.rand.NextFloat(-12f, 12f), 4f),
                        DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(1f, 2.4f)), 120, default, 1.1f);
                    dust.noGravity = true;
                }
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    //震地帧（各端本地播放）
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                            Main.rand.NextBool() ? DustID.Ice : DustID.Snow,
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)), 90, default, Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }
        }

        /// <summary>提交帧发射：与虚影同一 EmitOffset，缺口是循环真正跳过的角度带（档位只加密度）</summary>
        private void Emit() {
            int count = SpikeBaseCount + Bonus;
            int spikeType = ModContent.ProjectileType<FrmIceShardProj>();
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, GapSide);
                if (offset == null) {
                    continue;//具名缺口：逃生巷
                }
                Vector2 vel = (LockedAim + offset.Value).ToRotationVector2() * SpikeSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    spikeType, Projectile.damage, 1f, Main.myPlayer, SpikeGravity);
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
            if (fade <= 0.01f) {
                return false;
            }

            int count = SpikeBaseCount + Bonus;
            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float ghostDist = 22f + 46f * progress;
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //弹道虚影：与发射同一 EmitOffset，虚影即承诺
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, GapSide);
                if (offset == null) {
                    continue;
                }
                float ang = LockedAim + offset.Value;
                Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                Color ghost = new Color(190, 228, 255, 160) * (0.55f * fade * pulse);
                Main.EntitySpriteDraw(tex, pos, null, ghost, ang + MathHelper.PiOver2, orig, 0.8f, SpriteEffects.None, 0);
            }

            //地面震源提示（扁平冷光）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, center, null, FrostWarn * (0.35f * fade * pulse), 0f,
                glow.Size() / 2f, new Vector2(1.3f + 0.5f * progress, 0.4f), SpriteEffects.None, 0);

            //缺口亮巷（加色光，指示安全方向）
            float gapAng = LockedAim + ConeGapOffset * GapSide;
            Vector2 lanePos = center + gapAng.ToRotationVector2() * (ghostDist + 28f);
            Color lane = new Color(200, 255, 235, 0) * (0.5f * fade);
            Main.EntitySpriteDraw(glow, lanePos, null, lane, gapAng, glow.Size() / 2f,
                new Vector2(2.5f, 0.45f), SpriteEffects.None, 0);
            return false;
        }
    }
}
