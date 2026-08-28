// ============================================================================
//SeaShrimpJet.fx 渊晶海虾 高压水柱
//材质=高压海水射流:暗鞘承体、泡沫亮芯(水不是能量,禁白热常驻)、
//团块湍流沿管推进、双缘异相撕沫、沿程垂坠(水柱会沉,这是它区别于激光的签名)
//
//TechJet 口吐射流(横向 quad,px 空间):
//  三端具名物理答案——源头=口部球根隆起+淡起埋进头壳;
//  落点(uImpact=1)=末端外扩溅开+落点亮斑+回溅碎水带;
//  打空(uImpact=0)=远端泄压渐窄+噪声颈缩断成滴串,散尽于画布内。禁平切
//  画布契约:C# quad 总长 uQuadLenPx = uLenPx + 末端散逸余量(≥120px),
//  总高 uQuadHPx = 峰值满宽×2.6 + 2×|uSagPx|;1 uv = quad px,折算在消费端具名注释
//  uSagPx 已按弹道角折算好符号(近水平射流有效,俯仰 ≤0.35rad)
//
//TechGeyser 间歇泉柱(竖直 quad,v=1 为地面):
//  根部球根隆起(底埋地下,禁水平实切)、纵向 sqrt 坐标=重力减速拉伸签名、
//  顶冠噪声撕裂+顶上飞散水屑、uLife 尾段撕裂消散
//
//整文件 ps-only(SpriteBatch 家族),禁加带 VS 的 technique(混批污染案 2026-08-27)
//uniform 上禁 if:端部两种模式全算后按 uImpact 权重混合
//噪声 s1=PerlinNoise LinearWrap;G 通道实测值域 0.227~0.776,阈值一律过 nrm()
// ============================================================================

float uTime;
float uSeed;
float fadeAlpha;
//TechJet
float uQuadLenPx;
float uQuadHPx;
float uLenPx;
float uWidthPx;
float uSagPx;
float uImpact;
//TechGeyser
float uQuadWPx;
float uHeightPx;
float uGeyserWPx;
float uLife;

sampler noiseSamp : register(s1);

//PerlinNoise G 通道实测 0.227~0.776,先归一再做阈值,防死代码
float nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

