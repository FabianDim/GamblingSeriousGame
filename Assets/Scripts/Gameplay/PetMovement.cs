using System.Collections;
using GamblingSeriousGame.Pets;
using GamblingSeriousGame.UI;
using UnityEngine;

public class PetMovement : MonoBehaviour
{
    private SelectedPet selectedPet;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameplayScreen gameplayScreen;
    private PetAnimationDefinition currentAnimation;
    private PetAnimationDefinition[] animationSequence = System.Array.Empty<PetAnimationDefinition>();
    // -1 means no sequence is currently playing.
    private int sequenceIndex = -1;
    void OnEnable()
    {
        if (gameStateManager == null) gameStateManager = FindAnyObjectByType<GameStateManager>();
        selectedPet = null;
        animationSequence = System.Array.Empty<PetAnimationDefinition>();
        sequenceIndex = -1;
        if (gameplayScreen == null) gameplayScreen = FindAnyObjectByType<GameplayScreen>();
        StartCoroutine(PlayAnimations());
    }
    private PetAnimationDefinition SelectActivityAnimation()
    {
        var pet = gameStateManager != null ? gameStateManager.SelectedPet : null;
        if (pet != selectedPet)
        {
            selectedPet = pet;
            animationSequence = CreateRunJumpRunSequence();
            sequenceIndex = -1;
        }

        if (sequenceIndex >= 0)
            return animationSequence[sequenceIndex];

        var activities = selectedPet?.Animations.Activities;
        if (activities == null || activities.Count == 0) return null;

        // The entire sequence is one extra choice alongside individual activities.
        int choiceCount = activities.Count + (animationSequence.Length > 0 ? 1 : 0);
        int choice = UnityEngine.Random.Range(0, choiceCount);
        if (choice == activities.Count)
        {
            sequenceIndex = 0;
            return animationSequence[sequenceIndex];
        }
        return activities[choice];
    }

    private PetAnimationDefinition[] CreateRunJumpRunSequence()
    {
        var activities = selectedPet?.Animations.Activities;
        PetAnimationDefinition run = null;
        PetAnimationDefinition jump = null;
        if (activities != null)
        {
            foreach (var activity in activities)
            {
                if (activity.name == "Running") run = activity;
                else if (activity.name == "Jump") jump = activity;
            }
        }

        // Keep the existing fallback for breeds without both animations.
        if (run == null || jump == null)
            return System.Array.Empty<PetAnimationDefinition>();

        return new[] { run, jump, run };
    }

    private IEnumerator PlayAnimations()
    {
        while (true)
        {
            currentAnimation = SelectActivityAnimation();

            if (gameplayScreen == null ||
                !gameplayScreen.PlayPetAnimation(currentAnimation))
            {
                // Retry later if the screen or pet isn't ready.
                yield return new WaitForSeconds(1);
                continue;
            }

            if (currentAnimation.loop)
            {
                yield return new WaitForSeconds(Random.Range(10f, 40f));
            }
            else
            {
                // Match the preview's FPS limit and use real time like its UI scheduler.
                float fps = Mathf.Clamp(currentAnimation.framesPerSecond, 1f, 30f);
                float duration = currentAnimation.frameRects.Length / fps;
                yield return new WaitForSecondsRealtime(duration);
            }

            if (sequenceIndex >= 0)
            {
                sequenceIndex++;
                if (sequenceIndex == animationSequence.Length)
                    sequenceIndex = -1;
            }
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
