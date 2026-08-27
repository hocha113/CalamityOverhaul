using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sky.Projectiles
{
    /// <summary>
    /// 羽刃扇面预兆：悬停凝羽（追踪期，羽影绕身收拢）→ 锁向扇面预览（走廊缺口）→ 齐射羽刃。
    /// ai[0]=锁定瞄角+10（0=未锁定，锁定帧由权威端写入纠偏）ai[1]=Pack(档位,走廊偏移) ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 幽灵预览与发射循环共用同一条 <see cref="InCorridor"/> 判定，看见的缺口就是安全的缺口；
    /// 预告期来源死亡则取消齐射（击杀施法者是有效反制）
    /// </summary>
    internal class SkyFeatherFanOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>悬停定位段帧数（追踪期，凝羽信号）</summary>
        internal const int HoverFrames = 30;
        /// <summary>锁向后的扇面预告帧数（≥34，各档位一律不缩短；锁向即承诺）</summary>
        internal const int AimFrames = 34;
        internal const int TotalFrames = HoverFrames + AimFrames;

        /// <summary>扇面半张角（弧度）</summary>
        private const float SpreadHalf = 0.55f;
        /// <summary>走廊缺口半张角（弧度）：发射循环真正读取的逃生阀门，档位不收窄</summary>
        internal const float CorridorHalf = 0.16f;
        /// <summary>走廊中心偏移上限的安全边距，保证缺口始终落在扇面内侧</summary>
        private const float CorridorSafetyMargin = 0.06f;
        /// <summary>羽刃数（档位 1/2/3）：档位只加密度，缺口测试不变</summary>
        private static readonly int[] BoltCountByTier = [5, 6, 7];
        private const float BoltSpeed = 7.6f;

        private static readonly Color Warn = new Color(208, 232, 255);
        private static readonly Color LaneGlow = new Color(190, 236, 255, 0);

        //==== ai[1] 位打包 ====
        internal static float Pack(int tier, int offsetQuant)
            => Math.Clamp(tier, 1, 3) | (Math.Clamp(offsetQuant, 0, 63) << 2);

        private int Packed => (int)Projectile.ai[1];
        private int Tier => Math.Clamp(Packed & 3, 1, 3);
        /// <summary>走廊中心偏移（生成帧锁定），钳在扇面内侧</summary>
        private float CorridorOffset {
            get {
                float maxOff = SpreadHalf - CorridorHalf - CorridorSafetyMargin;
                return MathHelper.Lerp(-maxOff, maxOff, ((Packed >> 2) & 63) / 63f);
            }
        }

        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private bool Locked => Elapsed >= HoverFrames;
        /// <summary>锁定瞄角：权威端在锁定帧写 ai[0]（+10 哨兵），未到包前各端读确定性冻结的追踪 rotation</summary>
        private float AimAngle => Projectile.ai[0] != 0f ? Projectile.ai[0] - 10f : Projectile.rotation;
        private float CorridorCenter => AimAngle + CorridorOffset;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>某发射角是否落在走廊缺口内（发射与预览共用，缺口即所见）</summary>
        private bool InCorridor(float angle)
            => Math.Abs(MathHelper.WrapAngle(angle - CorridorCenter)) < CorridorHalf;

        public override void AI() {
            //来源校验：施法者死亡则取消齐射；类型比对防槽位复用（各端读同步的 npc.active，结论一致）
            int srcPacked = (int)Projectile.ai[2];
            int src = (srcPacked & 255) - 1;
            NPC anchor = null;
            if (src >= 0 && src < Main.maxNPCs && Main.npc[src].active && Main.npc[src].type == srcPacked >> 8) {
                anchor = Main.npc[src];
            }
            if (anchor == null) {
                Cancelled = true;
            }

            if (!Cancelled && !Locked) {
                //追踪期：跟随施法者并直读目标方向（各端从同步数据确定性推得），锁定后原点与方向双冻结
                Projectile.Center = anchor.Center;
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }

            if (Elapsed == HoverFrames && !VaultUtils.isServer && !Cancelled) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 4 }, Projectile.Center);
            }

            //凝羽尘（≤2 粒/帧，向身侧收拢读作蓄势）
            if (!Cancelled && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(26f, 52f),
                    DustID.Cloud, -dir * Main.rand.NextFloat(1f, 2.2f), 130, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, Warn.ToVector3() * 0.16f);

            if (Projectile.timeLeft == 1 && !Cancelled && !VaultUtils.isClient) {
                FireVolley();
            }
        }

        /// <summary>提交帧齐射：与幽灵预览同一走廊判定，缺口是循环真正跳过的角度带</summary>
        private void FireVolley() {
            int count = BoltCountByTier[Tier - 1];
            float aim = AimAngle;
            int boltType = ModContent.ProjectileType<SkyFeatherBolt>();
            for (int i = 0; i < count; i++) {
                float angle = aim - SpreadHalf + 2f * SpreadHalf * i / (count - 1);
                //走廊缺口：这里跳过的方向就是预览里空着的方向
                if (InCorridor(angle)) {
                    continue;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    angle.ToRotationVector2() * BoltSpeed, boltType,
                    Projectile.damage, 0f, Main.myPlayer, Tier);
            }
        }

        /// <summary>齐射音效在死亡帧各端本地播放（发射走权威端路径，专用服务器上无人能听见）</summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ || Cancelled) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TotalFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            Main.instance.LoadProjectile(ProjectileID.HarpyFeather);
            Texture2D feather = TextureAssets.Projectile[ProjectileID.HarpyFeather].Value;
            Texture2D core = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = feather.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.9f);

            //凝形核：真透贴图打底（有遮挡像素）+ 外围加色晕
            Main.EntitySpriteDraw(core, center, null, Warn * (0.6f * fade),
                Projectile.rotation, core.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, (LaneGlow) * (0.4f * fade * pulse),
                0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);

            if (!Locked || Cancelled) {
                //悬停段：羽影绕身收拢，读作蓄势而非承诺
                float progress = MathHelper.Clamp(elapsed / (float)HoverFrames, 0f, 1f);
                float radius = MathHelper.Lerp(56f, 22f, progress);
                for (int i = 0; i < 4; i++) {
                    float a = Main.GlobalTimeWrappedHourly * 2.8f + i * MathHelper.PiOver2 + Projectile.identity * 0.6f;
                    Vector2 pos = center + a.ToRotationVector2() * radius;
                    Main.EntitySpriteDraw(feather, pos, null, Warn * (0.4f * fade * pulse),
                        a + MathHelper.Pi, origin, 0.8f, SpriteEffects.None, 0);
                }
                return false;
            }

            //锁向段：幽灵弹位预览，逐弹位画羽刃贴图，走廊缺口空出来，所见即所射
            float urgency = MathHelper.Clamp((elapsed - HoverFrames) / (float)AimFrames, 0f, 1f);
            float ghostAlpha = (0.3f + 0.32f * urgency) * fade * pulse;
            int count = BoltCountByTier[Tier - 1];
            float aim = AimAngle;
            for (int i = 0; i < count; i++) {
                float angle = aim - SpreadHalf + 2f * SpreadHalf * i / (count - 1);
                if (InCorridor(angle)) {
                    continue;
                }
                Vector2 dir = angle.ToRotationVector2();
                for (int r = 0; r < 2; r++) {
                    float radius = 26f + 26f * r + 10f * urgency;
                    Main.EntitySpriteDraw(feather, center + dir * radius, null,
                        Warn * (ghostAlpha * (r == 0 ? 1f : 0.6f)),
                        angle + MathHelper.PiOver2, origin, 0.85f, SpriteEffects.None, 0);
                }
            }

            //走廊亮巷（加色光，指示安全方向）
            Vector2 lanePos = center + CorridorCenter.ToRotationVector2() * (56f + 12f * urgency);
            Main.EntitySpriteDraw(glow, lanePos, null, LaneGlow * (0.45f * fade), CorridorCenter,
                glow.Size() / 2f, new Vector2(2.4f, 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
