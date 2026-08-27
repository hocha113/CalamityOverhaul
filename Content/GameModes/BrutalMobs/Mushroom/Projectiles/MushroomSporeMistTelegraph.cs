using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 孢雾锥幕预告体。ai[0]=锁定弧度 ai[1]=打包(参数档|风味&lt;&lt;4) ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 原点与方向在生成帧锁死（预告即承诺）；<see cref="MistGapSlot"/> 是发射循环真正跳过的
    /// 具名中央槽缺口（锥正中=安全线），虚影与发射共用同一槽位判定。
    /// 槽位数恒定不随档位增长（缺口角恒定），强度只走冷却与风味弹速；
    /// 预告期来源死亡则取消发射（击杀施法者是有效反制）
    /// </summary>
    internal class MushroomSporeMistTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 参数档 ====
        internal const int ProfileLadybug = 0;
        internal const int ProfilePuff = 1;

        internal readonly struct MistProfile(float halfArc, float minRange, float maxRange, int telegraphFrames)
        {
            /// <summary>锥半张角（弧度）</summary>
            public readonly float HalfArc = halfArc;
            public readonly float MinRange = minRange;
            public readonly float MaxRange = maxRange;
            /// <summary>预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
            public readonly int TelegraphFrames = telegraphFrames;
        }

        private static readonly MistProfile[] Profiles = [
            new(0.50f, 110f, 430f, 32),//瓢虫孢尘喷吐（立定鼓腹 ≥30 帧）
            new(0.55f, 70f, 340f, 30),//困难孢子系短距孢雾
        ];

        internal static MistProfile GetProfile(int id) => Profiles[Math.Clamp(id, 0, Profiles.Length - 1)];

        //==== 公平阀门：具名中央槽缺口（发射循环真正读取） ====
        /// <summary>锥内槽位数（恒定：档位不加槽密度，缺口角随之恒定）</summary>
        internal const int MistSlots = 5;
        /// <summary>具名中央槽缺口：发射与虚影共同跳过的槽位索引（锥正中=逃生线）</summary>
        internal const int MistGapSlot = 2;

        private const int FadeFrames = 8;

        /// <summary>第 i 槽相对锥轴的偏角（槽距均匀，几何与档位无关）</summary>
        internal static float SlotOffset(int i, float halfArc)
            => MathHelper.Lerp(-halfArc, halfArc, i / (float)(MistSlots - 1));

        //==== ai[1] 位打包 ====
        internal static int Pack(int profileId, int flavor) => profileId | (flavor << 4);

        private int ProfileId => (int)Projectile.ai[1] & 15;
        private int Flavor => ((int)Projectile.ai[1] >> 4) & 15;
        private float LockedAim => Projectile.ai[0];
        private int TelegraphFrames => GetProfile(ProfileId).TelegraphFrames;
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由孢弹
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期由参数档决定，各端以同一 ai 值展开时间轴
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
                }
            }
            int elapsed = Elapsed;

            //来源检查：施法者死亡则取消提交（玩家击杀=有效反制）；类型比对防槽位复用
            if (!Cancelled && elapsed < TelegraphFrames) {
                int srcPacked = (int)Projectile.ai[2];
                int src = (srcPacked & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != srcPacked >> 8) {
                    Cancelled = true;
                }
            }

            //鼓腹聚孢：孢尘向锥口聚拢（≤2 粒/帧）
            if (!Cancelled && elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = (LockedAim + Main.rand.NextFloat(-0.6f, 0.6f)).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(22f, 44f),
                    DustID.GlowingMushroom, -dir * Main.rand.NextFloat(1f, 2.2f), 130, default, 1.05f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, MushroomSporeBoltProj.SporeBright.ToVector3()
                * (0.2f * MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f)));

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.45f, Pitch = 0.55f, MaxInstances = 4 }, Projectile.Center);
                }
            }
        }

        /// <summary>提交帧发射：与虚影同一槽位几何，MistGapSlot 是循环真正跳过的槽位</summary>
        private void Emit() {
            (float speed, float gravity) = MushroomSporeBoltProj.FlavorShot(Flavor);
            float halfArc = GetProfile(ProfileId).HalfArc;
            int boltType = ModContent.ProjectileType<MushroomSporeBoltProj>();
            for (int i = 0; i < MistSlots; i++) {
                if (i == MistGapSlot) {
                    continue;//具名中央槽缺口：正对锥心的逃生线
                }
                Vector2 vel = (LockedAim + SlotOffset(i, halfArc)).ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    boltType, Projectile.damage, 1f, Main.myPlayer, gravity, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            int telegraph = TelegraphFrames;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)telegraph, 0f, 1f);
            }
            else if (elapsed >= telegraph) {
                fade = MathHelper.Clamp(1f - (elapsed - telegraph) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            float progress = MathHelper.Clamp(elapsed / (float)telegraph, 0f, 1f);
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);
            float halfArc = GetProfile(ProfileId).HalfArc;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //腹光：暗底+加色芯在原点鼓起（立定鼓腹的可见信号）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Main.EntitySpriteDraw(rim, center, null, MushroomSporeBoltProj.SporeDeep * (0.7f * fade),
                0f, rim.Size() / 2f, 0.26f + 0.12f * progress, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null,
                (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.55f * fade * pulse),
                0f, glow.Size() / 2f, 0.5f + 0.25f * progress, SpriteEffects.None, 0);

            //弹道虚影：与发射同一槽位判定，虚影即承诺
            float ghostDist = 18f + 42f * progress;
            for (int i = 0; i < MistSlots; i++) {
                if (i == MistGapSlot) {
                    continue;
                }
                float ang = LockedAim + SlotOffset(i, halfArc);
                Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                MushroomSporeBoltProj.DrawGlobAt(pos, ang + MathHelper.PiOver2,
                    0.55f * fade * pulse, new Vector2(0.22f, 0.3f));
            }

            //中央缺口亮巷（加色光，指示锥正中的安全线）
            float gapAng = LockedAim + SlotOffset(MistGapSlot, halfArc);
            Vector2 lanePos = center + gapAng.ToRotationVector2() * (ghostDist + 26f);
            Color lane = new Color(150, 255, 235, 0) * (0.5f * fade);
            Main.EntitySpriteDraw(glow, lanePos, null, lane, gapAng, glow.Size() / 2f,
                new Vector2(2.4f, 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
