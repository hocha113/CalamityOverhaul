// ============================================================================
//FssCorruptSkin.fx 脓蕾沙蟒变异体表（单 technique，整链 Immediate 批共用）
//暗色变异底：亮度重映射把 BSS 暖沙压向坏死紫（保留原图明暗细节）+ 病斑噪声
//湿亮流层：沿体轴缓移的湿光带（活体渗液的读数）
//灵液脉络：域扭曲单脊线成连贯金脉网，尾→头行波涌动（uPhase 链序连续，跨节不断线）
//囊肿热点：uSwell 驱动的节心金光（鼓包/充能读数）
//裂隙渗光：uCrack 驱动的皮下金光裂纹（蜕皮/怒放/濒死）
//环境光走顶点色（皮肤受光照），金色系全部自发光（黑暗里也亮 = 灵液是光源）
//预乘输出，AlphaBlend 批；uUvRect 帧钳制（Body 两帧表防串帧）
//无动态分支，噪声全走贴图采样（s1）
// ============================================================================

sampler uImage0 : register(s0);   //体节贴图
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSampler : register(s1);

float uTime;
float4 uUvRect;    //帧区域 (x, y, w, h) 归一
float uSeed;       //每节去相关种子
float uPhase;      //链序（头 0，向尾递增；脉络/湿光跨节连续的坐标基）
float uSwell;      //囊肿鼓胀 0~1
float uCrack;      //裂隙渗光 0~1
float uVein;       //脉络强度 0~1

//帧内局部 uv（0~1）
float2 LocalUV(float2 coords)
{
    return (coords - uUvRect.xy) / max(uUvRect.zw, 1e-4);
}

//PerlinNoise.png 实测值域约 0.227~0.776：归一到 0~1 再用，阈值窗口才是标称含义
float Nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

float4 FesterPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 body = tex2D(uImage0, coords);
    float2 local = LocalUV(coords);
    //链空间坐标：local.y 沿体轴（贴图前方朝下约定），加 uPhase 跨节续接
    float2 chainUV = float2(local.x * 0.9, (local.y + uPhase) * 0.5);

    //---------------- 暗色变异底 ----------------
    float lum = dot(body.rgb, float3(0.30, 0.59, 0.11));
    float3 deep = float3(0.13, 0.10, 0.19);
    float3 mid  = float3(0.31, 0.24, 0.41);
    float3 hi   = float3(0.56, 0.49, 0.45);
    float3 base = lerp(deep, mid, smoothstep(0.04, 0.50, lum));
    base = lerp(base, hi, smoothstep(0.50, 0.95, lum));
    //留一分原色，换皮不糊脸
    base = lerp(base, body.rgb, 0.16);

    //病斑：低频噪声明暗斑块（每节 uSeed 去相关）
    float blotch = Nrm(tex2D(noiseSampler, chainUV * 0.8 + uSeed * 3.7).r);
    base *= lerp(0.72, 1.05, blotch);

    //湿亮流层：沿体轴缓移的高光带（活体渗液）
    float sheenWave = sin((local.y + uPhase) * 1.8 - uTime * 1.1);
    float sheen = pow(saturate(sheenWave), 6.0) * 0.10;
    base += float3(0.62, 0.55, 0.80) * sheen;

    //---------------- 灵液脉络（自发光）----------------
    float wx = Nrm(tex2D(noiseSampler, chainUV * 1.15 + float2(uTime * 0.013, 0.0)).r);
    float wy = Nrm(tex2D(noiseSampler, chainUV * 1.15 + float2(0.37, 0.61) - float2(0.0, uTime * 0.010)).r);
    float2 warp = float2(wx, wy) - 0.5;
    float n1 = Nrm(tex2D(noiseSampler, chainUV * 2.6 + warp * 0.28).r);
    float ridge = abs(n1 - 0.5);
    float veinBody = 1.0 - smoothstep(0.010, 0.040, ridge);
    float veinCore = 1.0 - smoothstep(0.0, 0.013, ridge);
    float n2 = Nrm(tex2D(noiseSampler, chainUV * 4.2 - float2(uTime * 0.02, 0.0)).r);
    float vein = (veinBody * 0.50 + veinCore * 0.62) * (0.55 + 0.45 * n2);

    //涌动：尾→头行波（灵液向头部泵送），链序空间低频 + 节内相位
    float surge = 0.26 + 0.74 * smoothstep(0.15, 0.90, sin(uPhase * 0.55 + local.y * 0.9 - uTime * 3.2));
    float heat = saturate(vein * surge * uVein);

    //囊肿热点：节心径向金光（收窄半径），鼓胀时脉络同步增亮
    float2 c = local - 0.5;
    float hot = exp(-dot(c, c) * 30.0) * uSwell;
    heat = saturate(heat + hot * 0.35 * uVein);

    float3 amber  = float3(0.58, 0.36, 0.09);
    float3 gold   = float3(0.92, 0.72, 0.28);
    float3 bright = float3(1.00, 0.90, 0.55);
    float3 veinCol = lerp(amber, gold, saturate(heat * 1.5));
    veinCol = lerp(veinCol, bright, saturate(heat * heat * 1.8));

    //---------------- 裂隙渗光（自发光）----------------
    float cn = Nrm(tex2D(noiseSampler, chainUV * 1.6 + uSeed).r);
    float crackRidge = abs(cn - 0.5);
    float crack = (1.0 - smoothstep(0.0, 0.035 + 0.05 * uCrack, crackRidge)) * uCrack;
    float crackFlick = 0.75 + 0.25 * sin(uTime * 9.0 + uPhase * 2.0);

    //---------------- 合成（预乘输出；base 必须乘 body.a，否则透明区漏底板）----------------
    float3 glow = veinCol * heat * 0.85
        + gold * hot * 0.42
        + float3(1.0, 0.82, 0.42) * crack * crackFlick * 0.8;
    glow *= body.a;

    float3 col = base * body.a * vertexColor.rgb + glow;
    float aFinal = body.a * vertexColor.a;
    return float4(col * vertexColor.a, aFinal);
}

technique FesterTech
{
    pass FesterPass
    {
        PixelShader = compile ps_3_0 FesterPS();
    }
}
