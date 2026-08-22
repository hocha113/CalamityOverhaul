using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using InnoVault.RenderHandles;
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
    /// 梵鐘「一撞」的钟波：一圈向外推的低频声压。<br/>
    /// 主波之后跟两道追不上的余波，环推过去的地方空气被挤了一下
    /// 是"声音把人推开"，不是"一个亮圈在放大"。<br/>
    /// 只在主波扫过的那一圈上结算，所以站得远的敌手挨得晚，是有节奏的一击。<br/>
    /// ai[0]=半径
    /// </summary>
    internal class OniMeiBellWave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 42;
        /// <summary>主波环的判定半宽(px)</summary>
        private const float RingHalfWidth = 46f;

        private static readonly Vector3 ColorHot = new(1.00f, 0.94f, 0.82f);
        private static readonly Vector3 ColorBright = new(0.82f, 0.62f, 0.30f);
        private static readonly Vector3 ColorDark = new(0.16f, 0.09f, 0.07f);

        private int timer;
        private bool initialized;
        private float seed;
        private readonly System.Collections.Generic.HashSet<int> struckRoots = [];

        private float Radius => Projectile.ai[0] > 16f ? Projectile.ai[0] : OniMeiCombat.BellWaveRadius;
        private float Age => MathHelper.Clamp(timer / (float)LifeFrames, 0f, 1f);
        /// <summary>主波当前所在半径</summary>
        private float Front => Age * Radius;

        public override void SetDefaults() {
            Projectile.width = 32;
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

        internal static Projectile Fire(Player player, Vector2 at, int damage, float knockback,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return null;
            }
            return Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiBellWave"), at, Vector2.Zero,
                ModContent.ProjectileType<OniMeiBellWave>(), Math.Max(1, damage), knockback,
                player.whoAmI, ai0: OniMeiCombat.BellWaveRadius);
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                seed = Projectile.identity * 0.6180339887f % 1f;
                int box = (int)(Radius * 2f);
                Vector2 keep = Projectile.Center;
                Projectile.width = box;
                Projectile.height = box;
                Projectile.Center = keep;
                PlayTollCue();
            }
            timer++;
            if (timer >= LifeFrames) {
                Projectile.Kill();
                return;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                //滞缚跟着主波走：环扫到谁，谁才被按住
                BindAtFront();
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.40f, 0.18f) * (1f - Age));
            if (!Main.dedServ) {
                SpawnRingDust();
            }
        }

        private void BindAtFront() {
            float front = Front;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy()) {
                    continue;
                }
                NPC root = OniMeiCombat.ResolveEffectRoot(npc) ?? npc;
                if (struckRoots.Contains(root.whoAmI)) {
                    continue;
                }
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (MathF.Abs(distance - front) > RingHalfWidth) {
                    continue;
                }
                struckRoots.Add(root.whoAmI);
                root.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.BellWaveBindTicks);
            }
        }

        public override bool? CanDamage() => timer > 1 ? null : false;

        /// <summary>只有主波扫过的那一圈带伤害，站得远的挨得晚</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 center = Projectile.Center;
            Vector2 nearest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float near = Vector2.Distance(center, nearest);
            //碰撞箱最远角：环从箱上扫过就算，不要求圆心距刚好落在环上
            float far = 0f;
            far = MathF.Max(far, Vector2.Distance(center, targetHitbox.TopLeft()));
            far = MathF.Max(far, Vector2.Distance(center, targetHitbox.TopRight()));
            far = MathF.Max(far, Vector2.Distance(center, targetHitbox.BottomLeft()));
            far = MathF.Max(far, Vector2.Distance(center, targetHitbox.BottomRight()));
            float front = Front;
            return front + RingHalfWidth >= near && front - RingHalfWidth <= far;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //声压是从中心推出去的，击退方向照这个走
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.55f, Volume = 0.30f }, target.Center);
        }

        private void PlayTollCue() {
            //钟：一记闷响 + 一记长尾，低频压过所有刀声
            SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.85f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.90f, Volume = 0.45f },
                Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Main.player[Projectile.owner].CWR()?.GetScreenShake(5.2f);
        }

        /// <summary>环缘扬尘：贴着主波位置外撒一圈，让环有"推着东西走"的质感</summary>
        private void SpawnRingDust() {
            if (timer % 2 != 0) {
                return;
            }
            float front = Front;
            for (int i = 0; i < 3; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = ang.ToRotationVector2();
                //与判定和柱面同一个正圆，尘才落在环上
                Vector2 at = Projectile.Center + dir * front;
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(at, dir * Main.rand.NextFloat(1.2f, 3f),
                    Color.White, Main.rand.NextFloat(0.05f, 0.09f))
                    ?.Configure(Main.rand.Next(16, 26), new Color(122, 86, 40), new Color(24, 16, 12));
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
            Effect fx = EffectLoader.OniBellWave?.Value;
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

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uAge"]?.SetValue(Age);
            fx.Parameters["uCharge"]?.SetValue(0f);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColHot"]?.SetValue(ColorHot);
            fx.Parameters["uColBright"]?.SetValue(ColorBright);
            fx.Parameters["uColDark"]?.SetValue(ColorDark);
            fx.CurrentTechnique = fx.Techniques["WaveTech"];

            //正圆：判定用的是真实距离（Colliding/BindAtFront 都按 Distance 算），
            //所以画面也必须是正圆，压成贴地椭圆会让头顶的敌人在"环还没扫到"时就挨打
            //顶点收世界坐标：GetTransfromMatrix 自带 -screenPosition
            Vector2 center = Projectile.Center;
            float half = Radius;
            VertexPositionColorTexture[] verts = [
                new((center + new Vector2(-half, -half)).ToVector3(), Color.White, new Vector2(0f, 0f)),
                new((center + new Vector2(half, -half)).ToVector3(), Color.White, new Vector2(1f, 0f)),
                new((center + new Vector2(-half, half)).ToVector3(), Color.White, new Vector2(0f, 1f)),
                new((center + new Vector2(half, half)).ToVector3(), Color.White, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        internal static readonly Vector3 RimHot = ColorHot;
        internal static readonly Vector3 RimBright = ColorBright;
        internal static readonly Vector3 RimDark = ColorDark;
    }

    /// <summary>
    /// 梵鐘自鸣环：满架势起算的那三秒，在脚下画一圈逐渐咬合的钟纹。<br/>
    /// 这一圈就是「现在放终结，还是让钟自己响」的决策窗，没有它，
    /// 玩家只能靠听那记越来越紧的嗡声猜还剩多久。<br/>
    /// 只画本地玩家：自鸣计数与架势一样是本机自治的，远端读不到真值
    /// </summary>
    internal sealed class OniMeiBellRimRender : RenderHandle
    {
        /// <summary>与面影同层，压在地表之上、实体之下</summary>
        public override float Weight => 1.28f;

        /// <summary>环半径随蓄势略微收紧，读作"钟正在被拉满"</summary>
        private const float RimRadius = 92f;

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return;
            }
            float charge = player.GetModPlayer<OnikiriPlayer>().BellChargeRatio;
            if (charge <= 0.001f) {
                return;
            }
            Effect fx = EffectLoader.OniBellWave?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }

            BlendState prevBlend = graphicsDevice.BlendState;
            RasterizerState prevRaster = graphicsDevice.RasterizerState;
            DepthStencilState prevDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(0.31f);
            fx.Parameters["uAge"]?.SetValue(0f);
            fx.Parameters["uCharge"]?.SetValue(charge);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColHot"]?.SetValue(OniMeiBellWave.RimHot);
            fx.Parameters["uColBright"]?.SetValue(OniMeiBellWave.RimBright);
            fx.Parameters["uColDark"]?.SetValue(OniMeiBellWave.RimDark);
            fx.CurrentTechnique = fx.Techniques["RimTech"];

            //贴在脚下：环是绕着人转的一圈钟口，压扁读作地面上的圈
            Vector2 center = player.Bottom - Vector2.UnitY * 6f;
            float halfX = RimRadius;
            float halfY = RimRadius * 0.42f;
            VertexPositionColorTexture[] verts = [
                new((center + new Vector2(-halfX, -halfY)).ToVector3(), Color.White, new Vector2(0f, 0f)),
                new((center + new Vector2(halfX, -halfY)).ToVector3(), Color.White, new Vector2(1f, 0f)),
                new((center + new Vector2(-halfX, halfY)).ToVector3(), Color.White, new Vector2(0f, 1f)),
                new((center + new Vector2(halfX, halfY)).ToVector3(), Color.White, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            graphicsDevice.BlendState = prevBlend;
            graphicsDevice.RasterizerState = prevRaster;
            graphicsDevice.DepthStencilState = prevDepth;
        }
    }
}
