using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 汲取脉冲:汲取家族命中玩家后由受击端生成的无害状态实体。
    /// 弹幕原生同步让所有端看到"血被抽走"的流束;权威端(服务端/单人)在首个本地刻
    /// 一次性结算怪物回血与叼食后撤,回血经 HealEffect 与 netUpdate 原生下发。
    /// ai[0]=怪物槽位 ai[1]=怪物类型(跨端身份校验兼风味来源)
    /// </summary>
    internal class SiphonBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>存续帧数(纯表现窗口)</summary>
        private const int LifeFrames = 26;
        /// <summary>叼食后撤速度</summary>
        private const float DartSpeed = 5f;

        private int NpcIndex => (int)Projectile.ai[0];
        private int NpcType => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>槽位+类型双重校验,防槽位复用继承陈旧状态</summary>
        private NPC ResolveNpc() {
            if (NpcIndex < 0 || NpcIndex >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[NpcIndex];
            if (!npc.active || npc.type != NpcType) {
                return null;
            }
            return npc;
        }

        public override void AI() {
            //权威端一次性结算(单人=本机,联机=服务端收到生成包后的首个刻)
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isClient) {
                    ExecuteSiphon();
                }
            }

            //全端可见的抽取流束(受击玩家 → 怪物,预算 ≤3 粒/帧)
            if (VaultUtils.isServer) {
                return;
            }
            NPC target = ResolveNpc();
            Player owner = Main.player[Projectile.owner];
            if (target == null || !owner.active) {
                return;
            }
            int flavor = EvilBiomeMobsNPC.SiphonFlavor(NpcType);
            float progress = 1f - Projectile.timeLeft / (float)LifeFrames;
            for (int i = 0; i < 3; i++) {
                float t = MathHelper.Clamp(progress + i * 0.12f, 0f, 1f);
                Vector2 pos = Vector2.Lerp(Projectile.Center, target.Center, t)
                    + Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(pos, EvilBiomeFX.DustFor(flavor),
                    (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 1.5f, 120, default, 1f);
                dust.noGravity = true;
            }
        }

        /// <summary>回血 + 叼食后撤;只在权威端跑一次</summary>
        private void ExecuteSiphon() {
            NPC npc = ResolveNpc();
            if (npc == null) {
                return;
            }
            int permille = EvilBiomeMobsNPC.SiphonHealPermille(NpcType);
            if (permille > 0 && npc.life < npc.lifeMax) {
                int heal = Math.Max(1, npc.lifeMax * permille / 1000);
                npc.life = Math.Min(npc.life + heal, npc.lifeMax);
                npc.HealEffect(heal, true);
            }
            //蠕虫头不吃位移脉冲(体链转向由原版 AI 掌管,硬拽会拉散)
            if (!EvilBiomeMobsNPC.IsWormHead(NpcType)) {
                Player owner = Main.player[Projectile.owner];
                Vector2 away = (npc.Center - owner.Center).SafeNormalize(-Vector2.UnitY);
                npc.velocity = npc.velocity * 0.35f + away * DartSpeed;
            }
            npc.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC target = ResolveNpc();
            if (target == null) {
                return false;
            }
            //怪物身上的饱食红晕:随进度胀起再淡去
            int flavor = EvilBiomeMobsNPC.SiphonFlavor(NpcType);
            float progress = 1f - Projectile.timeLeft / (float)LifeFrames;
            float swell = MathF.Sin(progress * MathHelper.Pi);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = target.Center - Main.screenPosition;
            float scale = (target.width + 26f) / glow.Width * (1f + 0.3f * swell);
            Main.EntitySpriteDraw(glow, pos, null, EvilBiomeFX.Bright(flavor) with { A = 0 } * (0.5f * swell),
                0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
