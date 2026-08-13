using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 霜牢晶枪：材质=凝晶寒冰（晶棱多面/凝结成形/棱面glint+碎屑剥落）；
    /// 凝晶前摇（锁定前跟瞄，末12帧锁死）→急速刺出（复合加速）；
    /// ai[0]=前摇帧 ai[1]=刺出速度；出生时 velocity 为归一化瞄准方向
    /// </summary>
    internal class CultistIceLance : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int AimLockLead = 12;
        private const float LancePx = 132f;
        //shader辉层长度：收进晶簇本体内（≈110px），避免光效反客为主
        private const float OverlayPx = 108f;
        private int TelegraphTime => Math.Max((int)Projectile.ai[0], 10);
        private float LaunchSpeed => Projectile.ai[1] > 0f ? Projectile.ai[1] : 19f;

        private bool launched;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 500;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];

            if (t == 1) {
                //缓存伤害，前摇期归零（公平阀）
                Projectile.localAI[1] = Projectile.damage;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 6 }, Projectile.Center);
                }
            }

            //同步端已进入飞行段（前摇期两端速度≤1，刺出后≥13）而本地仍在前摇：
            //对齐时间轴防重播——中途加入端会整段冻结重演前摇，正常端刺出包早到一拍
            //会吃一帧 position-=velocity 的回拽
            if (!launched && t <= TelegraphTime && Projectile.velocity.LengthSquared() > 4f) {
                if ((int)t == 1) {
                    //中途加入：跳过出膛演出，直接接管飞行段
                    launched = true;
                    Projectile.localAI[1] = Projectile.damage;
                }
                Projectile.localAI[0] = TelegraphTime + 1;
                t = TelegraphTime + 1;
            }

            if (!launched && t <= TelegraphTime) {
                //前摇期无伤害（判伤在受击玩家本端，本地门即可；服务端保持满伤害，
                //防跟瞄 netUpdate 同步包快照 0 伤毒化客户端缓存）
                if (!VaultUtils.isServer) {
                    Projectile.damage = 0;
                }
                Projectile.position -= Projectile.velocity;

                //锁定前服务端跟瞄
                if (!VaultUtils.isClient && t < TelegraphTime - AimLockLead) {
                    int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                    Player target = Main.player[idx];
                    if (target.Alives()) {
                        Vector2 aim = (target.Center + target.velocity * 10f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        Projectile.velocity = aim * 0.0001f;
                        if ((int)t % 10 == 0) {
                            Projectile.netUpdate = true;
                        }
                    }
                }

                Projectile.rotation = Projectile.velocity.ToRotation();

                //凝结中的霜雾被拉入晶体（材质签名：从雾中凝出）
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    Vector2 start = Projectile.Center + Main.rand.NextVector2CircularEdge(42f, 42f);
                    PRTLoader.NewParticle<PRT_CultistRune>(start, Vector2.Zero,
                        CultistPalette.IceBright, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Projectile.Center, 0.2f, 14);
                }
                //凝结咔嗒声（60%处一声脆响）
                if ((int)t == (int)(TelegraphTime * 0.6f) && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 6 }, Projectile.Center);
                }
                return;
            }

            //刺出帧：恢复伤害+出膛演出
            if (!launched) {
                launched = true;
                Projectile.damage = (int)Projectile.localAI[1];
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * LaunchSpeed;
                Projectile.timeLeft = 240;
                if (!VaultUtils.isClient) {
                    Projectile.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.75f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    CultistRenderHelper.CastBurst(Projectile.Center, dir, CultistElement.Ice, 1f);
                    //出膛：垂直碎霜环+尾向冰屑反喷（后坐语义）
                    for (int i = 0; i < 6; i++) {
                        Vector2 side = dir.RotatedBy(MathHelper.PiOver2 * (i % 2 == 0 ? 1 : -1))
                            * Main.rand.NextFloat(2f, 5f) - dir * Main.rand.NextFloat(1f, 3f);
                        PRTLoader.NewParticle<PRT_CultistShard>(Projectile.Center - dir * 20f, side,
                            CultistPalette.IceBright, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
            }

            //复合加速：刺出后持续增速（恒速飞行=失败）
            float speed = Projectile.velocity.Length();
            if (speed < LaunchSpeed * 1.7f) {
                Projectile.velocity *= 1.022f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //航迹霜雾+冰屑剥落（材质签名：晶体高速摩擦掉屑）
            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CultistFrost>(Projectile.Center - Projectile.velocity * 0.6f,
                        -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        CultistPalette.IceMain, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(18, 30));
                }
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_CultistShard>(Projectile.Center - Projectile.velocity * 0.3f,
                        -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                        CultistPalette.IceBright, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(20, 32));
                }
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.IceMain.ToVector3() * 0.5f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 120);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //命中：棱片爆散（前向锥形为主）+霜雾团滞留（余韵超弹体寿命）
            CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Ice, 1f);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, MaxInstances = 6 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 6 }, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2.5f, 9f);
                PRTLoader.NewParticle<PRT_CultistShard>(Projectile.Center, vel,
                    CultistPalette.IceBright, Main.rand.NextFloat(0.45f, 0.9f))?.Configure(Main.rand.Next(26, 44));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CultistFrost>(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    Main.rand.NextVector2Circular(1.4f, 1.4f) - Vector2.UnitY * 0.3f,
                    CultistPalette.IceMain, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(36, 56));
            }
        }

        /// <summary>
        /// 晶枪本体=原版霜晶349真实纹理拼簇：主晶居前、两枚侧晶错后（尖端沿枪轴），全亮绘制；
        /// grow 驱动三晶依次成形（先侧后主），调用方须处于实体绘制批
        /// </summary>
        private void DrawShardCluster(SpriteBatch sb, Vector2 worldPos, float grow, float alpha) {
            Main.instance.LoadProjectile(ProjectileID.FrostShard);
            Texture2D shard = TextureAssets.Projectile[ProjectileID.FrostShard].Value;
            //原版霜晶349五帧变体（竖排），identity 定型保证各端一致
            int fh = shard.Height / 5;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //像素实测：五帧均顶部锚定、占行0..19~27（双尖晶体），视觉中心取 y≈12
            Vector2 origin = new(shard.Width / 2f, 12f);
            //贴图长轴竖直，指向枪轴需 -PiOver2
            float rot = Projectile.rotation - MathHelper.PiOver2;
            Color body = new Color(255, 255, 255, 255) * alpha;

            //两枚侧晶：先成形，错后错侧，变体各异（单帧12×30，放大后仍窄长）
            float sideGrow = MathHelper.Clamp(grow * 1.6f, 0f, 1f);
            for (int s = -1; s <= 1; s += 2) {
                Rectangle src = new(0, (Projectile.identity + s + 2) % 5 * fh, shard.Width, fh);
                Vector2 pos = worldPos - dir * 32f + perp * (s * 10f);
                sb.Draw(shard, pos - Main.screenPosition, src, body * (0.9f * sideGrow),
                    rot + s * 0.1f, origin, new Vector2(1.7f, 1.9f) * sideGrow, SpriteEffects.None, 0f);
            }
            //主晶：后成形，居前拉长（≈22×90px，撑满枪身）
            float mainGrow = MathHelper.Clamp((grow - 0.25f) / 0.75f, 0f, 1f);
            if (mainGrow > 0.01f) {
                Rectangle src = new(0, Projectile.identity % 5 * fh, shard.Width, fh);
                sb.Draw(shard, worldPos + dir * 16f * mainGrow - Main.screenPosition, src, body * mainGrow,
                    rot, origin, new Vector2(1.9f, 3f) * mainGrow, SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float t = Projectile.localAI[0];
            float assemble = MathHelper.Clamp(t / TelegraphTime, 0f, 1f);
            float seed = Projectile.identity * 0.77f;

            if (!launched) {
                //瞄准预示线：亮头端藏在晶体下，渐淡端指向玩家（断口两端均软）
                float lockFlash = t > TelegraphTime - AimLockLead ? (t - (TelegraphTime - AimLockLead)) / AimLockLead : 0f;
                CultistRenderHelper.BeginAdditive(sb);
                Texture2D line = CWRAsset.LightShot.Value;
                sb.Draw(line, drawPos, null, CultistPalette.IceBright * (0.16f + 0.14f * assemble + 0.2f * lockFlash),
                    Projectile.rotation, new Vector2(0f, line.Height / 2f),
                    new Vector2(4.2f * assemble, 0.09f + 0.05f * lockFlash), SpriteEffects.None, 0f);
                //凝结期的冷雾垫底（外层媒介，≤30%视觉量）
                Texture2D glow = CWRAsset.SoftGlow.Value;
                sb.Draw(glow, drawPos, null, CultistPalette.IceDeep * (0.3f * assemble),
                    0f, glow.Size() / 2f, 0.55f * assemble, SpriteEffects.None, 0f);
                CultistRenderHelper.EndAdditive(sb);

                //本体基底：霜晶簇凝结成形（真实纹理），晶辉shader降级为叠加辉层
                DrawShardCluster(sb, Projectile.Center, assemble, 1f);
                CultistRenderHelper.DrawCrystal(sb, Projectile.Center, OverlayPx, Projectile.rotation,
                    assemble, lockFlash * 0.7f, seed, 0.4f);
            }
            else {
                //速度残影：两枚旧位置的低亮度晶簇（速度涂抹）
                for (int i = 2; i >= 1; i--) {
                    Vector2 ghost = Projectile.Center - Projectile.velocity * (i * 1.7f);
                    DrawShardCluster(sb, ghost, 1f, 0.28f / i);
                }

                //霜雾丝带尾（亮头藏在晶尾下，渐淡向后）
                CultistRenderHelper.BeginAdditive(sb);
                Texture2D ribbon = CWRAsset.LightShotAlt.Value;
                float speed = Projectile.velocity.Length();
                float ribbonLen = MathHelper.Clamp(speed * 9f, 90f, 260f);
                sb.Draw(ribbon, drawPos - Projectile.velocity.SafeNormalize(Vector2.UnitX) * LancePx * 0.32f, null,
                    CultistPalette.IceMain * 0.5f, Projectile.rotation + MathHelper.Pi,
                    new Vector2(0f, ribbon.Height / 2f), new Vector2(ribbonLen / ribbon.Width, 0.3f), SpriteEffects.None, 0f);
                CultistRenderHelper.EndAdditive(sb);

                //本体基底+叠加辉层：刺出后首4帧过曝
                float launchFlash = MathHelper.Clamp(1f - (t - TelegraphTime) / 4f, 0f, 1f);
                DrawShardCluster(sb, Projectile.Center, 1f, 1f);
                CultistRenderHelper.DrawCrystal(sb, Projectile.Center, OverlayPx, Projectile.rotation,
                    1f, launchFlash, seed, 0.45f);
            }
            return false;
        }
    }
}
