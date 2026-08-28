using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart.Projectiles
{
    /// <summary>
    /// 「沼气袋」气泡包：泥水边低频鼓起的沼气泡。ai[0]=体型。
    /// 预告 52 帧（包体膨大 + 咕噜声渐急渐高）→ 破裂帧喷出一团沼瘴雾 → 泡沫散尽。
    /// 生成位置即锁定（预告即承诺），全程走位可避；本体无判定，伤害全在雾团。
    /// 预告期水体被抽走或 Boss 入场则哑火（只散泡不喷雾）
    /// </summary>
    internal class MireheartGasPocketProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 52;
        /// <summary>咕噜声间隔（帧）</summary>
        private const int GurgleGap = 16;
        /// <summary>泡体满径（像素，×体型）</summary>
        private const float BubbleFullRadius = 15f;
        /// <summary>水体复查间隔（帧）</summary>
        private const int WaterCheckGap = 10;

        private float Scale => Projectile.ai[0];
        private int Elapsed => TelegraphFrames - Projectile.timeLeft;
        private float Progress => MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

        /// <summary>哑火：水没了 / Boss 入场（各端从同步世界状态得出同一结论）</summary>
        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 160;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //哑火判定：水被抽走 / Boss 入场（伤害机制暂停）
            if (!Cancelled) {
                if (CWRWorld.HasBoss) {
                    Cancelled = true;
                }
                else if (elapsed % WaterCheckGap == 0) {
                    Point cell = Projectile.Center.ToTileCoordinates();
                    if (!WorldGen.InWorld(cell.X, cell.Y, 10)
                        || Main.tile[cell.X, cell.Y].LiquidAmount < 60
                        || Main.tile[cell.X, cell.Y].LiquidType != LiquidID.Water) {
                        Cancelled = true;
                    }
                }
            }

            //破裂帧：权威端喷出雾团（提交帧行为，镜像凝晶残核的家族写法）
            if (Projectile.timeLeft == 1 && !Cancelled
                && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center - new Vector2(0f, 6f), Vector2.Zero,
                    ModContent.ProjectileType<MireheartMiasmaProj>(),
                    MireheartMiasmaProj.MiasmaDamage(), 0f, Main.myPlayer, Scale);
            }

            if (Main.dedServ || Cancelled) {
                return;
            }

            //咕噜声：随膨大渐急渐高（听觉预告通道）
            if (elapsed % GurgleGap == 0) {
                SoundEngine.PlaySound(SoundID.Item13 with {
                    Volume = 0.3f + 0.2f * Progress,
                    Pitch = -0.7f + 0.55f * Progress,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            //泥沸与气泡（≤1 粒/帧预算）
            if (Main.rand.NextBool(2)) {
                Dust mud = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f) * Scale, 6f),
                    DustID.Mud, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)),
                    90, default, 0.9f + 0.4f * Progress);
                mud.noGravity = true;
            }
            else if (Main.rand.NextBool(3)) {
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 6f) * Scale,
                    DustID.ToxicBubble, new Vector2(0f, -0.4f), 100, default, 0.8f);
                bubble.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Cancelled) {
                return false;
            }
            Texture2D blob = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 blobOrigin = blob.Size() * 0.5f;
            float progress = Progress;

            //包体膨大：半径随预告推进撑满，临破裂时高频颤动
            float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * (10f + 14f * progress)
                + Projectile.identity) * (0.05f + 0.10f * progress);
            float radius = BubbleFullRadius * Scale * (0.25f + 0.75f * progress);
            Vector2 pos = Projectile.Center - new Vector2(0f, radius * 0.4f) - Main.screenPosition;
            Vector2 bodyScale = new Vector2(radius * 2.4f / blob.Width * (1f + wobble),
                radius * 2.1f / blob.Height * (1f - wobble));

            //浊膜本体（真 alpha 暗层承担轮廓）
            Color membrane = new Color(46, 60, 30) * (0.55f + 0.3f * progress);
            Main.EntitySpriteDraw(blob, pos, null, membrane, 0f, blobOrigin, bodyScale, SpriteEffects.None, 0);
            //膜内沼气微光（加色敷料）
            Color gasGlow = new Color(140, 186, 80, 0) * (0.28f * (0.4f + 0.6f * progress));
            Main.EntitySpriteDraw(glow, pos, null, gasGlow, 0f, glow.Size() * 0.5f,
                radius * 2.6f / glow.Width, SpriteEffects.None, 0);
            //顶部水膜高光
            Color sheen = new Color(200, 230, 170, 0) * (0.22f * progress);
            Main.EntitySpriteDraw(blob, pos + new Vector2(-radius * 0.3f, -radius * 0.45f), null,
                sheen, -0.6f, blobOrigin, bodyScale * 0.3f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (Cancelled) {
                //哑火：几粒水泡散掉
                for (int i = 0; i < 3; i++) {
                    Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                        DustID.BreatheBubble, new Vector2(0f, -0.6f), 80, default, 0.9f);
                }
                return;
            }
            //破裂：水花 + 毒沫上喷
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.6f,
                Pitch = -0.2f,
                MaxInstances = 3
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item34 with {
                Volume = 0.4f,
                Pitch = 0.3f,
                MaxInstances = 3
            }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust splash = Dust.NewDustPerfect(Projectile.Center, DustID.Water_Jungle,
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(1.5f, 3.6f)) * Scale,
                    60, default, Main.rand.NextFloat(0.9f, 1.4f));
                splash.noGravity = false;
            }
            for (int i = 0; i < 6; i++) {
                Dust gas = Dust.NewDustPerfect(Projectile.Center - new Vector2(0f, 4f),
                    DustID.Poisoned,
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(0.6f, 1.6f)) * Scale,
                    110, default, Main.rand.NextFloat(1f, 1.5f));
                gas.noGravity = true;
            }
        }
    }
}
