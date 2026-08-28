using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles
{
    /// <summary>
    /// 沙喷锥形幕预告体。ai[0]=锁定弧度 ai[1]=打包参数（档位/密度/缺口侧/变体色） ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 原点与方向在生成帧锁死（预告即承诺）；预告期用沙弹虚影逐条标出弹道，
    /// 缺口亮巷指示逃生方向，虚影与发射走同一个 <see cref="EmitOffset"/>，看到什么就来什么。
    /// 预告期来源死亡则取消发射（击杀施法者是有效反制）
    /// </summary>
    internal class WastesSandConeTelegraph : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        //==== 参数档 ====
        internal const int ProfileSpitter = 0;
        internal const int ProfileFlyer = 1;
        internal const int ProfileGiantFlyer = 2;
        internal const int ProfileKick = 3;
        internal const int ProfileBreath = 4;

        internal readonly struct ConeProfile(int count, float halfArc, float speed, float gravity, float minRange, float maxRange, int cooldown)
        {
            /// <summary>基础沙弹数（档位密度加成叠加其上，缺口测试不受影响）</summary>
            public readonly int Count = count;
            /// <summary>锥半张角（弧度）</summary>
            public readonly float HalfArc = halfArc;
            public readonly float Speed = speed;
            public readonly float Gravity = gravity;
            public readonly float MinRange = minRange;
            public readonly float MaxRange = maxRange;
            public readonly int Cooldown = cooldown;
        }

        private static readonly ConeProfile[] Profiles = [
            new(7, 0.55f, 7.5f, 0.10f, 220f, 700f, 300),//蹲守蚁狮吐沙
            new(6, 0.48f, 8.0f, 0.06f, 200f, 640f, 340),//飞行蚁狮
            new(8, 0.60f, 8.5f, 0.06f, 200f, 700f, 300),//巨型飞行蚁狮
            new(6, 0.40f, 7.0f, 0.16f, 170f, 480f, 360),//踢沙（走行蚁狮/拉弥亚）
            new(6, 0.42f, 6.5f, 0.04f, 140f, 400f, 280),//食尸鬼沙息
        ];

        internal static ConeProfile GetProfile(int id) => Profiles[Math.Clamp(id, 0, Profiles.Length - 1)];

        //==== 公平阀门：具名缺口（发射循环真正读取） ====
        /// <summary>缺口半角（弧度），发射与虚影共用同一判定</summary>
        internal const float GapHalfAngle = 0.13f;
        /// <summary>缺口中心偏离锥轴的量（弧度），偏向侧由打包位决定</summary>
        internal const float GapOffset = 0.24f;
        /// <summary>沙暴期缺口加宽量：沙隐（WastesBrutalNPC.SandVeilActive）的公平回款</summary>
        internal const float StormGapBonus = 0.06f;

        /// <summary>当前缺口半角：沙暴里更宽。读原版天气（全端同步），发射与虚影同读保持缺口即所见</summary>
        internal static float CurrentGapHalfAngle
            => GapHalfAngle + (Terraria.GameContent.Events.Sandstorm.Happening
                && Terraria.GameContent.Events.Sandstorm.Severity > 0.4f ? StormGapBonus : 0f);

        /// <summary>预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 34;
        private const int FadeFrames = 8;

        /// <summary>第 i 枚沙弹相对锥轴的偏角；落在缺口内返回 null（逃生巷由此保证）</summary>
        internal static float? EmitOffset(int i, int count, float halfArc, float gapSide) {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float ang = MathHelper.Lerp(-halfArc, halfArc, t);
            if (Math.Abs(MathHelper.WrapAngle(ang - GapOffset * gapSide)) < CurrentGapHalfAngle) {
                return null;
            }
            return ang;
        }

        //==== ai[1] 位打包 ====
        internal static int Pack(int profileId, int bonus, bool gapSideNegative, int tint)
            => profileId | (Math.Clamp(bonus, 0, 3) << 4) | (gapSideNegative ? 64 : 0) | (Math.Clamp(tint, 0, 3) << 7);

        private int Packed => (int)Projectile.ai[1];
        private int ProfileId => Packed & 15;
        private int Bonus => (Packed >> 4) & 3;
        private float GapSide => (Packed & 64) != 0 ? -1f : 1f;
        private int Tint => (Packed >> 7) & 3;

        private float LockedAim => Projectile.ai[0];
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由沙弹
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //来源检查：施法者死亡则取消提交（玩家击杀=有效反制）；各端读同步的 npc.active，结论一致。
            //类型比对防槽位复用：原怪死后同槽刷出新怪时不放行
            if (!Cancelled && elapsed < TelegraphFrames) {
                int srcPacked = (int)Projectile.ai[2];
                int src = (srcPacked & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != srcPacked >> 8) {
                    Cancelled = true;
                }
            }

            //预告期凝沙（≤2 粒/帧）
            if (!Cancelled && elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = (LockedAim + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(24f, 46f),
                    DustID.Sand, -dir * Main.rand.NextFloat(1f, 2.4f), 120, default, 1.1f);
                dust.noGravity = true;
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
                }
            }
        }

        /// <summary>提交帧发射：与虚影同一 EmitOffset，缺口是循环真正跳过的角度带</summary>
        private void Emit() {
            ConeProfile profile = GetProfile(ProfileId);
            int count = profile.Count + Bonus;//档位只加密度，缺口测试不变
            int pelletType = ModContent.ProjectileType<WastesSandPelletProj>();
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, profile.HalfArc, GapSide);
                if (offset == null) {
                    continue;//具名缺口：逃生巷
                }
                Vector2 vel = (LockedAim + offset.Value).ToRotationVector2() * profile.Speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    pelletType, Projectile.damage, 1f, Main.myPlayer, profile.Gravity, Tint);
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

            ConeProfile profile = GetProfile(ProfileId);
            int count = profile.Count + Bonus;
            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float ghostDist = 20f + 48f * progress;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;

            //弹道虚影：与发射同一 EmitOffset，虚影即承诺
            for (int i = 0; i < count; i++) {
                float? offset = EmitOffset(i, count, profile.HalfArc, GapSide);
                if (offset == null) {
                    continue;
                }
                float ang = LockedAim + offset.Value;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * ghostDist - Main.screenPosition;
                Color ghost = new Color(216, 186, 120, 160) * (0.55f * fade * pulse);
                Main.EntitySpriteDraw(tex, pos, null, ghost, ang, orig, 0.8f, SpriteEffects.None, 0);
            }

            //缺口亮巷（加色光，指示安全方向）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float gapAng = LockedAim + GapOffset * GapSide;
            Vector2 lanePos = Projectile.Center + gapAng.ToRotationVector2() * (ghostDist + 30f) - Main.screenPosition;
            Color lane = new Color(255, 240, 190, 0) * (0.5f * fade);
            Main.EntitySpriteDraw(glow, lanePos, null, lane, gapAng, glow.Size() / 2f,
                new Vector2(2.6f, 0.45f), SpriteEffects.None, 0);
            return false;
        }
    }
}
