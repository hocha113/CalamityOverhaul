// ============================================================================
//MLordOrb.fx 月总幻影星球"微型蚀月"(MLordOrbProj 专用)
//蚀盘暗面(真 alpha 遮挡剪影+幻影斑纹缓移) + 旋涡虹膜(差速旋转拧臂,能量透出暗面)
//+ 冕环新月相(贴缘亮环向 uCrescentDir 偏亮,冕丝散逸) + 凝聚侵蚀成形 uForm
//
//球体契约:球盘占画布半径 0.42(冕晕余量),C# 折算 quadPx = 可见半径px / 0.42 * 2
//混合契约:AlphaBlend 预乘输出;盘体 A≈0.96 遮挡背景(契约4暗层),盘外冕晕 A≈0 纯加光
//拉伸契约:uStretchDir/uStretch 只变形采样坐标,quad 不旋转(消费端保证 dir 归一)
//极角审计:无 atan2/经纬——旋涡=旋转笛卡尔,冕丝=单位向量喂平铺噪声,角向仅 dot 连续量
//阈值实测:PerlinNoise 值域上界 ~0.776,所有 smoothstep 上沿 ≤0.75
//采样器:显式 register(FishronStormSky 事故);s1 由消费端 Textures[1]+LinearWrap 绑定
// ============================================================================

sampler uImage0 : register(s0);   //画布(SpriteBatch 主贴图,本 shader 不采样)
sampler uNoise : register(s1);    //平铺 Perlin

float uTime;          //含逐弹相位种子
float uAlpha;         //整体透明度
float uForm;          //凝聚包络 0~1:阈值侵蚀成形,<1 时盘面缺蚀、冕先行
float uFlash;         //放飞点火 0~1:全盘向月白过曝(消费端只给 ≤2 帧高值)
float uSpin;          //虹膜旋涡角速度
float2 uStretchDir;   //速度方向(单位向量,屏幕系;零速传 (0,1))
float uStretch;       //速度拉伸量 0~0.55
float2 uCrescentDir;  //新月相方位(亮缘朝向,单位向量)
float3 uColDark;      //蚀盘暗鞘
float3 uColDeep;      //深紫
float3 uColMain;      //幻影青
float3 uColBright;    //月白

//球盘画布半径,C# 侧同名常量必须同步
static const float DiscR = 0.42;

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    //速度拉伸:沿运动向分解,坐标反向压缩=形体顺向拉长、横向微收
    float2 dir = uStretchDir;
    float along = dot(p, dir);
    float across = dot(p, float2(-dir.y, dir.x));
    float2 q = float2(along / (1.0 + uStretch), across / max(1.0 - uStretch * 0.40, 0.30)) / DiscR;
    float rq = length(q);
    if (rq > 1.62) {
        return float4(0, 0, 0, 0);   //冕晕已衰减到不可见,边界保险
    }

    float r2 = dot(q, q);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    float inDisc = 1.0 - smoothstep(0.985, 1.015, rq);
    float2 unit = rq > 0.001 ? q / rq : float2(0.0, 1.0);

    //---- 凝聚侵蚀:低频形骸噪声做阈值,uForm 扫过即成形;侵蚀锋亮缘 ----
    //阈值行程 0.14~0.86 盖满 Perlin 实测值域(~0.2..0.776),uForm=0.35 时仅 ~1/3 成形
    float formN = noise(q * 0.9 + uTime * 0.013 + 7.3);
    float formTh = lerp(0.14, 0.86, uForm);
    float solid = (1.0 - smoothstep(formTh - 0.07, formTh + 0.07, formN)) * smoothstep(0.0, 0.12, uForm);
    float formEdge = solid * (1.0 - solid) * 4.0;

    //---- 蚀盘暗面:暗鞘底色 + 双尺度幻影斑纹缓移 + 缘部微提亮 ----
    float mottle = noise(q * 1.4 + float2(uTime * 0.021, -uTime * 0.017));
    float mottle2 = noise(q * 3.1 + float2(-uTime * 0.013, uTime * 0.024) + 5.1);
    float3 face = lerp(uColDark, uColDeep * 0.55, mottle * 0.45 + mottle2 * 0.25);
    face = lerp(face, uColDeep * 0.85, pow(1.0 - N.z, 2.2) * 0.50);

    //---- 旋涡虹膜:差速旋转拧出旋臂,向心增强,幻影能量透出暗面 ----
    float swirlAng = (1.30 - rq) * 2.4 + uTime * uSpin;
    float sc = cos(swirlAng);
    float ss = sin(swirlAng);
    float2 v = float2(q.x * sc - q.y * ss, q.x * ss + q.y * sc);
    float arm = noise(v * 1.5 + uTime * float2(0.020, 0.014) + 2.7);
    float armMask = smoothstep(0.42, 0.72, arm);
    float irisW = 1.0 - smoothstep(0.15, 0.85, rq);
    float core = exp(-rq * rq * 7.0);

    //---- 相位明灭:盘内呼吸涟漪(连续 rq 相位,无角向) ----
    float phase = 0.82 + 0.18 * sin(uTime * 2.2 + rq * 3.0);

    //暗面主导:旋臂与芯只作透光,不许把内里点成白热(蚀月不是亮涡球)
    float3 body = face;
    body += lerp(uColDeep, uColMain, armMask) * (armMask * irisW * 0.36 * phase);
    body += uColMain * core * 0.45;
    body += uColMain * formEdge * 0.90;

    //---- 冕环:贴缘亮环,新月相偏亮,冕丝噪声撕散;凝聚期冕先行(能量壳先聚) ----
    float crescent = 0.30 + 0.70 * saturate(dot(unit, uCrescentDir) * 0.5 + 0.5);
    float rim = exp(-abs(rq - 1.0) * 10.0);
    float streamerN = noise(unit * 1.9 + rq * 1.1 - uTime * 0.05);
    float coronaBand = rim * (0.55 + 0.45 * streamerN) * crescent;
    float coronaGate = lerp(0.55, 1.0, uForm);
    float3 corona = lerp(uColMain, uColBright, 0.35) * coronaBand * coronaGate;

    //盘外冕晕:纯加光不遮挡
    float halo = exp(-max(rq - 1.0, 0.0) * 5.0) * smoothstep(0.985, 1.10, rq)
        * (0.40 + 0.60 * streamerN) * crescent * coronaGate;

    //---- 点火过曝:全盘向月白猛提,冕同步爆亮 ----
    body = lerp(body, uColBright, uFlash * 0.85);

    //---- 预乘合成 ----
    float bodyA = inDisc * 0.96 * solid;
    float3 C = body * bodyA;
    C += corona * (0.70 + 0.30 * phase);
    C += lerp(uColMain, uColBright, 0.4) * halo * 0.60;
    C += uColBright * uFlash * (rim + halo) * 0.80;
    float A = bodyA + halo * 0.08 + coronaBand * coronaGate * 0.10;

    return float4(C, saturate(A)) * uAlpha * vertexColor;
}

technique TechEclipse
{
    pass EclipsePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
