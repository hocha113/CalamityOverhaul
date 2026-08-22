using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 通用兜底夺身演出：封印墨收束。<br/>
    /// 材质为「墨」，无彩暗体收拢 + 札红芯闪，不借用任何一只鬼的专属语汇；
    /// 仅在该鬼未提供专属演出时使用。
    /// </summary>
    internal sealed class GenericSeizurePerformance : WraithDeathPerformance
    {
        private struct InkMote
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public int Life;
            public int MaxLife;
            public bool Rising;
        }

        private static readonly Color InkBody = new(20, 16, 28);
        private static readonly Color SealRed = new(158, 44, 54);

        private readonly List<InkMote> motes = [];
        private bool manifestCuePlayed;

        public override void OnBegin() {
            SoundEngine.PlaySound(SoundID.Zombie103 with {
                Volume = 0.8f,
                Pitch = -0.8f,
                MaxInstances = 1,
            }, Player.Center);
        }

        public override void Update() {
            switch (Phase) {
                case WraithSeizePhase.Omen:
                    if (Timer % 3 == 0) {
                        SpawnConverging(2);
                    }
                    break;
                case WraithSeizePhase.Manifest:
                    if (!manifestCuePlayed) {
                        manifestCuePlayed = true;
                        SoundEngine.PlaySound(SoundID.Roar with {
                            Volume = 0.9f,
                            Pitch = -0.55f,
                            MaxInstances = 1,
                        }, Player.Center);
                    }
                    SpawnConverging(3);
                    break;
                case WraithSeizePhase.Linger:
                    if (Timer % 5 == 0) {
                        SpawnRising();
                    }
                    break;
            }

            for (int i = motes.Count - 1; i >= 0; i--) {
                InkMote mote = motes[i];
                mote.Life++;
                if (!mote.Rising && !Player.dead) {
                    //收束：不断修正朝向玩家胸口
                    Vector2 pull = (Player.Center - mote.Position).SafeNormalize(Vector2.Zero);
                    mote.Velocity = Vector2.Lerp(mote.Velocity, pull * 7f, 0.08f);
                }
                mote.Position += mote.Velocity;
                mote.Velocity *= mote.Rising ? 0.985f : 0.97f;
                if (mote.Life >= mote.MaxLife
                    || !mote.Rising && Vector2.DistanceSquared(mote.Position, Player.Center) < 18f * 18f) {
                    motes.RemoveAt(i);
                    continue;
                }
                motes[i] = mote;
            }
        }

        public override void OnExecute() {
            SoundEngine.PlaySound(SoundID.NPCDeath59 with {
                Volume = 1.1f,
                Pitch = -0.6f,
                MaxInstances = 1,
            }, Player.Center);
            for (int i = 0; i < 46; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(9f, 9f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.Shadowflame,
                    velocity, 100, Color.Black, Main.rand.NextFloat(1.4f, 2.2f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 28; i++) {
                Dust.NewDustPerfect(Player.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(6f, 6f), 60, default, Main.rand.NextFloat(1.2f, 1.9f));
            }
        }

        public override void Draw(SpriteBatch sb) {
            Texture2D glow = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;

            //收束环：前兆到显形不断收紧的暗环
            if (Phase is WraithSeizePhase.Omen or WraithSeizePhase.Manifest) {
                float ringRadius = Phase == WraithSeizePhase.Omen
                    ? MathHelper.Lerp(200f, 90f, PhaseProgress)
                    : MathHelper.Lerp(90f, 26f, PhaseProgress);
                float ringAlpha = Phase == WraithSeizePhase.Omen ? 0.35f * PhaseProgress : 0.55f;
                const int Segments = 26;
                for (int i = 0; i < Segments; i++) {
                    float angle = MathHelper.TwoPi * i / Segments + Timer * 0.014f + Seed * 0.02f;
                    float wobble = 1f + 0.08f * MathF.Sin(Timer * 0.11f + i * 1.7f);
                    Vector2 pos = anchor + angle.ToRotationVector2() * ringRadius * wobble
                        - Main.screenPosition;
                    sb.Draw(glow, pos, null, InkBody * ringAlpha, angle, glowOrigin, 0.09f,
                        SpriteEffects.None, 0f);
                }
            }

            //札红芯：显形期在玩家胸口越压越亮
            if (Phase == WraithSeizePhase.Manifest) {
                float flick = 0.7f + 0.3f * MathF.Sin(Timer * 0.55f);
                float core = MathHelper.Lerp(0.18f, 0.55f, PhaseProgress) * flick;
                Vector2 heart = anchor - Main.screenPosition;
                sb.Draw(glow, heart, null, InkBody * 0.85f, 0f, glowOrigin, 0.42f, SpriteEffects.None, 0f);
                sb.Draw(glow, heart, null, SealRed * core, 0f, glowOrigin, 0.2f, SpriteEffects.None, 0f);
            }

            //余韵：地上的墨渍缓灭
            if (Phase == WraithSeizePhase.Linger) {
                float fade = 1f - PhaseProgress;
                Vector2 stain = anchor - Main.screenPosition;
                sb.Draw(glow, stain, null, InkBody * (0.7f * fade), 0f, glowOrigin,
                    new Vector2(0.6f, 0.28f), SpriteEffects.None, 0f);
                sb.Draw(glow, stain, null, SealRed * (0.22f * fade), 0f, glowOrigin,
                    new Vector2(0.24f, 0.12f), SpriteEffects.None, 0f);
            }

            foreach (InkMote mote in motes) {
                float lifeRatio = mote.Life / (float)mote.MaxLife;
                float alpha = mote.Rising ? (1f - lifeRatio) * 0.5f : 0.75f * (1f - lifeRatio * 0.35f);
                sb.Draw(glow, mote.Position - Main.screenPosition, null, InkBody * alpha,
                    mote.Life * 0.06f, glowOrigin, mote.Scale, SpriteEffects.None, 0f);
            }
        }

        private void SpawnConverging(int count) {
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(150f, 240f);
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                motes.Add(new InkMote {
                    Position = pos,
                    Velocity = (Player.Center - pos).SafeNormalize(Vector2.Zero)
                        * Main.rand.NextFloat(2f, 5f),
                    Scale = Main.rand.NextFloat(0.06f, 0.14f),
                    MaxLife = Main.rand.Next(50, 90),
                });
            }
        }

        private void SpawnRising() {
            motes.Add(new InkMote {
                Position = DeathAnchor + new Vector2(Main.rand.NextFloat(-26f, 26f),
                    Main.rand.NextFloat(-6f, 8f)),
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                    Main.rand.NextFloat(-1.4f, -0.6f)),
                Scale = Main.rand.NextFloat(0.05f, 0.1f),
                MaxLife = Main.rand.Next(40, 80),
                Rising = true,
            });
        }
    }
}
