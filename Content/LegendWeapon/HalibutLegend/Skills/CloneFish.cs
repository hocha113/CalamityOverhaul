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

        public AbyssFishBoid(Vector2 startPos) {
            var rand = Main.rand;
            Position = startPos;
            Velocity = (rand.NextVector2Unit() * 2f);
            Speed = 2f + rand.NextFloat();
            MaxSpeed = 4.2f;
            SeparationRadius = 36f;
            CohesionRadius = 140f;
            Scale = 0.6f + rand.NextFloat() * 0.5f;
            Frame = rand.NextFloat(6f);
        }

        public void Update(List<AbyssFishBoid> boids, Vector2 targetCenter) {
            Vector2 separation = Vector2.Zero;
            Vector2 alignment = Vector2.Zero;
            Vector2 cohesion = Vector2.Zero;
            int alignCount = 0, cohesionCount = 0;
            foreach (var other in boids) {
                if (other == this) continue;
                float dist = Vector2.Distance(Position, other.Position);
                if (dist < 0.001f) continue;
                if (dist < SeparationRadius) {
                    separation += (Position - other.Position) / dist;
                }
                if (dist < CohesionRadius) {
                    alignment += other.Velocity;
                    cohesion += other.Position;
                    alignCount++; cohesionCount++;
                }
            }
            if (alignCount > 0) alignment /= alignCount;
            if (cohesionCount > 0) {
                cohesion /= cohesionCount;
                cohesion = (cohesion - Position) * 0.01f;
            }
            separation *= 0.6f;
            alignment *= 0.05f;

            Vector2 toTarget = (targetCenter - Position);
            float distTarget = toTarget.Length();
            if (distTarget > 8f)
                toTarget = toTarget.SafeNormalize(Vector2.Zero) * 0.3f;
            else
                toTarget = Vector2.Zero;

            Velocity += separation + alignment + cohesion + toTarget;
            if (Velocity.Length() > MaxSpeed) Velocity = Velocity.SafeNormalize(Vector2.UnitX) * MaxSpeed;
            Position += Velocity;
            Speed = MathHelper.Lerp(Speed, Velocity.Length(), 0.1f);
            Frame += 0.2f + Speed * 0.03f;
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

            // AfterImage 记录
            afterImages.Add(snap);
            if (afterImages.Count > AfterImageCache) afterImages.RemoveAt(0);

            // 处理延迟射击事件
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

            // 初始化鱼群
            boids ??= CreateBoids(Owner.Center);
            foreach (var b in boids) b.Update(boids, Projectile.Center + new Vector2(0, -20));

            // 粒子效果（深渊气泡/暗光）
            particleTimer++;
            if (particleTimer % 6 == 0) {
                SpawnAbyssParticle(Projectile.Center + Main.rand.NextVector2Circular(40, 40));
            }
        }

        private List<AbyssFishBoid> CreateBoids(Vector2 center) {
            var list = new List<AbyssFishBoid>();
            int count = 8;
            for (int i = 0; i < count; i++) list.Add(new AbyssFishBoid(center + Main.rand.NextVector2Circular(60, 60)));
            return list;
        }

        private void SpawnAbyssParticle(Vector2 pos) {
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 150, default, 0.7f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity *= 0.1f;
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

            cp.hair = Owner.hair;
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

            // 主体
            cp.position = snap.Position;
            cp.bodyFrame = snap.BodyFrame;
            try {
                Main.PlayerRenderer.DrawPlayer(Main.Camera, cp, cp.position, 0f, cp.fullRotationOrigin);
            }
            catch { }

            // 鱼群绘制
            if (boids != null) {
                Main.instance.LoadItem(ItemID.FrostMinnow);
                Texture2D fishTex = TextureAssets.Item[ItemID.FrostMinnow].Value;
                foreach (var b in boids) {
                    Rectangle rect = fishTex.Bounds;
                    SpriteEffects spriteEffects = b.Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    float rot = b.Velocity.ToRotation() + b.Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                    Vector2 origin = rect.Size() * 0.5f;
                    float fade = 0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + b.Frame) * 0.2f;
                    Color c = new Color(70, 200, 255, 255) * fade;
                    Main.spriteBatch.Draw(fishTex, b.Position - Main.screenPosition, rect, c, rot, origin, b.Scale * 0.6f, spriteEffects, 0f);
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
