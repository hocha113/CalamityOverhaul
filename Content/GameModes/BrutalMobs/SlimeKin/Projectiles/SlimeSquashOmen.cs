using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin.Projectiles
{
    /// <summary>
    /// 蓄力压扁预告体（预告即实体，全端同步）。跟随宿主史莱姆绘制压缩凝胶与地面蓄力圈，
    /// 永不造成伤害；ai[0]=宿主 whoAmI，ai[1]=锁定跃向角（预告即承诺的可视化），ai[2]=凝胶色
    /// </summary>
    internal class SlimeSquashOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>跃向指示点数量</summary>
        private const int AimDotCount = 3;

        private NPC Host {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                //槽位索引不是跨端身份：宿主死后槽位被复用时按类型拒认，避免把蓄力预告画到无关怪身上
                return npc.active && SlimeKinNPC.SlimeTypes.Contains(npc.type) ? npc : null;
            }
        }

        private float AimAngle => Projectile.ai[1];
        private Color Gel => SlimeKinFlavor.UnpackColor(Projectile.ai[2]);
        /// <summary>蓄力进度 0→1</summary>
        private float Progress => 1f - Projectile.timeLeft / (float)SlimeKinNPC.TelegraphFrames;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SlimeKinNPC.TelegraphFrames;
            Projectile.netImportant = true;
        }

        /// <summary>预告体永无伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            NPC host = Host;
            if (host == null) {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.5f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //贴住宿主（npc 位置原生同步，各端一致）
            Projectile.Center = host.Center;

            //蓄力汇聚粉尘：从外圈吸向体心
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 from = host.Center + Main.rand.NextVector2CircularEdge(30f + host.width * 0.5f, 22f);
                Dust dust = Dust.NewDustPerfect(from, DustID.t_Slime,
                    (host.Center - from) * 0.09f, 120, Gel, 0.9f + 0.5f * Progress);
                dust.noGravity = true;
            }

            Lighting.AddLight(host.Center, Gel.ToVector3() * 0.25f * Progress);
        }

        public override void OnKill(int timeLeft) {
            //自然走完 = 起跳帧：弹起音 + 出膛粉尘（宿主中途死亡的提前销毁不播）
            if (timeLeft > 0 || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.4f, Volume = 0.75f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.t_Slime,
                    Main.rand.NextVector2Circular(3.5f, 2f) - Vector2.UnitY * 2f, 110, Gel, 1.2f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC host = Host;
            if (host == null) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float p = Progress;
            float ease = p * p;
            Color gel = Gel;
            //加速脉动传达"正在蓄压"
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (10f + 14f * ease) + Projectile.identity);

            //宿主体型归一
            Vector2 hostScale = new Vector2(host.width / (float)tex.Width, host.height / (float)tex.Height);
            Vector2 groundPos = host.Bottom - Main.screenPosition;

            //地面蓄力圈：真 alpha 暗层打底 + 加色亮边
            Main.EntitySpriteDraw(tex, groundPos, null, gel * (0.18f + 0.38f * ease), 0f, origin,
                new Vector2(hostScale.X * (1.7f + 1.3f * ease), 0.30f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, groundPos, null, (gel with { A = 0 }) * (0.3f * ease * pulse), 0f, origin,
                new Vector2(hostScale.X * (1.5f + 1.1f * ease), 0.22f), SpriteEffects.None, 0);

            //压扁体：凝胶越压越扁、越压越宽
            float squashY = MathHelper.Lerp(1.0f, 0.45f, ease);
            float squashX = MathHelper.Lerp(1.0f, 1.45f, ease);
            Vector2 bodyPos = groundPos - new Vector2(0f, host.height * squashY * 0.5f);
            Vector2 bodyScale = new Vector2(hostScale.X * 1.25f * squashX, hostScale.Y * 1.1f * squashY);
            Main.EntitySpriteDraw(tex, bodyPos, null, gel * (0.30f + 0.30f * ease), 0f, origin,
                bodyScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, bodyPos, null, (Color.Lerp(gel, Color.White, 0.35f) with { A = 0 }) * (0.25f * ease * pulse),
                0f, origin, bodyScale * 0.6f, SpriteEffects.None, 0);

            //跃向指示：沿锁定角逐点点亮（锁定后不再改向）
            Vector2 dir = AimAngle.ToRotationVector2();
            for (int i = 1; i <= AimDotCount; i++) {
                float lit = MathHelper.Clamp(p * (AimDotCount + 1) - i, 0f, 1f);
                if (lit <= 0f) {
                    continue;
                }
                Vector2 dotPos = host.Center + dir * (host.width * 0.6f + 24f * i) - Main.screenPosition;
                Main.EntitySpriteDraw(tex, dotPos, null, (gel with { A = 0 }) * (0.5f * lit * pulse), 0f, origin,
                    new Vector2(0.10f + 0.03f * i, 0.07f + 0.02f * i), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
