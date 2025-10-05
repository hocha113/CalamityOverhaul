using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class CloneFish
    {
        public static int ID = 3;
        public const int ReplayDelay = 60;
        private const int ToggleCD = 12; // 0.2 秒左右的触发后摇

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.CloneFishToggleCD > 0) return; // 后摇中禁止
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
                SpawnCloneProjectile(player);
            }
        }

        public static void Deactivate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            hp.CloneFishActive = false;
        }

        internal static void SpawnCloneProjectile(Player player) {
            var source = player.GetSource_Misc("CloneFishSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<ClonePlayer>(), 0, 0, player.whoAmI);
        }
    }

    #region 数据结构
    internal struct PlayerSnapshot {
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

    internal struct CloneShootEvent {
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

    internal class AbyssFishBoid {
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

        public AbyssFishBoid(Vector2 startPos) {
            var rand = Main.rand;
            Position = startPos;
            Velocity = (rand.NextVector2Unit() * 2f);
            Speed = 2f + rand.NextFloat();
            MaxSpeed = 6.0f; // 再提升一点上限，适应高速跟随
            SeparationRadius = 40f;
            CohesionRadius = 150f;
            Scale = 0.6f + rand.NextFloat() * 0.5f;
            Frame = rand.NextFloat(6f);
            DesiredRadiusBase = 40f + rand.NextFloat() * 50f; // 基础半径
            OrbitAngle = rand.NextFloat(MathHelper.TwoPi);
            OrbitSpeed = 0.03f + rand.NextFloat() * 0.04f;
            NoiseSeed = rand.NextFloat(1000f);
        }

        public void Update(List<AbyssFishBoid> boids, Vector2 targetCenter, Vector2 targetVelocity) {
            // 根据目标速度动态调整环绕半径：速度快 -> 缩紧，慢 -> 放开
            float speedMag = targetVelocity.Length();
            // 速度映射 (0 -> 1) (0 ~ 18 假设)
            float speedNorm = MathHelper.Clamp(speedMag / 18f, 0f, 1f);
            float radiusScale = MathHelper.Lerp(1.35f, 0.55f, speedNorm); // 高速时半径变小
            float desiredRadius = DesiredRadiusBase * radiusScale;

            OrbitAngle += OrbitSpeed * (1.0f + speedNorm * 0.8f); // 高速时旋转更快
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

            // 轨道吸引跟随
            Vector2 toOrbit = (orbitPos - Position) * 0.22f;

            

            // 超距强回拉
            float centerDist = Vector2.Distance(Position, targetCenter);
            if (centerDist > dynamicRadius * 3f) {
                toOrbit += (targetCenter - Position).SafeNormalize(Vector2.Zero) * 3.2f;
            }

            // 随机噪声（随速度增强紧张感）
            float time = (float)Main.GameUpdateCount * 0.07f + NoiseSeed;
            Vector2 jitter = new Vector2((float)Math.Sin(time * 1.5f), (float)Math.Cos(time * 1.9f)) * (0.9f + speedNorm * 0.6f);

            Velocity += separation + alignment + cohesion + toOrbit + jitter * 0.5f;

            // 末端再直接加一点速度锚定（保证不会拖尾偏离）
            Velocity = (Velocity * 0.85f);

            Position += targetVelocity * 0.75f;

            // 限速
            float len = Velocity.Length();
            float dynMax = MathHelper.Lerp(MaxSpeed * 0.55f, MaxSpeed, speedNorm * 0.8f); // 高速移动时允许更高跟随速度
            if (len > dynMax) Velocity = Velocity * (dynMax / len);

            Position += Velocity;
            Speed = MathHelper.Lerp(Speed, Velocity.Length(), 0.2f);
            Frame += 0.30f + Speed * 0.04f;
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
            if (hp == null || !hp.CloneFishActive) { Projectile.Kill(); return; }
            Projectile.timeLeft = 2;

            if (hp.CloneSnapshots.Count < CloneFish.ReplayDelay) return;
            int index = hp.CloneSnapshots.Count - CloneFish.ReplayDelay;
            var snap = hp.CloneSnapshots[index];
            Projectile.Center = snap.Position + Owner.Size * 0.5f;
            Projectile.velocity = snap.Velocity;

            afterImages.Add(snap);
            if (afterImages.Count > AfterImageCache) afterImages.RemoveAt(0);

            int replayFrame = hp.CloneFrameCounter - CloneFish.ReplayDelay;
            if (hp.CloneShootEvents.Count > 0) {
                for (int i = 0; i < hp.CloneShootEvents.Count; i++) {
                    var ev = hp.CloneShootEvents[i];
                    if (ev.FrameIndex == replayFrame && Projectile.IsOwnedByLocalPlayer()) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis()
                            , snap.Position + Owner.Size * 0.5f, ev.Velocity, ev.Type, ev.Damage, ev.KnockBack, Owner.whoAmI);
                        Main.projectile[proj].friendly = true;
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

        private List<AbyssFishBoid> CreateBoids(Vector2 center) {
            var list = new List<AbyssFishBoid>();
            int count = 10;
            for (int i = 0; i < count; i++) list.Add(new AbyssFishBoid(center + Main.rand.NextVector2Circular(40, 40)));
            return list;
        }

        private void SpawnAbyssParticle(Vector2 pos) {
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 150, default, 0.75f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity *= 0.15f;
            Main.dust[dust].velocity += Main.rand.NextVector2Circular(0.4f, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            var hp = Owner.GetOverride<HalibutPlayer>();
            if (hp == null || hp.CloneSnapshots.Count < CloneFish.ReplayDelay) return false;

            int index = hp.CloneSnapshots.Count - CloneFish.ReplayDelay;
            var snap = hp.CloneSnapshots[index];

            cloneRenderPlayer ??= new Player();
            var cp = cloneRenderPlayer;
            cp.ResetEffects();
            cp.CopyVisuals(Owner);
            cp.position = snap.Position;
            cp.velocity = snap.Velocity;
            cp.direction = snap.Direction;
            cp.selectedItem = snap.SelectedItem;
            cp.itemAnimation = snap.ItemAnimation;
            cp.itemTime = snap.ItemTime;
            cp.itemRotation = snap.ItemRotation;
            cp.bodyFrame = snap.BodyFrame;
            cp.legFrame = snap.LegFrame;
            cp.whoAmI = Owner.whoAmI;

            Color drawColor = Color.BlueViolet;
            cp.skinVariant = Owner.skinVariant;
            cp.skinColor = drawColor;
            cp.shirtColor = drawColor;
            cp.underShirtColor = drawColor;
            cp.pantsColor = drawColor;
            cp.shoeColor = drawColor;
            cp.hairColor = drawColor;
            cp.eyeColor = drawColor;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            cp.position = snap.Position;
            cp.bodyFrame = snap.BodyFrame;
            try { Main.PlayerRenderer.DrawPlayer(Main.Camera, cp, cp.position, 0f, cp.fullRotationOrigin); } catch { }

            if (boids != null) {
                Main.instance.LoadItem(ItemID.FrostMinnow);
                Texture2D fishTex = TextureAssets.Item[ItemID.FrostMinnow].Value;
                foreach (var b in boids) {
                    Rectangle rect = fishTex.Bounds;
                    SpriteEffects spriteEffects = b.Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                    float rot = b.Velocity.ToRotation() + (b.Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                    Vector2 origin = rect.Size() * 0.5f;
                    float fade = 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + b.Frame) * 0.25f;
                    Color c = new Color(70, 200, 255, 255) * fade;
                    Main.spriteBatch.Draw(fishTex, b.Position - Main.screenPosition, rect, c, rot, origin, b.Scale * 0.55f, spriteEffects, 0f);
                }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
            return false;
        }
    }

    internal static class PlayerCloneExtensions {
        public static void CopyVisuals(this Player dst, Player src) {
            dst.skinVariant = src.skinVariant;
            dst.hair = src.hair;
            dst.hairColor = src.hairColor;
            dst.skinColor = src.skinColor;
            dst.eyeColor = src.eyeColor;
            dst.shirtColor = src.shirtColor;
            dst.underShirtColor = src.underShirtColor;
            dst.pantsColor = src.pantsColor;
            dst.shoeColor = src.shoeColor;
            for (int i = 0; i < dst.armor.Length && i < src.armor.Length; i++) {
                dst.armor[i] = src.armor[i].Clone();
            }
            for (int i = 0; i < dst.dye.Length && i < src.dye.Length; i++) {
                dst.dye[i] = src.dye[i].Clone();
            }
            dst.Male = src.Male;
            dst.bodyFrame = src.bodyFrame;
            dst.legFrame = src.legFrame;
            dst.headRotation = src.headRotation;
            dst.bodyRotation = src.bodyRotation;
            dst.legRotation = src.legRotation;
        }
    }
}
