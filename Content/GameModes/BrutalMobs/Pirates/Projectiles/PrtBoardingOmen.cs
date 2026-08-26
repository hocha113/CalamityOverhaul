using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 荷兰飞船·跳帮号令预兆：ai[0]=飞船NPC索引 ai[1]=档位。<br/>
    /// 战旗在船桅上徐徐升起（预告实体，零伤害），升满后由服务端遴选最近的地面船员为旗手、
    /// 生成 <see cref="PrtBannerMark"/> 开启短脉冲提速；周围没有船员则号令落空，什么都不发生
    /// </summary>
    internal class PrtBoardingOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>升旗预告帧数（小Boss 签名技 ≥40 帧契约）</summary>
        internal const int TelegraphFrames = 48;
        /// <summary>旗手遴选半径（以飞船为圆心）</summary>
        internal const float BearerSearchRange = 1000f;
        /// <summary>旗杆升起高度（相对船体上沿）</summary>
        private const float FlagRise = 78f;

        private static readonly Color BannerRed = new Color(178, 34, 40);
        private static readonly Color TrimGold = new Color(255, 210, 110);

        private int ShipIndex => (int)Projectile.ai[0];
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private int Age => TelegraphFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不判定</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f && !Main.dedServ) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            }

            if (!(ShipIndex.TryGetNPC(out NPC ship) && ship.type == NPCID.PirateShip)) {
                //飞船没了：号令不会发生，战旗消散
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
                return;
            }

            //锚定船桅（含 gfxOffY 补偿；飞船悬浮时 gfxOffY 常为 0，纪律性保留）
            Projectile.Center = ship.Center + new Vector2(0f, ship.gfxOffY - ship.height / 2f - 20f);

            //升旗途中的金屑（≤2 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                float rise = MathHelper.Clamp(Age / (float)TelegraphFrames, 0f, 1f) * FlagRise;
                Dust glint = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -rise),
                    DustID.GoldFlame, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), 110, default, 0.9f);
                glint.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, TrimGold.ToVector3() * 0.2f);

            if (Projectile.timeLeft == 1 && !VaultUtils.isClient) {
                RallyCrew(ship);
            }
        }

        /// <summary>旗升满：遴选离船最近的合格地面船员为旗手，开启短脉冲；无人应旗则号令落空</summary>
        private void RallyCrew(NPC ship) {
            int bearerIndex = -1;
            float bestDistSq = BearerSearchRange * BearerSearchRange;
            foreach (NPC other in Main.ActiveNPCs) {
                if (!PrtPirateSets.IsGroundCrew(other.type) || other.SpawnedFromStatue
                    || other.boss || other.realLife >= 0 || other.dontTakeDamage) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(other.Center, ship.Center);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    bearerIndex = other.whoAmI;
                }
            }
            if (bearerIndex < 0) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                Main.npc[bearerIndex].Top, Vector2.Zero,
                ModContent.ProjectileType<PrtBannerMark>(), 0, 0f, Main.myPlayer,
                bearerIndex, PrtBannerMark.Pack(Main.npc[bearerIndex].type, Tier), ShipIndex);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D cloth = CWRAsset.Extra_98.Value;
            Vector2 basePos = Projectile.Center - Main.screenPosition;

            float rise = MathHelper.Clamp(Age / (float)TelegraphFrames, 0f, 1f);
            float fadeIn = MathHelper.Clamp(Age / 8f, 0f, 1f);
            float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.identity) * 0.12f;

            //旗杆：竖直细线自船桅向上生长
            float poleLen = FlagRise * rise + 12f;
            Main.EntitySpriteDraw(line, basePos, null,
                TrimGold with { A = 0 } * (0.5f * fadeIn),
                -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                new Vector2(poleLen / line.Width, 5f / line.Height), SpriteEffects.None, 0);

            //旗面：真 alpha 布面随升旗展开，摆动传达"号令正在酝酿"
            Vector2 flagPos = basePos + new Vector2(10f, -poleLen + 12f);
            Main.EntitySpriteDraw(cloth, flagPos, null,
                Color.Lerp(BannerRed, lightColor, 0.2f) * (0.85f * fadeIn),
                wave, new Vector2(0f, cloth.Height / 2f),
                new Vector2(0.34f * (0.4f + 0.6f * rise), 0.2f * rise + 0.03f), SpriteEffects.None, 0);
            //旗面金边挂光
            Main.EntitySpriteDraw(cloth, flagPos, null,
                (TrimGold with { A = 0 }) * (0.3f * fadeIn * rise),
                wave, new Vector2(0f, cloth.Height / 2f),
                new Vector2(0.36f * (0.4f + 0.6f * rise), 0.22f * rise + 0.03f), SpriteEffects.None, 0);
            return false;
        }
    }
}
