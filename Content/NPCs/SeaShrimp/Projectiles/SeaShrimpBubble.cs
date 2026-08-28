using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 泡幕气泡:慢速上升的可读威胁,横向水流摆(identity 确定性哈希,不掷随机)。
    /// 泡体走 FishronBubble 水膜材质(<see cref="SeaShrimpBubbleRender"/> 统一批绘),
    /// 触顶/寿尽先破膜 8 帧再消亡,破膜期无伤害(伤害窗=完整膜的可见窗)。
    /// ai[0]=半径,ai[1]=上升速度
    /// </summary>
    internal class SeaShrimpBubble : SeaShrimpModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private float Radius => Projectile.ai[0];

        /// <summary>破膜帧数:膜被噪声蚀开的可见过程</summary>
        private const int BurstFrames = 8;
        /// <summary>出生吹胀帧数</summary>
        private const int InflateFrames = 8;

        /// <summary>本地帧龄:逐端计数,吹胀与伤害窗用</summary>
        private int Age => (int)Projectile.localAI[0];
        /// <summary>破膜计数:0=完好,≥1=破膜第 n 帧(触发条件是确定性输入,各端一致)</summary>
        private int BurstAge => (int)Projectile.localAI[1];
        private bool Bursting => Projectile.localAI[1] > 0;

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //寿命压短:泡幕不得叠进下一招的弹幕图案(图案叠压的公平口径)
            Projectile.timeLeft = 260;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();

            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                Projectile.localAI[1]++;
                if (BurstAge > BurstFrames) {
                    Projectile.Kill();
                }
                return;
            }

            //横向水流摆:identity 定相位,各端一致
            float phase = Projectile.identity * 0.917f;
            Projectile.velocity.X = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + phase) * 0.55f;
            Projectile.velocity.Y = -Projectile.ai[1];
            Lighting.AddLight(Projectile.Center, 0.05f, 0.12f, 0.24f);

            //触顶或寿尽:先破膜再消亡(条件全是确定性输入)
            if (ShrimpTerrain.SolidAt(Projectile.Center - new Vector2(0f, Radius + 6f))
                || Projectile.timeLeft <= BurstFrames + 2) {
                StartBurst();
            }
        }

        private void StartBurst() {
            Projectile.localAI[1] = 1f;
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            //破膜水花:水珠抛物线四散,活得比弹体久(余痕规则)
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(0.8f, 2.4f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f);
                EverdeepVFX.ShedDroplet(Projectile.Center
                    + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f), vel, 0.8f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), SeaShrimpVFX.Body * 0.45f, Main.rand.NextFloat(0.4f, 0.6f))
                ?.Configure(Main.rand.Next(30, 48));
        }

        /// <summary>伤害窗=完整膜:吹胀成形前与破膜期都不伤人</summary>
        public override bool? CanDamage() => Age > 6 && !Bursting ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= Radius;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            //濒破抖动加剧:寿命尾段是可读的"要破了"预告
            float shiver = MathHelper.Clamp(1f - (Projectile.timeLeft - BurstFrames) / 46f, 0f, 1f);
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = Radius * MathHelper.Clamp(Age / (float)InflateFrames, 0.2f, 1f),
                Wobble = 0.45f + shiver * 0.4f,
                Arm = 0f,
                Burst = Bursting ? BurstAge / (float)BurstFrames : 0f,
                Fade = MathHelper.Clamp(Age / 5f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (SeaShrimpVFX.BubblePathReady) {
                //泡体由 SeaShrimpBubbleRender 统一批绘
                return false;
            }
            //着色器缺失回退:双环+高光
            Texture2D ring = RingTex?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null || Bursting) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wobble = 1f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            float scale = Radius * 2f / ring.Width * wobble;
            Main.spriteBatch.Draw(ring, pos, null, new Color(150, 200, 255) * 0.8f, 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(ring, pos, null, new Color(40, 70, 130) * 0.3f, 0f,
                ring.Size() * 0.5f, scale * 0.84f, SpriteEffects.None, 0f);
            if (glow != null) {
                Main.spriteBatch.Draw(glow, pos + new Vector2(-Radius * 0.32f, -Radius * 0.36f), null,
                    new Color(255, 255, 255, 0) * 0.45f, 0f, glow.Size() * 0.5f,
                    Radius * 0.42f / glow.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
