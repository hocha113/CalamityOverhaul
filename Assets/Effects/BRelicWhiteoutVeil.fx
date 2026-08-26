// ============================================================================
//BRelicWhiteoutVeil.fx 白视风暴核·玩家侧白化风暴(全屏后效)
//与 DeerBlizzardVeil 同语系反转：清明圈以风暴主人为心，风雪向屏缘加浓；
//屏缘霜晶渐晕(噪声撕裂内缘)+双层定向雪縷+触发白闪。
//直线算术+普通tex2D，无分支(FNA3D全屏效果铁律)
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（全屏拷贝）

//噪声固定 s1：C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
//（合同同 DeerclopsVeilRender：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图）
sampler2D noiseTex : register(s1);

float uTime;
float uStorm;       //0~1 风暴强度(推荐上限0.85，保留全屏可读性)
float uPunch;       //0~1 触发白闪
float2 uCenterUV;   //清明圈心(风暴主人屏幕uv)
float uClearRadius; //清明圈半径(屏高归一)
float uAspect;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //风轴坐标系(仿射旋转，无极角)：风向略缓于boss版，读作"你的风暴"而非它的
    float2 windDir = normalize(float2(-1.0, 0.22));
    float2 p = coords * float2(uAspect, 1.0);
    float lon = dot(p, windDir);
    float lat = dot(p, float2(-windDir.y, windDir.x));

    //雪幕对画面的细微扰动折射
    float turb = tex2D(noiseTex, float2(lon * 1.9 - uTime * 1.1, lat * 1.9)).r - 0.5;
    float2 refr = float2(-windDir.y, windDir.x) * turb * 0.004 * uStorm;
    refr.x /= uAspect;
    float3 col = tex2D(uImage0, coords + refr).rgb;

    //到圈心距离：圈内清明(视野保持)，向外风暴渐浓(雪幕吞没近景)
    float2 dc = (coords - uCenterUV) * float2(uAspect, 1.0);
    float distC = length(dc);
    float storm = smoothstep(uClearRadius, uClearRadius * 2.6, distC) * uStorm;

    //双层视差雪縷：沿风向压缩采样拉成风丝(阈值压在PerlinNoise实测值域0.78内)
    float s1 = tex2D(noiseTex, float2(lon * 1.0 - uTime * 3.0, lat * 4.2)).r;
    float s2 = tex2D(noiseTex, float2(lon * 1.8 - uTime * 1.9 + 0.41, lat * 7.6 + 0.23)).r;
    float snow = smoothstep(0.56, 0.88, s1) * 0.55
               + smoothstep(0.58, 0.87, s2) * 0.33;
    snow *= storm;

    //屏缘霜晶渐晕：径向渐晕×晶体噪声，内缘被噪声撕成霜枝而非光滑圆晕
    float2 e = (coords - 0.5) * float2(uAspect, 1.0);
    float re = length(e);
    float cryst = tex2D(noiseTex, coords * 3.1 + float2(uTime * 0.014, -uTime * 0.009)).r;
    float frostEdge = smoothstep(0.46 - 0.16 * cryst, 1.08, re) * uStorm;
    frostEdge *= 0.72 + 0.28 * cryst;

    //大尺度流动冷雾(圈外)
    float fogN = tex2D(noiseTex, coords * 0.9 + float2(-uTime * 0.06, uTime * 0.015)).r;
    float haze = storm * (0.10 + 0.20 * fogN);

    float3 snowColor  = float3(0.90, 0.95, 1.0);
    float3 hazeColor  = float3(0.68, 0.77, 0.88);
    float3 frostColor = float3(0.80, 0.90, 1.0);

    //冷调渗入→冷雾→雪縷→屏缘霜晶
    col = lerp(col, col * float3(0.90, 0.96, 1.05), uStorm * 0.45);
    col = lerp(col, hazeColor, haze * 0.5);
    col = lerp(col, snowColor, saturate(snow));
    col = lerp(col, frostColor, saturate(frostEdge) * 0.85);

    //清明圈缘一圈薄冰蓝呼吸环："风雪只为你让路"的边界
    float rim = smoothstep(0.085, 0.0, abs(distC - uClearRadius)) * uStorm;
    float rimN = tex2D(noiseTex, float2(lon * 1.4 - uTime * 2.4, lat * 5.4 + 0.77)).r;
    col += float3(0.32, 0.58, 0.95) * rim * (0.30 + 0.25 * rimN);

    //触发白闪：冰蓝一瞬
    col = lerp(col, float3(0.85, 0.94, 1.0), saturate(uPunch) * 0.8);

    return float4(col, 1.0);
}

technique Technique1
{
    pass BRelicWhiteoutVeilPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
