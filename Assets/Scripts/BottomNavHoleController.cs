using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BottomNavHoleController : MonoBehaviour
{
    [Header("底部栏，挂了挖洞材质的 Image")]
    public Image barBg;

    [Header("白色圆形选中背景 ImageRound")]
    public RectTransform imageRound;

    [Header("三个按钮位置")]
    public RectTransform homeTab;
    public RectTransform statsTab;
    public RectTransform profileTab;

    [Header("三个按钮图标，可选")]
    public Image homeIcon;
    public Image statsIcon;
    public Image profileIcon;

    [Header("图标颜色")]
    public Color normalColor = new Color(0.15f, 0.18f, 0.22f, 1f);
    public Color selectedColor = new Color(0.05f, 0.55f, 1f, 1f);

    [Header("凹槽参数")]
    public float holeRadius = 66f;
    public float holeSoftness = 4f;
    public Texture2D holeMask;
    public Vector2 holeMaskSize = new Vector2(132f, 132f);

    [Header("凹槽圆心Y偏移")]
    public float holeCenterY = 58f;

    [Header("圆形按钮Y位置")]
    public float roundY = 55f;

    [Header("动画时间")]
    public float moveDuration = 0.35f;

    [Header("ImageRound Animation")]
    public float roundMoveDuration = 0.35f;
    public Ease roundMoveEase = Ease.OutBack;
    public float roundMoveOvershoot = 1.70158f;
    public float roundScaleFrom = 0.9f;
    public float roundScaleDuration = 0.22f;
    public Ease roundScaleEase = Ease.OutBack;

    [Header("Icon Move Animation")]
    public float selectedIconY = 18f;
    public float normalIconY = 0f;
    public float iconMoveDuration = 0.22f;

    private Material runtimeMaterial;
    private Vector2 currentHoleCenter;

    private RectTransform[] tabs;
    private Image[] icons;
    private RectTransform[] iconTransforms;

    private static readonly int HoleCenterID = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleRadiusID = Shader.PropertyToID("_HoleRadius");
    private static readonly int HoleSoftnessID = Shader.PropertyToID("_HoleSoftness");
    private static readonly int HoleMaskID = Shader.PropertyToID("_HoleMask");
    private static readonly int HoleMaskSizeID = Shader.PropertyToID("_HoleMaskSize");
    private static readonly int UseHoleMaskID = Shader.PropertyToID("_UseHoleMask");

    private void Awake()
    {
        tabs = new RectTransform[]
        {
            homeTab,
            statsTab,
            profileTab
        };

        icons = new Image[]
        {
            homeIcon,
            statsIcon,
            profileIcon
        };

        iconTransforms = new RectTransform[]
        {
            ResolveIconTransform(homeIcon, homeTab),
            ResolveIconTransform(statsIcon, statsTab),
            ResolveIconTransform(profileIcon, profileTab)
        };

        runtimeMaterial = Instantiate(barBg.material);
        barBg.material = runtimeMaterial;
    }

    private void Start()
    {
        SelectTab(0, true);
    }

    public void SelectHome()
    {
        SelectTab(0, false);
    }

    public void SelectStats()
    {
        SelectTab(1, false);
    }

    public void SelectProfile()
    {
        SelectTab(2, false);
    }

    public void SelectTab(int index, bool instant = false)
    {
        if (index < 0 || index >= tabs.Length)
            return;

        RectTransform targetTab = tabs[index];

        float targetX = GetTabXInBarBg(targetTab);

        MoveHole(targetX, instant);
        MoveImageRound(targetX, instant);
        UpdateIconColor(index, instant);
        UpdateIconPosition(index, instant);
    }

    private float GetTabXInBarBg(RectTransform tab)
    {
        RectTransform barRect = barBg.rectTransform;

        Vector3 worldPos = tab.position;
        Vector3 localPos = barRect.InverseTransformPoint(worldPos);

        return localPos.x;
    }

    private void MoveHole(float targetX, bool instant)
    {
        Vector2 targetHoleCenter = new Vector2(targetX, holeCenterY);

        DOTween.Kill(this);

        if (instant)
        {
            currentHoleCenter = targetHoleCenter;
            ApplyHoleCenter(targetHoleCenter);
        }
        else
        {
            DOTween.To(
                () => currentHoleCenter,
                value =>
                {
                    currentHoleCenter = value;
                    ApplyHoleCenter(value);
                },
                targetHoleCenter,
                roundMoveDuration
            )
            .SetEase(roundMoveEase, roundMoveOvershoot)
            .SetId(this);
        }
    }

    private void ApplyHoleCenter(Vector2 center)
    {
        if (barBg == null)
            return;

        Vector3 worldCenter = barBg.rectTransform.TransformPoint(new Vector3(center.x, center.y, 0f));
        Vector3 worldRadiusPoint = barBg.rectTransform.TransformPoint(new Vector3(center.x + holeRadius, center.y, 0f));
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        Vector2 screenRadiusPoint = RectTransformUtility.WorldToScreenPoint(null, worldRadiusPoint);
        float screenRadius = Vector2.Distance(screenCenter, screenRadiusPoint);
        float screenSoftness = Mathf.Max(0.01f, holeSoftness * (screenRadius / Mathf.Max(0.01f, holeRadius)));
        Vector2 screenMaskSize = GetScreenMaskSize(center);

        Vector4 screenCenterVector = new Vector4(screenCenter.x, screenCenter.y, 0, 0);
        Vector4 screenMaskSizeVector = new Vector4(screenMaskSize.x, screenMaskSize.y, 0, 0);

        ApplyHoleMaterial(
            runtimeMaterial != null ? runtimeMaterial : barBg.material,
            barBg,
            screenCenterVector,
            screenRadius,
            screenSoftness,
            screenMaskSizeVector
        );
    }

    private void ApplyHoleMaterial(Material material, Image targetImage, Vector4 screenCenter, float screenRadius, float screenSoftness, Vector4 screenMaskSize)
    {
        if (material == null || targetImage == null)
            return;

        material.SetVector(HoleCenterID, screenCenter);
        material.SetFloat(HoleRadiusID, screenRadius);
        material.SetFloat(HoleSoftnessID, screenSoftness);
        material.SetVector(HoleMaskSizeID, screenMaskSize);
        material.SetFloat(UseHoleMaskID, holeMask != null ? 1f : 0f);

        if (holeMask != null)
        {
            material.SetTexture(HoleMaskID, holeMask);
        }

        targetImage.SetMaterialDirty();
    }

    private Vector2 GetScreenMaskSize(Vector2 center)
    {
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, holeMaskSize.x),
            Mathf.Max(0.01f, holeMaskSize.y)
        );

        RectTransform barRect = barBg.rectTransform;
        Vector3 worldLeft = barRect.TransformPoint(new Vector3(center.x - size.x * 0.5f, center.y, 0f));
        Vector3 worldRight = barRect.TransformPoint(new Vector3(center.x + size.x * 0.5f, center.y, 0f));
        Vector3 worldBottom = barRect.TransformPoint(new Vector3(center.x, center.y - size.y * 0.5f, 0f));
        Vector3 worldTop = barRect.TransformPoint(new Vector3(center.x, center.y + size.y * 0.5f, 0f));

        Vector2 screenLeft = RectTransformUtility.WorldToScreenPoint(null, worldLeft);
        Vector2 screenRight = RectTransformUtility.WorldToScreenPoint(null, worldRight);
        Vector2 screenBottom = RectTransformUtility.WorldToScreenPoint(null, worldBottom);
        Vector2 screenTop = RectTransformUtility.WorldToScreenPoint(null, worldTop);

        return new Vector2(
            Vector2.Distance(screenLeft, screenRight),
            Vector2.Distance(screenBottom, screenTop)
        );
    }

    private void OnValidate()
    {
        if (barBg != null)
        {
            float targetX = 0f;

            if (homeTab != null)
            {
                targetX = GetTabXInBarBg(homeTab);
            }

            ApplyHoleCenter(new Vector2(targetX, holeCenterY));
        }
    }

    private void MoveImageRound(float targetX, bool instant)
    {
        Vector2 targetPos = new Vector2(targetX, roundY);

        imageRound.DOKill();

        if (instant)
        {
            imageRound.anchoredPosition = targetPos;
            imageRound.localScale = Vector3.one;
        }
        else
        {
            imageRound.DOAnchorPos(targetPos, roundMoveDuration)
                .SetEase(roundMoveEase, roundMoveOvershoot);

            imageRound.localScale = Vector3.one * roundScaleFrom;
            imageRound.DOScale(1f, roundScaleDuration)
                .SetEase(roundScaleEase);
        }
    }

    private void UpdateIconColor(int selectedIndex, bool instant)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
                continue;

            icons[i].DOKill();

            Color targetColor = i == selectedIndex ? selectedColor : normalColor;

            if (instant)
            {
                icons[i].color = targetColor;
            }
            else
            {
                icons[i].DOColor(targetColor, 0.2f);
            }
        }
    }

    private RectTransform ResolveIconTransform(Image icon, RectTransform tab)
    {
        if (icon != null)
            return icon.rectTransform;

        if (tab == null)
            return null;

        Image childIcon = tab.GetComponentInChildren<Image>(true);
        return childIcon != null ? childIcon.rectTransform : null;
    }

    private void UpdateIconPosition(int selectedIndex, bool instant)
    {
        for (int i = 0; i < iconTransforms.Length; i++)
        {
            RectTransform iconTransform = iconTransforms[i];

            if (iconTransform == null)
                continue;

            iconTransform.DOKill();

            Vector2 targetPos = iconTransform.anchoredPosition;
            targetPos.y = i == selectedIndex ? selectedIconY : normalIconY;

            if (instant)
            {
                iconTransform.anchoredPosition = targetPos;
            }
            else
            {
                iconTransform.DOAnchorPos(targetPos, iconMoveDuration)
                    .SetEase(Ease.OutBack);
            }
        }
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);

        if (iconTransforms != null)
        {
            for (int i = 0; i < iconTransforms.Length; i++)
            {
                if (iconTransforms[i] != null)
                {
                    iconTransforms[i].DOKill();
                }
            }
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
