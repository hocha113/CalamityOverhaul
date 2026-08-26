// ============================================================================
//BRelicThroatTongue.fx 饕餮之喉·血肉巨舌(顶点带)
//UV.x 0喉根→1舌尖 UV.y 横截面；材质=活体肌舌：暗湿肉鞘+充血热芯+蠕动鼓包
//+像素域肌节环带+毛细血管缘+湿高光；根部生长包络埋进喉口、尖端噪声撕散。
//uFlow 为累计蠕动相位(C#按阶段正负推进：出舌向尖、回卷向根)，禁匀速观感由其非线性驱动。
//全程无 atan2/极角，接缝免疫由构造保证。
//顶点带 transformMatrix，输出预乘alpha，C#配 BlendState.AlphaBlend(暗鞘真正遮挡背景)
// ============================================================================

float4x4 transformMatrix;
float uTime;
float seed;         //实例种子
float fadeAlpha;    //整体透明度 0~1
float uQuadLen;     //当前舌长 px(像素域包络用)
float uFlow;        //蠕动累计相位(方向随阶段反转)
float uEngorge;     //蠕胀幅度 0~1(吞咽口内脉冲抬升)
float uTaut;        //绷紧度 0松弛湿摆 ~ 1紧绷高频微颤
float uTipErode;    //尖端撕散度 0~1(收舌时抬升)

// 噪声固定 s1：C# 侧在 pass.Apply 前显式 Textures[1]=PerlinNoise + LinearWrap
sampler noiseSamp : register(s1);

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //0 喉根 → 1 舌尖
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //=== 尖端撕散：噪声扰动的软切，收舌时撕口加深加碎 ===
    float tear = tex2D(noiseSamp, float2(along * 3.0 - uTime * 1.8, seed + 0.23)).r - 0.5;
    float tipEdge = 0.965 - 0.12 * uTipErode;
    float tipMask = smoothstep(1.015, tipEdge, along + tear * (0.05 + 0.30 * uTipErode));
    if (tipMask * fadeAlpha < 0.003)
    {
        return float4(0, 0, 0, 0);
    }

    //=== 主轴：松弛湿摆(慢而黏) + 紧绷高频微颤，两端钉住 ===
    float pin = smoothstep(0.0, 0.10, along) * smoothstep(1.0, 0.88, along);
    float wob = tex2D(noiseSamp, float2(along * 1.4 - uTime * 0.55, seed)).r - 0.5;
    float tremble = tex2D(noiseSamp, float2(along * 6.0 + uTime * 7.0, seed + 0.5)).r - 0.5;
    float axis = (wob * 2.0 * ((1.0 - uTaut) * 0.34 + 0.04)
                + tremble * 2.0 * uTaut * 0.05) * pin;

    //=== 宽度：根粗→中细→尖前肉锤微鼓，蠕动波沿程行进鼓包 ===
    float taper = lerp(0.95, 0.60, smoothstep(0.0, 0.45, along));
    taper += 0.16 * exp(-pow((along - 0.86) * 7.0, 2.0));
    //蠕动鼓包：相位由 C# 累计推进，方向即吞咽方向
    float wavePos = frac(along * 2.6 - uFlow * 0.35);
    float pulse = exp(-pow((wavePos - 0.5) * 3.4, 2.0));
    taper *= 1.0 + pulse * uEngorge * 0.42;
    taper = min(taper, 1.0);

    float d = abs(cross_ - axis) / max(taper, 0.05);

    //=== 剖面层次：暗湿肉鞘(遮挡体) / 肉体 / 充血热芯 ===
    float sheath = exp(-d * d * 8.0);
    float meat = exp(-d * d * 24.0);
    float core = exp(-d * d * 80.0);

    //=== 像素域肌节环带：随舌长自然自根生出，不随伸缩滑移 ===
    float ringMod = 1.0 + sin(along * uQuadLen / 22.0 + wob * 3.0) * 0.10;

    //=== 毛细血管缘：两条细丝贴核缘蜿蜒，噪声门断续 ===
    float cap1 = tex2D(noiseSamp, float2(along * 3.4 + uTime * 0.8, seed + 0.37)).g - 0.5;
    float dCap1 = abs(cross_ - (0.36 + cap1 * 0.26) * taper) / max(taper, 0.05);
    float cap2 = tex2D(noiseSamp, float2(along * 3.0 - uTime * 1.0, seed + 0.71)).g - 0.5;
    float dCap2 = abs(cross_ + (0.38 + cap2 * 0.24) * taper) / max(taper, 0.05);
    float capGate = smoothstep(0.30, 0.52, tex2D(noiseSamp, float2(along * 1.1 + uTime * 0.5, seed + 0.53)).r);
    float capillary = (exp(-dCap1 * dCap1 * 650.0) + exp(-dCap2 * dCap2 * 650.0)) * 0.7 * capGate;

    //=== 湿高光：偏轴细亮线，噪声门闪烁(拒绝均匀塑料高光) ===
    float dSpec = abs(cross_ - axis + 0.30 * taper) / max(taper, 0.05);
    float specGate = smoothstep(0.34, 0.60, tex2D(noiseSamp, float2(along * 2.2 - uTime * 1.3, seed + 0.83)).r);
    float spec = exp(-dSpec * dSpec * 420.0) * specGate;

    //=== 端部包络 ===
    //根部生长段：前 42px 埋进喉口涡内，切边永不暴露
    float rootGrow = smoothstep(0.0, 42.0, along * uQuadLen);
    //根部入喉变暗
    float rootShade = lerp(0.55, 1.0, smoothstep(0.0, 0.18, along));
    float edgeMask = smoothstep(1.0, 0.82, abs(cross_));

    //=== 调色：分层 lerp 防白爆，蠕胀波峰充血增亮 ===
    float3 cDark = float3(0.13, 0.015, 0.022);
    float3 cMeat = float3(0.50, 0.070, 0.075);
    float3 cHot  = float3(0.90, 0.140, 0.100);
    float3 cWet  = float3(1.00, 0.620, 0.550);
    float3 cCap  = float3(0.95, 0.260, 0.180);

    float3 col = cDark;
    col = lerp(col, cMeat, saturate(meat * 1.15));
    col = lerp(col, cHot, saturate(core * (0.50 + pulse * uEngorge * 0.9)));
    col += cCap * capillary;
    col += cWet * spec * 0.85;
    col *= ringMod * rootShade;

    float mask = tipMask * rootGrow * edgeMask;
    float alpha = saturate(sheath * 0.92 + core * 0.18) * mask * fadeAlpha;
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass BRelicThroatTonguePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
