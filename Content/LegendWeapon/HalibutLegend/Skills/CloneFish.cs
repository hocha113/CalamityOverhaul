using CalamityMod.Items.Weapons.Ranged;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class CloneFish
    {
        public static int ID = 3;
        private const int ToggleCD = 12; //0.2 秒左右的触发后摇

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.CloneFishToggleCD > 0) return; //后摇中禁止
            if (!hp.CloneFishActive) {
                Activate(player);
            }
            else {
                Deactivate(player);
            }
            hp.CloneFishToggleCD = ToggleCD;
        }

        public static void Activate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.CloneFishActive)
                return;
            hp.CloneFishActive = true;
            if (Main.myPlayer == player.whoAmI) {
                SpawnCloneProjectiles(player);
            }
        }

        public static void Deactivate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            hp.CloneFishActive = false;
        }

        internal static void SpawnCloneProjectiles(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            var source = player.GetSource_Misc("CloneFishSkill");

            //生成多个克隆体，每个有不同的延迟
            int count = Math.Clamp(hp.CloneCount, 1, 5);
            for (int i = 0; i < count; i++) {
                int delay = hp.CloneMinDelay + (i * hp.CloneInterval);
                int proj = Projectile.NewProjectile(source, player.Center, Vector2.Zero
                    , ModContent.ProjectileType<ClonePlayer>(), 0, 0, player.whoAmI, delay);
            }
        }
    }

    #region 数据结构
    internal struct PlayerSnapshot
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int Direction;
        public int SelectedItem;
        public int ItemAnimation;
        public int ItemTime;
        public float ItemRotation;
        public Rectangle BodyFrame;
        public Rectangle LegFrame;

        public PlayerSnapshot(Player p) {
            Position = p.position;
            Velocity = p.velocity;
            Direction = p.direction;
            SelectedItem = p.selectedItem;
            ItemAnimation = p.itemAnimation;
            ItemTime = p.itemTime;
            ItemRotation = p.itemRotation;
            BodyFrame = p.bodyFrame;
            LegFrame = p.legFrame;
        }
    }

    internal struct CloneShootEvent
    {
        public int FrameIndex;
        public Vector2 Position;
        public Vector2 Velocity;
        public int Type;
        public int Damage;
        public float KnockBack;
        public int Owner;
        public int ItemType;
    }
    #endregion

    internal class AbyssFishBoid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        private float Speed;
        private readonly float MaxSpeed;
        private readonly float SeparationRadius;
        private readonly float CohesionRadius;
        public float Scale;
        public float Frame;
        private float DesiredRadiusBase;
        private float OrbitAngle;
        private float OrbitSpeed;
        private float NoiseSeed;
        public Vector2 ScatterVelocity;
        public float ScatterProgress;

        public AbyssFishBoid(Vector2 startPos) {
            var rand = Main.rand;
            Position = startPos;
            Velocity = (rand.NextVector2Unit() * 2f);
            Speed = 2f + rand.NextFloat();
            MaxSpeed = 6.0f;
            SeparationRadius = 40f;
            CohesionRadius = 150f;
            Scale = 0.6f + rand.NextFloat() * 0.5f;
            Frame = rand.NextFloat(6f);
            DesiredRadiusBase = 40f + rand.NextFloat() * 50f;
            OrbitAngle = rand.NextFloat(MathHelper.TwoPi);
            OrbitSpeed = 0.03f + rand.NextFloat() * 0.04f;
            NoiseSeed = rand.NextFloat(1000f);
            ScatterVelocity = rand.NextVector2Unit() * (3f + rand.NextFloat() * 4f);
            ScatterProgress = 0f;
        }

        public void Update(List<AbyssFishBoid> boids, Vector2 targetCenter, Vector2 targetVelocity) {
            float speedMag = targetVelocity.Length();
            float speedNorm = MathHelper.Clamp(speedMag / 18f, 0f, 1f);
            float radiusScale = MathHelper.Lerp(1.35f, 0.55f, speedNorm);
            float desiredRadius = DesiredRadiusBase * radiusScale;

            OrbitAngle += OrbitSpeed * (1.0f + speedNorm * 0.8f);
            float wobble = (float)Math.Sin(OrbitAngle * 2f + NoiseSeed) * (6f * radiusScale);
            float dynamicRadius = desiredRadius + wobble;
            Vector2 orbitPos = targetCenter + OrbitAngle.ToRotationVector2() * dynamicRadius;

            Vector2 separation = Vector2.Zero;
            Vector2 alignment = Vector2.Zero;
            Vector2 cohesion = Vector2.Zero;
            int alignCount = 0, cohesionCount = 0;
            foreach (var other in boids) {
                if (other == this) continue;
                float dist = Vector2.Distance(Position, other.Position);
                if (dist < 0.001f) continue;
                if (dist < SeparationRadius) separation += (Position - other.Position) / dist;
                if (dist < CohesionRadius) {
                    alignment += other.Velocity;
                    cohesion += other.Position;
                    alignCount++; cohesionCount++;
                }
            }
            if (alignCount > 0) alignment /= alignCount;
            if (cohesionCount > 0) {
                cohesion /= cohesionCount;
                cohesion = (cohesion - Position) * 0.02f;
            }
            separation *= 0.8f;
            alignment *= 0.07f;

            Vector2 toOrbit = (orbitPos - Position) * 0.22f;

            float centerDist = Vector2.Distance(Position, targetCenter);
            if (centerDist > dynamicRadius * 3f) {
                toOrbit += (targetCenter - Position).SafeNormalize(Vector2.Zero) * 3.2f;
            }

            float time = Main.GameUpdateCount * 0.07f + NoiseSeed;
            Vector2 jitter = new Vector2((float)Math.Sin(time * 1.5f), (float)Math.Cos(time * 1.9f)) * (0.9f + speedNorm * 0.6f);

            Velocity += separation + alignment + cohesion + toOrbit + jitter * 0.5f;
            Velocity = (Velocity * 0.85f);
            Position += targetVelocity * 0.75f;

            float len = Velocity.Length();
            float dynMax = MathHelper.Lerp(MaxSpeed * 0.55f, MaxSpeed, speedNorm * 0.8f);
            if (len > dynMax) Velocity = Velocity * (dynMax / len);

            Position += Velocity;
            Speed = MathHelper.Lerp(Speed, Velocity.Length(), 0.2f);
            Frame += 0.30f + Speed * 0.04f;
        }

        public void UpdateScatter() {
            ScatterProgress += 0.02f;
            Position += ScatterVelocity;
            ScatterVelocity *= 0.96f;
            Scale *= 0.97f;
            Frame += 0.4f;
        }
    }

    internal class ClonePlayer : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private static Player cloneRenderPlayer;
        private readonly List<PlayerSnapshot> afterImages = new();
        private const int AfterImageCache = 12;
        private List<AbyssFishBoid> boids;
        private int particleTimer;
        private Vector2 lastCenter;

        //动画状态
        private enum AnimState { Spawning, Active, Dissolving }
        private AnimState currentState = AnimState.Spawning;
        private int animTimer = 0;
        private const int SpawnDuration = 45;
        private const int DissolveDuration = 50;
        private float cloneAlpha = 0f;

        //延迟配置（通过 ai[0] 传递）
        private int replayDelay;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 40;
            Projectile.timeLeft = 2;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }
            var hp = Owner.GetOverride<HalibutPlayer>();

            //第一帧初始化延迟
            if (Projectile.localAI[0] == 0f) {
                replayDelay = (int)Projectile.ai[0];
                if (replayDelay <= 0) replayDelay = 30; //默认最小延迟
                Projectile.localAI[0] = 1f;
            }

            //检测外部关闭信号
            if (hp == null || !hp.CloneFishActive) {
                if (currentState != AnimState.Dissolving) {
                    StartDissolve();
                }
            }

            Projectile.timeLeft = 2;

            //状态机
            switch (currentState) {
                case AnimState.Spawning:
                    UpdateSpawning(hp);
                    break;
                case AnimState.Active:
                    UpdateActive(hp);
                    break;
                case AnimState.Dissolving:
                    UpdateDissolving();
                    break;
            }
        }

        private void UpdateSpawning(HalibutPlayer hp) {
            animTimer++;
            cloneAlpha = MathHelper.Clamp(animTimer / (float)SpawnDuration, 0f, 1f);

            boids ??= CreateBoidsForSpawn(Projectile.Center);
            Vector2 gatherTarget = Projectile.Center;
            foreach (var b in boids) {
                Vector2 toCenter = (gatherTarget - b.Position) * 0.15f;
                b.Velocity += toCenter;
                if (b.Velocity.Length() > 5f) b.Velocity = b.Velocity.SafeNormalize(Vector2.Zero) * 5f;
                b.Position += b.Velocity;
                b.Frame += 0.3f;
            }

            if (animTimer % 3 == 0) {
                SpawnSpawnParticle(Projectile.Center + Main.rand.NextVector2Circular(60, 60));
            }

            if (animTimer >= SpawnDuration) {
                currentState = AnimState.Active;
                animTimer = 0;
                cloneAlpha = 1f;
                boids = CreateBoids(Projectile.Center);
                lastCenter = Projectile.Center;
            }
        }

        private void UpdateActive(HalibutPlayer hp) {
            //检查是否有足够的快照
            if (hp.CloneSnapshots.Count < replayDelay) return;

            int index = hp.CloneSnapshots.Count - replayDelay;
            var snap = hp.CloneSnapshots[index];
            Projectile.Center = snap.Position + Owner.Size * 0.5f;
            Projectile.velocity = snap.Velocity;

            afterImages.Add(snap);
            if (afterImages.Count > AfterImageCache) afterImages.RemoveAt(0);

            //重放射击事件
            int replayFrame = hp.CloneFrameCounter - replayDelay;
            int shootNum = 1;
            if (hp.CloneShootEvents.Count > 0) {
                for (int i = 0; i < hp.CloneShootEvents.Count; i++) {
                    var ev = hp.CloneShootEvents[i];
                    if (ev.FrameIndex == replayFrame && Projectile.IsOwnedByLocalPlayer()) {
                        for (int j = 0; j < shootNum; j++) {
                            int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis()
                            , snap.Position + Owner.Size * 0.5f, ev.Velocity, ev.Type, ev.Damage, ev.KnockBack, Owner.whoAmI);
                            Main.projectile[proj].friendly = true;
                        }
                    }
                }
            }

            boids ??= CreateBoids(Owner.Center);
            Vector2 clusterTarget = Projectile.Center + new Vector2(0, -16);
            Vector2 targetVel = (Projectile.Center - lastCenter);
            lastCenter = Projectile.Center;
            foreach (var b in boids) b.Update(boids, clusterTarget, targetVel);

            particleTimer++;
            if (particleTimer % 5 == 0) {
                SpawnAbyssParticle(Projectile.Center + Main.rand.NextVector2Circular(46, 46));
            }
        }

        private void UpdateDissolving() {
            animTimer++;
            cloneAlpha = 1f - MathHelper.Clamp(animTimer / (float)DissolveDuration, 0f, 1f);

            if (boids != null) {
                foreach (var b in boids) {
                    b.UpdateScatter();
                    if (animTimer % 4 == 0 && b.ScatterProgress < 0.6f) {
                        SpawnDissolveParticle(b.Position);
                    }
                }
            }

            if (animTimer % 2 == 0) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(30, 50);
                SpawnDissolveParticle(pos);
            }

            if (animTimer >= DissolveDuration) {
                Projectile.Kill();
            }
        }

        private void StartDissolve() {
            currentState = AnimState.Dissolving;
            animTimer = 0;
        }

        private static List<AbyssFishBoid> CreateBoids(Vector2 center) {
            var list = new List<AbyssFishBoid>();
            int count = 10;
            for (int i = 0; i < count; i++) list.Add(new AbyssFishBoid(center + Main.rand.NextVector2Circular(40, 40)));
            return list;
        }

        private static List<AbyssFishBoid> CreateBoidsForSpawn(Vector2 center) {
            var list = new List<AbyssFishBoid>();
            int count = 10;
            for (int i = 0; i < count; i++) {
                var boid = new AbyssFishBoid(center + Main.rand.NextVector2Circular(180, 180));
                boid.Velocity = (center - boid.Position).SafeNormalize(Vector2.Zero) * 3f;
                list.Add(boid);
            }
            return list;
        }

        private static void SpawnAbyssParticle(Vector2 pos) {
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 150, default, 0.75f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity *= 0.15f;
            Main.dust[dust].velocity += Main.rand.NextVector2Circular(0.4f, 0.4f);
        }

        private static void SpawnSpawnParticle(Vector2 pos) {
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 180, 255), 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
        }

        private static void SpawnDissolveParticle(Vector2 pos) {
            int dustType = Main.rand.NextBool() ? DustID.Water : DustID.WaterCandle;
            int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 120, new Color(80, 150, 255), 1.0f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Main.rand.NextVector2CircularEdge(2.5f, 2.5f);
            Main.dust[dust].fadeIn = 1.2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            var hp = Owner.GetOverride<HalibutPlayer>();

            PlayerSnapshot snap = default;
            bool drawPlayer = false;
            if (currentState == AnimState.Active && hp != null && hp.CloneSnapshots.Count >= replayDelay) {
                int index = hp.CloneSnapshots.Count - replayDelay;
                snap = hp.CloneSnapshots[index];
                drawPlayer = true;
            }
            else if (currentState == AnimState.Spawning || currentState == AnimState.Dissolving) {
                snap = new PlayerSnapshot {
                    Position = Projectile.position,
                    Velocity = Projectile.velocity,
                    Direction = Owner.direction,
                    BodyFrame = Owner.bodyFrame,
                    LegFrame = Owner.legFrame
                };
                drawPlayer = true;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            if (drawPlayer && cloneAlpha > 0.01f) {
                cloneRenderPlayer ??= new Player();
                var cp = cloneRenderPlayer;
                cp.ResetEffects();
                cp.CopyVisuals(Owner);
                cp.position = snap.Position;
                cp.velocity = snap.Velocity;
                cp.direction = snap.Direction;
                cp.bodyFrame = snap.BodyFrame;
                cp.legFrame = snap.LegFrame;
                cp.itemAnimation = snap.ItemAnimation;
                cp.itemRotation = snap.ItemRotation;
                cp.whoAmI = Owner.whoAmI;

                Color drawColor = Color.BlueViolet * cloneAlpha;
                cp.skinVariant = Owner.skinVariant;
                cp.skinColor = drawColor;
                cp.shirtColor = drawColor;
                cp.underShirtColor = drawColor;
                cp.pantsColor = drawColor;
                cp.shoeColor = drawColor;
                cp.hairColor = drawColor;
                cp.eyeColor = drawColor;

                if (cp.itemAnimation > 0) {
                    Texture2D gun = TextureAssets.Item[ModContent.ItemType<HalibutCannon>()].Value;
                    Main.spriteBatch.Draw(gun, cp.Center - Main.screenPosition + cp.itemRotation.ToRotationVector2() * 42 * cp.direction, null, Color.BlueViolet * 0.75f
                        , cp.itemRotation, gun.Size() / 2, 1f, cp.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
                }

                try { Main.PlayerRenderer.DrawPlayer(Main.Camera, cp, cp.position, 0f, cp.fullRotationOrigin); } catch { }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            if (boids != null) {
                Main.instance.LoadItem(ItemID.FrostMinnow);
                Texture2D fishTex = TextureAssets.Item[ItemID.FrostMinnow].Value;
                foreach (var b in boids) {
                    Rectangle rect = fishTex.Bounds;
                    SpriteEffects spriteEffects = b.Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                    float rot = b.Velocity.ToRotation() + (b.Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                    Vector2 origin = rect.Size() * 0.5f;
                    float fade = 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + b.Frame) * 0.25f;
                    float alphaMod = currentState == AnimState.Dissolving ? (1f - b.ScatterProgress) : cloneAlpha;
                    Color c = new Color(70, 200, 255, 255) * fade * alphaMod;
                    Main.spriteBatch.Draw(fishTex, b.Position - Main.screenPosition, rect, c, rot, origin, b.Scale * 0.55f, spriteEffects, 0f);
                }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
            return false;
        }
    }
}
