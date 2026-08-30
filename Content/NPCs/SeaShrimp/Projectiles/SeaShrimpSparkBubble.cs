using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 带电小泡：雷泡崩爆的散射子代 + 泡球连拍的待拍泡（同一材质语言，三种来历）。
    /// 散射：衰减漂浮、按 ai[0] 错帧起爆；待拍：ai[0]≥500 无害悬浮，被状态拍飞时权威端
    /// 重写 velocity 与起爆龄；飞行：速度门内本体有伤，龄到起爆。
    /// 起爆时经权威端链注册表与上一爆点连电弧（链沿爆序/玩家走位铺开）。
    /// ai[0]=起爆龄，ai[1]=链 id（<see cref="MakeChainId"/> 每次攻击实例独立，负值不连弧），
    /// ai[2]=泡半径
    /// </summary>
    internal class SeaShrimpSparkBubble : SeaShrimpModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>
        /// 链 id 编码：boss whoAmI × 1000 + 攻击序号（每次出招一条独立链，跨招绝不串联；
        /// whoAmI&lt;200 时值域 &lt;21 万，float 精确承载）
        /// </summary>
        internal static int MakeChainId(int who, int attackSeq) => who * 1000 + (attackSeq % 1000 + 1000) % 1000;

        /// <summary>从链 id 解码 boss whoAmI</summary>
        internal static int ChainOwner(int chainId) => chainId / 1000;

        /// <summary>起爆后的余帧（冲击环外扩+消散）</summary>
        private const int AfterFrames = 10;
        /// <summary>伤害窗帧数：波前推进段</summary>
        private const int DamageFrames = 6;
        /// <summary>冲击环最终可见半径 = 爆缩半径 × 此系数</summary>
        private const float RingOvershoot = 1.35f;
        /// <summary>待拍模式的起爆龄下限（判别标记）</summary>
        internal const int HeldBurstAge = 500;
        /// <summary>
        /// 飞行本体伤害与"被拍飞"判别的速度门 px/f。
        /// 必须严格高于散射初速上限（SparkScatterSpeed+3.2+0.8≈16）且低于拍飞速度（30）——
        /// 越过它散射泡会被误判成飞行泡（不衰减直飞+带伤）
        /// </summary>
        private const float FlightSpeedGate = 18f;

        private int BurstAge => (int)Projectile.ai[0];
        private int ChainId => (int)Projectile.ai[1];
        private float Radius => Projectile.ai[2];

        /// <summary>本地帧龄：逐端计数，迟入端不重播预告</summary>
        private int Age => (int)Projectile.localAI[0];
        private bool Bursting => Age >= BurstAge;
        private bool Held => BurstAge >= HeldBurstAge;

        /// <summary>
        /// 链弧注册表（仅权威端消费）：boss whoAmI → 最近一个爆点与其帧戳。
        /// 70f 过期——短于两次攻击的最小间隔，链不跨招串联
        /// </summary>
        private static readonly Dictionary<int, (Vector2 Pos, uint Time)> ChainRegistry = new();
        private const uint ChainExpireFrames = 70;

        /// <summary>登记一个爆点为链头（雷泡爆心调用：链从大爆炸处长出来，仅权威端）</summary>
        internal static void RegisterBurst(int chainId, Vector2 pos) {
            if (chainId < 0) {
                return;
            }
            ChainRegistry[chainId] = (pos, Main.GameUpdateCount);
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 700;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();
            int age = Age;

            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                //一次性起爆闩：对本地计数偏差也稳（迟入端/被拍改写起爆龄）
                if (Projectile.localAI[1] == 0f) {
                    Projectile.localAI[1] = 1f;
                    OnBurst();
                }
                if (age >= BurstAge + AfterFrames) {
                    Projectile.Kill();
                }
                return;
            }

            //待拍泡：主人不在泡球招内（被全局转移打断）→ 无主泡快速消散
            if (Held) {
                if (!ChainOwnerBatting()) {
                    Projectile.Kill();
                    return;
                }
                //悬浮呼吸：identity 定相位的确定性微漂
                float phase = Projectile.identity * 1.13f;
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + phase) * 0.02f;
            }
            else if (Projectile.velocity.Length() > FlightSpeedGate) {
                //飞行段：撞地即提前起爆（各端确定性输入；+1 让下一帧的起爆帧判等命中）
                if (ShrimpTerrain.SolidAt(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * Radius)) {
                    Projectile.ai[0] = age + 1;
                    Projectile.velocity = Vector2.Zero;
                }
            }
            else {
                //散射段：缓阻尼漂远（积分距离 ≈ 初速×28.6，先飞出崩爆环再链爆）+ 微上浮
                Projectile.velocity *= 0.965f;
                Projectile.velocity.Y -= 0.02f;
            }

            float lum = 0.3f + 0.4f * ChargeToBurst();
            Lighting.AddLight(Projectile.Center, 0.1f * lum, 0.22f * lum, 0.4f * lum);

            //带电颤火花：临爆更密（本地表现）
            if (!Main.dedServ && Main.rand.NextFloat() < 0.08f + 0.25f * ChargeToBurst()) {
                Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Radius * 0.8f;
                PRTLoader.NewParticle<PRT_AbyssSpark>(rim, Main.rand.NextVector2Circular(1.2f, 1.2f),
                    SeaShrimpBubbleArc.ArcColor, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(8);
            }
        }

        /// <summary>临爆度 0~1（起爆前 12f 内升满，膜面绷紧的可读预告）</summary>
        private float ChargeToBurst() {
            if (Held) {
                return 0f;
            }
            return MathHelper.Clamp(1f - (BurstAge - Age) / 12f, 0f, 1f);
        }

        /// <summary>待拍泡的主人是否仍在泡球招内（各端从同步的 ai[3] 自行判定）</summary>
        private bool ChainOwnerBatting() {
            int who = ChainOwner(ChainId);
            if (who < 0 || who >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[who];
            return npc.active && npc.ModNPC is SeaShrimpBoss
                && (int)npc.ai[3] == (int)SeaShrimpStateIndex.BubbleBat;
        }

        /// <summary>起爆帧：白闪 + 冲击波前 + 链弧 + 散水团（表现本地，链弧仅权威端）</summary>
        private void OnBurst() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.55f, Pitch = 0.45f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 6f,
                        dir * Main.rand.NextFloat(2.5f, 6f),
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22), 1.6f);
                }
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                        Main.rand.NextVector2Circular(4f, 4f),
                        SeaShrimpVFX.Glow, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(10);
                }
                if (Main.LocalPlayer != null
                    && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 900f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(2.5f);
                }
            }

            //链弧：与注册表里上一爆点连线，然后登记本爆点（仅权威端，电弧生成包广播；
            //链 id=boss whoAmI，槽位 0 也合法，无链用负值表示）
            if (VaultUtils.isClient || ChainId < 0) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (ChainRegistry.TryGetValue(ChainId, out var last) && now - last.Time <= ChainExpireFrames) {
                Vector2 mid = (last.Pos + Projectile.Center) * 0.5f;
                Vector2 half = (Projectile.Center - last.Pos) * 0.5f;
                int damage = Projectile.damage;
                int who = ChainOwner(ChainId);
                NPC owner = who >= 0 && who < Main.maxNPCs ? Main.npc[who] : null;
                if (owner != null && owner.active) {
                    damage = SeaShrimpDirector.ScaleProjectileDamage(owner, SeaShrimpDirector.BubbleArcDamage);
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mid, half,
                    ModContent.ProjectileType<SeaShrimpBubbleArc>(), damage, 1f, Main.myPlayer);
            }
            ChainRegistry[ChainId] = (Projectile.Center, now);

            //顺手清过期项：注册表恒小
            List<int> stale = null;
            foreach (var kv in ChainRegistry) {
                if (now - kv.Value.Time > ChainExpireFrames * 4) {
                    (stale ??= new List<int>(2)).Add(kv.Key);
                }
            }
            if (stale != null) {
                foreach (int key in stale) {
                    ChainRegistry.Remove(key);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f),
                    Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1f, 2.6f), 0.8f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), SeaShrimpVFX.Body * 0.4f, Main.rand.NextFloat(0.35f, 0.55f))
                ?.Configure(Main.rand.Next(26, 40));
        }

        /// <summary>伤害窗：爆缩波前 6f；飞行段速度门内本体有伤；待拍/漂浮/预告皆无害</summary>
        public override bool? CanDamage() {
            int age = Age;
            if (Bursting) {
                return age < BurstAge + DamageFrames ? null : false;
            }
            return !Held && Projectile.velocity.Length() > FlightSpeedGate ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            if (Bursting) {
                //波前推进判定：逐帧对齐可见冲击环，封顶爆缩半径
                float progress = MathHelper.Clamp((Age - BurstAge) / (float)AfterFrames, 0f, 1f);
                float shockR = MathF.Min(SeaShrimpDirector.SparkBlastRadius,
                    SeaShrimpVFX.CollapseRingRadius(SeaShrimpDirector.SparkBlastRadius * RingOvershoot, progress));
                return Vector2.Distance(nearest, Projectile.Center) <= shockR;
            }
            return Vector2.Distance(nearest, Projectile.Center) <= Radius;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            if (Bursting) {
                body = default;
                return false;
            }
            float charge = ChargeToBurst();
            float speed = Projectile.velocity.Length();
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = Radius * MathHelper.Clamp(Age / 6f, 0.25f, 1f),
                //带电颤：比水泡抖得凶，临爆绷紧
                Wobble = 0.55f + 0.35f * charge + MathHelper.Clamp(speed / 30f, 0f, 0.3f),
                Arm = MathHelper.Clamp(0.3f + charge * 0.7f, 0f, 1f),
                Burst = 0f,
                Fade = MathHelper.Clamp(Age / 5f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            if (Bursting) {
                //小型崩爆：海虾冲击环 + 电蓝闪点
                float progress = MathHelper.Clamp((age - BurstAge) / (float)AfterFrames, 0f, 1f);
                if (SeaShrimpVFX.CollapsePathReady) {
                    SeaShrimpVFX.DrawCollapse(Projectile.Center, SeaShrimpDirector.SparkBlastRadius * RingOvershoot,
                        progress, Projectile.identity * 0.37f, 1f);
                }
                Texture2D glowTex = CWRAsset.SoftGlow?.Value;
                float fade = 1f - progress;
                if (glowTex != null && fade > 0f) {
                    Vector2 pos = Projectile.Center - Main.screenPosition;
                    Main.spriteBatch.Draw(glowTex, pos, null, new Color(255, 255, 255, 0) * (0.7f * fade), 0f,
                        glowTex.Size() * 0.5f, SeaShrimpDirector.SparkBlastRadius * 1.1f / glowTex.Width * 2f * fade,
                        SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(glowTex, pos, null,
                        SeaShrimpBubbleArc.ArcColor with { A = 0 } * (0.6f * fade), 0f,
                        glowTex.Size() * 0.5f, SeaShrimpDirector.SparkBlastRadius * 1.9f / glowTex.Width * 2f,
                        SpriteEffects.None, 0f);
                }
                return false;
            }

            //泡体由批绘层接管，这里补带电核心辉（电泡与水泡的一眼区分）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float flicker = 0.55f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f + Projectile.identity * 1.7f);
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    SeaShrimpBubbleArc.ArcColor with { A = 0 } * (flicker * (0.35f + 0.4f * ChargeToBurst())), 0f,
                    glow.Size() * 0.5f, Radius * 0.8f / glow.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
