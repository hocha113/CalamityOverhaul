using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 分裂凝胶体。ai[0]=宿主whoAmI ai[1]=席位0~4 ai[2]=0环绕合围/1回聚<br/>
    /// 环绕期错相围跳压制，回聚令下加速吸回本体；服务端生成
    /// </summary>
    internal class BKSSplitSlimeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC Host => (int)Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.maxNPCs
            ? Main.npc[(int)Projectile.ai[0]] : null;

        private int Slot => (int)Projectile.ai[1];
        private bool Converging => Projectile.ai[2] == 1f;

        private ref float HopCooldown => ref Projectile.localAI[0];
        private ref float SquashSpring => ref Projectile.localAI[1];

        private float SizeScale => 0.62f + Slot % 3 * 0.16f;

        public override void SetDefaults() {
            Projectile.width = 52;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC host = Host;
            bool hostValid = host != null && host.active && host.type == NPCID.KingSlime;
            if (!hostValid) {
                Projectile.Kill();
                return;
            }

            //压扁弹簧回中
            SquashSpring = MathHelper.Lerp(SquashSpring, 0f, 0.14f);

            if (Converging) {
                UpdateConverge(host);
                return;
            }

            UpdateOrbitHop(host);
        }

        private void UpdateOrbitHop(NPC host) {
            Player target = Main.player[host.target];
            Projectile.tileCollide = true;

            //重力
            Projectile.velocity.Y += 0.42f;
            if (Projectile.velocity.Y > 15f) {
                Projectile.velocity.Y = 15f;
            }

            bool grounded = Projectile.velocity.Y == 0f;
            if (grounded) {
                Projectile.velocity.X *= 0.82f;
                if (HopCooldown > 0f) {
                    HopCooldown--;
                }
                else if (target.Alives()) {
                    //围绕目标的席位点：五席错相慢旋
                    float angle = Slot * MathHelper.TwoPi / 5f + Main.GameUpdateCount * 0.006f;
                    Vector2 slotPoint = target.Center + angle.ToRotationVector2() * 250f;
                    float dx = slotPoint.X - Projectile.Center.X;
                    //够近就直接压向玩家
                    if (Math.Abs(dx) < 130f) {
                        dx = target.Center.X - Projectile.Center.X;
                    }
                    float vx = MathHelper.Clamp(dx * 0.02f, -10f, 10f);
                    if (Math.Abs(vx) < 4.5f) {
                        vx = Math.Sign(dx) * 4.5f;
                    }
                    Projectile.velocity = new Vector2(vx, -9.6f - Slot % 2 * 1.6f);
                    HopCooldown = 30f + Slot * 6f;
                    SquashSpring = 0.5f;
                    KingSlimeGelFX.SquishSound(Projectile.Center, 0.2f + Slot * 0.05f, 0.4f);
                }
            }

            //空中轻微追向
            if (!grounded && target.Alives()) {
                float steer = MathHelper.Clamp((target.Center.X - Projectile.Center.X) * 0.0004f, -0.06f, 0.06f);
                Projectile.velocity.X += steer;
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.GelMid.ToVector3() * 0.3f * SizeScale);
        }

        private void UpdateConverge(NPC host) {
            //回聚：穿墙加速吸向本体
            Projectile.tileCollide = false;
            float speed = MathHelper.Clamp(6f + Projectile.localAI[2] * 0.65f, 6f, 30f);
            Projectile.localAI[2]++;
            Projectile.velocity = (host.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;

            //拖尾凝胶
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(Projectile.Center, -Projectile.velocity * 0.08f,
                    KingSlimeGelFX.GelMid * 0.7f, Main.rand.NextFloat(0.5f, 1f) * SizeScale)?.Configure(16);
            }

            if (Projectile.Distance(host.Center) < 42f) {
                //融回：溅射+挤压声，本体侧的膨胀由状态感知处理
                if (!VaultUtils.isServer) {
                    KingSlimeGelFX.GelSplatter(Projectile.Center, -Projectile.velocity.SafeNormalize(Vector2.UnitY), 7, 5f, SizeScale);
                    KingSlimeGelFX.SquishSound(Projectile.Center, -0.15f, 0.75f);
                }
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地压扁+小溅射，不销毁
            if (oldVelocity.Y > 3f) {
                SquashSpring = MathHelper.Clamp(oldVelocity.Y * 0.05f, 0.2f, 0.6f);
                if (!VaultUtils.isServer && oldVelocity.Y > 6f) {
                    KingSlimeGelFX.LandingBurst(Projectile.Bottom, oldVelocity.Y * 0.6f, SizeScale * 0.7f);
                }
            }
            if (Math.Abs(oldVelocity.X) > 0.5f && Projectile.velocity.X == 0f) {
                Projectile.velocity.X = 0f;
            }
            if (oldVelocity.Y != 0f && Projectile.velocity.Y != 0f) {
                Projectile.velocity.Y = 0f;
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.BlueSlime);
            Texture2D tex = TextureAssets.Npc[NPCID.BlueSlime].Value;
            int frameCount = Main.npcFrameCount[NPCID.BlueSlime];
            //空中用拉伸帧，地面用蹲帧
            int frame = Projectile.velocity.Y != 0f ? 1 : 0;
            if (frame >= frameCount) {
                frame = 0;
            }
            Rectangle rec = tex.GetRectangle(frame, frameCount);
            Vector2 origin = new Vector2(rec.Width * 0.5f, rec.Height);
            Vector2 pos = Projectile.Bottom - Main.screenPosition + new Vector2(0f, 4f);

            //压扁形变
            float squash = 1f - SquashSpring;
            float scaleX = SizeScale * (1f + SquashSpring * 0.9f);
            float scaleY = SizeScale * squash;
            //纵速拉伸
            float stretch = MathHelper.Clamp(Math.Abs(Projectile.velocity.Y) * 0.02f, 0f, 0.3f);
            scaleY *= 1f + stretch;
            scaleX *= 1f - stretch * 0.5f;

            Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.2f) * 0.85f;
            //按环境亮度压暗，保底0.4防全黑
            Color envLight = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            float lum = (envLight.R + envLight.G + envLight.B) / 765f;
            Color lit = gel * (0.4f + lum * 0.6f);

            Main.EntitySpriteDraw(tex, pos, rec, lit, 0f, origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            //高光内芯
            Main.EntitySpriteDraw(tex, pos - new Vector2(0f, 3f), rec, KingSlimeGelFX.GelFoam with { A = 0 } * 0.18f, 0f,
                origin, new Vector2(scaleX * 0.7f, scaleY * 0.7f), SpriteEffects.None, 0);
            return false;
        }
    }
}
