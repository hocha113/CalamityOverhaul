using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>领域装饰</summary>
    internal static class OniDomainDeco
    {
        private enum PetalState : byte { Falling, Frozen, Burning, Fading }

        private class Petal
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Rot;
            public float RotSpeed;
            public float SwayPhase;
            public float Scale;
            public Color Tint;
            public PetalState State;
            public int BurnDelay;
            public float BurnT;
            public float Alpha = 1f;
            public bool UraKind;
            public int BlinkTimer;
        }

        private class Lantern
        {
            public Vector2 Pos;
            public float Scale;
            public float BobPhase;
            public float DriftPhase;
            public float Alpha;
            public int ExtinguishDelay = -1;
            public bool Dying;
        }

        private class Ash
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Rot;
            public float RotSpeed;
            public float Size;
            public int Life;
            public int MaxLife;
        }

        private class Wisp
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public Vector2 Target;
            public bool Homing;
            public float Size;
            public int Life;
            public int MaxLife;
        }

        private static readonly List<Petal> petals = new();
        private static readonly List<Lantern> lanterns = new();
        private static readonly List<Ash> ashes = new();
        private static readonly List<Wisp> wisps = new();

        private const int OmotePetalCap = 44;
        private const int UraPetalCap = 10;
        private const int LanternCap = 8;
        private const int AshCap = 140;
        private const int WispCap = 90;

        private static readonly Color PetalPink = new(255, 205, 216);
        private static readonly Color PetalPinkDeep = new(250, 178, 194);
        private static readonly Color PetalUraRed = new(118, 16, 26);
        private static readonly Color LanternPaper = new(190, 32, 22);

        public static void Clear() {
            petals.Clear();
            lanterns.Clear();
            ashes.Clear();
            wisps.Clear();
        }

        public static void SpawnEyeConverge(Vector2 eyeWorld, int count) {
            for (int i = 0; i < count && wisps.Count < WispCap; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(140f, 280f);
                Vector2 pos = eyeWorld + ang.ToRotationVector2() * dist;
                Vector2 vel = (eyeWorld - pos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2.6f, 5.2f);
                wisps.Add(new Wisp {
                    Pos = pos,
                    Vel = vel,
                    Target = eyeWorld,
                    Homing = true,
                    Size = Main.rand.NextFloat(2.6f, 5.5f),
                    MaxLife = Main.rand.Next(45, 80)
                });
            }
        }

        public static void SpawnEyeScatter(Vector2 eyeWorld, int count) {
            for (int i = 0; i < count && wisps.Count < WispCap; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = eyeWorld + ang.ToRotationVector2() * Main.rand.NextFloat(10f, 55f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3.4f)
                    + new Vector2(0f, -0.7f);
                wisps.Add(new Wisp {
                    Pos = pos,
                    Vel = vel,
                    Homing = false,
                    Size = Main.rand.NextFloat(2.2f, 4.8f),
                    MaxLife = Main.rand.Next(45, 85)
                });
            }
        }

        private static void UpdateWisps() {
            for (int i = wisps.Count - 1; i >= 0; i--) {
                Wisp w = wisps[i];
                w.Life++;
                if (w.Homing) {
                    //加速扑向眼睛，近了就没入

                    Vector2 toT = w.Target - w.Pos;
                    float d = toT.Length();
                    if (d < 16f || w.Life >= w.MaxLife) {
                        wisps.RemoveAt(i);
                        continue;
                    }
                    float speed = w.Vel.Length() * 1.03f + 0.08f;
                    w.Vel = Vector2.Lerp(w.Vel, toT / d * speed, 0.14f);
                }
                else {
                    w.Vel *= 0.965f;
                    w.Vel.Y -= 0.012f;
                    if (w.Life >= w.MaxLife) {
                        wisps.RemoveAt(i);
                        continue;
                    }
                }
                w.Pos += w.Vel;
            }
        }

        /// <summary>死寂、花瓣全部空中冻结</summary>
        public static void NotifyFreeze() {
            foreach (Petal p in petals) {
                if (p.State == PetalState.Falling) {
                    p.State = PetalState.Frozen;
                }
            }
        }

        /// <summary>剥落开始、入里则冻瓣点燃，回表则冻瓣淡出并熄灭灯笼</summary>
        public static void NotifyPeelStart(bool toUra) {
            if (toUra) {
                int i = 0;
                foreach (Petal p in petals) {
                    if (p.State == PetalState.Frozen) {
                        p.State = PetalState.Burning;
                        p.BurnDelay = i * 3 + Main.rand.Next(14);
                        i++;
                    }
                }
                return;
            }
            BeginPetalFade();
            StaggerLanternExtinguish();
        }

        /// <summary>收域、花瓣淡出且灯逐盏熄灭</summary>
        public static void NotifyClosing() {
            BeginPetalFade();
            StaggerLanternExtinguish();
        }

        private static void BeginPetalFade() {
            foreach (Petal p in petals) {
                p.State = PetalState.Fading;
            }
        }

        private static void StaggerLanternExtinguish() {
            for (int i = 0; i < lanterns.Count; i++) {
                if (!lanterns[i].Dying) {
                    lanterns[i].Dying = true;
                    lanterns[i].ExtinguishDelay = i * 16 + Main.rand.Next(10);
                }
            }
        }

        public static void Update() {
            OniDomainPlayer odp = OniDomain.Local;
            if (odp == null) {
                if (petals.Count > 0 || lanterns.Count > 0 || ashes.Count > 0) {
                    Clear();
                }
                return;
            }

            UpdatePetals(odp);
            UpdateLanterns(odp);
            UpdateAshes(odp);
            UpdateWisps();
        }

        private static void UpdatePetals(OniDomainPlayer odp) {
            bool omoteLive = !odp.WorldIsUra
                && (odp.Phase == OniDomainPhase.Omote
                || (odp.Phase == OniDomainPhase.Opening && odp.SpreadProgress > 0.35f));
            bool uraLive = odp.WorldIsUra && odp.Phase == OniDomainPhase.Ura;

            if (omoteLive) {
                int target = (int)(OmotePetalCap * MathHelper.Clamp(odp.SpreadProgress, 0f, 1f));
                if (petals.Count < target && Main.rand.NextBool(2)) {
                    SpawnPetal(false);
                }
            }
            else if (uraLive) {
                if (petals.Count < UraPetalCap && Main.rand.NextBool(14)) {
                    SpawnPetal(true);
                }
            }

            float screenBottom = Main.screenPosition.Y + Main.screenHeight;
            float screenTop = Main.screenPosition.Y;

            for (int i = petals.Count - 1; i >= 0; i--) {
                Petal p = petals[i];
                switch (p.State) {
                    case PetalState.Falling:
                        p.SwayPhase += 0.030f + p.Scale * 0.008f;
                        if (p.UraKind) {
                            //阴间瓣、逆重力缓升

                            p.Vel = new Vector2(MathF.Sin(p.SwayPhase) * 0.45f, -0.35f - p.Scale * 0.2f);
                        }
                        else {
                            p.Vel = new Vector2(MathF.Sin(p.SwayPhase) * 0.85f + 0.25f, 0.85f + p.Scale * 0.55f);
                        }
                        p.Pos += p.Vel;
                        p.Rot += p.RotSpeed + MathF.Sin(p.SwayPhase * 0.7f) * 0.012f;

                        //异常一瞬、极低概率黑掉两帧

                        if (p.BlinkTimer > 0) {
                            p.BlinkTimer--;
                        }
                        else if (!p.UraKind && Main.rand.NextBool(2400)) {
                            p.BlinkTimer = 2;
                        }
                        break;

                    case PetalState.Frozen:
                        //完全静止

                        break;

                    case PetalState.Burning:
                        if (p.BurnDelay > 0) {
                            p.BurnDelay--;
                            break;
                        }
                        p.BurnT = MathF.Min(p.BurnT + 1f / 46f, 1f);
                        p.SwayPhase += 0.05f;
                        p.Vel = new Vector2(MathF.Sin(p.SwayPhase) * 1.1f, 1.7f);
                        p.Pos += p.Vel * (0.4f + p.BurnT);
                        p.Rot += p.RotSpeed * 2.2f;
                        p.Alpha = 1f - MathF.Max(p.BurnT - 0.72f, 0f) / 0.28f;

                        if (p.BurnT > 0.3f && Main.rand.NextBool(9) && ashes.Count < AshCap) {
                            SpawnAsh(p.Pos, p.Vel * 0.3f, Main.rand.NextFloat(2f, 4f));
                        }
                        if (p.BurnT >= 1f) {
                            petals.RemoveAt(i);
                            continue;
                        }
                        break;

                    case PetalState.Fading:
                        p.SwayPhase += 0.04f;
                        p.Vel *= 0.985f;
                        p.Vel.X += MathF.Sin(p.SwayPhase) * 0.025f;
                        p.Pos += p.Vel;
                        p.Rot += p.RotSpeed;
                        p.Alpha = MathF.Max(p.Alpha - 0.045f, 0f);
                        if (p.Alpha <= 0f) {
                            petals.RemoveAt(i);
                            continue;
                        }
                        break;
                }

                //离场回收，非稳态时旧瓣自然飘出屏幕

                bool offscreen = p.UraKind ? p.Pos.Y < screenTop - 80f : p.Pos.Y > screenBottom + 60f;
                if (offscreen) {
                    petals.RemoveAt(i);
                }
            }
        }

        private static void SpawnPetal(bool uraKind) {
            float x = Main.screenPosition.X + Main.rand.NextFloat(-160f, Main.screenWidth + 160f);
            float y = uraKind
                ? Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(20f, 70f)
                : Main.screenPosition.Y - Main.rand.NextFloat(30f, 90f);

            Color tint = uraKind
                ? PetalUraRed
                : Color.Lerp(PetalPink, PetalPinkDeep, Main.rand.NextFloat());

            petals.Add(new Petal {
                Pos = new Vector2(x, y),
                Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                Scale = Main.rand.NextFloat(0.55f, 1.05f),
                Tint = tint,
                State = PetalState.Falling,
                UraKind = uraKind
            });
        }

        private static void UpdateLanterns(OniDomainPlayer odp) {
            bool uraLive = odp.WorldIsUra
                && (odp.Phase == OniDomainPhase.Ura
                || (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.Peel)
                || (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.Settle));

            if (uraLive && lanterns.Count < LanternCap && Main.rand.NextBool(50)) {
                SpawnLantern();
            }

            float ura = odp.UraSmooth;
            for (int i = lanterns.Count - 1; i >= 0; i--) {
                Lantern l = lanterns[i];
                l.BobPhase += 0.017f;
                l.DriftPhase += 0.006f;
                l.Pos += new Vector2(MathF.Sin(l.DriftPhase) * 0.30f, -0.28f - l.Scale * 0.14f);
                l.Pos.Y += MathF.Sin(l.BobPhase) * 0.10f;

                if (l.Dying) {
                    if (l.ExtinguishDelay > 0) {
                        l.ExtinguishDelay--;
                    }
                    else {
                        l.Alpha -= 0.055f;
                    }
                }
                else {
                    l.Alpha = MathF.Min(l.Alpha + 0.012f, 1f);
                    if (!uraLive) {
                        l.Dying = true;
                        l.ExtinguishDelay = i * 12;
                    }
                }

                if (l.Alpha <= 0f) {
                    lanterns.RemoveAt(i);
                    continue;
                }

                //红灯点光

                float flicker = 0.82f + 0.18f * MathF.Sin(l.BobPhase * 4.7f + l.DriftPhase * 31f);
                float glow = l.Alpha * flicker * MathF.Max(ura, 0.25f);
                Lighting.AddLight(l.Pos, 0.92f * glow, 0.30f * glow, 0.09f * glow);

                //飘出视野回收

                Vector2 cam = Main.screenPosition;
                if (l.Pos.Y < cam.Y - 220f || MathF.Abs(l.Pos.X - (cam.X + Main.screenWidth * 0.5f)) > Main.screenWidth) {
                    lanterns.RemoveAt(i);
                }
            }
        }

        private static void SpawnLantern() {
            float x = Main.screenPosition.X + Main.rand.NextFloat(-60f, Main.screenWidth + 60f);
            float y = Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(30f, 160f);
            lanterns.Add(new Lantern {
                Pos = new Vector2(x, y),
                Scale = Main.rand.NextFloat(0.75f, 1.30f),
                BobPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                DriftPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                Alpha = 0f
            });
        }

        internal static void SpawnPeelAshLine(float slashAngle, int count) {
            Vector2 camCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 dir = slashAngle.ToRotationVector2();
            float diag = new Vector2(Main.screenWidth, Main.screenHeight).Length();
            for (int i = 0; i < count && ashes.Count < AshCap; i++) {
                float t = Main.rand.NextFloat(-0.55f, 0.55f) * diag;
                Vector2 pos = camCenter + dir * t;
                Vector2 vel = new(Main.rand.NextFloat(-0.9f, 0.9f), Main.rand.NextFloat(0.3f, 1.4f));
                SpawnAsh(pos, vel, Main.rand.NextFloat(2f, 5f));
            }
        }

        private static void SpawnAsh(Vector2 pos, Vector2 vel, float size) {
            ashes.Add(new Ash {
                Pos = pos,
                Vel = vel,
                Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.09f, 0.09f),
                Size = size,
                MaxLife = Main.rand.Next(50, 110),
                Life = 0
            });
        }

        private static void UpdateAshes(OniDomainPlayer odp) {
            //剥落期间沿刀痕持续冒灰

            if (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.Peel
                && odp.PeelProgress < 0.85f) {
                SpawnPeelAshLine(odp.FlipSlashAngle, 3);
            }

            for (int i = ashes.Count - 1; i >= 0; i--) {
                Ash a = ashes[i];
                a.Life++;
                a.Vel.X *= 0.985f;
                a.Vel.Y = MathF.Min(a.Vel.Y + 0.012f, 1.8f);
                a.Vel.X += MathF.Sin((a.Life + a.Rot * 37f) * 0.11f) * 0.02f;
                a.Pos += a.Vel;
                a.Rot += a.RotSpeed;
                if (a.Life >= a.MaxLife) {
                    ashes.RemoveAt(i);
                }
            }
        }

        public static void Draw(SpriteBatch spriteBatch) {
            if (petals.Count == 0 && lanterns.Count == 0 && ashes.Count == 0 && wisps.Count == 0) {
                return;
            }

            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Effect deco = EffectLoader.OniDomainDeco?.Value;
            float time = (float)Main.timeForVisualEffects * 0.05f;

            //SDF 件、花瓣 + 灯笼，Immediate 单批内切换 technique

            if (deco != null && (petals.Count > 0 || lanterns.Count > 0)) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                deco.Parameters["uTime"]?.SetValue(time);

                if (petals.Count > 0) {
                    deco.CurrentTechnique = deco.Techniques["TechPetal"];
                    deco.CurrentTechnique.Passes[0].Apply();
                    Vector2 origin = white.Size() * 0.5f;
                    foreach (Petal p in petals) {
                        float burnDim = p.State == PetalState.Burning && p.BurnDelay <= 0 ? p.BurnT : 0f;
                        Color c = p.Tint;
                        //点燃、粉→焰红→焦黑

                        if (burnDim > 0f) {
                            c = burnDim < 0.5f
                                ? Color.Lerp(p.Tint, new Color(235, 62, 30), burnDim * 2f)
                                : Color.Lerp(new Color(235, 62, 30), new Color(26, 18, 20), burnDim * 2f - 1f);
                        }
                        else if (p.BlinkTimer > 0) {
                            c = new Color(12, 10, 14);
                        }
                        c *= p.Alpha;

                        //32px 基准 quad

                        float pxSize = 30f * p.Scale;
                        Vector2 scale = new(pxSize / white.Width, pxSize * 1.15f / white.Height);
                        //冻结瓣拉直不再摇摆

                        float rot = p.Rot;
                        spriteBatch.Draw(white, p.Pos - Main.screenPosition, null, c,
                            rot, origin, scale, SpriteEffects.None, 0f);
                    }
                }

                if (lanterns.Count > 0) {
                    deco.CurrentTechnique = deco.Techniques["TechLantern"];
                    deco.CurrentTechnique.Passes[0].Apply();
                    Vector2 origin = white.Size() * 0.5f;
                    foreach (Lantern l in lanterns) {
                        float sway = MathF.Sin(l.DriftPhase * 3.1f) * 0.06f;
                        Color c = LanternPaper * l.Alpha;
                        float w = 46f * l.Scale;
                        float h = 62f * l.Scale;
                        spriteBatch.Draw(white, l.Pos - Main.screenPosition, null, c,
                            sway, origin, new Vector2(w / white.Width, h / white.Height),
                            SpriteEffects.None, 0f);
                    }
                }

                spriteBatch.End();
            }

            //灯笼光晕 + 燃瓣余烬 + 灵体，Additive

            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null && (lanterns.Count > 0 || petals.Count > 0 || wisps.Count > 0)) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 gOrigin = glowTex.Size() * 0.5f;
                foreach (Lantern l in lanterns) {
                    float flicker = 0.80f + 0.20f * MathF.Sin(l.BobPhase * 4.7f + l.DriftPhase * 31f);
                    Color halo = new Color(1f, 0.34f, 0.10f, 0f) * (0.42f * l.Alpha * flicker);
                    float haloScale = 150f * l.Scale / glowTex.Width;
                    spriteBatch.Draw(glowTex, l.Pos - Main.screenPosition, null, halo,
                        0f, gOrigin, haloScale, SpriteEffects.None, 0f);
                }

                foreach (Petal p in petals) {
                    if (p.State != PetalState.Burning || p.BurnDelay > 0 || p.BurnT > 0.8f) {
                        continue;
                    }
                    float emberA = MathF.Sin(p.BurnT * MathHelper.Pi) * 0.55f * p.Alpha;
                    Color ember = new Color(1f, 0.28f, 0.06f, 0f) * emberA;
                    float s = 34f * p.Scale / glowTex.Width;
                    spriteBatch.Draw(glowTex, p.Pos - Main.screenPosition, null, ember,
                        0f, gOrigin, s, SpriteEffects.None, 0f);
                }

                //灵体、红色小光点拖尾

                foreach (Wisp w in wisps) {
                    float lifeF = w.Life / (float)w.MaxLife;
                    float a = MathF.Sin(MathHelper.Clamp(lifeF, 0f, 1f) * MathHelper.Pi) * 0.65f;
                    Color c = new Color(1f, 0.24f, 0.08f, 0f) * a;
                    float s = w.Size * 11f / glowTex.Width;
                    spriteBatch.Draw(glowTex, w.Pos - Main.screenPosition, null, c,
                        0f, gOrigin, s, SpriteEffects.None, 0f);
                    //速度方向小拖尾

                    spriteBatch.Draw(glowTex, w.Pos - w.Vel * 1.6f - Main.screenPosition, null, c * 0.45f,
                        0f, gOrigin, s * 0.7f, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }

            //纸灰、素色小片，无着色器

            if (ashes.Count > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 origin = white.Size() * 0.5f;
                foreach (Ash a in ashes) {
                    float lifeF = a.Life / (float)a.MaxLife;
                    //初生带余烬红，随后转焦黑

                    Color c = lifeF < 0.25f
                        ? Color.Lerp(new Color(210, 62, 26), new Color(28, 22, 24), lifeF * 4f)
                        : new Color(28, 22, 24);
                    c *= 1f - MathF.Max(lifeF - 0.7f, 0f) / 0.3f;
                    Vector2 scale = new(a.Size / white.Width, a.Size * 0.6f / white.Height);
                    spriteBatch.Draw(white, a.Pos - Main.screenPosition, null, c,
                        a.Rot, origin, scale, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }
        }
    }
}
