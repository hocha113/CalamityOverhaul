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
    /// 梵鐘「一撞」的钟波：一圈向外推的低频声压。<br/>
    /// 主波之后跟两道追不上的余波，环推过去的地方空气被挤了一下——
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
                //贴地椭圆：竖向压扁，读作地面上的一圈而不是空中球壳
                Vector2 at = Projectile.Center + new Vector2(dir.X, dir.Y * 0.45f) * front;
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
            //背景折射源：屏幕后备缓冲不可直接取，退回噪声只做位移扰动的载体
            fx.Parameters["uBackdrop"]?.SetValue(noise);
            fx.Parameters["uColHot"]?.SetValue(ColorHot);
            fx.Parameters["uColBright"]?.SetValue(ColorBright);
            fx.Parameters["uColDark"]?.SetValue(ColorDark);
            fx.CurrentTechnique = fx.Techniques["WaveTech"];

            //贴地椭圆：地面上的一圈声压，不是空中球壳
            Vector2 center = Projectile.Center - Main.screenPosition;
            float halfX = Radius;
            float halfY = Radius * 0.46f;
            VertexPositionColorTexture[] verts = [
                new((center + new Vector2(-halfX, -halfY)).ToVector3(), Color.White, new Vector2(0f, 0f)),
                new((center + new Vector2(halfX, -halfY)).ToVector3(), Color.White, new Vector2(1f, 0f)),
                new((center + new Vector2(-halfX, halfY)).ToVector3(), Color.White, new Vector2(0f, 1f)),
                new((center + new Vector2(halfX, halfY)).ToVector3(), Color.White, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }
}