struct SBInput
{
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

//深渊水体色板(与 SeaShrimpVFX 同族)
static const float3 cDeep = float3(0.030, 0.075, 0.140);
static const float3 cBody = float3(0.075, 0.210, 0.360);
static const float3 cGlow = float3(0.290, 0.760, 0.860);
static const float3 cFoam = float3(0.740, 0.910, 0.965);

//=========================================================
//TechJet 口吐高压射流
//=========================================================
float4 JetPS(SBInput input) : COLOR0
{
    float x = input.TexCoords.x * uQuadLenPx;          //沿柱 px,0=口部
    float yq = (input.TexCoords.y - 0.5) * uQuadHPx;   //横向 px,0=quad 轴

    float len = max(uLenPx, 1.0);
    float x01 = saturate(x / len);

    //打空尾段进度(颈缩带):先算,尾段垂坠要用
    float neckT = smoothstep(len * 0.78, uQuadLenPx - 8.0, x) * (1.0 - uImpact);

    //轴线垂坠:二次垂坠,液体签名;断滴尾段坠得更快(滴串在掉)
    float axisY = uSagPx * x01 * x01 * (1.0 + neckT * 0.9);
    float dy = yq - axisY;
    float sideSign = dy >= 0.0 ? 1.0 : -1.0;

    //噪声:双频沿管高速滚动(高压速度感),上下缘异相
    float n1 = nrm(tex2D(noiseSamp, float2(x * 0.004 - uTime * 2.6, dy * 0.011 + uSeed)).g);
    float n2 = nrm(tex2D(noiseSamp, float2(x * 0.009 - uTime * 4.2, sideSign * 0.37 + uSeed * 2.3 + x01 * 0.4)).g);

    //端部宽度改形:打中=末端外扩溅开;打空=远端泄压渐窄(两式全算按 uImpact 混合)
    float flare = smoothstep(len - 46.0, len, x) * uImpact;
    float taper = 1.0 - 0.55 * smoothstep(len * 0.78, len * 1.02, x) * (1.0 - uImpact);
    //沿程宽度:喷口略窄→16%处满宽→远端缓泄压;根部球根隆起(禁平切源头)
    float bulge = exp(-x / 22.0) * 0.55;
    float wProfile = (0.62 + 0.38 * smoothstep(0.0, 0.16, x01)) * (1.0 - 0.10 * x01) + bulge;
    float halfW = 0.5 * uWidthPx * wProfile * (1.0 + flare * 0.85) * taper;

    float r = abs(dy) / max(halfW, 1.0);
    float edge = r + (n2 - 0.5) * 0.42;

    //暗鞘(遮挡承体)/管体/泡沫芯
    float body = 1.0 - smoothstep(0.62, 1.05, edge);
    float sheath = smoothstep(0.34, 0.66, edge) * (1.0 - smoothstep(0.80, 1.06, edge));
    float core = 1.0 - smoothstep(0.0, 0.34, r);
    //团块湍流:浓淡团沿管推进
    float chunk = 0.45 + 0.55 * n1;
    float coreFoam = core * (0.50 + 0.50 * nrm(tex2D(noiseSamp, float2(x * 0.006 - uTime * 5.0, 0.13 + uSeed)).g));

    //体外飞沫窄带:1.05~1.7 倍半宽的稀疏水屑
    float sprayBand = smoothstep(1.02, 1.12, r) * (1.0 - smoothstep(1.35, 1.75, r));
    float outerSpray = sprayBand * step(0.72, n2) * (0.4 + 0.6 * x01);

    //源头淡起:球根承形,口内 10px 内敛(quad 起点由 C# 埋进头壳)
    float rootIn = smoothstep(-4.0, 10.0, x);

    //落点收口(uImpact=1):柱止于落点,落点亮斑+回溅碎水带(至 +34px 内散尽)
    float impactCap = 1.0 - smoothstep(len + 2.0, len + 30.0, x);
    float hitGlow = exp(-pow((x - len) / 24.0, 2.0)) * uImpact * saturate(halfW / 12.0);
    float backSpray = smoothstep(len - 8.0, len + 4.0, x) * (1.0 - smoothstep(len + 6.0, len + 34.0, x))
                    * step(0.55, n2) * uImpact;

    //打空收口(uImpact=0):0.78L 起噪声颈缩,断成清晰滴串(窄窗阈值=块状存活),散尽于画布内
    float neckN = nrm(tex2D(noiseSamp, float2(x * 0.016 - uTime * 3.4, uSeed * 3.1)).g);
    float survive = smoothstep(neckT - 0.03, neckT + 0.06, neckN);
    float lenCap = lerp(survive, impactCap, uImpact);

    //quad 边缘保险(内容应在此之前自然归零)
    float yGuard = 1.0 - smoothstep(0.44, 0.5, abs(input.TexCoords.y - 0.5));

    float aSheath = sheath * 0.95;
    float aBody = body * (0.68 + 0.30 * chunk);
    float aCore = coreFoam * 0.55;
    float aSpray = outerSpray * 0.4 + backSpray * 0.5;

    float3 col = cDeep * aSheath
               + lerp(cBody, cGlow, 0.30 + 0.38 * chunk) * aBody
               + cFoam * aCore
               + cFoam * hitGlow * 0.9
               + cBody * aSpray;

    float alpha = saturate(aSheath + aBody * 0.9 + aCore * 0.6 + aSpray + hitGlow * 0.5);
    float gate = rootIn * lenCap * yGuard * fadeAlpha;
    return float4(col * gate, alpha * gate) * input.Color;
}

//=========================================================
//TechGeyser 间歇泉柱
//=========================================================
float4 GeyserPS(SBInput input) : COLOR0
{
    float h = (1.0 - input.TexCoords.y) * uQuadHPx;    //离地高度 px
    float hx = (input.TexCoords.x - 0.5) * uQuadWPx;   //横向 px
    float height = max(uHeightPx, 1.0);
    float h01 = saturate(h / height);

    //重力签名:纵向坐标过 sqrt——底部流速快纹理拉长,顶部减速纹理密
    float ny = sqrt(max(h, 0.0)) * 0.055 - uTime * 2.2;
    float n1 = nrm(tex2D(noiseSamp, float2(hx * 0.010 + uSeed, ny)).g);
    float n2 = nrm(tex2D(noiseSamp, float2(hx * 0.021 + uSeed * 2.7, ny * 1.9 + 0.4)).g);

    //宽度剖面:根部球根隆起(禁水平实切),沿高收窄
    float rootBulb = exp(-h / 26.0) * 0.65;
    float wProf = (0.58 + 0.42 * smoothstep(0.0, 0.10, h01)) * (1.0 - 0.34 * h01) + rootBulb;
    float halfW = 0.5 * uGeyserWPx * wProf;

    float r = abs(hx) / max(halfW, 1.0);
    float edge = r + (n2 - 0.5) * 0.5;

    float body = 1.0 - smoothstep(0.50, 1.05, edge);
    float sheath = smoothstep(0.28, 0.60, edge) * (1.0 - smoothstep(0.78, 1.05, edge));
    float core = 1.0 - smoothstep(0.0, 0.32, r);
    float chunk = 0.5 + 0.5 * n1;

    //顶冠:柱顶 20% 噪声撕裂散冠,冠上 0~40px 飞散水屑带
    float crownT = smoothstep(height * 0.80, height * 1.02, h);
    float crownSurvive = smoothstep(crownT - 0.06, crownT + 0.16, n1);
    float overTop = smoothstep(height - 4.0, height + 6.0, h)
                  * (1.0 - smoothstep(height + 10.0, height + 42.0, h));
    float topSpray = overTop * step(0.62, n2);

    //根部淡起(底埋地下 8px,由 C# 保证)
    float rootIn = smoothstep(-6.0, 6.0, h);

    //收场:uLife 尾段整体撕裂消散(不是原地淡出——噪声先蚀边)
    float fadeT = smoothstep(0.74, 1.0, uLife);
    float dissolve = smoothstep(fadeT - 0.1, fadeT + 0.2, n1 * 0.7 + 0.3);

    float xGuard = 1.0 - smoothstep(0.44, 0.5, abs(input.TexCoords.x - 0.5));

    float aSheath = sheath * 0.85;
    float aBody = body * (0.55 + 0.28 * chunk);
    float aCore = core * (0.42 + 0.25 * chunk);
    float aSpray = topSpray * 0.55;

    float3 col = cDeep * aSheath
               + lerp(cBody, cGlow, 0.28 + 0.4 * chunk) * aBody
               + cFoam * aCore * 0.8
               + cFoam * aSpray;

    float alpha = saturate(aSheath + aBody * 0.9 + aCore * 0.55 + aSpray);
    float gate = rootIn * crownSurvive * dissolve * xGuard * fadeAlpha;
    return float4(col * gate, alpha * gate) * input.Color;
}

technique TechJet
{
    pass P0
    {
        PixelShader = compile ps_3_0 JetPS();
    }
}

technique TechGeyser
{
    pass P0
    {
        PixelShader = compile ps_3_0 GeyserPS();
    }
}
