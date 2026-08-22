// ============================================================================
//GolemThruster.fx 拳部推进器喷焰（火箭拳身份件）
//FlameTech：Additive 批喷焰锥，quad origin 在左端中点，+X 即喷射方向
//喷口束腰→中段鼓包→尾段收尖；噪声撕焰缘；白热芯贴喷口；马赫环节律
//s0 刻意为批次主贴图：quad 本体直接画 Perlin 噪声图（LinearWrap 批），无二次绑定
//无极角运算无动态分支，调色对齐 GolemSolarFlare（白热芯/琥珀中段/深红焰缘）
// ============================================================================

sampler noiseS : register(s0);   //刻意 s0：quad 本体即噪声贴图

float uTime;
float uPower;    //喷焰强度 0~1
float uSeed;     //每拳去相关相位
float uAspect;   //焰长/焰宽（撕边滚动频率校正）

float4 FlamePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;               //0 喷口 → 1 焰尾
    float v = coords.y * 2.0 - 1.0;   //-1~1 横截

    //锥形轮廓：喷口略束腰，前段鼓包，尾段收尖
    float bulge = sin(saturate(u * 2.6) * 3.14159) * 0.22;
    float halfW = lerp(0.62, 0.05, pow(u, 0.75)) + bulge * (1.0 - u);

    //撕边噪声：沿焰长高速滚动（排气流）
    float n = tex2D(noiseS, float2(u * (uAspect * 0.35) - uTime * 3.1, coords.y * 0.8 + uSeed)).r;
    float edge = halfW * (0.72 + 0.55 * n);
    float body = pow(saturate((edge - abs(v)) / max(edge, 1e-3)), 0.8);

    //纵向衰减：喷口最亮，尾端呼吸
    float fade = (1.0 - smoothstep(0.25, 1.0, u)) * (0.85 + 0.15 * n);

    //马赫环：沿轴亮暗节律，机关排气的人工感
    float mach = 0.82 + 0.18 * sin(u * 22.0 - uTime * 34.0 + uSeed * 9.0);

    float t = body * fade * mach;

    //白热芯：贴喷口中轴
    float core = saturate(body * 1.6 - abs(v) * 1.8) * (1.0 - smoothstep(0.0, 0.55, u));

    float3 rimCol  = float3(0.85, 0.26, 0.05);
    float3 midCol  = float3(1.00, 0.60, 0.16);
    float3 coreCol = float3(1.00, 0.96, 0.84);

    float3 col = lerp(rimCol, midCol, t);
    col = lerp(col, coreCol, core);

    float a = saturate((t + core) * uPower);
    //Additive 批源因子是 SrcAlpha：强度必须写进 A（写 0 会整段乘零画不出来），与 GolemSolarFlare 同约定
    return float4(col * vertexColor.rgb, a * vertexColor.a);
}

technique FlameTech
{
    pass FlamePass
    {
        PixelShader = compile ps_3_0 FlamePS();
    }
}
