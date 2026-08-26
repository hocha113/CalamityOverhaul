using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine.Projectiles
{
    /// <summary>
    /// 菌盖蓄胀：大蘑菇喷发前的预告体（「巨菇喷发」的预告段）。
    /// ai[0]=锁定落点X ai[1]=锁定落点Y（预告生成帧锁死，此后不再重瞄），ai[2]=档位。
    /// 菌盖发亮膨大+吱嘎声 56 帧（视觉+听觉双通道，≥45 公平契约）→ 提交帧抛出两团孢子团。
    /// 预告期蘑菇被砍断则喷发取消（反制有效）
    /// </summary>
    internal class SporeshineCapSwellProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 56;
        /// <summary>提交后余韵帧数（喷发闪光回落）</summary>
        private const int FadeFrames = 12;
        /// <summary>每次喷发的孢子团数（机制形状不随档位变）</summary>
        private const int LobCount = 2;
        /// <summary>副团相对锁定点的横向散布</summary>
        private const float SecondLobSpread = 170f;
        /// <summary>孢雾伤害系数：经典档目标实收 = 参照接触伤 × 0.4（微量），
        /// 已预除原版敌对弹幕×2 结算系数（0.4 ÷ 2 = 0.2），难度放大交给引擎，不叠手动乘数</summary>
        private const float FogDamageFrac = 0.2f;
        /// <summary>群系参照接触伤：困难前取洞穴层小怪档，困难后取蘑菇地表小怪档</summary>
        private static int ReferenceContact => Main.hardMode ? 44 : 20;

        private static readonly Color BrightSpore = new(120, 210, 255);
        private static readonly Color WarnGlow = new(96, 190, 255);

        private Vector2 LockedTarget => new(Projectile.ai[0], Projectile.ai[1]);
        private int Tier => Math.Clamp((int)Projectile.ai[2], 1, 3);
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯预告体，伤害经由孢雾
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //预告期宿主检查：菌柱被砍断则取消（各端读各自的瓦片数据，结论一致）
            if (!Cancelled && elapsed < TelegraphFrames && elapsed % 8 == 0 && !AnchorTileAlive()) {
                Cancelled = true;
            }

            if (elapsed == 0 && !Main.dedServ) {
                //吱嘎起势（低音门轴声当菌盖纤维的呻吟）
                SoundEngine.PlaySound(SoundID.DoorOpen with { Volume = 0.6f, Pitch = -0.62f, MaxInstances = 4 }, Projectile.Center);
            }
            if (elapsed == 30 && !Cancelled && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DoorOpen with { Volume = 0.5f, Pitch = -0.42f, MaxInstances = 4 }, Projectile.Center);
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    EmitLobs();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.4f, Pitch = 0.45f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 12; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                            new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(2f, 6f)),
                            100, default, Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }

            if (Cancelled || Main.dedServ) {
                return;
            }

            //蓄力期：菌盖孢尘上滚（≤1 粒/3 帧）
            if (elapsed < TelegraphFrames && Main.rand.NextBool(3)) {
                float progress = elapsed / (float)TelegraphFrames;
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-6f, 6f)),
                    DustID.GlowingMushroom, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f + progress)),
                    120, default, 0.8f + progress * 0.5f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, BrightSpore.ToVector3() * (0.15f + 0.35f * MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f)));
        }

        /// <summary>锚点仍是大蘑菇瓦片（锚在菌柱顶格正下方）</summary>
        private bool AnchorTileAlive() {
            Point tile = (Projectile.Center + new Vector2(0f, 30f)).ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                return false;
            }
            Tile t = Main.tile[tile.X, tile.Y];
            return t.HasTile && t.TileType == TileID.MushroomTrees;
        }

        /// <summary>提交帧抛团：主团直取锁定点（预告即承诺），副团在其近旁散布</summary>
        private void EmitLobs() {
            int damage = (int)(ReferenceContact * FogDamageFrac);
            Vector2 from = Projectile.Center - new Vector2(0f, 14f);
            for (int i = 0; i < LobCount; i++) {
                Vector2 to = LockedTarget;
                if (i > 0) {
                    to.X += Main.rand.NextFloat(-SecondLobSpread, SecondLobSpread);
                }
                float dist = Vector2.Distance(from, to);
                int frames = (int)MathHelper.Clamp(dist / 8f, 42f, 68f);
                Vector2 vel = new((to.X - from.X) / frames,
                    (to.Y - from.Y) / frames - SporeshineSporeLobProj.Gravity * frames * 0.5f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                    ModContent.ProjectileType<SporeshineSporeLobProj>(), damage, 0f, Main.myPlayer, Tier);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.35f : 1f;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (elapsed >= TelegraphFrames) {
                if (Cancelled) {
                    return false;
                }
                //喷发闪光回落
                float flash = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
                Main.EntitySpriteDraw(glow, center, null, BrightSpore with { A = 0 } * (0.7f * flash), 0f,
                    glowOrigin, new Vector2(2.4f - flash * 0.8f, 1.7f - flash * 0.5f), SpriteEffects.None, 0);
                return false;
            }

            float progress = elapsed / (float)TelegraphFrames;
            //蓄力越满脉动越急（听觉外的第二条节奏线索）
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * (9f + 10f * progress) + Projectile.identity);
            float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 23f + Projectile.identity) * 2.2f * progress;

            //菌盖膨大：宽扁双瓣辉光渐涨（加色，盖在原版菌盖贴图上读作发亮鼓起）
            float swell = 0.7f + 0.9f * progress;
            Color capGlow = BrightSpore with { A = 0 } * (0.42f * progress * pulse * cancelDim);
            Main.EntitySpriteDraw(glow, center + new Vector2(jig, 0f), null, capGlow, 0f,
                glowOrigin, new Vector2(2.1f * swell, 1.05f * swell), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center + new Vector2(jig * 0.6f, -6f), null,
                BrightSpore with { A = 0 } * (0.3f * progress * cancelDim), 0f,
                glowOrigin, new Vector2(1.2f * swell, 0.8f * swell), SpriteEffects.None, 0);

            //锁定落点地标：脉动扁光斑（走位可避的承诺可视化）
            Vector2 markPos = LockedTarget - Main.screenPosition;
            Color mark = WarnGlow with { A = 0 } * (0.34f * progress * pulse * cancelDim);
            Main.EntitySpriteDraw(glow, markPos, null, mark, 0f, glowOrigin,
                new Vector2(2f + 0.3f * pulse, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
