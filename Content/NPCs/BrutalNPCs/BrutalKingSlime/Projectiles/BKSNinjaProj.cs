using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 体内忍者的影身。ai[0]=宿主whoAmI ai[1]=招式(0左袭 1右袭 2天袭 3死亡演出逃逸)<br/>
    /// 影步冲出→定格亮刃→三连斩(天袭附手里剑扇)→化影收回；本体无伤害，威胁在斩波；服务端生成
    /// </summary>
    internal class BKSNinjaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int DashTime = 8;
        private const int PoseTime = 15;
        private const int StrikeTime = 26;
        private const int ReturnTime = 10;
        private const int TotalLife = DashTime + PoseTime + StrikeTime + ReturnTime;
        private const int EscapeLife = 170;

        private NPC Host => (int)Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.maxNPCs
            ? Main.npc[(int)Projectile.ai[0]] : null;

        private int Style => (int)Projectile.ai[1];

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>定格点(冲刺终点)，由初速与冲刺时长唯一确定</summary>
        private Vector2 posePoint;
        private bool poseInit;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 20;
        }

        public override void AI() {
            //死亡演出逃逸：沿地奔逃，蹦跳两下，渐隐
            if (Style == 3) {
                UpdateEscape();
                return;
            }

            NPC host = Host;
            if (host == null || !host.active || host.type != NPCID.KingSlime) {
                Projectile.Kill();
                return;
            }

            if (!poseInit) {
                poseInit = true;
                posePoint = Projectile.Center + Projectile.velocity * DashTime;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.3f, Volume = 0.8f, MaxInstances = 3 }, Projectile.Center);
            }

            Timer++;

            if (Timer <= DashTime) {
                //影步：全速直线，残影由绘制层处理
                Projectile.rotation = Projectile.velocity.X * 0.02f;
            }
            else if (Timer <= DashTime + PoseTime) {
                //定格亮刃
                Projectile.velocity = Vector2.Zero;
                Projectile.Center = posePoint;

                //刃光预告
                if (!VaultUtils.isServer && (int)Timer == DashTime + 4) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f, Volume = 0.7f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            else if (Timer <= DashTime + PoseTime + StrikeTime) {
                //出招窗口：服务端排布斩波
                Projectile.velocity = Vector2.Zero;
                int strikeT = (int)Timer - DashTime - PoseTime;
                if (!VaultUtils.isClient && host.target >= 0) {
                    Player target = Main.player[host.target];
                    int dmg = (int)(host.defDamage * 0.42f);
                    //三连斩：0/8/16帧，角度绕目标错开
                    if (strikeT == 1 || strikeT == 9 || strikeT == 17) {
                        int slashIdx = strikeT / 8;
                        Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        float spread = (slashIdx - 1) * 0.5f;
                        Vector2 dir = toTarget.RotatedBy(spread);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + dir * 46f, dir * 2.2f,
                            ModContent.ProjectileType<BKSNinjaSlashProj>(), dmg, 0f, Main.myPlayer,
                            dir.ToRotation(), slashIdx);
                    }
                    //天袭追加手里剑扇
                    if (Style == 2 && strikeT == 12) {
                        Vector2 baseDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        for (int i = -2; i <= 2; i++) {
                            Vector2 dir = baseDir.RotatedBy(i * 0.22f);
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, dir * 13.5f,
                                ModContent.ProjectileType<BKSShurikenProj>(), (int)(host.defDamage * 0.35f), 0f, Main.myPlayer);
                        }
                    }
                }
            }
            else {
                //化影收回宿主
                float t = (Timer - DashTime - PoseTime - StrikeTime) / ReturnTime;
                Projectile.velocity = (host.Center - Projectile.Center) * MathHelper.Clamp(0.12f + t * 0.35f, 0.12f, 0.5f);
                Projectile.alpha = (int)(t * 200f);
            }

            if (Timer >= TotalLife || (Timer > DashTime + PoseTime + StrikeTime && Projectile.Distance(host.Center) < 30f)) {
                if (!VaultUtils.isServer) {
                    KingSlimeGelFX.GelSplatter(host.Center, Vector2.UnitY * -1f, 4, 3f, 0.7f);
                }
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.25f, 0.3f, 0.5f));
        }

        /// <summary>逃逸：贴地奔跑+偶尔跃步，末段渐隐；纯演出</summary>
        private void UpdateEscape() {
            Timer++;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 30);

            //重力+贴地
            Projectile.velocity.Y += 0.42f;
            if (Projectile.velocity.Y > 14f) {
                Projectile.velocity.Y = 14f;
            }
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(Projectile.Center, 8);
            bool onGround = Projectile.Center.Y + 16f >= ground.Y;
            if (onGround && Projectile.velocity.Y > 0f) {
                Projectile.Center = new Vector2(Projectile.Center.X, ground.Y - 16f);
                Projectile.velocity.Y = 0f;
                //每隔一段小跃步
                if ((int)Timer % 46 == 12) {
                    Projectile.velocity.Y = -6.5f;
                }
            }
            //保持奔逃横速(方向由生成时初速决定)
            float runDir = Projectile.velocity.X >= 0f ? 1f : -1f;
            Projectile.velocity.X = runDir * 9.5f;
            Projectile.rotation = Projectile.velocity.X * 0.02f;

            //奔逃尘土
            if (!VaultUtils.isServer && onGround && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Bottom - new Vector2(4f, 4f), 8, 4,
                    DustID.Smoke, 0, 0, 140, default, 0.8f);
                d.velocity = new Vector2(-runDir * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(0.2f, 0.8f));
            }

            if (Timer > EscapeLife - 40) {
                Projectile.alpha = (int)MathHelper.Clamp((Timer - (EscapeLife - 40)) / 40f * 255f, 0f, 255f);
            }
            if (Timer >= EscapeLife) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ninja = TextureAssets.Ninja.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = ninja.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;

            //逃逸样式：本色小人贴地奔逃，无影身装饰
            if (Style == 3) {
                SpriteEffects runFlip = Projectile.velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
                //奔跑残影一丝
                Main.EntitySpriteDraw(ninja, pos - Projectile.velocity * 1.2f, null, lit * (0.25f * fade),
                    Projectile.rotation, origin, 1f, runFlip, 0);
                Main.EntitySpriteDraw(ninja, pos, null, lit * fade, Projectile.rotation, origin, 1f, runFlip, 0);
                return false;
            }

            SpriteEffects flip = Projectile.velocity.X < 0f || (Host?.Center.X ?? 0f) > Projectile.Center.X
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            bool posing = Timer > DashTime && Timer <= DashTime + PoseTime;
            bool striking = Timer > DashTime + PoseTime && Timer <= DashTime + PoseTime + StrikeTime;

            //冲刺残影链
            if (Timer <= DashTime + 4) {
                for (int i = 1; i <= 5; i++) {
                    Vector2 ghost = pos - Projectile.velocity * (i * 1.4f);
                    Main.EntitySpriteDraw(ninja, ghost, null, new Color(30, 34, 60, 0) * (0.5f - i * 0.08f) * fade,
                        Projectile.rotation, origin, 1f, flip, 0);
                }
            }

            //暗色影身
            Color shade = new Color(24, 26, 44) * (0.94f * fade);
            Main.EntitySpriteDraw(ninja, pos, null, shade, Projectile.rotation, origin, 1f, flip, 0);

            //冷青轮廓光：定格时亮起，出招时锐利
            float rim = posing ? MathHelper.Clamp((Timer - DashTime) / 6f, 0f, 1f) : striking ? 1f : 0.25f;
            Color rimColor = new Color(150, 200, 255, 0) * (rim * 0.6f * fade);
            for (int i = 0; i < 4; i++) {
                Vector2 off = (MathHelper.PiOver2 * i).ToRotationVector2() * 1.6f;
                Main.EntitySpriteDraw(ninja, pos + off, null, rimColor, Projectile.rotation, origin, 1f, flip, 0);
            }

            //定格期眼缝亮线
            if (posing || striking) {
                Texture2D pixel = InnoVault.VaultAsset.placeholder2?.Value;
                if (pixel != null) {
                    Vector2 eyePos = pos + new Vector2(flip == SpriteEffects.FlipHorizontally ? 5f : -5f, -8f);
                    Main.spriteBatch.Draw(pixel, eyePos, null, new Color(210, 240, 255, 0) * (rim * 0.9f * fade), 0f,
                        pixel.Size() * 0.5f, new Vector2(9f / pixel.Width, 2f / pixel.Height), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
