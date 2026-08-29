using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.KingSlime
{
    /// <summary>
    /// 王的凝胶领域：坠击落点留存的减速池。ai[0]=池宽px ai[1]=存留帧 ai[2]=单跳伤害。<br/>
    /// 无接触伤害：减速按确定性公式各端同跑(只写权威端会橡皮筋)，
    /// 持续伤害由权威端 SimpleStrikeNPC 周期结算(自带同步)。<br/>
    /// 视觉复用 <see cref="EffectLoader.BKSGelPool"/>，泡沫改鎏金以区分敌我
    /// </summary>
    internal class RoyalGelField : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowTime = 16;
        private const int DrainTime = 36;
        /// <summary>持续伤害结算周期(帧)</summary>
        private const int DotInterval = 18;

        private float PoolWidth => Projectile.ai[0] <= 0f ? 280f : Projectile.ai[0];
        private int HoldTime => (int)(Projectile.ai[1] <= 0f ? 360f : Projectile.ai[1]);
        private int DotDamage => Projectile.ai[2] <= 0f ? 30 : (int)Projectile.ai[2];
        private int TotalLife => GrowTime + HoldTime + DrainTime;

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>铺开进度 0~1</summary>
        private float Spread => MathHelper.Clamp(Timer / GrowTime, 0f, 1f);
        /// <summary>排空进度 0~1</summary>
        private float Drain => MathHelper.Clamp((Timer - GrowTime - HoldTime) / DrainTime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>当前生效的扁平判定带</summary>
        private Rectangle FieldRect() {
            float halfW = PoolWidth * 0.5f * Spread * (1f - Drain * 0.8f);
            return new Rectangle(
                (int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - 34f),
                (int)(halfW * 2f), 44);
        }

        public override void AI() {
            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 600;

            Rectangle band = FieldRect();
            float strength = Spread * (1f - Drain);
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;
            bool dotTick = authority && Timer % DotInterval == 0 && Drain < 1f;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.dontTakeDamage) {
                    continue;
                }
                if (!band.Intersects(npc.Hitbox)) {
                    continue;
                }

                //减速=位移回滚：不复利、不碰velocity同步字段，各端确定性一致
                bool bossTier = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];
                float slow = (bossTier ? 0.12f : 0.35f) * strength;
                npc.position -= npc.velocity * slow;

                //持续伤害：权威端周期结算
                if (dotTick) {
                    npc.SimpleStrikeNPC(DotDamage, 0, false, 0f, null, false, 0f, true);
                }

                //受困敌人身上的凝胶滴淌(客户端)
                if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_BKSGelBead>(
                        npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)),
                        Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, Main.rand.NextFloat()) * 0.8f,
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(18, 30));
                }
            }

            //池面冒泡+偶发金光(客户端)
            if (!VaultUtils.isServer && Drain < 0.5f) {
                if (Main.rand.NextBool(8)) {
                    KingSlimeGelFX.BubbleFizz(Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * PoolWidth * Spread, -10f), 8f, 1);
                }
                if (Main.rand.NextBool(26)) {
                    KingSlimeGelFX.GoldGlint(Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-0.35f, 0.35f) * PoolWidth * Spread, -8f), 1, 2.5f);
                }
            }

            Lighting.AddLight(Projectile.Center,
                Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.CrownGold, 0.3f).ToVector3()
                * 0.4f * strength);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.BKSGelPool?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                //着色器不可用：扁平凝胶渍回退，绝不许无形领域
                Texture2D blob = CWRAsset.Extra_98?.Value;
                if (blob != null) {
                    float w = PoolWidth * Spread * (1f - Drain * 0.8f);
                    Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.CrownGold, 0.25f) * 0.6f;
                    Main.EntitySpriteDraw(blob, Projectile.Center - Main.screenPosition - new Vector2(0f, 6f), null,
                        gel, 0f, blob.Size() * 0.5f,
                        new Vector2(w / blob.Width, 26f / blob.Height), SpriteEffects.None, 0);
                }
                return false;
            }

            KingSlimeGelFX.SetPoolParams(shader,
                spread: Spread,
                drain: Drain,
                alpha: 0.9f,
                boil: 0.35f,
                seed: Projectile.identity * 0.137f % 1f);
            //泡沫改鎏金：王的凝胶，与Boss敌对池一眼区分
            shader.Parameters["uColorFoam"]?.SetValue(
                Color.Lerp(KingSlimeGelFX.GelFoam, KingSlimeGelFX.CrownGold, 0.55f).ToVector3());

            Vector2 quadSize = new Vector2(PoolWidth * 1.05f, 62f);
            KingSlimeGelFX.DrawShaderQuad(shader, noise,
                Projectile.Center + new Vector2(0f, -quadSize.Y * 0.5f + 12f), quadSize, 1f);
            return false;
        }
    }
}
