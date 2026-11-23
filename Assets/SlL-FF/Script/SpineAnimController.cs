using UnityEngine;
using Spine.Unity;

public class SpineAnimController : MonoBehaviour
{
    private SkeletonGraphic skeletonGraphic;
    [SerializeField] private string animName;
    // FF: Task 9 - Configurable animation playback speed
    [SerializeField] private float speed = 1f;

    // FF: Auto play - e.g. WinBox W_Frame: plays every time the object is shown, resets when hidden.
    // SlotView turns win boxes on/off, so no extra code is needed. Off by default (cell spines use Play()).
    [Header("Auto Play")]
    [SerializeField] private bool playOnEnable = false;
    [SerializeField] private bool loopOnEnable = true;

    private bool isPlaying;
    private bool hasLoggedMissingData = false;

    public SkeletonGraphic SkeletonGraphic
    {
        get
        {
            EnsureSkeletonGraphic();
            return skeletonGraphic;
        }
    }

    void Awake()
    {
        EnsureSkeletonGraphic();
    }

    // FF: Auto play when shown (restarts from frame 0 every time)
    private void OnEnable()
    {
        if (playOnEnable) Play(loopOnEnable);
    }

    // FF: Reset when hidden. Must NOT call SetActive here (Unity forbids changing active state in OnDisable).
    private void OnDisable()
    {
        if (playOnEnable) ResetTrack();
    }

    // FF: Clears the animation and returns to setup pose, without touching active state.
    private void ResetTrack()
    {
        EnsureSkeletonGraphic();
        if (skeletonGraphic == null) return;
        if (skeletonGraphic.AnimationState != null) skeletonGraphic.AnimationState.ClearTrack(0);
        if (skeletonGraphic.Skeleton != null) skeletonGraphic.Skeleton.SetToSetupPose();
        isPlaying = false;
    }

    // FF: If animName is empty or wrong, use the first animation in the SkeletonData (W_Frame has only "animation").
    private bool ResolveAnimName()
    {
        if (skeletonGraphic == null || skeletonGraphic.SkeletonData == null) return false;
        if (!string.IsNullOrEmpty(animName) && skeletonGraphic.SkeletonData.FindAnimation(animName) != null) return true;
        var anims = skeletonGraphic.SkeletonData.Animations;
        if (anims == null || anims.Count == 0) return false;
        animName = anims.Items[0].Name;
        return true;
    }

