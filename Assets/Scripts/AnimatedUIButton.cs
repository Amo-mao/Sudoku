using System.Collections;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public enum UIButtonAnimationMode
{
    None,
    Spine,
    LegacyAnimation
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("UI/Animated UI Button")]
public sealed class AnimatedUIButton : MonoBehaviour
{
    [SerializeField] private UIButtonAnimationMode animationMode = UIButtonAnimationMode.None;
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private UnityEngine.Animation legacyAnimation;
    [SerializeField] private string idleAnimation = "idle";
    [SerializeField] private string clickAnimation = "Click";
    [SerializeField] private bool playClickOnButtonClick = true;

    private Button button;
    private Coroutine legacyClickRoutine;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleButtonClick);
        CacheAnimationTargets();
    }

    private void OnEnable()
    {
        CacheAnimationTargets();
        PlayIdle();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClick);
        }
    }

    public void Configure(UIButtonAnimationMode mode, string idleName, string clickName)
    {
        Configure(mode, idleName, clickName, true);
    }

    public void Configure(UIButtonAnimationMode mode, string idleName, string clickName, bool playClickAutomatically)
    {
        animationMode = mode;
        idleAnimation = idleName;
        clickAnimation = clickName;
        playClickOnButtonClick = playClickAutomatically;
        CacheAnimationTargets();
        PlayIdle();
    }

    public void PlayClick()
    {
        PlayOneShot(clickAnimation);
    }

    public void PlayOneShot(string animationName)
    {
        switch (animationMode)
        {
            case UIButtonAnimationMode.Spine:
                PlaySpineOneShot(animationName);
                break;
            case UIButtonAnimationMode.LegacyAnimation:
                PlayLegacyOneShot(animationName);
                break;
        }
    }

    public void HandleButtonClick()
    {
        if (playClickOnButtonClick)
        {
            PlayClick();
        }
    }

    private void PlayIdle()
    {
        switch (animationMode)
        {
            case UIButtonAnimationMode.Spine:
                PlaySpine(idleAnimation, true);
                break;
            case UIButtonAnimationMode.LegacyAnimation:
                PlayLegacy(idleAnimation);
                break;
        }
    }

    private void PlaySpineOneShot(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            PlayIdle();
            return;
        }

        TrackEntry entry = PlaySpine(animationName, false);
        if (entry == null)
        {
            PlayIdle();
            return;
        }

        entry.Complete += _ => PlayIdle();
    }

    private TrackEntry PlaySpine(string animationName, bool loop)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return null;
        }

        if (skeletonGraphic != null)
        {
            skeletonGraphic.Initialize(false);
            if (HasSpineAnimation(skeletonGraphic.AnimationState, animationName))
            {
                return skeletonGraphic.AnimationState.SetAnimation(0, animationName, loop);
            }
        }

        if (skeletonAnimation != null)
        {
            skeletonAnimation.Initialize(false);
            if (HasSpineAnimation(skeletonAnimation.AnimationState, animationName))
            {
                return skeletonAnimation.AnimationState.SetAnimation(0, animationName, loop);
            }
        }

        return null;
    }

    private void PlayLegacyOneShot(string animationName)
    {
        if (legacyAnimation == null || string.IsNullOrEmpty(animationName))
        {
            PlayIdle();
            return;
        }

        if (legacyClickRoutine != null)
        {
            StopCoroutine(legacyClickRoutine);
        }

        PlayLegacy(animationName);
        legacyClickRoutine = StartCoroutine(ReturnToLegacyIdleAfterClick(animationName));
    }

    private IEnumerator ReturnToLegacyIdleAfterClick(string animationName)
    {
        float waitSeconds = GetLegacyClipLength(animationName);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        PlayLegacy(idleAnimation);
        legacyClickRoutine = null;
    }

    private void PlayLegacy(string animationName)
    {
        if (legacyAnimation == null || string.IsNullOrEmpty(animationName) || legacyAnimation.GetClip(animationName) == null)
        {
            return;
        }

        legacyAnimation.Stop();
        legacyAnimation.Play(animationName);
    }

    private float GetLegacyClipLength(string animationName)
    {
        AnimationClip clip = legacyAnimation != null ? legacyAnimation.GetClip(animationName) : null;
        return clip != null ? clip.length : 0f;
    }

    private void CacheAnimationTargets()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponentInChildren<SkeletonGraphic>(true);
        }

        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (legacyAnimation == null)
        {
            legacyAnimation = GetComponent<UnityEngine.Animation>();
        }

        if (legacyAnimation == null)
        {
            legacyAnimation = GetComponentInChildren<UnityEngine.Animation>(true);
        }
    }

    private static bool HasSpineAnimation(Spine.AnimationState animationState, string animationName)
    {
        return animationState?.Data?.SkeletonData?.FindAnimation(animationName) != null;
    }
}
