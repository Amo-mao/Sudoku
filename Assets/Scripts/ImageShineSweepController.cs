using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("UI/Image Shine Sweep Controller")]
public sealed class ImageShineSweepController : MonoBehaviour
{
    private const string AlphaShaderName = "Custom/UI/Image Shine Sweep";
    private const string AdditiveShaderName = "Custom/UI/Image Shine Sweep Additive";

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ShineColorId = Shader.PropertyToID("_ShineColor");
    private static readonly int ShineIntensityId = Shader.PropertyToID("_ShineIntensity");
    private static readonly int BaseVisibilityId = Shader.PropertyToID("_BaseVisibility");
    private static readonly int ShineWidthId = Shader.PropertyToID("_ShineWidth");
    private static readonly int ShineSoftnessId = Shader.PropertyToID("_ShineSoftness");
    private static readonly int ShineAngleId = Shader.PropertyToID("_ShineAngle");
    private static readonly int ShineSpeedId = Shader.PropertyToID("_ShineSpeed");
    private static readonly int ShineProgressId = Shader.PropertyToID("_ShineProgress");

    public enum BlendMode
    {
        Alpha,
        Additive
    }

    [SerializeField] private BlendMode blendMode = BlendMode.Alpha;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private Color shineColor = Color.white;
    [SerializeField, Range(0f, 5f)] private float shineIntensity = 1.5f;
    [SerializeField, Range(0f, 1f)] private float baseVisibility = 1f;
    [SerializeField, Range(0.001f, 1f)] private float shineWidth = 0.12f;
    [SerializeField, Range(0.001f, 1f)] private float shineSoftness = 0.08f;
    [SerializeField, Range(0f, 360f)] private float shineAngle = 35f;
    [SerializeField, Range(-5f, 5f)] private float shineSpeed = 0.8f;
    [SerializeField, Range(0f, 1f)] private float manualProgress;
    [SerializeField] private bool syncInLateUpdate = true;

    [SerializeField, HideInInspector] private Material originalMaterial;
    [System.NonSerialized] private Material runtimeMaterial;

    private Graphic graphic;
    private Renderer targetRenderer;
    private BlendMode lastBlendMode;
    private Color lastTint;
    private Color lastShineColor;
    private float lastShineIntensity;
    private float lastBaseVisibility;
    private float lastShineWidth;
    private float lastShineSoftness;
    private float lastShineAngle;
    private float lastShineSpeed;
    private float lastManualProgress;
    private bool hasCachedPropertyState;

    public BlendMode Mode
    {
        get => blendMode;
        set
        {
            blendMode = value;
            Apply();
        }
    }

    public float ManualProgress
    {
        get => manualProgress;
        set
        {
            manualProgress = Mathf.Repeat(value, 1f);
            ApplyProperties();
        }
    }

    private void OnEnable()
    {
        CacheTarget();
        CaptureOriginalMaterial();
        Apply();
    }

    private void OnValidate()
    {
        shineWidth = Mathf.Max(0.001f, shineWidth);
        shineSoftness = Mathf.Max(0.001f, shineSoftness);
        CacheTarget();
        Apply();
    }

    private void LateUpdate()
    {
        if (!syncInLateUpdate)
        {
            return;
        }

        if (blendMode != lastBlendMode)
        {
            Apply();
            return;
        }

        if (runtimeMaterial == null)
        {
            Apply();
            return;
        }

        if (!hasCachedPropertyState || HasPropertyChanges())
        {
            ApplyProperties();
        }
    }

    private void Reset()
    {
        CacheTarget();
        blendMode = BlendMode.Alpha;
        baseVisibility = 1f;
        Apply();
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterial();
        DestroyRuntimeMaterial();
    }

    public void Apply()
    {
        CacheTarget();

        Shader shader = Shader.Find(GetShaderName());
        if (shader == null)
        {
            Debug.LogWarning($"Shader not found: {GetShaderName()}", this);
            return;
        }

        CaptureOriginalMaterial();

        if (runtimeMaterial == null || runtimeMaterial.shader != shader)
        {
            DestroyRuntimeMaterial();
            runtimeMaterial = new Material(shader)
            {
                name = $"{GetShaderName()} Instance",
                hideFlags = HideFlags.DontSave
            };
        }

        ApplyProperties();
        AssignRuntimeMaterial();
    }

    public void ApplyProperties()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetColor(ColorId, tint);
        runtimeMaterial.SetColor(ShineColorId, shineColor);
        runtimeMaterial.SetFloat(ShineIntensityId, shineIntensity);
        runtimeMaterial.SetFloat(BaseVisibilityId, baseVisibility);
        runtimeMaterial.SetFloat(ShineWidthId, shineWidth);
        runtimeMaterial.SetFloat(ShineSoftnessId, shineSoftness);
        runtimeMaterial.SetFloat(ShineAngleId, shineAngle);
        runtimeMaterial.SetFloat(ShineSpeedId, shineSpeed);
        runtimeMaterial.SetFloat(ShineProgressId, manualProgress);

        CachePropertyState();
    }

    private string GetShaderName()
    {
        return blendMode == BlendMode.Additive ? AdditiveShaderName : AlphaShaderName;
    }

    private void CacheTarget()
    {
        if (graphic == null)
        {
            graphic = GetComponent<Graphic>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }

    private void CaptureOriginalMaterial()
    {
        if (originalMaterial != null)
        {
            return;
        }

        if (graphic != null)
        {
            originalMaterial = graphic.material;
        }
        else if (targetRenderer != null)
        {
            originalMaterial = targetRenderer.sharedMaterial;
        }
    }

    private void AssignRuntimeMaterial()
    {
        if (graphic != null)
        {
            graphic.material = runtimeMaterial;
        }
        else if (targetRenderer != null)
        {
            targetRenderer.sharedMaterial = runtimeMaterial;
        }
        else
        {
            Debug.LogWarning("ImageShineSweepController needs a UI Graphic or Renderer on the same GameObject.", this);
        }
    }

    private void RestoreOriginalMaterial()
    {
        if (graphic != null)
        {
            graphic.material = originalMaterial;
        }
        else if (targetRenderer != null)
        {
            targetRenderer.sharedMaterial = originalMaterial;
        }
    }

    private void DestroyRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }

        runtimeMaterial = null;
    }

    private bool HasPropertyChanges()
    {
        return tint != lastTint
            || shineColor != lastShineColor
            || !Mathf.Approximately(shineIntensity, lastShineIntensity)
            || !Mathf.Approximately(baseVisibility, lastBaseVisibility)
            || !Mathf.Approximately(shineWidth, lastShineWidth)
            || !Mathf.Approximately(shineSoftness, lastShineSoftness)
            || !Mathf.Approximately(shineAngle, lastShineAngle)
            || !Mathf.Approximately(shineSpeed, lastShineSpeed)
            || !Mathf.Approximately(manualProgress, lastManualProgress);
    }

    private void CachePropertyState()
    {
        lastBlendMode = blendMode;
        lastTint = tint;
        lastShineColor = shineColor;
        lastShineIntensity = shineIntensity;
        lastBaseVisibility = baseVisibility;
        lastShineWidth = shineWidth;
        lastShineSoftness = shineSoftness;
        lastShineAngle = shineAngle;
        lastShineSpeed = shineSpeed;
        lastManualProgress = manualProgress;
        hasCachedPropertyState = true;
    }
}
