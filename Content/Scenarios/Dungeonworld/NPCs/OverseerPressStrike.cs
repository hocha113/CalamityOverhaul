using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 压印锤：光柱预告与打击体共置一枚弹幕。ai[0]=预告帧数（P1 24 / P2 18 / 摆压 14），
    /// ai[1]=主人 whoAmI，全部随 spawn 包原子过线，锁定 x 即出生 x（光柱出现即承诺）。
    /// 时间线：光柱预告（无伤，宽=锤宽同值）→ 快照式下落（唯一伤害窗）→ 触底停 10f
    /// （贴锤输出窗）→ 收回（无伤）→ 消散。三声递进气锤声压在预告拍上。
    /// 落点地板由出生位向下确定性扫描，各端一致；全程不改写 damage/timeLeft
    /// </summary>
    internal class OverseerPressStrike : OverseerModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>锤宽=光柱宽（gap 视觉同一性）</summary>
        internal const float HammerWidth = 46f;
        private const int FloorDwellFrames = 10;
        private const float RetractSpeed = 8f;

        private int Windup => Math.Max(8, (int)Projectile.ai[0]);
        private int OwnerIndex => (int)Projectile.ai[1];

        /// <summary>本地时间线（各端从收包起本地推进；迟入场只多看一段预告，无害）</summary>
        private ref float Life => ref Projectile.localAI[0];
        /// <summary>相位：0 预告 / 1 下落 / 2 触底停 / 3 收回（本地确定性推进）</summary>
        private ref float Phase => ref Projectile.localAI[1];

        private float spawnY;
        private float floorY;
        private bool geomInit;
        private int dwellT;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;
            if (!geomInit) {
                //落点几何：出生位向下扫地板（对同步 tile 确定性一致）
                geomInit = true;
                spawnY = Projectile.Center.Y;
                int tx = (int)(Projectile.Center.X / 16f);
                int ty = Math.Max(4, (int)(Projectile.Center.Y / 16f));
                floorY = spawnY + 400f;
                for (int k = 0; k < 60; k++) {
                    if (ty + k >= Main.maxTilesY - 20) {
                        break;
                    }
                    if (WorldGen.SolidTile(tx, ty + k)) {
                        floorY = (ty + k) * 16f;
                        break;
                    }
                }
            }

            int t = (int)Life;
            if ((int)Phase == 0) {
                Projectile.velocity = Vector2.Zero;
                //三声递进气锤声（预告拍）
                if (t == 2 || t == Windup / 2 || t == Windup - 4) {
                    SoundEngine.PlaySound(SoundID.Mech with {
                        Volume = 0.4f + t * 0.01f,
                        Pitch = -0.6f + t * 0.02f,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
                if (t >= Windup) {
                    Phase = 1;
                    Projectile.velocity.Y = 5f;
                }
                return;
            }

            if ((int)Phase == 1) {
                //快照式下落：复利加速到 34px/f
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y * 1.35f, 34f);
                if (Projectile.Center.Y + Projectile.height * 0.5f >= floorY) {
                    Phase = 2;
                    Projectile.Center = new Vector2(Projectile.Center.X, floorY - Projectile.height * 0.5f);
                    Projectile.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                    ShakeNearby(2.5f);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 8; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(
                                Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), 16f),
                                new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1f, 3.5f)),
                                Color.Lerp(FoundryOverseer.FurnaceOrange, Color.White, Main.rand.NextFloat(0.5f)),
                                Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
                        }
                    }
                }
                return;
            }

            if ((int)Phase == 2) {
                //锤到底停（贴锤输出窗，无伤）
                Projectile.velocity = Vector2.Zero;
                if (++dwellT >= FloorDwellFrames) {
                    Phase = 3;
                }
                return;
            }

            //收回：无伤上行，回到出生高即消散
            Projectile.velocity.Y = -RetractSpeed;
            if (Projectile.Center.Y <= spawnY) {
                Projectile.Kill();
            }
        }

        /// <summary>伤害窗严格对齐可见打击：只在下落段开</summary>
        public override bool? CanDamage() => (int)Phase == 1 ? null : false;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    FoundryOverseer.SteamWhite * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        private void ShakeNearby(float amount, float range = 1100f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        //==================== 绘制：光柱预告 → 链柱 → 石拳锤头 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.GolemFistLeft);
            Texture2D fistTex = TextureAssets.Npc[NPCID.GolemFistLeft]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fistTex == null || chainTex == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //光柱预告（加色，宽=锤宽；强度随预告进度爬升，永不 A=0 染色）
            if ((int)Phase == 0 && geomInit) {
                float charge = MathHelper.Clamp(Life / Windup, 0f, 1f);
                float colH = floorY - Projectile.Center.Y;
                Vector2 colCenter = new(Projectile.Center.X, Projectile.Center.Y + colH * 0.5f);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(glow, colCenter - Main.screenPosition, null,
                    FoundryOverseer.FurnaceOrange * (0.18f + 0.3f * charge), 0f, glow.Size() * 0.5f,
                    new Vector2(HammerWidth * 2f / glow.Width, colH * 2.2f / glow.Height), SpriteEffects.None, 0f);
                //落点脚线加亮（预告指向真实落点）
                sb.Draw(glow, new Vector2(Projectile.Center.X, floorY - 4f) - Main.screenPosition, null,
                    FoundryOverseer.FurnaceOrange * (0.35f + 0.35f * charge), 0f, glow.Size() * 0.5f,
                    new Vector2(HammerWidth * 2.4f / glow.Width, 10f * 2f / glow.Height), SpriteEffects.None, 0f);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //锤臂链柱：从出生高垂到锤头
            Undrowned.DrawChainLine(sb, chainTex,
                new Vector2(Projectile.Center.X, spawnY - 24f),
                Projectile.Center + new Vector2(0f, -14f), lightColor, 1f);

            //锤头：石拳重染工业灰橙（暗缘压边）
            int count = Math.Max(1, Main.npcFrameCount[NPCID.GolemFistLeft]);
            Rectangle frame = new(0, 0, fistTex.Width, fistTex.Height / count);
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            sb.Draw(fistTex, pos, frame, FoundryOverseer.IronDeep * 0.8f,
                MathHelper.PiOver2, origin, 1.15f, SpriteEffects.None, 0f);
            sb.Draw(fistTex, pos, frame, lightColor.MultiplyRGB(FoundryOverseer.IronMul),
                MathHelper.PiOver2, origin, 1.05f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
