using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

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

        public override void SetStaticDefaults() {
            //下落残影缓存（不注册 oldPos 恒零）
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        /// <summary>迟入场快进：服务器权威时间线随弹幕同步包（含迟入快照）过线，
        /// 本地只允许向前追帧（单调闩，快照重放不回卷相位）。原版同步不带 timeLeft，
        /// 用 timeLeft 反推经历帧在迟入端恒得 0，此处必须显式过线</summary>
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)Life);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            short serverLife = reader.ReadInt16();
            if (serverLife > (int)Life) {
                Life = serverLife;
            }
        }

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
                //坠程风压：锤侧挤出的气流屑（客户端表现）
                if (!Main.dedServ && Projectile.velocity.Y > 12f && t % 2 == 0) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center + new Vector2(side * HammerWidth * 0.5f, Main.rand.NextFloat(-10f, 10f)),
                        new Vector2(side * Main.rand.NextFloat(0.8f, 1.8f), -Main.rand.NextFloat(0.2f, 0.8f)),
                        FoundryOverseer.SteamWhite * 0.35f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(10, 16));
                }
                if (Projectile.Center.Y + Projectile.height * 0.5f >= floorY) {
                    Phase = 2;
                    Projectile.Center = new Vector2(Projectile.Center.X, floorY - Projectile.height * 0.5f);
                    Projectile.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                    ShakeNearby(2.5f);
                    if (!Main.dedServ) {
                        Vector2 foot = new(Projectile.Center.X, floorY);
                        //火花 + 铁屑对开 + 贴地热印（触底的三层材质应答）
                        for (int k = 0; k < 8; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(
                                foot + new Vector2(Main.rand.NextFloat(-24f, 24f), -2f),
                                new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1f, 3.5f)),
                                Color.Lerp(FoundryOverseer.FurnaceOrange, Color.White, Main.rand.NextFloat(0.5f)),
                                Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
                        }
                        for (int k = 0; k < 6; k++) {
                            float dir = k % 2 == 0 ? 1f : -1f;
                            PRTLoader.NewParticle<PRT_OverseerIronChip>(
                                foot + new Vector2(dir * Main.rand.NextFloat(8f, 26f), -4f),
                                new Vector2(dir * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(2f, 5f)),
                                FoundryOverseer.IronMul, Main.rand.NextFloat(0.5f, 0.9f))
                                ?.Configure(Main.rand.Next(16, 26));
                        }
                        PRTLoader.NewParticle<PRT_OverseerHeatScar>(foot + new Vector2(0f, -3f),
                            Vector2.Zero, FoundryOverseer.SlagHot, 1f)
                            ?.Configure(56, HammerWidth * 1.5f);
                        //两侧挤压白汽
                        for (int k = 0; k < 4; k++) {
                            float dir = k % 2 == 0 ? 1f : -1f;
                            PRTLoader.NewParticle<PRT_GhostRainMist>(
                                foot + new Vector2(dir * HammerWidth * 0.55f, -6f),
                                new Vector2(dir * Main.rand.NextFloat(1f, 2.2f), -Main.rand.NextFloat(0.3f, 1f)),
                                FoundryOverseer.SteamWhite * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
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

        //==================== 绘制：行程标线（shader）→ 链柱 → 下落残影 → 铸铁锤头 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.GolemFistLeft);
            Texture2D fistTex = TextureAssets.Npc[NPCID.GolemFistLeft]?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fistTex == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            if ((int)Phase == 0 && geomInit) {
                DrawPressBeam(sb, glow);
            }

            //锤臂链柱：从出生高垂到锤头；下落段绷直高频振颤（受载的机械应答）
            float chainShiver = (int)Phase == 1 ? 1.6f : (int)Phase == 2 ? 0.8f : 0f;
            float chainSlack = (int)Phase == 0 ? 0.5f : 0.1f;
            OverseerVfx.DrawChain(sb,
                new Vector2(Projectile.Center.X, spawnY - 24f),
                Projectile.Center + new Vector2(0f, -14f), lightColor, 1f, chainSlack, chainShiver);

            int count = Math.Max(1, Main.npcFrameCount[NPCID.GolemFistLeft]);
            Rectangle frame = new(0, 0, fistTex.Width, fistTex.Height / count);
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);

            //下落残影：本体同材质重画（速度可读性=拖出的锤影长度）
            if ((int)Phase == 1) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    Vector2 op = Projectile.oldPos[i];
                    if (op == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    sb.Draw(fistTex, op + Projectile.Size * 0.5f - Main.screenPosition, frame,
                        lightColor.MultiplyRGB(FoundryOverseer.IronMul) * (0.22f * k * k),
                        MathHelper.PiOver2, origin, 1.05f, SpriteEffects.None, 0f);
                }
            }

            //锤头：铸铁材质层（下落/触底受热更亮，收回冷却）
            float heat = (int)Phase switch {
                0 => 0.35f + 0.4f * MathHelper.Clamp(Life / Windup, 0f, 1f),
                1 => 0.95f,
                2 => 0.85f,
                _ => 0.4f,
            };
            bool ironOn = OverseerVfx.BeginIronCast(sb);
            OverseerVfx.DrawIronPart(sb, ironOn, fistTex, Projectile.Center - Main.screenPosition,
                frame, lightColor, MathHelper.PiOver2, origin, 1.05f, SpriteEffects.None,
                heat, 0.45f, Projectile.identity * 0.7391f, 1f);
            OverseerVfx.EndIronCast(sb, ironOn);
            return false;
        }

        /// <summary>行程标线：OverseerPressBeam（两缘硬轨线+下行刻度+脚线+瞄准角标+锁定白闪），
        /// 无 shader 降级为 SoftGlow 柔柱</summary>
        private void DrawPressBeam(SpriteBatch sb, Texture2D glow) {
            float charge = MathHelper.Clamp(Life / Windup, 0f, 1f);
            float colH = floorY - Projectile.Center.Y;
            if (colH < 8f) {
                return;
            }
            Vector2 colCenter = new(Projectile.Center.X, Projectile.Center.Y + colH * 0.5f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (EffectLoader.OverseerPressBeam?.IsLoaded == true && CWRAsset.PerlinNoise?.IsLoaded == true) {
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                //锁定白闪：预告末 3f 拉满（画布契约：quad 宽 = HammerWidth*2）
                float toLock = Windup - Life;
                float lockFlash = MathHelper.Clamp((3f - toLock) / 3f, 0f, 1f);
                Effect fx = EffectLoader.OverseerPressBeam.Value;
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCharge"]?.SetValue(charge);
                fx.Parameters["uLock"]?.SetValue(lockFlash);
                fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.7391f % 3.7f);
                fx.Parameters["uLenPx"]?.SetValue(colH);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(glow, colCenter - Main.screenPosition, null, Color.White, 0f, glow.Size() * 0.5f,
                    new Vector2(HammerWidth * 2f / glow.Width, colH / glow.Height), SpriteEffects.None, 0f);
                gd.Textures[1] = null;
            }
            else {
                //降级：柔柱 + 脚线
                sb.Draw(glow, colCenter - Main.screenPosition, null,
                    FoundryOverseer.FurnaceOrange * (0.18f + 0.3f * charge), 0f, glow.Size() * 0.5f,
                    new Vector2(HammerWidth * 2f / glow.Width, colH * 2.2f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Projectile.Center.X, floorY - 4f) - Main.screenPosition, null,
                    FoundryOverseer.FurnaceOrange * (0.35f + 0.35f * charge), 0f, glow.Size() * 0.5f,
                    new Vector2(HammerWidth * 2.4f / glow.Width, 10f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
