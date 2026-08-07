using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.VFX;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 替死鬼夺身「这次轮到你」。<br/>
    /// 前兆：心跳两声，血珠自玩家身上渗出、飘向暗处聚成血核；<br/>
    /// 显形：救过你无数次的血臂破暗而至，攥住玩家，两段收紧、再一段拖拽；<br/>
    /// 处决：攥碎，血臂带着你缩回暗处；余韵：掌灶余温与滴落的血痕。<br/>
    /// 材质：替死血（暗底血臂 shader + 绯红血粒子），沿用 <see cref="ScapeArmRenderer"/> 语汇。
    /// </summary>
    internal sealed class ScapeSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 40;
        public override int ExecuteFrame => 124;
        public override int TotalFrames => 184;

        //血臂来处：前兆期聚血的暗点
        private Vector2 gatherPoint;
        private bool gatherSet;
        //收紧脉冲余震计时（帧）
        private int tightenFlash;
        private int tightenStep;
        private bool regripDone;

        private static Texture2D Glow => TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;

        public override void OnBegin() {
            int side = Seed % 2 == 0 ? -1 : 1;
            //优先躲在玩家背后的斜上暗处
            if (Player != null) {
                side = Player.direction != 0 ? -Player.direction : side;
                gatherPoint = Player.Center + new Vector2(side * 236f, -128f)
                    + new Vector2(Seed % 5 * 8f - 16f, Seed % 7 * 6f - 18f);
                gatherSet = true;
            }
            PlayHeartThud(-0.95f);
        }

        public override void Update() {
            if (!gatherSet && Player != null) {
                gatherPoint = Player.Center + new Vector2(-Player.direction * 236f, -128f);
                gatherSet = true;
            }
            if (tightenFlash > 0) {
                tightenFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    UpdateOmen();
                    break;
                case WraithSeizePhase.Manifest:
                    UpdateManifest();
                    break;
                case WraithSeizePhase.Linger:
                    UpdateLinger();
                    break;
            }
        }

        private void UpdateOmen() {
            //第二声心跳更近
            if (Timer == 24) {
                PlayHeartThud(-0.7f);
            }
            //血珠自玩家身上渗出，逆着惯性飘向血核
            if (Timer % 3 == 0) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(16f, 22f);
                Vector2 velocity = (gatherPoint - pos).SafeNormalize(Vector2.UnitX)
                    * Main.rand.NextFloat(1.6f, 3.4f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, velocity,
                    Main.rand.NextBool() ? CrimsonRendHitVFX.Blood : CrimsonRendHitVFX.BloodDeep,
                    Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(18, 30), 0.1f, 0.995f);
            }
        }

        private void UpdateManifest() {
            float progress = PhaseProgress;
            //相位切入帧：血臂破暗而出，攥向玩家
            if (Timer == OmenEndFrame + 1) {
                ScapeArmRenderer.Trigger(gatherPoint, Player.Center);
            }
            //两段收紧
            if (progress >= 0.35f && tightenStep == 0) {
                Tighten();
            }
            if (progress >= 0.72f && tightenStep == 1) {
                Tighten();
            }
            //拖拽中段补第二次攥握，维持血臂在场
            if (progress >= 0.55f && !regripDone) {
                regripDone = true;
                ScapeArmRenderer.Trigger(gatherPoint + new Vector2(0f, 26f), Player.Center);
            }
            //攥握期血珠不断被挤出
            if (Timer % 4 == 0) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(14f, 18f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos,
                    Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0f, 1.2f),
                    CrimsonRendHitVFX.Blood, Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(Main.rand.Next(12, 20), 0.3f);
            }
        }

        private void UpdateLinger() {
            //血痕自死点滴落
            if (Timer % 6 == 0) {
                Vector2 pos = DeathAnchor + Main.rand.NextVector2Circular(20f, 12f);
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.6f, 1.6f)),
                    CrimsonRendHitVFX.BloodDeep, Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(Main.rand.Next(26, 44), 0.2f, 0.99f, Main.rand.Next(30, 50));
            }
        }

        private void Tighten() {
            tightenStep++;
            tightenFlash = 10;
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.5f + tightenStep * 0.12f,
                Volume = 0.6f,
                MaxInstances = 2,
            }, Player.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4.5f, 4.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Player.Center + Main.rand.NextVector2Circular(12f, 16f), velocity,
                    CrimsonRendHitVFX.Arterial, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(14, 24), 0.32f);
            }
        }

        public override void OnExecute() {
            //攥碎：终末大血爆 + 血臂带着人缩回暗处（反向再触发一条臂承担退场行程与红晕）
            ScapeArmRenderer.Trigger(Player.Center, gatherPoint);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with {
                Pitch = -0.85f,
                Volume = 0.9f,
                MaxInstances = 1,
            }, Player.Center);
            for (int i = 0; i < 18; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Color color = Main.rand.NextBool(3)
                    ? CrimsonRendHitVFX.Arterial : CrimsonRendHitVFX.Blood;
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(Player.Center, velocity, color,
                    Main.rand.NextFloat(0.8f, 1.5f))
                    ?.Configure(Main.rand.Next(28, 48), 0.34f, 0.988f, Main.rand.Next(35, 60));
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Player.Center,
                    Main.rand.NextVector2Circular(9f, 9f), CrimsonRendHitVFX.Blood,
                    Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(16, 28), 0.34f);
            }
        }

        public override void Draw(SpriteBatch sb) {
            Vector2 glowOrigin = Glow.Size() * 0.5f;
            Vector2 gatherScreen = gatherPoint - Main.screenPosition;

            //前兆：暗处聚起的血核，越来越亮
            if (Phase == WraithSeizePhase.Omen) {
                float build = PhaseProgress;
                float flick = 0.75f + 0.25f * MathF.Sin(Timer * 0.42f + Seed);
                sb.Draw(Glow, gatherScreen, null, new Color(26, 4, 8) * (0.75f * build), 0f,
                    glowOrigin, 0.5f * build + 0.1f, SpriteEffects.None, 0f);
                sb.Draw(Glow, gatherScreen, null,
                    CrimsonRendHitVFX.BloodDeep * (0.55f * build * flick), 0f,
                    glowOrigin, 0.2f * build + 0.04f, SpriteEffects.None, 0f);
                //血核外的两三根搏动血脉
                DrawVeins(sb, build * 0.8f);
            }

            //显形：掌灶压在玩家身后 + 攥指横过玩家
            if (Phase == WraithSeizePhase.Manifest) {
                Vector2 target = (Player.dead ? DeathAnchor : Player.Center) - Main.screenPosition;
                float tightenBoost = tightenFlash > 0 ? tightenFlash / 10f * 0.35f : 0f;
                sb.Draw(Glow, target, null, new Color(30, 4, 9) * (0.72f + tightenBoost), 0f,
                    glowOrigin, 0.5f, SpriteEffects.None, 0f);
                DrawGripFingers(sb, target, tightenBoost);
                DrawVeins(sb, 0.5f);
            }

            //余韵：掌灶余温冷却 + 一道退回暗处的血渍
            if (Phase == WraithSeizePhase.Linger) {
                float fade = 1f - PhaseProgress;
                Vector2 anchorScreen = DeathAnchor - Main.screenPosition;
                sb.Draw(Glow, anchorScreen, null, new Color(26, 4, 8) * (0.6f * fade), 0f,
                    glowOrigin, 0.42f, SpriteEffects.None, 0f);
                //血渍拖向来处，随余韵缩短
                Vector2 toGather = gatherScreen - anchorScreen;
                int segments = 7;
                for (int i = 0; i < segments; i++) {
                    float t = i / (float)(segments - 1) * fade;
                    Vector2 pos = anchorScreen + toGather * t;
                    float alpha = (1f - t) * 0.5f * fade;
                    sb.Draw(Glow, pos, null, CrimsonRendHitVFX.BloodDeep * alpha, 0f,
                        glowOrigin, MathHelper.Lerp(0.16f, 0.05f, t), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>血核到玩家之间抽搐的细血脉。</summary>
        private void DrawVeins(SpriteBatch sb, float alpha) {
            if (alpha <= 0.02f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 from = gatherPoint;
            Vector2 to = Player.dead ? DeathAnchor : Player.Center;
            const int Veins = 3;
            for (int v = 0; v < Veins; v++) {
                float phase = Seed * 0.7f + v * 2.3f;
                const int Segs = 8;
                Vector2 prev = from;
                for (int i = 1; i <= Segs; i++) {
                    float t = i / (float)Segs;
                    Vector2 baseline = Vector2.Lerp(from, to, t);
                    Vector2 normal = (to - from).SafeNormalize(Vector2.UnitX)
                        .RotatedBy(MathHelper.PiOver2);
                    float wobble = MathF.Sin(t * MathHelper.Pi * 2.2f + Timer * 0.13f + phase)
                        * 14f * MathF.Sin(t * MathHelper.Pi);
                    Vector2 pos = baseline + normal * wobble;
                    Vector2 delta = pos - prev;
                    float len = delta.Length();
                    if (len > 0.5f) {
                        float fadeMid = MathF.Sin(t * MathHelper.Pi);
                        sb.Draw(pixel, prev - Main.screenPosition, src,
                            CrimsonRendHitVFX.BloodDeep * (alpha * 0.5f * fadeMid),
                            delta.ToRotation(), Vector2.Zero, new Vector2(len, 1.4f),
                            SpriteEffects.None, 0f);
                    }
                    prev = pos;
                }
            }
        }

        /// <summary>攥住玩家的血指：垂直于臂向横过身体的几道暗血短棒，随收紧脉冲压重。</summary>
        private void DrawGripFingers(SpriteBatch sb, Vector2 targetScreen, float tightenBoost) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 armDir = (Player.Center - gatherPoint).SafeNormalize(Vector2.UnitX);
            float fingerRot = armDir.ToRotation() + MathHelper.PiOver2;
            float squeeze = 1f - tightenStep * 0.08f - tightenBoost * 0.2f;
            const int Fingers = 4;
            for (int i = 0; i < Fingers; i++) {
                float offset = (i - (Fingers - 1) * 0.5f) * 11f;
                Vector2 pos = targetScreen + armDir * offset;
                float wobble = MathF.Sin(Timer * 0.2f + i * 1.9f + Seed) * 1.5f;
                float length = (34f + wobble) * squeeze;
                float alpha = 0.82f + tightenBoost;
                sb.Draw(pixel, pos, src, new Color(46, 5, 11) * alpha, fingerRot,
                    new Vector2(0.5f), new Vector2(5.2f, length), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos, src, CrimsonRendHitVFX.BloodDeep * (alpha * 0.55f), fingerRot,
                    new Vector2(0.5f), new Vector2(2.2f, length * 0.9f), SpriteEffects.None, 0f);
            }
        }

        public override Vector2 CameraFocus {
            get {
                if (Phase == WraithSeizePhase.Linger) {
                    return DeathAnchor;
                }
                Vector2 center = Player?.Center ?? DeathAnchor;
                //显形期镜头微微偏向血臂来处
                return Phase == WraithSeizePhase.Manifest
                    ? Vector2.Lerp(center, (center + gatherPoint) * 0.5f, 0.3f)
                    : center;
            }
        }

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.12f,
            WraithSeizePhase.Manifest => 1.34f,
            WraithSeizePhase.Linger => 1.16f,
            _ => 1f,
        };

        public override float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 1.5f * PhaseProgress,
            WraithSeizePhase.Manifest => 2.5f + (tightenFlash > 0 ? tightenFlash * 0.45f : 0f),
            _ => 0f,
        };

        public override void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            //显形后段被往血核方向拖
            if (Phase == WraithSeizePhase.Manifest
                && PhaseProgress is > 0.5f and < 0.85f) {
                Vector2 pull = gatherPoint - Player.Center;
                Player.velocity = pull.Length() > 48f
                    ? pull.SafeNormalize(Vector2.Zero) * 1.7f
                    : Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
                return;
            }
            base.UpdatePlayerMotion();
        }

        private void PlayHeartThud(float pitch) {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = pitch,
                Volume = 0.5f,
                MaxInstances = 2,
            }, Player?.Center ?? gatherPoint);
        }
    }
}
