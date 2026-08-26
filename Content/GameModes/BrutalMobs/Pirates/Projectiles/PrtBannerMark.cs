using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 跳帮战旗（旗手标记）：ai[0]=旗手NPC索引 ai[1]=Pack(旗手类型,档位) ai[2]=飞船NPC索引。<br/>
    /// 短脉冲提速的可见载体：存续期内旗手半径内的地面船员获得位置推进
    /// （镜像通用提速的碰撞钳制口径，各端从同步原语确定性同跑），
    /// 打掉旗手立即终止，脉冲最长 <see cref="PulseFrames"/> 帧（4 秒，短脉冲而非持续光环）。<br/>
    /// 旗手标记零伤害、永不判定
    /// </summary>
    internal class PrtBannerMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>脉冲时长（≤5 秒的短脉冲契约）</summary>
        internal const int PulseFrames = 240;
        /// <summary>提速圈半径（以旗手为圆心）</summary>
        internal const float SurgeRadius = 480f;
        /// <summary>船员位置推进系数（档位 1/2/3，叠加在通用提速之上）</summary>
        private static readonly float[] SurgeBonusByTier = [0.30f, 0.38f, 0.46f];
        /// <summary>悬浮高度（相对旗手头顶）</summary>
        private const float HoverHeight = 38f;

        private static readonly Color BannerRed = new Color(178, 34, 40);
        private static readonly Color TrimGold = new Color(255, 210, 110);

        private int BearerIndex => (int)Projectile.ai[0];
        private int BearerType => (int)Projectile.ai[1] / 4;
        private int Tier => Math.Clamp((int)Projectile.ai[1] % 4, 1, 3);
        private int Age => PulseFrames - Projectile.timeLeft;

        internal static float Pack(int bearerType, int tier) => bearerType * 4 + tier;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = PulseFrames;
            Projectile.netImportant = true;
        }

        /// <summary>零伤害标记，永不判定</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //旗手校验（索引+类型双校验，槽位易主即视为旗手阵亡）
            if (!(BearerIndex.TryGetNPC(out NPC bearer) && bearer.type == BearerType)) {
                //打掉旗手即止：脉冲随旗手一起结束（服务端权威收尾，客户端等同步）
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
                return;
            }

            Projectile.Center = bearer.Top + new Vector2(0f, bearer.gfxOffY - HoverHeight);

            if (Projectile.localAI[0] == 0f && !Main.dedServ) {
                Projectile.localAI[0] = 1f;
                //号令战吼+旗手就位：挂在战旗实体的出生帧上各端本地播
                //（预兆的倒计时死亡帧会与击杀包竞速，音效放那里会被偶发吞掉；号令落空则无声消散）
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.45f, Pitch = 0.55f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            }

            //短脉冲提速：旗手半径内的地面船员获得位置推进。
            //输入全是同步原语（NPC 位置/速度、旗手实体），各端确定性同跑，
            //镜像 GameModeNPC.PostAI 的碰撞钳制口径，不碰原版移速本值
            float bonus = SurgeBonusByTier[Tier - 1];
            float radiusSq = SurgeRadius * SurgeRadius;
            foreach (NPC crew in Main.ActiveNPCs) {
                if (!PrtPirateSets.IsGroundCrew(crew.type) || crew.SpawnedFromStatue
                    || crew.boss || crew.realLife >= 0) {
                    continue;
                }
                if (Vector2.DistanceSquared(crew.Center, bearer.Center) > radiusSq) {
                    continue;
                }
                Vector2 advance = crew.velocity * bonus;
                if (!crew.noTileCollide) {
                    advance = Collision.TileCollision(crew.position, advance, crew.width, crew.height);
                }
                crew.position += advance;

                //冲锋余尘（受益者身上低频，≤1 粒/帧/人）
                if (!Main.dedServ && Main.rand.NextBool(14)) {
                    Dust rush = Dust.NewDustDirect(crew.position, crew.width, crew.height,
                        DustID.Torch, crew.velocity.X * 0.3f, -0.6f, 110, default, 0.9f);
                    rush.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, TrimGold.ToVector3() * 0.22f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //收旗：无论自然到时还是旗手阵亡，各端可见的终止反馈
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust scrap = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(1.8f, 1.4f), 120, default, 0.9f);
                scrap.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D cloth = CWRAsset.Extra_98.Value;
            Vector2 basePos = Projectile.Center - Main.screenPosition;

            float fadeIn = MathHelper.Clamp(Age / 8f, 0f, 1f);
            //脉冲余量可读：最后 40 帧旗面渐暗，宣告提速将尽
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            float alpha = fadeIn * (0.35f + 0.65f * fadeOut);
            float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity) * 0.16f;

            //旗杆
            Main.EntitySpriteDraw(line, basePos + new Vector2(0f, 26f), null,
                TrimGold with { A = 0 } * (0.45f * alpha),
                -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                new Vector2(40f / line.Width, 4f / line.Height), SpriteEffects.None, 0);
            //旗面（真 alpha 布面）
            Main.EntitySpriteDraw(cloth, basePos + new Vector2(2f, -12f), null,
                Color.Lerp(BannerRed, lightColor, 0.2f) * (0.9f * alpha),
                wave, new Vector2(0f, cloth.Height / 2f),
                new Vector2(0.3f, 0.16f), SpriteEffects.None, 0);
            //交叉弯刀徽记：旗手身份的实体像素记号
            Main.instance.LoadItem(ItemID.Cutlass);
            Texture2D cutlass = TextureAssets.Item[ItemID.Cutlass].Value;
            Vector2 crest = basePos + new Vector2(14f, -12f);
            Main.EntitySpriteDraw(cutlass, crest, null, Color.Lerp(TrimGold, lightColor, 0.35f) * alpha,
                0.8f, cutlass.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(cutlass, crest, null, Color.Lerp(TrimGold, lightColor, 0.35f) * alpha,
                -0.8f, cutlass.Size() / 2f, 0.5f, SpriteEffects.FlipHorizontally, 0);
            //提速圈界线：极淡的圆环提示受益半径（加色，不构成任何判定）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, basePos, null,
                (TrimGold with { A = 0 }) * (0.1f * alpha),
                0f, glow.Size() / 2f, SurgeRadius * 2f / glow.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
