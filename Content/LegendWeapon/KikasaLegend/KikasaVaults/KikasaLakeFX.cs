using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖藏演出层：沉入与浮出的物品幽灵。数据在 <see cref="KikasaVaultPlayer"/> 早已入账，
    /// 这里只演"它沉下去了/它浮上来了"。幽灵画在 EndEntityDraw 时机，
    /// 水上倒影与水下血染由 KikasaGrade 的湖面着色器自动接管。
    /// </summary>
    internal static class KikasaLakeFX
    {
        //==================== 沉入幽灵 ====================

        private enum SinkState : byte
        {
            /// <summary>自手中浮出升至头顶悬点，旋至竖立</summary>
            Appear,
            /// <summary>悬滞呼吸，末拍微微上提蓄势</summary>
            Hover,
            /// <summary>重力加速坠落</summary>
            Fall,
            /// <summary>水阻减速下沉，噪声侵蚀溶解</summary>
            Submerge,
            /// <summary>余韵：残斑沉灭</summary>
            After
        }

        private class SinkGhost
        {
            public int OwnerIndex;
            public int ItemType;
            public float Seed;
            public SinkState State;
            public int Timer;
            public Vector2 Pos;
            public Vector2 SpawnPos;
            public float AnchorX;
            public float LakeY;
            public float ApexY;
            public float Vy;
            public float Rot;
            public float StandRot;
            public float Alpha;
            public float Dissolve;
            /// <summary>谢幕起点透明度，中断谢幕从当前值淡出不跳变</summary>
            public float FadeFrom = 1f;
            /// <summary>出手时已在水下：跳过坠落与水花，原地闷沉</summary>
            public bool SubmergedSpawn;
            /// <summary>起演延迟帧：快捷散沉的错帧起跳</summary>
            public int Delay;
            public bool Done;
        }

        //==================== 浮出幽灵 ====================

        private enum RaiseState : byte
        {
            /// <summary>湖面聚涟漪、水下血光上浮的预兆</summary>
            Omen,
            /// <summary>破水后血水态升至悬点</summary>
            RiseUp,
            /// <summary>血水自上而下凝实为真身</summary>
            Condense,
            /// <summary>交付后淡出</summary>
            Fade
        }

        private class RaiseGhost
        {
            public int OwnerIndex;
            public int ItemType;
            /// <summary>仅所有者本机非空；凝实完成拍交付后置空</summary>
            public Item Payload;
            public float Seed;
            public RaiseState State;
            public int Timer;
            public Vector2 Pos;
            public float AnchorX;
            public float LakeY;
            public float ApexY;
            public float Rot;
            public float Alpha;
            public float Form;
            public float Scale = 1f;
            /// <summary>谢幕起点透明度</summary>
            public float FadeFrom = 1f;
            public bool Done;
        }

        //==================== 时序 ====================

        private const int SinkAppearFrames = 14;
        private const int SinkHoverFrames = 16;
        private const int SinkSubmergeFrames = 44;
        private const int SinkAfterFrames = 20;

        private const int RaiseOmenFrames = 24;
        private const int RaiseUpFrames = 28;
        private const int RaiseCondenseFrames = 48;
        private const int RaiseFadeFrames = 12;

        private const int SinkCap = 8;
        private const int RaiseCap = 10;

        private static readonly List<SinkGhost> sinks = [];
        private static readonly List<RaiseGhost> raises = [];

        //血系配色，与 KikasaGrade/Deco 同族；鬼雨异化时随观看域冷化为浊水灰青
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(43, 6, 9), new(12, 20, 25));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        public static void Clear() {
            //不在这里交付：世界退出路径上 SaveData 会把在途物折返湖藏
            sinks.Clear();
            raises.Clear();
        }

        //==================== 生成 ====================

        /// <summary>沉入演出。数据已入账，这里起一具幽灵；本机所有者入口</summary>
        public static void SpawnSink(Player owner, Item stored) {
            if (Main.dedServ || owner == null || stored == null) {
                return;
            }
            SpawnSinkCore(owner, stored.type);
        }

        internal static void SpawnSinkCore(Player owner, int itemType) {
            if (Main.dedServ || sinks.Count >= SinkCap) {
                return;
            }
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = domain.LakeWorldY;
            Vector2 hand = owner.MountedCenter + new Vector2(owner.direction * 12f, -6f);
            float anchorX = owner.Center.X + owner.direction * 34f;
            bool underwater = hand.Y > lakeY + 8f;

            SinkGhost g = new() {
                OwnerIndex = owner.whoAmI,
                ItemType = itemType,
                Seed = Main.rand.NextFloat(10f),
                State = SinkState.Appear,
                Pos = hand,
                SpawnPos = hand,
                AnchorX = anchorX,
                LakeY = lakeY,
                //水下出手只轻抬一点，水上正常升至湖面上方悬点
                ApexY = underwater
                    ? hand.Y - 26f
                    : MathF.Min(hand.Y - 62f, lakeY - 88f),
                StandRot = ComputeStandRot(itemType),
                SubmergedSpawn = underwater,
            };
            sinks.Add(g);

            if (IsViewedOwner(g.OwnerIndex)) {
                //起手：一声轻水滴，东西被举到湖面上方
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 2 }, hand);
            }
        }

        /// <summary>
        /// 散布沉入（快捷沉湖用）：起点/落点锚/悬点/起演延迟全由调用方指定，
        /// 一批幽灵自玩家上方扇形铺开、错帧坠湖。数据须已由调用方入账
        /// </summary>
        internal static void SpawnSinkScattered(Player owner, int itemType,
            Vector2 from, float anchorX, float apexY, int delay) {
            if (Main.dedServ || owner == null || sinks.Count >= SinkCap) {
                return;
            }
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = domain.LakeWorldY;
            sinks.Add(new SinkGhost {
                OwnerIndex = owner.whoAmI,
                ItemType = itemType,
                Seed = Main.rand.NextFloat(10f),
                State = SinkState.Appear,
                Pos = from,
                SpawnPos = from,
                AnchorX = anchorX,
                LakeY = lakeY,
                ApexY = apexY,
                StandRot = ComputeStandRot(itemType),
                SubmergedSpawn = from.Y > lakeY + 8f,
                Delay = delay,
            });
        }

        /// <summary>浮出演出。payload 是在途物品实体，凝实完成拍交付背包</summary>
        public static void SpawnRaise(Player owner, Item payload) {
            if (owner == null || payload == null) {
                return;
            }
            if (Main.dedServ) {
                //纯服务器不演出，直接交付兜底
                owner.GetModPlayer<KikasaVaultPlayer>().DeliverExtract(payload);
                return;
            }
            if (raises.Count >= RaiseCap) {
                //演出满编就即刻交付，提取正确性不欠账
                owner.GetModPlayer<KikasaVaultPlayer>().DeliverExtract(payload);
                return;
            }
            SpawnRaiseCore(owner, payload.type, payload);
        }

        internal static void SpawnRaiseCore(Player owner, int itemType, Item payload) {
            if (Main.dedServ || raises.Count >= RaiseCap) {
                return;
            }
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            float lakeY = domain.LakeWorldY;
            float anchorX = owner.Center.X + owner.direction * 56f;

            RaiseGhost g = new() {
                OwnerIndex = owner.whoAmI,
                ItemType = itemType,
                Payload = payload,
                Seed = Main.rand.NextFloat(10f),
                State = RaiseState.Omen,
                AnchorX = anchorX,
                LakeY = lakeY,
                ApexY = lakeY - 96f,
                Pos = new Vector2(anchorX, lakeY + 6f),
                Rot = ComputeStandRot(itemType),
                Form = 1f,
            };
            raises.Add(g);
        }

        //==================== 推进 ====================

        /// <summary>由 KikasaDomainSystem.PostUpdateEverything 逐帧驱动</summary>
        public static void Update() {
            for (int i = sinks.Count - 1; i >= 0; i--) {
                SinkGhost g = sinks[i];
                UpdateSink(g);
                if (g.Done) {
                    sinks.RemoveAt(i);
                }
            }
            for (int i = raises.Count - 1; i >= 0; i--) {
                RaiseGhost g = raises[i];
                UpdateRaise(g);
                if (g.Done) {
                    raises.RemoveAt(i);
                }
            }
        }

        /// <summary>所有者的湖是否还接得住这场演出</summary>
        private static bool LakeAlive(int ownerIndex, out KikasaDomainPlayer domain) {
            domain = null;
            if (ownerIndex < 0 || ownerIndex >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[ownerIndex];
            if (owner?.active != true || !owner.TryGetModPlayer(out domain)) {
                return false;
            }
            return domain.AnyActive
                && domain.Phase != KikasaDomainPhase.Closing
                && domain.RiseT >= 0.9f;
        }

        private static void UpdateSink(SinkGhost g) {
            if (!LakeAlive(g.OwnerIndex, out _)) {
                //湖没了：数据早已入账，幽灵直接谢幕
                if (g.State < SinkState.After) {
                    g.State = SinkState.After;
                    g.Timer = 0;
                    g.FadeFrom = g.Alpha;
                }
            }

            //起演延迟（快捷散沉的错帧）：湖死谢幕照常，未起演时按兵不动
            if (g.Delay > 0 && g.State == SinkState.Appear) {
                g.Delay--;
                return;
            }

            bool visible = IsViewedOwner(g.OwnerIndex);
            g.Timer++;

            switch (g.State) {
                case SinkState.Appear: {
                    float t = MathHelper.Clamp(g.Timer / (float)SinkAppearFrames, 0f, 1f);
                    float ease = 1f - MathF.Pow(1f - t, 3f);
                    g.Pos.X = MathHelper.Lerp(g.SpawnPos.X, g.AnchorX, ease);
                    g.Pos.Y = MathHelper.Lerp(g.SpawnPos.Y, g.ApexY, ease);
                    g.Rot = g.StandRot * SmoothStep01(t);
                    g.Alpha = t;
                    if (g.Timer >= SinkAppearFrames) {
                        g.State = SinkState.Hover;
                        g.Timer = 0;
                    }
                    break;
                }
                case SinkState.Hover: {
                    g.Alpha = 1f;
                    //呼吸浮动；末三帧上提蓄势
                    g.Pos.Y = g.ApexY + MathF.Sin(g.Timer * 0.35f + g.Seed) * 1.6f;
                    int lift = g.Timer - (SinkHoverFrames - 3);
                    if (lift > 0) {
                        g.Pos.Y -= lift * 1.5f;
                    }
                    if (g.Timer == 2 && visible && !g.SubmergedSpawn) {
                        //湖面先荡开一圈小涟漪，湖在等它
                        KikasaDomainDeco.RippleAt(new Vector2(g.AnchorX, g.LakeY), 0.5f);
                    }
                    if (g.Timer >= SinkHoverFrames) {
                        g.State = g.SubmergedSpawn ? SinkState.Submerge : SinkState.Fall;
                        g.Timer = 0;
                        g.Vy = g.SubmergedSpawn ? 1.6f : 0f;
                        if (g.SubmergedSpawn && visible) {
                            //已在水下：水面上只闷出一圈涟漪
                            KikasaDomainDeco.RippleAt(new Vector2(g.AnchorX, g.LakeY), 0.8f);
                            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, g.Pos);
                        }
                    }
                    break;
                }
                case SinkState.Fall: {
                    //重力加速，禁匀速
                    g.Vy = MathF.Min(g.Vy + 1.05f, 17f);
                    g.Pos.Y += g.Vy;
                    g.Rot = g.StandRot + MathF.Sin(g.Timer * 0.5f + g.Seed) * 0.03f;
                    if (g.Pos.Y >= g.LakeY - 2f) {
                        //触水拍
                        g.Pos.Y = g.LakeY - 2f;
                        g.State = SinkState.Submerge;
                        g.Timer = 0;
                        if (visible) {
                            Vector2 hit = new(g.Pos.X, g.LakeY);
                            KikasaDomainDeco.SplashAt(hit, 13);
                            KikasaDomainDeco.RippleAt(hit, 1.5f);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, hit);
                            ShakeViewer(2.5f);
                        }
                    }
                    break;
                }
                case SinkState.Submerge: {
                    //水阻指数减速，沉势拖长；水流轻推，沉不出一条死直线
                    g.Vy = MathF.Max(g.Vy * 0.90f, 0.35f);
                    g.Pos.Y += g.Vy;
                    g.Pos.X += MathF.Sin(g.Timer * 0.11f + g.Seed) * 0.18f;
                    float t = MathHelper.Clamp(g.Timer / (float)SinkSubmergeFrames, 0f, 1f);
                    //先整件沉进浊水（血染由湖面着色器接管），过 1/4 才开始被蚀
                    g.Dissolve = MathF.Pow(MathHelper.Clamp((t - 0.24f) / 0.76f, 0f, 1f), 0.9f);
                    if (visible) {
                        if (g.Timer == 10) {
                            KikasaDomainDeco.RippleAt(new Vector2(g.Pos.X, g.LakeY), 0.6f);
                        }
                        if (g.Timer == 24) {
                            KikasaDomainDeco.RippleAt(new Vector2(g.Pos.X, g.LakeY), 0.4f);
                            //一小口血雾泡自沉点冒起
                            PRTLoader.NewParticle<PRT_GhostRainMist>(
                                g.Pos + new Vector2(Main.rand.NextFloat(-6f, 6f), -4f),
                                new Vector2(0f, -0.35f), MistBlood * 0.7f,
                                Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(50, 80));
                        }
                        if (g.Timer == 30) {
                            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 2 }, g.Pos);
                        }
                    }
                    if (g.Timer >= SinkSubmergeFrames) {
                        g.State = SinkState.After;
                        g.Timer = 0;
                    }
                    break;
                }
                case SinkState.After: {
                    //残斑沉灭
                    g.Pos.Y += 0.3f;
                    g.Dissolve = 1f;
                    g.Alpha = g.FadeFrom * MathHelper.Clamp(1f - g.Timer / (float)SinkAfterFrames, 0f, 1f);
                    if (g.Timer >= SinkAfterFrames) {
                        g.Done = true;
                    }
                    break;
                }
            }
        }

        private static void UpdateRaise(RaiseGhost g) {
            if (!LakeAlive(g.OwnerIndex, out _) && g.State < RaiseState.Fade) {
                //湖中途收了：立即交付，演出快进谢幕
                DeliverNow(g);
                g.State = RaiseState.Fade;
                g.Timer = 0;
                g.FadeFrom = g.Alpha;
            }

            bool visible = IsViewedOwner(g.OwnerIndex);
            g.Timer++;

            switch (g.State) {
                case RaiseState.Omen: {
                    g.Alpha = 0f;
                    if (visible) {
                        if (g.Timer % 8 == 2) {
                            KikasaDomainDeco.RippleAt(
                                new Vector2(g.AnchorX + Main.rand.NextFloat(-8f, 8f), g.LakeY),
                                0.45f + g.Timer / (float)RaiseOmenFrames * 0.3f);
                        }
                        if (g.Timer == 4 || g.Timer == 16) {
                            SoundEngine.PlaySound(SoundID.Drip with {
                                Volume = 0.4f,
                                Pitch = g.Timer == 4 ? -0.3f : -0.1f,
                                MaxInstances = 2
                            }, new Vector2(g.AnchorX, g.LakeY));
                        }
                    }
                    if (g.Timer >= RaiseOmenFrames) {
                        //破水拍
                        g.State = RaiseState.RiseUp;
                        g.Timer = 0;
                        g.Pos = new Vector2(g.AnchorX, g.LakeY + 6f);
                        g.Alpha = 1f;
                        if (visible) {
                            Vector2 hit = new(g.AnchorX, g.LakeY);
                            KikasaDomainDeco.SplashAt(hit, 14);
                            KikasaDomainDeco.RippleAt(hit, 1.6f);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.05f, MaxInstances = 2 }, hit);
                            ShakeViewer(2f);
                        }
                    }
                    break;
                }
                case RaiseState.RiseUp: {
                    float t = MathHelper.Clamp(g.Timer / (float)RaiseUpFrames, 0f, 1f);
                    float ease = 1f - MathF.Pow(1f - t, 3f);
                    g.Pos.Y = MathHelper.Lerp(g.LakeY + 6f, g.ApexY, ease);
                    g.Form = 1f;
                    if (visible) {
                        //沿途血珠回落，落点起微涟漪
                        if (g.Timer % 3 == 0) {
                            Vector2 dropPos = g.Pos + new Vector2(
                                Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-16f, 16f));
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(2.2f, 3.4f)),
                                BloodTint * Main.rand.NextFloat(0.35f, 0.55f),
                                Main.rand.NextFloat(0.4f, 0.65f))
                                ?.Configure(Main.rand.Next(14, 24), 0f);
                        }
                        if (g.Timer % 6 == 3) {
                            KikasaDomainDeco.RippleAt(
                                new Vector2(g.AnchorX + Main.rand.NextFloat(-16f, 16f), g.LakeY), 0.3f);
                        }
                    }
                    if (g.Timer >= RaiseUpFrames) {
                        g.State = RaiseState.Condense;
                        g.Timer = 0;
                    }
                    break;
                }
                case RaiseState.Condense: {
                    float t = MathHelper.Clamp(g.Timer / (float)RaiseCondenseFrames, 0f, 1f);
                    g.Pos.Y = g.ApexY + MathF.Sin(g.Timer * 0.16f + g.Seed) * 1.2f;
                    g.Form = 1f - SmoothStep01(t);
                    //末拍轻闪与定格微弹
                    if (g.Timer >= RaiseCondenseFrames - 8) {
                        float pop = (RaiseCondenseFrames - g.Timer) / 8f;
                        g.Scale = 1f + 0.06f * MathHelper.Clamp(pop, 0f, 1f);
                    }
                    if (visible) {
                        //表面血水往下淌
                        if (g.Timer % 8 == 5 && t < 0.85f) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                g.Pos + new Vector2(Main.rand.NextFloat(-8f, 8f), 12f),
                                new Vector2(0f, Main.rand.NextFloat(1.8f, 2.6f)),
                                BloodTint * 0.45f, Main.rand.NextFloat(0.35f, 0.55f))
                                ?.Configure(Main.rand.Next(16, 26), 0f);
                        }
                        if (g.Timer == RaiseCondenseFrames - 8) {
                            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = 0.2f, MaxInstances = 2 }, g.Pos);
                        }
                    }
                    if (g.Timer >= RaiseCondenseFrames) {
                        //交付拍：物品真正落进背包
                        DeliverNow(g);
                        g.State = RaiseState.Fade;
                        g.Timer = 0;
                        g.FadeFrom = 1f;
                        if (visible) {
                            KikasaDomainDeco.RippleAt(new Vector2(g.AnchorX, g.LakeY), 0.4f);
                        }
                    }
                    break;
                }
                case RaiseState.Fade: {
                    g.Form = 0f;
                    g.Scale = 1f;
                    g.Alpha = g.FadeFrom * MathHelper.Clamp(1f - g.Timer / (float)RaiseFadeFrames, 0f, 1f);
                    if (g.Timer >= RaiseFadeFrames) {
                        g.Done = true;
                    }
                    break;
                }
            }
        }

        private static void DeliverNow(RaiseGhost g) {
            if (g.Payload == null) {
                return;
            }
            Player owner = Main.player[g.OwnerIndex];
            if (owner?.active == true && g.OwnerIndex == Main.myPlayer) {
                owner.GetModPlayer<KikasaVaultPlayer>().DeliverExtract(g.Payload);
            }
            g.Payload = null;
        }

        //==================== 绘制 ====================

        /// <summary>由 KikasaDomainRender.EndEntityDraw 调用，湖面着色器随后接管水下观感</summary>
        public static void Draw(SpriteBatch spriteBatch) {
            if (sinks.Count == 0 && raises.Count == 0) {
                return;
            }
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            if (viewed == null) {
                return;
            }
            int viewedOwner = viewed.Player.whoAmI;

            DrawGlowLayer(spriteBatch, viewedOwner);
            DrawGhostLayer(spriteBatch, viewedOwner);
        }

        //加色层：浮出预兆的水下血光、凝实末拍闪光、沉没残斑

        private static void DrawGlowLayer(SpriteBatch spriteBatch, int viewedOwner) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }

            bool begun = false;
            Vector2 origin = glow.Size() * 0.5f;

            foreach (RaiseGhost g in raises) {
                if (g.OwnerIndex != viewedOwner) {
                    continue;
                }
                float a = 0f;
                Vector2 pos = default;
                Vector2 scale = default;
                if (g.State == RaiseState.Omen) {
                    //血光自深处上浮，宽扁的水下光斑
                    float t = MathHelper.Clamp(g.Timer / (float)RaiseOmenFrames, 0f, 1f);
                    float ease = 1f - (1f - t) * (1f - t);
                    pos = new Vector2(g.AnchorX, g.LakeY + MathHelper.Lerp(46f, 8f, ease));
                    a = 0.38f * ease;
                    float r = 24f + 14f * ease;
                    scale = new Vector2(r * 2.4f / glow.Width, r * 1.1f / glow.Height);
                }
                else if (g.State == RaiseState.Condense && g.Timer >= RaiseCondenseFrames - 8) {
                    //凝实末拍轻闪
                    float f = (g.Timer - (RaiseCondenseFrames - 8)) / 8f;
                    pos = g.Pos;
                    a = 0.5f * (1f - f);
                    float r = 30f + 26f * f;
                    scale = new Vector2(r * 2f / glow.Width, r * 2f / glow.Height);
                }
                if (a <= 0.01f) {
                    continue;
                }
                if (!begun) {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
                //真加色批源因子是 SourceAlpha，A 必须随强度走，置零=什么都不画
                spriteBatch.Draw(glow, pos - Main.screenPosition, null,
                    FoamGlow * a, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            foreach (SinkGhost g in sinks) {
                if (g.OwnerIndex != viewedOwner) {
                    continue;
                }
                float a = 0f;
                Vector2 pos = default;
                Vector2 scale = default;
                if (g.State == SinkState.After) {
                    //沉灭残斑
                    a = 0.26f * g.Alpha;
                    pos = g.Pos;
                    scale = new Vector2(22f / glow.Width, 16f / glow.Height);
                }
                else if (g.State == SinkState.Hover && !g.SubmergedSpawn) {
                    //悬滞期落点水面渐亮，湖在等它
                    float t = g.Timer / (float)SinkHoverFrames;
                    a = 0.22f * t;
                    pos = new Vector2(g.AnchorX, g.LakeY + 4f);
                    float r = 16f + 10f * t;
                    scale = new Vector2(r * 2.2f / glow.Width, r * 0.9f / glow.Height);
                }
                if (a <= 0.01f) {
                    continue;
                }
                if (!begun) {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
                spriteBatch.Draw(glow, pos - Main.screenPosition, null,
                    FoamGlow * a, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            if (begun) {
                spriteBatch.End();
            }
        }

        //幽灵本体：KikasaItemForm 着色器；缺编时 CPU 染色回退

        private static void DrawGhostLayer(SpriteBatch spriteBatch, int viewedOwner) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }

            foreach (SinkGhost g in sinks) {
                if (g.OwnerIndex != viewedOwner || g.Alpha <= 0.01f) {
                    continue;
                }
                DrawGhostSprite(spriteBatch, form, shaderOk, g.ItemType, g.Pos, g.Rot,
                    1f, g.Alpha, form: 0f, dissolve: g.Dissolve, g.Seed);
            }
            foreach (RaiseGhost g in raises) {
                if (g.OwnerIndex != viewedOwner || g.Alpha <= 0.01f || g.State == RaiseState.Omen) {
                    continue;
                }
                DrawGhostSprite(spriteBatch, form, shaderOk, g.ItemType, g.Pos, g.Rot,
                    g.Scale, g.Alpha, form: g.Form, dissolve: 0f, g.Seed);
            }

            spriteBatch.End();
        }

        private static void DrawGhostSprite(SpriteBatch spriteBatch, Effect formEffect, bool shaderOk,
            int itemType, Vector2 pos, float rot, float scale, float alpha,
            float form, float dissolve, float seed) {

            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            Vector2 origin = frame.Size() * 0.5f;

            Color color;
            if (shaderOk) {
                formEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                formEffect.Parameters["uSeed"]?.SetValue(seed);
                formEffect.Parameters["uForm"]?.SetValue(form);
                formEffect.Parameters["uDissolve"]?.SetValue(dissolve);
                formEffect.Parameters["uScanMode"]?.SetValue(1f);
                formEffect.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                formEffect.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                formEffect.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                formEffect.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                //无着色器：染色近似——沉入越蚀越沉入深血并透明，浮出自血色渐白
                float bloodMix = MathHelper.Clamp(form + dissolve * 1.2f, 0f, 1f);
                Color body = Color.Lerp(Color.White, form > 0f ? BloodTint : BloodDeep, bloodMix);
                color = body * (alpha * (1f - dissolve));
            }

            spriteBatch.Draw(tex, pos - Main.screenPosition, frame, color,
                rot, origin, scale, SpriteEffects.None, 0f);
        }

        //==================== 杂项 ====================

        /// <summary>竖立姿态：斜置刀剑立正，横置枪械枪口朝上，其余保持原样</summary>
        private static float ComputeStandRot(int itemType) {
            if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample) || sample == null) {
                return 0f;
            }
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return 0f;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            if (sample.damage > 0 && sample.useStyle == ItemUseStyleID.Swing
                && frame.Width >= 20 && frame.Height >= 20) {
                return -MathHelper.PiOver4;
            }
            if (sample.useStyle == ItemUseStyleID.Shoot && frame.Width > frame.Height + 6) {
                return -MathHelper.PiOver2;
            }
            return 0f;
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }

        /// <summary>屏震落在观看者身上，队友的湖也震在场的人</summary>
        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        private static float SmoothStep01(float t)
            => t * t * (3f - 2f * t);
    }
}
