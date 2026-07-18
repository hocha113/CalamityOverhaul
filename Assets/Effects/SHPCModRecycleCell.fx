// ============================================================================
//SHPCModRecycleCell.fx 高效握把：单枚回收能量晶胞（菱形晶格）
//画布为方形 quad，UV 中心为晶胞中心；s0 白色画布 s1 噪声
//纯笛卡尔 SDF 构图（L1 菱形 + length 核心），无 atan2/极坐标，无接缝风险
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;     //整体透明度 0~1（星环展开/单胞显隐）
float fill;          //0 空胞 → 1 凝聚完成（凝聚进度液面）
float flash;         //本胞凝聚成型/汇聚放电白闪 0~1
float primedPulse;   //零损待发全环脉冲 0~1
float cellRot;       //晶胞自转弧度
float3 mainColor;    //主色（青柠能源绿）
float3 coreColor;    //核心亮色（鎏金）

//L1 范数菱形 SDF：abs 折叠，笛卡尔连续
float diamondSDF(float2 p, float r)
{
    p = abs(p);
    return (p.x + p.y - r) * 0.7071;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //-1..1 局部坐标；q 保持未旋转（凝聚液面始终水平），p 随晶胞自转（刚性旋转无接缝）
    float2 q = coords * 2.0 - 1.0;
    float cs = cos(cellRot);
    float sn = sin(cellRot);
    float2 p = float2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);

    float d = diamondSDF(p, 0.72);

    //描边与内部遮罩
    float edge = 1.0 - smoothstep(0.0, 0.10, abs(d));
    float inside = 1.0 - smoothstep(-0.02, 0.02, d);

    //凝聚液面：能量自底部涨起（q.y 连续标量），液面微晃
    float level = lerp(0.88, -0.88, fill) + 0.04 * sin(uTime * 2.6);
    float liquid = inside * smoothstep(level + 0.12, level - 0.12, q.y);

    //内部能量流：噪声滚动（笛卡尔输入 + frac 仅喂 tex2D）
    float2 nuv = q * 0.85 + float2(uTime * 0.06, -uTime * 0.24);
    float energy = tex2D(noiseSamp, frac(nuv)).r;
    //SDF 等距轮廓波纹：d 是连续标量场，sin(d) 无缝
    float ripple = 0.5 + 0.5 * sin(d * 24.0 + uTime * 3.0);
    energy = energy * 0.65 + ripple * 0.35;

    //核心亮点（length 径向连续）
    float core = 1.0 - smoothstep(0.0, 0.5, length(p));

    //零损待发：全胞呼吸
    float breathe = 1.0 + primedPulse * 0.4 * sin(uTime * 8.0);

    float3 col = float3(0.0, 0.0, 0.0);
    float lum = 0.0;

    //描边：空胞暗描边打底，凝聚/待发增亮
    float edgeGain = lerp(0.35, 1.1, max(fill, primedPulse * 0.7)) * breathe;
    col += mainColor * edge * edgeGain;
    lum += edge * edgeGain * 0.5;

    //凝聚液体：能量流滚动
    col += mainColor * liquid * energy * 0.85 * breathe;
    lum += liquid * (0.28 + energy * 0.34);

    //鎏金核心：凝聚完成后点亮
    col += coreColor * core * fill * (0.45 + 0.25 * sin(uTime * 4.0 + cellRot)) * breathe;
    lum += core * fill * 0.35;

    //待发鎏金泛光：整胞染金
    col = lerp(col, coreColor * 1.3, primedPulse * 0.35 * inside * (0.5 + 0.5 * sin(uTime * 8.0)));

    //空胞内部微弱底色，保证"这里有一个待凝晶胞"的可读性
    col += mainColor * inside * (1.0 - fill) * 0.05;
    lum += inside * (1.0 - fill) * 0.04;

    //白闪：凝聚成型/汇聚放电
    float burst = saturate(flash);
    col = lerp(col, float3(1.7, 1.85, 1.3), burst * max(inside, edge));
    lum += burst * max(inside, edge);

    float alpha = saturate(lum) * fadeAlpha;
    return float4(col * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModRecycleCellPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
