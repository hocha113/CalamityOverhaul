using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 不溺者的巨锚弹幕，三模式（ai[2]，随 spawn 包原子过线，出厂后不改）：
    /// 0=掷锚（直线飞行→嵌墙→绷直链线 40f 伤害线→收锚拽行沿线）；
    /// 1=上掷锚（抛物线压柱顶→坠回收招，惩罚平台龟缩）；
    /// 2=涡轨锚（锚涡期绕主人 180px 恒定轨道）。
    /// ai[1]=主人 whoAmI；ai[0]=飞行/嵌定相位（嵌定由各端对同步 tile 做确定性判定，
    /// 服务器另盖 netUpdate 章校正）。链线判定宽=可见链宽（gap 视觉同一性）。
    /// 主人离开对应状态即自毁（镜像 GaolCuffHitbox 惯例）；
    /// 全程不改写 Projectile.damage（伤害窗一律走 CanDamage/Colliding 门控）
    /// </summary>
    internal class UndrownedAnchor : UndrownedModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Anchor;

        internal const int ModeThrow = 0;
        internal const int ModeUpThrow = 1;
        internal const int ModeOrbit = 2;

        private ref float FlightPhase => ref Projectile.ai[0];
        private int OwnerIndex => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2];

        /// <summary>本地寿命计时（表现与嵌定计窗用，各端从收包起本地推进）</summary>
        private ref float Life => ref Projectile.localAI[0];
        /// <summary>嵌定后的绷线计时</summary>
        private ref float EmbedTimer => ref Projectile.localAI[1];

        private NPC OwnerNpc => OwnerIndex >= 0 && OwnerIndex < Main.maxNPCs ? Main.npc[OwnerIndex] : null;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;
            NPC boss = OwnerNpc;
            if (boss == null || !boss.active || boss.ModNPC is not Undrowned owner) {
                Projectile.Kill();
                return;
            }
            //主人离开对应状态即撤（转场公平阀的弹幕侧兜底）
            bool stateOk = Mode == ModeOrbit
                ? owner.State == Undrowned.StateWhirl
                : owner.State == Undrowned.StateAnchorThrow;
            if (!stateOk) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 120;

            if (Mode == ModeOrbit) {
                //涡轨：角度/半径全部由主人同步计时推导（锚涡状态时间线连续不重置），
                //各端一致；出手拍后 ~15f 展开到 180px 恒定
                float t = MathF.Max(0f, boss.ai[1] - Undrowned.WhirlAnchorSpawnAt);
                float radius = MathHelper.Clamp(t * 12f, 0f, Undrowned.WhirlOrbitRadius);
                float angle = t * 0.09f;
                Projectile.Center = boss.Center + angle.ToRotationVector2() * radius;
                Projectile.rotation = angle + MathHelper.PiOver2;
                Projectile.velocity = Vector2.Zero;
                if (!Main.dedServ && (int)Life % 4 == 0) {
                    SpawnRustFleck(Projectile.Center, 0.4f);
                }
                return;
            }

            if ((int)FlightPhase == 0) {
                //飞行：掷锚直线微坠 / 上掷锚满重力抛物线
                Projectile.velocity.Y += Mode == ModeUpThrow ? 0.6f : 0.12f;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                //各端对同步 tile 做确定性嵌定判定
                Point cell = Projectile.Center.ToTileCoordinates();
                if (WorldGen.InWorld(cell.X, cell.Y, 5) && WorldGen.SolidTile(cell.X, cell.Y)) {
                    FlightPhase = 1;
                    EmbedTimer = 0;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = !VaultUtils.isClient;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 6; k++) {
                            SpawnRustFleck(Projectile.Center, 0.6f);
                        }
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Circular(2.4f, 2.4f),
                            Color.Lerp(Undrowned.RustOrange, Color.White, 0.5f),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
                    }
                }
                //上掷锚落水收招：坠回水面之下即熄
                if (Mode == ModeUpThrow && Projectile.velocity.Y > 0f
                    && Projectile.Center.Y > owner.WaterSurfaceY() + 60f) {
                    Projectile.Kill();
                }
                //掷锚全程未命中：飞出 60f 自沉（主人侧对 null 走踉跄出口）
                if (Mode == ModeThrow && Life > 60f) {
                    Projectile.Kill();
                }
                return;
            }

            //嵌定：静止绷线（伤害线=静态几何，站离线即安全）
            Projectile.velocity = Vector2.Zero;
            EmbedTimer++;
            if (Mode == ModeUpThrow && EmbedTimer > 30f) {
                //柱顶锚坠回水底
                Projectile.Kill();
            }
        }

        /// <summary>伤害窗门控：飞行/嵌线/拽行照各自窗口，涡轨恒开（环上锚体本身）</summary>
        public override bool? CanDamage() {
            NPC boss = OwnerNpc;
            if (boss == null || !boss.active || boss.ModNPC is not Undrowned) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //嵌定的掷锚：锚体 + 主人到锚的绷直链线（宽=可见链宽）
            if (Mode == ModeThrow && (int)FlightPhase == 1) {
                if (projHitbox.Intersects(targetHitbox)) {
                    return true;
                }
                NPC boss = OwnerNpc;
                if (boss != null && boss.active) {
                    float _ = 0f;
                    return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        boss.Center, Projectile.Center, Undrowned.ChainLineWidth, ref _);
                }
            }
            return null;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
            for (int k = 0; k < 5; k++) {
                SpawnRustFleck(Projectile.Center, 0.5f);
            }
        }

        private static void SpawnRustFleck(Vector2 pos, float scale) {
            PRTLoader.NewParticle<PRT_RustFleck>(pos + Main.rand.NextVector2Circular(10f, 10f),
                Main.rand.NextVector2Circular(1.6f, 1.2f) - new Vector2(0f, 0.6f),
                Color.Lerp(Undrowned.RustOrange, Undrowned.RustDeep, Main.rand.NextFloat()),
                scale * Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(16, 28));
        }

        //==================== 绘制：锈锚本体（嵌定期微颤）====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Anchor);
            Texture2D tex = TextureAssets.Item[ItemID.Anchor]?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center;
            if ((int)FlightPhase == 1) {
                //嵌墙微颤：链上还挂着一整个人的力
                pos += new Vector2(MathF.Sin(EmbedTimer * 1.7f) * 1.2f, 0f);
            }
            //涡轨锚由主人画链，这里补一段短链尾增强"抡起来"的读感
            if (Mode == ModeOrbit) {
                NPC boss = OwnerNpc;
                if (boss != null && boss.active) {
                    Texture2D chainTex = TextureAssets.Chain22?.Value;
                    if (chainTex != null) {
                        Undrowned.DrawChainLine(Main.spriteBatch, chainTex, boss.Center, pos, lightColor, 1f);
                    }
                }
            }
            Undrowned.DrawAnchor(Main.spriteBatch, tex, pos, Projectile.rotation, lightColor, 1f);
            return false;
        }
    }

    /// <summary>锈屑：磕碰迸出的铁锈碎屑，重坠、微旋、尾段转暗（真 alpha 布纹贴图）</summary>
    internal class PRT_RustFleck : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 300;

        private Color initialColor;

        public PRT_RustFleck Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 20;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //铁屑重坠 + 横向阻尼
            Velocity.X *= 0.94f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.16f, 5f);
            float t = LifetimeCompletion;
            Scale *= 0.97f;
            Color = Color.Lerp(initialColor, Undrowned.RustDeep, MathF.Pow(t, 1.3f));
            Opacity = 1f - MathF.Pow(t, 2.4f);
            Rotation += Velocity.X * 0.06f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null,
                Color * Opacity, Rotation, origin, new Vector2(0.16f, 0.2f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
