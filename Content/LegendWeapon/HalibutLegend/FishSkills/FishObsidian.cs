using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishObsidian : FishSkill
    {
        public override int UnlockFishID => ItemID.Obsidifish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 6;
        public override int ResearchDuration => 60 * 18;

        private static readonly List<int> ActiveObsidianFish = new();
        private static int MaxObsidianFish => 5 + HalibutData.GetDomainLayer() / 2;

        private int lastPlayerHitCount = 0;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActiveObsidianFish.Count < MaxObsidianFish) {
                    int fishProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ObsidianFishOrbit>(),
                        (int)(damage * (1.6f + HalibutData.GetDomainLayer() * 0.55f)),
                        knockback * 0.3f,
                        player.whoAmI,
                        ai0: ActiveObsidianFish.Count
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActiveObsidianFish.Add(fishProj);
                        SpawnSummonEffect(player.Center);
                        SoundEngine.PlaySound(SoundID.Item30 with {
                            Volume = 0.5f,
                            Pitch = -0.3f + ActiveObsidianFish.Count * 0.05f
                        }, player.Center);
                    }
                }
            }

            return null;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            int currentHitCount = player.CountProjectilesOfID<Content.Projectiles.Others.Hit>();

            if (currentHitCount > lastPlayerHitCount && ActiveObsidianFish.Count > 0) {
                ShatterOneFish(player);
            }

            lastPlayerHitCount = currentHitCount;
            return true;
        }

        private void ShatterOneFish(Player player) {
            CleanupInactiveFish();

            if (ActiveObsidianFish.Count > 0) {
                int fishID = ActiveObsidianFish[ActiveObsidianFish.Count - 1];

                if (fishID >= 0 && fishID < Main.maxProjectiles && Main.projectile[fishID].active) {
                    Projectile fish = Main.projectile[fishID];
                    if (fish.ModProjectile is ObsidianFishOrbit obsidianFish) {
                        obsidianFish.Shatter();
                    }
                }

                ActiveObsidianFish.RemoveAt(ActiveObsidianFish.Count - 1);
            }
        }

        private static void CleanupInactiveFish() {
            ActiveObsidianFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<ObsidianFishOrbit>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            //黑曜石"凝结"：碎屑向内汇聚 + 暗烟
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 from = position + angle.ToRotationVector2() * Main.rand.NextFloat(40f, 70f);
                PRTLoader.NewParticle<PRT_Spark>(from, (position - from) * 0.08f
                    , Color.Lerp(ObsidianFishOrbit.MoltenGlow, new Color(60, 35, 70), Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(false, Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(position + Main.rand.NextVector2Circular(18f, 18f)
                    , Main.rand.NextVector2Circular(2f, 2f), new Color(40, 30, 45), Main.rand.NextFloat(0.7f, 1f))
                    .Configure(24, 0.7f, 0.04f);
            }
        }
    }

    /// <summary>
    /// 黑曜石鱼：环绕玩家的熔岩玻璃护卫。倾斜椭圆轨道 + 弹簧滞后的次级运动让阵型"有重量"，
    /// 受击时以顶点冲击波 + 玻璃碎屑炸裂。
    /// </summary>
    internal class ObsidianFishOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Obsidifish;

        private enum FishState
        {
            Gathering,
            Orbiting,
            Shattering
        }

        private FishState State {
            get => (FishState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }
        private ref float FishIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //轨道
        private float orbitRadius = 152f;
        private float spinAngle;
        private float myAngle;
        private float depth;          //-1 远 .. 1 近
        private float swimPhase;
        private Vector2 gatherStart;
        private Vector2 followVel;    //弹簧速度（次级运动）

        //姿态
        private float bodyRotation;
        private float glow;
        private float scaleMul = 1f;
        private float crackPulse;

        private Vector2 shatterVelocity;
        private float shatterSpin;
        private readonly List<ShockRing> rings = new();

        public static readonly Color ObsidianDark = new(34, 22, 44);
        public static readonly Color MoltenGlow = new(255, 95, 40);
        public static readonly Color CrackGlow = new(255, 150, 70);

        private const int GatherDuration = 20;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishObsidian>().Active(Owner) && State != FishState.Shattering) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            crackPulse = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + FishIndex * 1.3f) * 0.4f;

            switch (State) {
                case FishState.Gathering:
                    GatheringAI(Owner);
                    break;
                case FishState.Orbiting:
                    OrbitingAI(Owner);
                    break;
                case FishState.Shattering:
                    ShatteringAI();
                    break;
            }

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            //暗红熔岩照明（受深度调制）
            float lit = glow * (0.6f + depth * 0.4f);
            Lighting.AddLight(Projectile.Center, MoltenGlow.ToVector3() * lit * 0.5f);
        }

        private int GetTotalActiveFish() {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner) {
                    count++;
                }
            }
            return count;
        }

        private int GetMyRealIndex() {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner) {
                    if (proj.whoAmI == Projectile.whoAmI) {
                        return index;
                    }
                    index++;
                }
            }
            return 0;
        }

        private void GatheringAI(Player owner) {
            float p = MathHelper.Clamp(StateTimer / GatherDuration, 0f, 1f);

            if (StateTimer == 1) {
                gatherStart = Projectile.Center;
                swimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                spinAngle = MathHelper.TwoPi * FishIndex / Math.Max(GetTotalActiveFish(), 1);
            }

            myAngle = spinAngle;
            Vector2 target = OrbitPoint(owner, myAngle, out depth);
            //EaseOutBack 过冲：玻璃"咔"地嵌入阵位
            Projectile.Center = Vector2.Lerp(gatherStart, target, VaultUtils.EaseOutBack(p));

            Vector2 toCenter = owner.Center - Projectile.Center;
            bodyRotation = toCenter.ToRotation();
            glow = MathHelper.Lerp(0.2f, 1f, p);
            scaleMul = MathHelper.Lerp(0.4f, DepthScale(depth), p);

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , (owner.Center - Projectile.Center) * 0.02f, MoltenGlow, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(false, 16);
            }

            if (StateTimer >= GatherDuration) {
                State = FishState.Orbiting;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void OrbitingAI(Player owner) {
            float orbitSpeed = 0.02f * (1f + HalibutData.GetDomainLayer() * 0.12f);
            spinAngle += orbitSpeed;

            int total = Math.Max(GetTotalActiveFish(), 1);
            int realIndex = GetMyRealIndex();
            //平滑收敛到均布理想角，避免数量变化时的瞬跳
            float idealAngle = MathHelper.TwoPi * realIndex / total + spinAngle;
            myAngle = MathHelper.WrapAngle(myAngle);
            myAngle += MathHelper.WrapAngle(idealAngle - myAngle) * 0.12f;

            swimPhase += 0.12f;
            Vector2 target = OrbitPoint(owner, myAngle, out depth);
            //游动摆动
            Vector2 sway = new Vector2((float)Math.Sin(swimPhase), (float)Math.Cos(swimPhase * 1.3f)) * 6f;
            target += sway;

            //弹簧滞后跟随：本体带惯性追轨道点，停不"死板"
            Vector2 toTarget = target - Projectile.Center;
            followVel += toTarget * 0.18f;
            followVel *= 0.74f;
            Projectile.Center += followVel;

            //朝向沿运动方向 + 轻微前后摆
            if (followVel.LengthSquared() > 0.4f) {
                float swayAngle = (float)Math.Sin(swimPhase * 2f) * 0.14f;
                bodyRotation = MathHelper.WrapAngle(MathHelper.Lerp(bodyRotation, followVel.ToRotation() + swayAngle, 0.25f));
            }

            scaleMul = MathHelper.Lerp(scaleMul, DepthScale(depth), 0.15f);
            glow = (0.7f + (float)Math.Sin(StateTimer * 0.12f + FishIndex) * 0.2f) * (0.7f + depth * 0.3f);

            //熔岩余烬偶尔滴落（近景的鱼才滴，强化层次）
            if (!Main.dedServ && depth > 0.2f && Main.rand.NextBool(26)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.4f, 1.4f)), MoltenGlow, 0.4f)
                    .Configure(20, hueShift: -0.012f);
            }
        }

        private void ShatteringAI() {
            Projectile.Center += shatterVelocity;
            shatterVelocity *= 0.95f;
            shatterVelocity.Y += 0.12f;
            bodyRotation += shatterSpin;
            shatterSpin *= 0.96f;
            scaleMul *= 0.97f;
            glow *= 0.9f;

            Projectile.alpha += 16;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        /// <summary>倾斜椭圆轨道点（俯视压扁形成伪 3D 层次），并输出深度</summary>
        private Vector2 OrbitPoint(Player owner, float angle, out float depthOut) {
            float radiusPulse = orbitRadius * (1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.6f + FishIndex) * 0.04f);
            float x = (float)Math.Cos(angle) * radiusPulse;
            float y = (float)Math.Sin(angle) * radiusPulse * 0.52f;//压扁成倾斜环
            depthOut = (float)Math.Sin(angle);//下半弧在前
            return owner.Center + new Vector2(x, y - 8f);
        }

        private static float DepthScale(float depth) => 0.78f + (depth * 0.5f + 0.5f) * 0.5f;

        public void Shatter() {
            if (State == FishState.Shattering) {
                return;
            }
            State = FishState.Shattering;
            StateTimer = 0;
            Projectile.friendly = false;
            shatterVelocity = Main.rand.NextVector2Circular(10f, 10f) - Vector2.UnitY * 2f;
            shatterSpin = Main.rand.NextFloat(-0.4f, 0.4f);

            Punch(Owner, 5f);
            SpawnShatterEffect();

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f }, Projectile.Center);
        }

        private void SpawnShatterEffect() {
            if (Main.dedServ) {
                return;
            }
            rings.Add(new ShockRing(Projectile.Center, 150f, 14f, MoltenGlow, 1f, 22, 40));

            //玻璃碎片（受重力四散）
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(11f, 11f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel
                    , Color.Lerp(CrackGlow, new Color(70, 45, 80), Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.5f))
                    .Configure(true, Main.rand.Next(24, 38));
            }
            //熔岩芯爆光
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(7f, 7f)
                    , MoltenGlow, Main.rand.NextFloat(0.5f, 0.9f)).Configure(22, hueShift: -0.01f);
            }
            //暗烟
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(4f, 4f)
                    , new Color(38, 28, 44), Main.rand.NextFloat(1f, 1.6f)).Configure(30, 0.7f, 0.05f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //玻璃磕碰的反射火花
            Vector2 contact = Vector2.Lerp(Projectile.Center, target.Center, 0.5f);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(contact, Main.rand.NextVector2Circular(5f, 5f)
                    , CrackGlow, Main.rand.NextFloat(0.5f, 0.9f)).Configure(true, 14);
            }
            rings.Add(new ShockRing(contact, 56f, 7f, MoltenGlow, 1f, 14, 28));
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (State != FishState.Shattering) {
                SpawnShatterEffect();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ItemID.Obsidifish].Value;
            Rectangle src = tex.Frame();
            Vector2 origin = src.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = (255f - Projectile.alpha) / 255f;
            float rot = bodyRotation + MathHelper.PiOver4;
            float scale = Projectile.scale * scaleMul;

            //游动残影（熔岩拖影）
            if (State != FishState.Gathering) {
                for (int i = 1; i < 6 && i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / 6f;
                    Vector2 gp = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    Color gc = MoltenGlow with { A = 0 };
                    Main.spriteBatch.Draw(tex, gp, src, gc * (t * 0.18f * fade), rot, origin, scale * MathHelper.Lerp(0.7f, 0.95f, t), SpriteEffects.None, 0f);
                }
            }

            //黑曜石本体：压暗偏黑紫，模拟暗色玻璃（远景更暗）
            Color body = Color.Lerp(lightColor, ObsidianDark, 0.55f);
            body = Color.Lerp(body, Color.Black, (1f - (depth * 0.5f + 0.5f)) * 0.35f);
            Main.spriteBatch.Draw(tex, drawPos, src, body * fade, rot, origin, scale, SpriteEffects.None, 0f);

            //熔岩裂纹内辉（加色脉动）
            Color crack = Color.Lerp(MoltenGlow, CrackGlow, crackPulse) with { A = 0 };
            Main.spriteBatch.Draw(tex, drawPos, src, crack * (glow * crackPulse * 0.5f * fade), rot, origin, scale * 0.96f, SpriteEffects.None, 0f);
            //外缘熔光
            Main.spriteBatch.Draw(tex, drawPos, src, MoltenGlow with { A = 0 } * (glow * 0.28f * fade), rot, origin, scale * 1.08f, SpriteEffects.None, 0f);

            //冲击波环（顶点绘制）
            if (rings.Count > 0) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Texture2D ringTex = CWRAsset.Placeholder_White.Value;
                foreach (ShockRing r in rings) {
                    r.Draw(ringTex);
                }
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        /// <summary>
        /// 取最大值的镜头冲击；仅本地玩家、且服务器配置开启屏幕震动时生效，避免多端各自抖动与配置越权
        /// </summary>
        public static void Punch(Player owner, float amount) {
            if (owner == null || owner.whoAmI != Main.myPlayer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            owner.CWR().ScreenShakeValue = MathHelper.Max(owner.CWR().ScreenShakeValue, amount);
        }

        /// <summary>
        /// 加色三角带圆环（真正的顶点绘制）。须在外部已 Begin 的 Immediate/Additive 批次中调用，
        /// 由该批次为设备绑定精灵着色器；颜色由内/外环顶点插值，<paramref name="squash"/> 做地面透视压扁
        /// </summary>
        public static void DrawShockRing(Texture2D tex, Vector2 screenCenter, float radius, float thickness
            , Color innerColor, Color outerColor, int segments = 72, float squash = 1f, float rot = 0f
            , float jitter = 0f, float jitterPhase = 0f, float jitterFreq = 6f) {
            if (radius <= 1f || thickness <= 0.1f || segments < 3) {
                return;
            }

            int vertCount = (segments + 1) * 2;
            ColoredVertex[] verts = new ColoredVertex[vertCount];
            float half = thickness * 0.5f;

            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                float ang = t * MathHelper.TwoPi + rot;
                Vector2 dir = ang.ToRotationVector2();
                dir.Y *= squash;

                float r = radius;
                if (jitter > 0f) {
                    r += (float)Math.Sin(ang * jitterFreq + jitterPhase) * jitter;
                }

                verts[i * 2] = new ColoredVertex(screenCenter + dir * (r - half), innerColor, new Vector3(t, 0f, 1f));
                verts[i * 2 + 1] = new ColoredVertex(screenCenter + dir * (r + half), outerColor, new Vector3(t, 1f, 1f));
            }

            Main.graphics.GraphicsDevice.Textures[0] = tex;
            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, vertCount - 2);
        }

        /// <summary>
        /// 可复用的顶点冲击波环：随生命扩张、变薄、淡出，可压扁成贴地椭圆。
        /// 由弹幕维护实例列表，AI 内 <see cref="Update"/>，绘制时在 Immediate/Additive 批次内 <see cref="Draw"/>。
        /// </summary>
        public sealed class ShockRing
        {
            private readonly Vector2 center;
            private readonly float maxRadius;
            private readonly float baseThickness;
            private readonly Color color;
            private readonly float squash;
            private readonly int segments;
            private readonly float phase;
            private readonly float edgeFade;
            private int life;
            private readonly int maxLife;

            public bool Dead => life >= maxLife;

            public ShockRing(Vector2 center, float maxRadius, float thickness, Color color
                , float squash = 1f, int maxLife = 26, int segments = 72, float edgeFade = 0.15f) {
                this.center = center;
                this.maxRadius = maxRadius;
                baseThickness = thickness;
                this.color = color;
                this.squash = squash;
                this.maxLife = maxLife;
                this.segments = segments;
                this.edgeFade = edgeFade;
                phase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public void Update() => life++;

            public void Draw(Texture2D tex) {
                float p = life / (float)maxLife;
                float radius = VaultUtils.EaseOutCubic(p) * maxRadius;
                float alpha = (float)Math.Sin((1f - p) * MathHelper.PiOver2);
                float thickness = baseThickness * (1.4f - p);
                Color inner = color * alpha;
                inner.A = 0;
                Color outer = color * (alpha * edgeFade);
                outer.A = 0;
                DrawShockRing(tex, center - Main.screenPosition, radius, thickness, inner, outer
                    , segments, squash, 0f, radius * 0.04f, phase + life * 0.2f);
            }
        }
    }
}
