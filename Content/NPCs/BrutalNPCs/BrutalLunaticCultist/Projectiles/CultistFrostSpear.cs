using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 霜晶枪：vanilla 349 霜碎晶做体<br/>
    /// ai[0]=晶列朝向 ai[1]=模式(0立柱 1碎片) ai[2]=生长延迟帧<br/>
    /// 立柱：20 帧无害生长（预告即实体）→ 驻场拒止 → 崩解沿列向放出碎片（轨迹已声明，公平阀）
    /// </summary>
    internal class CultistFrostSpear : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FrostShard;

        private float RayAngle => Projectile.ai[0];
        private bool IsShard => Projectile.ai[1] == 1f;
        private int GrowDelay => (int)Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        private const int GrowFrames = 20;
        private const int StandFrames = 112;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>生长完成度 0~1</summary>
        private float GrowT => IsShard ? 1f
            : MathHelper.Clamp((Timer - GrowDelay) / GrowFrames, 0f, 1f);

        public override void AI() {
            Timer++;

            //帧变体固定：349 的 5 帧是形状变体，不是动画
            Projectile.frame = Projectile.identity % Main.projFrames[Type];

            if (IsShard) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                Projectile.velocity *= 1.012f;
                if (Projectile.timeLeft > 90) {
                    Projectile.timeLeft = 90;
                }
            }
            else {
                //立柱沿晶列朝向立着
                Projectile.rotation = RayAngle + MathHelper.PiOver2;
                Projectile.velocity = Vector2.Zero;

                //延迟未到：完全蛰伏（逐节点亮的列队感）
                if (Timer < GrowDelay) {
                    return;
                }
                if (Timer == GrowDelay && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
                    CultistMotion.ImpactBurst(Projectile.Center, 1, 0.4f, playSound: false);
                }

                //驻场结束：崩解
                if (Timer > GrowDelay + GrowFrames + StandFrames) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.FrostCore.ToVector3() * 0.35f * GrowT);

            //驻场冰屑
            if (!VaultUtils.isServer && GrowT >= 1f && Main.rand.NextBool(14) && CultistMotion.OnScreen(Projectile.Center, 100f)) {
                PRTLoader.NewParticle<PRT_CultistFrostMote>(Projectile.Center + Main.rand.NextVector2Circular(10f, 16f),
                    new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)), CultistMotion.FrostCore, Main.rand.NextFloat(0.5f, 0.9f))?
                    .Configure(Main.rand.Next(18, 28));
            }
        }

        /// <summary>生长期无害：预告阶段不咬人</summary>
        public override bool CanHitPlayer(Player target) => GrowT >= 1f;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Frostburn2, 120);
        }

        public override void OnKill(int timeLeft) {
            CultistMotion.ImpactBurst(Projectile.Center, 1, IsShard ? 0.5f : 0.9f);

            //立柱崩解：沿已声明的列向放出 2 枚碎片，不折向玩家
            if (!IsShard && !VaultUtils.isClient && GrowT >= 1f) {
                for (int i = 0; i < 2; i++) {
                    float jitter = Main.rand.NextFloat(-0.07f, 0.07f);
                    Vector2 vel = (RayAngle + jitter).ToRotationVector2() * Main.rand.NextFloat(7.5f, 9.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        Type, (int)(Projectile.damage * 0.7f), 0f, Main.myPlayer, RayAngle, 1f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //蛰伏期不画
            if (!IsShard && Timer < GrowDelay) {
                return false;
            }

            Main.instance.LoadProjectile(ProjectileID.FrostShard);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.FrostShard].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int frameHeight = tex.Height / Main.projFrames[Type];
            Rectangle frame = new(0, frameHeight * Projectile.frame, tex.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            float grow = GrowT;
            float scale = IsShard ? 0.72f : 0.5f + grow * 1.05f;
            //生长期半透明幽蓝——看得见的"还没变硬"
            float solidity = 0.35f + grow * 0.65f;

            //底晕
            Main.EntitySpriteDraw(glow, pos, null, CultistMotion.FrostEdge with { A = 0 } * (0.4f * solidity),
                0f, glow.Size() * 0.5f, scale * 0.5f, SpriteEffects.None, 0);
            //vanilla 晶体
            Main.EntitySpriteDraw(tex, pos, frame, Color.White * solidity, Projectile.rotation, origin,
                scale, SpriteEffects.None, 0);
            //成形后的锋刃高光
            if (grow >= 1f) {
                float glint = 0.3f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity);
                Main.EntitySpriteDraw(tex, pos, frame, CultistMotion.FrostCore with { A = 0 } * glint,
                    Projectile.rotation, origin, scale * 0.92f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