    private void EnsureSkeletonGraphic()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
            if (skeletonGraphic == null)
            {
                skeletonGraphic = GetComponentInChildren<SkeletonGraphic>(true);
            }
        }
    }

    // FF: Task 3 - Robustness: checks if SkeletonDataAsset is present and initializes safely
    private bool CheckValidSkeletonData()
    {
        EnsureSkeletonGraphic();
        if (skeletonGraphic == null) return false;

        if (skeletonGraphic.skeletonDataAsset == null)
        {
            if (!hasLoggedMissingData)
            {
                hasLoggedMissingData = true;
                Debug.LogWarning($"[SpineAnimController] FF: SkeletonDataAsset missing on {gameObject.name}");
            }
            return false;
        }

        if (skeletonGraphic.SkeletonData == null)
        {
            skeletonGraphic.Initialize(false);
            if (skeletonGraphic.SkeletonData == null)
            {
                if (!hasLoggedMissingData)
                {
                    hasLoggedMissingData = true;
                    Debug.LogWarning($"[SpineAnimController] FF: SkeletonData failed to initialize on {gameObject.name}");
                }
                return false;
            }
        }

        return true;
    }

    // FF: Task 3 - Play(bool loop) restarts from frame 0 even if already playing
    internal void Play(bool loop)
    {
        if (!CheckValidSkeletonData()) return;
        if (!ResolveAnimName()) return;   // FF: fallback to first animation

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        skeletonGraphic.freeze = false;

        if (skeletonGraphic.AnimationState != null)
        {
            var track = skeletonGraphic.AnimationState.SetAnimation(0, animName, loop);
            if (track != null)
            {
                track.TimeScale = speed > 0f ? speed : 1f;
                track.TrackTime = 0f;
            }
            if (skeletonGraphic.Skeleton != null)
            {
                skeletonGraphic.Skeleton.SetToSetupPose();
            }
            skeletonGraphic.Update(0f);
        }
        isPlaying = true;
    }

    // FF: Task 9 & Task 3 - Play N loops then invoke callback
    internal void Play(int loops, System.Action onComplete)
    {
        if (!CheckValidSkeletonData())
        {
            onComplete?.Invoke();
            return;
        }

        if (ResolveAnimName() && loops > 0)   // FF: fallback to first animation
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            skeletonGraphic.freeze = false;
            if (skeletonGraphic.AnimationState != null)
            {
                var track = skeletonGraphic.AnimationState.SetAnimation(0, animName, true);
                if (track != null)
                {
                    track.TimeScale = speed > 0f ? speed : 1f;
                    track.TrackTime = 0f;
                    if (skeletonGraphic.Skeleton != null)
                    {
                        skeletonGraphic.Skeleton.SetToSetupPose();
                    }
                    skeletonGraphic.Update(0f);

                    int completedLoops = 0;
                    Spine.AnimationState.TrackEntryDelegate handler = null;
                    handler = entry =>
                    {
                        completedLoops++;
                        if (completedLoops >= loops)
                        {
                            entry.Complete -= handler;
                            onComplete?.Invoke();
                        }
                    };
                    track.Complete += handler;
                }
                else
                {
                    onComplete?.Invoke();
                }
            }
            else
            {
                onComplete?.Invoke();
            }
            isPlaying = true;
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    internal void Pause()
    {
        if (!CheckValidSkeletonData()) return;
        skeletonGraphic.freeze = true;
    }

    internal void Resume()
    {
        if (!CheckValidSkeletonData()) return;
        if (!isPlaying && !string.IsNullOrEmpty(animName))
        {
            Play(true);
        }
        skeletonGraphic.freeze = false;
    }

    // FF: Task 3 - Stop() is safe when called on an inactive object; clears track and sets setup pose
    internal void Stop()
    {
        EnsureSkeletonGraphic();
        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.AnimationState != null)
            {
                skeletonGraphic.AnimationState.ClearTrack(0);
            }
            if (skeletonGraphic.Skeleton != null)
            {
                skeletonGraphic.Skeleton.SetToSetupPose();
            }
        }
        isPlaying = false;
        // FF: auto-play objects (WinBox) are shown/hidden by SlotView - don't hide them here
        if (!playOnEnable && gameObject != null && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    // FF: Task 9 & Task 3 - GetAnimationDuration safely checks data
    internal float GetAnimationDuration()
    {
        if (!CheckValidSkeletonData()) return 0f;

        if (skeletonGraphic.SkeletonData != null && !string.IsNullOrEmpty(animName))
        {
            var anim = skeletonGraphic.SkeletonData.FindAnimation(animName);
            if (anim != null)
            {
                float currentSpeed = speed > 0f ? speed : 1f;
                return anim.Duration / currentSpeed;
            }
        }
        return 0f;
    }

    internal void SetSkeletonData(SkeletonDataAsset skeletonDataAsset, string overrideAnimName = null)
    {
        EnsureSkeletonGraphic();

        if (skeletonGraphic != null)
        {
            hasLoggedMissingData = false;
            string oldAnimName = animName;
            if (overrideAnimName != null)
            {
                animName = overrideAnimName;
            }

            if (skeletonGraphic.skeletonDataAsset != skeletonDataAsset)
            {
                skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
                skeletonGraphic.Initialize(true);
                isPlaying = false;
            }
            
            // Check if the current animName is valid in the new skeleton data.
            // If it is not found, we fall back to the first animation in the new skeleton data.
            if (skeletonGraphic.SkeletonData != null)
            {
                if (string.IsNullOrEmpty(animName) || skeletonGraphic.SkeletonData.FindAnimation(animName) == null)
                {
                    var animations = skeletonGraphic.SkeletonData.Animations;
                    if (animations.Count > 0)
                    {
                        animName = animations.Items[0].Name;
                    }
                    else
                    {
                        animName = "";
                    }
                }
            }

            if (animName != oldAnimName)
            {
                isPlaying = false;
            }
        }
    }
}
