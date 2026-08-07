// ============================================================================
//NeutronPulsar.fx 脉冲星本体
//中子星不是黑洞：有硬壳、有磁层、自旋极快，本体是实心发光体而非空洞
//s0 白块由 SpriteBatch 绑定；s1 传 Extra_193(Voronoi) 当地壳板块
//Crust 走 AlphaBlend 预乘画实心壳，Field 走 Additive 叠磁层
//直线算术无动态分支，极角只经 cos(th-a) 与 sin^2 消费(见 VFX.md 极坐标接缝)
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

texture uCellTex;
sampler cellSamp = sampler_state
{
    texture = <uCellTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uSpin;        //自旋相位(弧度累积)
float uSpinRate;    //自旋角速度归一 0~1，喂多普勒与边缘增亮
float uSeed;
float uFade;
float uRadius;      //本体半径，UV 单位
float uQuake;       //地壳应力 0~1，磁制动蓄力
float uGlitch;      //星震后超频 0~1
float uMagAngle;    //磁轴当前朝向(与自旋轴错开)
float uSquash;      //飞行速度拉伸，1=静止
float uMotAngle;    //运动方向

float3 uColHot;     //简并核炽白蓝
float3 uColMain;    //中子紫
float3 uColBeam;    //磁层冷蓝
float3 uColDeep;    //壳底深蓝紫

float2 Rot(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

struct Geo
{
    float r;        //到心距离，单位=本体半径
    float th;
    float z;        //球面法线 z，边缘趋 0
    float2 surf;    //壳面采样坐标，已含引力压缩
    float axisCos;  //到磁轴夹角余弦
};

Geo Build(float2 uv)
{
    Geo g;
    float2 p = uv - 0.5;

    //飞行时沿运动方向拉长本体
    p = Rot(p, -uMotAngle);
    p.x /= max(uSquash, 0.05);
    p = Rot(p, uMotAngle);

    float rad = max(uRadius, 0.001);
    g.r = length(p) / rad;
    g.th = atan2(p.y, p.x);

    //球面法线；表面引力大到能看见背面，故越靠边缘壳面压得越密
    g.z = sqrt(saturate(1.0 - g.r * g.r));
    float comp = 1.0 / (g.z * 0.34 + 0.16);
    g.surf = Rot(p / rad, -uSpin) * comp * 0.40;

    g.axisCos = cos(g.th - uMagAngle);
    return g;
}

//---- 实心简并壳 ----
float4 PSCrust(float2 uv : TEXCOORD0) : COLOR0
{
    Geo g = Build(uv);
    float body = smoothstep(1.03, 0.95, g.r);

    //两级板块，压缩坐标让边缘细节自然堆叠
    float cellA = tex2D(cellSamp, g.surf * 1.5 + uSeed).r;
    float cellB = tex2D(cellSamp, g.surf * 4.1 - uSeed * 0.7).r;
    float plate = cellA * 0.68 + cellB * 0.32;

    //板块边界带 = 地壳裂缝
    float seam = smoothstep(0.40, 0.50, plate) * smoothstep(0.60, 0.50, plate);
    seam = saturate(seam * 2.6);

    //多普勒：迎向观者的一侧更亮，随自旋扫过
    float doppler = 1.0 + cos(g.th - uSpin) * (0.26 + uSpinRate * 0.62);

    float3 crust = lerp(uColDeep * 0.5, uColMain * 0.8, plate);
    crust *= 0.45 + g.z * 0.8;
    crust *= doppler;

    //应力越高裂缝越烧向炽白
    float heat = saturate(uQuake * 1.2 + uGlitch * 0.8);
    float3 crackCol = lerp(uColMain * 1.35, uColHot * 2.3, heat);
    crust += crackCol * seam * (0.22 + heat * 1.85);

    //边缘增亮：透镜把背面的壳挤到轮廓上
    float limb = smoothstep(0.78, 0.99, g.r) * smoothstep(1.03, 0.97, g.r);
    crust += uColHot * limb * (0.8 + uSpinRate * 1.2);

    float a = body * uFade;
    return float4(crust * a, a);
}

//---- 磁层与光子环 ----
float4 PSField(float2 uv : TEXCOORD0) : COLOR0
{
    Geo g = Build(uv);
    float3 col = float3(0, 0, 0);

    //光子环：本体外一圈细亮环
    float ring = exp(-pow((g.r - 1.42) * 9.0, 2.0));
    col += uColHot * ring * (0.8 + uGlitch * 0.85);

    //偶极磁力线 r = L*sin^2(余纬)，等 L 线即一根闭合力线
    //sin^2 与 cos 都以整数倍角消费极角，跨 0/2pi 连续
    float sinSq = 1.0 - g.axisCos * g.axisCos;
    float shell = g.r / max(sinSq, 0.06);
    //制动时磁层被压扁并缠绕
    shell *= 1.0 + uQuake * 0.8;
    shell += (tex2D(cellSamp, g.surf * 0.6 + uTime * 0.02).r - 0.5) * uQuake * 0.9;

    float lines = frac(shell * 2.1 - uTime * 0.3);
    lines = pow(saturate(1.0 - abs(lines - 0.5) * 5.0), 4.0);
    float cage = smoothstep(0.98, 1.3, g.r) * smoothstep(4.4, 2.1, g.r) * saturate(sinSq * 1.6);
    col += lerp(uColBeam, uColMain, 0.4) * lines * cage * (0.5 + uQuake * 1.4);

    //极冠：磁轴打穿壳面的两块热斑，光束的根
    float cap = pow(saturate(abs(g.axisCos)), 24.0) * smoothstep(1.10, 0.70, g.r);
    col += uColHot * cap * (1.2 + uGlitch * 2.0);

    //应力晕：制动阶段整体外溢
    float halo = exp(-pow(max(g.r - 1.0, 0.0) * 1.4, 2.0)) * uQuake;
    col += uColMain * halo * 0.45;

    return float4(col * uFade, 1.0);
}

technique Crust
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCrust();
    }
}

technique Field
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSField();
    }
}
