// ============================================================================
//SHPCModThermal.fx 热成像瞄具 NPC 滤镜
//采样 uImage0 NPC 贴图；heat 驱动铁红→橙黄→白炽热成像调色板 + 体表热浪扭曲
//白热锁定时追加白炽脉冲、热像仪扫描线与轮廓热溢光
//噪声输入全部为笛卡尔像素坐标+时间平移，无极坐标路径
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float heat;          //热痕 0~1
float whiteHot;      //白热锁定强度 0~1（C# 侧含淡入淡出）
float seed;          //每 NPC 随机相位
float2 texelSize;    //1/texWidth, 1/texHeight
float4 frameUV;      //当前帧 UV 界 xy=min zw=max，半像素内缩，钳制扭曲与邻域采样防串帧

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//平滑 value noise，输入为笛卡尔像素坐标
float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//热成像调色板：冷底暗紫→铁红→炽橙→熔黄→白炽
float3 thermalPalette(float t)
{
    t = saturate(t);
    float3 col = lerp(float3(0.05, 0.0, 0.10), float3(0.55, 0.04, 0.02), smoothstep(0.00, 0.30, t));
    col = lerp(col, float3(1.00, 0.42, 0.04), smoothstep(0.30, 0.58, t));
    col = lerp(col, float3(1.00, 0.86, 0.30), smoothstep(0.58, 0.82, t));
    col = lerp(col, float3(1.00, 1.00, 0.96), smoothstep(0.82, 1.00, t));
    return col;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float energy = saturate(heat + whiteHot * 0.5);
    float2 px = coords / texelSize;

    //体表热浪：向上流动的双层噪声，只做水平摆动（NPC 帧竖排，垂直偏移会跨帧）
    float flow = uTime * (26.0 + whiteHot * 22.0);
    float wob = vnoise(px * 0.11 + float2(seed * 91.0, -flow * 0.11)) - 0.5;
    float wob2 = vnoise(px * 0.23 + float2(-seed * 47.0, -flow * 0.17)) - 0.5;
    float2 distorted = coords;
    distorted.x += (wob * 3.2 + wob2 * 1.4) * texelSize.x * (0.6 + energy * 3.4);
    distorted = clamp(distorted, frameUV.xy, frameUV.zw);

    float4 src = tex2D(uImage0, distorted);

    //轮廓热溢光，邻域 alpha 差分让辉光溢出体表边缘，采样钳回帧内防读相邻动画帧
    float2 o = texelSize * 2.0;
    float nAlpha = tex2D(uImage0, clamp(distorted + float2(0, -o.y), frameUV.xy, frameUV.zw)).a
                 + tex2D(uImage0, clamp(distorted + float2(0, o.y), frameUV.xy, frameUV.zw)).a
                 + tex2D(uImage0, clamp(distorted + float2(-o.x, 0), frameUV.xy, frameUV.zw)).a
                 + tex2D(uImage0, clamp(distorted + float2(o.x, 0), frameUV.xy, frameUV.zw)).a;
    nAlpha *= 0.25;
    float rim = saturate(nAlpha - src.a * 0.6);

    if (src.a < 0.01 && rim < 0.01)
        return float4(0, 0, 0, 0);

    //亮度→温度映射：热痕越深整体越偏向调色板高段，体表噪声制造受热不均
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float bodyNoise = vnoise(px * 0.06 + float2(seed * 31.0, -flow * 0.05)) - 0.5;
    float tempT = lum * (0.50 + energy * 0.25) + energy * 0.52
        + bodyNoise * 0.22 * energy + whiteHot * 0.18;
    float3 thermal = thermalPalette(tempT);

    //低热近原色，满热完全热成像
    float blend = saturate(heat * 1.1 + whiteHot * 0.35) * 0.92;
    float3 color = lerp(src.rgb, thermal * src.a, blend);

    //白热：白炽呼吸脉冲 + 热像仪滚动扫描线
    if (whiteHot > 0.001)
    {
        float pulse = 0.5 + 0.5 * sin(uTime * 9.0 + seed * 19.0);
        color += float3(1.0, 0.97, 0.9) * whiteHot * (0.10 + 0.10 * pulse) * src.a;
        float scan = 0.5 + 0.5 * sin(px.y * 1.9 - uTime * 30.0);
        scan = pow(scan, 6.0);
        color -= scan * whiteHot * 0.10 * src.a;
    }

    //轮廓热溢光按调色板高段上色
    float rimPulse = 0.75 + 0.25 * sin(uTime * 5.0 + px.y * 0.12 + seed * 11.0);
    color += thermalPalette(0.85 + whiteHot * 0.15) * rim * (heat * 0.55 + whiteHot * 0.75) * rimPulse;

    //热度越高越自发光，白热在黑暗中也白炽可见；保留原始透明度
    float selfGlow = saturate(heat * 0.85 + whiteHot);
    float3 litRgb = lerp(vertexColor.rgb, float3(1.0, 1.0, 1.0), selfGlow);
    float alpha = max(src.a, rim * saturate(heat + whiteHot) * 0.85);
    return float4(saturate(color) * litRgb, alpha) * vertexColor.a;
}

technique Technique1
{
    pass SHPCModThermalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
