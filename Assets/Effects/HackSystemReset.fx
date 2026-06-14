// ============================================================================
//HackSystemReset.fx 骇客时间系统重置
//采样 uImage0 NPC 贴图
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float progress;    //重启进度 0(刚关机)→1(即将恢复)
float intensity;
float2 texelSize;

float hash(float n)
{
    return frac(sin(n) * 43758.5453);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 texColor = tex2D(uImage0, coords);
    if (texColor.a < 0.01) return texColor;

    //==== 关机去饱和 ====
    float gray = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
    //从彩色渐变到几乎纯灰（保留微量蓝色调）
    float desatAmount = intensity * 0.85;
    float3 desatColor = lerp(texColor.rgb, float3(gray * 0.7, gray * 0.75, gray * 0.9), desatAmount);

    //==== 整体降亮（断电变暗）====
    float dimFactor = 1.0 - intensity * 0.5;
    //重启后期逐渐恢复亮度
    dimFactor = lerp(dimFactor, 1.0, smoothstep(0.7, 1.0, progress) * 0.4);
    desatColor *= dimFactor;

    //==== 水平扫描行（像CRT关机） ====
    float scanFreq = 1.0 / (texelSize.y * 3.0); //每3像素一条
    float scan = sin(coords.y * scanFreq * 3.14159) * 0.5 + 0.5;
    scan = smoothstep(0.4, 0.6, scan);
    desatColor *= 1.0 - scan * intensity * 0.12;

    //==== 蓝色数据流竖线 ====
    float colIdx = floor(coords.x / (texelSize.x * 8.0));
    float flowSpeed = hash(colIdx * 127.1) * 2.0 + 1.0;
    float flowPhase = frac(coords.y * 5.0 - uTime * flowSpeed + hash(colIdx * 311.7));
    float dataLine = smoothstep(0.0, 0.05, flowPhase) * smoothstep(0.15, 0.10, flowPhase);
    //只在部分列显示
    float colActive = step(0.7, hash(colIdx + floor(uTime * 2.0)));
    float3 dataColor = float3(0.15, 0.35, 0.8);
    desatColor += dataColor * dataLine * colActive * intensity * 0.25;

    //==== 进度条（底部向上填充）====
    float barHeight = 0.03;
    float barY = 1.0 - barHeight;
    float barFill = progress;
    if (coords.y > barY)
    {
        float barUV = (coords.y - barY) / barHeight;
        if (coords.x < barFill)
        {
            desatColor = lerp(desatColor, float3(0.2, 0.5, 1.0), intensity * 0.4);
        }
    }

    //==== 重启闪烁（最后阶段） ====
    if (progress > 0.85)
    {
        float flicker = sin(uTime * 30.0) * 0.5 + 0.5;
        flicker = pow(flicker, 8.0);
        float restoreT = (progress - 0.85) / 0.15;
        desatColor = lerp(desatColor, texColor.rgb, restoreT * 0.6);
        desatColor += float3(0.3, 0.5, 1.0) * flicker * restoreT * intensity * 0.3;
    }

    //==== 边缘蓝色描边 ====
    float a_r = tex2D(uImage0, coords + float2(texelSize.x * 2.0, 0)).a;
    float a_l = tex2D(uImage0, coords - float2(texelSize.x * 2.0, 0)).a;
    float a_u = tex2D(uImage0, coords + float2(0, texelSize.y * 2.0)).a;
    float a_d = tex2D(uImage0, coords - float2(0, texelSize.y * 2.0)).a;
    float edge = saturate(abs(texColor.a - a_r) + abs(texColor.a - a_l) + abs(texColor.a - a_u) + abs(texColor.a - a_d));
    desatColor += float3(0.1, 0.3, 0.9) * edge * intensity * 0.8;

    return float4(desatColor, texColor.a);
}

technique Technique1
{
    pass HackSystemResetPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
