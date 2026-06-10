Shader "UI/Circle Hole Bar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _HoleCenter ("Hole Center", Vector) = (0, 0, 0, 0)
        _HoleRadius ("Hole Radius", Float) = 66
        _HoleSoftness ("Hole Softness", Float) = 4
        _HoleMask ("Hole Mask", 2D) = "black" {}
        _HoleMaskSize ("Hole Mask Size", Vector) = (132, 132, 0, 0)
        _UseHoleMask ("Use Hole Mask", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _HoleMask;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _HoleCenter;
            float4 _HoleMaskSize;
            float _HoleRadius;
            float _HoleSoftness;
            float _UseHoleMask;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                OUT.screenPosition = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                float2 screenPos = IN.screenPosition.xy / IN.screenPosition.w * _ScreenParams.xy;
                float holeMask;

                if (_UseHoleMask > 0.5)
                {
                    float2 maskUv = (screenPos - _HoleCenter.xy) / _HoleMaskSize.xy + 0.5;
                    float insideMaskBounds = step(0, maskUv.x) * step(maskUv.x, 1) * step(0, maskUv.y) * step(maskUv.y, 1);
                    float cutAlpha = tex2D(_HoleMask, maskUv).a * insideMaskBounds;
                    holeMask = 1 - cutAlpha;
                }
                else
                {
                    float dist = distance(screenPos, _HoleCenter.xy);
                    holeMask = smoothstep(_HoleRadius, _HoleRadius + _HoleSoftness, dist);
                }

                color.a *= holeMask;

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
