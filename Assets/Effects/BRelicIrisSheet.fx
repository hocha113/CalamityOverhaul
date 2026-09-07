// ============================================================================
//BRelicIrisSheet.fx 血雾之瞳·溅片（冠状溅射的一片薄血）
//SpriteBatch Immediate 单 quad：quad 中心=溅射源点，+x=溅射方向，coords→p∈[-1,1]²
//顶点色打包：R=噪声种子  G=展开 0..1  B=撕裂 0..1  A=不透明度；uniform：uSpan 扇角(rad)
//
//材质=高速液体撞进空气后甩开的薄片：不是一张膜，是"厚基 + 4~5 根液舌"的扇
//  1) 扇心在 quad 内 (-0.6,0)，可见体从 |q|=0.28 的圆弧基部起——基部有宽度，不收成一个尖；
//  2) 液舌由低频角向噪声挑出，舌间只留贴基部的薄膜（先被撕掉）；舌尖薄而透亮偏粉；
//  3) 撕裂：随 B 上推，薄膜先撕洞、洞唇加厚变暗，最后只剩几根舌——C# 同拍沿舌尖放血珠；
//  4) 扇的两侧边沿半径起伏，不是两条直线
//
//极角说明：扇心 +x、扇角 ≤150°，atan2 的 ±π 接缝落在扇外被门控归零，角向噪声在此前提下安全
//C# 侧 quad 中心 = 源点 + 方向 × 0.32 × 半幅（让基部圆弧正落在源点上）
//直线代码+纯 tex2D；预乘输出进 AlphaBlend 批
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图(magic pixel，不采样)
//噪声固定 s1：C# 侧在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

float uTime;   //秒
float uSpan;   //扇角 rad（≤2.6）

//克眼血色板(与 EocMotion 同源)，无白热
static const float3 RimDark    = float3(0.10, 0.008, 0.02);
static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);
static const float3 Highlight  = float3(0.95, 0.36, 0.34);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float seed = vc.r * 7.31;
    float spread = vc.g;
    float tear = vc.b;
    float alphaIn = vc.a;

    //---- 扇心后移：基部是一段有宽度的圆弧，不是一个尖 ----
    float2 q = p - float2(-0.6, 0.0);
    float r = length(q);
    float ang = atan2(q.y, q.x);          //扇心 +x，扇角 < π，±π 接缝在扇外
    float halfSpan = uSpan * 0.5;
    float baseR = 0.28;

    //---- 液舌：低频角向噪声挑 4~5 根舌，高频一层只做毛口 ----
    float fa = tex2D(noiseTex, float2(ang * 0.45 + seed, seed * 0.61 + uTime * 0.1)).r;
    float fb = tex2D(noiseTex, float2(ang * 1.3 + seed * 2.3, seed * 0.17 - uTime * 0.15)).r;
    float tongue = pow(saturate((fa - 0.38) / 0.40), 1.4);
    float rimR = spread * (baseR + 0.12 + 0.60 * tongue + 0.08 * (fb - 0.5));

    //---- 径向 / 角向遮罩：基部圆弧软起，扇侧沿半径起伏 ----
    float aa = 0.02;
    float inR = smoothstep(rimR + aa, rimR - aa * 2.0, r) * smoothstep(baseR - 0.06, baseR + 0.05, r);
    float side = tex2D(noiseTex, float2(r * 0.6 + seed * 1.7, seed * 0.9)).r;
    float halfEff = halfSpan * (0.78 + 0.30 * side);
    float inAng = smoothstep(halfEff + 0.05, halfEff - 0.14, abs(ang));

    //---- 撕洞：笛卡尔低频噪声；基部厚、舌身厚，舌间薄膜先破 ----
    float hole = tex2D(noiseTex, q * 0.35 + float2(seed * 0.9, uTime * 0.25 + seed)).r;
    float holeC = saturate((hole - 0.5) * 1.8 + 0.5);
    float t = saturate((r - baseR) / max(rimR - baseR, 1e-3));
    float v = holeC + (1.0 - t) * 0.35 + tongue * 0.30;
    float survive = smoothstep(tear - 0.14, tear + 0.06, v);
    float mask = inR * inAng * survive;

    //---- 体色：厚根暗 → 薄沿亮，舌尖偏粉，洞唇加厚变暗，前沿一线暗沿 ----
    float3 col = lerp(VenousDark, Arterial, smoothstep(0.05, 0.6, t));
    col = lerp(col, Bright, smoothstep(0.65, 0.95, t) * 0.5);
    col = lerp(col, Highlight, smoothstep(0.85, 1.0, t) * tongue * 0.55);
    float lip = smoothstep(tear - 0.02, tear + 0.02, v) * (1.0 - smoothstep(tear + 0.06, tear + 0.14, v));
    col = lerp(col, RimDark, lip * 0.5);
    float rimLine = 1.0 - smoothstep(0.0, 0.04, rimR - r);
    col = lerp(col, RimDark, rimLine * 0.5);

    float alpha = mask * alphaIn * lerp(0.95, 0.62, t * t);
    return float4(col * alpha, alpha) * step(0.003, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
