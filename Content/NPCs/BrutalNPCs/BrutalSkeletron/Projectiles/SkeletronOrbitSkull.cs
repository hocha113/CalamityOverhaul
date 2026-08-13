using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>
    /// 诅咒环轰颅火：绕拍捉囚笼环行，读秒后向心俯冲<br/>
    /// ai[0]=起始角，ai[1]=头 whoAmI，ai[2]=俯冲延迟；全部出生即定，轨迹各端确定性推演<br/>
    /// 环行期无伤害（环阵本身就是 telegraph），俯冲才咬人
    /// </summary>
    internal class SkeletronOrbitSkull : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Skull;

        /// <summary>环行半径</summary>
        internal const float OrbitRadius = 260f;
        /// <summary>基础俯冲延迟帧</summary>
        internal const float BaseDiveDelay = 26f;
        /// <summary>俯冲速度</summary>
        private const float DiveSpeed = 14.5f;

        private ref float StartAngle => ref Projectile.ai[0];
        private ref float HeadIndex => ref Projectile.ai[1];
        private ref float DiveDelay => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        private bool Diving => Age >= DiveDelay;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 220;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;

            //初段淡入（公平阀：出膛不打脸）
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / 10f, 0f, 1f));

            //宿主校验：头没了或抓取已中断 → 自燃消散
            NPC head = ResolveHead();
            if (head == null) {
                Projectile.alpha = Math.Min(Projectile.alpha + 26, 255);
                Projectile.velocity *= 0.9f;
                if (Projectile.alpha >= 250) {
                    Projectile.Kill();
                }
                return;
            }

            Vector2 cage = SkeletronPalmSnatchState.GetCageCenter(head);

            if (!Diving) {
                //确定性环行：轨道参数全由出生 ai[] 推演，各端一致
                float angle = StartAngle + Age * 0.085f;
                float radius = OrbitRadius - Age * 0.5f;
                Vector2 want = cage + angle.ToRotationVector2() * radius;
                Projectile.velocity = (want - Projectile.Center) * 0.35f;
                Projectile.rotation = (cage - Projectile.Center).ToRotation() + MathHelper.PiOver2;

                //俯冲前一瞬向心俯冲发射
                if (Age + 1f >= DiveDelay) {
                    Projectile.velocity = (cage - Projectile.Center).SafeNormalize(Vector2.UnitY) * DiveSpeed;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = -0.5f }, Projectile.Center);
                    }
                }
            }
            else {
                //俯冲：直线咬合，穿过囚笼后限时消散
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                if (Age > DiveDelay + 55f || Projectile.Center.Distance(cage) > 1700f) {
                    Projectile.Kill();
                    return;
                }
            }

            //三帧循环
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //幽火剥落
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.16f,
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostDeep.ToVector3() * 0.5f);
        }

        /// <summary>仍在持人的拍捉头；无效返回 null</summary>
        private NPC ResolveHead() {
            int index = (int)HeadIndex;
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC head = Main.npc[index];
            if (!head.active || head.type != NPCID.SkeletronHead) {
                return null;
            }
            if ((int)head.ai[SkeletronAiSlots.HeadStateSlot] != (int)SkeletronStateIndex.PalmSnatch
                || head.ai[SkeletronAiSlots.HeadParamA] <= 0f) {
                return null;
            }
            return head;
        }

        /// <summary>环行期不咬人，俯冲且已显形才有伤害</summary>
        public override bool? CanDamage() {
            if (!Diving || Projectile.alpha > 100) {
                return false;
            }
            return null;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.6f, 2.6f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.6f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.GetRectangle(Projectile.frame, Main.projFrames[Type]);
            Vector2 orig = rect.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //拖尾残影（预乘批 A=0 加色）
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.4f * opacity;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, rect,
                    SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.CurseViolet) * fade,
                    Projectile.oldRot[i], orig, Projectile.scale * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //三层幽灵体：外层带诅咒紫（与普通颅火区别的环轰身份色）
            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.CurseViolet * (0.8f * opacity),
                Projectile.rotation, orig, Projectile.scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.GhostCyan * (0.85f * opacity),
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, new Color(230, 255, 250, 0) * (0.6f * opacity),
                Projectile.rotation, orig, Projectile.scale * 0.82f, SpriteEffects.None, 0);
            return false;
        }
    }
}
