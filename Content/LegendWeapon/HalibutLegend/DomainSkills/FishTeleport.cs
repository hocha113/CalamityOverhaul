using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    internal static class FishTeleport
    {
        public static int ID = 7;
        private const int ToggleCD = 15;
        private const int TeleportCooldown = 180; //3秒冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.FishTeleportToggleCD > 0 || hp.FishTeleportCooldown > 0) return;

            //计算目标位置（可能被领域限制）
            Vector2 targetPos = CalculateTeleportTarget(player);

            Activate(player, targetPos);
            hp.FishTeleportToggleCD = ToggleCD;
            hp.FishTeleportCooldown = TeleportCooldown;
        }

        private static Vector2 CalculateTeleportTarget(Player player) {
            Vector2 mouseWorld = Main.MouseWorld;
            Vector2 toMouse = mouseWorld - player.Center;
            float distance = toMouse.Length();

            //检查是否在海域领域内
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SeaDomainActive) {
                //查找领域弹幕
                float maxDomainRadius = GetActiveDomainRadius(player);
                if (maxDomainRadius > 0 && distance > maxDomainRadius) {
                    //限制在领域范围内
                    return player.Center + toMouse.SafeNormalize(Vector2.Zero) * maxDomainRadius;
                }
            }
            else {
                return player.Center;
            }

            //无领域或在范围内，直接传送到鼠标位置
            return mouseWorld;
        }

        private static float GetActiveDomainRadius(Player player) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.ModProjectile is SeaDomainProj domain) {
                    return domain.GetMaxRadius();
                }
            }
            return 0f;
        }

        public static void Activate(Player player, Vector2 targetPos) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnTeleportEffect(player, targetPos);
            }
        }

        internal static void SpawnTeleportEffect(Player player, Vector2 targetPos) {
            var source = player.GetSource_Misc("FishTeleportSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<FishTeleportProj>(), 0, 0, player.whoAmI,
                ai0: targetPos.X, ai1: targetPos.Y);
        }
    }

    #region 瞬移鱼群粒子
    internal class TeleportFishParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Frame;
        public int FishType;
        public Color TintColor;
        public float Life;
        public float MaxLife;
        public float Rotation;
        private float rotationSpeed;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 10;

        public TeleportFishParticle(Vector2 pos, Vector2 vel, bool isDissipating) {
            var rand = Main.rand;
            Position = pos;
            Velocity = vel;
            Scale = 0.6f + rand.NextFloat() * 0.4f;
            Frame = rand.NextFloat(10f);
            FishType = rand.Next(3);
            Life = 0f;
            MaxLife = isDissipating ? 30f : 25f;
            Rotation = vel.ToRotation();
            rotationSpeed = rand.NextFloat(-0.1f, 0.1f);

            TintColor = new Color(100 + rand.Next(50), 200 + rand.Next(55), 255);
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += rotationSpeed;
            Frame += 0.4f;

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public float GetAlpha() {
            float progress = Life / MaxLife;
            if (progress < 0.3f) {
                return progress / 0.3f; //淡入
            }
            else if (progress > 0.7f) {
                return 1f - ((progress - 0.7f) / 0.3f); //淡出
            }
            return 1f;
        }

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 2) return;
            Texture2D tex = VaultAsset.placeholder2.Value;
            float particleAlpha = GetAlpha() * globalAlpha;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * particleAlpha * 0.6f;
                float width = Scale * (4f - progress * 2.5f);

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float rot = diff.ToRotation();
                float len = diff.Length();

                Color c = TintColor * trailAlpha;
                Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    c, rot, Vector2.Zero, new Vector2(len, width), SpriteEffects.None, 0f);
            }
        }

        public void Draw(float globalAlpha) {
            int itemType = FishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };

            Main.instance.LoadItem(itemType);
            Texture2D fishTex = TextureAssets.Item[itemType].Value;
            Rectangle rect = fishTex.Bounds;
            Vector2 origin = rect.Size() * 0.5f;

            float particleAlpha = GetAlpha() * globalAlpha;
            Color c = TintColor * particleAlpha;

            //发光效果
            for (int i = 0; i < 3; i++) {
                Vector2 offset = (i * MathHelper.TwoPi / 3f).ToRotationVector2() * 2.5f;
                Main.spriteBatch.Draw(fishTex, Position + offset - Main.screenPosition, rect,
                    c * 0.4f, Rotation, origin, Scale * 0.9f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(fishTex, Position - Main.screenPosition, rect, c, Rotation, origin, Scale, SpriteEffects.None, 0f);
        }
    }
    #endregion

    internal class FishTeleportProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<TeleportFishParticle> dissipatingFish; //消散鱼群
        private List<TeleportFishParticle> gatheringFish; //聚拢鱼群
        private Vector2 targetPosition;

        private enum TeleportState { Dissipating, Teleporting, Gathering, Complete }
        private TeleportState currentState = TeleportState.Dissipating;
        private int stateTimer = 0;
        private const int DissipateDuration = 25;
        private const int TeleportDuration = 5;
        private const int GatherDuration = 20;
        private float effectAlpha = 1f;

        //特效元素
        private float dissipateFlashIntensity = 0f;
        private float gatherFlashIntensity = 0f;
        private readonly List<TeleportRing> teleportRings = new();
        private Vector2 dissipateCenter;
        private bool hasTeleported = false;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.timeLeft = DissipateDuration + TeleportDuration + GatherDuration + 10;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }

            //首帧初始化
            if (Projectile.localAI[0] == 0f) {
                targetPosition = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                dissipateCenter = Owner.Center;
                InitializeDissipatingFish();
                Projectile.localAI[0] = 1f;

                //播放消散音效
                SoundEngine.PlaySound(SoundID.Item8, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.5f }, Owner.Center); //水波音效
            }

            stateTimer++;

            switch (currentState) {
                case TeleportState.Dissipating:
                    UpdateDissipating();
                    break;
                case TeleportState.Teleporting:
                    UpdateTeleporting();
                    break;
                case TeleportState.Gathering:
                    UpdateGathering();
                    break;
                case TeleportState.Complete:
                    Projectile.Kill();
                    break;
            }

            //更新消散鱼群
            if (dissipatingFish != null) {
                for (int i = dissipatingFish.Count - 1; i >= 0; i--) {
                    dissipatingFish[i].Update();
                    if (dissipatingFish[i].ShouldRemove()) {
                        dissipatingFish.RemoveAt(i);
                    }
                }
            }

            //更新聚拢鱼群
            if (gatheringFish != null) {
                for (int i = gatheringFish.Count - 1; i >= 0; i--) {
                    gatheringFish[i].Update();
                    if (gatheringFish[i].ShouldRemove()) {
                        gatheringFish.RemoveAt(i);
                    }
                }
            }

            //更新传送环
            for (int i = teleportRings.Count - 1; i >= 0; i--) {
                teleportRings[i].Update();
                if (teleportRings[i].ShouldRemove()) {
                    teleportRings.RemoveAt(i);
                }
            }
        }

        private void UpdateDissipating() {
            float progress = stateTimer / (float)DissipateDuration;
            dissipateFlashIntensity = (float)Math.Sin(progress * MathHelper.Pi) * 1.5f;

            //玩家渐隐
            Owner.opacityForAnimation = 1f - progress;

            //生成消散粒子
            if (stateTimer % 2 == 0) {
                SpawnDissipateParticle();
            }

            //生成传送环（起点）
            if (stateTimer % 6 == 0) {
                teleportRings.Add(new TeleportRing(dissipateCenter, false));
            }

            if (stateTimer >= DissipateDuration) {
                currentState = TeleportState.Teleporting;
                stateTimer = 0;
                Owner.opacityForAnimation = 0f;

                //播放传送音效
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f }, Owner.Center);
            }
        }

        private void UpdateTeleporting() {
            if (!hasTeleported) {
                //执行传送
                Owner.Teleport(targetPosition, 999);
                Owner.velocity = Vector2.Zero;

                //生成传送特效
                for (int i = 0; i < 30; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = targetPosition + angle.ToRotationVector2() * Main.rand.NextFloat(80f);
                    int dust = Dust.NewDust(pos, 1, 1, DustID.Electric, 0, 0, 0, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (targetPosition - pos).SafeNormalize(Vector2.Zero) * 8f;
                }

                InitializeGatheringFish();
                hasTeleported = true;

                //播放出现音效
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f }, targetPosition);
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact, targetPosition);
            }

            if (stateTimer >= TeleportDuration) {
                currentState = TeleportState.Gathering;
                stateTimer = 0;
            }
        }

        private void UpdateGathering() {
            float progress = stateTimer / (float)GatherDuration;
            gatherFlashIntensity = (float)Math.Sin(progress * MathHelper.Pi) * 2f;

            //玩家渐现
            Owner.opacityForAnimation = progress;

            //生成聚拢粒子
            if (stateTimer % 2 == 0) {
                SpawnGatherParticle();
            }

            //生成传送环（终点）
            if (stateTimer % 5 == 0) {
                teleportRings.Add(new TeleportRing(targetPosition, true));
            }

            if (stateTimer >= GatherDuration) {
                currentState = TeleportState.Complete;
                Owner.opacityForAnimation = 1f;

                //最终爆发效果
                for (int i = 0; i < 20; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = targetPosition + Main.rand.NextVector2Circular(30, 30);
                    int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(120, 220, 255), 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 6f;
                }

                SoundEngine.PlaySound(SoundID.Splash, targetPosition);
            }
        }

        private void InitializeDissipatingFish() {
            dissipatingFish = new List<TeleportFishParticle>();
            int fishCount = 80;

            for (int i = 0; i < fishCount; i++) {
                float angle = (i / (float)fishCount) * MathHelper.TwoPi;
                Vector2 spawnPos = dissipateCenter + Main.rand.NextVector2Circular(40, 40);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);
                dissipatingFish.Add(new TeleportFishParticle(spawnPos, velocity, true));
            }
        }

        private void InitializeGatheringFish() {
            gatheringFish = new List<TeleportFishParticle>();
            int fishCount = 80;

            for (int i = 0; i < fishCount; i++) {
                float angle = (i / (float)fishCount) * MathHelper.TwoPi;
                Vector2 spawnPos = targetPosition + angle.ToRotationVector2() * Main.rand.NextFloat(150f, 250f);
                Vector2 velocity = (targetPosition - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 18f);
                gatheringFish.Add(new TeleportFishParticle(spawnPos, velocity, false));
            }
        }

        private void SpawnDissipateParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = dissipateCenter + Main.rand.NextVector2Circular(50, 50);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2() * 4f;
        }

        private void SpawnGatherParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = targetPosition + angle.ToRotationVector2() * Main.rand.NextFloat(100f, 150f);
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 120, new Color(120, 220, 255), 1.3f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (targetPosition - pos).SafeNormalize(Vector2.Zero) * 5f;
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制消散闪光
            if (dissipateFlashIntensity > 0f) {
                DrawTeleportFlash(dissipateCenter, dissipateFlashIntensity);
            }

            //绘制聚拢闪光
            if (gatherFlashIntensity > 0f) {
                DrawTeleportFlash(targetPosition, gatherFlashIntensity);
            }

            //绘制传送环
            foreach (var ring in teleportRings) {
                ring.Draw(effectAlpha);
            }

            //绘制消散鱼群拖尾
            if (dissipatingFish != null) {
                foreach (var fish in dissipatingFish) {
                    fish.DrawTrail(effectAlpha);
                }
            }

            //绘制聚拢鱼群拖尾
            if (gatheringFish != null) {
                foreach (var fish in gatheringFish) {
                    fish.DrawTrail(effectAlpha);
                }
            }

            //绘制消散鱼群主体
            if (dissipatingFish != null) {
                foreach (var fish in dissipatingFish) {
                    fish.Draw(effectAlpha);
                }
            }

            //绘制聚拢鱼群主体
            if (gatheringFish != null) {
                foreach (var fish in gatheringFish) {
                    fish.Draw(effectAlpha);
                }
            }
            return false;
        }

        private void DrawTeleportFlash(Vector2 center, float intensity) {
            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            float flashScale = 3f + intensity * 2f;
            Color flashColor = new Color(150, 230, 255, 0) * intensity * 0.4f;

            Main.spriteBatch.Draw(bloomTex, center - Main.screenPosition, null, flashColor,
                0f, bloomTex.Size() / 2f, flashScale, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(bloomTex, center - Main.screenPosition, null, flashColor * 0.7f,
                MathHelper.PiOver2, bloomTex.Size() / 2f, flashScale * 0.8f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(bloomTex, center - Main.screenPosition, null, flashColor * 0.5f,
                MathHelper.PiOver4, bloomTex.Size() / 2f, flashScale * 1.2f, SpriteEffects.None, 0f);
        }
    }

    #region 传送环效果
    internal class TeleportRing
    {
        public Vector2 Center;
        public float Radius;
        public float Life;
        public float MaxLife;
        public float RotationSpeed;
        public bool IsGathering; //true=聚拢环，false=消散环
        private float rotation;
        private float initialRadius;

        public TeleportRing(Vector2 center, bool isGathering) {
            Center = center;
            IsGathering = isGathering;
            initialRadius = isGathering ? 150f : 40f;
            Radius = initialRadius;
            Life = 0f;
            MaxLife = 50f;
            RotationSpeed = Main.rand.NextFloat(0.08f, 0.15f) * (Main.rand.NextBool() ? 1 : -1);
            rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            float progress = Life / MaxLife;

            if (IsGathering) {
                //聚拢环：从大到小
                Radius = initialRadius * (1f - progress * 0.8f);
            }
            else {
                //消散环：从小到大
                Radius = initialRadius + progress * 100f;
            }

            rotation += RotationSpeed;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(float globalAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * globalAlpha * 0.8f;
            if (alpha < 0.01f) return;

            Texture2D tex = CWRAsset.StarTexture.Value;
            int segments = 20;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle = i * angleStep + rotation;
                Vector2 pos = Center + angle.ToRotationVector2() * Radius;
                float scale = 0.6f + (float)Math.Sin(angle * 2f + Main.GlobalTimeWrappedHourly * 3f) * 0.25f;
                Color c = new Color(120, 230, 255, 0) * alpha;

                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, c, angle,
                    tex.Size() / 2f, scale * 0.4f, SpriteEffects.None, 0f);
            }

            //绘制连接线
            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep + rotation;
                float angle2 = (i + 1) * angleStep + rotation;
                Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float rot = diff.ToRotation();
                float length = diff.Length();

                Texture2D lineTex = VaultAsset.placeholder2.Value;
                Color lineColor = new Color(120, 230, 255, 0) * alpha * 0.5f;
                Main.spriteBatch.Draw(lineTex, p1 - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    lineColor, rot, Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion
}
