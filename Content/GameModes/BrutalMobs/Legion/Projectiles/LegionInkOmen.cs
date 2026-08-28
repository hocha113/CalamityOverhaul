using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 血乌贼墨汁预告：跟随乌贼凝形，倒数结束沿锁定瞄向放出三发弧线血墨，
    /// 其中第 <see cref="InkGapIndex"/> 发固定跳过（节奏缺口：幽灵预览与发射循环
    /// 共用同一判定，看见空着的弹位就是安全拍）。
    /// ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，乌贼死亡或槽位复用即取消）
    /// ai[1]=锁定瞄向弧度（生成即承诺，不重瞄） ai[2]=墨弹伤害。全程无判定
    /// </summary>
    internal class LegionInkOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodShot;

        /// <summary>预告帧数（公平底线 ≥30，档位一律不缩短）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>齐射弹位总数</summary>
        internal const int InkSlots = 3;
        /// <summary>固定跳过的弹位（具名节奏缺口，发射循环真正读取）</summary>
        internal const int InkGapIndex = 1;
        /// <summary>相邻弹位的角差（弧度）</summary>
        private const float InkSpreadStep = 0.24f;
        /// <summary>墨弹出手速率</summary>
        private const float InkSpeed = 8.5f;
        /// <summary>弧线上抬量（出手时从速度里抬走，墨弹自身重力补落）</summary>
        private const float InkLobLift = 2.6f;

        private float LockedAim => Projectile.ai[1];
        private int InkDamage => (int)Projectile.ai[2];
        private int Elapsed => TelegraphFrames - Projectile.timeLeft;
        private float Charge => MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告，永不造成伤害</summary>
        public override bool? CanDamage() => false;

        /// <summary>弹位 i 的出手速度；缺口弹位返回 null（预览与发射共用，所见即所射）</summary>
        internal Vector2? SlotVelocity(int i) {
            if (i == InkGapIndex) {
                return null;
            }
            Vector2 vel = (LockedAim + (i - (InkSlots - 1) * 0.5f) * InkSpreadStep).ToRotationVector2() * InkSpeed;
            vel.Y -= InkLobLift;
            return vel;
        }

        public override void AI() {
            //来源校验 + 跟随锚定：乌贼死亡或槽位复用即取消（击杀施法者=有效反制）
            int packed = (int)Projectile.ai[0];
            int src = (packed & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != packed >> 8) {
                Cancelled = true;
            }
            else {
                Projectile.Center = Main.npc[src].Center;
            }

            //凝墨尘（≤2 粒/帧）：血珠向身前汇聚
            if (!Cancelled && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = (LockedAim + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2();
                Dust ink = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(18f, 40f),
                    DustID.Blood, -dir * Main.rand.NextFloat(1f, 2.2f), 110, default, 1f + 0.4f * Charge);
                ink.noGravity = true;
            }

            if (Projectile.timeLeft == 1 && !Cancelled) {
                if (!VaultUtils.isClient) {
                    //发射循环与预览同一 SlotVelocity：InkGapIndex 是真正被跳过的弹位
                    for (int i = 0; i < InkSlots; i++) {
                        Vector2? vel = SlotVelocity(i);
                        if (vel == null) {
                            continue;//具名节奏缺口
                        }
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                            vel.Value, ModContent.ProjectileType<LegionInkGlob>(),
                            InkDamage, 0.5f, Main.myPlayer);
                    }
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 4 },
                        Projectile.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Cancelled) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float charge = Charge;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);
            float alpha = (0.30f + 0.45f * charge) * pulse;

            //幽灵弹位预览：逐弹位画将射墨弹（原版血弹贴图），缺口弹位空出来
            for (int i = 0; i < InkSlots; i++) {
                Vector2? vel = SlotVelocity(i);
                if (vel == null) {
                    continue;
                }
                Vector2 dir = vel.Value.SafeNormalize(Vector2.UnitX);
                for (int r = 0; r < 2; r++) {
                    Vector2 pos = center + dir * (24f + 22f * r + 10f * charge);
                    float layer = r == 0 ? 1f : 0.55f;
                    //真 alpha 本体层 + 猩红描辉
                    Main.EntitySpriteDraw(tex, pos, null,
                        Color.Lerp(lightColor, Color.White, 0.25f) * (alpha * layer),
                        dir.ToRotation(), orig, 0.9f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(tex, pos, null, new Color(255, 60, 70, 0) * (0.5f * alpha * layer),
                        dir.ToRotation(), orig, 1.05f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
