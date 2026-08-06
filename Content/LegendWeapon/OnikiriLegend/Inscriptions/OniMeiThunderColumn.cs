using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 雷切「斩雷」的雷柱：自天顶垂直劈到目标头上，沿途穿透。<br/>
    /// 落雷要求头顶通天——洞里砍不出雷，这条硬限制由 <see cref="TryStrike"/> 在生成前探顶把关，
    /// 玩家能从"这一刀没落雷"直接读出自己在室内。<br/>
    /// 三拍：闪现全宽过曝(3f) → 主放电收窄见分叉(~12f) → 余辉退成绯红残像。<br/>
    /// ai[0]=柱高(px) ai[1]=柱宽(px)
    /// </summary>
    internal class OniMeiThunderColumn : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 26;
        /// <summary>可造成伤害的窗口（帧），只有主放电那几帧带电</summary>
        private const int DamageWindow = 8;
        private const float DefaultWidth = 78f;
        /// <summary>落点辉光的贴地椭圆半宽</summary>
        private const float GlowHalfWidth = 96f;

        private static readonly Vector3 ColorHot = new(1.00f, 0.97f, 0.90f);
        private static readonly Vector3 ColorBright = new(0.93f, 0.72f, 0.34f);
        private static readonly Vector3 ColorDeep = new(0.88f, 0.16f, 0.14f);

        private int timer;
        private bool initialized;
        private float seed;

        private float ColumnHeight => Projectile.ai[0] > 8f ? Projectile.ai[0] : 320f;
        private float ColumnWidth => Projectile.ai[1] > 4f ? Projectile.ai[1] : DefaultWidth;
        private float Age => MathHelper.Clamp(timer / (float)LifeFrames, 0f, 1f);
        /// <summary>落点：弹幕心即目标位置</summary>
        private Vector2 Impact => Projectile.Center;

        public override void SetDefaults() {
            Projectile.width = (int)DefaultWidth;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = LifeFrames + 4;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>
        /// 在目标头顶探顶；探得到天才落雷。<br/>
        /// 返回是否真的落下——调用方据此决定要不要放"落空"的提示
        /// </summary>
        internal static bool TryStrike(Player player, Vector2 at, int damage, float knockback,
            float widthMul = 1f, IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return false;
            }
            if (!TryProbeSky(at, out float height)) {
                return false;
            }
            Projectile spawned = Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiThunderColumn"), at, Vector2.Zero,
                ModContent.ProjectileType<OniMeiThunderColumn>(), Math.Max(1, damage), knockback,
                player.whoAmI, ai0: height, ai1: DefaultWidth * widthMul);
            return spawned.active;
        }

        /// <summary>
        /// 自落点向上探：撞到实心砖即判为室内，探满上限或出屏顶即视为通天。<br/>
        /// 返回可用的柱高，够画一根从屏外劈进来的雷
        /// </summary>
        private static bool TryProbeSky(Vector2 at, out float height) {
            height = 0f;
            Point tile = at.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 2)) {
                return false;
            }
            int limit = Math.Max(0, tile.Y - OniMeiCombat.ThunderSkyProbeTiles);
            for (int y = tile.Y - 1; y >= limit; y--) {
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasTile && Main.tileSolid[probe.TileType] && !Main.tileSolidTop[probe.TileType]) {
                    return false;
                }
            }
            //柱高至少铺满一屏高，读作"从天上下来的"而不是"头顶冒出一条"
            height = Math.Max((tile.Y - limit) * 16f, Main.screenHeight * 0.75f);
            return true;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                seed = Projectile.identity * 0.6180339887f % 1f;
                //碰撞箱贴合柱体：只有柱子扫到的那一条竖带才吃伤害
                Projectile.width = (int)Math.Max(16f, ColumnWidth * 0.55f);
                Projectile.height = (int)ColumnHeight;
                Vector2 impact = Projectile.Center;
                Projectile.Center = impact - Vector2.UnitY * (ColumnHeight * 0.5f);
                PlayStrikeCue(impact);
            }
            timer++;
            if (timer >= LifeFrames) {
                Projectile.Kill();
                return;
            }
            Vector2 foot = Projectile.Center + Vector2.UnitY * (ColumnHeight * 0.5f);
            Lighting.AddLight(foot, new Vector3(1.0f, 0.72f, 0.34f) * (1f - Age));
            if (!Main.dedServ && timer < 10) {
                SpawnIonTrail(foot);
            }
        }

        public override bool? CanDamage() => timer <= DamageWindow ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //雷是自上而下劈的，击退方向别往横里推
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.35f, Volume = 0.55f }, target.Center);
            CrimsonRendHitVFX.SpawnImpactBurst(target.Center, -Vector2.UnitY * 4f, 0.4f, 0.7f,
                CWRLoad.NPCValue.ISTheofSteel(target));
        }

        private void PlayStrikeCue(Vector2 impact) {
            SoundEngine.PlaySound(SoundID.Thunder with { Pitch = 0.30f, Volume = 0.62f }, impact);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.15f, Volume = 0.50f }, impact);
            if (Main.dedServ) {
                return;
            }
            Main.player[Projectile.owner].CWR()?.GetScreenShake(3.6f);
            //落点：贴地横向甩出的金屑，不是向上喷泉
            for (int i = 0; i < 14; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(impact + Main.rand.NextVector2Circular(14f, 6f),
                    new Vector2(side * Main.rand.NextFloat(3f, 9f), -Main.rand.NextFloat(0.5f, 2.5f)),
                    new Color(240, 196, 110), Main.rand.NextFloat(0.24f, 0.42f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>电离通道：沿柱身残留几粒往上飘的余烬，读作空气被烧过</summary>
        private void SpawnIonTrail(Vector2 foot) {
            for (int i = 0; i < 2; i++) {
                float up = Main.rand.NextFloat(0.05f, 0.85f);
                Vector2 at = foot - Vector2.UnitY * ColumnHeight * up
                    + Vector2.UnitX * Main.rand.NextFloat(-ColumnWidth * 0.35f, ColumnWidth * 0.35f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at,
                    -Vector2.UnitY * Main.rand.NextFloat(1.2f, 3.4f)
                        + Vector2.UnitX * Main.rand.NextFloat(-0.6f, 0.6f),
                    Main.rand.NextBool(3) ? new Color(255, 244, 226) : new Color(236, 178, 96),
                    Main.rand.NextFloat(0.14f, 0.26f))
                    ?.Configure(Main.rand.Next(12, 20), affectedByGravity: false);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((short)timer);

        public override void ReceiveExtraAI(BinaryReader reader) {
            timer = reader.ReadInt16();
            initialized = true;
            seed = Projectile.identity * 0.6180339887f % 1f;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            Effect fx = EffectLoader.OniRaikiri?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            float age = Age;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uAge"]?.SetValue(age);
            fx.Parameters["uJitter"]?.SetValue(0.55f);
            fx.Parameters["uBranch"]?.SetValue(0.85f);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColHot"]?.SetValue(ColorHot);
            fx.Parameters["uColBright"]?.SetValue(ColorBright);
            fx.Parameters["uColDeep"]?.SetValue(ColorDeep);

            Vector2 foot = Projectile.Center + Vector2.UnitY * (ColumnHeight * 0.5f);
            Vector2 head = foot - Vector2.UnitY * ColumnHeight;
            float half = ColumnWidth * 0.5f;

            fx.CurrentTechnique = fx.Techniques["BoltTech"];
            DrawQuad(device, fx,
                head - Vector2.UnitX * half, head + Vector2.UnitX * half,
                foot - Vector2.UnitX * half, foot + Vector2.UnitX * half);

            fx.CurrentTechnique = fx.Techniques["GlowTech"];
            Vector2 glowHalf = new(GlowHalfWidth, GlowHalfWidth);
            DrawQuad(device, fx,
                foot + new Vector2(-glowHalf.X, -glowHalf.Y), foot + new Vector2(glowHalf.X, -glowHalf.Y),
                foot + new Vector2(-glowHalf.X, glowHalf.Y), foot + new Vector2(glowHalf.X, glowHalf.Y));

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        private static void DrawQuad(GraphicsDevice device, Effect fx,
            Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight) {
            Vector2 screen = -Main.screenPosition;
            VertexPositionColorTexture[] verts = [
                new((topLeft + screen).ToVector3(), Color.White, new Vector2(0f, 0f)),
                new((topRight + screen).ToVector3(), Color.White, new Vector2(1f, 0f)),
                new((bottomLeft + screen).ToVector3(), Color.White, new Vector2(0f, 1f)),
                new((bottomRight + screen).ToVector3(), Color.White, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
