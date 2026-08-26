using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 吸血鬼血狩印：ai[0]=吸血鬼NPC索引 ai[1]=绑定档位。owner=被标记玩家（受击端本机生成，原生同步）。
    /// 标记期内玩家再受任何伤害，吸血鬼按比例回血（服务端观测玩家生命镜像下降，权威结算）。
    /// 吸血鬼 Transform 变形（人形↔蝠形）时 whoAmI 不变而 GlobalNPC 实例重建，
    /// 本实体即跨形态状态的携带者：锚校验按形态对放行，标记与回血在两形态间无缝延续。
    /// 永不造成伤害
    /// </summary>
    internal class EclBloodMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>标记持续帧（5 秒）</summary>
        internal const int MarkFrames = 300;
        /// <summary>玩家每次掉血转化为吸血鬼回血的比例</summary>
        private const float LeechFrac = 0.6f;
        /// <summary>单张标记的回血总量上限（档位 1/2/3）</summary>
        private static readonly int[] LeechCapByTier = [140, 190, 240];
        /// <summary>单次结算回血下限，低于此不弹数字（防微量刷屏）</summary>
        private const int MinHealPerProc = 3;

        private static readonly Color BloodDeep = new Color(96, 10, 22);
        private static readonly Color BloodBright = new Color(232, 44, 64, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int BoundTier => (int)MathHelper.Clamp(Projectile.ai[1], 1f, 3f);

        //——服务端结算私产——
        /// <summary>玩家生命镜像哨兵：-1=尚未播种（首个服务端帧初始化）</summary>
        private int lastLife = -1;
        private int healedTotal;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MarkFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>该玩家名下是否已有此吸血鬼的活跃标记（受击端去重用）</summary>
        internal static bool ExistsFor(int playerIndex, int npcIndex) {
            int type = ModContent.ProjectileType<EclBloodMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == playerIndex && (int)proj.ai[0] == npcIndex) {
                    return true;
                }
            }
            return false;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = 0.35f }, Projectile.Center);
                }
            }

            Player marked = Main.player[Projectile.owner];
            if (!marked.Alives()) {
                //玩家倒下：标记连同其回血语义一并终止
                Projectile.Kill();
                return;
            }

            NPC vampire = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!vampire.Alives() || !EclEclipseSets.IsVampireForm(vampire.type)) {
                //吸血鬼已死或槽位被非吸血鬼复用：无受益者，标记消散
                Projectile.Kill();
                return;
            }

            //悬浮在被标记者头顶（各端从玩家同步位置确定性求得）
            Projectile.Center = marked.Top + new Vector2(0f, marked.gfxOffY - 30f);

            //——服务端权威结算：观测玩家生命镜像的下降沿——
            if (!VaultUtils.isClient) {
                if (lastLife < 0) {
                    lastLife = marked.statLife;
                }
                if (marked.statLife < lastLife) {
                    int drop = lastLife - marked.statLife;
                    int cap = LeechCapByTier[BoundTier - 1];
                    int heal = Math.Min((int)(drop * LeechFrac), cap - healedTotal);
                    if (heal >= MinHealPerProc && vampire.life < vampire.lifeMax) {
                        heal = Math.Min(heal, vampire.lifeMax - vampire.life);
                        vampire.life += heal;
                        healedTotal += heal;
                        vampire.HealEffect(heal);
                        vampire.netUpdate = true;
                    }
                }
                lastLife = marked.statLife;
            }

            //渗血尘（预算：至多 1 粒/帧）
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Dust drip = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 4f),
                    DustID.Blood, new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)), 60, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                drip.noGravity = false;
            }

            Lighting.AddLight(Projectile.Center, 0.18f, 0.03f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp((MarkFrames - Projectile.timeLeft) / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float strength = fadeIn * fadeOut;
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);

            //暗色血滴实底 + 亮血芯：竖长水滴形
            Main.EntitySpriteDraw(rim, drawPos, null, BloodDeep * (0.85f * strength), 0f,
                rim.Size() / 2f, new Vector2(0.16f, 0.24f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, BloodBright * (strength * (0.5f + 0.5f * pulse)), 0f,
                glow.Size() / 2f, new Vector2(0.3f, 0.42f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, -3f), null,
                new Color(255, 190, 190, 0) * (0.45f * strength * pulse), 0f,
                glow.Size() / 2f, 0.13f, SpriteEffects.None, 0);
            return false;
        }
    }
}
