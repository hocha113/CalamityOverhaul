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
    /// 枯山水「砂纹」：立定耙出来的一片石庭。<br/>
    /// 场世界锚定——你耙好就可以走开，它留在原地继续割；
    /// 你站在自己的场里时架势涨得更快，所以"要不要守着它"是个真选择。<br/>
    /// 同时只有一场，耙新的即废旧的。<br/>
    /// ai[0]=半径 ai[1]=寿命(帧)
    /// </summary>
    internal class OniMeiSandGarden : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>耙纹自内向外长完的帧数</summary>
        private const int RakeFrames = 26;
        /// <summary>贴地压扁比（场是画在地上的）</summary>
        private const float GroundSquash = 0.465f;

        private static readonly Vector3 ColorSand = new(0.82f, 0.78f, 0.72f);
        private static readonly Vector3 ColorShadow = new(0.14f, 0.10f, 0.11f);
        private static readonly Vector3 ColorCut = new(0.86f, 0.16f, 0.13f);

        private int timer;
        private bool initialized;
        private float seed;
        private int cutTimer;
        private float pulse;

        private float Radius => Projectile.ai[0] > 16f ? Projectile.ai[0] : OniMeiCombat.SandGardenRadius;
        private float Lifetime => Projectile.ai[1] > 8f ? Projectile.ai[1] : OniMeiCombat.SandGardenLifeTicks;
        private float Age => MathHelper.Clamp(timer / Lifetime, 0f, 1f);
        private float Rake => MathHelper.Clamp(timer / (float)RakeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = OniMeiCombat.SandGardenCutInterval;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端耙场；同时只留一场，新的顶掉旧的</summary>
        internal static Projectile Rake_(Player player, Vector2 at, int damage,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return null;
            }
            int type = ModContent.ProjectileType<OniMeiSandGarden>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    proj.Kill();
                }
            }
            return Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiSandGarden"), at, Vector2.Zero,
                type, Math.Max(1, damage), 0f, player.whoAmI,
                ai0: OniMeiCombat.SandGardenRadius, ai1: OniMeiCombat.SandGardenLifeTicks);
        }

        /// <summary>玩家是否站在自己耙的场里（架势加成的判据）</summary>
        internal static bool StandingInOwnGarden(Player player) {
            if (player == null) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiSandGarden>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type
                    || proj.ModProjectile is not OniMeiSandGarden garden) {
                    continue;
                }
                if (garden.Contains(player.Bottom)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>贴地椭圆内判定：场是画在地上的，纵向按压扁比收</summary>
        private bool Contains(Vector2 point) {
            Vector2 delta = point - Projectile.Center;
            delta.Y /= GroundSquash;
            return delta.LengthSquared() <= Radius * Radius;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                seed = Projectile.identity * 0.6180339887f % 1f;
                int box = (int)(Radius * 2f);
                Vector2 keep = Projectile.Center;
                Projectile.width = box;
                Projectile.height = (int)(Radius * 2f * GroundSquash);
                Projectile.Center = keep;
                PlayRakeCue();
            }
            timer++;
            Projectile.timeLeft = 2;
            if (timer >= Lifetime) {
                Projectile.Kill();
                return;
            }
            if (cutTimer > 0) {
                cutTimer--;
            }
            pulse *= 0.88f;
            if (Rake >= 1f && cutTimer <= 0) {
                cutTimer = OniMeiCombat.SandGardenCutInterval;
                pulse = 1f;
                PlayCutCue();
            }
            if (!Main.dedServ) {
                SpawnSandDrift();
            }
        }

        /// <summary>耙纹还没长完就不割；割是按周期来的，配合上面的 pulse 一起读</summary>
        public override bool? CanDamage() => Rake >= 1f && cutTimer >= OniMeiCombat.SandGardenCutInterval - 2
            ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Contains(targetHitbox.Center.ToVector2()) || Contains(targetHitbox.Bottom());

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            root?.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.SandGardenCutInterval);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0.55f, Volume = 0.26f }, target.Center);
        }

        private void PlayRakeCue() {
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.35f, Volume = 0.45f }, Projectile.Center);
        }

        private void PlayCutCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0.15f, Volume = 0.22f }, Projectile.Center);
        }

        /// <summary>砂在飘：贴地横向游走的细砂，读作干砂而不是发光地毯</summary>
        private void SpawnSandDrift() {
            if (timer % 5 != 0) {
                return;
            }
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(0.25f, 0.95f) * Radius * Rake;
            Vector2 dir = ang.ToRotationVector2();
            Vector2 at = Projectile.Center + new Vector2(dir.X, dir.Y * GroundSquash) * dist;
            PRTLoader.NewParticle<PRT_CrimsonSpark>(at,
                new Vector2(dir.X * Main.rand.NextFloat(0.4f, 1.2f), -0.1f),
                new Color(214, 204, 188), Main.rand.NextFloat(0.08f, 0.15f))
                ?.Configure(Main.rand.Next(18, 28), affectedByGravity: false);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)timer);
            writer.Write((short)cutTimer);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            timer = reader.ReadInt16();
            cutTimer = reader.ReadInt16();
            initialized = true;
            seed = Projectile.identity * 0.6180339887f % 1f;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            Effect fx = EffectLoader.OniSandGarden?.Value;
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
            fx.Parameters["uRake"]?.SetValue(Rake);
            fx.Parameters["uAge"]?.SetValue(Age);
            fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));
            fx.Parameters["uOpacity"]?.SetValue(0.92f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColSand"]?.SetValue(ColorSand);
            fx.Parameters["uColShadow"]?.SetValue(ColorShadow);
            fx.Parameters["uColCut"]?.SetValue(ColorCut);
            fx.CurrentTechnique = fx.Techniques["GardenTech"];

            //着色器里已按 1/GroundSquash 竖压做透视，这里必须给正方形 quad：
            //再乘一次压扁比会让画出来的场只有 Contains 判定的一半高
            Vector2 center = Projectile.Center - Main.screenPosition;
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
    }
}
